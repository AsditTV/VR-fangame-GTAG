using UnityEngine;

namespace Horror.Noise
{
    /// <summary>
    /// Un objet physique qui fait du bruit quand il cogne quelque chose (lancé, tombé, renversé).
    /// C'est l'outil du joueur pour faire diversion : lancer un objet loin attire le monstre là-bas.
    /// À mettre sur tout objet avec un Rigidbody (vase, peluche, boîte à musique...).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ImpactNoise : MonoBehaviour
    {
        [Tooltip("Vitesse de choc minimale pour faire du bruit (m/s).")]
        public float minSpeed = 1f;
        [Tooltip("Vitesse de choc pour le bruit maximal (m/s).")]
        public float loudSpeed = 8f;
        [Tooltip("Portée max (m). Un objet lourd ou en métal porte plus loin.")]
        public float maxRadius = 18f;
        [Tooltip("Temps minimum entre deux bruits (évite le spam quand l'objet roule).")]
        public float cooldown = 0.15f;

        [Header("Son (facultatif)")]
        public AudioClip[] hitClips;
        [Range(0f, 1f)] public float volume = 0.8f;

        private float _nextAllowed;

        private void OnCollisionEnter(Collision collision)
        {
            float speed = collision.relativeVelocity.magnitude;
            if (speed < minSpeed || Time.time < _nextAllowed) return;
            _nextAllowed = Time.time + cooldown;

            float t = Mathf.InverseLerp(minSpeed, loudSpeed, speed);
            Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            NoiseSystem.Emit(point, Mathf.Lerp(maxRadius * 0.15f, maxRadius, t), NoiseType.Objet, gameObject);

            if (hitClips != null && hitClips.Length > 0)
            {
                AudioClip clip = hitClips[Random.Range(0, hitClips.Length)];
                if (clip != null) AudioSource.PlayClipAtPoint(clip, point, volume * Mathf.Lerp(0.3f, 1f, t));
            }
        }
    }
}
