using System.Collections.Generic;
using UnityEngine;

public class TrainingManager : MonoBehaviour
{
    [SerializeField] ObjectHighlighter highlighter;
    [SerializeField] StimulusController stimulus;
    [SerializeField] private RectTransform canvasRoot;

    List<ExperimentObject> trainingObjects = new();

    bool running = false;

    float current_time = 0;
    public float blink_start_time = 5.0f;

    void Update()
    {
        if (running) return;

        current_time += Time.deltaTime;
        if(current_time >= blink_start_time) StartTraining();
    }

    public void StartTraining()
    {
        if (running) return;
        running = true;

        CreateTrainingObjects();

        highlighter.UpdateObjects(trainingObjects);

        stimulus.StartExperiment(trainingObjects);

        Debug.Log("Training START");
    }

    public void StopTraining()
    {
        if (!running) return;
        running = false;

        stimulus.ResetExperiment();
        highlighter.ClearAll();

        Debug.Log("Training STOP");
    }

    void CreateTrainingObjects()
    {
        trainingObjects.Clear();

        float size = 150f;
        float offset = 150f;
        float canvasH = canvasRoot.rect.height/2;
        float canvasW = canvasRoot.rect.width/2;

        trainingObjects.Add(MakeObj(0, "N", new Vector2(canvasW + offset, canvasH + offset), size));
        trainingObjects.Add(MakeObj(1, "N", new Vector2(canvasW + offset, canvasH - offset), size));
        trainingObjects.Add(MakeObj(2, "N", new Vector2(canvasW - offset, canvasH + offset), size));
        trainingObjects.Add(MakeObj(3, "N", new Vector2(canvasW - offset, canvasH - offset), size));
    }

    ExperimentObject MakeObj(int id, string label, Vector2 center, float size)
    {
        return new ExperimentObject
        {
            objectId = id,
            trackId = -1,
            label = label,
            bbox = new Rect(
                center.x - size * 0.5f,
                center.y - size * 0.5f,
                size,
                size
            )
        };
    }
}
