using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;

public class LiveYOLODetector : MonoBehaviour
{
    public Camera mainCamera;
    public string serverUrl = "http://127.0.0.1:8000/detect";
    const int imageWidth = 1600;
    const int imageHeight = 900;

    [Header("Crop")]
    [SerializeField][Range(0f, 0.5f)] float cropMarginX = 0f;
    [SerializeField][Range(0f, 0.5f)] float cropMarginY = 0f;

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

        // 크롭 영역 계산
        int marginX = Mathf.RoundToInt(cropMarginX * imageWidth);
        int marginY = Mathf.RoundToInt(cropMarginY * imageHeight);

        int cx = marginX;
        int cy = marginY;
        int cw = imageWidth - marginX * 2;
        int ch = imageHeight - marginY * 2;

        // 크롭된 픽셀 추출 (Y 반전)
        Color[] pixels = screenTex.GetPixels(cx, imageHeight - cy - ch, cw, ch);

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