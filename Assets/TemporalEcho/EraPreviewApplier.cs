using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TemporalEcho
{
    [ExecuteAlways]
    public class EraPreviewApplier : MonoBehaviour
    {
        public EraDatabase database;
        public EraId activeEra = EraId.E1920s;

        [Header("Anchors (spawn points in scene)")]
        public Transform screenAnchor;
        public Transform bookAnchor;
        public Transform humanAnchor;

        private const string SpawnedNamePrefix = "__EraPreview__";

        // Called by editor buttons
        public void ApplyPreview(EraId era)
        {
            activeEra = era;

            if (!database)
            {
                Debug.LogError("[EraPreviewApplier] Missing EraDatabase.");
                return;
            }

            var def = database.Get(era);

            // Clear previous preview spawns
            ClearPreviewChildren(screenAnchor);
            ClearPreviewChildren(bookAnchor);
            ClearPreviewChildren(humanAnchor);

            if (def.rules == null) return;

            foreach (var rule in def.rules)
            {
                var anchor = GetAnchor(rule.subject);
                if (!anchor || !rule.prefab) continue;

                var inst = InstantiatePrefabUnderAnchor(rule.prefab, anchor);
                if (!inst) continue;

                inst.name = $"{SpawnedNamePrefix}{rule.subject}";
                inst.transform.localPosition = rule.localPosition;
                inst.transform.localRotation = Quaternion.Euler(rule.localEulerRotation);
                inst.transform.localScale = (rule.localScale == Vector3.zero) ? Vector3.one : rule.localScale;
            }
        }

        private Transform GetAnchor(DetectedSubject subject) => subject switch
        {
            DetectedSubject.Screen => screenAnchor,
            DetectedSubject.Book => bookAnchor,
            DetectedSubject.Human => humanAnchor,
            _ => null
        };

        private void ClearPreviewChildren(Transform anchor)
        {
            if (!anchor) return;

            for (int i = anchor.childCount - 1; i >= 0; i--)
            {
                var child = anchor.GetChild(i);
                if (child.name.StartsWith(SpawnedNamePrefix))
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                        Undo.DestroyObjectImmediate(child.gameObject);
                    else
                        Destroy(child.gameObject);
#else
                    Destroy(child.gameObject);
#endif
                }
            }
        }

        private GameObject InstantiatePrefabUnderAnchor(GameObject prefab, Transform anchor)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                var inst = PrefabUtility.InstantiatePrefab(prefab, anchor) as GameObject;
                if (inst != null)
                {
                    Undo.RegisterCreatedObjectUndo(inst, "Spawn Era Preview Prefab");
                    return inst;
                }
            }
#endif
            return Instantiate(prefab, anchor);
        }
    }
}
