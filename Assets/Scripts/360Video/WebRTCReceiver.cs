using UnityEngine;
using Unity.WebRTC;
using UnityEngine.Networking;
using UnityEngine.UI;
using System.Collections;
using System.Text;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

public class WebRTCReceiver : MonoBehaviour
{
    private RTCPeerConnection pc;
    private VideoStreamTrack remoteVideoTrack;
    public Renderer sphereRenderer;

    private Texture receivedTexture;
    private bool hasNewFrame = false;

    [SerializeField] string signalingURL = "https://lashawna-semifused-limberly.ngrok-free.app/offer";

    [System.Serializable]
    private class RTCSessionDescriptionJson
    {
        public string type;
        public string sdp;
    }

    void Awake()
    {
        // SSL 인증서 검증 무시 (ngrok/테스트용)
        ServicePointManager.ServerCertificateValidationCallback =
            delegate (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) {
                return true;
            };
    }

    IEnumerator Start()
    {
        StartCoroutine(WebRTC.Update());

        var config = GetSelectedSdpSemantics();
        pc = new RTCPeerConnection(ref config);

        sphereRenderer.material.mainTexture = CreateRedTexture();

        pc.OnConnectionStateChange += state =>
        {
            Debug.Log($"[Unity] 연결 상태 변경: {state}");
        };

        pc.OnTrack += e =>
        {
            if (e.Track is VideoStreamTrack videoTrack)
            {
                Debug.Log("[Unity] 비디오 트랙 수신됨");

                if (remoteVideoTrack != null && remoteVideoTrack != videoTrack)
                {
                    remoteVideoTrack.OnVideoReceived -= OnVideoFrameReceived;
                    remoteVideoTrack.Dispose();
                }

                remoteVideoTrack = videoTrack;
                remoteVideoTrack.OnVideoReceived += OnVideoFrameReceived;
            }
        };

        pc.AddTransceiver(TrackKind.Video, new RTCRtpTransceiverInit
        {
            direction = RTCRtpTransceiverDirection.RecvOnly
        });

        pc.OnIceCandidate = candidate =>
        {
            Debug.Log($"[Unity] ICE 후보 생성됨: {candidate.Candidate}");
        };

        var offerOp = pc.CreateOffer();
        yield return offerOp;

        if (offerOp.IsError)
        {
            Debug.LogError("[Unity] Offer 생성 실패: " + offerOp.Error.message);
            yield break;
        }

        var offerDesc = offerOp.Desc;
        var setLocalOp = pc.SetLocalDescription(ref offerDesc);
        yield return setLocalOp;

        if (setLocalOp.IsError)
        {
            Debug.LogError("[Unity] LocalDescription 설정 실패: " + setLocalOp.Error.message);
            yield break;
        }

        // SDP 줄바꿈 안전 변환
        var safeSdp = offerDesc.sdp.Replace("\r\n", "\\r\\n");

        var dto = new RTCSessionDescriptionJson
        {
            type = offerDesc.type.ToString().ToLower(),
            sdp = safeSdp
        };

        string json = JsonUtility.ToJson(dto);
        Debug.Log($"[Unity] 시그널링 전송 JSON: {json.Substring(0, Mathf.Min(json.Length, 200))}...");

        using (UnityWebRequest req = new UnityWebRequest(signalingURL, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            if (req.result != UnityWebRequest.Result.Success)
#else
            if (req.isNetworkError || req.isHttpError)
#endif
            {
                Debug.LogError($"[Unity] 시그널링 요청 실패: {req.responseCode} {req.error}");
                Debug.LogError("[Unity] 응답 본문: " + req.downloadHandler.text);
                yield break;
            }

            var answer = JsonUtility.FromJson<RTCSessionDescriptionJson>(req.downloadHandler.text);
            // 서버에서 받은 SDP도 복원
            answer.sdp = answer.sdp.Replace("\\r\\n", "\r\n");

            var answerDesc = new RTCSessionDescription
            {
                type = RTCSdpType.Answer,
                sdp = answer.sdp
            };

            var setRemoteOp = pc.SetRemoteDescription(ref answerDesc);
            yield return setRemoteOp;

            if (setRemoteOp.IsError)
            {
                Debug.LogError("[Unity] RemoteDescription 설정 실패: " + setRemoteOp.Error.message);
                yield break;
            }

            Debug.Log("[Unity] WebRTC 연결 완료됨.");
        }
    }

    private void OnVideoFrameReceived(Texture tex)
    {
        if (tex == null)
        {
            Debug.LogWarning("[Unity] 수신된 텍스처가 null입니다.");
            return;
        }

        receivedTexture = tex;
        hasNewFrame = true;
        Debug.Log("Got new frame");
    }

    void Update()
    {
        if (hasNewFrame)
        {
            sphereRenderer.material.mainTexture = receivedTexture;
            hasNewFrame = false;
        }
    }

    private RTCConfiguration GetSelectedSdpSemantics()
    {
        return new RTCConfiguration
        {
            iceServers = new RTCIceServer[]
            {
                new RTCIceServer { urls = new[] { "stun:stun.l.google.com:19302" } }
            }
        };
    }

    private void OnDestroy()
    {
        if (remoteVideoTrack != null)
        {
            remoteVideoTrack.OnVideoReceived -= OnVideoFrameReceived;
            remoteVideoTrack.Dispose();
            remoteVideoTrack = null;
        }

        pc?.Close();
        pc?.Dispose();
        pc = null;
    }

    private Texture2D CreateRedTexture(int width = 128, int height = 128)
    {
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        var pixels = new Color32[width * height];
        var red = new Color32(255, 0, 0, 255);
        for (int i = 0; i < pixels.Length; i++) pixels[i] = red;
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }
}
