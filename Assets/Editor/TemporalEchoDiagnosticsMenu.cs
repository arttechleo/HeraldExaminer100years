#if UNITY_EDITOR
using UnityEditor;

/// <summary>
/// Tools menu for Temporal Echo. Diagnostics auto-run is OFF by default.
/// Any editor script that runs on load (e.g. bootstrap, project doctor) should check
/// TemporalEchoDiagnosticsMenu.DiagnosticsAutoRunEnabled before executing.
/// </summary>
public static class TemporalEchoDiagnosticsMenu
{
    public const string EditorPrefsKey = "TemporalEcho.DiagnosticsAutoRun";

    public static bool DiagnosticsAutoRunEnabled
    {
        get => EditorPrefs.GetBool(EditorPrefsKey, false);
        set => EditorPrefs.SetBool(EditorPrefsKey, value);
    }

    [MenuItem("Tools/Temporal Echo/Enable Diagnostics Auto-Run")]
    public static void EnableDiagnosticsAutoRun()
    {
        DiagnosticsAutoRunEnabled = true;
        UnityEngine.Debug.Log("[Temporal Echo] Diagnostics auto-run enabled. Editor scripts that check this pref will run on load.");
    }

    [MenuItem("Tools/Temporal Echo/Disable Diagnostics Auto-Run")]
    public static void DisableDiagnosticsAutoRun()
    {
        DiagnosticsAutoRunEnabled = false;
        UnityEngine.Debug.Log("[Temporal Echo] Diagnostics auto-run disabled (default).");
    }

    [MenuItem("Tools/Temporal Echo/Enable Diagnostics Auto-Run", true)]
    [MenuItem("Tools/Temporal Echo/Disable Diagnostics Auto-Run", true)]
    private static bool ValidateDiagnosticsMenu() => true;
}
#endif
