using Oculus.Interaction.UnityCanvas;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEditor.SearchService;
using UnityEngine;
using UnityEngine.UI;

public class ExperimentManager : MonoBehaviour
{
    [Header("Core")]
    [SerializeField] GazeStabilityDetector gaze;
    [SerializeField] LiveYOLODetector detector;
    [SerializeField] StimulusController stimulus;
    [SerializeField] ObjectHighlighter highlighter;
    [SerializeField] RobotController robot;
    [SerializeField] RectTransform canvasRoot;
    [SerializeField] TMP_Text helperText;

    [Header("Player")]
    [SerializeField] Camera vrCamera;
    [SerializeField] GameObject player;

    [Header("Robot Timing")]
    [SerializeField] float moveDuration = 5.0f;

    [Header("Robot Motion Params")]
    [SerializeField] float angularSpeedDegPerSec = 12f;

    [SerializeField] Button arrowButton;

    ObjectTracker tracker;
    bool experimentRunning = false;
    bool experimentInitialized = false;
    bool objectSelected = false;

    ExperimentObject selectedObject;
    ExperimentObject arrowButtonObject;
    ExperimentObject exitButtonObject;
    Coroutine navRoutine;

    List<ExperimentObject> allObjects;

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

        helperText.text = "Start Experiment";

        tracker = new ObjectTracker();

        arrowButtonObject = new ExperimentObject
        {
            objectId = 1,
            label = "Arrow_Button",
            bbox = new Rect()
        };

        exitButtonObject = new ExperimentObject
        {
            objectId = 2,
            label = "Exit_Button",
            bbox = new Rect()
        };

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

        helperText.text = "Experiment Aborted";

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

        var trackedObjects = tracker.Update(detections);

        allObjects = new List<ExperimentObject>();
        allObjects.Add(arrowButtonObject);
        allObjects.Add(exitButtonObject);
        allObjects.AddRange(trackedObjects); // id >= 3

        highlighter.UpdateObjects(allObjects);

        helperText.text = "Look at the Object.";

        if (!experimentInitialized && allObjects.Count > 0)
        {
            experimentInitialized = true;
            stimulus.StartExperiment(allObjects);
        }
    }

    void OnStimulusEnd()
    {
        if (!experimentRunning) return;

        detector.EnableDetection(false);
        stimulus.ResetExperiment();
        gaze.StopGazeCheck();

        int selectedId = 1; // TO-DO: Online LDA °á°ú
        selectedObject = GetObjectById(selectedId);
        Debug.Log(selectedObject.bbox);
        if (selectedObject == null) return;

        helperText.text = "Object Selected: " + selectedObject.label;

        if(selectedId == 1)
        {
            navRoutine = StartCoroutine(MoveToLookingDirection());
        }
        else if(selectedId == 2)
        {
            Application.Quit();
        }
        else
        {
            navRoutine = StartCoroutine(RotateThenMoveCoroutine());
        }
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

        selectedObject = GetObjectById(objectId);
        if (selectedObject == null) return;

        helperText.text = "Object Selected: " + selectedObject.label;

        if (navRoutine != null)
            StopCoroutine(navRoutine);

        if (objectId == 1)
        {
            navRoutine = StartCoroutine(MoveToLookingDirection());
        }
        else if (objectId == 2)
        {
            Application.Quit();
        }
        else
        {
            navRoutine = StartCoroutine(RotateThenMoveCoroutine());
        }
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

        helperText.text = "Robot Moving.";

        if (Mathf.Abs(yaw) > 1f)
        {
            bool rotateRight = !(yaw > 0f);
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
        helperText.text = "Arrived at Destination.";
        gaze.StartGazeCheck();
    }
    IEnumerator MoveToLookingDirection()
    {
        Vector3 baseForward = player.transform.forward;
        Vector3 camForward = vrCamera.transform.forward;

        float yaw = CalculateSignedYaw(baseForward, camForward);

        helperText.text = "Robot Moving to Looking Direction.";

        if (Mathf.Abs(yaw) > 1f)
        {
            bool rotateRight = !(yaw > 0f);
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
        helperText.text = "Arrived at Destination.";
        gaze.StartGazeCheck();
    }

    float CalculateSignedYaw(Vector3 baseForward, Vector3 targetDir)
    {
        baseForward.y = 0f;
        targetDir.y = 0f;

        baseForward.Normalize();
        targetDir.Normalize();

        return Vector3.SignedAngle(baseForward, targetDir, Vector3.up);
    }

    ExperimentObject GetObjectById(int id)
    {
        foreach(ExperimentObject obj in allObjects)
        {
            if (obj.objectId == id) return obj;
        }

        return null;
    }
}
