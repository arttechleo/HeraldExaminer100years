#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Adds the TemporalEchoUI layer if missing. Required for era panel pinch interaction.
/// </summary>
public static class TemporalEchoEnsureUILayer
{
    public const string TemporalEchoUILayerName = "TemporalEchoUI";

    [MenuItem("Tools/Temporal Echo/Ensure UI Layer")]
    public static void EnsureUILayer()
    {
        if (LayerMask.NameToLayer(TemporalEchoUILayerName) >= 0)
        {
            EditorUtility.DisplayDialog("Temporal Echo UI Layer", "Layer '" + TemporalEchoUILayerName + "' already exists.", "OK");
            return;
        }

        var tagManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (tagManager == null || tagManager.Length == 0)
        {
            EditorUtility.DisplayDialog("Error", "Could not load TagManager.asset.", "OK");
            return;
        }

        var tagAsset = tagManager[0];
        var so = new SerializedObject(tagAsset);
        var layersProp = so.FindProperty("layers");
        if (layersProp == null)
        {
            EditorUtility.DisplayDialog("Error", "Could not find layers in TagManager.", "OK");
            return;
        }

        for (int i = 8; i < 32; i++)
        {
            var p = layersProp.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(p.stringValue))
            {
                if (!EditorUtility.DisplayDialog("Add Temporal Echo UI Layer", "Add layer '" + TemporalEchoUILayerName + "' at index " + i + "?", "Add", "Cancel"))
                    return;
                p.stringValue = TemporalEchoUILayerName;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.DisplayDialog("Done", "Layer '" + TemporalEchoUILayerName + "' added. Assign it to PinchRayUIInteractor's UI Layer Mask and to EraSwitchPanel buttons.", "OK");
                return;
            }
        }
        EditorUtility.DisplayDialog("No Slot", "No empty layer slot (8–31) available. Free one and try again.", "OK");
    }
}
#endif
