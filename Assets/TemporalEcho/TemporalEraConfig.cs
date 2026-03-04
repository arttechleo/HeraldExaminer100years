using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Era enum for Temporal Echo — Herald Examiner (1920s, 1960s, Today).
/// </summary>
public enum TemporalEra
{
    Era1920s,
    Era1960s,
    EraToday
}

/// <summary>
/// Per-era slot (serialized): prefab, offsets, scale, optional mic overrides, and clip. Used by CategoryEraMapping.
/// Mic prefab/offset use config defaults unless overrides are set.
/// </summary>
[Serializable]
public struct EraSlot
{
    [Tooltip("Main prop prefab for this era.")]
    public GameObject prefab;
    [Tooltip("Local position offset (meters) applied to visual relative to stable root.")]
    public Vector3 localPositionOffset;
    [Tooltip("Local rotation (euler degrees) applied to visual.")]
    public Vector3 localRotationEuler;
    [Tooltip("Scale multiplier (1 = no change). Use 1, not 0 — 0 hides content. Resolved as 1 if 0 at runtime.")]
    public float scaleMultiplier;
    [Tooltip("If true, use Mic Prefab Override; otherwise use config default.")]
    public bool overrideMicPrefab;
    [Tooltip("Only used when Override Mic Prefab is true.")]
    public GameObject micPrefabOverride;
    [Tooltip("If true, use Mic Offset Override; otherwise use config default.")]
    public bool overrideMicOffset;
    [Tooltip("Only used when Override Mic Offset is true.")]
    public Vector3 micOffsetOverride;
    [Tooltip("Audio clip when mic is pinched (per-era; no global default).")]
    public AudioClip microphoneClip;
    [Tooltip("Optional: blend-in duration when swapping era (future crossfade).")]
    public float appearBlendSeconds;
    [Tooltip("Per-era display label e.g. 'Underwood No. 5' for plaque/title.")]
    public string displayLabel;
}

/// <summary>
/// Runtime-resolved slot with mic prefab/offset resolved from config defaults or overrides. Returned by TemporalEraManager.
/// </summary>
public struct ResolvedEraSlot
{
    public GameObject prefab;
    public Vector3 localPositionOffset;
    public Vector3 localRotationEuler;
    public float scaleMultiplier;
    public GameObject microphonePrefab;
    public Vector3 microphoneLocalOffset;
    public AudioClip microphoneClip;
    public float appearBlendSeconds;
    public string displayLabel;
}

[Serializable]
public class CategoryEraMapping
{
    public DetectionCategory category;
    [Tooltip("Display label e.g. 'Monitor → Typewriter'.")]
    public string displayName;
    public EraSlot era1920s;
    public EraSlot era1960s;
    public EraSlot eraToday;

    /// <summary>Deep copy so each list element can be a unique instance (avoids shared reference across eras).</summary>
    public CategoryEraMapping Clone()
    {
        return new CategoryEraMapping
        {
            category = category,
            displayName = displayName,
            era1920s = era1920s,
            era1960s = era1960s,
            eraToday = eraToday
        };
    }
}

/// <summary>
/// ScriptableObject holding prefab and microphone mappings per era per category.
/// Use EnsureAllCategoriesPresent() to populate all standard categories. Assign in TemporalEraManager.
/// </summary>
[CreateAssetMenu(fileName = "EraConfig", menuName = "Temporal Echo/Era Config", order = 0)]
public class TemporalEraConfig : ScriptableObject
{
    public TemporalEra defaultEra = TemporalEra.Era1920s;
    public List<CategoryEraMapping> mappings = new List<CategoryEraMapping>();

    [Header("Global defaults (used when slot values are null/zero)")]
    [Tooltip("Default microphone prefab when slot.microphonePrefab is null.")]
    public GameObject defaultMicrophonePrefab;
    [Tooltip("Default mic offset from prop root when slot.microphoneLocalOffset is (0,0,0).")]
    public Vector3 defaultMicrophoneLocalOffset = new Vector3(0.25f, 0.1f, 0f);
    [Tooltip("Default scale multiplier when slot.scaleMultiplier is 0.")]
    public float defaultScaleMultiplier = 1f;
    [Tooltip("If true, include Human in mappings (optional; Human is usually real-time only).")]
    public bool includeHumanCategoryInConfig = false;

    [Header("Ambience (optional)")]
    public AudioClip ambience1920s;
    public AudioClip ambience1960s;
    public AudioClip ambienceToday;

    private static readonly (DetectionCategory cat, string name)[] StandardCategories = new[]
    {
        (DetectionCategory.Screen, "Laptop/Monitor → Typewriter"),
        (DetectionCategory.Book, "Book → Newspaper"),
        (DetectionCategory.Chair, "Chair → Old Chair"),
        (DetectionCategory.Desk, "Desk → Desk Prop"),
        (DetectionCategory.Keyboard, "Keyboard → Period Keyboard"),
        (DetectionCategory.Mouse, "Mouse → Period Mouse"),
        (DetectionCategory.Phone, "Phone → Period Phone"),
        (DetectionCategory.Cup, "Cup/Bottle → Period Cup"),
    };

    /// <summary>
    /// Ensures mappings exist for Screen, Book, Chair, Desk, Keyboard, Mouse, Phone, Cup (and optionally Human).
    /// Sets displayName defaults. Does not remove or overwrite existing mappings for those categories.
    /// </summary>
    public void EnsureAllCategoriesPresent()
    {
        if (mappings == null)
            mappings = new List<CategoryEraMapping>();

        bool allUnknown = true;
        foreach (var m in mappings)
        {
            if (m.category != DetectionCategory.Unknown) { allUnknown = false; break; }
        }
        if (allUnknown && mappings.Count > 0)
            mappings.Clear();

        var list = new List<(DetectionCategory cat, string name)>(StandardCategories);
        if (includeHumanCategoryInConfig)
            list.Add((DetectionCategory.Human, "Human → Hat Overlay"));

        foreach (var (cat, displayName) in list)
        {
            bool has = false;
            foreach (var m in mappings)
            {
                if (m.category == cat)
                {
                    if (string.IsNullOrEmpty(m.displayName))
                        m.displayName = displayName;
                    has = true;
                    break;
                }
            }
            if (!has)
            {
                var m = new CategoryEraMapping { category = cat, displayName = displayName };
                m.era1920s.scaleMultiplier = 1f;
                m.era1960s.scaleMultiplier = 1f;
                m.eraToday.scaleMultiplier = 1f;
                mappings.Add(m);
            }
        }
    }

    /// <summary>Set every era slot's scaleMultiplier to 1 (fixes 0 = hidden). Call from editor.</summary>
    public void SetAllScaleMultipliersToOne()
    {
        if (mappings == null) return;
        foreach (var m in mappings)
        {
            var e20 = m.era1920s; e20.scaleMultiplier = 1f; m.era1920s = e20;
            var e60 = m.era1960s; e60.scaleMultiplier = 1f; m.era1960s = e60;
            var eToday = m.eraToday; eToday.scaleMultiplier = 1f; m.eraToday = eToday;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (mappings == null) return;
        bool dirty = false;

        // Ensure no duplicate CategoryEraMapping references (same instance in list twice => edits mirror across "eras")
        for (int i = 0; i < mappings.Count; i++)
        {
            for (int j = i + 1; j < mappings.Count; j++)
            {
                if (ReferenceEquals(mappings[i], mappings[j]))
                {
                    mappings[j] = mappings[i].Clone();
                    dirty = true;
                    Debug.LogWarning("[TemporalEraConfig] Duplicate CategoryEraMapping references found and fixed. Each mapping is now a unique instance.");
                }
            }
        }

        foreach (var m in mappings)
        {
            if (m.era1920s.scaleMultiplier == 0f) { var s = m.era1920s; s.scaleMultiplier = 1f; m.era1920s = s; dirty = true; }
            if (m.era1960s.scaleMultiplier == 0f) { var s = m.era1960s; s.scaleMultiplier = 1f; m.era1960s = s; dirty = true; }
            if (m.eraToday.scaleMultiplier == 0f) { var s = m.eraToday; s.scaleMultiplier = 1f; m.eraToday = s; dirty = true; }
        }
        if (dirty)
            UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
