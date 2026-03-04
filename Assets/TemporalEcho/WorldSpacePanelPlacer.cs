using UnityEngine;

namespace TemporalEcho
{
    /// <summary>
    /// Places a world-space canvas in front of CenterEyeAnchor with smooth follow. Use on EraSwitcherCanvas.
    /// </summary>
    public class WorldSpacePanelPlacer : MonoBehaviour
    {
        public const float MinDistance = 0.45f;
        public const float MaxDistance = 0.75f;

        [Header("Reference")]
        [Tooltip("Assign OVRCameraRig CenterEyeAnchor (Main Camera). Auto-found if null.")]
        public Transform centerEye;

        [Header("Placement")]
        [Tooltip("If true, canvas follows camera at distance. If false, canvas stays at current position.")]
        public bool follow = true;
        [Tooltip("Distance in front of camera (meters). Default 0.55 for close reach.")]
        [Range(MinDistance, MaxDistance)]
        public float distance = 0.55f;
        [Tooltip("Height offset from eye level (negative = lower, 0 = eye level, positive = higher). Raised 0.05 from floor for better visibility.")]
        public float heightOffset = 0f;
        [Tooltip("Lateral offset (right positive).")]
        public float lateralOffset = 0f;
        [Tooltip("If true, rotate panel 180° around up (fix reversed facing in edge cases).")]
        public bool flip180 = false;
        [Tooltip("Smooth follow time.")]
        public float smoothTime = 0.15f;

        private Vector3 _vel;

        private void Awake()
        {
            EnsureCenterEye();
        }

        private void Start()
        {
            CalibrateNow();
        }

        private void LateUpdate()
        {
            if (!follow || centerEye == null) return;

            float d = Mathf.Clamp(distance, MinDistance, MaxDistance);
            Vector3 right = centerEye.right;
            Vector3 targetPos = centerEye.position
                + centerEye.forward * d
                + centerEye.up * heightOffset
                + right * lateralOffset;
            transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref _vel, smoothTime);

            Vector3 toCamera = (centerEye.position - transform.position).normalized;
            if (toCamera.sqrMagnitude > 0.0001f)
            {
                Quaternion rot = Quaternion.LookRotation(toCamera, Vector3.up);
                if (flip180) rot = rot * Quaternion.Euler(0f, 180f, 0f);
                transform.rotation = rot;
            }
        }

        /// <summary>One-time snap to correct position/rotation (no smoothing). Call from editor or at runtime.</summary>
        public void CalibrateNow()
        {
            EnsureCenterEye();
            if (centerEye == null) return;

            float d = Mathf.Clamp(distance, MinDistance, MaxDistance);
            Vector3 right = centerEye.right;
            transform.position = centerEye.position + centerEye.forward * d + centerEye.up * heightOffset + right * lateralOffset;
            Vector3 toCamera = (centerEye.position - transform.position).normalized;
            if (toCamera.sqrMagnitude > 0.0001f)
            {
                Quaternion rot = Quaternion.LookRotation(toCamera, Vector3.up);
                if (flip180) rot = rot * Quaternion.Euler(0f, 180f, 0f);
                transform.rotation = rot;
            }
            _vel = Vector3.zero;
        }

        private void EnsureCenterEye()
        {
            if (centerEye != null) return;
            centerEye = QuestAudioHelper.FindXRCameraTransform();
            if (centerEye == null && Camera.main != null)
                centerEye = Camera.main.transform;
        }
    }
}
