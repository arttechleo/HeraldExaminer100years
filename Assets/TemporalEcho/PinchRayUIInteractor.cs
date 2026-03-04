using UnityEngine;

/// <summary>
/// Raycasts against UI layer on pinch-down and triggers EraButton on hit.
/// Uses same ray origin/direction as HandPinchInteractor (index tip + camera forward).
/// Add to scene once; no need to assign to EraSwitchPanel.
/// </summary>
public class PinchRayUIInteractor : MonoBehaviour
{
    [SerializeField] private LayerMask uiLayerMask = 0;
    [SerializeField] private float rayMaxDistance = 3f;

    /// <summary>Last UI collider name hit by raycast (for MRStatusOverlay debug).</summary>
    public static string LastUIHitName { get; private set; }

    private static LayerMask s_lastUIMask;

    public void SetUILayer(LayerMask mask)
    {
        uiLayerMask = mask;
        s_lastUIMask = mask;
    }

    /// <summary>For overlay debug: layer name(s) in the current UI mask.</summary>
    public static string GetUILayerMaskDebugNames()
    {
        if (s_lastUIMask == 0) return "none";
        int mask = s_lastUIMask;
        for (int i = 0; i < 32; i++)
        {
            if ((mask & (1 << i)) != 0)
            {
                string name = LayerMask.LayerToName(i);
                return string.IsNullOrEmpty(name) ? "Layer" + i : name;
            }
        }
        return "none";
    }

    private OVRHand _leftHand;
    private OVRHand _rightHand;
    private OVRSkeleton _leftSkeleton;
    private OVRSkeleton _rightSkeleton;
    private Transform _leftIndexTip;
    private Transform _rightIndexTip;
    private bool _leftPinching;
    private bool _rightPinching;
    private Transform _xrCameraTransform;
    private static readonly RaycastHit[] _hitBuffer = new RaycastHit[4];

    private void Awake()
    {
        _xrCameraTransform = QuestAudioHelper.FindXRCameraTransform();
        if (_xrCameraTransform == null)
            _xrCameraTransform = Camera.main != null ? Camera.main.transform : null;
        FindHands();
        CacheBoneTransforms();
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

    private void Update()
    {
        if (_leftSkeleton != null && _leftSkeleton.IsInitialized && _leftIndexTip == null)
            _leftIndexTip = GetIndexTipTransform(_leftSkeleton);
        if (_rightSkeleton != null && _rightSkeleton.IsInitialized && _rightIndexTip == null)
            _rightIndexTip = GetIndexTipTransform(_rightSkeleton);

        ProcessHand(_leftHand, _leftIndexTip, ref _leftPinching);
        ProcessHand(_rightHand, _rightIndexTip, ref _rightPinching);
    }

    private void ProcessHand(OVRHand hand, Transform indexTip, ref bool wasPinching)
    {
        if (hand == null || uiLayerMask == 0) return;

        bool isPinching = hand.GetFingerIsPinching(OVRHand.HandFinger.Index) || hand.GetFingerPinchStrength(OVRHand.HandFinger.Index) >= 0.7f;
        bool pinchRelease = !hand.GetFingerIsPinching(OVRHand.HandFinger.Index) && hand.GetFingerPinchStrength(OVRHand.HandFinger.Index) <= 0.4f;

        if (pinchRelease)
        {
            wasPinching = false;
            return;
        }
        if (!isPinching) return;

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
        else return;

        if (direction.sqrMagnitude < 0.0001f) direction = Vector3.forward;
        direction.Normalize();

        int hitCount = Physics.RaycastNonAlloc(origin, direction, _hitBuffer, rayMaxDistance, uiLayerMask, QueryTriggerInteraction.Ignore);
        if (hitCount <= 0 || !isPinching || wasPinching)
        {
            if (hitCount <= 0) LastUIHitName = null;
            return;
        }

        float bestDist = float.MaxValue;
        int bestIdx = -1;
        for (int i = 0; i < hitCount; i++)
        {
            var h = _hitBuffer[i];
            if (h.collider == null || h.distance >= bestDist) continue;
            bestDist = h.distance;
            bestIdx = i;
        }
        if (bestIdx >= 0)
            LastUIHitName = _hitBuffer[bestIdx].collider != null ? _hitBuffer[bestIdx].collider.gameObject.name : null;

        EraButton bestButton = bestIdx >= 0 && _hitBuffer[bestIdx].collider != null ? _hitBuffer[bestIdx].collider.GetComponent<EraButton>() : null;
        if (bestButton != null)
        {
            bestButton.Trigger();
            wasPinching = true;
            return;
        }
        for (int i = 0; i < hitCount; i++)
        {
            var h = _hitBuffer[i];
            if (h.collider == null) continue;
            var exportBtn = h.collider.GetComponent<ExportLabelsButton>();
            if (exportBtn != null) { exportBtn.Trigger(); wasPinching = true; return; }
            var labelsToggle = h.collider.GetComponent<ToggleLabelsViewButton>();
            if (labelsToggle != null) { labelsToggle.Trigger(); wasPinching = true; return; }
            var recognitionToggle = h.collider.GetComponent<ToggleRecognitionViewButton>();
            if (recognitionToggle != null) { recognitionToggle.Trigger(); wasPinching = true; return; }
            var chooseStory = h.collider.GetComponent<ChooseStoryButton>();
            if (chooseStory != null) { chooseStory.Trigger(); wasPinching = true; return; }
            var nextObj = h.collider.GetComponent<NextObjectButton>();
            if (nextObj != null) { nextObj.Trigger(); wasPinching = true; return; }
            var prevObj = h.collider.GetComponent<PrevObjectButton>();
            if (prevObj != null) { prevObj.Trigger(); wasPinching = true; return; }
            var pageBtn = h.collider.GetComponent<LabelsPageButton>();
            if (pageBtn != null) { pageBtn.Trigger(); wasPinching = true; return; }
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void OnGUI()
    {
        if (!Application.isPlaying || uiLayerMask != 0) return;
        GUI.Label(new Rect(10, 45, 500, 22), "PinchRayUIInteractor: UI layer mask is empty. Assign TemporalEchoUI layer.");
    }
#endif
}
