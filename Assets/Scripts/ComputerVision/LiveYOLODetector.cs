using UnityEngine;
using Unity.Sentis;
using System;
using System.Collections.Generic;
using System.Linq;

public class LiveYOLODetector : MonoBehaviour
{
    [Header("YOLO")]
    public ModelAsset modelAsset;
    public TextAsset classesAsset;
    public Camera mainCamera;

    public event Action<List<Detection>> OnDetections;

    const int imageWidth = 640;
    const int imageHeight = 640;
    const BackendType backend = BackendType.GPUCompute;

    Worker worker;
    string[] labels;

    RenderTexture camRT;
    Texture2D screenTex;

    bool canDetect = false;

    void Start()
    {
        labels = classesAsset.text
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrEmpty(l))
            .ToArray();

        camRT = new RenderTexture(imageWidth, imageHeight, 24);
        screenTex = new Texture2D(imageWidth, imageHeight, TextureFormat.RGB24, false);

        LoadModel();
    }

    void LoadModel()
    {
        var model = ModelLoader.Load(modelAsset);

        var graph = new FunctionalGraph();
        var inputs = graph.AddInputs(model);
        var output = Functional.Forward(model, inputs)[0];

        var boxCoords = output[0, 0..4, ..].Transpose(0, 1);
        var scores = Functional.ReduceMax(output[0, 4.., ..], 0);
        var classIds = Functional.ArgMax(output[0, 4.., ..], 0);

        var indices = Functional.NMS(boxCoords, scores, 0.5f, 0.5f);
        var finalBoxes = Functional.IndexSelect(boxCoords, 0, indices);
        var finalIds = Functional.IndexSelect(classIds, 0, indices);

        worker = new Worker(graph.Compile(finalBoxes, finalIds), backend);
    }

    void Update()
    {
        if (!canDetect) return;
        CaptureAndDetect();
    }

    void CaptureAndDetect()
    {
        var prevRT = mainCamera.targetTexture;
        mainCamera.targetTexture = camRT;
        mainCamera.Render();

        RenderTexture.active = camRT;
        screenTex.ReadPixels(new Rect(0, 0, imageWidth, imageHeight), 0, 0);
        screenTex.Apply();

        RenderTexture.active = null;
        mainCamera.targetTexture = prevRT;

        using var input = new Tensor<float>(new TensorShape(1, 3, imageHeight, imageWidth));
        TextureConverter.ToTensor(screenTex, input, new TextureTransform());

        worker.Schedule(input);

        using var boxesGPU = worker.PeekOutput(0) as Tensor<float>;
        using var idsGPU = worker.PeekOutput(1) as Tensor<int>;

        using var boxes = boxesGPU.ReadbackAndClone();
        using var ids = idsGPU.ReadbackAndClone();

        var detections = new List<Detection>();
        int count = boxes.shape[0];

        for (int i = 0; i < count; i++)
        {
            detections.Add(new Detection
            {
                cx = boxes[i, 0],
                cy = boxes[i, 1],
                w = boxes[i, 2],
                h = boxes[i, 3],
                label = labels[ids[i]]
            });
        }
        OnDetections?.Invoke(detections);
    }

    public void EnableDetection(bool enable)
    {
        canDetect = enable;
    }

    void OnDestroy()
    {
        worker?.Dispose();
        if (camRT != null) camRT.Release();
    }
}

[Serializable]
public class Detection
{
    public float cx, cy;
    public float w, h;
    public string label;
}
