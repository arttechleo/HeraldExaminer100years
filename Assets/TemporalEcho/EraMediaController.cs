using System;
using UnityEngine;
using UnityEngine.Video;

namespace TemporalEcho
{
    /// <summary>
    /// Single canonical path: plays video or audio per era. Play once then fire OnEraMediaFinished for auto-advance.
    /// Stops all other VideoPlayers before playing to prevent two videos.
    /// </summary>
    public class EraMediaController : MonoBehaviour
    {
        [SerializeField] private EraDatabase eraDatabase;
        [Tooltip("Anchor where video screen is (Screen anchor).")]
        [SerializeField] private Transform screenAnchor;
        [Tooltip("EraVideoScreen on screen anchor. If null, will find in children.")]
        [SerializeField] private EraVideoScreen videoScreen;
        [Tooltip("Canonical audio source for soundtrack. If null, uses PlayClipAtPoint.")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private float soundtrackVolume = 1f;

        private EraId _lastEraId = EraId.E1920s;
        private AudioClip _playingClip;
        private bool _audioFinishedFired;
        private VideoPlayer _canonicalVideoPlayer;

        /// <summary>Fired when current era's media (audio or video) has finished playing once. Orchestrator advances to next era.</summary>
        public event Action OnEraMediaFinished;

        /// <summary>Current media state for UI debug.</summary>
        public enum MediaState { Idle, AudioPlaying, VideoPlaying }
        public MediaState CurrentState { get; private set; } = MediaState.Idle;

        private void Awake()
        {
            if (eraDatabase == null) eraDatabase = FindAnyObjectByType<EraDatabase>();
            if (videoScreen == null && screenAnchor != null)
                videoScreen = screenAnchor.GetComponentInChildren<EraVideoScreen>(true);
            if (videoScreen != null)
                _canonicalVideoPlayer = videoScreen.GetComponent<VideoPlayer>();
        }

        private void OnEnable()
        {
            var ui = FindAnyObjectByType<EraSwitcherUI>();
            if (ui != null) ui.OnEraSelected += OnEraSelected;
            if (_canonicalVideoPlayer != null)
                _canonicalVideoPlayer.loopPointReached += OnVideoLoopPointReached;
        }

        private void OnDisable()
        {
            var ui = FindAnyObjectByType<EraSwitcherUI>();
            if (ui != null) ui.OnEraSelected -= OnEraSelected;
            if (_canonicalVideoPlayer != null)
                _canonicalVideoPlayer.loopPointReached -= OnVideoLoopPointReached;
        }

        private void OnEraSelected(TemporalEra era)
        {
            ApplyMediaForEra(era);
        }

        /// <summary>Apply media for the given era (soundtrack or video). Play once; OnEraMediaFinished when done.</summary>
        public void ApplyMediaForEra(TemporalEra era)
        {
            ApplyEra(ToEraId(era));
        }

        private void Update()
        {
            if (CurrentState == MediaState.AudioPlaying && audioSource != null && _playingClip != null && !audioSource.isPlaying)
            {
                _playingClip = null;
                CurrentState = MediaState.Idle;
                if (!_audioFinishedFired)
                {
                    _audioFinishedFired = true;
                    OnEraMediaFinished?.Invoke();
                }
            }
        }

        private static EraId ToEraId(TemporalEra era)
        {
            switch (era)
            {
                case TemporalEra.Era1920s: return EraId.E1920s;
                case TemporalEra.Era1960s: return EraId.E1960s;
                case TemporalEra.EraToday: return EraId.E2026;
                default: return EraId.E2026;
            }
        }

        private void ApplyEra(EraId era)
        {
            _lastEraId = era;
            _audioFinishedFired = false;
            if (eraDatabase == null) return;
            var def = eraDatabase.Get(era);

            if (def.videoClip != null)
            {
                StopAllVideosExceptCanonical();
                StopAudio();
                if (videoScreen != null) videoScreen.gameObject.SetActive(true);
                CurrentState = MediaState.VideoPlaying;
                PlayVideo(def.videoClip);
                Debug.Log($"[TemporalEcho] Media: video clip={def.videoClip.name} (play once)");
            }
            else
            {
                StopVideo();
                CurrentState = def.soundtrack != null ? MediaState.AudioPlaying : MediaState.Idle;
                if (def.soundtrack != null)
                {
                    PlayAudio(def.soundtrack);
                    Debug.Log($"[TemporalEcho] Media: audio clip={def.soundtrack.name} (play once)");
                }
            }
        }

        /// <summary>Stop every VideoPlayer in scene except the canonical one on VideoScreen under ScreenAnchor.</summary>
        public void StopAllVideosExceptCanonical()
        {
            var all = FindObjectsByType<VideoPlayer>(FindObjectsSortMode.None);
            foreach (var vp in all)
            {
                if (vp == null || !vp.enabled) continue;
                if (vp == _canonicalVideoPlayer) continue;
                vp.Stop();
                vp.enabled = false;
            }
        }

        private void OnVideoLoopPointReached(VideoPlayer vp)
        {
            CurrentState = MediaState.Idle;
            OnEraMediaFinished?.Invoke();
        }

        private void StopAudio()
        {
            if (audioSource != null && audioSource.isPlaying)
                audioSource.Stop();
            _playingClip = null;
        }

        private void PlayAudio(AudioClip clip)
        {
            StopVideo();
            _playingClip = clip;
            if (audioSource != null)
            {
                audioSource.clip = clip;
                audioSource.loop = false;
                audioSource.volume = soundtrackVolume;
                audioSource.Play();
            }
            else
            {
                AudioSource.PlayClipAtPoint(clip, Camera.main != null ? Camera.main.transform.position : Vector3.zero, soundtrackVolume);
            }
        }

        private void StopVideo()
        {
            if (videoScreen != null)
                videoScreen.Stop();
        }

        private void PlayVideo(VideoClip clip)
        {
            if (videoScreen == null) return;
            videoScreen.Play(clip, loop: false);
        }
    }
}
