using UnityEngine;

namespace TemporalEcho
{
    [DefaultExecutionOrder(-1000)]
    public class ShowcaseSanitizer : MonoBehaviour
    {
        private void Awake()
        {
            var orchestrators = FindObjectsByType<TemporalEchoRuntimeOrchestrator>(FindObjectsSortMode.None);
            for (int i = 0; i < orchestrators.Length; i++)
            {
                if (orchestrators[i].gameObject != gameObject)
                {
                    orchestrators[i].enabled = false;
                    Debug.Log("[TemporalEcho] Disabled duplicate Orchestrator on " + orchestrators[i].gameObject.name);
                }
            }

            var bridges = FindObjectsByType<TemporalEchoDetectionBridge>(FindObjectsSortMode.None);
            for (int i = 0; i < bridges.Length; i++)
            {
                if (bridges[i].gameObject != gameObject)
                {
                    bridges[i].enabled = false;
                    Debug.Log("[TemporalEcho] Disabled duplicate Bridge on " + bridges[i].gameObject.name);
                }
            }

            var mediaControllers = FindObjectsByType<EraMediaController>(FindObjectsSortMode.None);
            for (int i = 0; i < mediaControllers.Length; i++)
            {
                if (mediaControllers[i].gameObject != gameObject)
                {
                    mediaControllers[i].enabled = false;
                    Debug.Log("[TemporalEcho] Disabled duplicate EraMediaController on " + mediaControllers[i].gameObject.name);
                }
            }

            var eventSystems = FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None);
            if (eventSystems.Length > 1)
            {
                for (int i = 1; i < eventSystems.Length; i++)
                {
                    eventSystems[i].gameObject.SetActive(false);
                    Debug.Log("[TemporalEcho] Disabled duplicate EventSystem.");
                }
            }

            var videoScreens = FindObjectsByType<EraVideoScreen>(FindObjectsSortMode.None);
            var screenAnchor = GameObject.Find("ScreenAnchor")?.transform;
            EraVideoScreen canonical = screenAnchor != null ? screenAnchor.GetComponentInChildren<EraVideoScreen>(true) : null;
            for (int i = 0; i < videoScreens.Length; i++)
            {
                if (videoScreens[i] != canonical)
                {
                    videoScreens[i].gameObject.SetActive(false);
                    Debug.Log("[TemporalEcho] Disabled duplicate VideoScreen.");
                }
            }
        }
    }
}
