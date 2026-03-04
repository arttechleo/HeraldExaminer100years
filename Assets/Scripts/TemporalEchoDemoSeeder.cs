using UnityEngine;

/// <summary>
/// DEV-only: spawns 3 demo props (typewriter, newspaper, chair) in front of the user at Start so demos are not empty.
/// Does nothing in release builds. Does not change detection logic.
/// </summary>
public class TemporalEchoDemoSeeder : MonoBehaviour
{
    [Tooltip("Enable demo seeding. Auto-disabled if !Debug.isDebugBuild.")]
    [SerializeField] private bool demoSeedEnabled = true;

    [Tooltip("Era to use for seeded props.")]
    [SerializeField] private TemporalEra demoEra = TemporalEra.Era1920s;

    [Tooltip("Distance in front of camera (meters).")]
    [SerializeField] private float seedDistance = 1.5f;

    [Tooltip("Offset for typewriter (X=right, Y=up from center).")]
    [SerializeField] private Vector3 seedOffsetTypewriter = new Vector3(-0.4f, 0f, 0f);

    [Tooltip("Offset for newspaper (center).")]
    [SerializeField] private Vector3 seedOffsetNewspaper = new Vector3(0f, 0f, 0f);

    [Tooltip("Offset for chair (X=right, Y=up).")]
    [SerializeField] private Vector3 seedOffsetChair = new Vector3(0.4f, 0f, 0f);

    [Tooltip("Spawn typewriter (Screen).")]
    [SerializeField] private bool seedTypewriter = true;

    [Tooltip("Spawn newspaper (Book).")]
    [SerializeField] private bool seedNewspaper = true;

    [Tooltip("Spawn chair (Chair).")]
    [SerializeField] private bool seedChair = true;

    [Tooltip("Also spawn microphones on seeded props.")]
    [SerializeField] private bool alsoSpawnMicrophones = true;

    private void Start()
    {
        if (!Debug.isDebugBuild)
        {
            demoSeedEnabled = false;
            enabled = false;
            return;
        }
        if (!demoSeedEnabled) return;

        var eraManager = FindAnyObjectByType<TemporalEraManager>();
        if (eraManager != null)
            eraManager.SetEra(demoEra);

        var pdm = FindAnyObjectByType<PersistentDiscoveryManager>();
        if (pdm == null) return;

        Transform cam = QuestAudioHelper.FindXRCameraTransform();
        if (cam == null)
        {
            var cams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (var c in cams)
            {
                if (c.enabled) { cam = c.transform; break; }
            }
        }
        if (cam == null) return;

        Vector3 pos = cam.position;
        Vector3 fwd = cam.forward;
        Vector3 right = cam.right;
        Vector3 up = cam.up;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
        else fwd.Normalize();
        right.y = 0f;
        if (right.sqrMagnitude > 0.0001f) right.Normalize();

        Quaternion rot = Quaternion.LookRotation(-fwd);
        Vector3 scale = Vector3.one;

        if (seedTypewriter)
        {
            Vector3 worldPos = pos + fwd * seedDistance + right * seedOffsetTypewriter.x + up * seedOffsetTypewriter.y;
            pdm.CreateSeededProp(DetectionCategory.Screen, worldPos, rot, scale, alsoSpawnMicrophones);
        }
        if (seedNewspaper)
        {
            Vector3 worldPos = pos + fwd * seedDistance + right * seedOffsetNewspaper.x + up * seedOffsetNewspaper.y;
            pdm.CreateSeededProp(DetectionCategory.Book, worldPos, rot, scale, alsoSpawnMicrophones);
        }
        if (seedChair)
        {
            Vector3 worldPos = pos + fwd * seedDistance + right * seedOffsetChair.x + up * seedOffsetChair.y;
            pdm.CreateSeededProp(DetectionCategory.Chair, worldPos, rot, scale, alsoSpawnMicrophones);
        }
    }
}
