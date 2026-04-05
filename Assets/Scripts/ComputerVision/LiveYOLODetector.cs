using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;

public class LiveYOLODetector : MonoBehaviour
{
    public Camera mainCamera;
    public string serverUrl = "http://127.0.0.1:8000/detect";
    const int imageWidth = 4000;
    const int imageHeight = 4303;

    [Header("Crop")]
    [SerializeField] RectTransform canvasRect; // Canvas의 RectTransform 연결

    RenderTexture camRT;
    Texture2D screenTex;
    Texture2D croppedTex;

    public event System.Action<List<Detection>> OnDetections;
    bool canDetect = false;
    bool isDetecting = false;

    void Start()
    {
        camRT = new RenderTexture(imageWidth, imageHeight, 24);
        screenTex = new Texture2D(imageWidth, imageHeight, TextureFormat.RGB24, false);
    }

    void Update()
    {
        if (!canDetect) return;
        if (isDetecting) return;
        StartCoroutine(CaptureAndSend());
    }

    IEnumerator CaptureAndSend()
    {
        isDetecting = true;

        int originalMask = mainCamera.cullingMask;
        int uiLayer = LayerMask.NameToLayer("UI");
        mainCamera.cullingMask &= ~(1 << uiLayer);

        var prevRT = mainCamera.targetTexture;
        mainCamera.targetTexture = camRT;
        mainCamera.Render();

        RenderTexture.active = camRT;
        screenTex.ReadPixels(new Rect(0, 0, imageWidth, imageHeight), 0, 0);
        screenTex.Apply();
        RenderTexture.active = null;

        mainCamera.targetTexture = prevRT;
        mainCamera.cullingMask = originalMask;

        // Canvas 기준으로 크롭 영역 계산
        int cx, cy, cw, ch;
        if (canvasRect != null)
        {
            // Canvas의 월드 코너 4개를 스크린 좌표로 변환
            Vector3[] corners = new Vector3[4];
            canvasRect.GetWorldCorners(corners);

            // corners 순서: 0=좌하, 1=좌상, 2=우상, 3=우하
            Vector2 screenMin = mainCamera.WorldToScreenPoint(corners[0]);
            Vector2 screenMax = mainCamera.WorldToScreenPoint(corners[2]);

            // 렌더 텍스처 기준으로 스케일
            float scaleX = (float)imageWidth / Screen.width;
            float scaleY = (float)imageHeight / Screen.height;

            cx = Mathf.Clamp(Mathf.RoundToInt(screenMin.x * scaleX), 0, imageWidth);
            cy = Mathf.Clamp(Mathf.RoundToInt(screenMin.y * scaleY), 0, imageHeight);
            cw = Mathf.Clamp(Mathf.RoundToInt((screenMax.x - screenMin.x) * scaleX), 1, imageWidth - cx);
            ch = Mathf.Clamp(Mathf.RoundToInt((screenMax.y - screenMin.y) * scaleY), 1, imageHeight - cy);
        }
        else
        {
            // canvasRect 없으면 전체 이미지 사용
            cx = 0; cy = 0; cw = imageWidth; ch = imageHeight;
        }

        // 크롭된 픽셀 추출 (Y 반전)
        Color[] pixels = screenTex.GetPixels(cx, cy, cw, ch);

        if (croppedTex == null || croppedTex.width != cw || croppedTex.height != ch)
        {
            if (croppedTex != null) Destroy(croppedTex);
            croppedTex = new Texture2D(cw, ch, TextureFormat.RGB24, false);
        }

        croppedTex.SetPixels(pixels);
        croppedTex.Apply();

        byte[] jpg = croppedTex.EncodeToJPG(80);

        string url = $"{serverUrl}?offset_x={cx}&offset_y={cy}&orig_w={imageWidth}&orig_h={imageHeight}";

        UnityWebRequest req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(jpg);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/octet-stream");

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            var json = req.downloadHandler.text;
            var result = JsonUtility.FromJson<DetectionResult>(json);
            OnDetections?.Invoke(result.detections);
        }
        else
        {
            Debug.LogError(req.error);
        }

        isDetecting = false;
    }

    public void EnableDetection(bool enable)
    {
        canDetect = enable;
    }

    void OnDestroy()
    {
        if (camRT != null) camRT.Release();
        if (croppedTex != null) Destroy(croppedTex);
    }
}

[System.Serializable]
public class DetectionResult
{
    public List<Detection> detections;
}

[System.Serializable]
public class Detection
{
    public float cx, cy, w, h;
    public string label;
}