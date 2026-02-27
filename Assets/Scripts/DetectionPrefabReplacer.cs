using System;
using System.Collections.Generic;
using Meta.XR;
using Meta.XR.BuildingBlocks.AIBlocks;
using Meta.XR.EnvironmentDepth;
using UnityEngine;

/// <summary>
/// Production-ready detection → prefab overlay system for Quest MR.
/// Resolve cadence (e.g. 10 Hz) + smoothing; per-category tuning; no bbox rotation.
/// </summary>
[RequireComponent(typeof(ObjectDetectionAgent), typeof(DepthTextureAccess), typeof(EnvironmentDepthManager))]
public class DetectionPrefabReplacer : MonoBehaviour
{
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
        public float minConfidence = 0.3f;

        public void ResetToDefaults()
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

        public void ResetToDefaultsHuman()
        {
            worldPositionOffset = Vector3.zero;
            localPositionOffset = Vector3.zero;
            rotationOffsetEuler = Vector3.zero;
            localScaleMultiplier = Vector3.one;
            lockPitchAndRoll = true;
            faceCameraYawOnly = true;
            useCameraForwardYaw = false;
            minConfidence = 0.3f;
        }
    }

    #region Prefab References
    [Header("Prefabs")]
    [SerializeField] private GameObject typewriterHighPrefab;
    [SerializeField] private GameObject typewriterLowPrefab;
    [SerializeField] private GameObject newspaperfoldPrefab;
    [SerializeField] private GameObject hatPrefab;
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

    #region Per-Category Tuning
    [Header("Per-Category Tuning")]
    [SerializeField] public CategoryTuning screenTuning = new CategoryTuning();
    [SerializeField] public CategoryTuning bookTuning = new CategoryTuning();
    [SerializeField] public CategoryTuning humanTuning = new CategoryTuning();
    #endregion

    #region Hysteresis & Cadence
    [Header("Hysteresis & Cadence")]
    [Tooltip("Frames detection must persist before spawning (1 = immediate)")]
    [SerializeField] [Min(1)] private int minPersistFrames = 1;
    [Tooltip("Seconds to keep overlay after detection disappears")]
    [SerializeField] [Min(0f)] private float graceSeconds = 0.5f;
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
    [SerializeField] private bool debugDrawRay;
    [SerializeField] private bool debugDrawGizmoAtResolved;
    [SerializeField] private bool debugShowTextOverlay;
    [SerializeField] private bool debugLogBboxInfo;
    [SerializeField] private bool logLabelCounts;
    [Tooltip("Log timing: first seen -> spawned, and spawn -> first stable pose")]
    [SerializeField] private bool logTiming;
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

    private sealed class TrackedOverlay
    {
        public GameObject GameObject;
        public float LastSeenTime;
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
        public bool IsHigh; // for screen: TypewriterHIGH vs LOW
        public bool HasResolvedOnce;
    }

    private enum DetectionCategory { Unknown, Screen, Book, Human }

    private static readonly HashSet<string> ScreenLabels = new()
        { "computer", "laptop", "monitor", "tv", "screen", "television" };
    private static readonly HashSet<string> BookLabels = new() { "book", "books" };
    private static readonly HashSet<string> HumanLabels = new() { "person", "human", "people" };

    private void Awake()
    {
        _agent = GetComponent<ObjectDetectionAgent>();
        _cam = FindAnyObjectByType<PassthroughCameraAccess>();
        _depth = GetComponent<DepthTextureAccess>();
        _resolveInterval = 1f / Mathf.Max(0.5f, resolveHz);

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
        foreach (var kv in _overlays)
        {
            if (t - kv.Value.LastSeenTime > graceSeconds)
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

        var camPos = xrCameraTransform != null ? xrCameraTransform.position : _frame.Pose.position;
        var camForward = xrCameraTransform != null ? xrCameraTransform.forward : _frame.Pose.forward;
        var cameraTexture = _cam.GetTexture();
        if (cameraTexture == null) return;

        float t = Time.time;
        var seenKeys = new HashSet<string>();

        foreach (var b in batch)
        {
            var xmin = b.position.x;
            var ymin = b.position.y;
            var xmax = b.scale.x;
            var ymax = b.scale.y;

            var (baseLabel, confidence) = ExtractLabelAndConfidence(b.label);
            var category = GetCategory(baseLabel);
            if (category == DetectionCategory.Unknown) continue;

            var tuning = GetTuning(category);
            if (confidence < tuning.minConfidence) continue;

            string key = MakeKey(cameraTexture.width, cameraTexture.height, xmin, ymin, xmax, ymax, baseLabel);

            if (!_overlays.TryGetValue(key, out var overlay))
            {
                overlay = new TrackedOverlay { SeenCount = 0, FirstSeenTime = t, Key = key };
                _overlays[key] = overlay;
            }

            overlay.LastSeenTime = t;
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
            if (shouldResolve)
            {
                overlay.NextResolveTime = t + _resolveInterval;
                if (!TryResolveWorldPose(cameraTexture, xmin, ymin, xmax, ymax, category, tuning, camPos, camForward, out var worldPos, out var worldRot, out var scaleMultiplier, out var usedFallbackDepth))
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
                    Debug.Log($"[BBox] {baseLabel} center px=({cx:F0},{cy:F0}) norm=({nx:F3},{ny:F3}) depth={(usedFallbackDepth ? "fallback" : "texture")} pos={worldPos}");
                }

                overlay.TargetPosition = worldPos;
                overlay.TargetRotation = worldRot;
                overlay.TargetScaleMultiplier = scaleMultiplier;
                overlay.HasResolvedOnce = true;

                float dist = Vector3.Distance(camPos, worldPos);
                overlay.LastDistance = dist;
                overlay.IsHigh = category == DetectionCategory.Screen && dist <= nearThreshold;
            }
            else
            {
                // Keep previous target; only refresh label/confidence for debug
            }

            GameObject prefab = GetPrefabForCategory(category, overlay.TargetPosition, camPos);
            if (prefab == null) { _overlays[key] = overlay; continue; }

            bool needsNewInstance = overlay.GameObject == null || !overlay.GameObject.activeSelf;
            bool needsPrefabSwap = category == DetectionCategory.Screen && overlay.PrefabUsed != prefab;

            if (needsNewInstance || needsPrefabSwap)
            {
                if (overlay.GameObject != null)
                    overlay.GameObject.SetActive(false);

                overlay.GameObject = Instantiate(prefab);
                overlay.PrefabOriginalScale = overlay.GameObject.transform.localScale;
                overlay.PrefabUsed = prefab;
                overlay.SpawnTime = t;

                if (logTiming && overlay.FirstSeenTime >= 0)
                    Debug.Log($"[DetectionPrefabReplacer] Time to spawn: {(overlay.SpawnTime - overlay.FirstSeenTime):F2}s (key={key})");
            }

            overlay.GameObject.transform.position = overlay.TargetPosition;
            overlay.GameObject.transform.rotation = overlay.TargetRotation;
            overlay.GameObject.transform.localScale = Vector3.Scale(overlay.PrefabOriginalScale, overlay.TargetScaleMultiplier);
            overlay.GameObject.SetActive(true);

            if (debugDrawRay)
                Debug.DrawLine(camPos, overlay.TargetPosition, category == DetectionCategory.Human ? Color.red : Color.green, 0.2f);

            _overlays[key] = overlay;
        }

        if (logLabelCounts)
        {
            int screens = 0, books = 0, humans = 0;
            foreach (var b in batch)
            {
                var (baseLabel, _) = ExtractLabelAndConfidence(b.label);
                var c = GetCategory(baseLabel);
                if (c == DetectionCategory.Screen) screens++;
                else if (c == DetectionCategory.Book) books++;
                else if (c == DetectionCategory.Human) humans++;
            }
            Debug.Log($"[DetectionPrefabReplacer] Screens:{screens} Books:{books} Humans:{humans} Overlays:{_overlays.Count}");
        }
    }

    private bool TryResolveWorldPose(Texture cameraTexture, float xmin, float ymin, float xmax, float ymax,
        DetectionCategory category, CategoryTuning tuning, Vector3 camPos, Vector3 camForward,
        out Vector3 worldPos, out Quaternion worldRot, out Vector3 scaleMultiplier, out bool usedFallbackDepth)
    {
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
            px = (xmin + xmax) * 0.5f;
            py = (ymin + ymax) * 0.5f;
        }

        float normX = px / cameraTexture.width;
        float normY = py / cameraTexture.height;
        var viewport = new Vector2(normX, 1f - normY);
        var ray = _cam.ViewportPointToRay(viewport, _frame.Pose);
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
                float d = fallbackDepth;
                if (_frame.Depth != null && idx < _frame.Depth.Length)
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

        return true;
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

    private static DetectionCategory GetCategory(string baseLabel)
    {
        if (ScreenLabels.Contains(baseLabel)) return DetectionCategory.Screen;
        if (BookLabels.Contains(baseLabel)) return DetectionCategory.Book;
        if (HumanLabels.Contains(baseLabel)) return DetectionCategory.Human;
        return DetectionCategory.Unknown;
    }

    private CategoryTuning GetTuning(DetectionCategory category)
    {
        switch (category)
        {
            case DetectionCategory.Screen: return screenTuning;
            case DetectionCategory.Book: return bookTuning;
            case DetectionCategory.Human: return humanTuning;
            default: return screenTuning;
        }
    }

    private GameObject GetPrefabForCategory(DetectionCategory category, Vector3 worldPos, Vector3 camPos)
    {
        switch (category)
        {
            case DetectionCategory.Screen:
                return Vector3.Distance(camPos, worldPos) <= nearThreshold ? typewriterHighPrefab : typewriterLowPrefab;
            case DetectionCategory.Book: return newspaperfoldPrefab;
            case DetectionCategory.Human: return hatPrefab;
            default: return null;
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Reset Screen Tuning to Defaults")]
    private void ResetScreenTuningDefaults() { screenTuning?.ResetToDefaults(); }

    [ContextMenu("Reset Book Tuning to Defaults")]
    private void ResetBookTuningDefaults() { bookTuning?.ResetToDefaults(); }

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
        if (!debugShowTextOverlay || !Application.isPlaying) return;
        float y = 10f;
        foreach (var kv in _overlays)
        {
            var ov = kv.Value;
            if (ov.GameObject == null || !ov.GameObject.activeSelf) continue;
            string prefabName = ov.PrefabUsed != null ? ov.PrefabUsed.name : "?";
            if (ov.Category == DetectionCategory.Screen) prefabName += ov.IsHigh ? " (HIGH)" : " (LOW)";
            float timeToSpawn = ov.SpawnTime >= 0 && ov.FirstSeenTime >= 0 ? (ov.SpawnTime - ov.FirstSeenTime) : -1f;
            var text = $"{ov.LastLabel} | conf={ov.LastConfidence:F2} | key={ov.Key} | lastSeen={ov.LastSeenTime:F1} | timeToSpawn={timeToSpawn:F2}s | dist={ov.LastDistance:F2}m | {prefabName}";
            GUI.Label(new Rect(10, y, 800, 22), text);
            y += 22f;
        }
    }
#endif
}
