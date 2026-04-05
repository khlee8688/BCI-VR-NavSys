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
    [SerializeField] HelperText helperText;

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
    private byte completelyFinished = 49;

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

        for (int i = 0; i < TRAINING_OBJECT_NUM; i++)
        {
            sessionObjects.Add(new ExperimentObject
            {
                objectId = i + 2,
                label = $"Object_{i + 2}",
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

            // highlighter.ClearAll();
        }

        FinishExperiment();
    }

    IEnumerator RunSingleSession()
    {
        sender.SendStimulation(start);
        currentObject = sessionObjects[currentObjectIndex];
        highlighter.UpdateObjects(sessionObjects);
        stimulus.ResetExperiment();

        // 3초 동안 봐야 할 오브젝트 이름 표시
        helperText.Show($"Look at: {currentObject.label}");
        yield return new WaitForSeconds(3f);
        helperText.Hide();
        yield return new WaitForSeconds(5f);
        stimulus.StartExperiment(sessionObjects, currentObject.objectId);

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

        helperText.Show("Training Finished");

        sender.SendStimulation(completelyFinished);
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
