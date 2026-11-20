using Horror.Locomotion;
using UnityEngine;

namespace Horror.Noise
{
    /// <summary>
    /// Transforme les impacts des mains (la locomotion aux bras) en bruits de "pas".
    /// Taper doucement = presque silencieux. Courir en frappant fort = on t'entend de loin.
    /// À mettre sur l'objet "Joueur" (celui qui a ArmLocomotion).
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

        [Header("Son (facultatif)")]
        [Tooltip("Petits bruits de tissu / de tapotement joués à chaque impact.")]
        public AudioClip[] tapClips;
        [Range(0f, 1f)] public float volume = 0.6f;

        private ArmLocomotion _locomotion;

        private void Awake() => _locomotion = GetComponent<ArmLocomotion>();
        private void OnEnable() => GetComponent<ArmLocomotion>().HandImpact += OnHandImpact;
        private void OnDisable()
        {
            if (_locomotion != null) _locomotion.HandImpact -= OnHandImpact;
        }

        private void OnHandImpact(Vector3 position, float speed, bool isLeft)
        {
            float t = Mathf.Clamp01(speed / loudSpeed);
            float radius = Mathf.Lerp(minRadius, maxRadius, t * t); // les gros coups portent beaucoup plus loin
            NoiseSystem.Emit(position, radius, NoiseType.Pas, gameObject);

            if (tapClips != null && tapClips.Length > 0)
            {
                AudioClip clip = tapClips[Random.Range(0, tapClips.Length)];
                if (clip != null) AudioSource.PlayClipAtPoint(clip, position, volume * Mathf.Lerp(0.3f, 1f, t));
            }
        }
    }
}
