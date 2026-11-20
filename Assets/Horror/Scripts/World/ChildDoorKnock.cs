using Horror.Locomotion;
using Horror.Noise;
using UnityEngine;

namespace Horror.World
{
    /// <summary>
    /// Petite porte d'enfant (qui ne s'ouvre pas). De temps en temps, quand un joueur passe devant,
    /// quelqu'un frappe de l'autre côté... et parfois chuchote. Le bruit des coups est un vrai bruit
    /// pour le monstre aveugle : rester devant une porte qui frappe, c'est l'attirer vers toi.
    /// </summary>
    public class ChildDoorKnock : MonoBehaviour
    {
        public string childName;
        public AudioClip[] knocks;
        public AudioClip[] voices;
        [Tooltip("Il faut un joueur à moins de cette distance pour que ça frappe.")]
        public float hearRadius = 4.5f;
        [Tooltip("Attente entre deux essais (secondes, min / max).")]
        public Vector2 interval = new Vector2(20f, 55f);
        [Range(0f, 1f)] public float voiceChance = 0.25f;
        [Tooltip("Portée du bruit pour le monstre (petit : c'est étouffé par la porte).")]
        public float noiseRadius = 6f;

        private static float s_lastAny = -999f;   // jamais deux portes en même temps
        private AudioSource _audio;
        private float _next;

        private void Awake()
        {
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.spatialBlend = 1f;
            _audio.minDistance = 0.8f;
            _audio.maxDistance = 12f;
            _audio.rolloffMode = AudioRolloffMode.Linear;
            _audio.playOnAwake = false;
            _next = Time.time + Random.Range(interval.x * 0.3f, interval.y);
        }

        private void Update()
        {
            if (Time.time < _next) return;
            _next = Time.time + Random.Range(interval.x, interval.y);
            if (Time.time - s_lastAny < 8f || !PlayerNear()) return;
            Knock();
        }

        public void Knock()
        {
            s_lastAny = Time.time;
            if (knocks != null && knocks.Length > 0)
            {
                AudioClip k = knocks[Random.Range(0, knocks.Length)];
                if (k != null) _audio.PlayOneShot(k);
            }
            NoiseSystem.Emit(transform.position, noiseRadius, NoiseType.Leurre, gameObject);
            if (voices != null && voices.Length > 0 && Random.value < voiceChance)
                Invoke(nameof(Whisper), 2f);
        }

        private void Whisper()
        {
            AudioClip v = voices[Random.Range(0, voices.Length)];
            if (v != null) _audio.PlayOneShot(v, 0.9f);
        }

        private bool PlayerNear()
        {
            float r2 = hearRadius * hearRadius;
            foreach (ArmLocomotion p in ArmLocomotion.All)
                if (p != null && (p.HeadPosition - transform.position).sqrMagnitude < r2) return true;
            return false;
        }
    }
}
