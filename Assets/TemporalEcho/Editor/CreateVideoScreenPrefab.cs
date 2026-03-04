#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;
using TemporalEcho;

public static class CreateVideoScreenPrefab
{
    private const string PrefabPath = "Assets/TemporalEcho/Prefabs/VideoScreen.prefab";

    [MenuItem("Tools/Temporal Echo/Create VideoScreen Prefab")]
    public static void CreatePrefab()
    {
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "VideoScreen";
        quad.transform.localPosition = Vector3.zero;
        quad.transform.localRotation = Quaternion.identity;
        quad.transform.localScale = Vector3.one;

        var renderer = quad.GetComponent<Renderer>();
        var shader = Shader.Find("Unlit/Texture") ?? Shader.Find("Universal Render Pipeline/Unlit");
        if (shader != null)
        {
            var mat = new Material(shader);
            mat.mainTexture = Texture2D.blackTexture;
            renderer.sharedMaterial = mat;
        }

        var videoPlayer = quad.AddComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = true;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;

        quad.AddComponent<EraVideoScreen>();

        string dir = Path.GetDirectoryName(PrefabPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var prefabRoot = PrefabUtility.SaveAsPrefabAsset(quad, PrefabPath);
        Object.DestroyImmediate(quad);

        if (prefabRoot != null)
        {
            Debug.Log($"[EraSwitcher] VideoScreen prefab created: {PrefabPath}. Assign to screen anchor for 2026 era video.");
            Selection.activeObject = prefabRoot;
            EditorGUIUtility.PingObject(prefabRoot);
        }
    }

    private const string MaterialPath = "Assets/TemporalEcho/Materials/VideoScreenMat.mat";
    private const string VideoMatPath = "Assets/TemporalEcho/Prefabs/VideoMat.mat";

    [MenuItem("Tools/Temporal Echo/Fix VideoScreen Prefab Material")]
    public static void FixVideoScreenPrefabMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(VideoMatPath);
        if (mat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Unlit/Texture")
                ?? Shader.Find("Unlit/Color");
            if (shader == null)
            {
                Debug.LogError("[EraSwitcher] No valid shader found. VideoScreen will stay pink.");
                return;
            }
            var matDir = Path.GetDirectoryName(MaterialPath);
            if (!string.IsNullOrEmpty(matDir) && !Directory.Exists(matDir))
                Directory.CreateDirectory(matDir);
            mat = new Material(shader);
            mat.name = "VideoScreenMat";
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", Texture2D.blackTexture);
            else if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", Texture2D.blackTexture);
            AssetDatabase.CreateAsset(mat, MaterialPath);
            Debug.Log("[EraSwitcher] Created VideoScreenMat at " + MaterialPath);
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning("[EraSwitcher] VideoScreen prefab not found at " + PrefabPath + ". Create it first.");
            return;
        }
        string path = AssetDatabase.GetAssetPath(prefab);
        var root = PrefabUtility.LoadPrefabContents(path);
        if (root == null) return;

        var renderer = root.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = mat;
        }

        var vp = root.GetComponent<VideoPlayer>();
        if (vp != null)
        {
            vp.renderMode = VideoRenderMode.RenderTexture;
            vp.playOnAwake = false;
            vp.targetTexture = null;
        }

        var evs = root.GetComponent<EraVideoScreen>();
        if (evs != null)
        {
            var so = new SerializedObject(evs);
            so.FindProperty("renderTextureWidth").intValue = 1920;
            so.FindProperty("renderTextureHeight").intValue = 1080;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
        AssetDatabase.SaveAssets();
        Debug.Log("[EraSwitcher] VideoScreen prefab fixed: assigned VideoScreenMat (URP Unlit). Pink should be gone.");
    }

    [MenuItem("Tools/Temporal Echo/Ensure VideoScreen Ready for Video")]
    public static void EnsureVideoScreenReady()
    {
        FixVideoScreenPrefabMaterial();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab != null)
        {
            var instances = Object.FindObjectsByType<EraVideoScreen>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var evs in instances)
            {
                if (evs.GetComponent<VideoPlayer>() == null) evs.gameObject.AddComponent<VideoPlayer>();
                if (evs.GetComponent<Renderer>() == null) { Debug.LogWarning("[EraSwitcher] VideoScreen " + evs.name + " has no Renderer - add a Quad."); }
                UnityEditor.EditorUtility.SetDirty(evs);
            }
            Debug.Log("[EraSwitcher] VideoScreen components verified. Run Fix VideoScreen Prefab Material if video still does not render.");
        }
    }
}
#endif
