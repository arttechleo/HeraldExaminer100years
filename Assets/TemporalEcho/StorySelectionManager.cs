using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Guided exhibit mode: "Choose a story" — only one microphone is active at a time. User cycles through discovered objects.
/// </summary>
public class StorySelectionManager : MonoBehaviour
{
    [Tooltip("If null, found at runtime.")]
    [SerializeField] private PersistentDiscoveryManager pdm;
    [Tooltip("If null, found at runtime.")]
    [SerializeField] private TemporalEraManager eraManager;
    [Tooltip("If null, created at runtime under active prop.")]
    [SerializeField] private SelectionHighlighter highlighterPrefab;

    private SelectionHighlighter _activeHighlighter;
    private GameObject _highlighterRoot;
    private int _activeIndex;
    private List<DetectionCategory> _available = new List<DetectionCategory>();

    /// <summary>True when "Choose a story" is on — only one mic is interactable.</summary>
    public bool SelectionModeActive { get; private set; }

    /// <summary>Currently highlighted category (when selection mode on).</summary>
    public DetectionCategory? ActiveCategory => _available.Count > 0 && _activeIndex >= 0 && _activeIndex < _available.Count ? _available[_activeIndex] : (DetectionCategory?)null;

    /// <summary>Display label for current era + active category (e.g. "Typewriter — Tap mic to listen").</summary>
    public string ActiveDisplayLabel
    {
        get
        {
            var cat = ActiveCategory;
            if (!cat.HasValue) return "";
            string label = eraManager != null ? eraManager.GetDisplayLabelForCategory(cat.Value) : cat.Value.ToString();
            return label + " — Tap mic to listen";
        }
    }

    private void Awake()
    {
        if (pdm == null) pdm = FindAnyObjectByType<PersistentDiscoveryManager>();
        if (eraManager == null) eraManager = FindAnyObjectByType<TemporalEraManager>();
    }

    private void Start()
    {
        RefreshAvailable();
    }

    /// <summary>Refresh list of categories that have a prop. Call when new props appear.</summary>
    public void RefreshAvailable()
    {
        _available = pdm != null ? pdm.GetAvailableCategories() : new List<DetectionCategory>();
        _activeIndex = _available.Count > 0 ? 0 : -1;
        if (SelectionModeActive)
            ApplySelectionState();
    }

    /// <summary>Toggle "Choose a story" mode. When on, only one mic is active.</summary>
    public void SetSelectionMode(bool on)
    {
        SelectionModeActive = on;
        if (pdm == null) return;
        if (!on)
        {
            pdm.SetAllMicrophonesInteractableEnabled(true);
            HideHighlighter();
            return;
        }
        RefreshAvailable();
        ApplySelectionState();
    }

    public void ToggleSelectionMode()
    {
        SetSelectionMode(!SelectionModeActive);
    }

    /// <summary>Cycle to next object. Call from Next button.</summary>
    public void SelectNext()
    {
        if (_available.Count == 0) return;
        _activeIndex = (_activeIndex + 1) % _available.Count;
        ApplySelectionState();
    }

    /// <summary>Cycle to previous object. Call from Prev button.</summary>
    public void SelectPrev()
    {
        if (_available.Count == 0) return;
        _activeIndex = _activeIndex <= 0 ? _available.Count - 1 : _activeIndex - 1;
        ApplySelectionState();
    }

    private void ApplySelectionState()
    {
        if (pdm == null) return;
        pdm.SetAllMicrophonesInteractableEnabled(false);
        if (_activeIndex >= 0 && _activeIndex < _available.Count)
        {
            var cat = _available[_activeIndex];
            pdm.SetMicrophoneInteractableEnabled(cat, true);
            ShowHighlighterFor(pdm.GetPersistentPropRoot(cat));
        }
        else
            HideHighlighter();
    }

    private void ShowHighlighterFor(GameObject propRoot)
    {
        if (propRoot == null)
        {
            HideHighlighter();
            return;
        }
        if (_highlighterRoot == null)
        {
            _highlighterRoot = new GameObject("SelectionHighlighter_Root");
            _activeHighlighter = _highlighterRoot.AddComponent<SelectionHighlighter>();
        }
        _highlighterRoot.transform.SetParent(propRoot.transform, false);
        _highlighterRoot.transform.localPosition = Vector3.zero;
        _highlighterRoot.transform.localRotation = Quaternion.identity;
        _highlighterRoot.transform.localScale = Vector3.one;
        _highlighterRoot.SetActive(true);
        _activeHighlighter.SetLabel(ActiveDisplayLabel);
        _activeHighlighter.enabled = true;
    }

    private void HideHighlighter()
    {
        if (_highlighterRoot != null)
            _highlighterRoot.SetActive(false);
    }

    /// <summary>Whether the given mic is the currently active one (for external checks).</summary>
    public bool IsMicActive(MicrophoneInteractable mic)
    {
        if (mic == null || !SelectionModeActive) return false;
        var cat = ActiveCategory;
        if (!cat.HasValue) return false;
        var activeMic = pdm != null ? pdm.GetMicrophoneForCategory(cat.Value) : null;
        return activeMic == mic;
    }
}
