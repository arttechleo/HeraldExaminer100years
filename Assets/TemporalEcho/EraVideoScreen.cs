using UnityEngine;
using UnityEngine.Video;

namespace TemporalEcho
{
    /// <summary>
    /// Plays a VideoClip on a quad via RenderTexture. Used for 2026 era video on screen anchor.
    /// Assign videoClip and ensure Quad has Renderer with Unlit/Texture material.
    /// </summary>
    [RequireComponent(typeof(VideoPlayer))]
    [RequireComponent(typeof(Renderer))]
    public class EraVideoScreen : MonoBehaviour
    {
        [SerializeField] private VideoClip videoClip;
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private RenderTexture renderTexture;
        [SerializeField] private int renderTextureWidth = 1920;
        [SerializeField] private int renderTextureHeight = 1080;

        private RenderTexture _rt;
        private Renderer _renderer;

        private void Awake()
        {
            CacheAndEnsure();
        }

        private void OnEnable()
        {
            CacheAndEnsure();
        }

        private void CacheAndEnsure()
        {
            if (_renderer == null) _renderer = GetComponent<Renderer>();
            if (videoPlayer == null) videoPlayer = GetComponent<VideoPlayer>();
            EnsureRenderTarget();
        }

        private void OnDestroy()
        {
            if (_rt != null && _rt != renderTexture)
            {
                _rt.Release();
                _rt = null;
            }
        }

        private void EnsureRenderTarget()
        {
            if (renderTexture != null)
            {
                _rt = renderTexture;
            }
            else if (_rt == null || !_rt.IsCreated())
            {
                _rt = new RenderTexture(renderTextureWidth, renderTextureHeight, 0);
                _rt.Create();
            }
            if (videoPlayer != null)
            {
                videoPlayer.targetTexture = _rt;
                videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            }
            if (_renderer != null)
            {
                EnsureMaterial();
                if (Application.isPlaying && _rt != null)
                    SetMaterialTexture(_renderer.material, _rt);
            }
        }

        private void EnsureMaterial()
        {
            if (_renderer == null) return;
            var mat = _renderer.sharedMaterial;
            if (mat != null && mat.shader != null && mat.shader.isSupported && (mat.HasProperty("_MainTex") || mat.HasProperty("_BaseMap")))
                return;
            if (!Application.isPlaying)
                return;
            var shader = Shader.Find("Unlit/Texture") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (shader == null) return;
            var newMat = new Material(shader);
            SetMaterialTexture(newMat, _rt != null ? _rt : Texture2D.blackTexture);
            _renderer.material = newMat;
        }

        private void SetMaterialTexture(Material mat, Texture tex)
        {
            if (mat == null || tex == null) return;
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        }

        /// <summary>Play the assigned video clip. Stops if null. Set loop = false for play-once (auto-advance when finished).</summary>
        public void Play(VideoClip clip, bool loop = false)
        {
            videoClip = clip;
            if (videoPlayer == null) videoPlayer = GetComponent<VideoPlayer>();
            if (videoPlayer == null) return;
            EnsureRenderTarget();
            if (clip == null)
            {
                videoPlayer.Stop();
                if (_renderer != null && Application.isPlaying)
                    SetMaterialTexture(_renderer.material, _rt != null ? _rt : Texture2D.blackTexture);
                return;
            }
            videoPlayer.clip = clip;
            videoPlayer.isLooping = loop;
            videoPlayer.Play();
            if (_renderer != null && Application.isPlaying && _rt != null)
            {
                if (!_renderer.enabled) _renderer.enabled = true;
                var mat = _renderer.material;
                if (mat != null)
                {
                    SetMaterialTexture(mat, _rt);
                    mat.renderQueue = 3000;
                }
            }
        }

        /// <summary>Stop playback.</summary>
        public void Stop()
        {
            if (videoPlayer != null)
                videoPlayer.Stop();
        }

        /// <summary>Play the configured videoClip.</summary>
        public void PlayAssigned()
        {
            Play(videoClip);
        }
    }
}
