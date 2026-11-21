using UnityEngine;
using Horror.Locomotion;

namespace Horror.Noise
{
    /// <summary>
    /// Un objet fragile (plateau d'instruments, bocaux, pied à perfusion...) : si un joueur le TOUCHE (main, corps),
    /// il bascule et s'écrase avec un fracas ÉNORME (bruit de grande portée : le Docteur l'entend de loin, L'Écoute
    /// aussi). Il reste par terre, puis se remet en place quand personne ne regarde, quelques minutes plus tard.
    /// À mettre sur le noeud qui tombe (« Chute_* ») ; un déclencheur un peu plus grand que l'objet détecte le contact.
    /// </summary>
    public class FragileNoisy : MonoBehaviour
    {
        public AudioClip crash;
        [Tooltip("Portée du fracas (m). Le Docteur n'entend que les bruits de plus de 12 m.")]
        public float noiseRadius = 34f;   // énorme : à travers un mur, encore ~15 m
        public float resetDelay = 180f;
        [Tooltip("Petit coup donné à l'objet quand il bascule (dans le sens du contact).")]
        public float push = 1.6f;

        private Rigidbody _rb;
        private Vector3 _pos; private Quaternion _rot;
        private bool _fallen;
        private float _resetAt;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            if (_rb == null) _rb = gameObject.AddComponent<Rigidbody>();
            _rb.isKinematic = true;
            _rb.mass = 2f;
            _pos = transform.position; _rot = transform.rotation;
        }

        private void OnTriggerEnter(Collider other) { TryKnock(other); }
        private void OnCollisionEnter(Collision c) { TryKnock(c.collider); }

        private void TryKnock(Collider other)
        {
            if (_fallen || other == null) return;
            ArmLocomotion p = other.GetComponentInParent<ArmLocomotion>();
            if (p == null) return;
            Knock((transform.position - other.transform.position).normalized);
        }

        /// <summary>Le fait tomber tout de suite (tests).</summary>
        [ContextMenu("Faire tomber")]
        public void KnockNow() => Knock(transform.forward);

        private void Knock(Vector3 from)
        {
            _fallen = true;
            _rb.isKinematic = false;
            from.y = 0.4f;
            _rb.AddForce(from.normalized * push, ForceMode.VelocityChange);
            _rb.AddTorque(Random.insideUnitSphere * 3f, ForceMode.VelocityChange);
            if (crash != null) AudioSource.PlayClipAtPoint(crash, transform.position, 1f);
            NoiseSystem.Emit(transform.position, noiseRadius, NoiseType.Objet, gameObject);
            _resetAt = Time.time + resetDelay;
        }

        private void Update()
        {
            if (!_fallen || Time.time < _resetAt) return;
            foreach (ArmLocomotion p in ArmLocomotion.All)          // jamais sous les yeux de quelqu'un
                if (p != null && (p.HeadPosition - _pos).sqrMagnitude < 64f) { _resetAt = Time.time + 5f; return; }
            _rb.isKinematic = true;
            transform.SetPositionAndRotation(_pos, _rot);
            _fallen = false;
        }
    }
}
