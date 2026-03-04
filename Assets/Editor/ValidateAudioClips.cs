#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class ValidateAudioClips
{
    [MenuItem("Tools/Temporal Echo/Validate Audio Clips")]
    public static void Validate()
    {
        var manager = Object.FindFirstObjectByType<PersistentDiscoveryManager>();
        if (manager == null)
        {
            EditorUtility.DisplayDialog("Validate Audio Clips", "No PersistentDiscoveryManager in scene. Assign in Inspector or enter Play Mode.", "OK");
            return;
        }

        var t = manager.GetMicrophoneClipsForValidation();
        var clips = new[] { t.screen, t.book, t.desk, t.chair };
        var names = new[] { "Screen", "Book", "Desk", "Chair" };

        var log = new System.Text.StringBuilder();
        if (manager.MicDebugForceBeep)
            log.AppendLine("WARN: Mic debug beep is enabled — clips will not play. Disable via Inspector or use 'Disable Debug Beep' below.");

        for (int i = 0; i < clips.Length; i++)
        {
            var clip = clips[i];
            var name = names[i];
            if (clip == null)
            {
                log.AppendLine($"[{name}] (not assigned) — WARN: {name} clip missing: mic will be silent");
                continue;
            }
            LogClipInfo(clip, name, log);
        }

        Debug.Log("[Validate Audio Clips]\n" + log);
        AudioClipValidatorWindow.Show(manager, clips, names, log.ToString());
    }

    private static void LogClipInfo(AudioClip clip, string label, System.Text.StringBuilder log)
    {
        log.AppendLine($"[{label}] {clip.name}");
        log.AppendLine($"  loadType: {clip.loadType}");
        log.AppendLine($"  frequency: {clip.frequency} channels: {clip.channels}");
        log.AppendLine($"  loadState: {clip.loadState}");

        string path = AssetDatabase.GetAssetPath(clip);
        if (!string.IsNullOrEmpty(path))
        {
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer != null)
            {
                var samples = importer.defaultSampleSettings;
                log.AppendLine($"  compressionFormat: {samples.compressionFormat}");
                log.AppendLine($"  preloadAudioData: {samples.preloadAudioData}");
                log.AppendLine($"  loadType (importer): {samples.loadType}");
                log.AppendLine("  Quest-safe: DecompressOnLoad, Vorbis/ADPCM, Preload On, Force To Mono");
            }
        }
        log.AppendLine();
    }

    public static bool FixImportSettings(AudioClip clip)
    {
        if (clip == null) return false;
        string path = AssetDatabase.GetAssetPath(clip);
        if (string.IsNullOrEmpty(path)) return false;

        var importer = AssetImporter.GetAtPath(path) as AudioImporter;
        if (importer == null) return false;

        var samples = importer.defaultSampleSettings;
        samples.loadType = AudioClipLoadType.DecompressOnLoad;
        samples.preloadAudioData = true;
        samples.compressionFormat = AudioCompressionFormat.Vorbis;
        importer.forceToMono = true;
        importer.defaultSampleSettings = samples;
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
        return true;
    }
}

internal class AudioClipValidatorWindow : EditorWindow
{
    private Vector2 _scroll;
    private string _text;
    private AudioClip[] _clips;
    private string[] _names;
    private PersistentDiscoveryManager _manager;

    public static void Show(PersistentDiscoveryManager manager, AudioClip[] clips, string[] names, string text)
    {
        var w = GetWindow<AudioClipValidatorWindow>("Audio Clip Validation");
        w._manager = manager;
        w._clips = clips;
        w._names = names;
        w._text = text;
        w.minSize = new Vector2(450, 400);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Quest-safe: DecompressOnLoad, Vorbis, Preload On, Force To Mono", EditorStyles.boldLabel);
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        EditorGUILayout.TextArea(_text, GUILayout.ExpandHeight(false));

        if (_manager != null)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Mic debug beep:", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            bool beepOn = _manager.MicDebugForceBeep;
            EditorGUILayout.LabelField($"micDebugForceBeep = {beepOn}");
            if (beepOn && GUILayout.Button("Disable Debug Beep", GUILayout.Width(140)))
            {
                _manager.MicDebugForceBeep = false;
                EditorUtility.SetDirty(_manager);
            }
            EditorGUILayout.EndHorizontal();
        }

        if (_clips != null && _names != null)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Fix Import Settings (per clip):", EditorStyles.boldLabel);
            for (int i = 0; i < _clips.Length; i++)
            {
                var clip = _clips[i];
                var name = _names[i];
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"[{name}] {(clip != null ? clip.name : "(not assigned)")}", GUILayout.Width(250));
                GUI.enabled = clip != null;
                if (GUILayout.Button("Fix", GUILayout.Width(60)))
                {
                    if (ValidateAudioClips.FixImportSettings(clip))
                        EditorUtility.DisplayDialog("Fix Applied", $"Import settings updated for {clip.name}", "OK");
                    else
                        EditorUtility.DisplayDialog("Fix Failed", "Could not modify import settings.", "OK");
                }
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
            }
        }

        EditorGUILayout.EndScrollView();
    }
}
#endif
