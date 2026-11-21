using UnityEngine;
using Horror.Noise;
using Horror.Player;
using Horror.World;

namespace Horror.Monsters
{
    /// <summary>
    /// La Veilleuse, la surveillante de nuit du grenier. Ni aveugle ni sourde : elle CRAINT LA LUMIÈRE.
    ///  - Elle ne peut pas entrer dans le halo d'une ampoule allumée (AtticBulb) : dans la lumière, tu es en sécurité.
    ///  - Elle souffle les ampoules avec son éteignoir : celles qui protègent un joueur d'abord (elle attend au bord
    ///    du halo, puis souffle), et de temps en temps une autre, pour te faire bouger. L'ampoule grésille avant.
    ///  - Dans le NOIR, si tu es à moins de 13 m, elle fonce (4,4 m/s) : à moins de 1,15 m, elle t'attrape.
    ///  - Tirer le cordon d'une ampoule la rallume : rallumée sur elle, elle recule en sifflant.
    ///  - On l'entend venir : la petite cloche de son éteignoir tinte quand elle marche, le plancher grince.
    /// L'IA ne tourne que chez celui qui fait tourner les monstres (en ligne) ; elle chasse tous les joueurs.
    /// </summary>
    public class VeilleuseAI : MonoBehaviour, IScreamer
    {
        public enum State { Errance, Traque, Guet, Souffle, Pause }

        public Transform head, face;
        public AudioClip jumpscareSound, bellLoop, creak;
        [Tooltip("Le grenier (en coordonnées monde) : elle n'en sort pas, et ne chasse que les joueurs dedans.")]
        public Bounds zone;
        public float wanderSpeed = 1.1f, huntSpeed = 4.4f, catchDistance = 1.15f, senseRange = 13f;
        public Vector2 snuffEvery = new Vector2(18f, 30f);
        [Tooltip("Portée de l'éteignoir : elle souffle une ampoule à cette distance (depuis le bord du noir).")]
        public float snuffReach = 3.4f;
        public float pauseAfterCatch = 4f;
        [Tooltip("Temps d'arrêt quand elle te repère dans le noir, avant de foncer (le temps de fuir vers la lumière).")]
        public float windUp = 0.9f;
        [Tooltip("Le coup de cloche quand elle te repère.")]
        public AudioClip warnBell;
        [Tooltip("Jusqu'où elle attrape en hauteur (m au-dessus de ses pieds) : grimper aux poutres ne suffit pas.")]
        public float reachHeight = 3.3f;

        public State state;

        public Transform ScreamBody => transform;
        public Transform ScreamFace => face != null ? face : head;
        public AudioClip ScreamClip => jumpscareSound;
        public float ScreamScale => 0.42f;

        private AtticBulb _target;
        private Vector3 _wanderTo;
        private float _timer, _nextSnuff, _speed, _creakDist, _guetTime, _empty;
        private Vector3 _lastPos;
        private AudioSource _bell;
        private Quaternion _headBase;

        private void Start()
        {
            _nextSnuff = Time.time + Random.Range(snuffEvery.x, snuffEvery.y);
            _wanderTo = transform.position;
            _lastPos = transform.position;
            if (head != null) _headBase = head.localRotation;
            if (bellLoop != null)
            {
                _bell = gameObject.AddComponent<AudioSource>();
                _bell.clip = bellLoop; _bell.loop = true; _bell.spatialBlend = 1f; _bell.volume = 0f;
                _bell.rolloffMode = AudioRolloffMode.Linear; _bell.minDistance = 2f; _bell.maxDistance = 22f;
                _bell.Play();
            }
        }

        private PlayerTarget NearestPrey()
        {
            PlayerTarget best = null; float bd = float.MaxValue;
            foreach (PlayerTarget p in PlayerTargets.All)
            {
                if (p == null || !p.inputEnabled) continue;
                Vector3 h = p.HeadPosition;
                if (!zone.Contains(h) || SafeZone.Contains(h)) continue;
                AtticBulb refuge = AtticBulb.CoveringBulb(h);
                if (refuge != null && !refuge.snuffable) continue;        // sous la lampe de la cage : intouchable
                float d = (h - transform.position).sqrMagnitude;
                if (d < bd) { bd = d; best = p; }
            }
            return best;
        }

        private void Update()
        {
            if (!Horror.Net.NetworkBootstrap.RunsAI) return;
            float dt = Time.deltaTime;
            _timer += dt;

            // rallumée sur elle : elle recule hors du halo, en sifflant (la lumière la brûle)
            AtticBulb burning = AtticBulb.CoveringBulb(transform.position + Vector3.up);
            if (burning != null && state != State.Pause)
            {
                Vector3 away = transform.position - burning.transform.position; away.y = 0f;
                if (away.sqrMagnitude < 0.01f) away = transform.forward;
                Move(transform.position + away.normalized * 3f, huntSpeed * 0.8f, ignoreLight: true);
                Sounds(dt); return;
            }

            PlayerTarget prey = NearestPrey();
            // grenier vide depuis 20 s : les ampoules reviennent comme au début (personne n'arrive dans le noir total)
            if (prey == null)
            {
                _empty += dt;
                if (_empty > 20f)
                {
                    _empty = float.NegativeInfinity;
                    foreach (AtticBulb b in AtticBulb.All)
                        if (b != null && b.IsLit != b.startLit && !b.Flickering) b.SetLit(b.startLit, true);
                }
            }
            else
            {
                if (_empty != 0f) _nextSnuff = Time.time + Random.Range(8f, 14f);   // quelqu'un arrive : un répit avant la première ampoule
                _empty = 0f;
            }
            switch (state)
            {
                case State.Pause:
                    if (_timer > pauseAfterCatch) SetState(State.Errance);
                    break;

                case State.Souffle:
                    if (_target == null || !_target.IsLit) { SetState(prey != null ? State.Guet : State.Errance); break; }
                    Vector3 edge = EdgeOf(_target, prey != null ? prey.HeadPosition : transform.position);
                    if (Flat(_target.transform.position - transform.position) <= snuffReach)
                    {
                        _target.Snuff();                                 // grésille 1,5 s puis s'éteint
                        _nextSnuff = Time.time + Random.Range(snuffEvery.x, snuffEvery.y);
                        SetState(prey != null ? State.Guet : State.Errance);
                    }
                    else Move(edge, huntSpeed * 0.7f);
                    break;

                default:
                    if (prey != null)
                    {
                        Vector3 h = prey.HeadPosition;
                        AtticBulb safe = AtticBulb.CoveringBulb(h);
                        float d = Flat(h - transform.position);
                        if (safe == null && d < senseRange)
                        {
                            if (state != State.Traque) { SetState(State.Traque); if (warnBell != null) AudioSource.PlayClipAtPoint(warnBell, transform.position, 1f); }
                            if (_timer < windUp) break;                          // elle se fige et te regarde... puis fonce
                            Move(h, huntSpeed);
                            if (d < catchDistance && h.y - transform.position.y < reachHeight)
                            {
                                prey.Catch(this);
                                SetState(State.Pause);
                            }
                            break;
                        }
                        if (safe != null && d < senseRange + 4f)
                        {
                            // dans la lumière : elle attend au bord du halo... puis vient souffler cette ampoule
                            if (state != State.Guet) { SetState(State.Guet); _guetTime = Random.Range(4f, 7f); }
                            Move(EdgeOf(safe, h), wanderSpeed * 1.6f);
                            if (_timer > _guetTime && !safe.Flickering && safe.snuffable) { _target = safe; SetState(State.Souffle); }
                            break;
                        }
                    }
                    if (state != State.Errance) SetState(State.Errance);
                    Wander(prey);
                    break;
            }
            LookAt(prey);
            Sounds(dt);
        }

        private void Wander(PlayerTarget prey)
        {
            if (prey != null && Time.time > _nextSnuff)                         // elle ne souffle que s'il y a quelqu'un
            {
                // de temps en temps : souffler l'ampoule allumée la plus proche d'un joueur (pour le faire bouger)
                AtticBulb pick = null; float bd = float.MaxValue;
                Vector3 from = prey != null ? prey.HeadPosition : transform.position;
                foreach (AtticBulb b in AtticBulb.All)
                {
                    if (b == null || !b.IsLit || !b.snuffable || !zone.Contains(b.transform.position)) continue;
                    float d = (b.transform.position - from).sqrMagnitude;
                    if (d < bd) { bd = d; pick = b; }
                }
                if (pick != null) { _target = pick; SetState(State.Souffle); return; }
                _nextSnuff = Time.time + Random.Range(snuffEvery.x, snuffEvery.y);
            }
            if (Flat(_wanderTo - transform.position) < 0.8f || _timer > 9f)
            {
                for (int i = 0; i < 12; i++)
                {
                    Vector3 c = new Vector3(Random.Range(zone.min.x + 1f, zone.max.x - 1f), transform.position.y, Random.Range(zone.min.z + 1f, zone.max.z - 1f));
                    if (!AtticBulb.IsLitAt(c + Vector3.up)) { _wanderTo = c; break; }
                }
                _timer = 0f;
            }
            Move(_wanderTo, wanderSpeed);
        }

        /// <summary>Un point juste hors du halo de l'ampoule, du côté du joueur (ou d'elle).</summary>
        private Vector3 EdgeOf(AtticBulb b, Vector3 toward)
        {
            Vector3 c = b.transform.position; c.y = transform.position.y;
            Vector3 dir = transform.position - c; dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) { dir = toward - c; dir.y = 0f; }
            return c + dir.normalized * (b.radius + 0.4f);
        }

        private void Move(Vector3 goal, float speed, bool ignoreLight = false)
        {
            goal.y = transform.position.y;
            Vector3 to = goal - transform.position;
            if (to.sqrMagnitude < 0.0004f) return;
            Vector3 dir = to.normalized;
            float step = Mathf.Min(speed * Time.deltaTime, to.magnitude);
            Vector3 origin = transform.position + Vector3.up * 0.8f;
            if (Physics.SphereCast(origin, 0.3f, dir, out RaycastHit hit, step + 0.15f, NoiseSystem.occluderLayers, QueryTriggerInteraction.Ignore)
                && !hit.transform.IsChildOf(transform))
            {
                Vector3 slide = Vector3.ProjectOnPlane(dir, hit.normal); slide.y = 0f;
                if (slide.sqrMagnitude < 0.04f) slide = Vector3.Cross(Vector3.up, hit.normal);
                dir = slide.normalized;
            }
            Vector3 next = transform.position + dir * step;
            if (!ignoreLight && AtticBulb.IsLitAt(next + Vector3.up))
            {
                // elle ne franchit pas la lumière : elle la contourne (on essaie de tourner de plus en plus)
                bool found = false;
                foreach (float a in new[] { 40f, -40f, 80f, -80f, 120f, -120f })
                {
                    Vector3 d2 = Quaternion.Euler(0f, a, 0f) * dir;
                    Vector3 n2 = transform.position + d2 * step;
                    if (AtticBulb.IsLitAt(n2 + Vector3.up)) continue;
                    dir = d2; next = n2; found = true; break;
                }
                if (!found) return;
            }
            next.x = Mathf.Clamp(next.x, zone.min.x + 0.4f, zone.max.x - 0.4f);
            next.z = Mathf.Clamp(next.z, zone.min.z + 0.4f, zone.max.z - 0.4f);
            transform.position = next;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir, Vector3.up), 1f - Mathf.Exp(-6f * Time.deltaTime));
        }

        private void LookAt(PlayerTarget prey)
        {
            if (head == null) return;
            Quaternion want = _headBase;
            if (prey != null && state != State.Errance)
            {
                Vector3 local = transform.InverseTransformPoint(prey.HeadPosition);
                want = _headBase * Quaternion.Euler(Mathf.Clamp(-Mathf.Atan2(local.y - 2f, local.z) * Mathf.Rad2Deg, -30f, 30f), Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -60f, 60f), 0f);
            }
            if (state == State.Pause) want = _headBase * Quaternion.Euler(28f, 0f, 0f);
            head.localRotation = Quaternion.Slerp(head.localRotation, want, 1f - Mathf.Exp(-5f * Time.deltaTime));
        }

        private void Sounds(float dt)
        {
            Vector3 mv = transform.position - _lastPos; mv.y = 0f; _lastPos = transform.position;
            _speed = Mathf.Lerp(_speed, dt > 0f ? mv.magnitude / dt : 0f, 1f - Mathf.Exp(-6f * dt));
            if (_bell != null) _bell.volume = Mathf.Clamp01(_speed / 1.2f) * 0.7f;   // la cloche tinte quand elle marche
            _creakDist += mv.magnitude;
            if (_creakDist > 1.1f && creak != null)
            {
                _creakDist = 0f;
                AudioSource.PlayClipAtPoint(creak, transform.position, 0.6f);
            }
        }

        private void SetState(State s) { state = s; _timer = 0f; }
        private static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }
    }
}
