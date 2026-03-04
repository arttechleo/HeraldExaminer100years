using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Discover-once, persistent props for Screen, Book, Desk, Chair. Keys by quantized viewport
/// to avoid duplicates. Human is handled real-time elsewhere.
/// Supports per-category instance cap, overlap resolution, and microphone spawning.
/// </summary>
public class PersistentDiscoveryManager : MonoBehaviour
{
    /// <summary>When true, no microphone 3D model or logic is spawned or used. Set true for showcase.</summary>
    public static bool DisableVirtualMic = true;

    private const int GridBuckets = 32;
    private const string MicrophoneLayerName = "Microphone";

    [Tooltip("After this many seconds from first spawn, prop pose is frozen (no more updates).")]
    [SerializeField] [Min(0.1f)] private float lockAfterSeconds = 0.8f;

    [Header("Instance Mode")]
    [Tooltip("When true, only one persistent prop per category (Screen/Book/Desk/Chair). First detection wins; pose can refine during unlock window.")]
    [SerializeField] private bool singleInstancePerCategory = true;

    #region Per-Category Cap (ignored when singleInstancePerCategory)
    [Header("Per-Category Cap")]
    [Tooltip("Max persistent instances per category. Ignored when Single Instance Per Category is on.")]
    [SerializeField] private int maxInstancesPerCategory = 3;
    [Tooltip("Enforce per-category cap. Ignored when Single Instance Per Category is on.")]
    [SerializeField] private bool enforceCategoryCap = true;
    #endregion

    #region Overlap Resolution
    [Header("Overlap Resolution")]
    [Tooltip("Push overlapping persistent props apart on XZ plane during unlock window.")]
    [SerializeField] private bool resolveOverlaps = true;
    [Tooltip("Minimum horizontal separation between props (meters).")]
    [SerializeField] private float minSeparationMeters = 0.5f;
    [Tooltip("Conservative sphere radius for overlap checks.")]
    [SerializeField] private float overlapRadiusMeters = 0.35f;
    [Tooltip("Max iterations for multi-overlap settlement.")]
    [SerializeField] private int maxSeparationIterations = 4;
    #endregion

    #region Microphone
    [Header("Microphone")]
    [Tooltip("Prefab for microphone (optional; creates sphere if null).")]
    [SerializeField] private GameObject microphonePrefab;
    [Tooltip("Offset from main prop (XZ) and height (Y).")]
    [SerializeField] private Vector3 microphoneOffset = new Vector3(0.25f, 0.1f, 0f);
    [Tooltip("Sphere radius for mic overlap resolution.")]
    [SerializeField] private float microphoneOverlapRadius = 0.08f;
    [Tooltip("If true, do not spawn microphone when clip is missing for that category.")]
    [SerializeField] private bool requireClipToSpawnMic;
    [Tooltip("If true, spawned mics play generated 440Hz beep instead of clip. Debug only — leave false to use custom clips.")]
    [SerializeField] private bool micDebugForceBeep;
    [SerializeField] private AudioClip screenMicrophoneClip;
    [SerializeField] private AudioClip bookMicrophoneClip;
    [SerializeField] private AudioClip deskMicrophoneClip;
    [SerializeField] private AudioClip chairMicrophoneClip;

    /// <summary>When true, all spawned mics play generated beep instead of assigned clip. Debug only — leave false for custom clips.</summary>
    public bool MicDebugForceBeep { get => micDebugForceBeep; set => micDebugForceBeep = value; }
    #endregion

    private const string VisualChildName = "Visual";

    private struct PersistentProp
    {
        public GameObject GameObject;
        public GameObject MicrophoneGameObject;
        public string Key;
        public string Category;
        public float LockTime;
        public Vector3 PrefabOriginalScale;
        public Vector3 ScaleMultiplier;
        public float SlotScaleMultiplier; // from EraSlot for refresh
    }

    private readonly Dictionary<string, PersistentProp> _discovered = new Dictionary<string, PersistentProp>();
    private readonly Dictionary<string, PersistentProp> _singleByCategory = new Dictionary<string, PersistentProp>();
    private int _microphoneLayer = -1;
    private bool _microphoneLayerWarned;

    private void Awake()
    {
        _microphoneLayer = LayerMask.NameToLayer(MicrophoneLayerName);
        if (_microphoneLayer < 0 && !_microphoneLayerWarned)
        {
            _microphoneLayerWarned = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UnityEngine.Debug.LogWarning("[PersistentDiscoveryManager] Layer 'Microphone' not found. Add it in Tags and Layers. Microphones will use Default layer.");
#endif
        }
        EnsureHandPinchInteractor();
        EnsureQuestAudioDoctor();
        EnsureAudioFocusManager();
    }

    private void EnsureAudioFocusManager()
    {
        if (FindAnyObjectByType<AudioFocusManager>() != null) return;
        gameObject.AddComponent<AudioFocusManager>();
    }

    private void EnsureQuestAudioDoctor()
    {
        if (GetComponent<QuestAudioDoctor>() == null)
            gameObject.AddComponent<QuestAudioDoctor>();
    }

    private void EnsureHandPinchInteractor()
    {
        if (FindAnyObjectByType<HandPinchInteractor>() != null) return;
        var interactor = gameObject.AddComponent<HandPinchInteractor>();
        var layer = LayerMask.NameToLayer(MicrophoneLayerName);
        if (layer >= 0)
            interactor.SetMicrophoneLayer(1 << layer);
    }

    /// <summary>
    /// Stable key for deduplication: category + quantized screen center (normalized 0..1 into GridBuckets).
    /// </summary>
    public static string MakeStableKey(int texWidth, int texHeight, float xmin, float ymin, float xmax, float ymax, string category)
    {
        if (texWidth <= 0 || texHeight <= 0) return category + "_0_0";
        float cx = (xmin + xmax) * 0.5f;
        float cy = (ymin + ymax) * 0.5f;
        float nx = cx / texWidth;
        float ny = cy / texHeight;
        int gx = Mathf.Clamp((int)(nx * GridBuckets), 0, GridBuckets - 1);
        int gy = Mathf.Clamp((int)(ny * GridBuckets), 0, GridBuckets - 1);
        return category + "_" + gx + "_" + gy;
    }

    /// <summary>
    /// Count of active persistent props for a category.
    /// </summary>
    public int GetCountForCategory(string category)
    {
        int count = 0;
        foreach (var kv in _discovered)
        {
            if (kv.Value.Category == category)
                count++;
        }
        return count;
    }

    private static readonly DetectionCategory[] PersistentCategoryOrder = new[]
    {
        DetectionCategory.Screen, DetectionCategory.Book, DetectionCategory.Chair, DetectionCategory.Desk,
        DetectionCategory.Keyboard, DetectionCategory.Mouse, DetectionCategory.Phone, DetectionCategory.Cup
    };

    /// <summary>Categories that currently have a persistent prop (for story selection cycle).</summary>
    public System.Collections.Generic.List<DetectionCategory> GetAvailableCategories()
    {
        var list = new System.Collections.Generic.List<DetectionCategory>();
        foreach (var cat in PersistentCategoryOrder)
        {
            string key = cat.ToString();
            if (_singleByCategory.TryGetValue(key, out var prop) && prop.GameObject != null && prop.GameObject.activeSelf)
                list.Add(cat);
        }
        return list;
    }

    /// <summary>Root GameObject for the persistent prop of this category, or null.</summary>
    public GameObject GetPersistentPropRoot(DetectionCategory category)
    {
        return _singleByCategory.TryGetValue(category.ToString(), out var prop) && prop.GameObject != null ? prop.GameObject : null;
    }

    /// <summary>MicrophoneInteractable for the persistent prop of this category, or null.</summary>
    public MicrophoneInteractable GetMicrophoneForCategory(DetectionCategory category)
    {
        if (!_singleByCategory.TryGetValue(category.ToString(), out var prop) || prop.MicrophoneGameObject == null)
            return null;
        return prop.MicrophoneGameObject.GetComponent<MicrophoneInteractable>();
    }

    /// <summary>Enable or disable pinch interaction for a category's mic. In "Choose a story" mode only the active mic should be enabled.</summary>
    public void SetMicrophoneInteractableEnabled(DetectionCategory category, bool enabled)
    {
        var mic = GetMicrophoneForCategory(category);
        if (mic != null)
            mic.SetInteractable(enabled);
    }

    /// <summary>Set all persistent mics interactable or not. Call with true to restore sandbox; false then enable only active for story mode.</summary>
    public void SetAllMicrophonesInteractableEnabled(bool enabled)
    {
        foreach (var cat in PersistentCategoryOrder)
        {
            var mic = GetMicrophoneForCategory(cat);
            if (mic != null)
                mic.SetInteractable(enabled);
        }
    }

    /// <summary>
    /// If key already discovered (or category in single-instance mode), returns existing instance.
    /// Otherwise spawns prefab if under category cap (or when single-instance), stores it, returns new instance.
    /// Returns null if cap reached and new spawn would exceed limit (non-single mode only).
    /// </summary>
    public GameObject TryGetOrCreate(string key, GameObject prefab, Vector3 worldPos, Quaternion worldRot, Vector3 scaleMultiplier, string category, Vector3 cameraForwardXZ)
    {
        if (prefab == null) return null;

        float now = Time.time;
        string lookupKey = key;
        if (singleInstancePerCategory)
        {
            if (_singleByCategory.TryGetValue(category, out var singleProp))
            {
                bool stillStabilizing = (now - singleProp.LockTime) < lockAfterSeconds;
                if (stillStabilizing)
                {
                    var eraMgr = FindAnyObjectByType<TemporalEraManager>();
                    float mult = 1f;
                    if (eraMgr != null && System.Enum.TryParse<DetectionCategory>(category, true, out var catStab))
                    {
                        var s = eraMgr.GetSlotOrDefault(catStab);
                        mult = s.scaleMultiplier > 0f ? s.scaleMultiplier : 1f;
                        singleProp.SlotScaleMultiplier = mult;
                    }
                    Vector3 combined = scaleMultiplier * mult;
                    singleProp.GameObject.transform.position = worldPos;
                    singleProp.GameObject.transform.rotation = worldRot;
                    singleProp.ScaleMultiplier = combined;
                    ApplyVisualScale(singleProp.GameObject.transform, singleProp.PrefabOriginalScale, combined);
                    if (resolveOverlaps)
                        ApplyOverlapResolution(ref singleProp, SingleKey(category), cameraForwardXZ);
                    _singleByCategory[category] = singleProp;
                    _discovered[SingleKey(category)] = singleProp;
                }
                singleProp.GameObject.SetActive(true);
                if (singleProp.MicrophoneGameObject != null && !DisableVirtualMic)
                    singleProp.MicrophoneGameObject.SetActive(true);
                return singleProp.GameObject;
            }
            lookupKey = SingleKey(category);
        }
        else if (_discovered.TryGetValue(key, out var prop))
        {
            bool stillStabilizing = (now - prop.LockTime) < lockAfterSeconds;
            if (stillStabilizing)
            {
                var eraMgr = FindAnyObjectByType<TemporalEraManager>();
                float mult = 1f;
                if (eraMgr != null && System.Enum.TryParse<DetectionCategory>(category, true, out var catStab))
                {
                    var s = eraMgr.GetSlotOrDefault(catStab);
                    mult = s.scaleMultiplier > 0f ? s.scaleMultiplier : 1f;
                    prop.SlotScaleMultiplier = mult;
                }
                Vector3 combined = scaleMultiplier * mult;
                prop.GameObject.transform.position = worldPos;
                prop.GameObject.transform.rotation = worldRot;
                prop.ScaleMultiplier = combined;
                ApplyVisualScale(prop.GameObject.transform, prop.PrefabOriginalScale, combined);
                if (resolveOverlaps)
                    ApplyOverlapResolution(ref prop, key, cameraForwardXZ);
            }
            prop.GameObject.SetActive(true);
            if (prop.MicrophoneGameObject != null && !DisableVirtualMic)
                prop.MicrophoneGameObject.SetActive(true);
            _discovered[key] = prop;
            return prop.GameObject;
        }

        if (!singleInstancePerCategory && enforceCategoryCap && GetCountForCategory(category) >= maxInstancesPerCategory)
            return null;

        var eraManager = FindAnyObjectByType<TemporalEraManager>();
        ResolvedEraSlot slot = default;
        if (eraManager != null && System.Enum.TryParse<DetectionCategory>(category, true, out var cat))
            slot = eraManager.GetSlotOrDefault(cat);
        if (slot.prefab == null)
        {
            if (eraManager != null)
                TemporalEraManager.AddMissingPrefabWarning(eraManager.CurrentEra, category);
            return null;
        }
        GameObject effectivePrefab = slot.prefab;

        float slotScale = slot.scaleMultiplier > 0f ? slot.scaleMultiplier : 1f;
        Vector3 effectiveScale = scaleMultiplier * slotScale;

        GameObject root = new GameObject("PersistentAnchor_" + category);
        root.transform.SetPositionAndRotation(worldPos, worldRot);
        root.transform.localScale = Vector3.one;

        Transform visualT = GetOrCreateVisual(root.transform);
        GameObject visualInstance = Instantiate(effectivePrefab, visualT);
        visualInstance.transform.localPosition = slot.localPositionOffset;
        visualInstance.transform.localRotation = Quaternion.Euler(slot.localRotationEuler);
        Vector3 origScale = effectivePrefab.transform.localScale;
        visualInstance.transform.localScale = Vector3.Scale(origScale, effectiveScale);

        var newProp = new PersistentProp
        {
            GameObject = root,
            MicrophoneGameObject = null,
            Key = lookupKey,
            Category = category,
            LockTime = now,
            PrefabOriginalScale = origScale,
            ScaleMultiplier = effectiveScale,
            SlotScaleMultiplier = slotScale
        };
        if (resolveOverlaps)
            ApplyOverlapResolution(ref newProp, lookupKey, cameraForwardXZ);

        GameObject mic = CreateAndSetupMicrophone(category, root.transform, slot, cameraForwardXZ, lookupKey);
        if (mic != null)
        {
            newProp.MicrophoneGameObject = mic;
            mic.transform.SetParent(root.transform, true);
            mic.SetActive(true);
        }

        _discovered[lookupKey] = newProp;
        if (singleInstancePerCategory)
            _singleByCategory[category] = newProp;
        return root;
    }

    /// <summary>
    /// DEV/demo only: create a persistent prop at a fixed position without detection. Uses current era slot. Registers in _singleByCategory so RefreshEra works.
    /// </summary>
    public GameObject CreateSeededProp(DetectionCategory category, Vector3 worldPos, Quaternion worldRot, Vector3 baseScale, bool createMic = true)
    {
        var eraManager = FindAnyObjectByType<TemporalEraManager>();
        if (eraManager == null) return null;
        var slot = eraManager.GetSlotOrDefault(category);
        if (slot.prefab == null) return null;

        string key = "SEEDED::" + category;
        string categoryStr = category.ToString();
        if (_singleByCategory.TryGetValue(categoryStr, out var existing) && existing.GameObject != null)
        {
            existing.GameObject.SetActive(true);
            if (existing.MicrophoneGameObject != null && !DisableVirtualMic) existing.MicrophoneGameObject.SetActive(true);
            return existing.GameObject;
        }

        float slotScale = slot.scaleMultiplier > 0f ? slot.scaleMultiplier : 1f;
        Vector3 effectiveScale = baseScale * slotScale;
        float now = Time.time;

        GameObject root = new GameObject("PersistentAnchor_" + categoryStr + "_Seeded");
        root.transform.SetPositionAndRotation(worldPos, worldRot);
        root.transform.localScale = Vector3.one;

        Transform visualT = GetOrCreateVisual(root.transform);
        GameObject visualInstance = Object.Instantiate(slot.prefab, visualT);
        visualInstance.transform.localPosition = slot.localPositionOffset;
        visualInstance.transform.localRotation = Quaternion.Euler(slot.localRotationEuler);
        Vector3 origScale = slot.prefab.transform.localScale;
        visualInstance.transform.localScale = Vector3.Scale(origScale, effectiveScale);

        var newProp = new PersistentProp
        {
            GameObject = root,
            MicrophoneGameObject = null,
            Key = key,
            Category = categoryStr,
            LockTime = now,
            PrefabOriginalScale = origScale,
            ScaleMultiplier = effectiveScale,
            SlotScaleMultiplier = slotScale
        };

        if (createMic)
        {
            Vector3 camFwd = Vector3.forward;
            var cam = QuestAudioHelper.FindXRCameraTransform();
            if (cam != null) { camFwd = cam.forward; camFwd.y = 0f; if (camFwd.sqrMagnitude > 0.0001f) camFwd.Normalize(); }
            GameObject mic = CreateAndSetupMicrophone(categoryStr, root.transform, slot, camFwd, key);
            if (mic != null)
            {
                newProp.MicrophoneGameObject = mic;
                mic.transform.SetParent(root.transform, true);
                mic.SetActive(true);
            }
        }

        _discovered[key] = newProp;
        _singleByCategory[categoryStr] = newProp;
        return root;
    }

    private static Transform GetOrCreateVisual(Transform root)
    {
        var t = root.Find(VisualChildName);
        if (t != null) return t;
        var go = new GameObject(VisualChildName);
        go.transform.SetParent(root, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        return go.transform;
    }

    private static void ApplyVisualScale(Transform root, Vector3 prefabOriginalScale, Vector3 scaleMultiplier)
    {
        if (scaleMultiplier.sqrMagnitude < 0.0001f)
            scaleMultiplier = Vector3.one;
        var visualT = root.Find(VisualChildName);
        if (visualT == null) return;
        for (int i = 0; i < visualT.childCount; i++)
        {
            visualT.GetChild(i).localScale = Vector3.Scale(prefabOriginalScale, scaleMultiplier);
        }
    }

    private static string SingleKey(string category) => "SINGLE::" + category;

    private static bool IsDescendantOf(Transform t, Transform potentialAncestor)
    {
        if (potentialAncestor == null) return false;
        for (var p = t; p != null; p = p.parent)
            if (p == potentialAncestor) return true;
        return false;
    }

    private void ApplyOverlapResolution(ref PersistentProp prop, string excludeKey, Vector3 cameraForwardXZ)
    {
        Vector3 camFwd = cameraForwardXZ;
        camFwd.y = 0f;
        if (camFwd.sqrMagnitude < 0.0001f) camFwd = Vector3.forward;
        else camFwd.Normalize();

        Vector3 thisPos = prop.GameObject.transform.position;
        for (int iter = 0; iter < maxSeparationIterations; iter++)
        {
            bool anyOverlap = false;
            foreach (var kv in _discovered)
            {
                if (kv.Key == excludeKey) continue;
                var other = kv.Value;
                if (other.GameObject == null || !other.GameObject.activeSelf) continue;

                Vector3 otherPos = other.GameObject.transform.position;
                Vector3 delta = thisPos - otherPos;
                delta.y = 0f;
                float d = delta.magnitude;
                if (d < 0.0001f)
                {
                    delta = camFwd;
                    d = 1f;
                }
                else
                {
                    delta /= d;
                }

                if (d < minSeparationMeters)
                {
                    thisPos += delta * (minSeparationMeters - d);
                    anyOverlap = true;
                }
            }
            if (!anyOverlap) break;
        }
        prop.GameObject.transform.position = thisPos;
    }

    private GameObject CreateAndSetupMicrophone(string category, Transform rootTransform, ResolvedEraSlot slot, Vector3 cameraForwardXZ, string excludeKey)
    {
        if (DisableVirtualMic) return null;

        GameObject micPrefab = slot.microphonePrefab != null ? slot.microphonePrefab : microphonePrefab;
        Vector3 micOffset = slot.microphoneLocalOffset != Vector3.zero ? slot.microphoneLocalOffset : microphoneOffset;
        AudioClip clipForCategory = slot.microphoneClip != null ? slot.microphoneClip : GetMicrophoneClipForCategoryFromEra(category);
        if (requireClipToSpawnMic && clipForCategory == null)
            return null;

        Vector3 mainPropPos = rootTransform.position;
        Quaternion mainPropRot = rootTransform.rotation;

        GameObject mic = micPrefab != null ? Instantiate(micPrefab) : CreateMinimalMicrophoneObject();
        mic.name = "Microphone_" + category;

        mic.transform.rotation = Quaternion.identity;
        Vector3 micPos = mainPropPos + mainPropRot * micOffset;
        mic.transform.position = micPos;

        if (resolveOverlaps)
            ApplyMicOverlapResolution(mic.transform, excludeKey, cameraForwardXZ);

        SetLayerRecursive(mic.transform, _microphoneLayer);

        var col = mic.GetComponentInChildren<Collider>();
        if (col == null)
        {
            var box = mic.AddComponent<BoxCollider>();
            var r = mic.GetComponentInChildren<Renderer>();
            if (r != null)
            {
                box.center = mic.transform.InverseTransformPoint(r.bounds.center);
                Vector3 size = r.bounds.size;
                var scale = mic.transform.lossyScale;
                if (scale.x > 0.001f && scale.y > 0.001f && scale.z > 0.001f)
                    size = new Vector3(size.x / scale.x, size.y / scale.y, size.z / scale.z);
                size *= 1.2f;
                box.size = size;
            }
            else
            {
                box.size = Vector3.one * (microphoneOverlapRadius * 2.4f);
            }
            box.isTrigger = false;
        }
        else
        {
            col.enabled = true;
            col.isTrigger = false;
        }

        var interactable = mic.GetComponent<MicrophoneInteractable>();
        if (interactable == null)
            interactable = mic.AddComponent<MicrophoneInteractable>();
        interactable.enabled = true;
        interactable.SetClip(clipForCategory);
        interactable.SetPlayGeneratedBeepInstead(micDebugForceBeep);

        return mic;
    }

    private static void SetLayerRecursive(Transform t, int layer)
    {
        if (layer < 0) return;
        t.gameObject.layer = layer;
        for (int i = 0; i < t.childCount; i++)
            SetLayerRecursive(t.GetChild(i), layer);
    }

    private GameObject CreateMinimalMicrophoneObject()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        var col = go.GetComponent<Collider>();
        if (col != null) col.isTrigger = false;
        go.transform.localScale = Vector3.one * (microphoneOverlapRadius * 2f);
        return go;
    }

    private void ApplyMicOverlapResolution(Transform micTransform, string excludeKey, Vector3 cameraForwardXZ)
    {
        Vector3 camFwd = cameraForwardXZ;
        camFwd.y = 0f;
        if (camFwd.sqrMagnitude < 0.0001f) camFwd = Vector3.forward;
        else camFwd.Normalize();

        Vector3 thisPos = micTransform.position;
        for (int iter = 0; iter < maxSeparationIterations; iter++)
        {
            bool anyOverlap = false;
            foreach (var kv in _discovered)
            {
                if (kv.Key == excludeKey) continue;
                var other = kv.Value;
                if (other.GameObject != null && other.GameObject.activeSelf)
                    TryPushApart(ref thisPos, other.GameObject.transform.position, camFwd, ref anyOverlap);
                if (other.MicrophoneGameObject != null && other.MicrophoneGameObject.activeSelf)
                    TryPushApart(ref thisPos, other.MicrophoneGameObject.transform.position, camFwd, ref anyOverlap);
            }
            if (!anyOverlap) break;
        }
        micTransform.position = thisPos;
    }

    private void TryPushApart(ref Vector3 thisPos, Vector3 otherPos, Vector3 camFwd, ref bool anyOverlap)
    {
        Vector3 delta = thisPos - otherPos;
        delta.y = 0f;
        float d = delta.magnitude;
        if (d < 0.0001f)
        {
            delta = camFwd;
            d = 1f;
        }
        else
        {
            delta /= d;
        }
        if (d < minSeparationMeters)
        {
            thisPos += delta * (minSeparationMeters - d);
            anyOverlap = true;
        }
    }

    private AudioClip GetMicrophoneClipForCategoryFromEra(string category)
    {
        var eraManager = FindAnyObjectByType<TemporalEraManager>();
        if (eraManager != null && System.Enum.TryParse<DetectionCategory>(category, true, out var cat))
        {
            var clip = eraManager.GetMicClip(cat);
            if (clip != null) return clip;
        }
        return GetMicrophoneClipForCategoryLegacy(category);
    }

    private AudioClip GetMicrophoneClipForCategoryLegacy(string category)
    {
        return category switch
        {
            "Screen" => screenMicrophoneClip,
            "Book" => bookMicrophoneClip,
            "Desk" => deskMicrophoneClip,
            "Chair" => chairMicrophoneClip,
            _ => null
        };
    }

    public bool HasKey(string key)
    {
        return _discovered.ContainsKey(key);
    }

    /// <summary>Refresh existing persistent props to the given era: swap only the Visual child contents; root anchor unchanged. Strict: if era prefab is null, disable visual and mic.</summary>
    public void RefreshEra(TemporalEra era)
    {
        var eraManager = FindAnyObjectByType<TemporalEraManager>();
        if (eraManager == null) return;

        TemporalEraManager.ClearMissingPrefabWarnings();

        foreach (var kv in _singleByCategory)
        {
            string categoryStr = kv.Key;
            var prop = kv.Value;
            if (prop.GameObject == null) continue;

            if (!System.Enum.TryParse<DetectionCategory>(categoryStr, true, out var cat))
                continue;

            if (!eraManager.TryGetSlot(cat, era, out var slot))
            {
                UpdateMicClipForProp(prop, eraManager.GetMicClip(cat));
                _singleByCategory[categoryStr] = prop;
                _discovered[SingleKey(categoryStr)] = prop;
                continue;
            }

            GameObject prefab = slot.prefab;
            Transform root = prop.GameObject.transform;
            Transform visualT = root.Find(VisualChildName);
            if (visualT == null)
            {
                visualT = GetOrCreateVisual(root);
                foreach (var r in root.GetComponentsInChildren<Renderer>())
                {
                    if (!IsDescendantOf(r.transform, visualT))
                        r.enabled = false;
                }
                foreach (var c in root.GetComponentsInChildren<Collider>())
                {
                    if (!IsDescendantOf(c.transform, visualT))
                        c.enabled = false;
                }
            }

            if (prefab != null)
            {
                visualT.gameObject.SetActive(true);
                for (int i = visualT.childCount - 1; i >= 0; i--)
                    Object.Destroy(visualT.GetChild(i).gameObject);

                GameObject visualInstance = Instantiate(prefab, visualT);
                visualInstance.transform.localPosition = slot.localPositionOffset;
                visualInstance.transform.localRotation = Quaternion.Euler(slot.localRotationEuler);
                Vector3 newPrefabScale = prefab.transform.localScale;
                float slotScale = slot.scaleMultiplier > 0f ? slot.scaleMultiplier : 1f;
                Vector3 baseMult = prop.SlotScaleMultiplier > 0.001f ? prop.ScaleMultiplier / prop.SlotScaleMultiplier : prop.ScaleMultiplier;
                Vector3 mult = baseMult * slotScale;
                visualInstance.transform.localScale = Vector3.Scale(newPrefabScale, mult);

                prop.PrefabOriginalScale = newPrefabScale;
                prop.ScaleMultiplier = mult;
                prop.SlotScaleMultiplier = slotScale;

                Vector3 micOffset = slot.microphoneLocalOffset != Vector3.zero ? slot.microphoneLocalOffset : microphoneOffset;
                AudioClip clip = slot.microphoneClip != null ? slot.microphoneClip : GetMicrophoneClipForCategoryFromEra(categoryStr);
                if (prop.MicrophoneGameObject != null && !DisableVirtualMic)
                {
                    prop.MicrophoneGameObject.SetActive(true);
                    prop.MicrophoneGameObject.transform.position = root.position + root.rotation * micOffset;
                    prop.MicrophoneGameObject.transform.rotation = Quaternion.identity;
                    UpdateMicClipForProp(prop, clip);
                }
            }
            else
            {
                TemporalEraManager.AddMissingPrefabWarning(era, categoryStr);
                visualT.gameObject.SetActive(false);
                if (prop.MicrophoneGameObject != null)
                    prop.MicrophoneGameObject.SetActive(false);
            }

            _singleByCategory[categoryStr] = prop;
            _discovered[SingleKey(categoryStr)] = prop;
        }
    }

    private void UpdateMicClipForProp(PersistentProp prop, AudioClip clip)
    {
        if (prop.MicrophoneGameObject == null || clip == null) return;
        var interactable = prop.MicrophoneGameObject.GetComponent<MicrophoneInteractable>();
        if (interactable != null)
            interactable.SetClip(clip);
    }

    /// <summary>Call to clear all persistent props and single-instance state (e.g. on session reset).</summary>
    public void ResetSession()
    {
        foreach (var kv in _discovered)
        {
            if (kv.Value.GameObject != null)
                kv.Value.GameObject.SetActive(false);
            if (kv.Value.MicrophoneGameObject != null)
                kv.Value.MicrophoneGameObject.SetActive(false);
        }
        _discovered.Clear();
        _singleByCategory.Clear();
    }

    public int DiscoveredCount => _discovered.Count;
    public bool SingleInstancePerCategory => singleInstancePerCategory;

#if UNITY_EDITOR
    public (AudioClip screen, AudioClip book, AudioClip desk, AudioClip chair) GetMicrophoneClipsForValidation() =>
        (screenMicrophoneClip, bookMicrophoneClip, deskMicrophoneClip, chairMicrophoneClip);
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public int MaxInstancesPerCategory => maxInstancesPerCategory;
    public bool EnforceCategoryCap => enforceCategoryCap;
    public bool ResolveOverlaps => resolveOverlaps;
    public float MinSeparationMeters => minSeparationMeters;
    public bool HasScreenClip => screenMicrophoneClip != null;
    public bool HasBookClip => bookMicrophoneClip != null;
    public bool HasDeskClip => deskMicrophoneClip != null;
    public bool HasChairClip => chairMicrophoneClip != null;
#endif
}
