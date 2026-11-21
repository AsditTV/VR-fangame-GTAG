using UnityEngine;
using Horror.Noise;

namespace Horror.Monsters
{
    /// <summary>
    /// Le chariot du Docteur. En ronde, il le pousse devant lui : la roulette couine et les instruments tintent,
    /// on l'entend venir de loin (comme sa comptine). Dès qu'il te voit, ou qu'il part voir un bruit, il le LÂCHE
    /// sur place : le chariot devient un obstacle fragile au milieu d'un couloir (son plateau s'écrase si on le
    /// frôle : FragileNoisy). De temps en temps, il le gare lui-même et repart sans lui ; plus tard il revient le chercher.
    /// Les collisions du chariot sont sur le calque « Ignore Raycast » : le Docteur ne se cogne pas dedans et
    /// voit par-dessus, mais le joueur, lui, le heurte.
    /// </summary>
    [DefaultExecutionOrder(50)]   // après GaitAnimator : les mains se posent sur la poignée par-dessus la démarche
    public class DoctorCart : MonoBehaviour
    {
        public DoctorAI doctor;
        public AudioClip squeak, release;
        [Tooltip("Distance du chariot devant le Docteur (m).")]
        public float ahead = 1.25f;
        [Tooltip("Il gare son chariot toutes les X à Y secondes de ronde.")]
        public Vector2 parkEvery = new Vector2(40f, 75f);
        [Tooltip("Après ce temps sans chariot (en ronde), il revient le chercher.")]
        public float fetchAfter = 45f;
        [Tooltip("Points de la poignée où il pose ses mains (repère local du chariot).")]
        public Vector3 gripLeft = new Vector3(-0.15f, 1f, -0.2f), gripRight = new Vector3(0.15f, 1f, -0.2f);
        [Tooltip("Calcule la distance du chariot pour que ses bras tombent pile sur la poignée.")]
        public bool autoAhead = true;

        public bool Attached { get; private set; } = true;

        private AudioSource _audio;
        private Vector3 _last;
        private float _parkAt, _detachedAt, _fetchAt, _speed;
        private DoctorAI.State _prev;
        private Transform _shL, _shR, _hdL, _hdR;
        private float _grip;
        private Vector3 _palmL, _palmR;
        private float _arm;   // centre de la main gantée, dans le repère de l'épaule
        private bool _fetching;
        private FragileNoisy _tray;

        private void Start()
        {
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.clip = squeak; _audio.loop = true; _audio.spatialBlend = 1f; _audio.volume = 0f;
            _audio.rolloffMode = AudioRolloffMode.Linear; _audio.minDistance = 2f; _audio.maxDistance = 24f;
            if (squeak != null) _audio.Play();
            _tray = GetComponentInChildren<FragileNoisy>();
            Rigidbody rb = GetComponent<Rigidbody>();                 // on le place nous-mêmes : pas d'interpolation physique
            if (rb != null) rb.interpolation = RigidbodyInterpolation.None;
            _parkAt = Time.time + Random.Range(parkEvery.x, parkEvery.y);
            _last = transform.position;
            if (doctor != null) _prev = doctor.state;
            if (doctor != null) SetupArms();
        }

        /// <summary>Trouve épaules et mains du Docteur, et place le chariot à la longueur de ses bras.</summary>
        private void SetupArms()
        {
            foreach (Transform t in doctor.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Epaule_G") _shL = t; else if (t.name == "Epaule_D") _shR = t;
                else if (t.name == "Main_G") _hdL = t; else if (t.name == "Main_D") _hdR = t;
            }
            if (_shL == null || _hdL == null) return;
            _palmL = _shL.InverseTransformPoint(Palm(_hdL));
            if (_shR != null && _hdR != null) _palmR = _shR.InverseTransformPoint(Palm(_hdR));
            _arm = Vector3.Distance(_shL.position, _shL.TransformPoint(_palmL));
            if (autoAhead) ahead = ArmAhead(Vector3.ProjectOnPlane(doctor.transform.forward, Vector3.up).normalized);
        }

        /// <summary>Distance du chariot pour que la poignée tombe pile au bout de ses bras (recalculée à chaque image :
        /// le rebond et l'inclinaison de la démarche font monter et descendre les épaules).</summary>
        private float ArmAhead(Vector3 fwd)
        {
            Transform d = doctor.transform;
            float dy = (_shL.position.y - d.position.y) - gripLeft.y * transform.lossyScale.y;
            float reach = Mathf.Sqrt(Mathf.Max(0.04f, _arm * _arm * 0.98f - dy * dy));
            float shoulderAhead = Vector3.Dot(_shL.position - d.position, fwd);
            float gripBack = -gripLeft.z * transform.lossyScale.z;
            return Mathf.Clamp(shoulderAhead + reach + gripBack, 0.6f, 1.9f);
        }

        /// <summary>Tourne l'épaule pour que la main tombe sur la cible (bras d'un seul tenant, comme le modèle).</summary>
        /// <summary>Une main sur la poignée ; celle qui tient la lanterne la porte en avant, à côté du chariot
        /// (sinon la lanterne pendrait dans le plateau).</summary>
        private void ArmPose(Transform shoulder, Transform hand, Vector3 palm, Vector3 grip)
        {
            if (shoulder == null || hand == null) return;
            if (HasLantern(hand))
            {
                Vector3 fwd = transform.forward, outward = shoulder.position - doctor.transform.position;
                outward = Vector3.ProjectOnPlane(outward, Vector3.up); outward = outward - Vector3.Project(outward, fwd);
                outward = outward.sqrMagnitude > 0.0001f ? outward.normalized : transform.right;
                Reach(shoulder, palm, shoulder.position + fwd * 0.75f - Vector3.up * 0.8f + outward * 0.3f, _grip);
            }
            else Reach(shoulder, palm, transform.TransformPoint(grip), _grip);
        }

        private readonly System.Collections.Generic.Dictionary<Transform, bool> _lanternHand = new System.Collections.Generic.Dictionary<Transform, bool>();
        private bool HasLantern(Transform hand)
        {
            if (_lanternHand.TryGetValue(hand, out bool v)) return v;
            v = false;
            foreach (Transform t in hand.GetComponentsInChildren<Transform>(true)) if (t.name == "Lanterne") { v = true; break; }
            _lanternHand[hand] = v;
            return v;
        }

        private static Vector3 Palm(Transform hand)
        {
            Bounds b = default; bool first = true;
            foreach (Renderer r in hand.GetComponentsInChildren<Renderer>(true))
            {
                bool lantern = false;                                        // la lanterne (main droite) ne compte pas
                for (Transform t = r.transform; t != null && t != hand; t = t.parent) if (t.name.StartsWith("Lanterne")) { lantern = true; break; }
                if (lantern) continue;
                if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
            }
            return first ? hand.position : b.center;
        }

        private void Reach(Transform shoulder, Vector3 palmLocal, Vector3 target, float w)
        {
            if (shoulder == null || w <= 0.001f) return;
            Quaternion delta = Quaternion.FromToRotation(shoulder.TransformPoint(palmLocal) - shoulder.position, target - shoulder.position);
            shoulder.rotation = Quaternion.Slerp(Quaternion.identity, delta, w) * shoulder.rotation;
        }

        private void LateUpdate()
        {
            if (doctor == null) return;
            DoctorAI.State st = doctor.state;
            bool entered = st != _prev;          // il vient de changer d'état (ce cadre-ci)
            _prev = st;
            bool trayOk = _tray == null || !_tray.Fallen;
            if (Attached)
            {
                // il lâche tout dès qu'il te voit, qu'il t'a attrapé, ou qu'il PART voir un bruit (pas quand il vient de reprendre son chariot)
                bool letGo = st == DoctorAI.State.Traque || st == DoctorAI.State.Pause || (entered && st == DoctorAI.State.Inspecte);
                if (letGo || (st == DoctorAI.State.Patrouille && Time.time > _parkAt)) Detach();
                else
                {
                    Vector3 fwd = Vector3.ProjectOnPlane(doctor.transform.forward, Vector3.up).normalized;
                    if (fwd.sqrMagnitude < 0.01f) return;
                    float dist = autoAhead && _shL != null && _arm > 0f ? ArmAhead(fwd) : ahead;   // au bout de ses bras
                    Vector3 from = doctor.transform.position + Vector3.up * 0.7f + fwd * 0.3f;
                    if (Physics.Raycast(from, fwd, out RaycastHit hit, dist + 0.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                        dist = Mathf.Min(dist, Mathf.Max(0.55f, hit.distance + 0.3f - 0.55f));   // un mur devant : contre lui, pas dedans
                    Vector3 want = doctor.transform.position + fwd * dist;
                    transform.position = Vector3.Lerp(transform.position, want, 1f - Mathf.Exp(-30f * Time.deltaTime));
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(fwd, Vector3.up), 1f - Mathf.Exp(-8f * Time.deltaTime));
                }
            }
            else
            {
                if (st == DoctorAI.State.Traque || st == DoctorAI.State.Pause) _fetching = false;
                if (_fetching && Time.time - _fetchAt > 25f)            // il n'y est pas arrivé : il réessaiera un peu plus tard
                {
                    _fetching = false;
                    _detachedAt = Time.time - fetchAfter + 15f;
                }
                float d = Vector3.Distance(doctor.transform.position, transform.position);
                if (d < 1.9f && trayOk && st != DoctorAI.State.Traque && st != DoctorAI.State.Pause
                    && (st == DoctorAI.State.Patrouille || _fetching)) Attach();
                else if (!_fetching && trayOk && st == DoctorAI.State.Patrouille && Time.time - _detachedAt > fetchAfter)
                {
                    _fetching = true; _fetchAt = Time.time;
                    doctor.GoTo(transform.position - transform.forward * 1.2f);   // il revient chercher son chariot
                }
            }
            // ses mains sur la poignée tant qu'il pousse (la démarche reprend les bras dès qu'il lâche)
            _grip = Mathf.MoveTowards(_grip, Attached ? 1f : 0f, Time.deltaTime * 4f);
            if (_grip > 0f && Attached)
            {
                bool leftIsNeg = _shL != null && transform.InverseTransformPoint(_shL.position).x < 0f;   // la main gauche prend le côté gauche
                ArmPose(_shL, _hdL, _palmL, leftIsNeg ? gripLeft : gripRight);
                ArmPose(_shR, _hdR, _palmR, leftIsNeg ? gripRight : gripLeft);
            }
            // la roulette couine quand le chariot roule
            Vector3 mv = transform.position - _last; mv.y = 0f; _last = transform.position;
            _speed = Mathf.Lerp(_speed, Time.deltaTime > 0f ? mv.magnitude / Time.deltaTime : 0f, 1f - Mathf.Exp(-6f * Time.deltaTime));
            if (_audio != null)
            {
                _audio.volume = Mathf.Clamp01(_speed / 1.5f) * 0.75f;
                _audio.pitch = 0.9f + Mathf.Clamp01(_speed / 5f) * 0.4f;
            }
        }

        private void Detach()
        {
            Attached = false; _fetching = false;
            _detachedAt = Time.time;
            if (release != null && _speed > 0.3f) AudioSource.PlayClipAtPoint(release, transform.position, 0.7f);
        }

        private void Attach()
        {
            Attached = true; _fetching = false;
            _parkAt = Time.time + Random.Range(parkEvery.x, parkEvery.y);
        }
    }
}
