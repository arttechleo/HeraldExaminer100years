using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ToggleRecognitionViewButton : MonoBehaviour
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
            overlay.SetRecognitionView(!overlay.RecognitionViewMode);
    }
}
