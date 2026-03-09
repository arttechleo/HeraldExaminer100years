using UnityEngine;

/// <summary>
/// At Start, optionally sets Show Bounding Boxes = false on all ObjectDetectionVisualizerV2
/// instances so boxes start hidden. Does NOT disable the visualizer component — it stays
/// enabled so detections continue and the checkbox/button can toggle visibility at any time.
/// </summary>
public class DisableDetectionVisualizerOnStart : MonoBehaviour
{
    [Tooltip("When on (default), sets Show Bounding Boxes = false on all ObjectDetectionVisualizerV2 at Start. Boxes start hidden; user can tick the checkbox or press the optional toggle button to show for demos. When off, leaves each visualizer's Show Bounding Boxes as set in the Inspector.")]
    [SerializeField] private bool hideBoxesOnStart = true;

    private void Start()
    {
        if (!hideBoxesOnStart) return;

        var visualizers = FindObjectsByType<ObjectDetectionVisualizerV2>(FindObjectsSortMode.None);
        foreach (var v in visualizers)
        {
            if (v != null)
                v.SetShowBoundingBoxes(false);
        }
    }
}
