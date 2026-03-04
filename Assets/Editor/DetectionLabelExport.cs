#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class DetectionLabelExport
{
    [MenuItem("Tools/Temporal Echo/Export Seen Labels")]
    public static void ExportSeenLabels()
    {
        int count = DetectionLabelCollector.TotalUniqueTokens;
        string path = DetectionLabelCollector.ExportToFile();
        EditorUtility.DisplayDialog("Export Seen Labels",
            count > 0
                ? "Exported " + count + " token(s) to:\n" + path + "\n\nContent copied to clipboard."
                : "No labels collected yet. Enter Play Mode and run detection to collect labels, then export again.",
            "OK");
    }
}
#endif
