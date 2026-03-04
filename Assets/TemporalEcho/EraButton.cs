using UnityEngine;

/// <summary>
/// Attach to a GameObject with a Collider. When PinchRayUIInteractor hits it on pinch-down, Trigger() is called and sets the era on TemporalEraManager.
/// </summary>
[RequireComponent(typeof(Collider))]
public class EraButton : MonoBehaviour
{
    public TemporalEra era;
    [Tooltip("If null, found at runtime.")]
    [SerializeField] private TemporalEraManager eraManager;

    private void Awake()
    {
        if (eraManager == null)
            eraManager = FindAnyObjectByType<TemporalEraManager>();
    }

    public void SetEraManager(TemporalEraManager manager) => eraManager = manager;

    public void Trigger()
    {
        if (eraManager != null)
            eraManager.SetEra(era);
    }
}
