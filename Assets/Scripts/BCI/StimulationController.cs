using System;
using System.Collections.Generic;
using UnityEngine;

public class StimulusController : MonoBehaviour
{
    public StimulusSender sender;
    public ObjectHighlighter highlighter;

    [Tooltip("한 세트(n개 전체 랜덤)를 몇 번 반복할지")]
    public int totalTrials = 20;
    public float interval = 0.1f;

    public event Action<int> OnStimulus;

    readonly Queue<int> queue = new();
    float timer;

    public void StartExperiment(List<ExperimentObject> objs)
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

        timer = 0f;
    }

    void Update()
    {
        if (queue.Count == 0) return;

        timer -= Time.deltaTime;
        if (timer > 0f) return;

        int id = queue.Dequeue();

        OnStimulus?.Invoke(id);
        sender?.SendStimulation(id);
        highlighter?.Highlight(id);

        timer = interval;
    }

    public void ResetExperiment()
    {
        queue.Clear();
        timer = 0f;
    }
}