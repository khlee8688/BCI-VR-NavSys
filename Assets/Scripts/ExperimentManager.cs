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
    [SerializeField] StimulusSender sender;
    [SerializeField] ObjectHighlighter highlighter;
    [SerializeField] ResultReceiver ldaReceiver;
    [SerializeField] RobotController robot;
    [SerializeField] RectTransform canvasRoot;
    [SerializeField] TMP_Text helperText;

    [Header("Player")]
    [SerializeField] Camera vrCamera;
    [SerializeField] GameObject player;

    [Header("Robot Motion Params")]
    [SerializeField] float angularSpeedDegPerSec = 12f;

    [SerializeField] Button arrowButton;

    ObjectTracker tracker;
    ObjectFilter filter;
    bool experimentRunning = false;
    bool experimentInitialized = false;
    bool objectSelected = false;
    bool isMoving = false;

    ExperimentObject selectedObject;
    ExperimentObject arrowButtonObject;
    ExperimentObject exitButtonObject;
    Coroutine navRoutine;

    List<ExperimentObject> allObjects;

    public byte finish = 9;
    public byte start = 8;

    void Start()
    {
        gaze.OnLocked += StartExperiment;
        gaze.OnBroken += AbortExperiment;

        detector.OnDetections += OnDetections;
        stimulus.OnStimulusEnd += OnStimulusEnd;

        RobotController.OnArrived += HandleArrived;

        BoxClickHandler.OnBoxClicked += OnBoxClicked;

        Debug.Log(canvasRoot.rect.width + "/" + canvasRoot.rect.height);

        sender.SendStimulation(finish); // 세션이 시작됐을 때 뇌파 측정을 시작하기 위해 처음에 종료시킴
    }

    void OnDestroy()
    {
        gaze.OnLocked -= StartExperiment;
        gaze.OnBroken -= AbortExperiment;
        detector.OnDetections -= OnDetections;
        stimulus.OnStimulusEnd -= OnStimulusEnd;
        RobotController.OnArrived -= HandleArrived;
        BoxClickHandler.OnBoxClicked -= OnBoxClicked;
    }

    void StartExperiment()
    {
        experimentRunning = true;
        experimentInitialized = false;
        objectSelected = false;

        helperText.text = "Hold your gaze in one direction";

        tracker = new ObjectTracker();
        filter = new ObjectFilter();

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

        sender.SendStimulation(start);
        detector.EnableDetection(true);
    }

    void AbortExperiment()
    {
        if (!experimentRunning) return;

        sender.SendStimulation(finish);

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
        trackedObjects = filter.Filter(trackedObjects);

        allObjects = new List<ExperimentObject>();
        allObjects.Add(arrowButtonObject);
        allObjects.Add(exitButtonObject);
        allObjects.AddRange(trackedObjects); // id >= 3

        highlighter.UpdateObjects(allObjects);

        helperText.text = "Look at a target or a control button";

        if (!experimentInitialized && allObjects.Count > 0)
        {
            experimentInitialized = true;
            stimulus.StartExperiment(allObjects);
        }
    }

    void OnStimulusEnd()
    {
        if (!experimentRunning) return;

        sender.SendStimulation(finish);

        detector.EnableDetection(false);
        stimulus.ResetExperiment();
        gaze.StopGazeCheck();

        // LDA 결과 요청 시작
        helperText.text = "Processing brain signal...";
        StartCoroutine(WaitForLDAResult());
    }

    IEnumerator WaitForLDAResult()
    {
        int selectedId = -1;
        string errorMessage = "";

        // allObjects.Count를 buttonNum으로 전달
        int buttonNum = allObjects.Count;
        Debug.Log($"[LDA] Total buttons/objects: {buttonNum}");

        // LDA 결과 요청
        yield return StartCoroutine(ldaReceiver.GetResult(
            buttonNum,
            (result) =>
            {
                selectedId = result;
            },
            (error) =>
            {
                errorMessage = error;
            }
        ));

        if (!string.IsNullOrEmpty(errorMessage))
        {
            helperText.text = "Error: " + errorMessage;
            Debug.LogError($"[LDA] Failed: {errorMessage}");
            AbortExperiment();
            yield break;
        }

        if (selectedId < 1 || selectedId > buttonNum)
        {
            helperText.text = $"Error: Invalid result {selectedId} (expected 1-{buttonNum})";
            Debug.LogError($"[LDA] Invalid result: {selectedId}");
            AbortExperiment();
            yield break;
        }

        selectedObject = GetObjectById(selectedId);
        if (selectedObject == null)
        {
            helperText.text = $"Error: Object not found for ID {selectedId}";
            Debug.LogError($"[LDA] Object not found: {selectedId}");
            AbortExperiment();
            yield break;
        }

        objectSelected = true;
        helperText.text = "Target selected: " + selectedObject.label;

        if (navRoutine != null)
            StopCoroutine(navRoutine);

        if (selectedId == 1)
        {
            robot.PublishReset();
            navRoutine = StartCoroutine(MoveToLookingDirection());
        }
        else if (selectedId == 2)
        {
            Application.Quit();
        }
        else
        {
            robot.PublishReset();
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

        helperText.text = "Target selected: " + selectedObject.label;

        if (navRoutine != null)
            StopCoroutine(navRoutine);

        if (objectId == 1)
        {
            robot.PublishReset();
            navRoutine = StartCoroutine(MoveToLookingDirection());
        }
        else if (objectId == 2)
        {
            Application.Quit();
        }
        else
        {
            robot.PublishReset();
            navRoutine = StartCoroutine(RotateThenMoveCoroutine());
        }
    }

    IEnumerator RotateThenMoveCoroutine()
    {
        isMoving = true;

        GameObject box = highlighter.GetBoxFromObjectID(selectedObject.objectId);
        if (box == null)
        {
            Debug.LogError("Box GameObject not found!");
            isMoving = false;
            yield break;
        }

        RectTransform rt = box.GetComponent<RectTransform>();

        Vector3 worldPos = rt.position;
        Vector3 camPos = vrCamera.transform.position;

        Ray ray = new Ray(camPos, (worldPos - camPos).normalized);

        Vector3 baseForward = player.transform.forward;
        Vector3 targetDir = ray.direction;

        float yaw = CalculateSignedYaw(baseForward, targetDir);

        helperText.text = "Robot is moving";

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

        while (isMoving)
        {
            robot.MoveForward();
            yield return null;
        }

        robot.Stop();
    }

    IEnumerator MoveToLookingDirection()
    {
        isMoving = true;

        Vector3 baseForward = player.transform.forward;
        Vector3 camForward = vrCamera.transform.forward;

        float yaw = CalculateSignedYaw(baseForward, camForward);

        helperText.text = "Moving in the looking direction";

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

        while (isMoving)
        {
            robot.MoveForward();
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

    ExperimentObject GetObjectById(int id)
    {
        foreach (ExperimentObject obj in allObjects)
        {
            if (obj.objectId == id) return obj;
        }

        return null;
    }

    void HandleArrived()
    {
        if (isMoving) EmergencyStop();
    }

    void EmergencyStop()
    {
        if (navRoutine != null)
        {
            StopCoroutine(navRoutine);
            navRoutine = null;
        }

        isMoving = false;
        robot.Stop();
        AbortExperiment();
    }
}