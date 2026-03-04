#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TemporalEcho;

public static class EraPreviewSetupMenu
{
    private const string DatabasePath = "Assets/TemporalEcho/EraDatabase.asset";
    private const string AnchorNameScreen = "ScreenAnchor";
    private const string AnchorNameBook = "BookAnchor";
    private const string AnchorNameHuman = "HumanAnchor";
    private const string ApplierObjectName = "Era Preview";

    [MenuItem("Tools/Temporal Echo/Setup Era Preview (Create Database + Scene)")]
    public static void SetupEraPreview()
    {
        EraDatabase db = EnsureEraDatabase();
        if (db == null)
        {
            Debug.LogError("[EraPreview] Could not create or load Era Database.");
            return;
        }

        CreateSceneSetup(db);
        Debug.Log("[EraPreview] Setup complete. Select 'Era Preview' in the hierarchy, assign prefabs in the Era Database, then click 1920s / 1960s / 2026 in the inspector.");
    }

    [MenuItem("Tools/Temporal Echo/Create Era Database Asset Only")]
    public static void CreateEraDatabaseOnly()
    {
        var db = EnsureEraDatabase();
        if (db != null)
        {
            Selection.activeObject = db;
            EditorGUIUtility.PingObject(db);
            Debug.Log("[EraPreview] Era Database created/updated at " + DatabasePath);
        }
    }

    [MenuItem("Tools/Temporal Echo/Create Era Preview in Scene Only")]
    public static void CreateSceneSetupOnly()
    {
        var db = AssetDatabase.LoadAssetAtPath<EraDatabase>(DatabasePath);
        if (db == null)
        {
            Debug.LogWarning("[EraPreview] No Era Database at " + DatabasePath + ". Run 'Create Era Database Asset Only' first.");
            return;
        }
        CreateSceneSetup(db);
    }

    private static EraDatabase EnsureEraDatabase()
    {
        var existing = AssetDatabase.LoadAssetAtPath<EraDatabase>(DatabasePath);
        if (existing != null)
            return existing;

        string dir = Path.GetDirectoryName(DatabasePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var db = ScriptableObject.CreateInstance<EraDatabase>();
        db.era1920s = DefaultEraDefinition("1920s");
        db.era1960s = DefaultEraDefinition("1960s");
        db.era2026 = DefaultEraDefinition("2026");

        AssetDatabase.CreateAsset(db, DatabasePath);
        AssetDatabase.SaveAssets();
        return db;
    }

    private static EraDefinition DefaultEraDefinition(string label)
    {
        return new EraDefinition
        {
            label = label,
            soundtrack = null,
            rules = new[]
            {
                new EraRule { subject = DetectedSubject.Screen, localScale = Vector3.one },
                new EraRule { subject = DetectedSubject.Book, localScale = Vector3.one },
                new EraRule { subject = DetectedSubject.Human, localScale = Vector3.one }
            }
        };
    }

    private static void CreateSceneSetup(EraDatabase database)
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            Debug.LogError("[EraPreview] No active scene. Open a scene first.");
            return;
        }

        Transform screenAnchor = FindOrCreate(AnchorNameScreen, Vector3.zero);
        Transform bookAnchor = FindOrCreate(AnchorNameBook, new Vector3(0.5f, 0f, 0f));
        Transform humanAnchor = FindOrCreate(AnchorNameHuman, new Vector3(1f, 0f, 0f));

        GameObject applierGo = GameObject.Find(ApplierObjectName);
        if (applierGo == null)
        {
            applierGo = new GameObject(ApplierObjectName);
            Undo.RegisterCreatedObjectUndo(applierGo, "Create Era Preview");
        }

        var applier = applierGo.GetComponent<EraPreviewApplier>();
        if (applier == null)
            applier = applierGo.AddComponent<EraPreviewApplier>();

        Undo.RecordObject(applier, "Assign Era Preview");
        applier.database = database;
        applier.screenAnchor = screenAnchor;
        applier.bookAnchor = bookAnchor;
        applier.humanAnchor = humanAnchor;
        applier.activeEra = EraId.E1920s;

        EditorUtility.SetDirty(applier);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = applierGo;
    }

    private static Transform FindOrCreate(string name, Vector3 position)
    {
        var existing = GameObject.Find(name);
        if (existing != null)
            return existing.transform;

        var go = new GameObject(name);
        go.transform.position = position;
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        return go.transform;
    }
}
#endif
