using System;
using System.Collections;
using UnityEngine.Networking;
using UnityEngine;
using System.Text;

[System.Serializable]
public class PredictRequest
{
    public int button_num;
    public int[] marker_ids;
}

[System.Serializable]
public class PredictResponse
{
    public int result;
}

public class ResultReceiver : MonoBehaviour
{
    public string serverUrl = "http://127.0.0.1:12240/result";

    // buttonNum을 파라미터로 받도록 변경
    public IEnumerator GetResult(int buttonNum, int[] markerID, Action<int> onResultReceived, Action<string> onError)
    {
        PredictRequest requestData = new PredictRequest { button_num = buttonNum, marker_ids = markerID };
        string jsonData = JsonUtility.ToJson(requestData);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);

        UnityWebRequest req = new UnityWebRequest(serverUrl, "POST");
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.timeout = 65;

        Debug.Log($"[LDA] Sending request with button_num={buttonNum}");
        Debug.Log("[LDA] Waiting for result...");

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            try
            {
                var json = req.downloadHandler.text;
                var response = JsonUtility.FromJson<PredictResponse>(json);
                int result = response.result;

                Debug.Log($"[LDA] Prediction Result: {result}");
                onResultReceived?.Invoke(result);
            }
            catch (Exception e)
            {
                string errorMsg = $"Failed to parse response: {e.Message}";
                Debug.LogError($"[LDA] {errorMsg}");
                onError?.Invoke(errorMsg);
            }
        }
        else
        {
            string errorMsg = $"{req.error} - {req.downloadHandler.text}";
            Debug.LogError($"[LDA] Error: {errorMsg}");
            onError?.Invoke(errorMsg);
        }
    }
}