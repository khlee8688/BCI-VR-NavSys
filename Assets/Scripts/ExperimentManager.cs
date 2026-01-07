using UnityEngine;
using System.Collections.Generic;

public class ExperimentManager : MonoBehaviour
{
    public GazeStabilityDetector gaze;
    public LiveYOLODetector detector;
    public StimulusController stimulus;

    bool experimentRunning;

    void Start()
    {
        gaze.OnLocked += StartExperiment;
        gaze.OnBroken += AbortExperiment;
    }

    void StartExperiment()
    {
        experimentRunning = true;
        detector.EnableDetection(true);
        Debug.Log("Experiment START");
    }

    void AbortExperiment()
    {
        if (!experimentRunning) return;

        experimentRunning = false;

        detector.EnableDetection(false);
        stimulus.ResetExperiment();
        gaze.ResetState();

        Debug.Log("Experiment ABORTED ¡æ reset");
    }
}
