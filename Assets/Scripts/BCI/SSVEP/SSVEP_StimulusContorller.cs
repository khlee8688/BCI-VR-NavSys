using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SSVEP_StimulusContorller : MonoBehaviour
{
    [Serializable]
    public class TargetFrequency
    {
        public int objectId;
        public float frequency;
    }

    [SerializeField] private SSVEPStimulusSender sender;
    [SerializeField] private ObjectHighlighter highlighter;

    [Header("Sender")]
    public string host = "127.0.0.1";
    public int portNo = 12140;

    [Header("Timing")]
    [Min(0f)] public float startDelay = 0f;
    [Min(0.1f)] public float flickerDuration = 5f;
    [Min(1f)] public float refreshRate = 72f;

    [Header("Target frequencies")]
    [SerializeField] private List<TargetFrequency> targetFrequencies = new()
    {
        //new TargetFrequency { objectId = 2, frequency = 60f / 9f },
        //new TargetFrequency { objectId = 3, frequency = 60f / 8f },
        //new TargetFrequency { objectId = 4, frequency = 60f / 7f },
        //new TargetFrequency { objectId = 5, frequency = 60f / 6f },
        //new TargetFrequency { objectId = 6, frequency = 60f / 5f }

        new TargetFrequency { objectId = 2, frequency = 72f / 5f },
        new TargetFrequency { objectId = 3, frequency = 72f / 6f },
        new TargetFrequency { objectId = 4, frequency = 72f / 7f },
        new TargetFrequency { objectId = 5, frequency = 72f / 8f },
        new TargetFrequency { objectId = 6, frequency = 72f / 11f }
    };

    [Header("Flicker colors")]
    [SerializeField] private Color onColor = Color.white;
    [SerializeField] private Color offColor = Color.clear;

    public event Action<int> OnStimulus;
    public event Action OnStimulusEnd;

    private readonly Dictionary<int, Graphic> targetGraphics = new();
    private readonly Dictionary<int, int> periodFrames = new();

    private List<ExperimentObject> activeObjects;
    private float stateTimer;
    private int flickerStartFrame;
    private int targetId = -1;

    private int object2FlashCount;
    private bool object2WasOn;
    private double diagnosticStartTime;

    private enum State
    {
        Idle,
        StartDelay,
        Flickering
    }

    private State state = State.Idle;

    public bool IsRunning { get; private set; }

    private void Start()
    {
        if (sender != null && !sender.IsConnected)
            sender.Open();
    }

    public void StartExperiment(List<ExperimentObject> objs, int tID = -1)
    {
        ResetExperiment();

        if (objs == null || objs.Count == 0)
        {
            Debug.LogWarning("[SSVEP] No stimulus objects were supplied.");
            OnStimulusEnd?.Invoke();
            return;
        }

        activeObjects = new List<ExperimentObject>(objs);
        targetId = tID;

        CacheTargetGraphics();
        BuildFramePeriods();
        SetAllTargets(false);

        stateTimer = startDelay;
        state = startDelay > 0f ? State.StartDelay : State.Flickering;
        IsRunning = true;

        if (state == State.Flickering)
            BeginFlicker();
    }

    private void Awake()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 72;
        Application.runInBackground = true;
    }

    private void Update()
    {
        if (state == State.Idle)
            return;

        if (state == State.StartDelay)
        {
            stateTimer -= Time.unscaledDeltaTime;
            if (stateTimer <= 0f)
            {
                state = State.Flickering;
                BeginFlicker();
            }

            return;
        }

        stateTimer -= Time.unscaledDeltaTime;
        if (stateTimer <= 0f)
        {
            FinishFlicker();
            return;
        }

        int elapsedFrames = Time.frameCount - flickerStartFrame;

        foreach (ExperimentObject obj in activeObjects)
        {
            if (!periodFrames.TryGetValue(obj.objectId, out int period))
                continue;

            bool isOn = elapsedFrames % period < period / 2f;
            SetTargetState(obj.objectId, isOn);
        }
    }
    
    //private void BeginFlicker()
    //{
    //    flickerStartFrame = Time.frameCount;
    //    stateTimer = flickerDuration;

    //    sender?.SendFlickerStart();

    //    OnStimulus?.Invoke(targetId);

    //    foreach (ExperimentObject obj in activeObjects)
    //        SetTargetState(obj.objectId, true);
    //}
    private void BeginFlicker()
    {
        flickerStartFrame = Time.frameCount;
        stateTimer = flickerDuration;

        object2FlashCount = 0;
        object2WasOn = false;
        diagnosticStartTime = Time.unscaledTimeAsDouble;

        sender?.SendFlickerStart();

        OnStimulus?.Invoke(targetId);

        foreach (ExperimentObject obj in activeObjects)
            SetTargetState(obj.objectId, true);
    }

    private void FinishFlicker()
    {
        SetAllTargets(false);

        //COUNT FLICKER
        double elapsed = Time.unscaledTimeAsDouble - diagnosticStartTime;
        int frames = Time.frameCount - flickerStartFrame;
        double averageFps = elapsed > 0 ? frames / elapsed : 0;
        double measuredHz = elapsed > 0 ? object2FlashCount / elapsed : 0;

        Debug.Log(
            $"[SSVEP DIAG] Object 2: flashes={object2FlashCount}, " +
            $"elapsed={elapsed:F3}s, measured={measuredHz:F3}Hz, " +
            $"frames={frames}, averageFPS={averageFps:F1}"
        );
        Debug.Log(
            $"[FPS CONFIG] target={Application.targetFrameRate}, " +
            $"vSync={QualitySettings.vSyncCount}, " +
            $"displayHz={Screen.currentResolution.refreshRateRatio.value:F1}"
        );
        sender?.SendFlickerEnd();

        state = State.Idle;
        IsRunning = false;
        targetId = -1;

        OnStimulusEnd?.Invoke();
    }

    private void BuildFramePeriods()
    {
        periodFrames.Clear();

        foreach (ExperimentObject obj in activeObjects)
        {
            float frequency = GetFrequency(obj.objectId);
            if (frequency <= 0f)
            {
                Debug.LogWarning($"[SSVEP] Invalid frequency for object {obj.objectId}.");
                continue;
            }

            periodFrames[obj.objectId] = Mathf.Max(
                2,
                Mathf.RoundToInt(refreshRate / frequency)
            );
        }
    }

    private float GetFrequency(int objectId)
    {
        foreach (TargetFrequency setting in targetFrequencies)
        {
            if (setting.objectId == objectId)
                return setting.frequency;
        }

        Debug.LogWarning($"[SSVEP] Frequency is not configured for object {objectId}.");
        return 0f;
    }

    private void CacheTargetGraphics()
    {
        targetGraphics.Clear();

        Graphic[] graphics = highlighter.canvasRoot.GetComponentsInChildren<Graphic>(true);

        foreach (ExperimentObject obj in activeObjects)
        {
            string expectedParent = obj.objectId == 1
                ? "arrowKey"
                : $"Object_{obj.objectId}";

            foreach (Graphic graphic in graphics)
            {
                if (graphic.name == "HighlightImage" &&
                    graphic.transform.parent != null &&
                    graphic.transform.parent.name == expectedParent)
                {
                    targetGraphics[obj.objectId] = graphic;
                    break;
                }
            }

            if (targetGraphics.ContainsKey(obj.objectId))
                continue;

            GameObject box = highlighter.GetBoxFromObjectID(obj.objectId);
            Graphic fallback = box != null
                ? box.transform.Find("Highlight")?.GetComponent<Graphic>()
                : null;

            if (fallback != null)
                targetGraphics[obj.objectId] = fallback;
            else
                Debug.LogWarning($"[SSVEP] Highlight graphic not found for object {obj.objectId}.");
        }
    }

    //private void SetTargetState(int objectId, bool isOn)
    //{
    //    if (targetGraphics.TryGetValue(objectId, out Graphic graphic))
    //        graphic.color = isOn ? onColor : offColor;
    //}
    private void SetTargetState(int objectId, bool isOn)
    {
        if (objectId == 2)
        {
            if (isOn && !object2WasOn)
                object2FlashCount++;

            object2WasOn = isOn;
        }

        if (targetGraphics.TryGetValue(objectId, out Graphic graphic))
            graphic.color = isOn ? onColor : offColor;
    }

    private void SetAllTargets(bool isOn)
    {
        foreach (Graphic graphic in targetGraphics.Values)
            graphic.color = isOn ? onColor : offColor;
    }

    public void ResetExperiment()
    {
        SetAllTargets(false);

        state = State.Idle;
        stateTimer = 0f;
        flickerStartFrame = 0;
        targetId = -1;
        IsRunning = false;
        activeObjects = null;
    }
}
