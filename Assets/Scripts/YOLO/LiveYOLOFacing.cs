using UnityEngine;
using UnityEngine.UI;
using Unity.Sentis;
using System.Collections.Generic;

public class LiveYOLOFacing : MonoBehaviour
{
    [Header("YOLO Settings")]
    [SerializeField] private ModelAsset modelAsset;
    [SerializeField] private TextAsset classesAsset;
    [SerializeField] private Texture2D borderTexture;
    [SerializeField] private Font font;

    [Header("Renderers")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private RawImage displayImage;

    const BackendType backend = BackendType.GPUCompute;
    private Worker worker;
    private string[] labels;
    private Sprite borderSprite;
    private Tensor<float> centersToCorners;
    private List<GameObject> boxPool = new();

    const int imageWidth = 640;
    const int imageHeight = 640;

    [SerializeField, Range(0, 1)]
    float iouThreshold = 0.5f;
    [SerializeField, Range(0, 1)]
    float scoreThreshold = 0.5f;

    private RenderTexture camRenderTexture;
    private Texture2D screenTexture;

    void Start()
    {
        labels = classesAsset.text.Split('\n');
        LoadModel();

        borderSprite = Sprite.Create(borderTexture,
            new Rect(0, 0, borderTexture.width, borderTexture.height),
            new Vector2(0.5f, 0.5f));

        camRenderTexture = new RenderTexture(Screen.width, Screen.height, 24);
        screenTexture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);

        // RawImage를 화면 전체에 꽉 차게
        RectTransform rt = displayImage.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        displayImage.color = new Color(1, 1, 1, 0);
    }

    void LoadModel()
    {
        var model = ModelLoader.Load(modelAsset);

        centersToCorners = new Tensor<float>(new TensorShape(4, 4),
        new float[]
        {
            1, 0, 1, 0,
            0, 1, 0, 1,
            -0.5f, 0, 0.5f, 0,
            0, -0.5f, 0, 0.5f
        });

        var graph = new FunctionalGraph();
        var inputs = graph.AddInputs(model);
        var modelOutput = Functional.Forward(model, inputs)[0];
        var boxCoords = modelOutput[0, 0..4, ..].Transpose(0, 1);
        var allScores = modelOutput[0, 4.., ..];
        var scores = Functional.ReduceMax(allScores, 0);
        var classIDs = Functional.ArgMax(allScores, 0);
        var boxCorners = Functional.MatMul(boxCoords, Functional.Constant(centersToCorners));
        var indices = Functional.NMS(boxCorners, scores, iouThreshold, scoreThreshold);
        var coords = Functional.IndexSelect(boxCoords, 0, indices);
        var labelIDs = Functional.IndexSelect(classIDs, 0, indices);

        worker = new Worker(graph.Compile(coords, labelIDs), backend);
    }

    void Update()
    {
        CaptureAndDetect();
    }

    void CaptureAndDetect()
    {
        ClearAnnotations();

        var originalRT = mainCamera.targetTexture;
        mainCamera.targetTexture = camRenderTexture;

        mainCamera.Render();

        RenderTexture.active = camRenderTexture;
        screenTexture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
        screenTexture.Apply();
        RenderTexture.active = null;

        mainCamera.targetTexture = originalRT;

        using Tensor<float> inputTensor = new Tensor<float>(new TensorShape(1, 3, imageHeight, imageWidth));
        TextureConverter.ToTensor(screenTexture, inputTensor, default);
        worker.Schedule(inputTensor);

        using var output = (worker.PeekOutput("output_0") as Tensor<float>).ReadbackAndClone();
        using var labelIDs = (worker.PeekOutput("output_1") as Tensor<int>).ReadbackAndClone();

        // Canvas 기준 크기 사용
        float displayWidth = displayImage.canvas.pixelRect.width;
        float displayHeight = displayImage.canvas.pixelRect.height;

        float scaleX = displayWidth / imageWidth;
        float scaleY = displayHeight / imageHeight;

        int boxesFound = output.shape[0];
        for (int n = 0; n < Mathf.Min(boxesFound, 50); n++)
        {
            var box = new BoundingBox
            {
                centerX = (output[n, 0] * scaleX),
                centerY = (output[n, 1] * scaleY),
                width = output[n, 2] * scaleX,
                height = output[n, 3] * scaleY,
                label = labels[labelIDs[n]],
            };
            DrawBox(box, n, displayHeight * 0.05f);
            DrawBox(box, n, displayHeight * 0.05f);
        }
    }

    struct BoundingBox
    {
        public float centerX, centerY, width, height;
        public string label;
    }


    void DrawBox(BoundingBox box, int id, float fontSize)
    {
        GameObject panel;
        if (id < boxPool.Count)
        {
            panel = boxPool[id];
            panel.SetActive(true);
        }
        else
        {
            panel = CreateNewBox(Color.yellow);
            boxPool.Add(panel);
        }

        var rect = panel.GetComponent<RectTransform>();
        rect.pivot = new Vector2(0.5f, 0.5f); // 중심 pivot
        rect.sizeDelta = new Vector2(box.width, box.height);

        // Canvas의 RectTransform 가져오기
        var canvasRect = displayImage.rectTransform.rect;

        // --- 수정된 부분 ---
        // YOLO의 좌상단 기준 좌표를 Canvas의 중앙 기준 좌표로 변환합니다.
        float anchoredX = box.centerX - (canvasRect.width / 2);
        float anchoredY = (canvasRect.height / 2) - box.centerY;
        rect.anchoredPosition = new Vector2(anchoredX, anchoredY);
        // --- 수정 끝 ---

        var label = panel.GetComponentInChildren<Text>();
        label.text = box.label;
        label.fontSize = (int)fontSize;
    }

    GameObject CreateNewBox(Color color)
    {
        var panel = new GameObject("ObjectBox");
        panel.AddComponent<CanvasRenderer>();
        var img = panel.AddComponent<Image>();
        img.color = color;
        img.sprite = borderSprite;
        img.type = Image.Type.Sliced;
        panel.transform.SetParent(displayImage.transform, false);

        var rect = panel.GetComponent<RectTransform>();
        rect.pivot = new Vector2(0.5f, 0.5f); // 중심 pivot

        var text = new GameObject("ObjectLabel");
        text.AddComponent<CanvasRenderer>();
        text.transform.SetParent(panel.transform, false);
        var txt = text.AddComponent<Text>();
        txt.font = font;
        txt.color = color;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;

        var rt2 = text.GetComponent<RectTransform>();
        rt2.offsetMin = new Vector2(20, 0);
        rt2.offsetMax = new Vector2(0, 30);
        rt2.anchorMin = new Vector2((float)0.5, (float)0.5);
        rt2.anchorMax = new Vector2((float)0.5, (float)0.5);

        return panel;
    }

    void ClearAnnotations()
    {
        foreach (var box in boxPool)
            box.SetActive(false);
    }

    void OnDestroy()
    {
        centersToCorners?.Dispose();
        worker?.Dispose();
        if (camRenderTexture) camRenderTexture.Release();
    }
}
