using System;
using UnityEngine;

namespace TemporalEcho
{
    public enum EraId { E1920s, E1960s, E2026 }
    public enum DetectedSubject { Screen, Book, Human }

    [CreateAssetMenu(menuName = "TemporalEcho/Era Database", fileName = "EraDatabase")]
    public class EraDatabase : ScriptableObject
    {
        public EraDefinition era1920s;
        public EraDefinition era1960s;
        public EraDefinition era2026;

        public EraDefinition Get(EraId era) => era switch
        {
            EraId.E1920s => era1920s,
            EraId.E1960s => era1960s,
            EraId.E2026  => era2026,
            _ => era2026
        };
    }

    [Serializable]
    public struct EraDefinition
    {
        public string label;
        public AudioClip soundtrack;
        [Tooltip("2026 era: use video on screen anchor instead of audio when set.")]
        public UnityEngine.Video.VideoClip videoClip;
        public EraRule[] rules; // Screen/Book/Human -> prefab + offsets
    }

    [Serializable]
    public struct EraRule
    {
        public DetectedSubject subject;
        public GameObject prefab;
        public Vector3 localPosition;
        public Vector3 localEulerRotation;
        public Vector3 localScale;
    }
}
