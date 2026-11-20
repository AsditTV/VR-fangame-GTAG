using Horror.Locomotion;
using UnityEngine;

namespace Horror.World
{
    /// <summary>
    /// Une chaise (ou un objet) qui flotte dans le noir en tournant doucement.
    /// Si un joueur s'approche trop : elle tremble... puis TOMBE. Le choc fait du bruit (ImpactNoise)
    /// et le monstre aveugle vient voir. Quand plus personne n'est à côté, elle remonte lentement
    /// à sa place. Le pivot de l'objet doit être au centre de la chaise.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class FloatingProp : MonoBehaviour
    {
        [Header("Flotter")]
        public float bobHeight = 0.08f;
        public float bobSpeed = 0.6f;
        [Tooltip("Rotation lente sur elle-même (degrés par seconde).")]
        public float spinSpeed = 6f;

        [Header("Tomber")]
        [Tooltip("Distance (tête du joueur) qui fait tomber la chaise.")]
        public float dropRadius = 1.8f;
        [Tooltip("Durée du tremblement d'avertissement avant la chute.")]
        public float shiverTime = 0.8f;
        public AudioClip shiverSound;

        [Header("Remonter")]
        [Tooltip("Temps passé au sol avant de pouvoir remonter.")]
        public float riseDelay = 10f;
        public float riseDuration = 4f;
        [Tooltip("Elle ne remonte pas tant qu'un joueur est plus près que ça.")]
        public float keepAwayRadius = 3f;

        private enum State { Floating, Shivering, Fallen, Rising }
        private State _state;
        private Rigidbody _rb;
        private AudioSource _audio;
        private Vector3 _home;
        private Quaternion _homeRot;
        private Vector3 _from;
        private Quaternion _fromRot;
        private float _timer;
        private float _phase;
        private float _spin;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.isKinematic = true;
            _rb.useGravity = true;
            _home = transform.position;
            _homeRot = transform.rotation;
            _phase = Random.value * 10f;
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.spatialBlend = 1f;
            _audio.maxDistance = 10f;
            _audio.playOnAwake = false;
        }

        private void Update()
        {
            switch (_state)
            {
                case State.Floating:
                    Float(Time.time);
                    if (PlayerNear(dropRadius))
                    {
                        _state = State.Shivering;
                        _timer = 0f;
                        if (shiverSound != null) _audio.PlayOneShot(shiverSound, 0.8f);
                    }
                    break;

                case State.Shivering:
                    _timer += Time.deltaTime;
                    Float(Time.time);
                    float k = _timer / shiverTime;
                    transform.position += Random.insideUnitSphere * 0.015f * k;     // tremble de plus en plus
                    transform.rotation *= Quaternion.Euler(Random.insideUnitSphere * 4f * k);
                    if (_timer >= shiverTime) Drop();
                    break;

                case State.Fallen:
                    _timer += Time.deltaTime;
                    if (_timer > riseDelay && !PlayerNear(keepAwayRadius) && Speed2 < 0.01f)
                    {
                        _state = State.Rising;
                        _timer = 0f;
                        _rb.isKinematic = true;
                        _from = transform.position;
                        _fromRot = transform.rotation;
                    }
                    break;

                case State.Rising:
                    _timer += Time.deltaTime;
                    float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_timer / riseDuration));
                    transform.SetPositionAndRotation(Vector3.Lerp(_from, _home, t), Quaternion.Slerp(_fromRot, _homeRot, t));
                    if (t >= 1f) { _state = State.Floating; _spin = 0f; }
                    break;
            }
        }

        private void Float(float time)
        {
            _spin += spinSpeed * Time.deltaTime;
            Vector3 pos = _home + Vector3.up * Mathf.Sin((time + _phase) * bobSpeed * Mathf.PI * 2f) * bobHeight;
            Quaternion rot = Quaternion.AngleAxis(_spin, Vector3.up) * _homeRot
                             * Quaternion.Euler(Mathf.Sin(time * 0.7f + _phase) * 4f, 0f, Mathf.Cos(time * 0.5f + _phase) * 4f);
            transform.SetPositionAndRotation(pos, rot);
        }

        private void Drop()
        {
            _state = State.Fallen;
            _timer = 0f;
            _rb.isKinematic = false;
#if UNITY_6000_0_OR_NEWER
            _rb.linearVelocity = Vector3.down * 0.5f;
#else
            _rb.velocity = Vector3.down * 0.5f;
#endif
            _rb.angularVelocity = Random.insideUnitSphere * 3f;
        }

#if UNITY_6000_0_OR_NEWER
        private float Speed2 => _rb.linearVelocity.sqrMagnitude;
#else
        private float Speed2 => _rb.velocity.sqrMagnitude;
#endif

        private bool PlayerNear(float radius)
        {
            float r2 = radius * radius;
            foreach (ArmLocomotion p in ArmLocomotion.All)
                if (p != null && (p.HeadPosition - transform.position).sqrMagnitude < r2) return true;
            return false;
        }
    }
}
