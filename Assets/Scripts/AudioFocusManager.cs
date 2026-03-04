using UnityEngine;

/// <summary>
/// Ensures only one audio source plays at a time. RequestPlay stops any current playback then plays the requested source.
/// Auto-added next to PersistentDiscoveryManager if missing.
/// </summary>
public class AudioFocusManager : MonoBehaviour
{
    private AudioSource _activeSource;
    private AudioClip _activeClip;

    public string ActiveAudioSourceName => _activeSource != null ? _activeSource.gameObject.name : "none";
    public string ActiveClipName => _activeClip != null ? _activeClip.name : "none";

    /// <summary>Stops any currently playing source, then assigns clip and plays the requested source. Returns true if a previous source was stopped.</summary>
    public bool RequestPlay(AudioSource source, AudioClip clip)
    {
        if (source == null) return false;

        bool stoppedPrevious = false;
        if (_activeSource != null && _activeSource != source && _activeSource.isPlaying)
        {
            _activeSource.Stop();
            stoppedPrevious = true;
        }

        _activeSource = source;
        _activeClip = clip;
        source.clip = clip;
        source.Play();
        return stoppedPrevious;
    }

    /// <summary>Stops the currently tracked active source if any.</summary>
    public void StopActiveIfAny()
    {
        if (_activeSource != null && _activeSource.isPlaying)
            _activeSource.Stop();
        _activeSource = null;
        _activeClip = null;
    }

    private void Update()
    {
        if (_activeSource != null && !_activeSource.isPlaying)
        {
            _activeSource = null;
            _activeClip = null;
        }
    }
}
