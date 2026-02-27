using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Runs on Unity load to validate project bootstrap: Unity version, required packages,
/// Android build settings, and required assets. Shows a dialog with fix steps if something is wrong.
/// </summary>
[InitializeOnLoadMethod]
public static class ProjectBootstrapCheck
{
    private const string ExpectedUnityVersion = "6000.2.15f1";
    private const int ExpectedAndroidMinSdk = 32;
    private const int ExpectedScriptingBackendAndroid = 1; // IL2CPP
    private const int ExpectedAndroidArchitectures = 2;    // ARM64

    private static readonly string[] RequiredPackageIds = new[]
    {
        "com.meta.xr.mrutilitykit",
        "com.unity.ai.inference",
        "com.unity.xr.openxr",
        "com.unity.xr.meta-openxr",
        "com.unity.render-pipelines.universal"
    };

    private static readonly string[] RequiredScenePaths = new[]
    {
        "Assets/Scenes/AIBuildingBlocks.unity"
    };

    private static readonly string[] RequiredScriptPaths = new[]
    {
        "Assets/Scripts/DetectionPrefabReplacer.cs",
        "Assets/Scripts/ObjectDetectionVisualizerV2.cs"
    };

    static ProjectBootstrapCheck()
    {
        EditorApplication.delayCall += RunCheck;
    }

    private static void RunCheck()
    {
        if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
            PerformBootstrapCheck();
    }

    [MenuItem("Tools/Project Bootstrap/Run Bootstrap Check")]
    public static void PerformBootstrapCheck()
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        CheckUnityVersion(warnings);
        CheckRequiredPackages(errors);
        CheckAndroidBuildSettings(errors, warnings);
        CheckRequiredAssets(errors);

        if (errors.Count > 0 || warnings.Count > 0)
        {
            string message = "";
            if (errors.Count > 0)
                message += "Errors (fix before building):\n" + string.Join("\n", errors) + "\n\n";
            if (warnings.Count > 0)
                message += "Warnings:\n" + string.Join("\n", warnings) + "\n\n";
            message += GetFixInstructions(errors.Count > 0);
            EditorUtility.DisplayDialog("Project Bootstrap Check", message, "OK");
            return;
        }
    }

    private static void CheckUnityVersion(List<string> warnings)
    {
        string path = Path.Combine(Application.dataPath, "..", "ProjectSettings", "ProjectVersion.txt");
        if (!File.Exists(path))
        {
            warnings.Add("- ProjectVersion.txt not found. Cannot verify Unity version.");
            return;
        }
        string text = File.ReadAllText(path);
        if (!text.Contains(ExpectedUnityVersion))
            warnings.Add($"- Unity version mismatch. Expected {ExpectedUnityVersion}. Install from Unity Hub: https://unity.com/download. Current: " + (text.Trim().Length > 0 ? text.Split('\n')[0].Trim() : "unknown"));
    }

    private static void CheckRequiredPackages(List<string> errors)
    {
        string manifestPath = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");
        string lockPath = Path.Combine(Application.dataPath, "..", "Packages", "packages-lock.json");
        if (!File.Exists(manifestPath))
        {
            errors.Add("- Packages/manifest.json is missing. Restore from git or recreate.");
            return;
        }
        if (!File.Exists(lockPath))
            errors.Add("- Packages/packages-lock.json is missing. Open project in Unity to regenerate, then commit it.");

        string manifestJson = File.ReadAllText(manifestPath);
        foreach (string packageId in RequiredPackageIds)
        {
            if (!manifestJson.Contains(packageId))
                errors.Add($"- Required package not in manifest: {packageId}. Add via Window > Package Manager.");
        }
    }

    private static void CheckAndroidBuildSettings(List<string> errors, List<string> warnings)
    {
        string path = Path.Combine(Application.dataPath, "..", "ProjectSettings", "ProjectSettings.asset");
        if (!File.Exists(path)) return;
        string text = File.ReadAllText(path);
        if (!text.Contains("scriptingBackend"))
            warnings.Add("- Could not parse ProjectSettings for scripting backend.");
        if (text.Contains("Android: 0")) // Mono
            warnings.Add("- Android scripting backend should be IL2CPP for Quest. Edit > Project Settings > Player > Android > Other Settings > Scripting Backend = IL2CPP.");
        if (text.Contains("AndroidTargetArchitectures: 1") && !text.Contains("AndroidTargetArchitectures: 2"))
            warnings.Add("- Android should target ARM64 for Quest. Edit > Project Settings > Player > Android > Target Architectures: ARM64.");
        int minSdk = GetAndroidMinSdk(text);
        if (minSdk > 0 && minSdk < ExpectedAndroidMinSdk)
            warnings.Add($"- Android Min SDK should be at least {ExpectedAndroidMinSdk} for Quest. Edit > Project Settings > Player > Android.");
    }

    private static int GetAndroidMinSdk(string projectSettingsText)
    {
        const string key = "AndroidMinSdkVersion: ";
        int i = projectSettingsText.IndexOf(key);
        if (i < 0) return -1;
        i += key.Length;
        int end = projectSettingsText.IndexOf('\n', i);
        string val = end > i ? projectSettingsText.Substring(i, end - i).Trim() : projectSettingsText.Substring(i).Trim();
        return int.TryParse(val, out int v) ? v : -1;
    }

    private static void CheckRequiredAssets(List<string> errors)
    {
        foreach (string scenePath in RequiredScenePaths)
        {
            if (!File.Exists(Path.Combine(Application.dataPath, "..", scenePath)))
                errors.Add($"- Missing required scene: {scenePath}");
        }
        foreach (string scriptPath in RequiredScriptPaths)
        {
            if (!File.Exists(Path.Combine(Application.dataPath, "..", scriptPath)))
                errors.Add($"- Missing required script: {scriptPath}");
        }
    }

    private static string GetFixInstructions(bool hasErrors)
    {
        string linkHub = "https://unity.com/download";
        string linkPackages = "Window > Package Manager in Unity";
        string linkPlayer = "Edit > Project Settings > Player > Android";
        return "Steps to fix:\n" +
               "• Unity version: Install " + ExpectedUnityVersion + " via Unity Hub: " + linkHub + "\n" +
               "• Packages: Ensure Packages/manifest.json and packages-lock.json are committed. Restore: open project in Unity; add packages via " + linkPackages + ".\n" +
               "• Android (Quest): " + linkPlayer + " — Scripting Backend = IL2CPP, Target Architectures = ARM64, Minimum API level >= " + ExpectedAndroidMinSdk + ".\n" +
               "• Scenes/Scripts: Restore from git (e.g. git checkout, git lfs pull).";
    }
}
