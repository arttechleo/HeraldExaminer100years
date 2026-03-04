using UnityEngine;

/// <summary>
/// TEST-ONLY master toggle: when enabled, turns on AI recognition, label collection, all categories, and optional UI.
/// Add to the same GameObject as ObjectDetectionAgent / DetectionPrefabReplacer. Does not change defaults when off.
/// In non-debug builds, if test mode is enabled it is auto-disabled and a warning is shown.
/// </summary>
public class TemporalEchoTestMode : MonoBehaviour
{
    [Tooltip("Master switch. When true, applies all test options below. Default OFF so release behavior is unchanged.")]
    [SerializeField] private bool testModeEnabled = false;

    [Tooltip("When test mode is on: enable DetectionLabelCollector so labels view populates.")]
    [SerializeField] private bool enableLabelsCollection = true;

    [Tooltip("When test mode is on: enable all 8 prop categories (Screen, Book, Chair, Desk, Keyboard, Mouse, Phone, Cup).")]
    [SerializeField] private bool enableAllCategories = true;

    [Tooltip("When test mode is on: show era UI panel if EraSwitchPanel exists.")]
    [SerializeField] private bool enableEraUI = true;

    [Tooltip("When test mode is on and in dev build: show overlay warnings if components are missing.")]
    [SerializeField] private bool verboseOverlayWarnings = true;

    private DetectionPrefabReplacer _replacer;
    private DetectionLabelCollector _labelCollector;
    private MRStatusOverlay _overlay;
    private EraSwitchPanel _panel;
    private bool _applied;
    private bool _nonDebugWarningShown;

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        if (_replacer == null) CacheReferences();
    }

    private void Update()
    {
        if (!Debug.isDebugBuild && testModeEnabled)
        {
            testModeEnabled = false;
            if (!_nonDebugWarningShown)
            {
                _nonDebugWarningShown = true;
#if UNITY_EDITOR
                Debug.LogWarning("[TemporalEchoTestMode] Test mode was enabled in a non-debug build; it has been auto-disabled.");
#endif
            }
        }

        if (testModeEnabled && !_applied)
        {
            ApplyTestMode();
            _applied = true;
        }
        else if (!testModeEnabled)
            _applied = false;
    }

    private void CacheReferences()
    {
        _replacer = GetComponent<DetectionPrefabReplacer>();
        _labelCollector = GetComponent<DetectionLabelCollector>();
        if (_replacer == null) _replacer = FindAnyObjectByType<DetectionPrefabReplacer>();
        if (_labelCollector == null) _labelCollector = FindAnyObjectByType<DetectionLabelCollector>();
        _overlay = FindAnyObjectByType<MRStatusOverlay>();
        _panel = FindAnyObjectByType<EraSwitchPanel>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (testModeEnabled && verboseOverlayWarnings)
        {
            if (_replacer == null) Debug.LogWarning("[TemporalEchoTestMode] DetectionPrefabReplacer not found.");
            if (_labelCollector == null) Debug.LogWarning("[TemporalEchoTestMode] DetectionLabelCollector not found.");
        }
#endif
    }

    private void ApplyTestMode()
    {
        if (!testModeEnabled) return;

        if (enableLabelsCollection && _labelCollector != null)
        {
            _labelCollector.SetEnabled(true);
        }

        if (_replacer != null)
        {
            if (enableAllCategories)
                _replacer.SetAllCategoriesEnabledForTest(true);
            _replacer.SetShowRecognitionTextForTest(true);
        }

#if DEVELOPMENT_BUILD
        if (_overlay != null)
            _overlay.SetVisible(true);
#endif

        if (enableEraUI && _panel != null)
            _panel.SetShowEraUI(true);
    }

    /// <summary>Enable or disable test mode at runtime.</summary>
    public void SetTestModeEnabled(bool enabled)
    {
        testModeEnabled = enabled;
    }

    /// <summary>Current test mode state.</summary>
    public bool IsTestModeEnabled => testModeEnabled;

    private void OnGUI()
    {
        if (!Application.isPlaying) return;
        if (!Debug.isDebugBuild && _nonDebugWarningShown)
        {
            float w = 420;
            float x = Screen.width * 0.5f - w * 0.5f;
            GUI.Box(new Rect(x, 10, w, 40), "");
            GUI.Label(new Rect(x + 10, 14, w - 20, 32),
                "TEST MODE ENABLED IN NON-DEBUG BUILD — auto-disabled.");
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Enable Test Mode Now")]
    private void EditorEnableTestMode()
    {
        UnityEditor.Undo.RecordObject(this, "Enable Test Mode");
        testModeEnabled = true;
        _applied = false;
    }

    [ContextMenu("Disable Test Mode Now")]
    private void EditorDisableTestMode()
    {
        UnityEditor.Undo.RecordObject(this, "Disable Test Mode");
        testModeEnabled = false;
        _applied = false;
    }
#endif
}

