#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor window: 3 buttons (1920s / 1960s / 2026) to swap prefabs in the scene by era.
/// Uses TemporalEraConfig mappings (Screen, Book, Human) and named anchors. Edit-mode only.
/// </summary>
public class EraSwitcherWindow : EditorWindow
{
    [SerializeField] private TemporalEraConfig eraConfig;

    private const string ScreenAnchorName = "EraPreview_Anchor_Screen";
    private const string BookAnchorName   = "EraPreview_Anchor_Book";
    private const string HumanAnchorName  = "EraPreview_Anchor_Human";
    private const string SpawnedChildName = "EraPreview_Spawned";
    private const string PrefKey          = "TemporalEcho.ActiveEraId";

    [MenuItem("Tools/Temporal Echo/Era Switcher (Preview)")]
    public static void Open()
    {
        var w = GetWindow<EraSwitcherWindow>("Era Switcher");
        w.minSize = new Vector2(360, 220);
        w.Show();
    }

    private void OnEnable()
    {
        if (eraConfig != null) return;
        var guid = AssetDatabase.FindAssets("t:TemporalEraConfig").FirstOrDefault();
        if (!string.IsNullOrEmpty(guid))
            eraConfig = AssetDatabase.LoadAssetAtPath<TemporalEraConfig>(AssetDatabase.GUIDToAssetPath(guid));
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Temporal Echo — Era Preview (Edit Mode)", EditorStyles.boldLabel);
        EditorGUILayout.Space(6);

        eraConfig = (TemporalEraConfig)EditorGUILayout.ObjectField("Era Config", eraConfig, typeof(TemporalEraConfig), false);

        EditorGUILayout.HelpBox(
            "Create 3 empty GameObjects in your scene named:\n" +
            $"- {ScreenAnchorName}\n- {BookAnchorName}\n- {HumanAnchorName}\n\n" +
            "Click an era button to swap prefabs immediately (in the editor).",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(eraConfig == null))
        {
            EditorGUILayout.Space(6);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("1920s", GUILayout.Height(34))) ApplyEra(TemporalEra.Era1920s);
                if (GUILayout.Button("1960s", GUILayout.Height(34))) ApplyEra(TemporalEra.Era1960s);
                if (GUILayout.Button("2026",  GUILayout.Height(34))) ApplyEra(TemporalEra.EraToday);
            }

            EditorGUILayout.Space(6);

            var activeEra = GetActiveEra();
            EditorGUILayout.LabelField("Active Era:", activeEra.ToString());

            if (GUILayout.Button("Re-Apply Active Era (Refresh)", GUILayout.Height(28)))
                ApplyEra(activeEra);
        }

        EditorGUILayout.Space(8);
    }

    private static TemporalEra GetActiveEra()
    {
        var v = EditorPrefs.GetInt(PrefKey, (int)TemporalEra.Era1920s);
        return (TemporalEra)v;
    }

    private static void SetActiveEra(TemporalEra era)
    {
        EditorPrefs.SetInt(PrefKey, (int)era);
    }

    private void ApplyEra(TemporalEra era)
    {
        if (eraConfig == null)
        {
            Debug.LogError("[EraSwitcher] Missing TemporalEraConfig reference.");
            return;
        }

        if (eraConfig.mappings == null || eraConfig.mappings.Count == 0)
        {
            Debug.LogWarning("[EraSwitcher] Era Config has no mappings. Use Ensure All Categories in config inspector.");
            return;
        }

        var screenAnchor = EnsureAnchor(ScreenAnchorName);
        var bookAnchor   = EnsureAnchor(BookAnchorName);
        var humanAnchor  = EnsureAnchor(HumanAnchorName);

        var (screenPrefab, screenSlot) = GetPrefabAndSlotFor(DetectionCategory.Screen, era);
        var (bookPrefab, bookSlot)     = GetPrefabAndSlotFor(DetectionCategory.Book, era);
        var (humanPrefab, humanSlot)   = GetPrefabAndSlotFor(DetectionCategory.Human, era);
        ReplaceUnderAnchorWithSlot(screenAnchor, screenPrefab, screenSlot);
        ReplaceUnderAnchorWithSlot(bookAnchor,   bookPrefab,   bookSlot);
        ReplaceUnderAnchorWithSlot(humanAnchor,  humanPrefab,  humanSlot);

        SetActiveEra(era);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"[EraSwitcher] Applied era: {era}");
    }

    private (GameObject prefab, EraSlot slot) GetPrefabAndSlotFor(DetectionCategory category, TemporalEra era)
    {
        if (eraConfig?.mappings == null) return (null, default);
        var mapping = eraConfig.mappings.FirstOrDefault(m => m.category == category);
        if (mapping == null) return (null, default);
        EraSlot slot;
        switch (era)
        {
            case TemporalEra.Era1920s: slot = mapping.era1920s; break;
            case TemporalEra.Era1960s: slot = mapping.era1960s; break;
            case TemporalEra.EraToday: slot = mapping.eraToday; break;
            default: return (null, default);
        }
        return (slot.prefab, slot);
    }

    private static Transform EnsureAnchor(string name)
    {
        var go = GameObject.Find(name);
        if (go == null)
        {
            go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        }
        return go.transform;
    }

    private static void ReplaceUnderAnchorWithSlot(Transform anchor, GameObject prefab, EraSlot slot)
    {
        var old = anchor.Find(SpawnedChildName);
        if (old != null)
            Undo.DestroyObjectImmediate(old.gameObject);

        if (prefab == null)
            return;

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, anchor);
        if (instance == null)
            instance = (GameObject)Object.Instantiate(prefab, anchor);

        Undo.RegisterCreatedObjectUndo(instance, "Spawn Era Preview Prefab");
        instance.name = SpawnedChildName;

        var t = instance.transform;
        t.localPosition = slot.localPositionOffset;
        t.localRotation = Quaternion.Euler(slot.localRotationEuler);
        float scale = slot.scaleMultiplier > 0f ? slot.scaleMultiplier : 1f;
        t.localScale = new Vector3(scale, scale, scale);
    }
}
#endif
