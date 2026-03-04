#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using TemporalEcho;

public static class TemporalEchoRebuildCanonical
{
    private const string EraDatabasePath = "Assets/TemporalEcho/EraDatabase.asset";
    private const string EraSwitcherCanvasPrefabPath = "Assets/TemporalEcho/Prefabs/EraSwitcherCanvas.prefab";
    private const string VideoScreenPrefabPath = "Assets/TemporalEcho/Prefabs/VideoScreen.prefab";
    private const string SystemGoName = "TemporalEchoSystem";
    private const string AnchorNameScreen = "ScreenAnchor";
    private const string AnchorNameBook = "BookAnchor";
    private const string AnchorNameHuman = "HumanAnchor";

    private static readonly System.Type OVRInputModuleType = System.Type.GetType("UnityEngine.EventSystems.OVRInputModule, Oculus.VR") ?? System.Type.GetType("UnityEngine.EventSystems.OVRInputModule, Meta.XR.SDK.Core");
    private static readonly System.Type OVRRaycasterType = System.Type.GetType("OVRRaycaster, Oculus.VR") ?? System.Type.GetType("OVRRaycaster, Meta.XR.SDK.Core");
    private static readonly System.Type XRUIInputModuleType = System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule, Unity.XR.Interaction.Toolkit");
    private static readonly System.Type ObjectDetectionAgentType = System.Type.GetType("Meta.XR.BuildingBlocks.AIBlocks.ObjectDetectionAgent, Meta.XR.BuildingBlocks.AIBlocks");

    [MenuItem("Tools/Temporal Echo/Rebuild Scene to Canonical System")]
    public static void RebuildSceneToCanonical()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid()) { Debug.LogError("[TemporalEcho] No active scene."); return; }

        var log = new List<string>();

        int removedMissing = 0;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                int n = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                if (n > 0)
                {
                    Undo.RegisterCompleteObjectUndo(t.gameObject, "Remove missing");
                    removedMissing += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                }
            }
        }
        if (removedMissing > 0) log.Add("Removed " + removedMissing + " missing script(s).");

        DisableConflicts(log);

        Transform screenAnchor = FindOrCreateAnchor(AnchorNameScreen, new Vector3(0f, 0f, 1.5f));
        Transform bookAnchor = FindOrCreateAnchor(AnchorNameBook, new Vector3(0.5f, 0f, 1.5f));
        Transform humanAnchor = FindOrCreateAnchor(AnchorNameHuman, new Vector3(1f, 0f, 1.5f));

        GameObject canvasGo = FindOrCreateEraSwitcherCanvas();
        EnsureOneVideoScreenUnder(screenAnchor, log);

        Transform centerEye = FindCenterEye();
        EnsureEventSystem(log);
        EnsureCanvasMetaXR(canvasGo, centerEye);

        GameObject systemGo = FindOrCreateTemporalEchoSystem();
        WireTemporalEchoSystem(systemGo, screenAnchor, bookAnchor, humanAnchor, canvasGo, centerEye);

        var placer = canvasGo != null ? canvasGo.GetComponent<WorldSpacePanelPlacer>() : null;
        if (placer != null)
        {
            placer.centerEye = centerEye;
            placer.distance = 0.55f;
            placer.heightOffset = 0f;
            placer.CalibrateNow();
        }

        SaveTemporalEchoSystemPrefab(systemGo);

        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = systemGo;

        LogCanonicalReady(log, systemGo, canvasGo, screenAnchor, removedMissing);
    }

    private static void SaveTemporalEchoSystemPrefab(GameObject systemGo)
    {
        if (systemGo == null) return;
        string prefabPath = "Assets/TemporalEcho/Prefabs/TemporalEchoSystem.prefab";
        if (!AssetDatabase.IsValidFolder("Assets/TemporalEcho")) return;
        if (!AssetDatabase.IsValidFolder("Assets/TemporalEcho/Prefabs"))
            AssetDatabase.CreateFolder("Assets/TemporalEcho", "Prefabs");
        PrefabUtility.SaveAsPrefabAsset(systemGo, prefabPath);
    }

    private static void DisableConflicts(List<string> log)
    {
        var storyMgrs = Object.FindObjectsByType<StorySelectionManager>(FindObjectsSortMode.None);
        for (int i = 0; i < storyMgrs.Length; i++)
        {
            storyMgrs[i].enabled = false;
            log.Add("Disabled StorySelectionManager on " + storyMgrs[i].gameObject.name);
        }

        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var name = t.gameObject.name;
                if (name.IndexOf("Mic", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("Microphone", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (t.gameObject.activeSelf) { t.gameObject.SetActive(false); log.Add("Disabled " + name); }
                }
            }
        }

        var eventSystems = Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None);
        for (int i = 1; i < eventSystems.Length; i++)
        {
            eventSystems[i].gameObject.SetActive(false);
            log.Add("Disabled duplicate EventSystem.");
        }

        var videoScreens = Object.FindObjectsByType<EraVideoScreen>(FindObjectsSortMode.None);
        var screenAnchor = GameObject.Find(AnchorNameScreen)?.transform;
        EraVideoScreen canonical = screenAnchor != null ? screenAnchor.GetComponentInChildren<EraVideoScreen>(true) : null;
        for (int i = 0; i < videoScreens.Length; i++)
        {
            if (videoScreens[i] != canonical)
            {
                videoScreens[i].gameObject.SetActive(false);
                log.Add("Disabled duplicate VideoScreen.");
            }
        }

        var allVps = Object.FindObjectsByType<VideoPlayer>(FindObjectsSortMode.None);
        VideoPlayer canonicalVP = canonical != null ? canonical.GetComponent<VideoPlayer>() : null;
        for (int i = 0; i < allVps.Length; i++)
        {
            if (allVps[i] != canonicalVP) { allVps[i].enabled = false; }
        }
    }

    private static void EnsureEventSystem(List<string> log)
    {
        var es = Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
        if (es == null)
        {
            var go = new GameObject("EventSystem");
            Undo.RegisterCreatedObjectUndo(go, "EventSystem");
            go.AddComponent<UnityEngine.EventSystems.EventSystem>();
            if (OVRInputModuleType != null) go.AddComponent(OVRInputModuleType);
            return;
        }
        es.gameObject.SetActive(true);
        var standalone = es.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        if (standalone != null) { Object.DestroyImmediate(standalone); log.Add("Removed StandaloneInputModule"); }
        if (XRUIInputModuleType != null)
        {
            var xri = es.GetComponent(XRUIInputModuleType);
            if (xri != null) { Object.DestroyImmediate(xri); log.Add("Removed XRUIInputModule"); }
        }
        if (OVRInputModuleType != null && es.GetComponent(OVRInputModuleType) == null)
            es.gameObject.AddComponent(OVRInputModuleType);
    }

    private static void EnsureCanvasMetaXR(GameObject canvasGo, Transform centerEye)
    {
        if (canvasGo == null) return;
        var canvas = canvasGo.GetComponent<Canvas>();
        if (canvas != null && centerEye != null)
        {
            var cam = centerEye.GetComponent<Camera>() ?? centerEye.GetComponentInChildren<Camera>();
            if (cam != null) canvas.worldCamera = cam;
        }
        if (OVRRaycasterType != null && canvasGo.GetComponent(OVRRaycasterType) == null)
            canvasGo.AddComponent(OVRRaycasterType);
    }

    private static void EnsureOneVideoScreenUnder(Transform screenAnchor, List<string> log)
    {
        if (screenAnchor == null) return;
        var existing = screenAnchor.Find("VideoScreen");
        if (existing != null) return;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VideoScreenPrefabPath);
        if (prefab == null) { log.Add("VideoScreen prefab not found"); return; }
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, screenAnchor);
        instance.name = "VideoScreen";
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
        Undo.RegisterCreatedObjectUndo(instance, "VideoScreen");
    }

    private static GameObject FindOrCreateTemporalEchoSystem()
    {
        var existing = GameObject.Find(SystemGoName);
        if (existing != null) return existing;

        var go = new GameObject(SystemGoName);
        Undo.RegisterCreatedObjectUndo(go, SystemGoName);

        if (go.GetComponent<ShowcaseSanitizer>() == null) go.AddComponent<ShowcaseSanitizer>();
        if (go.GetComponent<TemporalEchoRuntimeOrchestrator>() == null) go.AddComponent<TemporalEchoRuntimeOrchestrator>();
        if (go.GetComponent<EraMediaController>() == null) go.AddComponent<EraMediaController>();
        if (go.GetComponent<AudioSource>() == null) go.AddComponent<AudioSource>();

        var bridgeType = typeof(TemporalEchoDetectionBridge);
        if (go.GetComponent(bridgeType) == null)
            go.AddComponent(bridgeType);

        return go;
    }

    private static void WireTemporalEchoSystem(GameObject systemGo, Transform screenAnchor, Transform bookAnchor, Transform humanAnchor, GameObject canvasGo, Transform centerEye)
    {
        var db = AssetDatabase.LoadAssetAtPath<EraDatabase>(EraDatabasePath);
        var eraUI = canvasGo != null ? canvasGo.GetComponent<EraSwitcherUI>() : null;
        var videoScreen = screenAnchor != null ? screenAnchor.GetComponentInChildren<EraVideoScreen>(true) : null;
        var audioSource = systemGo.GetComponent<AudioSource>();

        UnityEngine.Component agent = null;
        if (ObjectDetectionAgentType != null)
            agent = Object.FindAnyObjectByType(ObjectDetectionAgentType) as UnityEngine.Component;

        var orch = systemGo.GetComponent<TemporalEchoRuntimeOrchestrator>();
        var med = systemGo.GetComponent<EraMediaController>();
        var bridge = systemGo.GetComponent<TemporalEchoDetectionBridge>();
        var eraMgr = Object.FindAnyObjectByType<TemporalEraManager>();

        Undo.RecordObject(orch, "Wire");
        var soOrch = new SerializedObject(orch);
        soOrch.FindProperty("database").objectReferenceValue = db;
        soOrch.FindProperty("eraSync").objectReferenceValue = eraMgr;
        soOrch.FindProperty("eraUI").objectReferenceValue = eraUI;
        soOrch.FindProperty("mediaController").objectReferenceValue = med;
        soOrch.FindProperty("screenAnchor").objectReferenceValue = screenAnchor;
        soOrch.FindProperty("bookAnchor").objectReferenceValue = bookAnchor;
        soOrch.FindProperty("humanAnchor").objectReferenceValue = humanAnchor;
        soOrch.FindProperty("videoScreen").objectReferenceValue = videoScreen;
        soOrch.ApplyModifiedPropertiesWithoutUndo();

        Undo.RecordObject(med, "Wire");
        var soMed = new SerializedObject(med);
        soMed.FindProperty("eraDatabase").objectReferenceValue = db;
        soMed.FindProperty("screenAnchor").objectReferenceValue = screenAnchor;
        soMed.FindProperty("videoScreen").objectReferenceValue = videoScreen;
        soMed.FindProperty("audioSource").objectReferenceValue = audioSource;
        soMed.ApplyModifiedPropertiesWithoutUndo();

        if (bridge != null && agent != null)
        {
            Undo.RecordObject(bridge, "Wire");
            var soBridge = new SerializedObject(bridge);
            soBridge.FindProperty("detectionAgent").objectReferenceValue = agent;
            soBridge.ApplyModifiedPropertiesWithoutUndo();
        }

        if (eraUI != null)
        {
            Undo.RecordObject(eraUI, "Wire");
            var soUI = new SerializedObject(eraUI);
            var xrProp = soUI.FindProperty("xrCamera");
            if (xrProp != null) xrProp.objectReferenceValue = centerEye;
            soUI.ApplyModifiedPropertiesWithoutUndo();
        }

        if (eraMgr != null && db != null)
        {
            Undo.RecordObject(eraMgr, "Wire");
            var soEra = new SerializedObject(eraMgr);
            soEra.FindProperty("eraDatabase").objectReferenceValue = db;
            soEra.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static Transform FindOrCreateAnchor(string name, Vector3 pos)
    {
        var existing = GameObject.Find(name);
        if (existing != null) return existing.transform;
        var go = new GameObject(name);
        go.transform.position = pos;
        Undo.RegisterCreatedObjectUndo(go, name);
        return go.transform;
    }

    private static GameObject FindOrCreateEraSwitcherCanvas()
    {
        var existing = GameObject.Find("EraSwitcherCanvas");
        if (existing != null) return existing;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EraSwitcherCanvasPrefabPath);
        if (prefab == null) return null;
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(instance, "EraSwitcherCanvas");
        return instance;
    }

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
        return Camera.main != null ? Camera.main.transform : null;
    }

    private static void LogCanonicalReady(List<string> log, GameObject systemGo, GameObject canvasGo, Transform screenAnchor, int removedMissing)
    {
        Debug.Log("[TemporalEcho] ========== CANONICAL READY ==========");
        foreach (var m in log) Debug.Log("[TemporalEcho] " + m);
        Debug.Log("[TemporalEcho] Scene objects: EventSystem, EraSwitcherCanvas, ScreenAnchor, BookAnchor, HumanAnchor, VideoScreen (under ScreenAnchor), " + SystemGoName);
        Debug.Log("[TemporalEcho] " + SystemGoName + " components: ShowcaseSanitizer, TemporalEchoRuntimeOrchestrator, EraMediaController, AudioSource, TemporalEchoDetectionBridge");
        Debug.Log("[TemporalEcho] Disabled: duplicate orchestrators/bridges/media, StorySelectionManager, mic objects, extra VideoScreens");
        Debug.Log("[TemporalEcho] Removed " + removedMissing + " missing script(s).");
        Debug.Log("[TemporalEcho] 1) Assign EraDatabase rules (prefabs+audio/video). 2) Build & run. 3) Show laptop/book/person -> overlay + media. 4) UI pinch -> switch era.");
    }
}
#endif
