using System;
using UnityEngine;

namespace TemporalEcho
{
    /// <summary>
    /// Single canonical orchestrator: detection → overlays from EraDatabase; era media (play once) → auto-advance on finish.
    /// UI era click → SetEra → overlays + media. Only one video source; audio plays once then advances.
    /// </summary>
    public class TemporalEchoRuntimeOrchestrator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private EraDatabase database;
        [Tooltip("Optional. When set, syncs era so DetectionPrefabReplacer/PDM update overlays on era switch.")]
        [SerializeField] private TemporalEraManager eraSync;
        [SerializeField] private EraSwitcherUI eraUI;
        [SerializeField] private EraMediaController mediaController;
        [SerializeField] private Transform screenAnchor;
        [SerializeField] private Transform bookAnchor;
        [SerializeField] private Transform humanAnchor;
        [SerializeField] private EraVideoScreen videoScreen;

        [Header("Anchor Placement (MR)")]
        [Tooltip("Position anchors in front of camera at Start so overlays are visible. Essential for MR.")]
        [SerializeField] private bool positionAnchorsInFrontOfCamera = true;
        [Tooltip("Distance in front of camera (meters).")]
        [SerializeField] private float anchorDistance = 1.5f;

        [Header("Demo / Fallback")]
        [Tooltip("When true, show all overlays (Screen, Book, Human) after delay seconds. Use to verify EraDatabase works without waiting for AI detection.")]
        [SerializeField] private bool showOverlaysOnStart = true;
        [Tooltip("Seconds to wait before applying demo overlays. Gives detection a chance to run first.")]
        [SerializeField] private float demoOverlayDelaySeconds = 1.5f;

        private const string OverlayPrefix = "EraOverlay_";
        private EraId _activeEra = EraId.E1920s;
        private bool _screenDetected;
        private bool _bookDetected;
        private bool _humanDetected;
        private bool _mediaStartedThisEra;
        private float _startTime;

        public static event Action<DetectedSubject> OnSubjectDetectedStatic;

        private void Awake()
        {
            if (database == null) database = FindAnyObjectByType<EraDatabase>();
            if (eraSync == null) eraSync = FindAnyObjectByType<TemporalEraManager>();
            if (eraUI == null) eraUI = FindAnyObjectByType<EraSwitcherUI>();
            if (mediaController == null) mediaController = GetComponent<EraMediaController>();
            if (mediaController == null) mediaController = FindAnyObjectByType<EraMediaController>();
            if (screenAnchor == null) screenAnchor = GameObject.Find("ScreenAnchor")?.transform;
            if (bookAnchor == null) bookAnchor = GameObject.Find("BookAnchor")?.transform;
            if (humanAnchor == null) humanAnchor = GameObject.Find("HumanAnchor")?.transform;
            if (videoScreen == null && screenAnchor != null) videoScreen = screenAnchor.GetComponentInChildren<EraVideoScreen>(true);
        }

        private void Start()
        {
            _startTime = Time.time;
            if (positionAnchorsInFrontOfCamera)
                PositionAnchorsInFrontOfCamera();
            if (screenAnchor == null || bookAnchor == null || humanAnchor == null)
                Debug.LogWarning("[TemporalEcho] Missing anchors. Ensure ScreenAnchor, BookAnchor, HumanAnchor exist in scene. Run Tools > Temporal Echo > Rebuild Scene to Canonical System.");
        }

        private void PositionAnchorsInFrontOfCamera()
        {
            Transform cam = QuestAudioHelper.FindXRCameraTransform();
            if (cam == null && Camera.main != null) cam = Camera.main.transform;
            if (cam == null) return;
            Vector3 pos = cam.position + cam.forward * anchorDistance;
            Vector3 right = cam.right;
            if (right.sqrMagnitude < 0.01f) right = Vector3.right;
            else right.Normalize();
            if (screenAnchor != null) { screenAnchor.position = pos + right * -0.4f; screenAnchor.rotation = Quaternion.LookRotation(cam.forward, Vector3.up); }
            if (bookAnchor != null)   { bookAnchor.position = pos; bookAnchor.rotation = Quaternion.LookRotation(cam.forward, Vector3.up); }
            if (humanAnchor != null)  { humanAnchor.position = pos + right * 0.4f; humanAnchor.rotation = Quaternion.LookRotation(cam.forward, Vector3.up); }
            Debug.Log("[TemporalEcho] Anchors positioned in front of camera.");
        }

        private void OnEnable()
        {
            if (eraUI != null)
                eraUI.OnEraSelected += OnEraSelectedFromUI;
            if (eraSync != null)
                eraSync.SetEra(ToTemporalEra(_activeEra));
            OnSubjectDetectedStatic += OnSubjectDetected;
            TemporalEchoDetectionBridge.OnSubjectDetected += OnBridgeSubjectDetected;
            if (mediaController != null)
                mediaController.OnEraMediaFinished += OnEraMediaFinished;
        }

        private void OnDisable()
        {
            if (eraUI != null)
                eraUI.OnEraSelected -= OnEraSelectedFromUI;
            OnSubjectDetectedStatic -= OnSubjectDetected;
            TemporalEchoDetectionBridge.OnSubjectDetected -= OnBridgeSubjectDetected;
            if (mediaController != null)
                mediaController.OnEraMediaFinished -= OnEraMediaFinished;
        }

        private void OnBridgeSubjectDetected(DetectedSubject subject, float confidence)
        {
            ReportDetected(subject);
        }

        private void Update()
        {
            bool anyDetected = _screenDetected || _bookDetected || _humanDetected;

            if (showOverlaysOnStart && !anyDetected && Time.time - _startTime >= demoOverlayDelaySeconds)
            {
                _screenDetected = true;
                _bookDetected = true;
                _humanDetected = true;
                anyDetected = true;
                showOverlaysOnStart = false;
                Debug.Log("[TemporalEcho] Demo mode: showing overlays from EraDatabase (no detection yet).");
            }

            if (anyDetected && !_mediaStartedThisEra)
            {
                _mediaStartedThisEra = true;
                ApplyOverlaysForCurrentlyDetectedSubjects();
                if (mediaController != null)
                    mediaController.ApplyMediaForEra(ToTemporalEra(_activeEra));
            }
            if (eraUI != null && mediaController != null)
                eraUI.SetDebugMediaState(mediaController.CurrentState);
        }

        private void OnEraSelectedFromUI(TemporalEra era)
        {
            SetEra(era);
        }

        /// <summary>Set era from UI. Stops current media, applies overlays, starts era media if any subject detected.</summary>
        public void SetEra(TemporalEra era)
        {
            _activeEra = ToEraId(era);
            _mediaStartedThisEra = false;
            if (eraSync != null) eraSync.SetEra(era);
            Debug.Log($"[TemporalEcho] Era: {era}");
            ApplyOverlaysForCurrentlyDetectedSubjects();
            if (_screenDetected || _bookDetected || _humanDetected)
            {
                _mediaStartedThisEra = true;
                if (mediaController != null)
                    mediaController.ApplyMediaForEra(era);
            }
        }

        private void OnEraMediaFinished()
        {
            _activeEra = NextEra(_activeEra);
            _mediaStartedThisEra = false;
            if (eraSync != null) eraSync.SetEra(ToTemporalEra(_activeEra));
            Debug.Log($"[TemporalEcho] Media finished → era {_activeEra}");
            ApplyOverlaysForCurrentlyDetectedSubjects();
            if (_screenDetected || _bookDetected || _humanDetected)
            {
                _mediaStartedThisEra = true;
                if (mediaController != null)
                    mediaController.ApplyMediaForEra(ToTemporalEra(_activeEra));
            }
            if (eraUI != null)
                eraUI.SetSelectedEra(ToTemporalEra(_activeEra));
        }

        private static EraId NextEra(EraId current)
        {
            return current switch { EraId.E1920s => EraId.E1960s, EraId.E1960s => EraId.E2026, _ => EraId.E1920s };
        }

        public void ReportDetected(DetectedSubject subject)
        {
            switch (subject)
            {
                case DetectedSubject.Screen: _screenDetected = true; break;
                case DetectedSubject.Book: _bookDetected = true; break;
                case DetectedSubject.Human: _humanDetected = true; break;
            }
            if (eraUI != null)
                eraUI.SetDebugDetectedSubject(subject.ToString());
            OnSubjectDetectedStatic?.Invoke(subject);
            ApplyOverlaysForCurrentlyDetectedSubjects();
        }

        private void OnSubjectDetected(DetectedSubject subject)
        {
            ApplyOverlaysForCurrentlyDetectedSubjects();
        }

        private void ApplyOverlaysForCurrentlyDetectedSubjects()
        {
            if (database == null)
            {
                Debug.LogWarning("[TemporalEcho] EraDatabase is null. Assign it in the inspector.");
                return;
            }
            foreach (var subject in new[] { DetectedSubject.Screen, DetectedSubject.Book, DetectedSubject.Human })
            {
                var anchor = GetAnchor(subject);
                if (anchor == null) continue;
                bool shouldShow = subject == DetectedSubject.Screen ? _screenDetected
                    : subject == DetectedSubject.Book ? _bookDetected
                    : _humanDetected;

                if (!shouldShow)
                {
                    ClearOverlayUnder(anchor, subject);
                    continue;
                }
                var rule = GetRuleForSubject(subject);
                if (rule.prefab == null)
                {
                    Debug.LogWarning($"[TemporalEcho] No prefab for {subject} in EraDatabase. Assign prefab in EraDatabase rules for era {_activeEra}.");
                    continue;
                }
                SpawnOrReplaceUnderAnchor(anchor, rule);
            }
        }

        /// <summary>Get prefab + offsets from EraDatabase only (single source of truth for replacements).</summary>
        private EraRule GetRuleForSubject(DetectedSubject subject)
        {
            var rule = new EraRule { subject = subject };
            if (database != null)
            {
                var def = database.Get(_activeEra);
                if (def.rules != null)
                {
                    foreach (var r in def.rules)
                        if (r.subject == subject) { rule = r; break; }
                }
            }
            return rule;
        }

        private void ClearOverlayUnder(Transform anchor, DetectedSubject subject)
        {
            string name = OverlayPrefix + subject;
            var existing = anchor.Find(name);
            if (existing != null)
                Destroy(existing.gameObject);
        }

        private Transform GetAnchor(DetectedSubject subject)
        {
            switch (subject)
            {
                case DetectedSubject.Screen: return screenAnchor;
                case DetectedSubject.Book: return bookAnchor;
                case DetectedSubject.Human: return humanAnchor;
                default: return null;
            }
        }

        private void SpawnOrReplaceUnderAnchor(Transform anchor, EraRule rule)
        {
            if (rule.prefab == null) return;

            string overlayName = OverlayPrefix + rule.subject;
            var existing = anchor.Find(overlayName);
            if (existing != null)
                Destroy(existing.gameObject);

            var instance = Instantiate(rule.prefab, anchor);
            instance.name = overlayName;
            instance.transform.localPosition = rule.localPosition;
            instance.transform.localRotation = Quaternion.Euler(rule.localEulerRotation);
            instance.transform.localScale = rule.localScale == Vector3.zero ? Vector3.one : rule.localScale;

            Debug.Log($"[TemporalEcho] Replacement spawn: subject={rule.subject} prefab={rule.prefab.name} era={_activeEra}");

            if (rule.subject == DetectedSubject.Screen) { }
            else if (rule.subject == DetectedSubject.Book) { }
            else if (rule.subject == DetectedSubject.Human) { }
        }

        private static EraId ToEraId(TemporalEra era)
        {
            switch (era)
            {
                case TemporalEra.Era1920s: return EraId.E1920s;
                case TemporalEra.Era1960s: return EraId.E1960s;
                case TemporalEra.EraToday: return EraId.E2026;
                default: return EraId.E2026;
            }
        }

        private static TemporalEra ToTemporalEra(EraId era)
        {
            switch (era)
            {
                case EraId.E1920s: return TemporalEra.Era1920s;
                case EraId.E1960s: return TemporalEra.Era1960s;
                case EraId.E2026: return TemporalEra.EraToday;
                default: return TemporalEra.EraToday;
            }
        }
    }
}
