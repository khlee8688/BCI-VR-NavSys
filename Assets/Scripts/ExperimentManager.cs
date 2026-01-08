using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class ExperimentManager : MonoBehaviour
{
    [SerializeField] GazeStabilityDetector gaze;
    [SerializeField] LiveYOLODetector detector;
    [SerializeField] StimulusController stimulus;
    [SerializeField] ObjectHighlighter highlighter;

    ObjectTracker tracker;

    bool experimentRunning = false;
    bool experimentInitialized = false;

    void Start()
    {
        gaze.OnLocked += StartExperiment;
        gaze.OnBroken += AbortExperiment;

        detector.OnDetections += OnDetections;
    }

    void StartExperiment()
    {
        experimentRunning = true;
        experimentInitialized = false;

        tracker = new ObjectTracker();
        // tracker.OnReferenceLost += AbortExperiment;

        detector.EnableDetection(true);

        Debug.Log("Experiment START");
    }

    void AbortExperiment()
    {
        if (!experimentRunning) return;

        experimentRunning = false;
        experimentInitialized = false;

        detector.EnableDetection(false);
        stimulus.ResetExperiment();
        gaze.ResetState();
        highlighter.ClearAll();

        Debug.Log("Experiment ABORTED ¡æ reset");
    }

    void OnDetections(List<Detection> detections)
    {
        if (!experimentRunning) return;

        var objects = tracker.Update(detections);

        highlighter.UpdateObjects(objects);

        if (!experimentInitialized && objects.Count > 0)
        {
            experimentInitialized = true;
            stimulus.StartExperiment(objects);
        }
    }
}
