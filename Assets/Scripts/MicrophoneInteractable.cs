using UnityEngine;

/// <summary>
/// Responds to hand pinch raycast by playing the assigned AudioClip.
/// Supports generated beep proof mode and visual flash for Quest audio debugging.
/// </summary>
[RequireComponent(typeof(Collider))]
public class MicrophoneInteractable : MonoBehaviour
{
    /// <summary>When true, Trigger() does nothing. Set true for showcase (no mic interaction).</summary>
    public static bool DISABLE_VIRTUAL_MIC = true;
    [SerializeField] private AudioClip clip;
    [Tooltip("Play generated 440Hz beep instead of assigned clip. Debug only.")]
    [SerializeField] private bool playGeneratedBeepInstead;
    [Tooltip("3D rolloff min distance (meters).")]
    [SerializeField] private float minDistance = 0.2f;
    [Tooltip("3D rolloff max distance (meters).")]
    [SerializeField] private float maxDistance = 15f;

    private AudioSource _source;
    private float _flashEndAt = -1f;
    private Vector3 _originalScale;

    private static AudioClip _beepClip;

    public bool HasClip => clip != null;
    public string AssignedClipName => clip != null ? clip.name : "";
    public bool LastTriggerSuccess { get; private set; }
    public string LastTriggerReason { get; private set; } = "";
    public string ClipName => AssignedClipName;
    public AudioDataLoadState ClipLoadState => clip != null ? clip.loadState : AudioDataLoadState.Unloaded;
    public bool IsPlaying => _source != null && _source.isPlaying;
    public bool PlayGeneratedBeepInstead => playGeneratedBeepInstead;

    public float LastTriggerTime { get; private set; }
    public int TriggerCount { get; private set; }

    /// <summary>When false, Trigger() does nothing (e.g. inactive in "Choose a story" mode).</summary>
    public bool Interactable { get; private set; } = true;

    public void SetInteractable(bool value)
    {
        Interactable = value;
        var c = GetComponent<Collider>();
        if (c != null) c.enabled = value;
    }

#if DEVELOPMENT_BUILD
    public int ListenerCount { get; private set; }
    public string ActiveListenerName { get; private set; } = "";
#endif

    private static AudioClip EnsureBeepClip()
    {
        if (_beepClip != null) return _beepClip;
        int sampleRate = AudioSettings.outputSampleRate;
        if (sampleRate <= 0) sampleRate = 44100;
        int sampleCount = (int)(0.25f * sampleRate);
        _beepClip = AudioClip.Create("Beep440", sampleCount, 1, sampleRate, false);
        float[] data = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
            data[i] = 0.2f * Mathf.Sin(2f * Mathf.PI * 440f * i / sampleRate);
        _beepClip.SetData(data, 0);
        return _beepClip;
    }

    private void Awake()
    {
        _source = GetComponent<AudioSource>();
        if (_source == null)
            _source = gameObject.AddComponent<AudioSource>();

        _originalScale = transform.localScale;
        Enforce3DSpatial();
        _source.volume = 1f;
        _source.mute = false;
        _source.bypassEffects = true;
        _source.bypassListenerEffects = true;
        _source.bypassReverbZones = true;
        _source.loop = false;
        _source.priority = 128;
        _source.dopplerLevel = 0f;
        _source.playOnAwake = false;
        _source.outputAudioMixerGroup = null;
    }

    private void Enforce3DSpatial()
    {
        if (_source == null) return;
        _source.spatialBlend = 1f;
        _source.spatialize = false;
        _source.rolloffMode = AudioRolloffMode.Linear;
        _source.minDistance = minDistance;
        _source.maxDistance = maxDistance;
    }

    private void Update()
    {
        if (_flashEndAt > 0 && Time.time >= _flashEndAt)
        {
            transform.localScale = _originalScale;
            _flashEndAt = -1f;
        }
    }

    /// <summary>
    /// Attempts to play the assigned clip or generated beep. Returns (success, reason).
    /// </summary>
    public (bool success, string reason) Trigger()
    {
        if (DISABLE_VIRTUAL_MIC)
        {
            LastTriggerSuccess = false;
            LastTriggerReason = "Virtual mic disabled";
            return (false, LastTriggerReason);
        }
        if (!Interactable)
        {
            LastTriggerSuccess = false;
            LastTriggerReason = "Not active (choose a story mode)";
            return (false, LastTriggerReason);
        }
        if (_source == null)
        {
            LastTriggerSuccess = false;
            LastTriggerReason = "AudioSource missing";
            return (false, "AudioSource missing");
        }

        QuestAudioHelper.EnsureSingleListenerOnXRCamera();
        UpdateListenerTelemetry();

        LastTriggerTime = Time.time;
        TriggerCount++;
        _flashEndAt = Time.time + 0.25f;
        transform.localScale = _originalScale * 1.2f;

        _source.outputAudioMixerGroup = null;
        Enforce3DSpatial();

        var focus = Object.FindAnyObjectByType<AudioFocusManager>();
        if (focus == null)
        {
            LastTriggerSuccess = false;
            LastTriggerReason = "AudioFocusManager missing";
            return (false, "AudioFocusManager missing");
        }

        if (playGeneratedBeepInstead)
        {
            var beep = EnsureBeepClip();
            bool stoppedPrevious = focus.RequestPlay(_source, beep);
            LastTriggerSuccess = true;
            LastTriggerReason = stoppedPrevious ? "Played BEEP (debug); Stopped previous" : "Played BEEP (debug)";
            return (true, LastTriggerReason);
        }

        if (clip == null)
        {
            LastTriggerSuccess = false;
            LastTriggerReason = "Clip is null";
            return (false, "Clip is null");
        }

        if (clip.loadState == AudioDataLoadState.Unloaded)
            clip.LoadAudioData();

        bool stoppedPrev = focus.RequestPlay(_source, clip);
        LastTriggerSuccess = true;
        LastTriggerReason = stoppedPrev ? "Played CLIP: " + clip.name + "; Stopped previous" : "Played CLIP: " + clip.name;
        return (true, LastTriggerReason);
    }

    private void UpdateListenerTelemetry()
    {
#if DEVELOPMENT_BUILD
        var listeners = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
        ListenerCount = listeners.Length;
        foreach (var l in listeners)
        {
            if (l.enabled)
            {
                ActiveListenerName = l.gameObject.name;
                return;
            }
        }
        ActiveListenerName = "none";
#endif
    }

    public void SetClip(AudioClip audioClip)
    {
        clip = audioClip;
    }

    public void SetPlayGeneratedBeepInstead(bool value)
    {
        playGeneratedBeepInstead = value;
    }
}
