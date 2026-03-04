#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using TemporalEcho;

public static class TemporalEchoPrepareShowcase
{
    private static readonly System.Type OVRInputModuleType = System.Type.GetType("UnityEngine.EventSystems.OVRInputModule, Oculus.VR") ?? System.Type.GetType("UnityEngine.EventSystems.OVRInputModule, Meta.XR.SDK.Core");
    private static readonly System.Type XRUIInputModuleType = System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule, Unity.XR.Interaction.Toolkit");

    [MenuItem("Tools/Temporal Echo/Prepare Scene for Showcase")]
    public static void PrepareSceneForShowcase()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid()) { Debug.LogError("[TemporalEcho] No active scene."); return; }

        var log = new List<string>();
        int removedMissing = 0;

        // 1) Remove missing script components
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                int n = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                if (n > 0)
                {
                    Undo.RegisterCompleteObjectUndo(t.gameObject, "Remove missing scripts");
                    removedMissing += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                }
            }
        }
        if (removedMissing > 0) log.Add($"Removed {removedMissing} missing script(s).");

        // 2) Disable mic-related GameObjects
        int micDisabled = 0;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var name = t.gameObject.name;
                if (name.IndexOf("Mic", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("Microphone", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("VirtualMic", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("Mic3D", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (t.gameObject.activeSelf)
                    {
                        Undo.RecordObject(t.gameObject, "Disable mic");
                        t.gameObject.SetActive(false);
                        micDisabled++;
                    }
                }
            }
        }
        if (micDisabled > 0) log.Add($"Disabled {micDisabled} mic-related GameObject(s).");

        // 3) EventSystem: keep one, remove Standalone/XRUI, ensure OVR only
        var eventSystems = Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None);
        if (eventSystems.Length > 1)
        {
            for (int i = 1; i < eventSystems.Length; i++)
            {
                Undo.DestroyObjectImmediate(eventSystems[i].gameObject);
                log.Add("Removed duplicate EventSystem.");
            }
        }
        var es = Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
        if (es != null)
        {
            if (StandaloneInputModuleType() != null)
            {
                var standalone = es.GetComponent(StandaloneInputModuleType());
                if (standalone != null) { Undo.DestroyObjectImmediate(standalone); log.Add("Removed StandaloneInputModule."); }
            }
            if (XRUIInputModuleType != null)
            {
                var xri = es.GetComponent(XRUIInputModuleType);
                if (xri != null) { Undo.DestroyObjectImmediate(xri); log.Add("Removed XRUIInputModule."); }
            }
            if (OVRInputModuleType != null && es.GetComponent(OVRInputModuleType) == null)
                es.gameObject.AddComponent(OVRInputModuleType);
        }

        // 4) Canvas: OVRRaycaster, worldCamera
        var canvasGo = GameObject.Find("EraSwitcherCanvas");
        if (canvasGo != null)
        {
            var canvas = canvasGo.GetComponent<Canvas>();
            if (canvas != null)
            {
                var centerEye = FindCenterEye();
                if (centerEye != null)
                {
                    var cam = centerEye.GetComponent<Camera>() ?? centerEye.GetComponentInChildren<Camera>();
                    if (cam != null) { Undo.RecordObject(canvas, "worldCamera"); canvas.worldCamera = cam; }
                }
            }
            var ovrRaycasterType = System.Type.GetType("OVRRaycaster, Oculus.VR") ?? System.Type.GetType("OVRRaycaster, Meta.XR.SDK.Core");
            if (ovrRaycasterType != null && canvasGo.GetComponent(ovrRaycasterType) == null)
                canvasGo.AddComponent(ovrRaycasterType);
        }

        // 5) Video: exactly ONE canonical VideoPlayer (VideoScreen under ScreenAnchor)
        Transform screenAnchor = GameObject.Find("ScreenAnchor")?.transform;
        EraVideoScreen canonicalVideo = null;
        UnityEngine.Video.VideoPlayer canonicalVP = null;
        if (screenAnchor != null)
        {
            canonicalVideo = screenAnchor.GetComponentInChildren<EraVideoScreen>(true);
            if (canonicalVideo != null) canonicalVP = canonicalVideo.GetComponent<UnityEngine.Video.VideoPlayer>();
        }

        var allVps = Object.FindObjectsByType<UnityEngine.Video.VideoPlayer>(FindObjectsSortMode.None);
        int videoDisabled = 0;
        foreach (var vp in allVps)
        {
            if (vp == null) continue;
            bool isCanonical = (vp == canonicalVP);
            if (!isCanonical)
            {
                Undo.RecordObject(vp, "Disable video");
                vp.enabled = false;
                videoDisabled++;
            }
        }
        if (videoDisabled > 0) log.Add($"Disabled {videoDisabled} non-canonical VideoPlayer(s).");

        // 6) Audio: one canonical AudioSource (Temporal Echo Runtime)
        var runtimeGo = GameObject.Find("Temporal Echo Runtime");
        AudioSource canonicalAudio = runtimeGo != null ? runtimeGo.GetComponent<AudioSource>() : null;
        var allAudios = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        int audioDisabled = 0;
        foreach (var a in allAudios)
        {
            if (a == null) continue;
            if (a != canonicalAudio && a.gameObject.activeInHierarchy)
            {
                // Disable only if it might be used for era soundtrack (e.g. on same kind of manager objects)
                var goName = a.gameObject.name;
                if (goName.IndexOf("Era", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || goName.IndexOf("Temporal", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || goName.IndexOf("Media", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (a != canonicalAudio) { Undo.RecordObject(a, "Disable audio"); a.enabled = false; audioDisabled++; }
                }
            }
        }
        if (audioDisabled > 0) log.Add($"Disabled {audioDisabled} duplicate era AudioSource(s).");

        // 7) Disable legacy/duplicate components (by type name; avoid breaking refs)
        DisableDuplicatePlacers(log);
        DisableDuplicateEraManagers(log);

        EditorSceneManager.MarkSceneDirty(scene);

        // Checklist log
        Debug.Log("[TemporalEcho] === Showcase Checklist ===");
        foreach (var msg in log)
            Debug.Log("[TemporalEcho] " + msg);
        Debug.Log(es != null ? "[TemporalEcho] [OK] One EventSystem (OVR only)" : "[TemporalEcho] [--] EventSystem");
        Debug.Log(canvasGo != null ? "[TemporalEcho] [OK] EraSwitcherCanvas" : "[TemporalEcho] [--] EraSwitcherCanvas");
        Debug.Log(canonicalVideo != null ? "[TemporalEcho] [OK] One canonical VideoScreen" : "[TemporalEcho] [--] VideoScreen");
        Debug.Log(canonicalAudio != null ? "[TemporalEcho] [OK] Canonical AudioSource" : "[TemporalEcho] [--] AudioSource");
        Debug.Log("[TemporalEcho] Mic objects disabled. Set PersistentDiscoveryManager.DisableVirtualMic = true at runtime.");
    }

    private static System.Type StandaloneInputModuleType() => typeof(UnityEngine.EventSystems.StandaloneInputModule);

    private static Transform FindCenterEye()
    {
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (t.name == "CenterEyeAnchor")
            {
                if (t.GetComponent<Camera>() != null) return t;
                var c = t.GetComponentInChildren<Camera>();
                if (c != null) return c.transform;
                return t;
            }
        }
        var main = Camera.main;
        return main != null ? main.transform : null;
    }

    private static void DisableDuplicatePlacers(List<string> log)
    {
        var placers = Object.FindObjectsByType<WorldSpacePanelPlacer>(FindObjectsSortMode.None);
        if (placers.Length <= 1) return;
        var canvas = GameObject.Find("EraSwitcherCanvas");
        for (int i = 0; i < placers.Length; i++)
        {
            if (canvas != null && placers[i].gameObject == canvas) continue;
            Undo.RecordObject(placers[i], "Disable placer");
            placers[i].enabled = false;
            log.Add("Disabled duplicate WorldSpacePanelPlacer.");
        }
    }

    private static void DisableDuplicateEraManagers(List<string> log)
    {
        var eraManagers = Object.FindObjectsByType<TemporalEraManager>(FindObjectsSortMode.None);
        if (eraManagers.Length <= 1) return;
        for (int i = 1; i < eraManagers.Length; i++)
        {
            Undo.RecordObject(eraManagers[i], "Disable");
            eraManagers[i].enabled = false;
            log.Add("Disabled duplicate TemporalEraManager.");
        }
    }
}
#endif
