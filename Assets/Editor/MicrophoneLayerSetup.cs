#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ensures the Microphone layer exists for hand-pinch interaction.
/// Invoke via Tools > Microphone > Ensure Microphone Layer (do not run from delayCall to avoid editor assertions).
/// </summary>
public static class MicrophoneLayerSetup
{
    private const string MicrophoneLayerName = "Microphone";

    [MenuItem("Tools/Microphone/Ensure Microphone Layer")]
    public static void EnsureMicrophoneLayer()
    {
        if (LayerMask.NameToLayer(MicrophoneLayerName) >= 0) return;

        var tagManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (tagManager == null || tagManager.Length == 0) return;

        var tagAsset = tagManager[0];
        var so = new SerializedObject(tagAsset);
        var layersProp = so.FindProperty("layers");
        if (layersProp == null) return;

        for (int i = 8; i < 32; i++)
        {
            var p = layersProp.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(p.stringValue))
            {
                p.stringValue = MicrophoneLayerName;
                so.ApplyModifiedPropertiesWithoutUndo();
                return;
            }
        }
    }
}
#endif
