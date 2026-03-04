using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ToggleLabelsViewButton : MonoBehaviour
{
    public MRStatusOverlay overlay;

    private void Awake()
    {
        if (overlay == null)
            overlay = FindAnyObjectByType<MRStatusOverlay>();
    }

    public void Trigger()
    {
        if (overlay != null)
            overlay.SetLabelsView(!overlay.LabelsViewMode);
    }
}
