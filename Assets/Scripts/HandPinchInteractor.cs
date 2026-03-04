using UnityEngine;

/// <summary>
/// Pure hand-tracking pinch interaction for microphone objects.
/// Uses raycast from index tip (or PointerPose fallback). Draws visible ray line while pinching.
/// Requires OVRHand/OVRSkeleton. No controller, gaze, or keyboard.
/// </summary>
public class HandPinchInteractor : MonoBehaviour
{
    [Header("Pinch Detection")]
    [SerializeField] private float pinchThreshold = 0.7f;
    [SerializeField] private float pinchReleaseThreshold = 0.4f;

    [Header("Raycast")]
    [SerializeField] private LayerMask microphoneLayerMask = -1;
    [SerializeField] private float rayMaxDistance = 3f;
    [SerializeField] private float rayThickness = 0.003f;

    public void SetMicrophoneLayer(LayerMask mask) => microphoneLayerMask = mask;

    private OVRHand _leftHand;
    private OVRHand _rightHand;
    private OVRSkeleton _leftSkeleton;
    private OVRSkeleton _rightSkeleton;

    private Transform _leftIndexTip;
    private Transform _rightIndexTip;

    private bool _leftPinching;
    private bool _rightPinching;

    private LineRenderer _leftLine;
    private LineRenderer _rightLine;
    private Material _lineMaterial;

    private static readonly RaycastHit[] _hitBuffer = new RaycastHit[4];

#if DEVELOPMENT_BUILD
    private float _leftPinchStrength;
    private float _rightPinchStrength;
    private bool _leftIsPinching;
    private bool _rightIsPinching;
    private string _lastHitName;
    private string _lastHitRootName;
    private string _lastHitLayerName;
    private string _lastHitColliderName;
    private float _lastHitDistance;
    private string _lastTriggeredMicName;
    private int _lastTriggeredMicTriggerCount;
    private string _lastHitNoInteractableMessage;
    private MicrophoneInteractable _lastHitMic;
    private MicrophoneInteractable _lastTriggeredMic;
    private int _lastHitMicId;
    private int _lastTriggeredMicId;
    private int _listenerCount;
    private string _activeListenerName;
#endif

    private Transform _xrCameraTransform;

    private void Awake()
    {
        _xrCameraTransform = QuestAudioHelper.FindXRCameraTransform();
        if (_xrCameraTransform == null)
            _xrCameraTransform = Camera.main != null ? Camera.main.transform : null;
        FindHands();
        CacheBoneTransforms();
        CreateLineRenderers();
    }

    private void FindHands()
    {
        var hands = FindObjectsByType<OVRHand>(FindObjectsSortMode.None);
        foreach (var h in hands)
        {
            var handType = h.GetHand();
            if (handType == OVRPlugin.Hand.HandLeft) _leftHand = h;
            else if (handType == OVRPlugin.Hand.HandRight) _rightHand = h;
        }
        var skeletons = FindObjectsByType<OVRSkeleton>(FindObjectsSortMode.None);
        foreach (var s in skeletons)
        {
            var skelType = s.GetSkeletonType();
            if (skelType == OVRSkeleton.SkeletonType.HandLeft) _leftSkeleton = s;
            else if (skelType == OVRSkeleton.SkeletonType.HandRight) _rightSkeleton = s;
        }
    }

    private void CacheBoneTransforms()
    {
        _leftIndexTip = GetIndexTipTransform(_leftSkeleton);
        _rightIndexTip = GetIndexTipTransform(_rightSkeleton);
    }

    private static Transform GetIndexTipTransform(OVRSkeleton skeleton)
    {
        if (skeleton == null) return null;
        var bones = skeleton.Bones;
        for (int i = 0; i < bones.Count; i++)
        {
            if (bones[i].Id == OVRSkeleton.BoneId.Hand_IndexTip)
                return bones[i].Transform;
        }
        return null;
    }

    private void CreateLineRenderers()
    {
        var shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        _lineMaterial = shader != null ? new Material(shader) : new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended"));

        _leftLine = CreateLineRenderer("LeftPinchRay");
        _rightLine = CreateLineRenderer("RightPinchRay");
    }

    private LineRenderer CreateLineRenderer(string name)
    {
        var go = new GameObject(name);
        var lr = go.AddComponent<LineRenderer>();
        lr.material = _lineMaterial;
        lr.startWidth = rayThickness;
        lr.endWidth = rayThickness * 0.5f;
        lr.positionCount = 2;
        lr.useWorldSpace = true;
        lr.startColor = lr.endColor = Color.cyan;
        lr.enabled = false;
        return lr;
    }

    private void Update()
    {
        if (_xrCameraTransform == null)
            _xrCameraTransform = QuestAudioHelper.FindXRCameraTransform();
        if (_xrCameraTransform == null && Camera.main != null)
            _xrCameraTransform = Camera.main.transform;

        if (_leftSkeleton != null && _leftSkeleton.IsInitialized && _leftIndexTip == null)
            _leftIndexTip = GetIndexTipTransform(_leftSkeleton);
        if (_rightSkeleton != null && _rightSkeleton.IsInitialized && _rightIndexTip == null)
            _rightIndexTip = GetIndexTipTransform(_rightSkeleton);

        ProcessHand(_leftHand, _leftSkeleton, _leftIndexTip, ref _leftPinching, _leftLine);
        ProcessHand(_rightHand, _rightSkeleton, _rightIndexTip, ref _rightPinching, _rightLine);
    }

    private void ProcessHand(OVRHand hand, OVRSkeleton skeleton, Transform indexTip, ref bool wasPinching, LineRenderer line)
    {
        if (hand == null)
        {
            if (line != null) line.enabled = false;
            return;
        }

        bool isPinchingApi = hand.GetFingerIsPinching(OVRHand.HandFinger.Index);
        float strength = hand.GetFingerPinchStrength(OVRHand.HandFinger.Index);

        bool pinchDown = isPinchingApi || strength >= pinchThreshold;
        bool pinchRelease = !isPinchingApi && strength <= pinchReleaseThreshold;

#if DEVELOPMENT_BUILD
        if (hand == _leftHand) { _leftPinchStrength = strength; _leftIsPinching = isPinchingApi; }
        else if (hand == _rightHand) { _rightPinchStrength = strength; _rightIsPinching = isPinchingApi; }
#endif

        if (pinchRelease)
        {
            wasPinching = false;
            if (line != null) line.enabled = false;
            return;
        }

        if (!pinchDown)
        {
            if (line != null) line.enabled = false;
            return;
        }

        Vector3 origin;
        Vector3 direction;

        if (indexTip != null)
        {
            origin = indexTip.position;
            direction = _xrCameraTransform != null ? _xrCameraTransform.forward : (hand.PointerPose != null ? hand.PointerPose.forward : Vector3.forward);
        }
        else if (hand.PointerPose != null)
        {
            origin = hand.PointerPose.position;
            direction = hand.PointerPose.forward;
        }
        else
        {
            if (line != null) line.enabled = false;
            return;
        }
        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector3.forward;
        direction.Normalize();

        int hitCount = Physics.RaycastNonAlloc(origin, direction, _hitBuffer, rayMaxDistance, microphoneLayerMask, QueryTriggerInteraction.Ignore);

        RaycastHit? bestHit = null;
        float bestDist = float.MaxValue;
        for (int i = 0; i < hitCount; i++)
        {
            var h = _hitBuffer[i];
            if (h.collider != null && h.distance < bestDist)
            {
                bestDist = h.distance;
                bestHit = h;
            }
        }

        if (pinchDown && !wasPinching)
        {
            MicrophoneInteractable hitMic = null;
            if (bestHit.HasValue)
            {
                var hitCol = bestHit.Value.collider;
                hitMic = hitCol.GetComponentInParent<MicrophoneInteractable>();
                if (hitMic == null)
                    hitMic = hitCol.GetComponentInChildren<MicrophoneInteractable>();
                if (hitMic != null)
                {
                    hitMic.Trigger();
#if DEVELOPMENT_BUILD
                    _lastTriggeredMicId = hitMic.GetInstanceID();
                    _lastTriggeredMicName = hitMic.gameObject.name;
                    _lastTriggeredMicTriggerCount = hitMic.TriggerCount;
                    _lastTriggeredMic = hitMic;
                    _lastHitNoInteractableMessage = null;
#endif
                }
#if DEVELOPMENT_BUILD
                _lastHitName = hitCol.gameObject.name;
                _lastHitRootName = hitCol.transform.root.name;
                _lastHitColliderName = hitCol.gameObject.name;
                _lastHitLayerName = LayerMask.LayerToName(hitCol.gameObject.layer);
                _lastHitDistance = bestHit.Value.distance;
                _lastHitMic = hitMic;
                _lastHitMicId = hitMic != null ? hitMic.GetInstanceID() : 0;
                if (hitMic == null)
                    _lastHitNoInteractableMessage = "HIT MICROPHONE LAYER BUT NO MicInteractable FOUND";
                _listenerCount = hitMic != null ? hitMic.ListenerCount : 0;
                _activeListenerName = hitMic != null ? hitMic.ActiveListenerName : "";
#endif
            }
#if DEVELOPMENT_BUILD
            else
            {
                _lastHitName = null;
                _lastHitRootName = null;
                _lastHitLayerName = null;
                _lastHitColliderName = null;
                _lastHitDistance = 0f;
                _lastTriggeredMicName = null;
                _lastTriggeredMicTriggerCount = 0;
                _lastHitNoInteractableMessage = null;
                _lastHitMic = null;
                _lastTriggeredMic = null;
                _lastHitMicId = 0;
                _lastTriggeredMicId = 0;
                _listenerCount = 0;
                _activeListenerName = "";
            }
#endif
            wasPinching = true;
        }

        if (line != null && pinchDown)
        {
            line.enabled = true;
            Vector3 end = bestHit.HasValue ? bestHit.Value.point : origin + direction * rayMaxDistance;
            line.SetPosition(0, origin);
            line.SetPosition(1, end);
        }
    }

#if DEVELOPMENT_BUILD
    private void OnGUI()
    {
        if (!Application.isPlaying) return;
        float y = 10f;
        GUI.Label(new Rect(10, y, 650, 22), $"Pinch L: str={_leftPinchStrength:F2} isPinch={_leftIsPinching} | R: str={_rightPinchStrength:F2} isPinch={_rightIsPinching}");
        y += 22f;
        GUI.Label(new Rect(10, y, 650, 22), $"Ray hit: layer={(_lastHitLayerName ?? "none")} collider={(_lastHitColliderName ?? "-")} dist={_lastHitDistance:F2}m");
        y += 22f;
        GUI.Label(new Rect(10, y, 650, 22), $"LastTriggeredMic: {(_lastTriggeredMicName ?? "-")} TriggerCount={_lastTriggeredMicTriggerCount}");
        y += 22f;
        if (!string.IsNullOrEmpty(_lastHitNoInteractableMessage))
        {
            GUI.Label(new Rect(10, y, 650, 22), _lastHitNoInteractableMessage);
            y += 22f;
        }
        GUI.Label(new Rect(10, y, 650, 22), $"LastHitMicID={_lastHitMicId} LastTriggeredMicID={_lastTriggeredMicId} (match={_lastHitMicId == _lastTriggeredMicId})");
        y += 22f;
        GUI.Label(new Rect(10, y, 650, 22), $"ListenerCount={_listenerCount} ActiveListener={_activeListenerName}");
        y += 22f;
        var manager = FindFirstObjectByType<PersistentDiscoveryManager>();
        bool micDebugBeepOn = manager != null && manager.MicDebugForceBeep;
        bool singleInstance = manager != null && manager.SingleInstancePerCategory;
        GUI.Label(new Rect(10, y, 650, 22), $"singleInstancePerCategory={singleInstance} micDebugForceBeep={micDebugBeepOn}");
        y += 22f;

        var focus = FindFirstObjectByType<AudioFocusManager>();
        string activeSourceName = focus != null ? focus.ActiveAudioSourceName : "none";
        string activeClipName = focus != null ? focus.ActiveClipName : "none";
        GUI.Label(new Rect(10, y, 650, 22), $"ActiveAudioSource={activeSourceName} ActiveClip={activeClipName}");
        y += 22f;

        var mic = _lastTriggeredMic;
        string mode = mic != null ? (mic.PlayGeneratedBeepInstead ? "BEEP" : "CLIP") : "-";
        GUI.Label(new Rect(10, y, 650, 22), $"Mode={mode} AssignedClipName={(mic != null ? mic.AssignedClipName : "-")} HasClip={mic != null && mic.HasClip}");
        y += 22f;
        GUI.Label(new Rect(10, y, 650, 22), $"LastTriggerSuccess={mic != null && mic.LastTriggerSuccess} Reason={(mic != null ? mic.LastTriggerReason : "-")}");
        y += 22f;
        GUI.Label(new Rect(10, y, 650, 22), $"IsPlaying={mic != null && mic.IsPlaying} LastTriggerTime={(mic != null ? mic.LastTriggerTime.ToString("F1") : "-")}");
    }
#endif
}
