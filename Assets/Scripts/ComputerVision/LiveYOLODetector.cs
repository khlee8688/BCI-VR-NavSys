using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;

public class LiveYOLODetector : MonoBehaviour
{
    public Camera mainCamera;
    public string serverUrl = "http://127.0.0.1:8000/detect";

    const int imageWidth = 640;
    const int imageHeight = 640;

    RenderTexture camRT;
    Texture2D screenTex;

    public event System.Action<List<Detection>> OnDetections;

    bool canDetect = false;
    bool isDetecting = false;   // 중복 요청 방지

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

        // UI 레이어 제외
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

        // 마스크 복구
        mainCamera.cullingMask = originalMask;

        byte[] jpg = screenTex.EncodeToJPG(80);

        UnityWebRequest req = new UnityWebRequest(serverUrl, "POST");
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

    // 외부 제어용
    public void EnableDetection(bool enable)
    {
        canDetect = enable;
    }

    void OnDestroy()
    {
        if (camRT != null) camRT.Release();
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