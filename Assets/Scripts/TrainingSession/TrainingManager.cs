using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class TrainingManager : MonoBehaviour
{
    [Header("Core")]
    [SerializeField] StimulusController stimulus;
    [SerializeField] StimulusSender sender;
    [SerializeField] ObjectHighlighter highlighter;
    [SerializeField] RectTransform canvasRoot;
    [SerializeField] TMP_Text helperText;

    [Header("Player")]
    [SerializeField] Camera vrCamera;
    [SerializeField] GameObject player;

    [Header("Timing")]
    [SerializeField] float sessionInterval = 10f; // 세션 간 간격

    bool experimentRunning = false;

    // === Objects ===
    List<ExperimentObject> sessionObjects;   // Arrow, Exit 포함 총 7개
    ExperimentObject currentObject;
    int currentObjectIndex = 0;

    const int TRAINING_OBJECT_NUM = 5; // 동적 생성 5개

    public byte finish = 9;
    public byte start = 8;

    Coroutine sessionCoroutine;

    void Start()
    {
        stimulus.OnStimulusEnd += OnStimulusEnd;

        sessionObjects = new List<ExperimentObject>();

        sessionObjects.Add(new ExperimentObject
        {
            objectId = 1,
            label = "Arrow_Button",
            bbox = new Rect()
        });

        sessionObjects.Add(new ExperimentObject
        {
            objectId = 2,
            label = "Exit_Button",
            bbox = new Rect()
        });

        for (int i = 0; i < TRAINING_OBJECT_NUM; i++)
        {
            sessionObjects.Add(new ExperimentObject
            {
                objectId = i + 3,
                label = $"Object_{i + 3}",
                bbox = new Rect()
            });
        }

        // 초기 상태: 측정 차단
        sender.SendStimulation(finish);

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

        helperText.text = "Hold your gaze";

        sessionCoroutine = StartCoroutine(SessionLoop());
    }

    IEnumerator SessionLoop()
    {
        while (experimentRunning && currentObjectIndex < sessionObjects.Count)
        {
            if (currentObjectIndex < sessionObjects.Count)
            {
                helperText.text = "Relax";
                yield return new WaitForSeconds(sessionInterval);
            }

            yield return StartCoroutine(RunSingleSession());

            currentObjectIndex++;

            // highlighter.ClearAll();
        }

        FinishExperiment();
    }

    IEnumerator RunSingleSession()
    {
        sender.SendStimulation(start);

        currentObject = sessionObjects[currentObjectIndex];

        highlighter.UpdateObjects(sessionObjects); // 항상 7개 전부 표시

        stimulus.ResetExperiment();
        stimulus.StartExperiment(sessionObjects, currentObject.objectId);

        helperText.text = $"Focus on {currentObject.label}";

        // OnStimulusEnd에서 끝날 때까지 대기
        while (stimulus.IsRunning)
            yield return null;

        sender.SendStimulation(finish);
    }

    void OnStimulusEnd()
    {
        // 실제 세션 종료 처리는 코루틴에서 담당
    }

    void FinishExperiment()
    {
        experimentRunning = false;

        sender.SendStimulation(finish);

        stimulus.ResetExperiment();
        highlighter.ClearAll();

        helperText.text = "Training Finished";
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

        helperText.text = "Experiment Aborted";
    }
}
