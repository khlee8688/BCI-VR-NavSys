using UnityEngine;
using Unity.Sentis;
using System;
using System.Collections.Generic;
using System.Linq;

public class LiveYOLODetector : MonoBehaviour
{
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
        labels = classesAsset.text.Split('\n')
            .Select(l => l.Trim()).Where(l => l != "").ToArray();

        camRT = new RenderTexture(Screen.width, Screen.height, 24);
        screenTex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);

        LoadModel();
    }

    void LoadModel()
    {
        var model = ModelLoader.Load(modelAsset);
        var graph = new FunctionalGraph();
        var inputs = graph.AddInputs(model);
        var output = Functional.Forward(model, inputs)[0];

        var boxes = output[0, 0..4, ..].Transpose(0, 1);
        var scores = Functional.ReduceMax(output[0, 4.., ..], 0);
        var ids = Functional.ArgMax(output[0, 4.., ..], 0);

        var indices = Functional.NMS(boxes, scores, 0.5f, 0.5f);
        var finalBoxes = Functional.IndexSelect(boxes, 0, indices);
        var finalIds = Functional.IndexSelect(ids, 0, indices);

        worker = new Worker(graph.Compile(finalBoxes, finalIds), backend);
    }

    void Update()
    {
        if(canDetect) Capture();
    }

    void Capture()
    {
        mainCamera.targetTexture = camRT;
        mainCamera.Render();

        RenderTexture.active = camRT;
        screenTex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
        screenTex.Apply();
        RenderTexture.active = null;
        mainCamera.targetTexture = null;

        using var input = new Tensor<float>(new TensorShape(1, 3, imageHeight, imageWidth));
        TextureConverter.ToTensor(screenTex, input, new TextureTransform());

        worker.Schedule(input);

        using var boxes = worker.PeekOutput(0) as Tensor<float>;
        using var ids = worker.PeekOutput(1) as Tensor<int>;

        var dets = new List<Detection>();
        for (int i = 0; i < boxes.shape[0]; i++)
        {
            dets.Add(new Detection
            {
                cx = boxes[i, 0],
                cy = boxes[i, 1],
                w = boxes[i, 2],
                h = boxes[i, 3],
                label = labels[ids[i]]
            });
        }

        OnDetections?.Invoke(dets);
    }

    public void EnableDetection(bool enable)
    {
        canDetect = enable;
    }
}

public class Detection
{
    public float cx, cy, w, h;
    public string label;
}