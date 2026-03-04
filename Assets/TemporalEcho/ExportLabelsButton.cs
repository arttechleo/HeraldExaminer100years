using UnityEngine;

/// <summary>
/// Pinch-triggered button that exports DetectionLabelCollector labels to file and shows a toast (e.g. on EraSwitchPanel).
/// </summary>
[RequireComponent(typeof(Collider))]
public class ExportLabelsButton : MonoBehaviour
{
    [Tooltip("If null, toast is not shown.")]
    [SerializeField] private EraSwitchPanel panelForToast;

    private void Awake()
    {
        if (panelForToast == null)
            panelForToast = FindAnyObjectByType<EraSwitchPanel>();
    }

    public void SetPanelForToast(EraSwitchPanel panel) => panelForToast = panel;

    public void Trigger()
    {
        string path = DetectionLabelCollector.ExportToFile();
        if (panelForToast != null)
            panelForToast.ShowToast("Exported: " + path, 2f);
    }
}
