using UnityEngine;
using Unity.WebRTC;
using UnityEngine.Networking;
using UnityEngine.UI;
using System.Collections;
using System.Text;

public class WebRTCReceiver : MonoBehaviour
{
    private RTCPeerConnection pc;
    private VideoStreamTrack remoteVideoTrack;
    //public RawImage targetRawImage;
    public Renderer sphereRenderer;

    private Texture receivedTexture;
    private bool hasNewFrame = false;

    [SerializeField] string signaliingURL = "https://d46127388bb1.ngrok-free.app/offer";

    [System.Serializable]
    private class RTCSessionDescriptionJson
    {
        public string type;
        public string sdp;
    }

    IEnumerator Start()
    {
        // Unity WebRTC 초기화 (필요시만 호출)
        // WebRTC.Initialize();

        // WebRTC.Update()를 코루틴으로 매 프레임 호출 (필수!)
        StartCoroutine(WebRTC.Update());

        var config = GetSelectedSdpSemantics();
        pc = new RTCPeerConnection(ref config);

        //targetRawImage.texture = CreateRedTexture();
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
                    Debug.Log("[Unity] 이전 비디오 트랙 제거 및 해제");
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

        var dto = new RTCSessionDescriptionJson
        {
            type = offerDesc.type.ToString().ToLower(),
            sdp = offerDesc.sdp
        };

        string json = JsonUtility.ToJson(dto);
        using (UnityWebRequest req = new UnityWebRequest(signaliingURL, "POST"))
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
                Debug.LogError("[Unity] 시그널링 요청 실패: " + req.error);
                yield break;
            }

            var answer = JsonUtility.FromJson<RTCSessionDescriptionJson>(req.downloadHandler.text);
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

        // 메인 스레드에서 텍스처 교체를 위해 저장
        receivedTexture = tex;
        hasNewFrame = true;
        Debug.Log("Got new frame");
    }

    void Update()
    {
        // 프레임 업데이트 시 텍스처 교체만 수행
        if (hasNewFrame)
        {
            //targetRawImage.texture = receivedTexture;
            sphereRenderer.material.mainTexture = receivedTexture;

            Debug.Log("frame to sphere");
            hasNewFrame = false;
        }
    }

    private RTCConfiguration GetSelectedSdpSemantics()
    {
        return new RTCConfiguration
        {
            iceServers = new RTCIceServer[]
            {
                new RTCIceServer
                {
                    urls = new[] { "stun:stun.l.google.com:19302" }
                }
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

        // WebRTC 리소스 해제 (필요시 호출)
        //WebRTC.Dispose();
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
