using System;
using Horror.Locomotion;
using UnityEngine;

namespace Horror.Noise
{
    /// <summary>
    /// Transforme les impacts des mains (la locomotion aux bras) en bruits de "pas".
    /// Taper doucement = presque silencieux. Courir en frappant fort = on t'entend de loin.
    /// Le son ET la portée changent selon la surface : métal et carrelage résonnent, la moquette étouffe,
    /// l'encre fait "splash". À mettre sur l'objet "Joueur" (celui qui a ArmLocomotion).
    /// </summary>
    [RequireComponent(typeof(ArmLocomotion))]
    public class FootstepNoise : MonoBehaviour
    {
        [Tooltip("Vitesse d'impact (m/s) à partir de laquelle le bruit est au maximum.")]
        public float loudSpeed = 4f;
        [Tooltip("Portée (m) d'un impact très doux.")]
        public float minRadius = 1.5f;
        [Tooltip("Portée (m) d'un impact violent.")]
        public float maxRadius = 16f;

        [Serializable]
        public class SurfaceSounds
        {
            public SurfaceType surface;
            public AudioClip[] soft;
            public AudioClip[] hard;
        }

        [Header("Sons par surface")]
        public SurfaceSounds[] surfaces;
        [Tooltip("Sons de secours si la surface n'a pas de sons.")]
        public AudioClip[] tapClips;
        [Range(0f, 1f)] public float volume = 0.6f;
        [Tooltip("Vitesse au-delà de laquelle on joue les sons « forts ».")]
        public float hardSpeed = 2.5f;

        private ArmLocomotion _locomotion;

        private void Awake() => _locomotion = GetComponent<ArmLocomotion>();
        private void OnEnable() => GetComponent<ArmLocomotion>().HandImpactOn += OnHandImpact;
        private void OnDisable()
        {
            if (_locomotion != null) _locomotion.HandImpactOn -= OnHandImpact;
        }

        private void OnHandImpact(Vector3 position, float speed, bool isLeft, Collider surface)
        {
            SurfaceType type = Surface.Of(surface);
            float t = Mathf.Clamp01(speed / loudSpeed);
            float radius = Mathf.Lerp(minRadius, maxRadius, t * t) * Surface.Loudness(type);   // les gros coups portent plus loin
            NoiseSystem.Emit(position, radius, NoiseType.Pas, gameObject);

            AudioClip clip = Pick(type, speed >= hardSpeed);
            if (clip != null)
                AudioSource.PlayClipAtPoint(clip, position, volume * Mathf.Lerp(0.3f, 1f, t) * Mathf.Lerp(0.8f, 1.1f, Surface.Loudness(type) / 1.6f));
        }

        private AudioClip Pick(SurfaceType type, bool hard)
        {
            if (surfaces != null)
                foreach (SurfaceSounds s in surfaces)
                {
                    if (s == null || s.surface != type) continue;
                    AudioClip[] pool = hard && s.hard != null && s.hard.Length > 0 ? s.hard : s.soft;
                    if (pool != null && pool.Length > 0) return pool[UnityEngine.Random.Range(0, pool.Length)];
                }
            return tapClips != null && tapClips.Length > 0 ? tapClips[UnityEngine.Random.Range(0, tapClips.Length)] : null;
        }
    }
}
