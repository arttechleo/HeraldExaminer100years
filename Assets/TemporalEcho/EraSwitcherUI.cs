using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TemporalEcho
{
    /// <summary>
    /// World-space MR canvas with 1920s / 1960s / 2026 buttons. Invokes OnEraSelected for era switching.
    /// Optionally follows XR camera; assign xrCamera or leave null to auto-find.
    /// </summary>
    public class EraSwitcherUI : MonoBehaviour
    {
        [Header("Follow")]
        [Tooltip("If true, canvas stays ~1.2m in front of XR camera. Set to false to keep canvas fixed (recommended for XR UI).")]
        [SerializeField] private bool enableFollow = false;
        [Tooltip("XR camera transform. If null, found at runtime via QuestAudioHelper / main camera.")]
        [SerializeField] private Transform xrCamera;

        [Header("Layout (optional)")]
        [Tooltip("Title text above buttons. If null, no title.")]
        [SerializeField] private GameObject titleRoot;

        [Header("Button names (for auto-wire and highlight)")]
        [SerializeField] private string button1920sName = "Btn_1920s";
        [SerializeField] private string button1960sName = "Btn_1960s";
        [SerializeField] private string button2026Name = "Btn_2026";

        [Header("Debug (optional)")]
        [Tooltip("If set, shows last clicked era and optionally last detected subject.")]
        [SerializeField] private TMPro.TMP_Text debugText;

        [Header("Highlight")]
        [Tooltip("Scale of selected button (e.g. 1.05 = slightly larger).")]
        [SerializeField] private float selectedScale = 1.05f;
        [Tooltip("Alpha of selected button label (0–1).")]
        [SerializeField] private float selectedAlpha = 1f;
        [SerializeField] private float unselectedAlpha = 0.75f;

        /// <summary>Fired when user picks an era. Hook this to TemporalEraManager.SetEra or your era logic.</summary>
        public event Action<TemporalEra> OnEraSelected;

        public const float FollowDistance = 1.2f;
        public const float FollowHeightOffset = -0.12f;
        public const float FollowSmoothTime = 0.15f;

        private Button _btn1920s;
        private Button _btn1960s;
        private Button _btn2026;
        private TemporalEra _lastSelected = TemporalEra.Era1920s;
        private string _lastDetectedLabel = "—";
        private Vector3 _followVel;

        private void Awake()
        {
            if (xrCamera == null)
                xrCamera = QuestAudioHelper.FindXRCameraTransform();
            if (xrCamera == null)
            {
                var cam = Camera.main;
                if (cam != null) xrCamera = cam.transform;
            }
            AssignCanvasWorldCamera();
        }

        private void Start()
        {
            EnsureEventSystem();
            WireButtons();
            ApplyHighlight(_lastSelected);
        }

        // Panel placement is handled ONLY by WorldSpacePanelPlacer. Do not move panel here.
        private void Update()
        {
            // enableFollow intentionally false by default; placement is WorldSpacePanelPlacer only.
        }

        private static void EnsureEventSystem()
        {
            var existing = UnityEngine.EventSystems.EventSystem.current;
            if (existing != null)
            {
                EnsureMetaInputModule(existing.gameObject);
                return;
            }
            var go = new GameObject("EventSystem");
            go.AddComponent<UnityEngine.EventSystems.EventSystem>();
            EnsureMetaInputModule(go);
        }

        private static void EnsureMetaInputModule(GameObject eventSystemGo)
        {
            if (eventSystemGo == null) return;
            var standalone = eventSystemGo.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            if (standalone != null)
                UnityEngine.Object.Destroy(standalone);
            var xriType = Type.GetType("UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule, Unity.XR.Interaction.Toolkit");
            if (xriType != null)
            {
                var xriModule = eventSystemGo.GetComponent(xriType);
                if (xriModule != null)
                    UnityEngine.Object.Destroy(xriModule);
            }
            if (eventSystemGo.GetComponent<UnityEngine.EventSystems.BaseInputModule>() != null)
                return;
            if (!AddOVRInputModule(eventSystemGo))
                eventSystemGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        private void AssignCanvasWorldCamera()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas == null || canvas.renderMode != RenderMode.WorldSpace) return;
            var cam = xrCamera != null ? xrCamera.GetComponent<Camera>() : null;
            if (cam == null) cam = Camera.main;
            if (cam != null) canvas.worldCamera = cam;
        }

        private static bool AddOVRInputModule(GameObject go)
        {
            var t = Type.GetType("UnityEngine.EventSystems.OVRInputModule, Oculus.VR") ?? Type.GetType("UnityEngine.EventSystems.OVRInputModule, Meta.XR.SDK.Core");
            if (t == null) return false;
            go.AddComponent(t);
            return true;
        }

        private void WireButtons()
        {
            _btn1920s = FindButton(button1920sName);
            _btn1960s = FindButton(button1960sName);
            _btn2026 = FindButton(button2026Name);

            if (_btn1920s != null) _btn1920s.onClick.AddListener(On1920sClicked);
            if (_btn1960s != null) _btn1960s.onClick.AddListener(On1960sClicked);
            if (_btn2026 != null) _btn2026.onClick.AddListener(On2026Clicked);
        }

        private Button FindButton(string name)
        {
            var t = transform.Find(name);
            if (t == null) t = transform.Find($"Panel/{name}");
            if (t == null) return null;
            return t.GetComponent<Button>();
        }

        public void On1920sClicked()
        {
            Debug.Log("[EraSwitcherUI] Selected era: 1920s");
            SelectEra(TemporalEra.Era1920s);
        }

        public void On1960sClicked()
        {
            Debug.Log("[EraSwitcherUI] Selected era: 1960s");
            SelectEra(TemporalEra.Era1960s);
        }

        public void On2026Clicked()
        {
            Debug.Log("[EraSwitcherUI] Selected era: 2026");
            SelectEra(TemporalEra.EraToday);
        }

        private void SelectEra(TemporalEra era)
        {
            _lastSelected = era;
            ApplyHighlight(era);
            if (debugText != null)
                debugText.text = "Era: " + (era == TemporalEra.Era1920s ? "1920s" : era == TemporalEra.Era1960s ? "1960s" : "2026");
            OnEraSelected?.Invoke(era);
        }

        /// <summary>Optional: set debug text to show last detected subject (e.g. from orchestrator).</summary>
        public void SetDebugDetectedSubject(string subjectLabel)
        {
            _lastDetectedLabel = string.IsNullOrEmpty(subjectLabel) ? "—" : subjectLabel;
        }

        /// <summary>Update debug text with era, last detected, and media state.</summary>
        public void SetDebugMediaState(EraMediaController.MediaState state)
        {
            if (debugText == null) return;
            string eraStr = _lastSelected == TemporalEra.Era1920s ? "1920s" : _lastSelected == TemporalEra.Era1960s ? "1960s" : "2026";
            string mediaStr = state == EraMediaController.MediaState.AudioPlaying ? "Audio playing" : state == EraMediaController.MediaState.VideoPlaying ? "Video playing" : "Idle";
            debugText.text = "Era: " + eraStr + "\nDetected: " + _lastDetectedLabel + "\nMedia: " + mediaStr;
        }

        private void ApplyHighlight(TemporalEra era)
        {
            ApplyButtonHighlight(_btn1920s, era == TemporalEra.Era1920s);
            ApplyButtonHighlight(_btn1960s, era == TemporalEra.Era1960s);
            ApplyButtonHighlight(_btn2026, era == TemporalEra.EraToday);
        }

        private void ApplyButtonHighlight(Button btn, bool selected)
        {
            if (btn == null) return;
            float scale = selected ? selectedScale : 1f;
            btn.transform.localScale = Vector3.one * scale;
            float alpha = selected ? selectedAlpha : unselectedAlpha;
            SetGraphicAlpha(btn.targetGraphic, alpha);
            var text = btn.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
                text.alpha = alpha;
        }

        private static void SetGraphicAlpha(Graphic g, float alpha)
        {
            if (g == null) return;
            var c = g.color;
            c.a = alpha;
            g.color = c;
        }

        /// <summary>Call to set selected era from code (e.g. sync with TemporalEraManager).</summary>
        public void SetSelectedEra(TemporalEra era)
        {
            _lastSelected = era;
            ApplyHighlight(era);
        }
    }
}
