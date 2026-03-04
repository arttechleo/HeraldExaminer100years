#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using TMPro;
using TemporalEcho;

public static class CreateEraSwitcherCanvasPrefab
{
    private const string PrefabPath = "Assets/TemporalEcho/Prefabs/EraSwitcherCanvas.prefab";
    private static readonly System.Type OVRRaycasterType = System.Type.GetType("OVRRaycaster, Oculus.VR") ?? System.Type.GetType("OVRRaycaster, Meta.XR.SDK.Core");
    private static readonly System.Type OVRInputModuleType = System.Type.GetType("UnityEngine.EventSystems.OVRInputModule, Oculus.VR") ?? System.Type.GetType("UnityEngine.EventSystems.OVRInputModule, Meta.XR.SDK.Core");
    private const float CanvasWidth = 600f;
    private const float CanvasHeight = 520f;
    private const float WorldScale = 0.002f;
    private const float ButtonWidth = 500f;
    private const float ButtonHeight = 140f;
    private const float Spacing = 16f;
    private const float TitleHeight = 48f;

    [MenuItem("Tools/Temporal Echo/Create Era Switcher Canvas Prefab")]
    public static void CreatePrefab()
    {
        try
        {
            CreatePrefabInternal();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[EraSwitcher] Create prefab failed: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private static void CreatePrefabInternal()
    {
        TMP_FontAsset font = GetDefaultTMPFont();
        if (font == null)
        {
            Debug.LogWarning("[EraSwitcher] No TMP font found. Import TMP Essential Resources: Window > TextMeshPro > Import TMP Essential Resources. Using fallback.");
        }

        var root = new GameObject("EraSwitcherCanvas");
        if (root == null)
        {
            Debug.LogError("[EraSwitcher] Failed to create root GameObject.");
            return;
        }

        var canvas = root.AddComponent<Canvas>();
        if (canvas == null)
        {
            Object.DestroyImmediate(root);
            Debug.LogError("[EraSwitcher] Failed to add Canvas.");
            return;
        }
        canvas.renderMode = RenderMode.WorldSpace;

        if (!AddOVRRaycaster(root))
        {
            if (root.AddComponent<GraphicRaycaster>() == null)
            {
                Object.DestroyImmediate(root);
                Debug.LogError("[EraSwitcher] Failed to add raycaster.");
                return;
            }
            Debug.LogWarning("[EraSwitcher] Meta OVRRaycaster not found. Added GraphicRaycaster. Run Tools > Temporal Echo > Fix EraSwitcherCanvas for Meta XR.");
        }

        root.AddComponent<WorldSpacePanelPlacer>();

        // Canvas already adds a RectTransform; do not add a second one (can cause NRE on some versions)
        var rect = root.GetComponent<RectTransform>();
        if (rect == null)
            rect = root.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);
        rect.localScale = Vector3.one * WorldScale;
        rect.anchoredPosition = Vector2.zero;

        if (root.AddComponent<EraSwitcherUI>() == null)
        {
            Object.DestroyImmediate(root);
            Debug.LogError("[EraSwitcher] Failed to add EraSwitcherUI. Ensure TemporalEcho.EraSwitcherUI exists.");
            return;
        }

        GameObject panel = CreatePanel(root.transform, rect);
        if (panel == null)
        {
            Object.DestroyImmediate(root);
            Debug.LogError("[EraSwitcher] Failed to create Panel.");
            return;
        }
        CreateTitle(panel.transform, font);
        CreateButton(panel.transform, "Btn_1920s", "1920s", font);
        CreateButton(panel.transform, "Btn_1960s", "1960s", font);
        CreateButton(panel.transform, "Btn_2026", "2026", font);

        string dir = Path.GetDirectoryName(PrefabPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        string prefabPath = PrefabPath;
        var prefabRoot = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);

        if (prefabRoot != null)
        {
            Debug.Log($"[EraSwitcher] Prefab created: {prefabPath}. Drop it into the scene and assign XR Camera on EraSwitcherUI if needed.");
            Selection.activeObject = prefabRoot;
            EditorGUIUtility.PingObject(prefabRoot);
        }
        else
        {
            Debug.LogError("[EraSwitcher] Failed to save prefab at " + prefabPath);
        }
    }

    private static bool AddOVRRaycaster(GameObject canvasGo)
    {
        if (OVRRaycasterType == null) return false;
        return canvasGo.AddComponent(OVRRaycasterType) != null;
    }

    [MenuItem("Tools/Temporal Echo/Fix EraSwitcherCanvas for Meta XR")]
    public static void FixEraSwitcherCanvasForMetaXR()
    {
        if (OVRRaycasterType == null || OVRInputModuleType == null)
        {
            Debug.LogError("[EraSwitcher] Meta XR SDK not found. Ensure OVRRaycaster and OVRInputModule are available (Meta XR Building Blocks).");
            return;
        }

        string path = PrefabPath;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogError("[EraSwitcher] Prefab not found at " + path);
            return;
        }

        string prefabPath = AssetDatabase.GetAssetPath(prefab);
        var roots = PrefabUtility.LoadPrefabContents(prefabPath);
        if (roots == null)
        {
            Debug.LogError("[EraSwitcher] Could not open prefab contents.");
            return;
        }

        bool changed = false;
        var graphicRaycaster = roots.GetComponent<GraphicRaycaster>();
        if (graphicRaycaster != null)
        {
            Object.DestroyImmediate(graphicRaycaster);
            changed = true;
        }
        var xriRaycaster = roots.GetComponent(System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit"));
        if (xriRaycaster != null)
        {
            Object.DestroyImmediate(xriRaycaster);
            changed = true;
        }
        if (roots.GetComponent(OVRRaycasterType) == null)
        {
            roots.AddComponent(OVRRaycasterType);
            changed = true;
        }
        if (roots.GetComponent<WorldSpacePanelPlacer>() == null)
        {
            roots.AddComponent<WorldSpacePanelPlacer>();
            changed = true;
        }

        PrefabUtility.UnloadPrefabContents(roots);
        if (changed)
        {
            AssetDatabase.SaveAssets();
            Debug.Log("[EraSwitcher] Prefab updated for Meta XR: OVRRaycaster, WorldSpacePanelPlacer.");
        }

        EnsureMetaSceneSetup();
    }

    private static void EnsureMetaSceneSetup()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid()) return;

        var eventSystems = Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None);
        if (eventSystems.Length > 1)
            Debug.LogWarning("[EraSwitcher] Multiple EventSystems in scene. Consider keeping only one.");

        foreach (var es in eventSystems)
        {
            var go = es.gameObject;
            var standalone = go.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            if (standalone != null) Object.DestroyImmediate(standalone);
            var xriModule = go.GetComponent(System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule, Unity.XR.Interaction.Toolkit"));
            if (xriModule != null) Object.DestroyImmediate(xriModule);
            if (go.GetComponent(OVRInputModuleType) == null)
                go.AddComponent(OVRInputModuleType);
        }
    }

    private static TMP_FontAsset GetDefaultTMPFont()
    {
        var guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
        foreach (string g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (asset != null) return asset;
        }
        var legacy = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        return legacy;
    }

    private static GameObject CreatePanel(Transform parent, RectTransform canvasRect)
    {
        var go = new GameObject("Panel");
        go.transform.SetParent(parent, false);

        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;

        var image = go.AddComponent<Image>();
        image.color = new Color(0.12f, 0.12f, 0.14f, 0.92f);

        var layout = go.AddComponent<VerticalLayoutGroup>();
        layout.spacing = Spacing;
        layout.padding = new RectOffset(24, 24, 24, 24);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        return go;
    }

    private static void CreateTitle(Transform panel, TMP_FontAsset font)
    {
        var go = new GameObject("Title");
        go.transform.SetParent(panel, false);

        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(CanvasWidth - 48, TitleHeight);

        var layout = go.AddComponent<LayoutElement>();
        layout.preferredHeight = TitleHeight;
        layout.flexibleWidth = 1;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (tmp != null)
        {
            tmp.text = "Choose Era";
            tmp.fontSize = 32;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            if (font != null) tmp.font = font;
        }
        else
        {
            var legacy = go.AddComponent<UnityEngine.UI.Text>();
            if (legacy != null) { legacy.text = "Choose Era"; legacy.fontSize = 32; legacy.alignment = TextAnchor.MiddleCenter; legacy.color = Color.white; }
        }
    }

    private static void CreateButton(Transform panel, string objectName, string label, TMP_FontAsset font)
    {
        var go = new GameObject(objectName);
        go.transform.SetParent(panel, false);

        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);

        var layout = go.AddComponent<LayoutElement>();
        layout.preferredWidth = ButtonWidth;
        layout.preferredHeight = ButtonHeight;

        var image = go.AddComponent<Image>();
        image.color = new Color(0.22f, 0.22f, 0.28f, 1f);

        var button = go.AddComponent<Button>();
        var colors = button.colors;
        colors.highlightedColor = new Color(0.35f, 0.35f, 0.4f, 1f);
        colors.pressedColor = new Color(0.18f, 0.18f, 0.22f, 1f);
        button.colors = colors;

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);

        var textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;

        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        if (tmp != null)
        {
            tmp.text = label;
            tmp.fontSize = 42;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            if (font != null) tmp.font = font;
        }
        else
        {
            var legacy = textGo.AddComponent<UnityEngine.UI.Text>();
            if (legacy != null) { legacy.text = label; legacy.fontSize = 28; legacy.alignment = TextAnchor.MiddleCenter; legacy.color = Color.white; }
        }

        button.targetGraphic = image;
    }
}
#endif
