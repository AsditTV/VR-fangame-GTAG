using Horror.Player;
using UnityEngine;

namespace Horror.Noise
{
    /// <summary>
    /// Ta voix fait du bruit : plus tu parles fort, plus le monstre t'entend de loin.
    /// Lit MicMouth.Openness (la même valeur qui fait bouger la bouche du personnage).
    /// À mettre sur le joueur, à côté de MicMouth (ou dans un parent).
    /// </summary>
    public class VoiceNoise : MonoBehaviour
    {
        [Tooltip("Laisse vide : il cherche MicMouth sur cet objet ou ses enfants.")]
        public MicMouth mic;
        [Tooltip("D'où part la voix (la tête). Vide = cet objet.")]
        public Transform mouthPoint;

        [Tooltip("En dessous de cette ouverture de bouche, c'est un chuchotement inaudible.")]
        [Range(0f, 1f)] public float whisperThreshold = 0.15f;
        [Tooltip("Portée (m) d'une voix normale.")]
        public float talkRadius = 7f;
        [Tooltip("Portée (m) d'un cri.")]
        public float screamRadius = 30f;
        [Tooltip("Intervalle entre deux bruits de voix (secondes).")]
        public float interval = 0.25f;

        [Tooltip("Faux = micro coupé (mode Muet du Minitel) : la voix ne fait plus de bruit.")]
        public bool voiceEnabled = true;

        private float _nextEmit;

        private void Awake()
        {
            if (mic == null) mic = GetComponentInChildren<MicMouth>();
            if (mouthPoint == null) mouthPoint = transform;
        }

        private void Update()
        {
            if (!voiceEnabled || mic == null || Time.time < _nextEmit) return;

            float level = mic.Openness;
            if (level < whisperThreshold) return;

            float t = Mathf.InverseLerp(whisperThreshold, 1f, level);
            NoiseSystem.Emit(mouthPoint.position, Mathf.Lerp(talkRadius * 0.5f, screamRadius, t * t), NoiseType.Voix, gameObject);
            _nextEmit = Time.time + interval;
        }
    }
}
