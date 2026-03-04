using System;
using System.Collections.Generic;
using Meta.XR.BuildingBlocks.AIBlocks;
using TemporalEcho;
using UnityEngine;

/// <summary>
/// Strict token-based mapping: Screen/Book/Human ONLY. Top-1 per subject by confidence. Edge-trigger (newly detected).
/// Attach to TemporalEchoSystem; assign detectionAgent (ObjectDetectionAgent in scene).
/// </summary>
public class TemporalEchoDetectionBridge : MonoBehaviour
{
    [Header("Detection Source")]
    [Tooltip("ObjectDetectionAgent (e.g. on AI Building Blocks detection GO). Auto-found if null.")]
    [SerializeField] private ObjectDetectionAgent detectionAgent;

    [Header("Strict Token Mapping")]
    [Tooltip("Min confidence (0-1). Labels below this are rejected. 0.35 matches DetectionPrefabReplacer.")]
    [Range(0.1f, 1f)]
    [SerializeField] private float minConfidence = 0.35f;

    [SerializeField] private string[] screenTokens = { "laptop", "screen", "monitor", "computer", "notebook", "tv", "television", "cell phone", "cellphone", "tablet" };
    [SerializeField] private string[] bookTokens = { "book", "newspaper", "magazine" };
    [SerializeField] private string[] humanTokens = { "person", "human", "man", "woman", "face" };

    [Header("Debug")]
    [SerializeField] private bool logAcceptedLabels;
    [SerializeField] private bool logRawLabelsPerSecond;
    [SerializeField] private bool logDetectionHealth = true;

    private TemporalEchoRuntimeOrchestrator _orchestrator;
    private bool _screenWasDetected;
    private bool _bookWasDetected;
    private bool _humanWasDetected;
    private int _lastBatchCount;
    private string _lastRawLabels = "";
    private string _lastAcceptedLabels = "";
    private float _healthLogNextTime;

    public static event Action<TemporalEcho.DetectedSubject, float> OnSubjectDetected;

    private void Awake()
    {
        if (detectionAgent == null)
            detectionAgent = FindAnyObjectByType<ObjectDetectionAgent>();
        _orchestrator = GetComponent<TemporalEchoRuntimeOrchestrator>();
        if (_orchestrator == null)
            _orchestrator = FindAnyObjectByType<TemporalEchoRuntimeOrchestrator>();
    }

    private void Start()
    {
        if (detectionAgent == null)
        {
            detectionAgent = FindAnyObjectByType<ObjectDetectionAgent>();
            if (detectionAgent != null)
            {
                if (isActiveAndEnabled)
                    detectionAgent.OnBoxesUpdated += HandleBatch;
                Debug.Log("[TemporalEcho] Detection bridge: found ObjectDetectionAgent at Start.");
            }
        }
        if (detectionAgent == null)
            Debug.LogWarning("[TemporalEcho] ObjectDetectionAgent not found. Overlays will only show in demo mode (showOverlaysOnStart) or if ReportDetected is called externally.");
    }

    private void OnEnable()
    {
        if (detectionAgent != null)
            detectionAgent.OnBoxesUpdated += HandleBatch;
    }

    private void OnDisable()
    {
        if (detectionAgent != null)
            detectionAgent.OnBoxesUpdated -= HandleBatch;
    }

    private void OnDestroy()
    {
        if (detectionAgent != null)
            detectionAgent.OnBoxesUpdated -= HandleBatch;
    }

    private void HandleBatch(List<BoxData> batch)
    {
        if (batch == null) return;
        _lastBatchCount = batch.Count;
        if (logRawLabelsPerSecond && batch.Count > 0)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < Mathf.Min(5, batch.Count); i++)
                sb.Append(batch[i].label).Append("; ");
            _lastRawLabels = sb.ToString();
        }

        if (batch.Count == 0) return;

        var screenBest = (conf: 0f, area: 0f);
        var bookBest = (conf: 0f, area: 0f);
        var humanBest = (conf: 0f, area: 0f);

        foreach (var b in batch)
        {
            var (baseLabel, confidence) = ExtractLabelAndConfidence(b.label);
            if (confidence < minConfidence) continue;

            float area = (b.scale.x - b.position.x) * (b.scale.y - b.position.y);
            var subject = ClassifyStrict(baseLabel);
            if (subject == null) continue;

            switch (subject.Value)
            {
                case TemporalEcho.DetectedSubject.Screen:
                    if (confidence > screenBest.conf || (Mathf.Approximately(confidence, screenBest.conf) && area > screenBest.area))
                        screenBest = (confidence, area);
                    break;
                case TemporalEcho.DetectedSubject.Book:
                    if (confidence > bookBest.conf || (Mathf.Approximately(confidence, bookBest.conf) && area > bookBest.area))
                        bookBest = (confidence, area);
                    break;
                case TemporalEcho.DetectedSubject.Human:
                    if (confidence > humanBest.conf || (Mathf.Approximately(confidence, humanBest.conf) && area > humanBest.area))
                        humanBest = (confidence, area);
                    break;
            }
        }

        var accepted = new List<string>();
        if (screenBest.conf >= minConfidence)
        {
            accepted.Add("Screen:" + screenBest.conf.ToString("F2"));
            if (!_screenWasDetected)
            {
                _screenWasDetected = true;
                OnSubjectDetected?.Invoke(TemporalEcho.DetectedSubject.Screen, screenBest.conf);
                if (_orchestrator != null) _orchestrator.ReportDetected(TemporalEcho.DetectedSubject.Screen);
                if (logAcceptedLabels) Debug.Log("[TemporalEcho] Edge-trigger Screen (conf=" + screenBest.conf + ")");
            }
        }
        if (bookBest.conf >= minConfidence)
        {
            accepted.Add("Book:" + bookBest.conf.ToString("F2"));
            if (!_bookWasDetected)
            {
                _bookWasDetected = true;
                OnSubjectDetected?.Invoke(TemporalEcho.DetectedSubject.Book, bookBest.conf);
                if (_orchestrator != null) _orchestrator.ReportDetected(TemporalEcho.DetectedSubject.Book);
                if (logAcceptedLabels) Debug.Log("[TemporalEcho] Edge-trigger Book (conf=" + bookBest.conf + ")");
            }
        }
        if (humanBest.conf >= minConfidence)
        {
            accepted.Add("Human:" + humanBest.conf.ToString("F2"));
            if (!_humanWasDetected)
            {
                _humanWasDetected = true;
                OnSubjectDetected?.Invoke(TemporalEcho.DetectedSubject.Human, humanBest.conf);
                if (_orchestrator != null) _orchestrator.ReportDetected(TemporalEcho.DetectedSubject.Human);
                if (logAcceptedLabels) Debug.Log("[TemporalEcho] Edge-trigger Human (conf=" + humanBest.conf + ")");
            }
        }
        _lastAcceptedLabels = string.Join(", ", accepted);
    }

    /// <summary>Strict: only maps tokens in screenTokens/bookTokens/humanTokens. Returns null for lamp, etc.</summary>
    private TemporalEcho.DetectedSubject? ClassifyStrict(string label)
    {
        if (string.IsNullOrWhiteSpace(label)) return null;
        string lower = label.Trim().ToLowerInvariant();
        foreach (var t in screenTokens)
            if (!string.IsNullOrEmpty(t) && lower.Contains(t.ToLowerInvariant())) return TemporalEcho.DetectedSubject.Screen;
        foreach (var t in bookTokens)
            if (!string.IsNullOrEmpty(t) && lower.Contains(t.ToLowerInvariant())) return TemporalEcho.DetectedSubject.Book;
        foreach (var t in humanTokens)
            if (!string.IsNullOrEmpty(t) && lower.Contains(t.ToLowerInvariant())) return TemporalEcho.DetectedSubject.Human;
        return null;
    }

    private static (string baseLabel, float confidence) ExtractLabelAndConfidence(string label)
    {
        if (string.IsNullOrEmpty(label)) return ("", 0f);
        int lastSpace = label.LastIndexOf(' ');
        if (lastSpace < 0) return (label.Trim(), 1f);
        string baseLabel = label.Substring(0, lastSpace).Trim();
        if (!float.TryParse(label.Substring(lastSpace + 1).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float conf))
            return (label.Trim(), 1f);
        return (baseLabel, conf);
    }

    private void Update()
    {
        if (!logDetectionHealth || !Application.isPlaying) return;
        if (Time.time < _healthLogNextTime) return;
        _healthLogNextTime = Time.time + 1f;
        bool ok = detectionAgent != null;
        string raw = logRawLabelsPerSecond ? " raw=\"" + _lastRawLabels + "\"" : "";
        Debug.Log("[TemporalEcho] Detection health: agent=" + ok + " batchCount=" + _lastBatchCount + " accepted=[" + _lastAcceptedLabels + "]" + raw);
    }

    public void SetDetectionAgent(ObjectDetectionAgent agent) => detectionAgent = agent;
}
