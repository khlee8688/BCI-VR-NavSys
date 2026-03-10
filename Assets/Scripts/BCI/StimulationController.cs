using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class StimulusController : MonoBehaviour
{
    [SerializeField] StimulusSender sender;
    [SerializeField] ObjectHighlighter highlighter;

    [Header("Timing")]
    public int totalTrials = 20;
    public float startDelay = 0.0f;
    public float interval = 0.1f;            // stimulus ON time
    public float timeBetweenArrows = 0.1f;   // stimulus OFF gap

    public event Action<int> OnStimulus;
    public event Action OnStimulusEnd;

    readonly Queue<int> queue = new();

    enum State
    {
        Idle,
        StartDelay,
        StimulusOn,
        Gap
    }

    State state = State.Idle;
    float timer = 0f;
    int currentId = -1;
    int targetID = -1;

    public bool IsRunning { get; private set; } = false;

    public void StartExperiment(List<ExperimentObject> objs, int tID = -1)
    {
        queue.Clear();

        int n = objs.Count;
        int m = totalTrials;
        var rnd = new System.Random();

        for (int k = 0; k < m; k++)
        {
            int[] indices = new int[n];
            for (int i = 0; i < n; i++)
                indices[i] = i;

            for (int i = n - 1; i > 0; i--)
            {
                int j = rnd.Next(i + 1);
                (indices[i], indices[j]) = (indices[j], indices[i]);
            }

            for (int i = 0; i < n; i++)
                queue.Enqueue(objs[indices[i]].objectId);
        }

        targetID = tID;
        timer = startDelay;
        state = State.StartDelay;
        IsRunning = true;
    }

    void Update()
    {
        if (state == State.Idle)
        {
            sender.SendStimulation((byte)50);
            return;
        }

        timer -= Time.deltaTime;
        if (timer > 0f) return;

        switch (state)
        {
            case State.StartDelay:
                NextStimulus();
                break;

            case State.StimulusOn:
                highlighter.ClearHighlight();
                timer = timeBetweenArrows;
                state = State.Gap;
                break;

            case State.Gap:
                NextStimulus();
                break;
        }
    }

    void NextStimulus()
    {
        if (queue.Count == 0)
        {
            state = State.Idle;
            currentId = -1;
            IsRunning = false;

            highlighter?.ClearHighlight();
            OnStimulusEnd?.Invoke();
            return;
        }

        currentId = queue.Dequeue();

        OnStimulus?.Invoke(currentId);
        if (targetID == -1) sender?.SendStimulation((byte)currentId);
        else if (currentId == targetID) sender?.SendStimulation((byte)1);
        else sender?.SendStimulation((byte)0);
        highlighter?.Highlight(currentId);

        timer = interval;
        state = State.StimulusOn;
    }

    public void ResetExperiment()
    {
        queue.Clear();
        timer = 0f;
        state = State.Idle;
        currentId = -1;
        IsRunning = false;

        highlighter?.ClearHighlight();
    }
}