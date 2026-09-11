using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class TrainingManager : MonoBehaviour
{
    [Header("Core")]
    //[SerializeField] StimulusController stimulus;
    [SerializeField] SSVEP_StimulusContorller stimulus;
    //[SerializeField] StimulusSender sender;
    [SerializeField] private SSVEPStimulusSender sender;
    [SerializeField] ObjectHighlighter highlighter;
    [SerializeField] RectTransform canvasRoot;
    [SerializeField] HelperText helperText;
    [SerializeField] private ResultReceiver resultReceiver;// add for result receiving

    [Header("Player")]
    [SerializeField] Camera vrCamera;
    [SerializeField] GameObject player;

    [Header("Timing")]
    [SerializeField] float sessionInterval = 5f; // ���� �� ����

    bool experimentRunning = false;

    // === Objects ===
    List<ExperimentObject> sessionObjects;   // Arrow, Exit ���� �� 7��
    ExperimentObject currentObject;
    int currentObjectIndex = 0;

    const int TRAINING_OBJECT_NUM = 5; // ���� ���� 5�� 자극수ㄴ
    private readonly int[] markerIds = { 2, 3, 4, 5, 6 };//add for ids
    public byte finish = 9;
    public byte start = 8;
    private byte completelyFinished = 49;

    Coroutine sessionCoroutine;

    void Start()
    {
        stimulus.OnStimulusEnd += OnStimulusEnd;

        sessionObjects = new List<ExperimentObject>();

        //sessionObjects.Add(new ExperimentObject
        //{
        //    objectId = 1,
        //    label = "Arrow_Button",
        //    bbox = new Rect()
        //});
        

        for (int i = 0; i < TRAINING_OBJECT_NUM; i++)
        {
            sessionObjects.Add(new ExperimentObject
            {
                objectId = i + 2,
                label = $"Object_{i + 2}",
                bbox = new Rect()
            });
        }

        // �ʱ� ����: ���� ����
        //sender.SendStimulation(finish);

        StartExperiment();
    }

    void OnDestroy()
    {
        stimulus.OnStimulusEnd -= OnStimulusEnd;
    }

    public void StartExperiment()
    {
        if (experimentRunning) return;

        experimentRunning = true;
        currentObjectIndex = 0;

        helperText.Show("Hold your gaze");

        sessionCoroutine = StartCoroutine(SessionLoop());
    }

    IEnumerator SessionLoop()
    {
        while (experimentRunning && currentObjectIndex < sessionObjects.Count)
        {
            if (currentObjectIndex < sessionObjects.Count)
            {
                helperText.Show("Relax");
                yield return new WaitForSeconds(sessionInterval);
            }

            yield return StartCoroutine(RunSingleSession());

            currentObjectIndex++;

            highlighter.ClearAll();
        }

        FinishExperiment();
    }

    IEnumerator RunSingleSession()
    {
        
        currentObject = sessionObjects[currentObjectIndex];
        highlighter.UpdateObjects(sessionObjects);
        stimulus.ResetExperiment();

        // 3�� ���� ���� �� ������Ʈ �̸� ǥ��
        helperText.Show($"Look at: {currentObject.label}");
        sender.SendStimulation(start);//추가
        yield return new WaitForSeconds(1f);
        helperText.Hide();
        yield return new WaitForSeconds(2f);
        stimulus.StartExperiment(sessionObjects, currentObject.objectId);

        while (stimulus.IsRunning)
            yield return null;
        yield return new WaitForSeconds(1f);
        sender.SendStimulation(finish);

        //for ssvep result receiving
        // Python에 FBCCA 분류 요청
        int selectedId = -1;
        string errorMessage = null;

        yield return StartCoroutine(
            resultReceiver.GetResult(
                TRAINING_OBJECT_NUM,
                markerIds,
                (result) => { selectedId = result; },
                (error) => { errorMessage = error; }
            )
        );

        if (errorMessage != null)
        {
            Debug.LogError($"[SSVEP] Error: {errorMessage}");
        }
        else
        {
            Debug.Log($"[SSVEP] 정답 ID: {currentObject.objectId}");
            Debug.Log($"[SSVEP] 분류 ID: {selectedId}");

            if (selectedId == currentObject.objectId)
            {
                Debug.Log("[SSVEP] Correct");
            }
            else if (selectedId == -1)
            {
                Debug.LogWarning("[SSVEP] Rejected");
            }
            else
            {
                Debug.LogWarning("[SSVEP] Incorrect");
            }
        }
    }

    void OnStimulusEnd()
    {
        // ���� ���� ���� ó���� �ڷ�ƾ���� ���
    }

    //void FinishExperiment()
    //{
    //    experimentRunning = false;

    //    sender.SendStimulation(finish);

    //    stimulus.ResetExperiment();
    //    highlighter.ClearAll();

    //    helperText.Show("Training Finished");

    //    sender.SendStimulation(completelyFinished);
    //}

    void FinishExperiment()
    {
        experimentRunning = false;

        //sender.SendStimulation(finish);

        stimulus.ResetExperiment();
        highlighter.ClearAll();

        helperText.Show("Training Finished");
        

        sender.SendStimulation(completelyFinished);

        StartCoroutine(QuitAfterDelay());
    }

    IEnumerator QuitAfterDelay()
    {
        yield return new WaitForSeconds(2f);

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
    Application.Quit();
#endif
    }

    public void AbortExperiment()
    {
        if (!experimentRunning) return;

        experimentRunning = false;

        if (sessionCoroutine != null)
            StopCoroutine(sessionCoroutine);

        sender.SendStimulation(finish);

        stimulus.ResetExperiment();
        highlighter.ClearAll();

        helperText.Show("Experiment Aborted");
    }
}
