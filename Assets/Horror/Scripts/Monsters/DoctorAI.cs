using System.Collections.Generic;
using UnityEngine;
using Horror.Locomotion;
using Horror.Noise;
using Horror.Player;
using Horror.World;

namespace Horror.Monsters
{
    /// <summary>
    /// Le Docteur (l'infirmerie du sous-sol). Lui n'est PAS aveugle : il voit, mais seulement ce que sa lanterne
    /// éclaire (un cône devant lui) ; les murs et les rideaux coupent sa vue. Il est sourd aux petits bruits :
    /// seuls les FRACAS (plateaux, bocaux, perfusions qu'on renverse) l'attirent.
    ///  - Patrouille : il marche lentement de salle en salle en FREDONNANT (on l'entend venir) ;
    ///  - Inspecte : un fracas ou une silhouette entrevue : il y va, lanterne levée, et fouille du regard ;
    ///  - Traque : il t'a vu. Il se TAIT (plus de comptine : le silence, c'est le danger), claque du bec, et marche
    ///    vite vers toi sans jamais courir. Il ne te lâche pas tant qu'il te voit ; caché 4 s, il te perd ;
    ///  - Pause : après une prise, il reste penché quelques secondes (les autres ont le temps de fuir).
    /// Se cacher : derrière un mur, un rideau, ou allongé dans un lit (HideZone) sans bouger.
    /// </summary>
    public class DoctorAI : MonoBehaviour
    {
        public enum State { Patrouille, Inspecte, Traque, Pause }

        [Header("Corps")]
        public Transform head;
        public Transform lantern;
        public Light lanternLight;

        [Header("Vision (la lanterne)")]
        public float viewRange = 10f;
        public float viewAngle = 55f;
        [Tooltip("Tout près, il te sent même dans le dos (m).")]
        public float senseRadius = 2f;
        public float loseSightTime = 4f;

        [Header("Ouïe (seulement les fracas)")]
        [Tooltip("Portée minimale d'un bruit pour qu'il l'entende (les objets fragiles font ~22 m).")]
        public float loudRadius = 12f;
        [Tooltip("Il est à l'affût des fracas : leur portée compte x1,6 pour lui (même à travers un mur).")]
        public float crashHearing = 1.6f;

        [Header("Vitesses (m/s)")]
        public float patrolSpeed = 1.3f, inspectSpeed = 2.6f, huntSpeed = 5.6f;
        [Tooltip("Un cri (voix de portée >= cette valeur) le fait accourir au lieu de marcher.")]
        public float screamRadius = 16f;
        public float inspectTime = 6f, pauseAfterCatch = 4f;
        public float catchDistance = 1.4f;

        [Header("Chemins (posés par InstitutBuilder)")]
        public Vector3[] points = new Vector3[0];
        public Vector2Int[] links = new Vector2Int[0];

        [Header("Sons")]
        public AudioClip humLoop, breathLoop, beak, step, scream;
        [Tooltip("Son du screamer (unique au Docteur).")]
        public AudioClip jumpscareSound;
        public float jumpscareScale = 0.32f;   // le bec est long : plus petit, sa pointe s'arrête juste devant tes yeux

        [Header("Lecture seule")]
        public State state = State.Patrouille;

        public static readonly List<DoctorAI> All = new List<DoctorAI>();

        /// <summary>Appelé à chaque changement d'état (journal des vidéos, futur réseau).</summary>
        public System.Action<State> StateChangedHook;

        private AudioSource _hum, _breath, _fx;
        private List<int>[] _adj;
        private List<Vector3> _path = new List<Vector3>();
        private int _pathIdx, _patrolNode;
        private Vector3 _target, _lastSeen;
        private float _stateTimer, _unseen, _speed, _stepTimer, _twitchAt, _repathAt;
        private ArmLocomotion _prey;
        private Quaternion _headBase, _twitch = Quaternion.identity;
        private float _slideSign = 1f, _slideUntil;
        private bool _blocked, _rush;

        private void OnEnable() { All.Add(this); NoiseSystem.Heard += OnNoise; }
        private void OnDisable() { All.Remove(this); NoiseSystem.Heard -= OnNoise; }

        private void Start()
        {
            _hum = MakeSource(humLoop, true, 0.55f, 14f);
            _breath = MakeSource(breathLoop, true, 0.35f, 6f);
            _fx = MakeSource(null, false, 1f, 18f);
            if (head != null) _headBase = head.localRotation;
            _adj = new List<int>[points.Length];
            for (int i = 0; i < points.Length; i++) _adj[i] = new List<int>();
            foreach (Vector2Int l in links)
            {
                if (l.x < 0 || l.y < 0 || l.x >= points.Length || l.y >= points.Length) continue;
                _adj[l.x].Add(l.y); _adj[l.y].Add(l.x);
            }
            _patrolNode = Nearest(transform.position);
            NextPatrol();
        }

        private AudioSource MakeSource(AudioClip clip, bool loop, float vol, float maxDist)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.clip = clip; s.loop = loop; s.volume = vol; s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Linear; s.minDistance = 1.5f; s.maxDistance = maxDist;
            s.playOnAwake = false;
            if (loop && clip != null) s.Play();
            return s;
        }

        // ------------------------------------------------------------------ les sens

        private void OnNoise(NoiseEvent e)
        {
            if (state == State.Pause || state == State.Traque) return;
            if (e.radius < loudRadius || (e.source != null && e.source.transform.IsChildOf(transform))) return;
            if (SafeZone.Contains(e.position) || Territory.IsForeign(e.position, "Docteur")) return;
            Vector3 ears = head != null ? head.position : transform.position + Vector3.up * 2f;
            var loud = e; loud.radius *= crashHearing;
            if (NoiseSystem.Audibility(loud, ears) < 0.05f) return;
            _lastSeen = e.position;
            SetState(State.Inspecte);
            _rush = e.type == NoiseType.Voix && e.radius >= screamRadius;   // tu as crié : il accourt
            RepathTo(e.position);
            PlayFx(beak, 0.7f);
        }

        /// <summary>Le joueur le plus proche qu'il voit (lanterne, ligne de vue, pas caché) ; null sinon.</summary>
        private ArmLocomotion See()
        {
            Vector3 eyes = head != null ? head.position : transform.position + Vector3.up * 2f;
            Vector3 fwd = head != null ? Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized : transform.forward;
            if (fwd.sqrMagnitude < 0.01f) fwd = transform.forward;
            ArmLocomotion best = null; float bestD = float.MaxValue;
            foreach (ArmLocomotion p in ArmLocomotion.All)
            {
                if (p == null || !p.inputEnabled) continue;
                Vector3 h = p.HeadPosition;
                if (SafeZone.Contains(h) || Territory.IsForeign(h, "Docteur")) continue;
                Vector3 to = h - eyes; float d = to.magnitude;
                bool near = d < senseRadius;
                if (!near)
                {
                    if (d > viewRange || Vector3.Angle(fwd, to) > viewAngle) continue;
                    if (HideZone.MuffleAt(h) < 0.99f && p.Velocity.magnitude < 0.6f) continue;   // allongé dans un lit, immobile
                }
                if (Physics.Linecast(eyes, h, out RaycastHit hit, NoiseSystem.occluderLayers, QueryTriggerInteraction.Ignore)
                    && !hit.transform.IsChildOf(p.transform) && !hit.transform.IsChildOf(transform)) continue;   // mur, rideau
                if (d < bestD) { bestD = d; best = p; }
            }
            return best;
        }

        // ------------------------------------------------------------------ cerveau

        private void Update()
        {
            if (!Horror.Net.NetworkBootstrap.RunsAI) return;
            _stateTimer += Time.deltaTime;
            ArmLocomotion seen = state == State.Pause ? null : See();

            if (seen != null)
            {
                _prey = seen; _lastSeen = seen.transform.position; _unseen = 0f;
                if (state != State.Traque)
                {
                    SetState(State.Traque);
                    PlayFx(beak, 1f);
                }
            }

            switch (state)
            {
                case State.Patrouille:
                    if (FollowPath(patrolSpeed)) NextPatrol();
                    break;

                case State.Inspecte:
                    if (FollowPath(_rush ? huntSpeed : inspectSpeed))
                    {
                        Move(transform.position, 0f);
                        if (head != null)        // il balaie la pièce de sa lanterne
                            _twitch = Quaternion.Euler(0f, Mathf.Sin(_stateTimer * 1.4f) * 70f, 0f);
                        if (_stateTimer > inspectTime) { SetState(State.Patrouille); NextPatrol(); }
                    }
                    else _stateTimer = 0f;
                    break;

                case State.Traque:
                    if (seen == null)
                    {
                        _unseen += Time.deltaTime;
                        if (_unseen > loseSightTime || Arrived(_lastSeen, 0.8f))
                        {
                            SetState(State.Inspecte);
                            RepathTo(_lastSeen);
                            break;
                        }
                    }
                    // en vue : droit sur toi ; sinon, vers le dernier endroit où il t'a vu
                    Vector3 goal = _lastSeen;
                    bool direct = !Physics.Linecast(transform.position + Vector3.up * 0.8f, goal + Vector3.up * 0.6f,
                                                    NoiseSystem.occluderLayers, QueryTriggerInteraction.Ignore);
                    // tout près, il va droit sur toi en contournant (un lit, un rideau) ; de loin, il suit les couloirs
                    if (direct || Vector3.Distance(transform.position, goal) < 6f) Move(goal, huntSpeed);
                    else { if (Time.time > _repathAt) RepathTo(goal); FollowPath(huntSpeed); }
                    TryCatch();
                    break;

                case State.Pause:
                    Move(transform.position, 0f);
                    if (_stateTimer > pauseAfterCatch) { SetState(State.Patrouille); NextPatrol(); }
                    break;
            }

            Sounds();
        }

        /// <summary>L'envoie marcher jusqu'à un point (mise en scène, tests) : il y va comme pour inspecter, en fredonnant.</summary>
        public void GoTo(Vector3 p)
        {
            SetState(State.Inspecte);
            _stateTimer = 0f;
            RepathTo(p);
        }

        /// <summary>Vrai s'il est arrivé (à <paramref name="tol"/> près) au point donné.</summary>
        public bool IsNear(Vector3 p, float tol = 0.9f) => Arrived(p, tol);

        private void SetState(State s)
        {
            if (state == s) return;
            if (s != State.Inspecte) _rush = false;
            state = s; _stateTimer = 0f; _twitch = Quaternion.identity;
            StateChangedHook?.Invoke(s);
        }

        private void TryCatch()
        {
            if (_prey == null) return;
            Vector3 reach = transform.position + transform.forward * 0.6f + Vector3.up * 1.2f;
            if (Vector3.Distance(reach, _prey.HeadPosition) > catchDistance) return;
            Jumpscare js = _prey.GetComponent<Jumpscare>();
            if (js != null) js.TriggerScreamer(transform, head, jumpscareSound != null ? jumpscareSound : scream, jumpscareScale);
            else _prey.TeleportTo(_prey.SpawnPosition);
            _prey = null;
            SetState(State.Pause);
        }

        // ------------------------------------------------------------------ chemins (petit graphe de l'infirmerie)

        private int Nearest(Vector3 p)
        {
            int best = 0; float bd = float.MaxValue;
            for (int i = 0; i < points.Length; i++)
            {
                float d = (points[i] - p).sqrMagnitude;
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        private void NextPatrol()
        {
            if (points.Length == 0) return;
            List<int> n = _adj[_patrolNode];
            _patrolNode = n.Count > 0 ? n[Random.Range(0, n.Count)] : Random.Range(0, points.Length);
            RepathTo(points[_patrolNode]);
        }

        private void RepathTo(Vector3 goal)
        {
            _repathAt = Time.time + 1f;
            _target = goal;
            _path.Clear(); _pathIdx = 0;
            if (points.Length == 0) { _path.Add(goal); return; }
            // ligne droite dégagée (même salle) : il y va tout droit, sans détour par ses points de passage
            Vector3 from = transform.position + Vector3.up * 0.8f, to = goal + Vector3.up * 0.8f;
            if (!Physics.SphereCast(from, 0.35f, (to - from).normalized, out _, (to - from).magnitude, NoiseSystem.occluderLayers, QueryTriggerInteraction.Ignore))
            { _path.Add(goal); return; }
            int a = Nearest(transform.position), b = Nearest(goal);
            var prev = new int[points.Length];
            for (int i = 0; i < prev.Length; i++) prev[i] = -2;
            var q = new Queue<int>(); q.Enqueue(a); prev[a] = -1;
            while (q.Count > 0)
            {
                int c = q.Dequeue();
                if (c == b) break;
                foreach (int n in _adj[c]) if (prev[n] == -2) { prev[n] = c; q.Enqueue(n); }
            }
            var rev = new List<Vector3>();
            if (prev[b] != -2) for (int c = b; c >= 0; c = prev[c]) rev.Add(points[c]);
            rev.Reverse();
            _path.AddRange(rev);
            _path.Add(goal);
        }

        /// <summary>Avance le long du chemin ; vrai quand il est arrivé au bout.</summary>
        private bool FollowPath(float speed)
        {
            // un point bouché par un meuble : assez près, on le considère atteint (sinon il pousse le meuble à l'infini)
            while (_pathIdx < _path.Count - 1 && (Arrived(_path[_pathIdx], 0.7f) || (_blocked && Arrived(_path[_pathIdx], 1.6f)))) _pathIdx++;
            if (_pathIdx >= _path.Count - 1 && _blocked && Arrived(_path[_path.Count - 1], 1.6f)) return true;
            if (_path.Count == 0 || (_pathIdx >= _path.Count - 1 && Arrived(_path[_path.Count - 1], 0.7f))) return true;
            Move(_path[_pathIdx], speed);
            return false;
        }

        private bool Arrived(Vector3 p, float tol)
        {
            Vector3 d = p - transform.position; d.y = 0f;
            return d.magnitude <= tol;
        }

        private void Move(Vector3 goal, float speed)
        {
            _speed = Mathf.Lerp(_speed, speed, 1f - Mathf.Exp(-5f * Time.deltaTime));
            Vector3 to = goal - transform.position; to.y = 0f;
            if (to.sqrMagnitude < 0.01f || _speed < 0.02f) return;
            Vector3 dir = to.normalized;
            float stepLen = Mathf.Min(_speed * Time.deltaTime, to.magnitude);
            Vector3 origin = transform.position + Vector3.up * 0.6f;
            _blocked = false;
            if (Physics.SphereCast(origin, 0.35f, dir, out RaycastHit hit, stepLen + 0.1f, NoiseSystem.occluderLayers, QueryTriggerInteraction.Ignore)
                && !hit.transform.IsChildOf(transform))
            {
                _blocked = true;
                Vector3 slide = Vector3.ProjectOnPlane(dir, hit.normal); slide.y = 0f;
                if (slide.sqrMagnitude < 0.04f)
                {
                    Vector3 along = Vector3.Cross(Vector3.up, hit.normal); along.y = 0f;
                    if (Time.time > _slideUntil)
                    {
                        float side = Vector3.Dot(along, to);
                        _slideSign = Mathf.Abs(side) > 0.05f ? Mathf.Sign(side) : (Random.value < 0.5f ? -1f : 1f);
                    }
                    _slideUntil = Time.time + 0.8f;
                    slide = along * _slideSign;
                    if (slide.sqrMagnitude < 0.01f) return;
                }
                dir = slide.normalized;
            }
            transform.position += dir * stepLen;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir, Vector3.up), 1f - Mathf.Exp(-6f * Time.deltaTime));
            if (Physics.Raycast(transform.position + Vector3.up * 1.2f, Vector3.down, out RaycastHit g, 3f, NoiseSystem.occluderLayers, QueryTriggerInteraction.Ignore)
                && g.point.y - transform.position.y < 0.35f)
            {
                Vector3 p = transform.position; p.y = Mathf.Lerp(p.y, g.point.y, 1f - Mathf.Exp(-15f * Time.deltaTime)); transform.position = p;
            }
        }

        // ------------------------------------------------------------------ allure et sons

        private void LateUpdate()
        {
            if (head == null) return;
            // petits mouvements d'oiseau : la tête se penche d'un coup, puis reste immobile
            if (Time.time > _twitchAt && state != State.Inspecte)
            {
                _twitchAt = Time.time + Random.Range(1.2f, 3.5f);
                _twitch = state == State.Traque ? Quaternion.Euler(Random.Range(-6f, 6f), 0f, Random.Range(-12f, 12f))
                                                : Quaternion.Euler(Random.Range(-10f, 15f), Random.Range(-25f, 25f), Random.Range(-28f, 28f));
            }
            Quaternion want = _headBase * _twitch;
            if (state == State.Traque && _prey != null)          // il te fixe
            {
                Vector3 to = _prey.HeadPosition - head.position;
                want = Quaternion.Inverse(head.parent.rotation) * Quaternion.LookRotation(to, Vector3.up);
            }
            if (state == State.Pause) want = _headBase * Quaternion.Euler(30f, 0f, 0f);
            head.localRotation = Quaternion.Slerp(head.localRotation, want, 1f - Mathf.Exp((state == State.Traque ? -10f : -18f) * Time.deltaTime));
            if (lantern != null)                                 // la lanterne se balance au pas
                lantern.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 4f) * 6f * Mathf.Clamp01(_speed), 0f, Mathf.Sin(Time.time * 2f) * 4f);
            if (lanternLight != null)
                lanternLight.intensity = Mathf.Lerp(lanternLight.intensity, state == State.Traque ? 5.5f : 3.2f, Time.deltaTime * 3f)
                                         * (1f + 0.03f * Mathf.Sin(Time.time * 23f));
        }

        private void Sounds()
        {
            // il fredonne... sauf quand il t'a vu (le silence est l'alerte)
            if (_hum != null) _hum.volume = Mathf.MoveTowards(_hum.volume, state == State.Traque || state == State.Pause ? 0f : 0.55f, Time.deltaTime * 1.5f);
            if (_speed > 0.3f && step != null)
            {
                _stepTimer -= Time.deltaTime;
                if (_stepTimer <= 0f)
                {
                    _stepTimer = Mathf.Lerp(0.8f, 0.36f, _speed / huntSpeed);
                    _fx.pitch = Random.Range(0.9f, 1.05f);
                    _fx.PlayOneShot(step, 0.55f);
                }
            }
        }

        private void PlayFx(AudioClip c, float vol) { if (c != null && _fx != null) { _fx.pitch = 1f; _fx.PlayOneShot(c, vol); } }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.7f, 1f, 0.5f, 0.6f);
            for (int i = 0; i < links.Length; i++)
                if (links[i].x < points.Length && links[i].y < points.Length) Gizmos.DrawLine(points[links[i].x], points[links[i].y]);
        }
    }
}
