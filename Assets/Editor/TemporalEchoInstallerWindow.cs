#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TemporalEchoInstallerWindow : EditorWindow
{
    private const string RootName = "Temporal Echo System";
    private const string ManagersName = "Managers";
    private const string UIName = "UI";
    private const string DiagnosticsName = "Diagnostics";
    private const string TemporalEchoUILayer = "TemporalEchoUI";
    private const string MicrophoneLayerName = "Microphone";
    private const string EraConfigPath = "Assets/TemporalEcho/EraConfig.asset";

    private Vector2 _scroll;
    private string _log = "";
    private bool _installLayersConfirmed;

    [MenuItem("Tools/Temporal Echo/One-Click Install")]
    public static void Open()
    {
        var w = GetWindow<TemporalEchoInstallerWindow>("Temporal Echo Installer");
        w.minSize = new Vector2(400, 380);
    }

    [MenuItem("Tools/Temporal Echo/Toggle Test Mode")]
    public static void ToggleTestMode()
    {
        var t = UnityEngine.Object.FindObjectOfType<TemporalEchoTestMode>();
        if (t == null)
        {
            Debug.LogWarning("[TemporalEcho] No TemporalEchoTestMode in scene. Add it to the detection chain GameObject.");
            return;
        }
        Undo.RecordObject(t, "Toggle Test Mode");
        t.SetTestModeEnabled(!t.IsTestModeEnabled);
        Debug.Log("[TemporalEcho] Test Mode: " + (t.IsTestModeEnabled ? "ON" : "OFF"));
    }

    [MenuItem("Tools/Temporal Echo/Ensure UI Layer (TemporalEchoUI)")]
    public static void EnsureUILayerMenu() => TemporalEchoEnsureUILayer.EnsureUILayer();

    [MenuItem("Tools/Temporal Echo/Ensure Microphone Layer")]
    public static void EnsureMicrophoneLayerMenu()
    {
        MicrophoneLayerSetup.EnsureMicrophoneLayer();
        Debug.Log("[TemporalEcho] Microphone layer ensured. Run again if it was missing.");
    }

    [MenuItem("Tools/Temporal Echo/Rebuild Era UI Panel")]
    public static void RebuildEraUIPanel()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[TemporalEcho] Enter Play mode first to rebuild Era UI Panel.");
            return;
        }
        var panel = UnityEngine.Object.FindObjectOfType<EraSwitchPanel>();
        if (panel == null)
        {
            Debug.LogWarning("[TemporalEcho] No EraSwitchPanel in scene.");
            return;
        }
        panel.RebuildNow();
        Debug.Log("[TemporalEcho] Era UI Panel rebuilt.");
    }

    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Temporal Echo — One-Click Install", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Install/Update sets up the current scene: root hierarchy, layers, components, and EraConfig. " +
            "After install, assign prefabs and audio clips in the EraConfig asset only.",
            MessageType.Info);
        EditorGUILayout.Space(8);

        if (GUILayout.Button("1) Install / Update in Current Scene", GUILayout.Height(32)))
            InstallOrUpdate();

        if (GUILayout.Button("2) Create EraConfig Asset (if missing)", GUILayout.Height(28)))
            CreateEraConfigAsset();

        if (GUILayout.Button("3) Select EraConfig Asset", GUILayout.Height(28)))
            SelectEraConfigAsset();

        if (GUILayout.Button("4) Validate Installation", GUILayout.Height(28)))
            ValidateInstallation();

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("Log", EditorStyles.boldLabel);
        EditorGUILayout.TextArea(_log, GUILayout.ExpandHeight(true));

        EditorGUILayout.EndScrollView();
    }

    private void Log(string msg)
    {
        _log += msg + "\n";
        Debug.Log("[TemporalEchoInstaller] " + msg);
    }

    private void InstallOrUpdate()
    {
        _log = "";
        Undo.SetCurrentGroupName("Temporal Echo Install");
        int undoGroup = Undo.GetCurrentGroup();

        if (!Application.isPlaying && !EnsureLayers())
        {
            Log("Install aborted: layers not set.");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Log("FAIL: No active scene.");
            return;
        }

        GameObject root = FindOrCreateRoot();
        if (root == null) { Log("FAIL: Could not create root."); return; }

        GameObject managers = FindOrCreateChild(root, ManagersName);
        GameObject ui = FindOrCreateChild(root, UIName);
        GameObject diagnostics = FindOrCreateChild(root, DiagnosticsName);

        TemporalEraConfig config = EnsureEraConfigAsset();
        PersistentDiscoveryManager persistentManager = FindOrCreateDetectionChain(managers, out GameObject detectionGo);
        if (persistentManager == null)
            Log("WARN: No ObjectDetectionAgent in scene; DetectionPrefabReplacer/PersistentDiscoveryManager not found. Add detection GO and run Install again.");

        TemporalEraManager eraManager = EnsureComponent<TemporalEraManager>(managers);
        if (eraManager != null)
        {
            Undo.RecordObject(eraManager, "Assign EraConfig");
            SerializedObject soEra = new SerializedObject(eraManager);
            soEra.FindProperty("eraConfig").objectReferenceValue = config;
            if (persistentManager != null)
                soEra.FindProperty("persistentManager").objectReferenceValue = persistentManager;
            soEra.ApplyModifiedPropertiesWithoutUndo();
        }

        EnsureComponent<AudioFocusManager>(managers);
        QuestAudioDoctor doctor = EnsureComponent<QuestAudioDoctor>(managers);
        if (doctor != null)
        {
            SerializedObject soDoc = new SerializedObject(doctor);
            soDoc.FindProperty("enableStartupBeepTest").boolValue = false;
            soDoc.ApplyModifiedPropertiesWithoutUndo();
        }

        StorySelectionManager storySelection = EnsureComponent<StorySelectionManager>(ui);
        EraSwitchPanel panel = EnsureComponent<EraSwitchPanel>(ui);
        if (panel != null)
        {
            SerializedObject soPanel = new SerializedObject(panel);
            soPanel.FindProperty("eraManager").objectReferenceValue = eraManager;
            if (storySelection != null)
                soPanel.FindProperty("storyManager").objectReferenceValue = storySelection;
            soPanel.ApplyModifiedPropertiesWithoutUndo();
        }

        PinchRayUIInteractor pincher = EnsureComponent<PinchRayUIInteractor>(ui);
        int uiLayer = LayerMask.NameToLayer(TemporalEchoUILayer);
        if (pincher != null && uiLayer >= 0)
        {
            SerializedObject soP = new SerializedObject(pincher);
            soP.FindProperty("uiLayerMask").intValue = 1 << uiLayer;
            soP.ApplyModifiedPropertiesWithoutUndo();
        }

        EnsureComponent<MRStatusOverlay>(ui);
        EnsureComponent<TemporalEchoDemoSeeder>(managers);

        if (config != null && eraManager != null)
            EnsureEraConfigMappings(config);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        Log("Install/Update done. Assign prefabs and clips in EraConfig: " + EraConfigPath);
    }

    private bool EnsureLayers()
    {
        bool needUI = LayerMask.NameToLayer(TemporalEchoUILayer) < 0;
        bool needMic = LayerMask.NameToLayer(MicrophoneLayerName) < 0;
        if (!needUI && !needMic) return true;

        if (!EditorUtility.DisplayDialog("Add Layers", "Add missing layer(s): " + (needUI ? TemporalEchoUILayer + " " : "") + (needMic ? MicrophoneLayerName : "") + "?", "Add", "Cancel"))
            return false;

        var tagManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (tagManager == null || tagManager.Length == 0) { Log("FAIL: TagManager not found."); return false; }

        SerializedObject so = new SerializedObject(tagManager[0]);
        SerializedProperty layers = so.FindProperty("layers");
        if (layers == null) { Log("FAIL: layers property not found."); return false; }

        for (int i = 8; i < 32; i++)
        {
            var p = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(p.stringValue))
            {
                if (needUI) { p.stringValue = TemporalEchoUILayer; needUI = false; }
                else if (needMic) { p.stringValue = MicrophoneLayerName; needMic = false; }
                so.ApplyModifiedPropertiesWithoutUndo();
                if (!needUI && !needMic) break;
            }
        }
        return true;
    }

    private GameObject FindOrCreateRoot()
    {
        var existing = GameObject.Find(RootName);
        if (existing != null) { Log("Root '" + RootName + "' found."); return existing; }
        GameObject go = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(go, "Create " + RootName);
        Log("Created " + RootName);
        return go;
    }

    private GameObject FindOrCreateChild(GameObject parent, string name)
    {
        Transform t = parent.transform.Find(name);
        if (t != null) return t.gameObject;
        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    private T EnsureComponent<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        if (c != null) return c;
        c = Undo.AddComponent<T>(go);
        Log("Added " + typeof(T).Name + " to " + go.name);
        return c;
    }

    private static System.Type GetObjectDetectionAgentType()
    {
        var type = Type.GetType("Meta.XR.BuildingBlocks.AIBlocks.ObjectDetectionAgent, Meta.XR.BuildingBlocks.AIBlocks");
        if (type != null) return type;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                type = asm.GetType("Meta.XR.BuildingBlocks.AIBlocks.ObjectDetectionAgent");
                if (type != null) return type;
            }
            catch { }
        }
        return null;
    }

    private PersistentDiscoveryManager FindOrCreateDetectionChain(GameObject managers, out GameObject detectionGo)
    {
        detectionGo = null;
        var agentType = GetObjectDetectionAgentType();
        Component[] agents = null;
        if (agentType != null)
            agents = UnityEngine.Object.FindObjectsByType(agentType, FindObjectsSortMode.None) as Component[];
        if (agents == null || agents.Length == 0)
        {
            Log("WARN: No ObjectDetectionAgent in scene.");
            return null;
        }
        if (agents.Length > 1)
            Log("WARN: Multiple ObjectDetectionAgent found; using first active.");

        for (int i = 0; i < agents.Length; i++)
        {
            if (agents[i] != null && agents[i].gameObject.activeInHierarchy) { detectionGo = agents[i].gameObject; break; }
            if (detectionGo == null && agents[i] != null) detectionGo = agents[i].gameObject;
        }
        if (detectionGo == null) return null;

        var replacer = detectionGo.GetComponent<DetectionPrefabReplacer>();
        var persistent = detectionGo.GetComponent<PersistentDiscoveryManager>();
        if (replacer == null) Log("WARN: DetectionPrefabReplacer not on detection GO (add Building Blocks detection first).");
        if (persistent == null)
        {
            persistent = Undo.AddComponent<PersistentDiscoveryManager>(detectionGo);
            Log("Added PersistentDiscoveryManager to " + detectionGo.name);
        }
        if (detectionGo.GetComponent<DetectionLabelCollector>() == null)
        {
            Undo.AddComponent<DetectionLabelCollector>(detectionGo);
            Log("Added DetectionLabelCollector to " + detectionGo.name);
        }
        return persistent;
    }

    private TemporalEraConfig EnsureEraConfigAsset()
    {
        var config = AssetDatabase.LoadAssetAtPath<TemporalEraConfig>(EraConfigPath);
        if (config != null) return config;
        return CreateEraConfigAssetInternal();
    }

    private void CreateEraConfigAsset()
    {
        _log = "";
        if (AssetDatabase.LoadAssetAtPath<TemporalEraConfig>(EraConfigPath) != null)
        {
            Log("EraConfig already exists at " + EraConfigPath);
            return;
        }
        CreateEraConfigAssetInternal();
        Log("Created " + EraConfigPath);
    }

    private TemporalEraConfig CreateEraConfigAssetInternal()
    {
        if (!AssetDatabase.IsValidFolder("Assets/TemporalEcho"))
            AssetDatabase.CreateFolder("Assets", "TemporalEcho");
        var config = ScriptableObject.CreateInstance<TemporalEraConfig>();
        config.mappings = new List<CategoryEraMapping>();
        config.EnsureAllCategoriesPresent();
        var guids = AssetDatabase.FindAssets("OldMicrophone t:Prefab");
        if (guids != null && guids.Length > 0)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            if (!string.IsNullOrEmpty(path))
                config.defaultMicrophonePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
        AssetDatabase.CreateAsset(config, EraConfigPath);
        AssetDatabase.SaveAssets();
        return config;
    }

    private void EnsureEraConfigMappings(TemporalEraConfig config)
    {
        config.EnsureAllCategoriesPresent();
        EditorUtility.SetDirty(config);
    }

    private void SelectEraConfigAsset()
    {
        var config = AssetDatabase.LoadAssetAtPath<TemporalEraConfig>(EraConfigPath);
        if (config == null)
        {
            config = CreateEraConfigAssetInternal();
            Log("Created and selected " + EraConfigPath);
        }
        Selection.activeObject = config;
        EditorGUIUtility.PingObject(config);
    }

    private void ValidateInstallation()
    {
        _log = "";
        bool pass = true;

        if (LayerMask.NameToLayer(TemporalEchoUILayer) < 0) { Log("FAIL: Layer '" + TemporalEchoUILayer + "' missing."); pass = false; }
        else Log("PASS: Layer " + TemporalEchoUILayer);
        if (LayerMask.NameToLayer(MicrophoneLayerName) < 0) { Log("FAIL: Layer '" + MicrophoneLayerName + "' missing."); pass = false; }
        else Log("PASS: Layer " + MicrophoneLayerName);

        var root = GameObject.Find(RootName);
        if (root == null) { Log("FAIL: Root '" + RootName + "' not in scene."); pass = false; }
        else
        {
            Log("PASS: Root " + RootName);
            if (root.transform.Find(ManagersName) == null) { Log("WARN: Managers child missing."); }
            if (root.transform.Find(UIName) == null) { Log("WARN: UI child missing."); }
        }

        var config = AssetDatabase.LoadAssetAtPath<TemporalEraConfig>(EraConfigPath);
        if (config == null) { Log("FAIL: EraConfig asset missing at " + EraConfigPath); pass = false; }
        else
        {
            Log("PASS: EraConfig at " + EraConfigPath);
            if (config.mappings != null)
            {
                foreach (var m in config.mappings)
                {
                    GameObject prefab = null;
                    switch (config.defaultEra)
                    {
                        case TemporalEra.Era1920s: prefab = m.era1920s.prefab; break;
                        case TemporalEra.Era1960s: prefab = m.era1960s.prefab; break;
                        case TemporalEra.EraToday: prefab = m.eraToday.prefab; break;
                    }
                    if (prefab == null)
                        Log("WARN: EraConfig category " + m.category + " has no prefab for default era " + config.defaultEra + ". Assign in EraConfig asset.");
                }
            }
        }

        var eraManager = UnityEngine.Object.FindAnyObjectByType<TemporalEraManager>();
        if (eraManager == null) { Log("FAIL: TemporalEraManager not in scene."); pass = false; }
        else
        {
            var so = new SerializedObject(eraManager);
            if (so.FindProperty("eraConfig").objectReferenceValue == null) { Log("WARN: TemporalEraManager.eraConfig not assigned."); }
            else Log("PASS: TemporalEraManager has EraConfig.");
        }

        var agentType = GetObjectDetectionAgentType();
        Component agent = null;
        if (agentType != null)
            agent = UnityEngine.Object.FindAnyObjectByType(agentType) as Component;
        if (agent == null) Log("WARN: ObjectDetectionAgent not in scene.");
        else if (agent.GetComponent<DetectionLabelCollector>() == null) { Log("FAIL: DetectionLabelCollector missing on detection GO."); pass = false; }
        else Log("PASS: DetectionLabelCollector on detection GO.");

        Log(pass ? "--- Validation PASS ---" : "--- Validation FAIL ---");
    }
}
#endif
