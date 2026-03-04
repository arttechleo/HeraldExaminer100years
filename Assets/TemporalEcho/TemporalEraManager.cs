using System.Collections.Generic;
using UnityEngine;
using TemporalEcho;

/// <summary>
/// Holds current era and provides prefabs per category. EraDatabase is preferred for Screen/Book/Human; EraConfig is fallback.
/// On SetEra, notifies PersistentDiscoveryManager to refresh existing props to the new era.
/// </summary>
public class TemporalEraManager : MonoBehaviour
{
    private static readonly List<(TemporalEra era, string category)> s_missingPrefabWarnings = new List<(TemporalEra, string)>();

    [Header("Prefab Sources")]
    [Tooltip("Primary source for Screen, Book, Human overlays. Assign for ObjectDetection integration.")]
    [SerializeField] private EraDatabase eraDatabase;
    [Tooltip("Fallback when eraDatabase has no rule; used for Chair, Desk, etc.")]
    [SerializeField] private TemporalEraConfig eraConfig;
    [Tooltip("If null, found at runtime.")]
    [SerializeField] private PersistentDiscoveryManager persistentManager;

    public TemporalEra CurrentEra { get; private set; }
    public bool HasEraConfig => eraConfig != null;

    private void Awake()
    {
        if (eraConfig != null)
            CurrentEra = eraConfig.defaultEra;
        if (persistentManager == null)
            persistentManager = FindAnyObjectByType<PersistentDiscoveryManager>();
    }

    private void Start()
    {
        TemporalEra target = (eraConfig != null ? eraConfig.defaultEra : TemporalEra.Era1920s);
        if (CurrentEra != target)
            SetEra(target);
    }

    public void SetEra(TemporalEra era)
    {
        CurrentEra = era;
        if (persistentManager != null)
            persistentManager.RefreshEra(era);
    }

    /// <summary>Try get the resolved era slot for a category in the given era. EraDatabase preferred for Screen/Book/Human.</summary>
    public bool TryGetSlot(DetectionCategory cat, TemporalEra era, out ResolvedEraSlot slot)
    {
        slot = default;
        if (eraDatabase != null && TryGetSlotFromEraDatabase(cat, era, out slot))
            return true;
        if (eraConfig == null || eraConfig.mappings == null) return false;
        var m = GetMapping(cat);
        if (m == null) return false;
        var raw = GetSlotFromMapping(m, era);
        slot = BuildResolvedSlot(raw);
        return true;
    }

    private bool TryGetSlotFromEraDatabase(DetectionCategory cat, TemporalEra era, out ResolvedEraSlot slot)
    {
        slot = default;
        var subject = ToDetectedSubject(cat);
        if (subject == null) return false;
        var eraId = ToEraId(era);
        var def = eraDatabase.Get(eraId);
        if (def.rules == null) return false;
        foreach (var r in def.rules)
        {
            if (r.subject != subject.Value) continue;
            if (r.prefab == null) return false;
            slot = new ResolvedEraSlot
            {
                prefab = r.prefab,
                localPositionOffset = r.localPosition,
                localRotationEuler = r.localEulerRotation,
                scaleMultiplier = r.localScale != Vector3.zero ? r.localScale.x : 1f,
                microphoneClip = null,
                appearBlendSeconds = 0.2f,
                displayLabel = cat.ToString()
            };
            return true;
        }
        return false;
    }

    private static DetectedSubject? ToDetectedSubject(DetectionCategory cat)
    {
        return cat switch
        {
            DetectionCategory.Screen => DetectedSubject.Screen,
            DetectionCategory.Book => DetectedSubject.Book,
            DetectionCategory.Human => DetectedSubject.Human,
            _ => null
        };
    }

    private static EraId ToEraId(TemporalEra era)
    {
        return era switch
        {
            TemporalEra.Era1920s => EraId.E1920s,
            TemporalEra.Era1960s => EraId.E1960s,
            TemporalEra.EraToday => EraId.E2026,
            _ => EraId.E2026
        };
    }

    /// <summary>Get resolved slot for category in current era. Mic prefab/offset come from config defaults unless overridden.</summary>
    public ResolvedEraSlot GetSlotOrDefault(DetectionCategory cat)
    {
        if (TryGetSlot(cat, CurrentEra, out var slot))
            return slot;
        return BuildResolvedSlot(default);
    }

    private static EraSlot GetSlotFromMapping(CategoryEraMapping m, TemporalEra era)
    {
        return era switch
        {
            TemporalEra.Era1920s => m.era1920s,
            TemporalEra.Era1960s => m.era1960s,
            TemporalEra.EraToday => m.eraToday,
            _ => default
        };
    }

    /// <summary>Strict per-era resolution: prefab is never inherited. Scale 0 → 1. Mic from config defaults unless overridden.</summary>
    private ResolvedEraSlot BuildResolvedSlot(EraSlot raw)
    {
        var slot = new ResolvedEraSlot
        {
            prefab = raw.prefab,
            localPositionOffset = raw.localPositionOffset,
            localRotationEuler = raw.localRotationEuler,
            scaleMultiplier = raw.scaleMultiplier > 0f ? raw.scaleMultiplier : 1f,
            microphoneClip = raw.microphoneClip,
            appearBlendSeconds = raw.appearBlendSeconds > 0f ? raw.appearBlendSeconds : 0.2f,
            displayLabel = raw.displayLabel
        };
        if (eraConfig != null)
        {
            slot.microphonePrefab = raw.overrideMicPrefab && raw.micPrefabOverride != null ? raw.micPrefabOverride : eraConfig.defaultMicrophonePrefab;
            slot.microphoneLocalOffset = raw.overrideMicOffset ? raw.micOffsetOverride : eraConfig.defaultMicrophoneLocalOffset;
        }
        return slot;
    }

    /// <summary>Clear missing-prefab warnings (call at start of RefreshEra).</summary>
    public static void ClearMissingPrefabWarnings()
    {
        s_missingPrefabWarnings.Clear();
    }

    /// <summary>Record missing prefab for overlay. Strict mode: no silent inherit.</summary>
    public static void AddMissingPrefabWarning(TemporalEra era, string category)
    {
        s_missingPrefabWarnings.Add((era, category));
    }

    /// <summary>Get up to maxCount missing-prefab messages for the given era (e.g. "Screen prefab").</summary>
    public static IReadOnlyList<string> GetMissingPrefabWarningsForEra(TemporalEra era, int maxCount = 3)
    {
        var list = new List<string>();
        foreach (var (e, cat) in s_missingPrefabWarnings)
        {
            if (e != era) continue;
            list.Add(cat + " prefab");
            if (list.Count >= maxCount) break;
        }
        return list;
    }

    private CategoryEraMapping GetMapping(DetectionCategory cat)
    {
        foreach (var m in eraConfig.mappings)
        {
            if (m.category == cat) return m;
        }
        return null;
    }

    /// <summary>Returns the prefab for the given category in the current era (backward compat).</summary>
    public GameObject GetPrefab(DetectionCategory cat) => GetSlotOrDefault(cat).prefab;

    /// <summary>Returns the microphone clip for the given category in the current era (backward compat).</summary>
    public AudioClip GetMicClip(DetectionCategory cat) => GetSlotOrDefault(cat).microphoneClip;

    /// <summary>Returns rotation offset (euler) for the category in current era (backward compat).</summary>
    public Vector3 GetRotationOffsetEuler(DetectionCategory cat) => GetSlotOrDefault(cat).localRotationEuler;

    /// <summary>Per-era display label for plaque/title (e.g. "Underwood No. 5").</summary>
    public string GetDisplayLabelForCategory(DetectionCategory cat) => GetSlotOrDefault(cat).displayLabel ?? cat.ToString();

    public void SetEraConfig(TemporalEraConfig config)
    {
        eraConfig = config;
        if (config != null)
            CurrentEra = config.defaultEra;
    }
}
