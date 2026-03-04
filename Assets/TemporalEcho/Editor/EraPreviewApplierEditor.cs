#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using TemporalEcho;

[CustomEditor(typeof(EraPreviewApplier))]
public class EraPreviewApplierEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var applier = (EraPreviewApplier)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Era Preview (Edit Mode)", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("1920s", GUILayout.Height(32)))
                Apply(applier, EraId.E1920s);

            if (GUILayout.Button("1960s", GUILayout.Height(32)))
                Apply(applier, EraId.E1960s);

            if (GUILayout.Button("2026", GUILayout.Height(32)))
                Apply(applier, EraId.E2026);
        }

        EditorGUILayout.HelpBox(
            "Click an era to spawn preview prefabs under the anchors.\n" +
            "Preview objects are named with prefix __EraPreview__ and are safe to delete.",
            MessageType.Info);
    }

    private void Apply(EraPreviewApplier applier, EraId era)
    {
        Undo.RecordObject(applier, "Switch Era Preview");
        applier.ApplyPreview(era);
        EditorUtility.SetDirty(applier);

        if (!Application.isPlaying)
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(applier.gameObject.scene);
    }
}
#endif
