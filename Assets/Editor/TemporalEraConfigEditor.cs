#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TemporalEraConfig))]
public class TemporalEraConfigEditor : Editor
{
    private SerializedProperty _defaultEra;
    private SerializedProperty _mappings;
    private SerializedProperty _defaultMicrophonePrefab;
    private SerializedProperty _defaultMicrophoneLocalOffset;
    private SerializedProperty _defaultScaleMultiplier;
    private SerializedProperty _includeHumanCategoryInConfig;
    private SerializedProperty _ambience1920s;
    private SerializedProperty _ambience1960s;
    private SerializedProperty _ambienceToday;

    private void OnEnable()
    {
        _defaultEra = serializedObject.FindProperty("defaultEra");
        _mappings = serializedObject.FindProperty("mappings");
        _defaultMicrophonePrefab = serializedObject.FindProperty("defaultMicrophonePrefab");
        _defaultMicrophoneLocalOffset = serializedObject.FindProperty("defaultMicrophoneLocalOffset");
        _defaultScaleMultiplier = serializedObject.FindProperty("defaultScaleMultiplier");
        _includeHumanCategoryInConfig = serializedObject.FindProperty("includeHumanCategoryInConfig");
        _ambience1920s = serializedObject.FindProperty("ambience1920s");
        _ambience1960s = serializedObject.FindProperty("ambience1960s");
        _ambienceToday = serializedObject.FindProperty("ambienceToday");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(_defaultEra);
        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField("Global defaults", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_defaultMicrophonePrefab);
        EditorGUILayout.PropertyField(_defaultMicrophoneLocalOffset);
        EditorGUILayout.PropertyField(_defaultScaleMultiplier);
        EditorGUILayout.PropertyField(_includeHumanCategoryInConfig);
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Ambience (optional)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_ambience1920s);
        EditorGUILayout.PropertyField(_ambience1960s);
        EditorGUILayout.PropertyField(_ambienceToday);
        EditorGUILayout.Space(8);

        EditorGUILayout.HelpBox(
            "Mic Prefab and Mic Offset use the global defaults above unless you enable the overrides per era. Clip is per-era (story differs).",
            MessageType.Info);

        EditorGUILayout.LabelField("Category mappings", EditorStyles.boldLabel);

        var config = (TemporalEraConfig)target;
        if (config.mappings != null && config.mappings.Count == 1 && config.mappings[0].category == DetectionCategory.Unknown)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox("Only one mapping with Category=Unknown. Click below to fill all categories (Screen, Book, Chair, Desk, Keyboard, Mouse, Phone, Cup).", MessageType.Warning);
            if (GUILayout.Button("Auto-Fill Categories (recommended)", GUILayout.Height(28)))
            {
                config.EnsureAllCategoriesPresent();
                EditorUtility.SetDirty(config);
                serializedObject.Update();
            }
            EditorGUILayout.Space(4);
        }

        if (GUILayout.Button("Auto-Fill Categories", GUILayout.Height(24)))
        {
            config = (TemporalEraConfig)target;
            config.EnsureAllCategoriesPresent();
            EditorUtility.SetDirty(config);
            serializedObject.Update();
        }
        if (GUILayout.Button("Sort by Category", GUILayout.Height(22)))
        {
            if (config.mappings != null && config.mappings.Count > 1)
            {
                config.mappings.Sort((a, b) => a.category.CompareTo(b.category));
                EditorUtility.SetDirty(config);
                serializedObject.Update();
            }
        }
        if (GUILayout.Button("Set all Scale Multipliers to 1", GUILayout.Height(22)))
        {
            config.SetAllScaleMultipliersToOne();
            EditorUtility.SetDirty(config);
            serializedObject.Update();
        }
        if (GUILayout.Button("Validate Config", GUILayout.Height(22)))
        {
            ValidateConfig((TemporalEraConfig)target);
        }
        if (GUILayout.Button("Validate All Eras", GUILayout.Height(22)))
        {
            ValidateAllEras((TemporalEraConfig)target);
        }
        EditorGUILayout.Space(6);

        if (_mappings == null) { serializedObject.ApplyModifiedProperties(); return; }

        for (int i = 0; i < _mappings.arraySize; i++)
        {
            // Always use the current index so each category's 1920s/1960s/Today slots are independent
            var elem = _mappings.GetArrayElementAtIndex(i);
            var categoryProp = elem.FindPropertyRelative("category");
            var displayNameProp = elem.FindPropertyRelative("displayName");
            var era1920 = elem.FindPropertyRelative("era1920s");
            var era1960 = elem.FindPropertyRelative("era1960s");
            var eraToday = elem.FindPropertyRelative("eraToday");

            string catName = categoryProp.enumDisplayNames[categoryProp.enumValueIndex];
            string summary = SummaryLine(catName, era1920, era1960, eraToday);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(categoryProp);
            EditorGUILayout.PropertyField(displayNameProp);
            if (!string.IsNullOrEmpty(summary))
                EditorGUILayout.HelpBox(summary, MessageType.None);
            bool isHuman = categoryProp.enumValueIndex == (int)DetectionCategory.Human;
            DrawEraSlot("1920s", era1920, isHuman);
            DrawEraSlot("1960s", era1960, isHuman);
            DrawEraSlot("Today", eraToday, isHuman);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawEraSlot(string label, SerializedProperty slot, bool isHuman)
    {
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        DrawProperty(slot, "prefab");
        if (isHuman)
            EditorGUILayout.HelpBox("Human: overlay only; position/rotation/scale apply to hat placement.", MessageType.None);
        DrawProperty(slot, "localPositionOffset");
        DrawProperty(slot, "localRotationEuler");
        DrawProperty(slot, "scaleMultiplier");
        EditorGUILayout.LabelField("Mic (optional overrides)", EditorStyles.miniLabel);
        var overridePrefab = slot.FindPropertyRelative("overrideMicPrefab");
        EditorGUILayout.PropertyField(overridePrefab);
        if (overridePrefab.boolValue)
            DrawProperty(slot, "micPrefabOverride");
        var overrideOffset = slot.FindPropertyRelative("overrideMicOffset");
        EditorGUILayout.PropertyField(overrideOffset);
        if (overrideOffset.boolValue)
            DrawProperty(slot, "micOffsetOverride");
        EditorGUILayout.PropertyField(slot.FindPropertyRelative("microphoneClip"), new GUIContent("Mic Clip (per-era)"));
        DrawProperty(slot, "appearBlendSeconds");
        DrawProperty(slot, "displayLabel");
        EditorGUI.indentLevel--;
    }

    private void DrawProperty(SerializedProperty parent, string name)
    {
        var p = parent.FindPropertyRelative(name);
        if (p != null) EditorGUILayout.PropertyField(p);
    }

    private string SummaryLine(string catName, SerializedProperty e20, SerializedProperty e60, SerializedProperty eToday)
    {
        string p20 = e20.FindPropertyRelative("prefab").objectReferenceValue != null ? "✅" : "⚠️";
        string p60 = e60.FindPropertyRelative("prefab").objectReferenceValue != null ? "✅" : "⚠️";
        string pToday = eToday.FindPropertyRelative("prefab").objectReferenceValue != null ? "✅" : "⚠️";
        return catName + ": 1920s " + p20 + " | 1960s " + p60 + " | Today " + pToday;
    }

    private void ValidateConfig(TemporalEraConfig config)
    {
        if (config.mappings == null) return;
        var defaultEra = config.defaultEra;
        int warn = 0;
        foreach (var m in config.mappings)
        {
            EraSlot slot = default;
            switch (defaultEra)
            {
                case TemporalEra.Era1920s: slot = m.era1920s; break;
                case TemporalEra.Era1960s: slot = m.era1960s; break;
                case TemporalEra.EraToday: slot = m.eraToday; break;
            }
            if (slot.prefab == null)
            {
                Debug.LogWarning("[EraConfig] Category " + m.category + " has no prefab for default era " + defaultEra + ". Assign in inspector.");
                warn++;
            }
            if (slot.microphoneClip == null && config.defaultMicrophonePrefab != null)
                Debug.Log("[EraConfig] Category " + m.category + " has no mic clip for era " + defaultEra + "; mic will be silent unless assigned.");
        }
        if (warn == 0)
            Debug.Log("[EraConfig] Validate: all mapped categories have a prefab for default era " + defaultEra + ".");
    }

    private void ValidateAllEras(TemporalEraConfig config)
    {
        if (config.mappings == null) return;
        foreach (TemporalEra era in new[] { TemporalEra.Era1920s, TemporalEra.Era1960s, TemporalEra.EraToday })
        {
            var missingPrefab = new System.Collections.Generic.List<string>();
            var scaleZero = new System.Collections.Generic.List<string>();
            var missingClip = new System.Collections.Generic.List<string>();
            foreach (var m in config.mappings)
            {
                if (m.category == DetectionCategory.Unknown) continue;
                EraSlot slot = era == TemporalEra.Era1920s ? m.era1920s : (era == TemporalEra.Era1960s ? m.era1960s : m.eraToday);
                if (slot.prefab == null) missingPrefab.Add(m.category.ToString());
                if (slot.scaleMultiplier == 0f) scaleZero.Add(m.category.ToString());
                if (slot.microphoneClip == null && config.defaultMicrophonePrefab != null) missingClip.Add(m.category.ToString());
            }
            Debug.Log("[EraConfig] " + era + " — Missing prefab: " + (missingPrefab.Count > 0 ? string.Join(", ", missingPrefab) : "none") +
                "; Scale=0: " + (scaleZero.Count > 0 ? string.Join(", ", scaleZero) : "none") +
                "; No mic clip: " + (missingClip.Count > 0 ? string.Join(", ", missingClip) : "none"));
        }
    }
}
#endif
