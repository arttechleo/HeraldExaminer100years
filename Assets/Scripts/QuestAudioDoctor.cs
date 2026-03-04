using UnityEngine;

/// <summary>
/// Global audio sanity check for Quest. Creates test beep, enforces single listener,
/// reports audio engine state. Auto-added to PersistentDiscoveryManager GO.
/// Startup beep is OFF by default; enable via enableStartupBeepTest for diagnostics.
/// </summary>
public class QuestAudioDoctor : MonoBehaviour
{
    [Tooltip("If true (and DEVELOPMENT_BUILD), play a test beep 1s after Start. Default OFF for production.")]
    [SerializeField] private bool enableStartupBeepTest;

    private GameObject _testSourceGo;
    private AudioSource _testSource;
    private static AudioClip _beepClip;
    private float _playTestAt = -1f;

#if DEVELOPMENT_BUILD
    private float _lastDspTime;
    private bool _audioOutputFailedFlag;
    private string _overlayText = "";
#endif

    private void Start()
    {
        ApplyQuestAudioFixes();
        CreateGlobalTestSource();
#if DEVELOPMENT_BUILD
        if (enableStartupBeepTest)
            _playTestAt = Time.time + 1f;
#endif

#if DEVELOPMENT_BUILD
        _lastDspTime = (float)AudioSettings.dspTime;
#endif
    }

    private void Update()
    {
        if (_playTestAt > 0 && Time.time >= _playTestAt)
        {
            _playTestAt = -1f;
            PlayTestBeepNow();
        }

#if DEVELOPMENT_BUILD
        float dsp = (float)AudioSettings.dspTime;
        bool dspAdvances = dsp > _lastDspTime;
        _lastDspTime = dsp;

        var listener = Object.FindFirstObjectByType<AudioListener>();
        bool hasListener = listener != null && listener.enabled;
        bool testPlaying = _testSource != null && _testSource.isPlaying;

        _audioOutputFailedFlag = testPlaying && dspAdvances && hasListener;

        string speakerMode = "N/A";
        try { speakerMode = AudioSettings.GetConfiguration().speakerMode.ToString(); } catch { }

        _overlayText = $"sampleRate={AudioSettings.outputSampleRate} dspTime={dsp:F1} speakerMode={speakerMode}\n" +
                       $"AudioListener.volume={AudioListener.volume} pause={AudioListener.pause}\n" +
                       $"timeScale={Time.timeScale} isFocused={Application.isFocused}\n" +
                       $"TestSource playing={testPlaying} dspAdvances={dspAdvances} listener={hasListener}\n" +
                       (_audioOutputFailedFlag ? "Code OK. If still silent: AUDIO OUTPUT FAILED.\n" +
                        "Check: Quest system volume, app focus, Bluetooth/cast routing\n" : "");
#endif

#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.Space))
            PlayTestBeepNow();
#endif
    }

#if DEVELOPMENT_BUILD
    private void OnGUI()
    {
        if (!Application.isPlaying) return;
        float y = 200f;
        GUI.Box(new Rect(10, y, 600, 140), "QuestAudioDoctor");
        y += 25f;
        GUI.Label(new Rect(15, y, 590, 100), _overlayText);
    }
#endif

    private void ApplyQuestAudioFixes()
    {
        if (Time.timeScale <= 0f)
            Time.timeScale = 1f;

        AudioListener.volume = 1f;
        AudioListener.pause = false;

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            var mobileType = typeof(AudioSettings).GetNestedType("Mobile", System.Reflection.BindingFlags.Public);
            if (mobileType != null)
            {
                var prop = mobileType.GetProperty("stopAudioOutputOnMute", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (prop != null && prop.CanWrite)
                    prop.SetValue(null, false);
            }
        }
        catch { }
#endif

        QuestAudioHelper.EnsureSingleListenerOnXRCamera();
    }

    private void CreateGlobalTestSource()
    {
        _testSourceGo = new GameObject("__AudioDoctorSource");
        _testSource = _testSourceGo.AddComponent<AudioSource>();
        _testSource.spatialBlend = 0f;
        _testSource.volume = 1f;
        _testSource.outputAudioMixerGroup = null;
        _testSource.ignoreListenerPause = false;
        _testSource.playOnAwake = false;
        _testSource.clip = EnsureBeepClip();
    }

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

    public void PlayTestBeepNow()
    {
        if (_testSource == null) return;
        _testSource.outputAudioMixerGroup = null;
        _testSource.Stop();
        _testSource.clip = EnsureBeepClip();
        _testSource.Play();
    }
}
