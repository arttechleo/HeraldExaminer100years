using System.Linq;
using System.Text;
using Meta.XR.BuildingBlocks.AIBlocks;
using UnityEngine;

/// <summary>
/// World-space status panel: era, detection counts, top tokens, active audio, persistent props, warnings.
/// Labels view mode: TotalUniqueTokens, Top 20 tokens (paged), up to 10 sample full labels.
/// Toggle visibility: default ON in DEVELOPMENT_BUILD, OFF in release; or hold pinch 2s to toggle.
/// </summary>
public class MRStatusOverlay : MonoBehaviour
{
    private const int LabelsPageSize = 20;
    private const int LabelsSampleCount = 10;
    private const int LabelsMaxTokens = 500;
    private const int RecognitionPageSize = 15;

    [Tooltip("World-space offset from parent (or origin).")]
    [SerializeField] private Vector3 panelOffset = new Vector3(0f, -0.4f, 0f);
    [Tooltip("Default visible in dev builds. OFF by default so exhibit feels clean; use 2s pinch to show.")]
    [SerializeField] private bool defaultVisibleInDev = false;

    private GameObject _panelRoot;
    private TextMesh _textMesh;
    private bool _visible;
    private float _pinchHoldTime;
    private const float PinchHoldThreshold = 2f;
    private bool _labelsViewMode;
    private int _labelsPage;
    private bool _recognitionViewMode;
    private int _recognitionPage;

    private void Start()
    {
#if DEVELOPMENT_BUILD
        _visible = defaultVisibleInDev;
#else
        _visible = false;
#endif
        BuildPanel();
    }

    private void BuildPanel()
    {
        _panelRoot = new GameObject("MRStatusOverlay_Panel");
        _panelRoot.transform.SetParent(transform);
        _panelRoot.transform.localPosition = panelOffset;
        _panelRoot.transform.localRotation = Quaternion.identity;
        _panelRoot.transform.localScale = Vector3.one * 0.4f;

        var go = new GameObject("Text");
        go.transform.SetParent(_panelRoot.transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        _textMesh = go.AddComponent<TextMesh>();
        _textMesh.anchor = TextAnchor.UpperLeft;
        _textMesh.alignment = TextAlignment.Left;
        _textMesh.fontSize = 24;
        _textMesh.characterSize = 0.02f;
        _panelRoot.SetActive(_visible);

        CreateLabelsPageButtons();
    }

    private void CreateLabelsPageButtons()
    {
        int layer = LayerMask.NameToLayer(EraSwitchPanel.UILayerName);
        if (layer < 0) return;
        var nextGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        nextGo.name = "LabelsNext";
        nextGo.transform.SetParent(_panelRoot.transform, false);
        nextGo.transform.localPosition = new Vector3(0.2f, -0.35f, 0f);
        nextGo.transform.localScale = new Vector3(0.08f, 0.04f, 0.01f);
        nextGo.layer = layer;
        nextGo.SetActive(false);
        var nextBtn = nextGo.AddComponent<LabelsPageButton>();
        nextBtn.isNext = true;
        nextBtn.overlay = this;
        _labelsNextGo = nextGo;
        var prevGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        prevGo.name = "LabelsPrev";
        prevGo.transform.SetParent(_panelRoot.transform, false);
        prevGo.transform.localPosition = new Vector3(-0.2f, -0.35f, 0f);
        prevGo.transform.localScale = new Vector3(0.08f, 0.04f, 0.01f);
        prevGo.layer = layer;
        prevGo.SetActive(false);
        var prevBtn = prevGo.AddComponent<LabelsPageButton>();
        prevBtn.isNext = false;
        prevBtn.overlay = this;
        _labelsPrevGo = prevGo;
    }

    private GameObject _labelsNextGo;
    private GameObject _labelsPrevGo;

    private void Update()
    {
        if (_panelRoot == null) return;

        bool pinching = false;
        var hands = FindObjectsByType<OVRHand>(FindObjectsSortMode.None);
        foreach (var h in hands)
        {
            if (h.GetFingerIsPinching(OVRHand.HandFinger.Index)) { pinching = true; break; }
        }
        if (pinching)
        {
            _pinchHoldTime += Time.deltaTime;
            if (_pinchHoldTime >= PinchHoldThreshold)
            {
                _visible = !_visible;
                _panelRoot.SetActive(_visible);
                _pinchHoldTime = 0f;
            }
        }
        else
            _pinchHoldTime = 0f;

        if (_recognitionViewMode)
            _textMesh.text = BuildRecognitionText();
        else if (_labelsViewMode)
            _textMesh.text = BuildLabelsText();
        else
            _textMesh.text = BuildStatusText();
        bool anyPageMode = _labelsViewMode || _recognitionViewMode;
        if (_labelsNextGo != null) _labelsNextGo.SetActive(_visible && anyPageMode);
        if (_labelsPrevGo != null) _labelsPrevGo.SetActive(_visible && anyPageMode);
    }

    private string BuildRecognitionText()
    {
        var sb = new StringBuilder();
        int total = DetectionLabelCollector.TotalUniqueTokens;
        sb.AppendLine("--- RECOGNITION ---");
        sb.AppendLine("Total tokens: " + total);
        sb.AppendLine("Screen map: " + DetectionCategoryFilter.ScreenTokensHint);
        int totalPages = total <= 0 ? 1 : (total + RecognitionPageSize - 1) / RecognitionPageSize;
        _recognitionPage = Mathf.Clamp(_recognitionPage, 0, totalPages - 1);
        sb.AppendLine("Page " + (_recognitionPage + 1) + "/" + totalPages);
        var page = DetectionLabelCollector.GetTopTokensPaged(_recognitionPage, RecognitionPageSize);
        foreach (var info in page)
        {
            var cat = DetectionCategoryFilter.Classify(info.token);
            string catStr = cat == DetectionCategoryFilter.Category.None ? "Unknown" : cat.ToString();
            sb.AppendLine(info.token + " (" + info.count + ") → " + catStr);
            if (!string.IsNullOrEmpty(info.sample1)) sb.AppendLine("  " + info.sample1);
            if (!string.IsNullOrEmpty(info.sample2)) sb.AppendLine("  " + info.sample2);
            if (!string.IsNullOrEmpty(info.sample3)) sb.AppendLine("  " + info.sample3);
        }
        return sb.ToString();
    }

    private string BuildLabelsText()
    {
        var sb = new StringBuilder();
        int total = DetectionLabelCollector.TotalUniqueTokens;
        sb.AppendLine("--- Labels (what AI sees) ---");
        sb.AppendLine("Unique tokens: " + total);
        var all = DetectionLabelCollector.GetTopTokens(LabelsMaxTokens);
        int totalPages = total <= 0 ? 1 : (total + LabelsPageSize - 1) / LabelsPageSize;
        _labelsPage = Mathf.Clamp(_labelsPage, 0, totalPages - 1);
        int start = _labelsPage * LabelsPageSize;
        sb.AppendLine("Page " + (_labelsPage + 1) + "/" + totalPages);
        for (int i = start; i < Mathf.Min(start + LabelsPageSize, all.Count); i++)
        {
            var t = all[i];
            sb.AppendLine("  " + t.token + ": " + t.count);
        }
        sb.AppendLine("--- Sample full labels ---");
        var samples = DetectionLabelCollector.GetSampleFullLabels(LabelsSampleCount);
        for (int i = 0; i < samples.Count; i++)
            sb.AppendLine("  " + samples[i]);
        return sb.ToString();
    }

    public void NextPage()
    {
        if (_recognitionViewMode) _recognitionPage++;
        else _labelsPage++;
    }

    public void PrevPage()
    {
        if (_recognitionViewMode) _recognitionPage = Mathf.Max(0, _recognitionPage - 1);
        else _labelsPage = Mathf.Max(0, _labelsPage - 1);
    }

    public void SetLabelsView(bool on)
    {
        _labelsViewMode = on;
        if (on) { _labelsPage = 0; _recognitionViewMode = false; }
    }

    public void SetRecognitionView(bool on)
    {
        _recognitionViewMode = on;
        if (on) { _recognitionPage = 0; _labelsViewMode = false; }
    }

    public bool LabelsViewMode => _labelsViewMode;
    public bool RecognitionViewMode => _recognitionViewMode;

    private string BuildStatusText()
    {
        var sb = new StringBuilder();

        var eraManager = FindAnyObjectByType<TemporalEraManager>();
        bool hasEraManager = eraManager != null;
        bool hasEraConfig = hasEraManager && eraManager.HasEraConfig;

        sb.AppendLine("Era: " + (hasEraManager ? eraManager.CurrentEra.ToString() : "?"));
        sb.AppendLine("EraConfig assigned? " + (hasEraConfig ? "yes" : "no"));

        var persistent = FindAnyObjectByType<PersistentDiscoveryManager>();
        sb.AppendLine("PDM present? " + (persistent != null ? "yes" : "no"));

        int raw = DetectionLabelCollector.LastBatchRawCount;
        int passed = DetectionLabelCollector.LastBatchPassedCount;
        sb.AppendLine("Detection: raw=" + raw + " passed=" + passed);
        if (persistent != null)
        {
            sb.Append("Props: S=" + persistent.GetCountForCategory("Screen") + " B=" + persistent.GetCountForCategory("Book"));
            sb.Append(" C=" + persistent.GetCountForCategory("Chair") + " D=" + persistent.GetCountForCategory("Desk"));
            sb.Append(" K=" + persistent.GetCountForCategory("Keyboard") + " M=" + persistent.GetCountForCategory("Mouse"));
            sb.Append(" P=" + persistent.GetCountForCategory("Phone") + " Cu=" + persistent.GetCountForCategory("Cup"));
            sb.AppendLine();
        }

        var eraPanel = FindAnyObjectByType<EraSwitchPanel>();
        sb.AppendLine("UI Panel present? " + (eraPanel != null && eraPanel.IsPanelVisible ? "yes" : "no"));
        sb.AppendLine("UI Ray Mask: " + PinchRayUIInteractor.GetUILayerMaskDebugNames());
        sb.AppendLine("Last UI hit: " + (string.IsNullOrEmpty(PinchRayUIInteractor.LastUIHitName) ? "-" : PinchRayUIInteractor.LastUIHitName));

        var focus = FindAnyObjectByType<AudioFocusManager>();
        string micName = focus != null ? focus.ActiveAudioSourceName : "none";
        string clipName = focus != null ? focus.ActiveClipName : "none";
        sb.AppendLine("Last mic: " + micName + " | clip: " + clipName);

        int uiLayer = LayerMask.NameToLayer(EraSwitchPanel.UILayerName);
        if (uiLayer < 0)
            sb.AppendLine("WARN: UI layer missing — run Tools > Temporal Echo > Ensure UI Layer");
        if (!hasEraConfig)
            sb.AppendLine("WARN: EraConfig missing — assign Assets/TemporalEcho/EraConfig.asset");
        if (raw == 0 && passed == 0 && (persistent == null || persistent.DiscoveredCount == 0))
            sb.AppendLine("No detections yet — look at a laptop/book/chair/desk to discover");

        var missing = hasEraManager ? TemporalEraManager.GetMissingPrefabWarningsForEra(eraManager.CurrentEra, 3) : null;
        if (missing != null && missing.Count > 0)
        {
            string eraStr = hasEraManager ? eraManager.CurrentEra.ToString() : "?";
            foreach (var line in missing)
                sb.AppendLine(eraStr + " missing: " + line);
        }

        return sb.ToString();
    }

    public void SetVisible(bool visible)
    {
        _visible = visible;
        if (_panelRoot != null)
            _panelRoot.SetActive(_visible);
    }
}
