using Oculus.Interaction.UnityCanvas;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.SearchService;
using UnityEngine;

public class ExperimentManager : MonoBehaviour
{
    [Header("Core")]
    [SerializeField] GazeStabilityDetector gaze;
    [SerializeField] LiveYOLODetector detector;
    [SerializeField] StimulusController stimulus;
    [SerializeField] ObjectHighlighter highlighter;
    [SerializeField] RobotController robot;
    [SerializeField] RectTransform canvasRoot;

    [Header("Player")]
    [SerializeField] Camera vrCamera;
    [SerializeField] GameObject player;

    [Header("Robot Timing")]
    [SerializeField] float moveDuration = 2.0f;

    [Header("Robot Motion Params")]
    [SerializeField] float angularSpeedDegPerSec = 10f;

    ObjectTracker tracker;
    bool experimentRunning = false;
    bool experimentInitialized = false;
    bool objectSelected = false;

    ExperimentObject selectedObject;
    Coroutine navRoutine;

    void Start()
    {
        gaze.OnLocked += StartExperiment;
        gaze.OnBroken += AbortExperiment;

        detector.OnDetections += OnDetections;
        stimulus.OnStimulusEnd += OnStimulusEnd;

        BoxClickHandler.OnBoxClicked += OnBoxClicked;
    }

    void StartExperiment()
    {
        experimentRunning = true;
        experimentInitialized = false;
        objectSelected = false;

        tracker = new ObjectTracker();
        detector.EnableDetection(true);
    }

    void AbortExperiment()
    {
        if (!experimentRunning) return;

        experimentRunning = false;
        experimentInitialized = false;
        objectSelected = false;

        if (navRoutine != null)
        {
            StopCoroutine(navRoutine);
            navRoutine = null;
        }

        detector.EnableDetection(false);
        stimulus.ResetExperiment();
        gaze.ResetState();
        highlighter.ClearAll();
        robot.Stop();
    }

    void OnDetections(List<Detection> detections)
    {
        if (!experimentRunning) return;
        if (objectSelected) return;

        var objects = tracker.Update(detections);
        highlighter.UpdateObjects(objects);

        if (!experimentInitialized && objects.Count > 0)
        {
            experimentInitialized = true;
            stimulus.StartExperiment(objects);
        }
    }

    void OnStimulusEnd()
    {
        if (!experimentRunning) return;

        detector.EnableDetection(false);
        stimulus.ResetExperiment();
        gaze.StopGazeCheck();

        int selectedId = 0; // TO-DO: Online LDA °á°ú
        selectedObject = tracker.GetObjectById(selectedId);
        if (selectedObject == null) return;

        navRoutine = StartCoroutine(RotateThenMoveCoroutine());
    }

    void OnBoxClicked(int objectId)
    {
        if (!experimentRunning) return;
        if (objectSelected) return;

        objectSelected = true;

        detector.EnableDetection(false);
        stimulus.ResetExperiment();
        gaze.StopGazeCheck();
        highlighter.ClearAll();

        selectedObject = tracker.GetObjectById(objectId);
        if (selectedObject == null) return;

        Debug.Log($"[TEST] Click selected object {objectId}");

        if (navRoutine != null)
            StopCoroutine(navRoutine);

        navRoutine = StartCoroutine(RotateThenMoveCoroutine());
    }

    IEnumerator RotateThenMoveCoroutine()
    {
        GameObject box = highlighter.GetBoxFromObjectID(selectedObject.objectId);
        RectTransform rt = box.GetComponent<RectTransform>();

        Vector3 worldPos = rt.position;
        Debug.Log(worldPos);
        Vector3 camPos = vrCamera.transform.position;

        Ray ray = new Ray(camPos, (worldPos-camPos).normalized);

        Debug.DrawRay(ray.origin, ray.direction * 150f, Color.red, 20f);

        Vector3 baseForward = player.transform.forward;
        Vector3 targetDir = ray.direction;

        float yaw = CalculateSignedYaw(baseForward, targetDir);

        if (Mathf.Abs(yaw) > 1f)
        {
            bool rotateRight = yaw > 0f;
            float rotateDuration = Mathf.Abs(yaw) / angularSpeedDegPerSec;

            float t = 0f;
            while (t < rotateDuration)
            {
                if (rotateRight)
                    robot.TurnRight();
                else
                    robot.TurnLeft();

                t += Time.deltaTime;
                yield return null;
            }

            robot.Stop();
        }

        float mt = 0f;
        while (mt < moveDuration)
        {
            robot.MoveForward();
            mt += Time.deltaTime;
            yield return null;
        }

        robot.Stop();
    }

    float CalculateSignedYaw(Vector3 baseForward, Vector3 targetDir)
    {
        baseForward.y = 0f;
        targetDir.y = 0f;

        baseForward.Normalize();
        targetDir.Normalize();

        return Vector3.SignedAngle(baseForward, targetDir, Vector3.up);
    }
}
