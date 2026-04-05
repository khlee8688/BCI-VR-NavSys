using Oculus.Interaction.UnityCanvas;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ExperimentManager : MonoBehaviour
{
    [SerializeField] bool isTest = false;

    [Header("Core")]
    [SerializeField] GazeStabilityDetector gaze;
    [SerializeField] LiveYOLODetector detector;
    [SerializeField] StimulusController stimulus;
    [SerializeField] StimulusSender sender;
    [SerializeField] ObjectHighlighter highlighter;
    [SerializeField] ResultReceiver ldaReceiver;
    [SerializeField] RobotController robot;
    [SerializeField] RectTransform canvasRoot;
    [SerializeField] HelperText helperText;

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

        helperText.Show("Hold your gaze in one direction");

        tracker = new ObjectTracker();
        filter = new ObjectFilter();

        arrowButtonObject = new ExperimentObject
        {
            objectId = 1,
            label = "Arrow_Button",
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

        helperText.Show("Experiment Aborted");

        detector.EnableDetection(false);
        stimulus.ResetExperiment();
        gaze.StartGazeCheck();
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
        allObjects.AddRange(trackedObjects);

        highlighter.UpdateObjects(allObjects);

        if (!experimentInitialized && allObjects.Count > 0)
        {
            experimentInitialized = true;
            StartCoroutine(StartStimulusAfterDelay(3f));
        }
    }

    IEnumerator StartStimulusAfterDelay(float delay)
    {
        helperText.Show("Look at a target or a control button");
        yield return new WaitForSeconds(delay);

        helperText.Hide();
        yield return new WaitForSeconds(delay);

        if (!experimentRunning || objectSelected) yield break;

        stimulus.StartExperiment(allObjects);
    }

    void OnStimulusEnd()
    {
        if (!experimentRunning) return;

        sender.SendStimulation(finish);

        detector.EnableDetection(false);
        stimulus.ResetExperiment();
        gaze.StopGazeCheck();

        helperText.Show("Processing brain signal...");
        StartCoroutine(WaitForLDAResult());
    }

    IEnumerator WaitForLDAResult()
    {
        int selectedId = -1;
        string errorMessage = "";
        int buttonNum = allObjects.Count;

        int[] markerIds = new int[buttonNum];
        for (int i = 0; i < buttonNum; i++)
            markerIds[i] = allObjects[i].objectId;

        Debug.Log($"[LDA] Total buttons/objects: {buttonNum}, marker_ids: [{string.Join(", ", markerIds)}]");

        yield return StartCoroutine(ldaReceiver.GetResult(
            buttonNum,
            markerIds,
            (result) => { selectedId = result; },
            (error) => { errorMessage = error; }
        ));

        if (!string.IsNullOrEmpty(errorMessage))
        {
            helperText.Show("Error: " + errorMessage);
            Debug.LogError($"[LDA] Failed: {errorMessage}");
            AbortExperiment();
            yield break;
        }

        if (!System.Array.Exists(markerIds, id => id == selectedId))
        {
            helperText.Show($"Error: Invalid result {selectedId}");
            Debug.LogError($"[LDA] Invalid result: {selectedId}");
            AbortExperiment();
            yield break;
        }

        selectedObject = GetObjectById(selectedId);
        if (selectedObject == null)
        {
            helperText.Show($"Error: Object not found for ID {selectedId}");
            Debug.LogError($"[LDA] Object not found: {selectedId}");
            AbortExperiment();
            yield break;
        }

        objectSelected = true;

        // 선택된 오브젝트만 3초 표시
        helperText.Show("Target selected: " + selectedObject.label, 3f);
        highlighter.UpdateObjects(new List<ExperimentObject> { selectedObject });
        yield return new WaitForSeconds(3f);
        highlighter.ClearAll();

        if (navRoutine != null)
            StopCoroutine(navRoutine);

        if (isTest)
        {
            Debug.Log($"[Test] Selected: {selectedObject.label} (id={selectedId})");
            experimentRunning = false;
            experimentInitialized = false;
            objectSelected = false;
            gaze.ResetState();
            StartExperiment();
            yield break;
        }

        if (selectedId == 1)
        {
            robot.PublishReset();
            navRoutine = StartCoroutine(MoveToLookingDirection());
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

        helperText.Show("Target selected: " + selectedObject.label, 2f);

        if (navRoutine != null)
            StopCoroutine(navRoutine);

        if (isTest)
        {
            Debug.Log($"[Test] Clicked: {selectedObject.label} (id={objectId})");
            experimentRunning = false;
            experimentInitialized = false;
            objectSelected = false;
            highlighter.ClearAll();
            gaze.ResetState();
            StartExperiment();
            return;
        }

        if (objectId == 1)
        {
            robot.PublishReset();
            navRoutine = StartCoroutine(MoveToLookingDirection());
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

        helperText.Show("Robot is moving", 3f);

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

        helperText.Show("Moving in the looking direction", 3f);

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