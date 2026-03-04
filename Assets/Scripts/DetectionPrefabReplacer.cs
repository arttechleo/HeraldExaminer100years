using System;
using System.Collections.Generic;
using System.Diagnostics;
using Meta.XR;
using Meta.XR.BuildingBlocks.AIBlocks;
using Meta.XR.EnvironmentDepth;
using UnityEngine;

/// <summary>
/// Production-ready detection → prefab overlay system for Quest MR.
/// Resolve cadence (e.g. 10 Hz) + smoothing; per-category tuning; no bbox rotation.
/// </summary>
[RequireComponent(typeof(ObjectDetectionAgent), typeof(DepthTextureAccess), typeof(EnvironmentDepthManager))]
[RequireComponent(typeof(PersistentDiscoveryManager))]
public class DetectionPrefabReplacer : MonoBehaviour
{
    [System.Serializable]
    public struct PlacementTuning
    {
        [Tooltip("World-space offset (meters) applied after resolve.")]
        public Vector3 worldOffset;
        [Tooltip("Screen-space offset in pixels (applied before ray).")]
        public Vector2 screenOffsetPx;
        [Tooltip("Euler degrees applied after base rotation.")]
        public Vector3 rotationOffsetEuler;
        [Tooltip("Overwrite yaw to face camera.")]
        public bool faceCameraYawOnly;
        [Tooltip("Extra yaw degrees when facing camera.")]
        public float yawOffsetDeg;
        [Tooltip("If > 0, use this depth (meters) instead of sampling.")]
        public float placementDepth;
    }

    [Serializable]
    public class CategoryTuning
    {
        [Tooltip("Applied in world space after anchor resolve")]
        public Vector3 worldPositionOffset = Vector3.zero;
        [Tooltip("Applied in local space after rotation is set")]
        public Vector3 localPositionOffset = Vector3.zero;
        [Tooltip("Euler degrees applied after base rotation")]
        public Vector3 rotationOffsetEuler = Vector3.zero;
        [Tooltip("Multiplied with prefab's original scale")]
        public Vector3 localScaleMultiplier = Vector3.one;
        [Tooltip("Keep object upright (lock pitch and roll to 0)")]
        public bool lockPitchAndRoll = true;
        [Tooltip("Rotate around world up to face camera")]
        public bool faceCameraYawOnly = false;
        [Tooltip("Use camera forward direction yaw instead of facing camera")]
        public bool useCameraForwardYaw = false;
        [Tooltip("Minimum confidence (0-1) to accept detection")]
        [Range(0f, 1f)]
        public float minConfidence = 0.35f;

        public void ResetToDefaults()
        {
            worldPositionOffset = Vector3.zero;
            localPositionOffset = Vector3.zero;
            rotationOffsetEuler = Vector3.zero;
            localScaleMultiplier = Vector3.one;
            lockPitchAndRoll = true;
            faceCameraYawOnly = false;
            useCameraForwardYaw = false;
            minConfidence = 0.35f;
        }

        public void ResetToDefaultsHuman()
        {
            worldPositionOffset = Vector3.zero;
            localPositionOffset = Vector3.zero;
            rotationOffsetEuler = Vector3.zero;
            localScaleMultiplier = Vector3.one;
            lockPitchAndRoll = true;
            faceCameraYawOnly = true;
            useCameraForwardYaw = false;
            minConfidence = 0.35f;
        }

        public void ResetToDefaultsBook()
        {
            worldPositionOffset = Vector3.zero;
            localPositionOffset = Vector3.zero;
            rotationOffsetEuler = Vector3.zero;
            localScaleMultiplier = Vector3.one;
            lockPitchAndRoll = true;
            faceCameraYawOnly = false;
            useCameraForwardYaw = false;
            minConfidence = 0.3f;
        }
    }

    #region Prefab References
    [Header("Prefabs")]
    [Tooltip("Unused in persistent mode; screen uses low-poly only.")]
    [SerializeField] private GameObject typewriterHighPrefab;
    [SerializeField] private GameObject typewriterLowPrefab;
    [SerializeField] private GameObject newspaperfoldPrefab;
    [SerializeField] private GameObject hatPrefab;
    [Tooltip("Optional: 1920s chair for Chair category (future asset).")]
    [SerializeField] private GameObject chair1920sPrefab;
    [Tooltip("Optional: desk items for Desk category (future asset).")]
    [SerializeField] private GameObject deskItemsPrefab;
    [Tooltip("Prefab to spawn on/above detected desk (assign in Inspector).")]
    [SerializeField] private GameObject deskTopPropPrefab;
    #endregion

    #region References
    [Header("References")]
    [SerializeField] private Transform xrCameraTransform;
    #endregion

    #region Screen
    [Header("Screen (Typewriter)")]
    [SerializeField] private float nearThreshold = 1.5f;
    #endregion

    #region Human Hat (legacy offsets – prefer CategoryTuning)
    [Header("Human (Hat) - Head point")]
    [SerializeField] [Range(-0.2f, 0.2f)] private float hatScreenSpaceYOffset = 0.02f;
    [SerializeField] [Range(0f, 0.3f)] private float hatWorldUpOffset = 0.08f;
    #endregion

    #region Recognition filter (allowlist)
    [Header("Recognition Filter (allowlist only)")]
    [SerializeField] private bool enableScreenCategory = true;
    [SerializeField] private bool enableBookCategory = true;
    [SerializeField] private bool enableHumanCategory = true;
    [SerializeField] private bool enableChairCategory = false;
    [SerializeField] private bool enableDeskCategory = true;
    [SerializeField] private bool enableKeyboardCategory = false;
    [SerializeField] private bool enableMouseCategory = false;
    [SerializeField] private bool enablePhoneCategory = false;
    [SerializeField] private bool enableCupCategory = false;
    #endregion

    #region Placement Tuning (Screen / Book / Desk)
    [Header("Placement Tuning (applied before freeze)")]
    [Tooltip("Screen → typewriter alignment. Rotation/world offset applied after resolve.")]
    [SerializeField] public PlacementTuning screenPlacementTuning;
    [Tooltip("Book → newspaper alignment.")]
    [SerializeField] public PlacementTuning bookPlacementTuning;
    [Tooltip("Desk → prop on top. Default worldOffset (0, 0.05, 0) places slightly above surface.")]
    [SerializeField] public PlacementTuning deskPlacementTuning = new PlacementTuning { worldOffset = new Vector3(0f, 0.05f, 0f) };
    #endregion

    [Header("Human (real-time only)")]
    [Tooltip("Frames without detection before hat is hidden.")]
    [SerializeField] [Min(1)] private int humanFramesBeforeHide = 10;

    #region Per-Category Tuning
    [Header("Per-Category Tuning")]
    [Tooltip("Typewriter HIGH (near screens). Pivot/scale/rotation for high-poly typewriter.")]
    [SerializeField] public CategoryTuning screenTuning = new CategoryTuning();
    [Tooltip("Typewriter LOW (far screens). Separate pivot/scale/rotation for low-poly typewriter.")]
    [SerializeField] public CategoryTuning typewriterLowTuning = new CategoryTuning();
    [Tooltip("Book. Min confidence.")]
    [SerializeField] public CategoryTuning bookTuning = new CategoryTuning();
    [Tooltip("Human: person, human. Min confidence.")]
    [SerializeField] public CategoryTuning humanTuning = new CategoryTuning();
    [Tooltip("Chair (future).")]
    [SerializeField] public CategoryTuning chairTuning = new CategoryTuning();
    [Tooltip("Desk (future).")]
    [SerializeField] public CategoryTuning deskTuning = new CategoryTuning();
    #endregion

    #region Hysteresis & Cadence
    [Header("Hysteresis & Cadence")]
    [Tooltip("Frames detection must persist before spawning (1 = immediate)")]
    [SerializeField] [Min(1)] private int minPersistFrames = 1;
    [Header("Grace (per-category)")]
    [Tooltip("Seconds to keep overlay after detection disappears")]
    [SerializeField] [Min(0f)] private float graceSeconds = 0.4f;
    [Tooltip("Screen overlays: extra time before despawn")]
    [SerializeField] [Min(0f)] private float screenGraceSeconds = 1.5f;
    [Tooltip("Book overlays: extra time before despawn")]
    [SerializeField] [Min(0f)] private float bookGraceSeconds = 1.5f;
    [Tooltip("Human (hat) overlays: extra time before despawn")]
    [SerializeField] [Min(0f)] private float humanGraceSeconds = 1.2f;
    [Header("Grace (camera movement)")]
    [Tooltip("Extra grace when camera moved a lot since last seen (reduces pop-off when turning head)")]
    [SerializeField] [Min(0f)] private float extraGraceOnCameraMoveSeconds = 0.75f;
    [Tooltip("Camera position move (m) to trigger extra grace")]
    [SerializeField] [Min(0.01f)] private float cameraMoveDistanceThreshold = 0.25f;
    [Tooltip("Camera yaw change (degrees) to trigger extra grace")]
    [SerializeField] [Min(1f)] private float cameraYawThresholdDegrees = 25f;
    [Tooltip("How often to re-resolve world pose (Hz). Higher = more responsive, more jitter risk.")]
    [SerializeField] [Min(0.5f)] private float resolveHz = 10f;
    [Tooltip("Position smoothing: 1 - exp(-posSmoothing * dt). Higher = snappier.")]
    [SerializeField] [Min(0.1f)] private float positionSmoothing = 12f;
    [Tooltip("Rotation smoothing: 1 - exp(-rotSmoothing * dt). Higher = snappier.")]
    [SerializeField] [Min(0.1f)] private float rotationSmoothing = 10f;
    #endregion

    #region Tracking
    [Header("Tracking")]
    [SerializeField] [Range(0.01f, 0.2f)] private float bboxQuantizeStep = 0.05f;
    #endregion

    #region Fallback
    [Header("Fallback")]
    [SerializeField] private float fallbackDepth = 1.8f;
    #endregion

    #region Scale from bbox (optional)
    [Header("Scale (optional)")]
    [Tooltip("If enabled, scale overlay from bbox height and distance. Off = use prefab scale * tuning only.")]
    [SerializeField] private bool deriveScaleFromBbox = false;
    #endregion

    #region Debug
    [Header("Debug")]
    [Tooltip("Master toggle: when OFF (default), no recognition text is shown anywhere. Detection + 3D overlays still work.")]
    [SerializeField] private bool showRecognitionText = false;
    [SerializeField] private bool debugDrawRay;
    [SerializeField] private bool debugDrawGizmoAtResolved;
    [SerializeField] private bool debugShowTextOverlay;
    [SerializeField] private bool debugLogBboxInfo;
    [SerializeField] private bool logLabelCounts;
    [Tooltip("Log timing: first seen -> spawned, detection Hz, resolve cost")]
    [SerializeField] private bool showDebugTiming;
    [Tooltip("Throttle timing logs to this interval (seconds)")]
    [SerializeField] [Min(0.5f)] private float logEveryNSeconds = 2f;
    [Tooltip("Editor only: draw bounding boxes for filtered detections in Game view")]
    [SerializeField] private bool showBoundingBoxes;
    #endregion

    private ObjectDetectionAgent _agent;
    private PassthroughCameraAccess _cam;
    private DepthTextureAccess _depth;
    private int _eyeIdx;

    private struct FrameData
    {
        public Pose Pose;
        public float[] Depth;
        public Matrix4x4[] ViewProjectionMatrix;
    }
    private FrameData _frame;

    private readonly Dictionary<string, TrackedOverlay> _overlays = new();
    private readonly List<string> _keysToRemove = new();
    private float _resolveInterval;
    private float _lastDebugLogTime = -999f;
    private int _detectionBatchCount;
    private float _detectionBatchCountStartTime;
    private float _lastResolveMs;

    private sealed class TrackedOverlay
    {
        public GameObject GameObject;
        public float LastSeenTime;
        public int LastSeenFrame;
        public Vector3 LastSeenCameraPosition;
        public float LastSeenCameraYaw;
        public int SeenCount;
        public DetectionCategory Category;
        public float FirstSeenTime = -1f;
        public float SpawnTime = -1f;
        public float NextResolveTime;
        public Vector3 TargetPosition;
        public Quaternion TargetRotation;
        public Vector3 TargetScaleMultiplier;
        public Vector3 PrefabOriginalScale;
        public GameObject PrefabUsed;
        public string LastLabel;
        public float LastConfidence;
        public string Key;
        public float LastDistance;
        public bool HasResolvedOnce;
    }

    private PersistentDiscoveryManager _persistentManager;
    private TemporalEraManager _eraManager;

#if UNITY_EDITOR
    private struct CachedBbox
    {
        public float Xmin, Ymin, Xmax, Ymax;
        public int TexWidth, TexHeight;
        public DetectionCategory Category;
    }
    private readonly List<CachedBbox> _cachedBboxesForDraw = new();
#endif

    private void Awake()
    {
        _agent = GetComponent<ObjectDetectionAgent>();
        _cam = FindAnyObjectByType<PassthroughCameraAccess>();
        _depth = GetComponent<DepthTextureAccess>();
        _resolveInterval = 1f / Mathf.Max(0.5f, resolveHz);
        if (!showRecognitionText)
        {
            var visualizer = FindAnyObjectByType<ObjectDetectionVisualizerV2>();
            if (visualizer != null)
                visualizer.enabled = false;
        }

        if (xrCameraTransform == null)
        {
            var cam = Camera.main;
            xrCameraTransform = cam != null ? cam.transform : null;
        }
        if (xrCameraTransform == null && _cam != null)
        {
            var go = new GameObject("DetectionPrefabReplacer_CameraProxy");
            go.transform.SetPositionAndRotation(_cam.GetCameraPose().position, _cam.GetCameraPose().rotation);
            xrCameraTransform = go.transform;
        }

        _eyeIdx = _cam != null && _cam.CameraPosition == PassthroughCameraAccess.CameraPositionType.Left ? 0 : 1;
        _persistentManager = GetComponent<PersistentDiscoveryManager>();
        _eraManager = FindAnyObjectByType<TemporalEraManager>();
    }

    private void OnEnable()
    {
        if (_agent != null) _agent.OnBoxesUpdated += HandleBatch;
        if (_depth != null) _depth.OnDepthTextureUpdateCPU += OnDepth;
    }

    private void OnDisable()
    {
        if (_agent != null) _agent.OnBoxesUpdated -= HandleBatch;
        if (_depth != null) _depth.OnDepthTextureUpdateCPU -= OnDepth;
    }

    private void Update()
    {
        float t = Time.time;
        _resolveInterval = 1f / Mathf.Max(0.5f, resolveHz);
        var camPos = xrCameraTransform != null ? xrCameraTransform.position : _frame.Pose.position;
        var camForward = xrCameraTransform != null ? xrCameraTransform.forward : _frame.Pose.forward;

        _keysToRemove.Clear();
        float camYaw = GetCameraYawDegrees(camForward);
        foreach (var kv in _overlays)
        {
            var ov = kv.Value;
            if (ov.Category != DetectionCategory.Human) continue;
            int framesSinceSeen = Time.frameCount - ov.LastSeenFrame;
            if (framesSinceSeen > humanFramesBeforeHide)
                _keysToRemove.Add(kv.Key);
        }
        foreach (var k in _keysToRemove)
        {
            if (_overlays.TryGetValue(k, out var ov) && ov.GameObject != null)
            {
                ov.GameObject.SetActive(false);
                _overlays.Remove(k);
            }
        }

        float dt = Time.deltaTime;
        float posT = 1f - Mathf.Exp(-positionSmoothing * dt);
        float rotT = 1f - Mathf.Exp(-rotationSmoothing * dt);

        foreach (var kv in _overlays)
        {
            var ov = kv.Value;
            if (ov.GameObject == null || !ov.GameObject.activeSelf) continue;

            ov.GameObject.transform.position = Vector3.Lerp(ov.GameObject.transform.position, ov.TargetPosition, posT);
            ov.GameObject.transform.rotation = Quaternion.Slerp(ov.GameObject.transform.rotation, ov.TargetRotation, rotT);
            var targetScale = Vector3.Scale(ov.PrefabOriginalScale, ov.TargetScaleMultiplier);
            ov.GameObject.transform.localScale = Vector3.Lerp(ov.GameObject.transform.localScale, targetScale, posT);
            _overlays[kv.Key] = ov;
        }
    }

    private void OnDepth(DepthTextureAccess.DepthFrameData d)
    {
        if (_cam == null) return;
        _frame.Pose = _cam.GetCameraPose();
        var len = d.DepthTexturePixels.Length;
        _frame.Depth = new float[len];
        d.DepthTexturePixels.CopyTo(_frame.Depth);
        _frame.ViewProjectionMatrix = d.ViewProjectionMatrix != null ? (Matrix4x4[])d.ViewProjectionMatrix.Clone() : null;
    }

    private void HandleBatch(List<BoxData> batch)
    {
        if (_cam == null || _depth == null) return;

        float t = Time.time;
        _detectionBatchCount++;
        if (_detectionBatchCountStartTime <= 0f) _detectionBatchCountStartTime = t;
        if (t - _detectionBatchCountStartTime > 10f) { _detectionBatchCount = 1; _detectionBatchCountStartTime = t; }

        var camPos = xrCameraTransform != null ? xrCameraTransform.position : _frame.Pose.position;
        var camForward = xrCameraTransform != null ? xrCameraTransform.forward : _frame.Pose.forward;
        var cameraTexture = _cam.GetTexture();
        if (cameraTexture == null) return;

#if UNITY_EDITOR
        if (showBoundingBoxes)
            _cachedBboxesForDraw.Clear();
#endif

        foreach (var b in batch)
        {
            var xmin = b.position.x;
            var ymin = b.position.y;
            var xmax = b.scale.x;
            var ymax = b.scale.y;

            var (baseLabel, confidence) = ExtractLabelAndConfidence(b.label);
            var filterCat = DetectionCategoryFilter.Classify(baseLabel);
            if (!DetectionCategoryFilter.IsAllowed(filterCat)) continue;
            var category = MapFilterCategoryToDetectionCategory(filterCat);
            if (!IsCategoryEnabled(category)) continue;

            var tuning = GetTuning(category);
            if (confidence < tuning.minConfidence) continue;

#if UNITY_EDITOR
            if (showBoundingBoxes)
                _cachedBboxesForDraw.Add(new CachedBbox { Xmin = xmin, Ymin = ymin, Xmax = xmax, Ymax = ymax, TexWidth = cameraTexture.width, TexHeight = cameraTexture.height, Category = category });
#endif

            string key = MakeKey(cameraTexture.width, cameraTexture.height, xmin, ymin, xmax, ymax, baseLabel);

            if (!_overlays.TryGetValue(key, out var overlay))
            {
                overlay = new TrackedOverlay { SeenCount = 0, FirstSeenTime = t, Key = key };
                _overlays[key] = overlay;
            }

            bool isPersistent = IsPersistentCategory(category);
            if (isPersistent && _persistentManager != null)
            {
                if (!_overlays.TryGetValue(key, out var persistOverlay))
                {
                    persistOverlay = new TrackedOverlay { SeenCount = 0, FirstSeenTime = t, Key = key, Category = category };
                    _overlays[key] = persistOverlay;
                }
                persistOverlay.SeenCount++;
                persistOverlay.LastSeenTime = t;
                persistOverlay.Category = category;
                _overlays[key] = persistOverlay;
                if (persistOverlay.SeenCount < minPersistFrames)
                    continue;
                var placementTuning = GetPlacementTuningForCategory(category);
                if (!TryResolveWorldPose(cameraTexture, xmin, ymin, xmax, ymax, category, tuning, camPos, camForward, !_persistentManager.HasKey(key), placementTuning.screenOffsetPx, placementTuning.placementDepth, out var worldPos, out var worldRot, out var scaleMultiplier, out _))
                    continue;
                ApplyPlacementTuning(ref worldPos, ref worldRot, placementTuning, camPos);
                GameObject persistentPrefab = GetPrefabForCategory(category, worldPos, camPos);
                if (persistentPrefab == null) continue;
                string stableKey = PersistentDiscoveryManager.MakeStableKey(cameraTexture.width, cameraTexture.height, xmin, ymin, xmax, ymax, category.ToString());
                Vector3 camForwardXZ = camForward;
                camForwardXZ.y = 0f;
                if (camForwardXZ.sqrMagnitude < 0.0001f) camForwardXZ = Vector3.forward;
                else camForwardXZ.Normalize();
                var go = _persistentManager.TryGetOrCreate(stableKey, persistentPrefab, worldPos, worldRot, scaleMultiplier, category.ToString(), camForwardXZ);
                if (go == null)
                {
                    _overlays.Remove(key);
                    continue;
                }
                go.SetActive(true);
                _overlays.Remove(key);
                continue;
            }

            if (!_overlays.TryGetValue(key, out overlay))
            {
                overlay = new TrackedOverlay { SeenCount = 0, FirstSeenTime = t, Key = key };
                _overlays[key] = overlay;
            }
            overlay.LastSeenTime = t;
            overlay.LastSeenFrame = Time.frameCount;
            overlay.LastSeenCameraPosition = camPos;
            overlay.LastSeenCameraYaw = GetCameraYawDegrees(camForward);
            overlay.SeenCount++;
            overlay.LastLabel = b.label;
            overlay.LastConfidence = confidence;
            overlay.Category = category;

            if (overlay.SeenCount < minPersistFrames)
            {
                _overlays[key] = overlay;
                continue;
            }

            bool shouldResolve = t >= overlay.NextResolveTime || !overlay.HasResolvedOnce;
            bool firstResolve = !overlay.HasResolvedOnce;
            if (shouldResolve)
            {
                overlay.NextResolveTime = t + _resolveInterval;
                if (!TryResolveWorldPose(cameraTexture, xmin, ymin, xmax, ymax, category, tuning, camPos, camForward, firstResolve, Vector2.zero, 0f, out var worldPos, out var worldRot, out var scaleMultiplier, out var usedFallbackDepth))
                {
                    _overlays[key] = overlay;
                    continue;
                }

                if (debugLogBboxInfo)
                {
                    float cx = (xmin + xmax) * 0.5f;
                    float cy = (ymin + ymax) * 0.5f;
                    float nx = cx / cameraTexture.width;
                    float ny = cy / cameraTexture.height;
                    UnityEngine.Debug.Log($"[BBox] {baseLabel} center px=({cx:F0},{cy:F0}) norm=({nx:F3},{ny:F3}) depth={(usedFallbackDepth ? "fallback" : "texture")} pos={worldPos}");
                }

                overlay.TargetPosition = worldPos;
                overlay.TargetRotation = worldRot;
                overlay.TargetScaleMultiplier = scaleMultiplier;
                overlay.HasResolvedOnce = true;
            }

            GameObject prefab = GetPrefabForCategory(category, overlay.TargetPosition, camPos);
            if (prefab == null) { _overlays[key] = overlay; continue; }

            bool needsNewInstance = overlay.GameObject == null || !overlay.GameObject.activeSelf;

            if (needsNewInstance)
            {
                if (overlay.GameObject != null)
                    overlay.GameObject.SetActive(false);

                overlay.GameObject = Instantiate(prefab);
                overlay.PrefabOriginalScale = overlay.GameObject.transform.localScale;
                overlay.PrefabUsed = prefab;
                overlay.SpawnTime = t;

                if (showDebugTiming && overlay.FirstSeenTime >= 0)
                {
                    float timeToSpawn = overlay.SpawnTime - overlay.FirstSeenTime;
                    if (t - _lastDebugLogTime >= logEveryNSeconds)
                    {
                        float hz = _detectionBatchCount > 0 && _detectionBatchCountStartTime < t
                            ? _detectionBatchCount / (t - _detectionBatchCountStartTime) : 0f;
                        UnityEngine.Debug.Log($"[DetectionPrefabReplacer] timeFirstSeen={overlay.FirstSeenTime:F2} timeSpawned={overlay.SpawnTime:F2} timeToSpawn={timeToSpawn:F2}s detectionHz≈{hz:F1} (key={key})");
                        _lastDebugLogTime = t;
                    }
                }
            }

            overlay.GameObject.transform.position = overlay.TargetPosition;
            overlay.GameObject.transform.rotation = overlay.TargetRotation;
            overlay.GameObject.transform.localScale = Vector3.Scale(overlay.PrefabOriginalScale, overlay.TargetScaleMultiplier);
            overlay.GameObject.SetActive(true);

            if (debugDrawRay)
                UnityEngine.Debug.DrawLine(camPos, overlay.TargetPosition, Color.red, 0.2f);

            _overlays[key] = overlay;
        }

        if (logLabelCounts)
        {
            int screens = 0, books = 0, humans = 0;
            foreach (var b in batch)
            {
                var (baseLabel, _) = ExtractLabelAndConfidence(b.label);
                var fc = DetectionCategoryFilter.Classify(baseLabel);
                if (!DetectionCategoryFilter.IsAllowed(fc)) continue;
                var c = MapFilterCategoryToDetectionCategory(fc);
                if (!IsCategoryEnabled(c)) continue;
                if (c == DetectionCategory.Screen) screens++;
                else if (c == DetectionCategory.Book) books++;
                else if (c == DetectionCategory.Human) humans++;
            }
            UnityEngine.Debug.Log($"[DetectionPrefabReplacer] Screens:{screens} Books:{books} Humans:{humans} Overlays:{_overlays.Count}");
        }

        if (showDebugTiming && t - _lastDebugLogTime >= logEveryNSeconds)
        {
            float hz = _detectionBatchCount > 0 && _detectionBatchCountStartTime < t
                ? _detectionBatchCount / (t - _detectionBatchCountStartTime) : 0f;
            UnityEngine.Debug.Log($"[DetectionPrefabReplacer] detectionHz≈{hz:F1} lastResolveMs={_lastResolveMs:F1} minPersistFrames={minPersistFrames} graceSeconds={graceSeconds}");
            _lastDebugLogTime = t;
        }
    }

    /// <summary>Test mode: set all 8 prop categories (Screen, Book, Chair, Desk, Keyboard, Mouse, Phone, Cup) on or off. Does not change Human.</summary>
    public void SetAllCategoriesEnabledForTest(bool enabled)
    {
        enableScreenCategory = enabled;
        enableBookCategory = enabled;
        enableChairCategory = enabled;
        enableDeskCategory = enabled;
        enableKeyboardCategory = enabled;
        enableMouseCategory = enabled;
        enablePhoneCategory = enabled;
        enableCupCategory = enabled;
    }

    /// <summary>Test mode: set one category enabled/disabled.</summary>
    public void SetCategoryEnabledForTest(DetectionCategory cat, bool enabled)
    {
        switch (cat)
        {
            case DetectionCategory.Screen: enableScreenCategory = enabled; break;
            case DetectionCategory.Book: enableBookCategory = enabled; break;
            case DetectionCategory.Chair: enableChairCategory = enabled; break;
            case DetectionCategory.Desk: enableDeskCategory = enabled; break;
            case DetectionCategory.Keyboard: enableKeyboardCategory = enabled; break;
            case DetectionCategory.Mouse: enableMouseCategory = enabled; break;
            case DetectionCategory.Phone: enablePhoneCategory = enabled; break;
            case DetectionCategory.Cup: enableCupCategory = enabled; break;
            default: break;
        }
    }

    /// <summary>Test mode: show recognition text overlay when true.</summary>
    public void SetShowRecognitionTextForTest(bool enabled) => showRecognitionText = enabled;

    private bool IsCategoryEnabled(DetectionCategory category)
    {
        return category switch
        {
            DetectionCategory.Screen => enableScreenCategory,
            DetectionCategory.Book => enableBookCategory,
            DetectionCategory.Human => enableHumanCategory,
            DetectionCategory.Chair => enableChairCategory,
            DetectionCategory.Desk => enableDeskCategory,
            DetectionCategory.Keyboard => enableKeyboardCategory,
            DetectionCategory.Mouse => enableMouseCategory,
            DetectionCategory.Phone => enablePhoneCategory,
            DetectionCategory.Cup => enableCupCategory,
            _ => false
        };
    }

    private static bool IsPersistentCategory(DetectionCategory category)
    {
        return category == DetectionCategory.Screen || category == DetectionCategory.Book
            || category == DetectionCategory.Chair || category == DetectionCategory.Desk
            || category == DetectionCategory.Keyboard || category == DetectionCategory.Mouse
            || category == DetectionCategory.Phone || category == DetectionCategory.Cup;
    }

    private bool TryResolveWorldPose(Texture cameraTexture, float xmin, float ymin, float xmax, float ymax,
        DetectionCategory category, CategoryTuning tuning, Vector3 camPos, Vector3 camForward,
        bool useFallbackDepthOnly,
        Vector2 screenOffsetPx,
        float placementDepthOverride,
        out Vector3 worldPos, out Quaternion worldRot, out Vector3 scaleMultiplier, out bool usedFallbackDepth)
    {
        var sw = showDebugTiming ? Stopwatch.StartNew() : default;
        worldPos = default;
        worldRot = Quaternion.identity;
        scaleMultiplier = tuning.localScaleMultiplier;
        usedFallbackDepth = true;

        float px, py;
        if (category == DetectionCategory.Human)
        {
            px = (xmin + xmax) * 0.5f;
            py = ymin - hatScreenSpaceYOffset * cameraTexture.height;
        }
        else
        {
            px = (xmin + xmax) * 0.5f + screenOffsetPx.x;
            py = (ymin + ymax) * 0.5f + screenOffsetPx.y;
        }

        float normX = px / cameraTexture.width;
        float normY = py / cameraTexture.height;
        var viewport = new Vector2(normX, 1f - normY);
        var ray = _cam.ViewportPointToRay(viewport, _frame.Pose);

        float depthToUse = placementDepthOverride > 0f ? placementDepthOverride : fallbackDepth;
        if (useFallbackDepthOnly || placementDepthOverride > 0f)
        {
            worldPos = ray.origin + ray.direction * depthToUse;
            usedFallbackDepth = true;
        }
        else
        {
            var world1M = ray.origin + ray.direction;
            if (_frame.ViewProjectionMatrix == null || _frame.ViewProjectionMatrix.Length <= _eyeIdx)
            {
                worldPos = ray.origin + ray.direction * fallbackDepth;
                usedFallbackDepth = true;
            }
            else
            {
                var clip = _frame.ViewProjectionMatrix[_eyeIdx] * new Vector4(world1M.x, world1M.y, world1M.z, 1f);
                if (clip.w <= 0)
                {
                    worldPos = ray.origin + ray.direction * fallbackDepth;
                    usedFallbackDepth = true;
                }
                else
                {
                    var uv = (new Vector2(clip.x, clip.y) / clip.w) * 0.5f + Vector2.one * 0.5f;
                    var texSize = DepthTextureAccess.TextureSize;
                    var sx = Mathf.Clamp((int)(uv.x * texSize), 0, texSize - 1);
                    var sy = Mathf.Clamp((int)(uv.y * texSize), 0, texSize - 1);
                    var idx = _eyeIdx * texSize * texSize + sy * texSize + sx;
                    float d = placementDepthOverride > 0f ? placementDepthOverride : fallbackDepth;
                    if (placementDepthOverride <= 0f && _frame.Depth != null && idx < _frame.Depth.Length)
                    {
                        var sampled = _frame.Depth[idx];
                        if (sampled > 0 && sampled < 20 && !float.IsInfinity(sampled))
                        {
                            d = sampled;
                            usedFallbackDepth = false;
                        }
                    }
                    worldPos = ray.origin + ray.direction * d;
                }
            }
        }

        if (category == DetectionCategory.Human)
            worldPos += Vector3.up * hatWorldUpOffset;

        worldPos += tuning.worldPositionOffset;

        float yawDeg;
        if (tuning.faceCameraYawOnly)
            yawDeg = Quaternion.LookRotation(worldPos - camPos).eulerAngles.y;
        else if (tuning.useCameraForwardYaw)
            yawDeg = Quaternion.LookRotation(camForward).eulerAngles.y;
        else
            yawDeg = Quaternion.LookRotation(camForward).eulerAngles.y;

        if (tuning.lockPitchAndRoll)
            worldRot = Quaternion.Euler(0, yawDeg, 0);
        else
            worldRot = Quaternion.Euler(0, yawDeg, 0);

        worldRot *= Quaternion.Euler(tuning.rotationOffsetEuler);
        worldPos += worldRot * tuning.localPositionOffset;

        if (deriveScaleFromBbox)
        {
            float bboxH = (ymax - ymin) / cameraTexture.height;
            float dist = Vector3.Distance(camPos, worldPos);
            float approxHeight = Mathf.Tan(Mathf.Deg2Rad * 30f) * 2f * dist * bboxH;
            float scaleMul = Mathf.Clamp(approxHeight / 0.3f, 0.3f, 3f);
            scaleMultiplier *= scaleMul;
        }

        if (showDebugTiming && sw.IsRunning) { sw.Stop(); _lastResolveMs = (float)sw.Elapsed.TotalMilliseconds; }
        return true;
    }

    private PlacementTuning GetPlacementTuningForCategory(DetectionCategory category)
    {
        return category switch
        {
            DetectionCategory.Screen => screenPlacementTuning,
            DetectionCategory.Book => bookPlacementTuning,
            DetectionCategory.Desk => deskPlacementTuning,
            DetectionCategory.Keyboard => deskPlacementTuning,
            DetectionCategory.Mouse => deskPlacementTuning,
            DetectionCategory.Phone => deskPlacementTuning,
            DetectionCategory.Cup => deskPlacementTuning,
            _ => default
        };
    }

    private void ApplyPlacementTuning(ref Vector3 worldPos, ref Quaternion worldRot, PlacementTuning pt, Vector3 camPos)
    {
        worldPos += pt.worldOffset;
        if (pt.faceCameraYawOnly)
        {
            float yawDeg = Quaternion.LookRotation(worldPos - camPos).eulerAngles.y;
            worldRot = Quaternion.Euler(0f, yawDeg + pt.yawOffsetDeg, 0f);
        }
        worldRot *= Quaternion.Euler(pt.rotationOffsetEuler);
    }

    private string MakeKey(int texWidth, int texHeight, float xmin, float ymin, float xmax, float ymax, string label)
    {
        float cxNorm = (xmin + xmax) * 0.5f / texWidth;
        float cyNorm = (ymin + ymax) * 0.5f / texHeight;
        int qx = Mathf.RoundToInt(cxNorm / bboxQuantizeStep);
        int qy = Mathf.RoundToInt(cyNorm / bboxQuantizeStep);
        return $"{label}_{qx}_{qy}";
    }

    private static (string baseLabel, float confidence) ExtractLabelAndConfidence(string label)
    {
        if (string.IsNullOrEmpty(label)) return ("", 0f);
        var parts = label.Trim().Split(' ');
        string baseLabel = parts.Length > 0 ? parts[0].ToLowerInvariant() : "";
        float conf = 0.5f;
        if (parts.Length > 1 && float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var c))
            conf = c;
        return (baseLabel, conf);
    }

    private static DetectionCategory MapFilterCategoryToDetectionCategory(DetectionCategoryFilter.Category filterCat)
    {
        return filterCat switch
        {
            DetectionCategoryFilter.Category.Human => DetectionCategory.Human,
            DetectionCategoryFilter.Category.Screen => DetectionCategory.Screen,
            DetectionCategoryFilter.Category.Book => DetectionCategory.Book,
            DetectionCategoryFilter.Category.Chair => DetectionCategory.Chair,
            DetectionCategoryFilter.Category.Desk => DetectionCategory.Desk,
            DetectionCategoryFilter.Category.Keyboard => DetectionCategory.Keyboard,
            DetectionCategoryFilter.Category.Mouse => DetectionCategory.Mouse,
            DetectionCategoryFilter.Category.Phone => DetectionCategory.Phone,
            DetectionCategoryFilter.Category.Cup => DetectionCategory.Cup,
            _ => DetectionCategory.Unknown
        };
    }

    private CategoryTuning GetTuning(DetectionCategory category)
    {
        switch (category)
        {
            case DetectionCategory.Screen: return screenTuning;
            case DetectionCategory.Book: return bookTuning;
            case DetectionCategory.Human: return humanTuning;
            case DetectionCategory.Chair: return chairTuning;
            case DetectionCategory.Desk: return deskTuning;
            case DetectionCategory.Keyboard:
            case DetectionCategory.Mouse:
            case DetectionCategory.Phone:
            case DetectionCategory.Cup: return deskTuning;
            default: return screenTuning;
        }
    }

    private float GetGraceSecondsForCategory(DetectionCategory category)
    {
        return category switch
        {
            DetectionCategory.Screen => screenGraceSeconds,
            DetectionCategory.Book => bookGraceSeconds,
            DetectionCategory.Human => humanGraceSeconds,
            DetectionCategory.Chair => graceSeconds,
            DetectionCategory.Desk => graceSeconds,
            DetectionCategory.Keyboard => graceSeconds,
            DetectionCategory.Mouse => graceSeconds,
            DetectionCategory.Phone => graceSeconds,
            DetectionCategory.Cup => graceSeconds,
            _ => graceSeconds
        };
    }

    private static float GetCameraYawDegrees(Vector3 camForward)
    {
        return Quaternion.LookRotation(camForward).eulerAngles.y;
    }

    private GameObject GetPrefabForCategory(DetectionCategory category, Vector3 worldPos, Vector3 camPos)
    {
        if (_eraManager != null)
        {
            var prefab = _eraManager.GetPrefab(category);
            if (prefab != null) return prefab;
        }
        switch (category)
        {
            case DetectionCategory.Screen: return typewriterLowPrefab;
            case DetectionCategory.Book: return newspaperfoldPrefab;
            case DetectionCategory.Human: return hatPrefab;
            case DetectionCategory.Chair: return chair1920sPrefab;
            case DetectionCategory.Desk: return deskTopPropPrefab != null ? deskTopPropPrefab : deskItemsPrefab;
            case DetectionCategory.Keyboard:
            case DetectionCategory.Mouse:
            case DetectionCategory.Phone:
            case DetectionCategory.Cup: return null;
            default: return null;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (enableScreenCategory && enableHumanCategory && !enableBookCategory)
            UnityEngine.Debug.LogWarning("[DetectionPrefabReplacer] Book category is disabled. Enable 'Enable Book Category' to spawn newspaper on books.");
    }

    [ContextMenu("Reset Screen (Typewriter HIGH) Tuning to Defaults")]
    private void ResetScreenTuningDefaults() { screenTuning?.ResetToDefaults(); }

    [ContextMenu("Reset Typewriter LOW Tuning to Defaults")]
    private void ResetTypewriterLowTuningDefaults() { typewriterLowTuning?.ResetToDefaults(); }

    [ContextMenu("Reset Book Tuning to Defaults")]
    private void ResetBookTuningDefaults() { bookTuning?.ResetToDefaultsBook(); }

    [ContextMenu("Reset Human Tuning to Defaults")]
    private void ResetHumanTuningDefaults() { humanTuning?.ResetToDefaultsHuman(); }

    private void OnDrawGizmos()
    {
        if (!debugDrawGizmoAtResolved || !Application.isPlaying) return;
        foreach (var kv in _overlays)
        {
            var ov = kv.Value;
            if (ov.GameObject == null || !ov.GameObject.activeSelf) continue;
            Gizmos.color = ov.Category == DetectionCategory.Screen ? Color.blue : ov.Category == DetectionCategory.Book ? Color.yellow : Color.red;
            Gizmos.DrawWireSphere(ov.TargetPosition, 0.05f);
        }
    }

    private void OnGUI()
    {
        if (!Application.isPlaying) return;
        float y = 10f;
        if (_persistentManager != null && (showRecognitionText || showDebugTiming))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var cap = _persistentManager.EnforceCategoryCap ? _persistentManager.MaxInstancesPerCategory.ToString() : "off";
            var counts = $"Screen={_persistentManager.GetCountForCategory("Screen")} Book={_persistentManager.GetCountForCategory("Book")} Desk={_persistentManager.GetCountForCategory("Desk")} Chair={_persistentManager.GetCountForCategory("Chair")}";
            var sep = _persistentManager.ResolveOverlaps ? $" sep={_persistentManager.MinSeparationMeters:F1}m" : "";
            GUI.Label(new Rect(10, y, 600, 22), $"Persistent Cap: {cap} | Counts: {counts}{sep}");
            y += 22f;
#endif
        }
        if (showRecognitionText && debugShowTextOverlay)
        {
            foreach (var kv in _overlays)
            {
                var ov = kv.Value;
                if (ov.GameObject == null || !ov.GameObject.activeSelf) continue;
                string prefabName = ov.PrefabUsed != null ? ov.PrefabUsed.name : "?";
                if (ov.Category == DetectionCategory.Screen) prefabName += " (low)";
                float timeToSpawn = ov.SpawnTime >= 0 && ov.FirstSeenTime >= 0 ? (ov.SpawnTime - ov.FirstSeenTime) : -1f;
                var text = $"{ov.LastLabel} | conf={ov.LastConfidence:F2} | key={ov.Key} | lastSeen={ov.LastSeenTime:F1} | timeToSpawn={timeToSpawn:F2}s | dist={ov.LastDistance:F2}m | {prefabName}";
                GUI.Label(new Rect(10, y, 800, 22), text);
                y += 22f;
            }
        }
#if UNITY_EDITOR
        if (showBoundingBoxes && _cachedBboxesForDraw.Count > 0 && Screen.width > 0 && Screen.height > 0)
        {
            foreach (var bb in _cachedBboxesForDraw)
            {
                if (bb.TexWidth <= 0 || bb.TexHeight <= 0) continue;
                float l = (bb.Xmin / bb.TexWidth) * Screen.width;
                float t = (bb.Ymin / bb.TexHeight) * Screen.height;
                float w = ((bb.Xmax - bb.Xmin) / bb.TexWidth) * Screen.width;
                float h = ((bb.Ymax - bb.Ymin) / bb.TexHeight) * Screen.height;
                var rect = new Rect(l, t, w, h);
                Color c = bb.Category == DetectionCategory.Screen ? Color.blue : bb.Category == DetectionCategory.Book ? Color.yellow : Color.red;
                c.a = 0.5f;
                var prev = GUI.color;
                GUI.color = c;
                GUI.Box(rect, "");
                GUI.color = prev;
            }
        }
#endif
    }
#endif
}
