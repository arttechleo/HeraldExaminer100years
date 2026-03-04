using UnityEngine;

/// <summary>
/// Shared XR camera and AudioListener selection for Quest (Building Blocks rig).
/// </summary>
public static class QuestAudioHelper
{
    public static Transform FindXRCameraTransform()
    {
        var all = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        foreach (var t in all)
        {
            if (t.name == "CenterEyeAnchor")
            {
                var cam = t.GetComponent<Camera>();
                if (cam != null) return t;
                cam = t.GetComponentInChildren<Camera>();
                if (cam != null) return cam.transform;
                return t;
            }
        }
        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            if (cam.enabled && cam.transform.name.Contains("CenterEye"))
                return cam.transform;
        }
        var main = Camera.main;
        return main != null ? main.transform : null;
    }

    public static void EnsureSingleListenerOnXRCamera()
    {
        var xrCam = FindXRCameraTransform();
        var listeners = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);

        AudioListener preferred = null;
        foreach (var l in listeners)
        {
            if (xrCam != null && (l.transform == xrCam || IsDescendantOf(l.transform, xrCam)))
            {
                preferred = l;
                break;
            }
        }
        if (preferred == null && xrCam != null)
            preferred = xrCam.gameObject.AddComponent<AudioListener>();
        if (preferred == null && listeners.Length > 0)
            preferred = listeners[0];

        foreach (var l in listeners)
        {
            if (l != preferred) l.enabled = false;
        }
        if (preferred != null) preferred.enabled = true;

        AudioListener.volume = 1f;
        AudioListener.pause = false;
    }

    private static bool IsDescendantOf(Transform child, Transform ancestor)
    {
        var p = child.parent;
        while (p != null) { if (p == ancestor) return true; p = p.parent; }
        return false;
    }
}
