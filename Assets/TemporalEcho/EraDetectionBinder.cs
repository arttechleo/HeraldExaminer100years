using System.Collections.Generic;
using Meta.XR;
using Meta.XR.BuildingBlocks.AIBlocks;
using Meta.XR.EnvironmentDepth;
using UnityEngine;
using UnityEngine.Video;

namespace TemporalEcho
{
    /// <summary>
    /// Minimal binding layer: detection → EraDatabase rules → overlay spawn.
    /// Anchors overlays at resolved 3D world positions from detection bounding boxes + depth.
    /// </summary>
    public class EraDetectionBinder : MonoBehaviour
    {
        [Header("References")]
        public EraDatabase database;
        [Tooltip("Auto-found if null. When set, era switches from UI update overlays.")]
        public EraSwitcherUI eraUI;
        [Tooltip("Auto-found if null. Detection GO usually has ObjectDetectionAgent + DepthTextureAccess.")]
        public ObjectDetectionAgent detectionAgent;
        public Transform screenAnchor;
        public Transform bookAnchor;
        public Transform humanAnchor;
        public AudioSource audioSource;
        public EraVideoScreen videoScreen;

        [Header("Runtime State")]
        public EraId activeEra = EraId.E1920s;

        [Header("Display & Scale")]
        [Tooltip("Multiply overlay scale from EraDatabase. Use 5-10 if overlays are too small.")]
        public float overlayScaleMultiplier = 5f;
        [Tooltip("Scale for video quad when playing (2026 era). Use 2-5 if video is invisible.")]
        public float videoScreenScale = 3f;
        [Tooltip("Use detection bbox + depth to anchor overlays at detected objects. Off = place in front of camera.")]
        public bool anchorToDetectedObjects = true;
        [Tooltip("Position anchors in front of camera at Start (fallback when no depth/detection).")]
        public bool positionAnchorsInFrontOfCamera = true;
        public float anchorDistance = 1.5f;
        [Tooltip("Depth fallback (m) when depth sampling fails.")]
        public float fallbackDepth = 1.8f;
        [Tooltip("Human: add this world-space offset above head.")]
        public float humanHeadOffsetY = 0.08f;
        [Tooltip("Show all 3 overlays after delay without waiting for detection.")]
        public bool showAllOnStart = true;
        public float showAllDelaySeconds = 1.5f;

        [Header("Logging")]
        public bool logVerbose = true;

        [Header("Detection")]
        [Range(0.1f, 1f)]
        public float minConfidence = 0.30f;
        public string[] screenTokens = { "laptop", "screen", "monitor", "computer", "notebook", "tv", "television", "cell phone", "cellphone", "tablet", "remote" };
        public string[] bookTokens = { "book", "books", "newspaper", "magazine", "textbook" };
        public string[] humanTokens = { "person", "persons", "people", "human", "face", "man", "woman", "boy", "girl" };

        private bool _screenWasDetected;
        private bool _bookWasDetected;
        private bool _humanWasDetected;
        private bool _eraMediaStarted;
        private float _startTime;

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

        private void Start()
        {
            _startTime = Time.time;
            if (positionAnchorsInFrontOfCamera)
                PositionAnchorsInFrontOfCamera();
            if (anchorToDetectedObjects && logVerbose)
                Debug.Log("[EraDetectionBinder] Anchoring overlays to detected objects (depth + bbox). Anchors update each detection batch.");
        }

        private void Update()
        {
            if (showAllOnStart && Time.time - _startTime >= showAllDelaySeconds)
            {
                showAllOnStart = false;
                if (!_screenWasDetected || !_bookWasDetected || !_humanWasDetected)
                {
                    _screenWasDetected = true;
                    _bookWasDetected = true;
                    _humanWasDetected = true;
                    if (logVerbose) Debug.Log("[EraDetectionBinder] Show-all mode: spawning Screen, Book, Human.");
                    RefreshAllDetectedOverlays();
                    if (!_eraMediaStarted) { _eraMediaStarted = true; StartEraMedia(); }
                }
            }
        }

        private void PositionAnchorsInFrontOfCamera()
        {
            Transform cam = QuestAudioHelper.FindXRCameraTransform();
            if (cam == null && Camera.main != null) cam = Camera.main.transform;
            if (cam == null) return;
            Vector3 pos = cam.position + cam.forward * anchorDistance;
            Vector3 right = cam.right; if (right.sqrMagnitude < 0.01f) right = Vector3.right; else right.Normalize();
            if (screenAnchor != null) { screenAnchor.position = pos + right * -0.5f; screenAnchor.rotation = Quaternion.LookRotation(-cam.forward, Vector3.up); }
            if (bookAnchor != null) { bookAnchor.position = pos; bookAnchor.rotation = Quaternion.LookRotation(-cam.forward, Vector3.up); }
            if (humanAnchor != null) { humanAnchor.position = pos + right * 0.5f; humanAnchor.rotation = Quaternion.LookRotation(-cam.forward, Vector3.up); }
            if (logVerbose) Debug.Log("[EraDetectionBinder] Anchors positioned in front of camera.");
        }

        private void Awake()
        {
            if (database == null) database = FindAnyObjectByType<EraDatabase>();
            if (eraUI == null) eraUI = FindAnyObjectByType<EraSwitcherUI>();
            if (detectionAgent == null) detectionAgent = FindAnyObjectByType<ObjectDetectionAgent>();
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (audioSource == null && screenAnchor != null)
                audioSource = screenAnchor.GetComponentInParent<AudioSource>();
            _cam = FindAnyObjectByType<PassthroughCameraAccess>();
            _depth = detectionAgent != null ? detectionAgent.GetComponent<DepthTextureAccess>() : FindAnyObjectByType<DepthTextureAccess>();
            _eyeIdx = (_cam != null && _cam.CameraPosition == PassthroughCameraAccess.CameraPositionType.Left) ? 0 : 1;
        }

        private void OnEnable()
        {
            if (eraUI != null)
                eraUI.OnEraSelected += OnEraSelectedFromUI;
            if (detectionAgent == null) detectionAgent = FindAnyObjectByType<ObjectDetectionAgent>();
            if (detectionAgent != null)
                detectionAgent.OnBoxesUpdated += HandleBatch;
            else if (logVerbose)
                Debug.Log("[EraDetectionBinder] OnBoxesUpdated: NO ObjectDetectionAgent found — detections will not be received.");
            if (_depth != null)
                _depth.OnDepthTextureUpdateCPU += OnDepth;
        }

        private void OnDisable()
        {
            if (eraUI != null)
                eraUI.OnEraSelected -= OnEraSelectedFromUI;
            if (detectionAgent != null)
                detectionAgent.OnBoxesUpdated -= HandleBatch;
            if (_depth != null)
                _depth.OnDepthTextureUpdateCPU -= OnDepth;
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

        private void OnEraSelectedFromUI(TemporalEra era)
        {
            activeEra = ToEraId(era);
            if (logVerbose) Debug.Log($"[EraDetectionBinder] ERA SWITCH UI -> {activeEra}");
            RefreshAllDetectedOverlays();
            StartEraMedia();
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

        private void RefreshAllDetectedOverlays()
        {
            if (_screenWasDetected) ApplySubject(DetectedSubject.Screen);
            if (_bookWasDetected) ApplySubject(DetectedSubject.Book);
            if (_humanWasDetected) ApplySubject(DetectedSubject.Human);
        }

        private void HandleBatch(List<BoxData> batch)
        {
            if (batch == null) return;

            if (logVerbose && batch.Count > 0)
            {
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < Mathf.Min(3, batch.Count); i++)
                    sb.Append(batch[i].label).Append("; ");
                Debug.Log($"[EraDetectionBinder] DETECTION UPDATE count={batch.Count} first3=[{sb}]");
            }

            if (batch.Count == 0) return;

            float screenBest = 0f, bookBest = 0f, humanBest = 0f;
            BoxData? screenBox = null, bookBox = null, humanBox = null;

            foreach (var b in batch)
            {
                var (baseLabel, conf) = ExtractLabelAndConfidence(b.label);
                if (conf < minConfidence) continue;

                var subject = Classify(baseLabel);
                if (subject == DetectedSubject.Screen && conf > screenBest)
                {
                    screenBest = conf;
                    screenBox = b;
                }
                else if (subject == DetectedSubject.Book && conf > bookBest)
                {
                    bookBest = conf;
                    bookBox = b;
                }
                else if (subject == DetectedSubject.Human && conf > humanBest)
                {
                    humanBest = conf;
                    humanBox = b;
                }
            }

            if (anchorToDetectedObjects && _cam != null)
            {
                if (screenBest >= minConfidence && screenBox.HasValue && screenAnchor != null &&
                    TryResolveWorldPose(screenBox.Value, DetectedSubject.Screen, out var screenPos, out var screenRot))
                {
                    screenAnchor.position = screenPos;
                    screenAnchor.rotation = screenRot;
                }
                if (bookBest >= minConfidence && bookBox.HasValue && bookAnchor != null &&
                    TryResolveWorldPose(bookBox.Value, DetectedSubject.Book, out var bookPos, out var bookRot))
                {
                    bookAnchor.position = bookPos;
                    bookAnchor.rotation = bookRot;
                }
                if (humanBest >= minConfidence && humanBox.HasValue && humanAnchor != null &&
                    TryResolveWorldPose(humanBox.Value, DetectedSubject.Human, out var humanPos, out var humanRot))
                {
                    humanAnchor.position = humanPos;
                    humanAnchor.rotation = humanRot;
                }
            }

            if (screenBest >= minConfidence && !_screenWasDetected)
            {
                _screenWasDetected = true;
                if (logVerbose) Debug.Log($"[EraDetectionBinder] ACCEPT Screen label={screenBox?.label ?? ""} conf={screenBest:F2}");
                ApplySubject(DetectedSubject.Screen);
            }
            if (bookBest >= minConfidence && !_bookWasDetected)
            {
                _bookWasDetected = true;
                if (logVerbose) Debug.Log($"[EraDetectionBinder] ACCEPT Book label={bookBox?.label ?? ""} conf={bookBest:F2}");
                ApplySubject(DetectedSubject.Book);
            }
            if (humanBest >= minConfidence && !_humanWasDetected)
            {
                _humanWasDetected = true;
                if (logVerbose) Debug.Log($"[EraDetectionBinder] ACCEPT Human label={humanBox?.label ?? ""} conf={humanBest:F2}");
                ApplySubject(DetectedSubject.Human);
            }
        }

        private bool TryResolveWorldPose(BoxData b, DetectedSubject subject, out Vector3 worldPos, out Quaternion worldRot)
        {
            worldPos = default;
            worldRot = Quaternion.identity;
            if (_cam == null) return false;
            var cameraTexture = _cam.GetTexture();
            if (cameraTexture == null) return false;

            float xmin = b.position.x, ymin = b.position.y, xmax = b.scale.x, ymax = b.scale.y;
            float px, py;
            if (subject == DetectedSubject.Human)
            {
                px = (xmin + xmax) * 0.5f;
                py = ymin - 0.02f * cameraTexture.height;
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

            float d = fallbackDepth;
            if (_frame.ViewProjectionMatrix != null && _frame.ViewProjectionMatrix.Length > _eyeIdx && _frame.Depth != null)
            {
                var world1M = ray.origin + ray.direction;
                var clip = _frame.ViewProjectionMatrix[_eyeIdx] * new Vector4(world1M.x, world1M.y, world1M.z, 1f);
                if (clip.w > 0)
                {
                    var uv = (new Vector2(clip.x, clip.y) / clip.w) * 0.5f + Vector2.one * 0.5f;
                    var texSize = DepthTextureAccess.TextureSize;
                    var sx = Mathf.Clamp((int)(uv.x * texSize), 0, texSize - 1);
                    var sy = Mathf.Clamp((int)(uv.y * texSize), 0, texSize - 1);
                    var idx = _eyeIdx * texSize * texSize + sy * texSize + sx;
                    if (idx < _frame.Depth.Length)
                    {
                        var sampled = _frame.Depth[idx];
                        if (sampled > 0 && sampled < 20 && !float.IsInfinity(sampled))
                            d = sampled;
                    }
                }
            }
            worldPos = ray.origin + ray.direction * d;
            if (subject == DetectedSubject.Human)
                worldPos += Vector3.up * humanHeadOffsetY;
            var camPos = _frame.Pose.position;
            float yawDeg = Quaternion.LookRotation(worldPos - camPos).eulerAngles.y;
            worldRot = Quaternion.Euler(0, yawDeg, 0);
            return true;
        }

        private void ApplySubject(DetectedSubject subject)
        {
            if (database == null)
            {
                Debug.LogError("[EraDetectionBinder] MISSING RULE/PREFAB era=" + activeEra + " subject=" + subject + " (database is null)");
                return;
            }

            var def = database.Get(activeEra);
            if (def.rules == null)
            {
                Debug.LogError("[EraDetectionBinder] MISSING RULE/PREFAB era=" + activeEra + " subject=" + subject + " (rules is null)");
                return;
            }

            EraRule? rule = null;
            foreach (var r in def.rules)
                if (r.subject == subject) { rule = r; break; }

            if (!rule.HasValue || rule.Value.prefab == null)
            {
                Debug.LogError("[EraDetectionBinder] MISSING RULE/PREFAB era=" + activeEra + " subject=" + subject + " (no prefab in EraDatabase)");
                return;
            }

            var anchor = GetAnchor(subject);
            if (anchor == null)
            {
                Debug.LogError("[EraDetectionBinder] MISSING ANCHOR subject=" + subject);
                return;
            }

            string overlayName = "Overlay_" + subject;
            ClearAllOverlaysForSubject(anchor, subject);

            var instance = Instantiate(rule.Value.prefab, anchor);
            instance.name = overlayName;
            instance.transform.localPosition = rule.Value.localPosition;
            instance.transform.localRotation = Quaternion.Euler(rule.Value.localEulerRotation);
            Vector3 scale = rule.Value.localScale == Vector3.zero ? Vector3.one : rule.Value.localScale;
            instance.transform.localScale = scale * overlayScaleMultiplier;

            if (logVerbose)
                Debug.Log($"[EraDetectionBinder] SPAWN era={activeEra} subject={subject} prefab={rule.Value.prefab.name} anchor={anchor.name}");

            if (!_eraMediaStarted)
            {
                _eraMediaStarted = true;
                StartEraMedia();
            }
        }

        private void StartEraMedia()
        {
            if (database == null) return;
            var def = database.Get(activeEra);

            if (def.videoClip != null)
            {
                if (audioSource != null) audioSource.Stop();
                if (videoScreen != null)
                {
                    videoScreen.gameObject.SetActive(true);
                    videoScreen.transform.localScale = Vector3.one * videoScreenScale;
                    videoScreen.transform.localPosition = Vector3.zero;
                    videoScreen.transform.localRotation = Quaternion.identity;
                    var renderer = videoScreen.GetComponent<Renderer>();
                    if (renderer != null) renderer.enabled = true;
                    videoScreen.Play(def.videoClip, loop: false);
                }
                SetScreenOverlayVisible(DetectedSubject.Screen, false);
                if (logVerbose) Debug.Log("[EraDetectionBinder] MEDIA video=" + def.videoClip.name);
            }
            else
            {
                if (videoScreen != null)
                {
                    videoScreen.Stop();
                    videoScreen.gameObject.SetActive(false);
                }
                SetScreenOverlayVisible(DetectedSubject.Screen, true);
                if (def.soundtrack != null && audioSource != null)
                {
                    audioSource.clip = def.soundtrack;
                    audioSource.loop = false;
                    audioSource.Play();
                }
                if (logVerbose) Debug.Log("[EraDetectionBinder] MEDIA audio=" + (def.soundtrack != null ? def.soundtrack.name : "null"));
            }
        }

        private void ClearAllOverlaysForSubject(Transform anchor, DetectedSubject subject)
        {
            if (anchor == null) return;
            string prefix1 = "Overlay_" + subject;
            string prefix2 = "EraOverlay_" + subject;
            for (int i = anchor.childCount - 1; i >= 0; i--)
            {
                var child = anchor.GetChild(i);
                if (child.GetComponent<EraVideoScreen>() != null) continue;
                string n = child.name;
                bool isOverlay = n == prefix1 || n == prefix2 || n.StartsWith(prefix1) || n.StartsWith(prefix2);
                if (isOverlay)
                {
                    if (logVerbose) Debug.Log($"[EraDetectionBinder] CLEAR subject={subject} removing {n}");
                    Destroy(child.gameObject);
                }
            }
        }

        private void SetScreenOverlayVisible(DetectedSubject subject, bool visible)
        {
            var anchor = GetAnchor(subject);
            if (anchor == null) return;
            string prefix = "Overlay_" + subject;
            for (int i = 0; i < anchor.childCount; i++)
            {
                var c = anchor.GetChild(i);
                if (c.name.StartsWith(prefix) || c.name.StartsWith("EraOverlay_" + subject))
                {
                    if (c.GetComponent<EraVideoScreen>() == null)
                        c.gameObject.SetActive(visible);
                    break;
                }
            }
        }

        private Transform GetAnchor(DetectedSubject subject)
        {
            return subject switch
            {
                DetectedSubject.Screen => screenAnchor,
                DetectedSubject.Book => bookAnchor,
                DetectedSubject.Human => humanAnchor,
                _ => null
            };
        }

        private DetectedSubject? Classify(string label)
        {
            if (string.IsNullOrWhiteSpace(label)) return null;
            string lower = label.Trim().ToLowerInvariant();
            foreach (var t in screenTokens)
                if (!string.IsNullOrEmpty(t) && lower.Contains(t.ToLowerInvariant())) return DetectedSubject.Screen;
            foreach (var t in bookTokens)
                if (!string.IsNullOrEmpty(t) && lower.Contains(t.ToLowerInvariant())) return DetectedSubject.Book;
            foreach (var t in humanTokens)
                if (!string.IsNullOrEmpty(t) && lower.Contains(t.ToLowerInvariant())) return DetectedSubject.Human;
            return null;
        }

        private static (string baseLabel, float confidence) ExtractLabelAndConfidence(string label)
        {
            if (string.IsNullOrEmpty(label)) return ("", 0f);
            int lastSpace = label.LastIndexOf(' ');
            if (lastSpace < 0) return (label.Trim(), 1f);
            string baseLabel = label.Substring(0, lastSpace).Trim();
            if (!float.TryParse(label.Substring(lastSpace + 1).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float conf))
                return (label.Trim(), 1f);
            return (baseLabel, conf);
        }
    }
}
