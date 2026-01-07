using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// YOLOv12-Seg (onnx) 기반으로 검출 + segmentation mask 생성.
/// P300 용 random single flashing: 매 100ms마다 탐지된 객체 중 하나를 랜덤 선택하여 마스크만 보이게 함.
/// 다른 객체들은 투명으로 유지.
/// </summary>
public class LiveYOLOFacing : MonoBehaviour
{
    [Header("YOLO Settings")]
    public string onnxModelPath;         // 예: StreamingAssets/yolov12-seg.onnx
    public TextAsset classesAsset;       // 클래스 이름 txt (newline 구분)
    public Font font;

    [Header("Renderers")]
    public Camera mainCamera;
    public RawImage displayImage;        // 화면에 전체 카메라 프레임을 보여주는 RawImage (캔버스)

    [Header("Highlight Logic")]
    [Tooltip("하이라이트가 랜덤으로 바뀌는 간격(초). P300 용으로 0.1f 설정.")]
    [SerializeField] private float highlightInterval = 0.1f;

    [Header("Detection thresholds")]
    [SerializeField, Range(0f, 1f)] private float scoreThreshold = 0.5f;

    // ONNX runtime session
    private InferenceSession session;
    private string[] labels;

    // 캡처용
    private RenderTexture camRenderTexture;
    private Texture2D screenTexture;

    // 활성 마스크(원본 마스크 텍스쳐 + GameObject)
    private List<GameObject> activeMasksForHighlight = new List<GameObject>();

    // 하이라이트 상태
    private float highlightTimer = 0f;
    private int highlightedIndex = -1;

    // 모델/입력 크기 (yolov12-seg 기준)
    const int imageWidth = 640;
    const int imageHeight = 640;

    void Start()
    {
        // 라벨 로드
        labels = classesAsset.text.Split('\n').Select(l => l.Trim()).Where(l => !string.IsNullOrEmpty(l)).ToArray();

        // ONNX 세션 로드 (onnxModelPath가 StreamingAssets 경로 등으로 설정되어 있어야 함)
        // 예시: StreamingAssets 경로에서 onnx 파일 불러오기
        session = new InferenceSession(onnxModelPath);

        // 캡처용 RT와 텍스쳐 (화면 해상도에 맞춤)
        camRenderTexture = new RenderTexture(imageWidth, imageHeight, 24, RenderTextureFormat.ARGB32);
        screenTexture = new Texture2D(imageWidth, imageHeight, TextureFormat.RGB24, false);

        // displayImage가 화면에 꽉 차도록 설정 (기존 Sentis 코드와 동일한 초기화 의도)
        RectTransform rt = displayImage.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        displayImage.color = new Color(1, 1, 1, 0); // 원래 영상은 투명으로 두려면 변경 가능
    }

    void Update()
    {
        // 매 프레임 캡처하고 검출 & 마스크 생성 (이 부분은 비용이 크므로 필요시 스레드/타임슬라이스 고려)
        CaptureAndDetect();

        // highlight: 100ms마다 랜덤 하나 골라 표시
        highlightTimer += Time.deltaTime;
        if (highlightTimer >= highlightInterval)
        {
            highlightTimer = 0f;
            if (activeMasksForHighlight.Count > 0)
            {
                highlightedIndex = UnityEngine.Random.Range(0, activeMasksForHighlight.Count);
            }
            else
            {
                highlightedIndex = -1;
            }
        }

        // mask 색 업데이트 (하이라이트된 것만 보이게, 나머지는 투명)
        UpdateMaskVisibility();
    }

    void CaptureAndDetect()
    {
        // 기존 마스크 전부 제거
        ClearMasks();
        activeMasksForHighlight.Clear();

        // 카메라로부터 프레임 캡처 (imageWidth x imageHeight)
        var originalRT = mainCamera.targetTexture;
        mainCamera.targetTexture = camRenderTexture;
        mainCamera.Render();
        RenderTexture.active = camRenderTexture;
        screenTexture.ReadPixels(new Rect(0, 0, imageWidth, imageHeight), 0, 0);
        screenTexture.Apply();
        RenderTexture.active = null;
        mainCamera.targetTexture = originalRT;

        // 입력 텐서 생성 (NCHW, float / normalize 0..1)
        float[] inputData = new float[1 * 3 * imageHeight * imageWidth];
        Color32[] pixels = screenTexture.GetPixels32();
        // note: screenTexture.GetPixels32() returns row-major left-to-right, bottom-to-top for ReadPixels usage.
        // we assume screenTexture stores imageWidth x imageHeight in row-major order
        for (int y = 0; y < imageHeight; y++)
        {
            for (int x = 0; x < imageWidth; x++)
            {
                int idx = y * imageWidth + x;
                Color32 c = pixels[idx];
                inputData[idx] = c.r / 255f;                                    // channel 0
                inputData[imageWidth * imageHeight + idx] = c.g / 255f;         // channel 1
                inputData[2 * imageWidth * imageHeight + idx] = c.b / 255f;     // channel 2
            }
        }
        var inputTensor = new DenseTensor<float>(inputData, new int[] { 1, 3, imageHeight, imageWidth });
        var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("images", inputTensor) };

        // ONNX 추론
        using var results = session.Run(inputs);

        // 모델 출력명은 사용 모델에 따라 다를수 있으므로 로깅/디버그 필요. 여기서는 제공해주신 이름/shape에 맞음.
        var output0 = results.First(x => x.Name == "output0").AsTensor<float>(); // [1, 116, 8400]
        var output1 = results.First(x => x.Name == "output1").AsTensor<float>(); // [1, 32, 160, 160]

        // 파싱: output0 dimension: [batch, featDim(=116), numDet(=8400)]
        int featDim = output0.Dimensions[1];   // 116
        int numDet = output0.Dimensions[2];    // 8400

        // output1 prototype dims
        int protoC = output1.Dimensions[1]; // 32
        int protoH = output1.Dimensions[2]; // 160
        int protoW = output1.Dimensions[3]; // 160

        // numClasses = featDim - (4 + maskDim + maybe something). For yolov12-seg typical: featDim = 4 + numClasses + maskDim
        // We don't know exact numClasses from model; user provided classesAsset length -> use that
        int numClasses = labels.Length;
        int maskDim = featDim - 4 - numClasses;
        if (maskDim <= 0)
        {
            Debug.LogError("Mask dimension calculated <=0. Check model output layout and classes count.");
            return;
        }

        // loop through detections
        for (int d = 0; d < numDet; d++)
        {
            // box coords/features are at feature indices 0..3
            float cx = output0[0, 0, d];
            float cy = output0[0, 1, d];
            float w = output0[0, 2, d];
            float h = output0[0, 3, d];

            // class scores start at index 4 ... 4 + numClasses -1
            // find best class and its score
            float bestScore = 0f;
            int bestClass = -1;
            for (int c = 0; c < numClasses; c++)
            {
                float sc = output0[0, 4 + c, d];
                if (sc > bestScore)
                {
                    bestScore = sc;
                    bestClass = c;
                }
            }

            if (bestScore < scoreThreshold) continue;

            // extract mask coefficients: they follow class scores
            float[] coeff = new float[maskDim];
            int coeffBase = 4 + numClasses;
            for (int m = 0; m < maskDim; m++)
                coeff[m] = output0[0, coeffBase + m, d];

            // 복원: proto (1, protoC, protoH, protoW) 와 coeff(protoC) => per-pixel value
            // create maskPixels in proto resolution
            Color32[] maskPixels = new Color32[protoW * protoH];
            for (int py = 0; py < protoH; py++)
            {
                for (int px = 0; px < protoW; px++)
                {
                    float val = 0f;
                    // sum_{c} proto[0,c,py,px] * coeff[c]
                    for (int c = 0; c < protoC && c < coeff.Length; c++)
                    {
                        val += output1[0, c, py, px] * coeff[c];
                    }
                    // sigmoid and threshold
                    float s = 1f / (1f + Mathf.Exp(-val));
                    byte alpha = (byte)(s > 0.5f ? 255 : 0); // threshold 0.5
                    maskPixels[py * protoW + px] = new Color32(255, 0, 0, alpha);
                }
            }

            // Texture 생성 (proto resolution). We'll stretch this texture to display size so it overlays correctly.
            Texture2D maskTex = new Texture2D(protoW, protoH, TextureFormat.RGBA32, false);
            maskTex.SetPixels32(maskPixels);
            maskTex.Apply();

            // Mask GameObject 생성: RawImage를 displayImage의 자식으로 두고 Stretch 한다.
            GameObject maskObj = new GameObject($"Mask_{bestClass}_{d}");
            maskObj.transform.SetParent(displayImage.transform, false);

            var raw = maskObj.AddComponent<RawImage>();
            raw.texture = maskTex;
            // 기본은 투명(안 보임). 하이라이트에서만 alpha를 올립니다.
            raw.color = new Color(1f, 0f, 0f, 0f);

            var rtTrans = maskObj.GetComponent<RectTransform>();
            // Stretch to fill displayImage rect so mask aligns with camera frame
            rtTrans.anchorMin = Vector2.zero;
            rtTrans.anchorMax = Vector2.one;
            rtTrans.sizeDelta = Vector2.zero;
            rtTrans.anchoredPosition = Vector2.zero;

            // Save some metadata (optional): attach a helper component to hold bbox/class if needed later
            var meta = maskObj.AddComponent<MaskMeta>();
            meta.classId = bestClass;
            meta.score = bestScore;
            meta.bbox_cx = cx;
            meta.bbox_cy = cy;
            meta.bbox_w = w;
            meta.bbox_h = h;
            meta.protoWidth = protoW;
            meta.protoHeight = protoH;

            activeMasksForHighlight.Add(maskObj);
        }

        // 정리: 입력 텐서 dispose
        inputTensor = null;
        // results disposable handled by using
    }

    void UpdateMaskVisibility()
    {
        for (int i = 0; i < activeMasksForHighlight.Count; i++)
        {
            var img = activeMasksForHighlight[i].GetComponent<RawImage>();
            if (img == null) continue;

            if (i == highlightedIndex)
            {
                // 하이라이트된 객체만 반투명하게 보여줌 (alpha 0.6)
                img.color = new Color(1f, 0f, 0f, 0.6f);
            }
            else
            {
                // 다른 객체들은 투명하게 (안 보이게)
                img.color = new Color(1f, 0f, 0f, 0f);
            }
        }
    }

    void ClearMasks()
    {
        // 안전하게 모든 자식 Mask 오브젝트 파괴
        foreach (var g in activeMasksForHighlight)
        {
            if (g != null) Destroy(g);
        }
    }

    void OnDestroy()
    {
        session?.Dispose();
        if (camRenderTexture) camRenderTexture.Release();
    }

    // 간단한 메타 저장용 컴포넌트
    private class MaskMeta : MonoBehaviour
    {
        public int classId;
        public float score;
        public float bbox_cx, bbox_cy, bbox_w, bbox_h;
        public int protoWidth, protoHeight;
    }
}
