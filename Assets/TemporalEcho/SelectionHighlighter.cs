using UnityEngine;

/// <summary>
/// Museum-style highlight for the active "Choose a story" object: subtle ring + floating label. Quest-friendly (no heavy shaders).
/// </summary>
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class SelectionHighlighter : MonoBehaviour
{
    [Tooltip("Emission pulse speed.")]
    [SerializeField] private float pulseSpeed = 2f;
    [Tooltip("Emission intensity min/max for pulse.")]
    [SerializeField] private float emissionMin = 0.3f;
    [SerializeField] private float emissionMax = 0.8f;
    [Tooltip("Height above prop root for label (meters).")]
    [SerializeField] private float labelHeight = 0.4f;

    private TextMesh _labelText;
    private GameObject _labelGo;
    private Renderer _ringRenderer;
    private Material _ringMaterial;
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    public void SetLabel(string text)
    {
        if (_labelText != null)
            _labelText.text = text ?? "";
    }

    private void Awake()
    {
        BuildRing();
        BuildLabel();
    }

    private void BuildRing()
    {
        var mf = GetComponent<MeshFilter>();
        var mr = GetComponent<MeshRenderer>();
        if (mf != null && mr != null)
        {
            var mesh = new Mesh();
            float r = 0.35f;
            mesh.vertices = new Vector3[]
            {
                new Vector3(-r, 0.01f, -r), new Vector3(r, 0.01f, -r), new Vector3(r, 0.01f, r), new Vector3(-r, 0.01f, r)
            };
            mesh.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            mf.sharedMesh = mesh;
            _ringRenderer = mr;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Legacy Shaders/Self-Illumin/Diffuse");
            _ringMaterial = shader != null ? new Material(shader) : new Material(Shader.Find("Sprites/Default"));
            if (_ringMaterial != null)
            {
                if (_ringMaterial.HasProperty("_EmissionColor"))
                    _ringMaterial.EnableKeyword("_EMISSION");
                else if (_ringMaterial.HasProperty("_EmissiveColor"))
                    _ringMaterial.EnableKeyword("_EMISSION");
                mr.sharedMaterial = _ringMaterial;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
        }
    }

    private void BuildLabel()
    {
        _labelGo = new GameObject("SelectionLabel");
        _labelGo.transform.SetParent(transform, false);
        _labelGo.transform.localPosition = new Vector3(0f, labelHeight, 0f);
        _labelGo.transform.localRotation = Quaternion.identity;
        _labelGo.transform.localScale = Vector3.one * 0.5f;
        _labelText = _labelGo.AddComponent<TextMesh>();
        _labelText.anchor = TextAnchor.MiddleCenter;
        _labelText.alignment = TextAlignment.Center;
        _labelText.fontSize = 24;
        _labelText.characterSize = 0.02f;
        _labelText.text = "";
    }

    private void Update()
    {
        if (_ringMaterial != null)
        {
            float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
            float intensity = Mathf.Lerp(emissionMin, emissionMax, t);
            var color = new Color(0.9f, 0.85f, 0.5f) * intensity;
            if (_ringMaterial.HasProperty(EmissionColor))
                _ringMaterial.SetColor(EmissionColor, color);
            else if (_ringMaterial.HasProperty("_EmissiveColor"))
                _ringMaterial.SetColor("_EmissiveColor", color);
            else if (_ringMaterial.HasProperty("_Color"))
                _ringMaterial.SetColor("_Color", color);
        }
        if (_labelGo != null)
        {
            var cam = Camera.main;
            if (cam == null) cam = UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (cam != null)
                _labelGo.transform.rotation = Quaternion.LookRotation(_labelGo.transform.position - cam.transform.position);
        }
    }

    private void OnDestroy()
    {
        if (_ringMaterial != null)
            Destroy(_ringMaterial);
    }
}
