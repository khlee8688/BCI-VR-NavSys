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
        // Unity WebRTC �ʱ�ȭ (�ʿ�ø� ȣ��)
        // WebRTC.Initialize();

        // WebRTC.Update()�� �ڷ�ƾ���� �� ������ ȣ�� (�ʼ�!)
        StartCoroutine(WebRTC.Update());

        var config = GetSelectedSdpSemantics();
        pc = new RTCPeerConnection(ref config);

        //targetRawImage.texture = CreateRedTexture();
        sphereRenderer.material.mainTexture = CreateRedTexture();


        pc.OnConnectionStateChange += state =>
        {
            Debug.Log($"[Unity] ���� ���� ����: {state}");
        };

        pc.OnTrack += e =>
        {
            if (e.Track is VideoStreamTrack videoTrack)
            {
                Debug.Log("[Unity] ���� Ʈ�� ���ŵ�");

                if (remoteVideoTrack != null && remoteVideoTrack != videoTrack)
                {
                    Debug.Log("[Unity] ���� ���� Ʈ�� ���� �� ����");
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
            Debug.Log($"[Unity] ICE �ĺ� ������: {candidate.Candidate}");
        };

        var offerOp = pc.CreateOffer();
        yield return offerOp;

        if (offerOp.IsError)
        {
            Debug.LogError("[Unity] Offer ���� ����: " + offerOp.Error.message);
            yield break;
        }

        var offerDesc = offerOp.Desc;
        var setLocalOp = pc.SetLocalDescription(ref offerDesc);
        yield return setLocalOp;

        if (setLocalOp.IsError)
        {
            Debug.LogError("[Unity] LocalDescription ���� ����: " + setLocalOp.Error.message);
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
                Debug.LogError("[Unity] �ñ׳θ� ��û ����: " + req.error);
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
                Debug.LogError("[Unity] RemoteDescription ���� ����: " + setRemoteOp.Error.message);
                yield break;
            }

            Debug.Log("[Unity] WebRTC ���� �Ϸ��.");
        }
    }

    private void OnVideoFrameReceived(Texture tex)
    {
        if (tex == null)
        {
            Debug.LogWarning("[Unity] ���ŵ� �ؽ�ó�� null�Դϴ�.");
            return;
        }

        // ���� �����忡�� �ؽ�ó ��ü�� ���� ����
        receivedTexture = tex;
        hasNewFrame = true;
        Debug.Log("Got new frame");
    }

    void Update()
    {
        // ������ ������Ʈ �� �ؽ�ó ��ü�� ����
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

        // WebRTC ���ҽ� ���� (�ʿ�� ȣ��)
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
