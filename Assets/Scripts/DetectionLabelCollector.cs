using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Meta.XR.BuildingBlocks.AIBlocks;
using UnityEngine;

/// <summary>Token plus count and up to 3 sample full labels for Recognition UI.</summary>
public readonly struct TokenInfo
{
    public readonly string token;
    public readonly int count;
    public readonly string sample1;
    public readonly string sample2;
    public readonly string sample3;

    public TokenInfo(string token, int count, string s1, string s2, string s3)
    {
        this.token = token ?? "";
        this.count = count;
        this.sample1 = s1 ?? "";
        this.sample2 = s2 ?? "";
        this.sample3 = s3 ?? "";
    }
}

/// <summary>
/// Collects detection labels at runtime: token (first word) with count and lastSeenTime,
/// plus up to 20 full label samples and up to 3 samples per token for Recognition view.
/// </summary>
[RequireComponent(typeof(ObjectDetectionAgent))]
public class DetectionLabelCollector : MonoBehaviour
{
    private const int OverlayMaxLabels = 12;
    private const int MaxFullLabelSamples = 20;
    private const int MaxSamplesPerToken = 3;

    private static readonly Dictionary<string, (int count, float lastSeenTime)> s_tokenData = new Dictionary<string, (int, float)>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, List<string>> s_tokenSamples = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
    private static readonly List<string> s_fullLabelSamples = new List<string>(MaxFullLabelSamples);

    [Tooltip("Show count + top tokens in overlay (debug).")]
    [SerializeField] private bool showLabelOverlay;

    private ObjectDetectionAgent _agent;

    public static int TotalUniqueTokens => s_tokenData.Count;
    public static IReadOnlyList<(string token, int count, float lastSeenTime)> TopTokens => GetTopTokensWithTime(12);

    /// <summary>Top n tokens by count. For overlay and export.</summary>
    public static IReadOnlyList<(string token, int count)> GetTopTokens(int n)
    {
        return s_tokenData
            .OrderByDescending(kv => kv.Value.count)
            .Take(n)
            .Select(kv => (kv.Key, kv.Value.count))
            .ToList();
    }

    /// <summary>Top tokens for a page (by count desc). Each entry has token, count, and up to 3 sample full labels.</summary>
    public static IReadOnlyList<TokenInfo> GetTopTokensPaged(int page, int pageSize)
    {
        if (page < 0 || pageSize <= 0) return Array.Empty<TokenInfo>();
        var ordered = s_tokenData.OrderByDescending(kv => kv.Value.count).ToList();
        int total = ordered.Count;
        int start = page * pageSize;
        if (start >= total) return Array.Empty<TokenInfo>();
        int take = Math.Min(pageSize, total - start);
        var list = new List<TokenInfo>(take);
        for (int i = start; i < start + take; i++)
        {
            var kv = ordered[i];
            string token = kv.Key;
            int count = kv.Value.count;
            string s1 = "", s2 = "", s3 = "";
            if (s_tokenSamples.TryGetValue(token, out var samples) && samples.Count > 0)
            {
                s1 = samples[0];
                if (samples.Count > 1) s2 = samples[1];
                if (samples.Count > 2) s3 = samples[2];
            }
            list.Add(new TokenInfo(token, count, s1, s2, s3));
        }
        return list;
    }

    /// <summary>Up to n sample full labels (as received from detector).</summary>
    public static IReadOnlyList<string> GetSampleFullLabels(int n)
    {
        int take = Math.Min(n, s_fullLabelSamples.Count);
        if (take <= 0) return Array.Empty<string>();
        var list = new List<string>(take);
        for (int i = 0; i < take; i++)
            list.Add(s_fullLabelSamples[i]);
        return list;
    }
    public static int LastBatchRawCount { get; private set; }
    public static int LastBatchPassedCount { get; private set; }

    public static IReadOnlyCollection<string> GetSeenLabels()
    {
        return s_tokenData.Keys.ToList();
    }

    public static int SeenCount => s_tokenData.Count;

    /// <summary>Enable or disable label collection (e.g. for test mode). Simple toggle; does not unsubscribe events.</summary>
    public void SetEnabled(bool enabled) => this.enabled = enabled;

    private static List<(string token, int count, float lastSeenTime)> GetTopTokensWithTime(int n)
    {
        return s_tokenData
            .OrderByDescending(kv => kv.Value.count)
            .Take(n)
            .Select(kv => (kv.Key, kv.Value.count, kv.Value.lastSeenTime))
            .ToList();
    }

    private void Awake()
    {
        _agent = GetComponent<ObjectDetectionAgent>();
    }

    private void OnEnable()
    {
        if (_agent != null)
            _agent.OnBoxesUpdated += OnBatch;
    }

    private void OnDisable()
    {
        if (_agent != null)
            _agent.OnBoxesUpdated -= OnBatch;
    }

    private void OnBatch(List<BoxData> batch)
    {
        if (batch == null) return;
        LastBatchRawCount = batch.Count;
        float t = Time.time;
        int passed = 0;
        foreach (var b in batch)
        {
            string raw = b.label;
            if (string.IsNullOrWhiteSpace(raw)) continue;
            string token = raw.Trim().Split(' ')[0].Trim().ToLowerInvariant();
            if (token.Length > 0)
            {
                passed++;
                if (s_tokenData.TryGetValue(token, out var prev))
                    s_tokenData[token] = (prev.count + 1, t);
                else
                    s_tokenData[token] = (1, t);
                if (!s_tokenSamples.TryGetValue(token, out var samples))
                {
                    samples = new List<string>(MaxSamplesPerToken);
                    s_tokenSamples[token] = samples;
                }
                if (!samples.Contains(raw))
                {
                    if (samples.Count >= MaxSamplesPerToken)
                        samples.RemoveAt(0);
                    samples.Add(raw);
                }
            }
            if (s_fullLabelSamples.Count < MaxFullLabelSamples && !s_fullLabelSamples.Contains(raw))
                s_fullLabelSamples.Add(raw);
        }
        LastBatchPassedCount = passed;
    }

    private void OnGUI()
    {
        if (!showLabelOverlay || s_tokenData.Count == 0) return;
        var top = GetTopTokensWithTime(OverlayMaxLabels);
        string line = "Tokens: " + TotalUniqueTokens + " — " + string.Join(", ", top.Select(x => x.token + "(" + x.count + ")"));
        GUI.Label(new Rect(10, 10, 900, 22), line);
    }

    /// <summary>
    /// Writes seen tokens (by count desc, with last seen) and full label samples to a file.
    /// On device: Application.persistentDataPath/SeenLabels_&lt;timestamp&gt;.txt
    /// In editor: Assets/Logs/SeenLabels_&lt;timestamp&gt;.txt and copies content to clipboard.
    /// </summary>
    public static string ExportToFile()
    {
        string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string dir;
        if (Application.isEditor)
        {
            dir = Path.Combine(Application.dataPath, "Logs");
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
        else
        {
            dir = Application.persistentDataPath;
        }
        string path = Path.Combine(dir, "SeenLabels_" + ts + ".txt");

        var sorted = s_tokenData.OrderByDescending(kv => kv.Value.count).ToList();
        var samples = s_fullLabelSamples.AsReadOnly();

        using (var w = new StreamWriter(path))
        {
            w.WriteLine("# AI detection labels — Temporal Echo");
            w.WriteLine("# Total unique tokens: " + s_tokenData.Count);
            w.WriteLine("# Exported: " + DateTime.Now.ToString("O"));
            w.WriteLine();

            w.WriteLine("# Tokens (by count desc, last seen time)");
            foreach (var kv in sorted)
                w.WriteLine(kv.Key + "\t" + kv.Value.count + "\t" + kv.Value.lastSeenTime.ToString("F1"));
            w.WriteLine();

            w.WriteLine("# Sample full labels (up to " + MaxFullLabelSamples + ")");
            foreach (string full in samples)
                w.WriteLine(full);
        }

#if UNITY_EDITOR
        string clip = "# Total unique tokens: " + s_tokenData.Count + "\n" +
                      string.Join("\n", sorted.Select(kv => kv.Key + "\t" + kv.Value.count + "\t" + kv.Value.lastSeenTime.ToString("F1"))) +
                      "\n\n# Sample full labels\n" + string.Join("\n", samples);
        UnityEditor.EditorGUIUtility.systemCopyBuffer = clip;
#endif
        return path;
    }
}
