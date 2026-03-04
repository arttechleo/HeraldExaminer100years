using UnityEngine;

[RequireComponent(typeof(Collider))]
public class LabelsPageButton : MonoBehaviour
{
    public bool isNext;
    public MRStatusOverlay overlay;

    private void Awake()
    {
        if (overlay == null)
            overlay = FindAnyObjectByType<MRStatusOverlay>();
    }

    public void Trigger()
    {
        if (overlay != null)
        {
            if (isNext)
                overlay.NextPage();
            else
                overlay.PrevPage();
        }
    }
}
