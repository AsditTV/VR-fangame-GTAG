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

        public bool Attached { get; private set; } = true;

        private AudioSource _audio;
        private Vector3 _last;
        private float _parkAt, _detachedAt, _fetchAt, _speed;
        private DoctorAI.State _prev;
        private bool _fetching;
        private FragileNoisy _tray;

        private void Start()
        {
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.clip = squeak; _audio.loop = true; _audio.spatialBlend = 1f; _audio.volume = 0f;
            _audio.rolloffMode = AudioRolloffMode.Linear; _audio.minDistance = 2f; _audio.maxDistance = 24f;
            if (squeak != null) _audio.Play();
            _tray = GetComponentInChildren<FragileNoisy>();
            _parkAt = Time.time + Random.Range(parkEvery.x, parkEvery.y);
            _last = transform.position;
            if (doctor != null) _prev = doctor.state;
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
                    float dist = ahead;                                  // un mur devant : le chariot reste contre lui, pas dedans
                    Vector3 from = doctor.transform.position + Vector3.up * 0.7f + fwd * 0.3f;
                    if (Physics.Raycast(from, fwd, out RaycastHit hit, ahead + 0.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                        dist = Mathf.Max(0.55f, hit.distance + 0.3f - 0.55f);
                    Vector3 want = doctor.transform.position + fwd * dist;
                    transform.position = Vector3.Lerp(transform.position, want, 1f - Mathf.Exp(-10f * Time.deltaTime));
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
