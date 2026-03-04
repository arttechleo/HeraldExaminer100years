#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TemporalEcho;

public static class TemporalEchoRuntimeSetupMenu
{
    private const string EraDatabasePath = "Assets/TemporalEcho/EraDatabase.asset";
    private const string EraSwitcherCanvasPrefabPath = "Assets/TemporalEcho/Prefabs/EraSwitcherCanvas.prefab";
    private const string VideoScreenPrefabPath = "Assets/TemporalEcho/Prefabs/VideoScreen.prefab";
    private const string RuntimeGoName = "Temporal Echo Runtime";
    private const string AnchorNameScreen = "ScreenAnchor";
    private const string AnchorNameBook = "BookAnchor";
    private const string AnchorNameHuman = "HumanAnchor";
    private static readonly System.Type OVRInputModuleType = System.Type.GetType("UnityEngine.EventSystems.OVRInputModule, Oculus.VR") ?? System.Type.GetType("UnityEngine.EventSystems.OVRInputModule, Meta.XR.SDK.Core");

    [MenuItem("Tools/Temporal Echo/Calibrate Era UI Panel")]
    public static void CalibrateEraUIPanel()
    {
        var placer = Object.FindAnyObjectByType<WorldSpacePanelPlacer>();
        if (placer == null)
        {
            var canvas = GameObject.Find("EraSwitcherCanvas");
            if (canvas != null) placer = canvas.GetComponent<WorldSpacePanelPlacer>();
        }
        if (placer == null)
        {
            Debug.LogWarning("[TemporalEcho] No EraSwitcherCanvas with WorldSpacePanelPlacer in scene. Run Setup Runtime Orchestrator first.");
            return;
        }
        placer.CalibrateNow();
        EditorUtility.SetDirty(placer);
        var scene = SceneManager.GetActiveScene();
        if (scene.IsValid()) EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[TemporalEcho] Era UI panel calibrated (position + rotation).");
    }

    [MenuItem("Tools/Temporal Echo/Setup Runtime Orchestrator")]
    public static void SetupRuntimeOrchestrator()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            Debug.LogError("[TemporalEcho] No active scene.");
            return;
        }

        EnsureEventSystem();
        Transform screenAnchor = FindOrCreateAnchor(AnchorNameScreen, new Vector3(0f, 0f, 1.5f));
        Transform bookAnchor = FindOrCreateAnchor(AnchorNameBook, new Vector3(0.5f, 0f, 1.5f));
        Transform humanAnchor = FindOrCreateAnchor(AnchorNameHuman, new Vector3(1f, 0f, 1.5f));

        GameObject canvasGo = FindOrCreateEraSwitcherCanvas();
        EnsureVideoScreenUnder(screenAnchor);

        GameObject runtimeGo = GameObject.Find(RuntimeGoName);
        if (runtimeGo == null)
        {
            runtimeGo = new GameObject(RuntimeGoName);
            Undo.RegisterCreatedObjectUndo(runtimeGo, "Create " + RuntimeGoName);
        }

        var orchestrator = runtimeGo.GetComponent<TemporalEchoRuntimeOrchestrator>();
        if (orchestrator == null)
            orchestrator = runtimeGo.AddComponent<TemporalEchoRuntimeOrchestrator>();

        var binder = runtimeGo.GetComponent<EraDetectionBinder>();
        if (binder == null)
            binder = runtimeGo.AddComponent<EraDetectionBinder>();

        var mediaController = runtimeGo.GetComponent<EraMediaController>();
        if (mediaController == null)
            mediaController = runtimeGo.AddComponent<EraMediaController>();

        var db = AssetDatabase.LoadAssetAtPath<EraDatabase>(EraDatabasePath);
        var eraUI = canvasGo != null ? canvasGo.GetComponent<EraSwitcherUI>() : null;
        var videoScreen = screenAnchor != null ? screenAnchor.GetComponentInChildren<EraVideoScreen>(true) : null;
        var centerEye = FindCenterEye();
        var agentType = System.Type.GetType("Meta.XR.BuildingBlocks.AIBlocks.ObjectDetectionAgent, Meta.XR.BuildingBlocks.AIBlocks");
        var agent = agentType != null ? Object.FindAnyObjectByType(agentType) as UnityEngine.Component : null;
        var placer = canvasGo != null ? canvasGo.GetComponent<WorldSpacePanelPlacer>() : null;

        Undo.RecordObject(orchestrator, "Assign Orchestrator");
        SerializedObject soOrch = new SerializedObject(orchestrator);
        soOrch.FindProperty("database").objectReferenceValue = db;
        soOrch.FindProperty("eraUI").objectReferenceValue = eraUI;
        soOrch.FindProperty("mediaController").objectReferenceValue = mediaController;
        soOrch.FindProperty("screenAnchor").objectReferenceValue = screenAnchor;
        soOrch.FindProperty("bookAnchor").objectReferenceValue = bookAnchor;
        soOrch.FindProperty("humanAnchor").objectReferenceValue = humanAnchor;
        soOrch.FindProperty("videoScreen").objectReferenceValue = videoScreen;
        soOrch.ApplyModifiedPropertiesWithoutUndo();

        Undo.RecordObject(mediaController, "Assign Media");
        SerializedObject soMed = new SerializedObject(mediaController);
        soMed.FindProperty("eraDatabase").objectReferenceValue = db;
        soMed.FindProperty("screenAnchor").objectReferenceValue = screenAnchor;
        soMed.FindProperty("videoScreen").objectReferenceValue = videoScreen;
        soMed.ApplyModifiedPropertiesWithoutUndo();

        if (placer != null)
        {
            Undo.RecordObject(placer, "Assign CenterEye");
            placer.centerEye = centerEye;
            placer.heightOffset = 0f;
        }

        if (eraUI != null)
        {
            Undo.RecordObject(eraUI, "Assign xrCamera");
            var eraUISo = new SerializedObject(eraUI);
            var xrCamProp = eraUISo.FindProperty("xrCamera");
            if (xrCamProp != null) xrCamProp.objectReferenceValue = centerEye;
            eraUISo.ApplyModifiedPropertiesWithoutUndo();
        }

        // AudioSource for EraMediaController (soundtrack)
        var audioSource = runtimeGo.GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = runtimeGo.AddComponent<AudioSource>();
        Undo.RecordObject(mediaController, "Assign AudioSource");
        var soMedAud = new SerializedObject(mediaController);
        var audProp = soMedAud.FindProperty("audioSource");
        if (audProp != null) audProp.objectReferenceValue = audioSource;
        soMedAud.ApplyModifiedPropertiesWithoutUndo();

        EnsureCanvasMetaXR(canvasGo, centerEye);

        Undo.RecordObject(binder, "Wire Binder");
        var soBinder = new SerializedObject(binder);
        soBinder.FindProperty("database").objectReferenceValue = db;
        soBinder.FindProperty("eraUI").objectReferenceValue = eraUI;
        soBinder.FindProperty("detectionAgent").objectReferenceValue = agent;
        soBinder.FindProperty("screenAnchor").objectReferenceValue = screenAnchor;
        soBinder.FindProperty("bookAnchor").objectReferenceValue = bookAnchor;
        soBinder.FindProperty("humanAnchor").objectReferenceValue = humanAnchor;
        soBinder.FindProperty("audioSource").objectReferenceValue = audioSource;
        soBinder.FindProperty("videoScreen").objectReferenceValue = videoScreen;
        soBinder.FindProperty("logVerbose").boolValue = true;
        soBinder.ApplyModifiedPropertiesWithoutUndo();

        DisableConflictingOverlaySpawners();

        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = runtimeGo;
        var es = Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
        LogChecklist(es, canvasGo, placer, screenAnchor, bookAnchor, humanAnchor, runtimeGo, orchestrator, mediaController, db);
    }

    [MenuItem("Tools/Temporal Echo/Setup Full Runtime (Meta XR)")]
    public static void SetupFullRuntimeMetaXR()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid()) { Debug.LogError("[TemporalEcho] No active scene."); return; }

        TemporalEchoPrepareShowcase.PrepareSceneForShowcase();
        SetupRuntimeOrchestrator();
        CalibrateEraUIPanel();

        var es = Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
        var canvasGo = GameObject.Find("EraSwitcherCanvas");
        var placer = canvasGo != null ? canvasGo.GetComponent<WorldSpacePanelPlacer>() : null;
        var screenAnchor = GameObject.Find("ScreenAnchor")?.transform;
        var bookAnchor = GameObject.Find("BookAnchor")?.transform;
        var humanAnchor = GameObject.Find("HumanAnchor")?.transform;
        var runtimeGo = GameObject.Find(RuntimeGoName);
        var orchestrator = runtimeGo != null ? runtimeGo.GetComponent<TemporalEchoRuntimeOrchestrator>() : null;
        var mediaController = runtimeGo != null ? runtimeGo.GetComponent<EraMediaController>() : null;
        var db = AssetDatabase.LoadAssetAtPath<EraDatabase>(EraDatabasePath);

        bool agentFound = false;
        var agentType = System.Type.GetType("Meta.XR.BuildingBlocks.AIBlocks.ObjectDetectionAgent, Meta.XR.BuildingBlocks.AIBlocks");
        if (agentType != null && Object.FindAnyObjectByType(agentType) != null)
            agentFound = true;

        int missingCount = 0;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                missingCount += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
        }

        Debug.Log("[TemporalEcho] ========== READY FOR SHOWCASE ==========");
        Debug.Log(es != null && OVRInputModuleType != null && es.GetComponent(OVRInputModuleType) != null ? "[TemporalEcho] [OK] EventSystem" : "[TemporalEcho] [!!] EventSystem");
        Debug.Log(canvasGo != null ? "[TemporalEcho] [OK] UI (EraSwitcherCanvas)" : "[TemporalEcho] [!!] UI");
        Debug.Log(agentFound ? "[TemporalEcho] [OK] Detection (agent found)" : "[TemporalEcho] [!!] Detection");
        Debug.Log(screenAnchor != null && bookAnchor != null && humanAnchor != null ? "[TemporalEcho] [OK] Anchors" : "[TemporalEcho] [!!] Anchors");
        Debug.Log(mediaController != null && runtimeGo != null && runtimeGo.GetComponent<AudioSource>() != null ? "[TemporalEcho] [OK] Media (one AudioSource)" : "[TemporalEcho] [!!] Media");
        Debug.Log(screenAnchor != null && screenAnchor.GetComponentInChildren<EraVideoScreen>(true) != null ? "[TemporalEcho] [OK] One VideoScreen" : "[TemporalEcho] [--] VideoScreen");
        Debug.Log(PersistentDiscoveryManager.DisableVirtualMic ? "[TemporalEcho] [OK] Mic disabled" : "[TemporalEcho] [!!] Enable DisableVirtualMic for showcase");
        Debug.Log(missingCount == 0 ? "[TemporalEcho] [OK] No missing scripts" : "[TemporalEcho] [!!] " + missingCount + " missing script(s)");
        var binderGo = GameObject.Find(RuntimeGoName);
        var binderComp = binderGo != null ? binderGo.GetComponent<EraDetectionBinder>() : null;
        Debug.Log(binderComp != null && binderComp.enabled ? "[TemporalEcho] [OK] EraDetectionBinder active (single overlay source)" : "[TemporalEcho] [!!] EraDetectionBinder");
        Debug.Log("[TemporalEcho] VERIFY: Editor Play → watch Console for ACCEPT/SPAWN/MEDIA logs. Device → confirm DETECTION UPDATE logs.");
        Debug.Log("[TemporalEcho] Assign prefabs/audio/video in EraDatabase then Build & Run.");
    }

    private static void EnsureEventSystem()
    {
        var es = Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
        if (es == null)
        {
            var go = new GameObject("EventSystem");
            Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
            go.AddComponent<UnityEngine.EventSystems.EventSystem>();
            if (OVRInputModuleType != null)
                go.AddComponent(OVRInputModuleType);
            return;
        }
        var standalone = es.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        if (standalone != null)
        {
            Undo.DestroyObjectImmediate(standalone);
        }
        var xriType = System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule, Unity.XR.Interaction.Toolkit");
        if (xriType != null)
        {
            var xriModule = es.GetComponent(xriType);
            if (xriModule != null)
                Undo.DestroyObjectImmediate(xriModule);
        }
        if (OVRInputModuleType != null && es.GetComponent(OVRInputModuleType) == null)
            es.gameObject.AddComponent(OVRInputModuleType);
    }

    private static readonly System.Type OVRRaycasterType = System.Type.GetType("OVRRaycaster, Oculus.VR") ?? System.Type.GetType("OVRRaycaster, Meta.XR.SDK.Core");

    private static void EnsureCanvasMetaXR(GameObject canvasGo, Transform centerEye)
    {
        if (canvasGo == null) return;
        var canvas = canvasGo.GetComponent<Canvas>();
        if (canvas != null && centerEye != null)
        {
            var cam = centerEye.GetComponent<Camera>();
            if (cam == null) cam = centerEye.GetComponentInChildren<Camera>();
            if (cam != null)
            {
                Undo.RecordObject(canvas, "Assign worldCamera");
                canvas.worldCamera = cam;
            }
        }
        if (OVRRaycasterType != null && canvasGo.GetComponent(OVRRaycasterType) == null)
            canvasGo.AddComponent(OVRRaycasterType);
    }

    private static void DisableConflictingOverlaySpawners()
    {
        var orch = Object.FindAnyObjectByType<TemporalEchoRuntimeOrchestrator>();
        if (orch != null && orch.enabled)
        {
            Undo.RecordObject(orch, "Disable orchestrator");
            orch.enabled = false;
            Debug.Log("[TemporalEcho] Disabled TemporalEchoRuntimeOrchestrator (EraDetectionBinder is active).");
        }
        var bridgeType = System.Type.GetType("TemporalEchoDetectionBridge");
        if (bridgeType != null)
        {
            var bridges = Object.FindObjectsByType(bridgeType, FindObjectsSortMode.None);
            foreach (var b in bridges)
            {
                if (b is MonoBehaviour mb && mb.enabled)
                {
                    Undo.RecordObject(mb, "Disable bridge");
                    mb.enabled = false;
                    Debug.Log("[TemporalEcho] Disabled TemporalEchoDetectionBridge.");
                }
            }
        }
        var dprType = System.Type.GetType("DetectionPrefabReplacer");
        if (dprType != null)
        {
            var dprs = Object.FindObjectsByType(dprType, FindObjectsSortMode.None);
            foreach (var d in dprs)
            {
                if (d is MonoBehaviour mb && mb.enabled)
                {
                    Undo.RecordObject(mb, "Disable DetectionPrefabReplacer");
                    mb.enabled = false;
                    Debug.Log("[TemporalEcho] Disabled DetectionPrefabReplacer (EraDetectionBinder spawns overlays).");
                }
            }
        }
    }

    private static void EnsureDetectionBridge()
    {
        var agentType = System.Type.GetType("Meta.XR.BuildingBlocks.AIBlocks.ObjectDetectionAgent, Meta.XR.BuildingBlocks.AIBlocks");
        if (agentType == null) return;
        var agent = Object.FindAnyObjectByType(agentType) as UnityEngine.Component;
        if (agent == null) return;
        var bridgeType = System.Type.GetType("TemporalEchoDetectionBridge");
        if (bridgeType == null)
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                bridgeType = asm.GetType("TemporalEchoDetectionBridge");
                if (bridgeType != null) break;
            }
        }
        if (bridgeType == null) return;
        if (agent.GetComponent(bridgeType) != null) return;
        agent.gameObject.AddComponent(bridgeType);
        Debug.Log("[TemporalEcho] Added TemporalEchoDetectionBridge to ObjectDetectionAgent.");
    }

    private static void LogChecklist(
        UnityEngine.EventSystems.EventSystem es,
        GameObject canvasGo,
        WorldSpacePanelPlacer placer,
        Transform screenAnchor,
        Transform bookAnchor,
        Transform humanAnchor,
        GameObject runtimeGo,
        TemporalEchoRuntimeOrchestrator orchestrator,
        EraMediaController mediaController,
        EraDatabase db)
    {
        Debug.Log("[TemporalEcho] === Setup Full Runtime (Meta XR) checklist ===");
        bool ovrOk = es != null && OVRInputModuleType != null && es.GetComponent(OVRInputModuleType) != null;
        Debug.Log(ovrOk ? "[TemporalEcho] [OK] EventSystem with OVRInputModule" : "[TemporalEcho] [--] EventSystem / OVRInputModule");
        Debug.Log(canvasGo != null ? "[TemporalEcho] [OK] EraSwitcherCanvas" : "[TemporalEcho] [--] EraSwitcherCanvas");
        Debug.Log(placer != null && placer.centerEye != null ? "[TemporalEcho] [OK] WorldSpacePanelPlacer + centerEye" : "[TemporalEcho] [--] WorldSpacePanelPlacer/centerEye");
        Debug.Log(screenAnchor != null ? "[TemporalEcho] [OK] ScreenAnchor" : "[TemporalEcho] [--] ScreenAnchor");
        Debug.Log(bookAnchor != null ? "[TemporalEcho] [OK] BookAnchor" : "[TemporalEcho] [--] BookAnchor");
        Debug.Log(humanAnchor != null ? "[TemporalEcho] [OK] HumanAnchor" : "[TemporalEcho] [--] HumanAnchor");
        Debug.Log(runtimeGo != null && orchestrator != null ? "[TemporalEcho] [OK] Temporal Echo Runtime + Orchestrator" : "[TemporalEcho] [--] Runtime GO");
        Debug.Log(mediaController != null ? "[TemporalEcho] [OK] EraMediaController" : "[TemporalEcho] [--] EraMediaController");
        Debug.Log(db != null ? "[TemporalEcho] [OK] EraDatabase" : "[TemporalEcho] [--] EraDatabase");
        Debug.Log("[TemporalEcho] Assign 3D prefabs in EraDatabase and test in headset. See Assets/TemporalEcho/_Diagnostics/HowToTestRuntime.md");
    }

    private static Transform FindOrCreateAnchor(string name, Vector3 position)
    {
        var existing = GameObject.Find(name);
        if (existing != null) return existing.transform;
        var go = new GameObject(name);
        go.transform.position = position;
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        return go.transform;
    }

    private static GameObject FindOrCreateEraSwitcherCanvas()
    {
        var existing = GameObject.Find("EraSwitcherCanvas");
        if (existing != null) return existing;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EraSwitcherCanvasPrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning("[TemporalEcho] EraSwitcherCanvas prefab not found. Create via Tools > Temporal Echo > Create Era Switcher Canvas Prefab.");
            return null;
        }
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(instance, "Create EraSwitcherCanvas");
        return instance;
    }

    private static void EnsureVideoScreenUnder(Transform screenAnchor)
    {
        if (screenAnchor == null) return;
        if (screenAnchor.GetComponentInChildren<EraVideoScreen>(true) != null) return;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VideoScreenPrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning("[TemporalEcho] VideoScreen prefab not found. Create via Tools > Temporal Echo > Create VideoScreen Prefab.");
            return;
        }
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, screenAnchor);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
        Undo.RegisterCreatedObjectUndo(instance, "Create VideoScreen");
    }

    private static Transform FindCenterEye()
    {
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (t.name == "CenterEyeAnchor")
            {
                if (t.GetComponent<Camera>() != null) return t;
                var cam = t.GetComponentInChildren<Camera>();
                if (cam != null) return cam.transform;
                return t;
            }
        }
        var main = Camera.main;
        return main != null ? main.transform : null;
    }
}
#endif
