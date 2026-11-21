using System;
using Horror.Locomotion;
using Horror.Noise;
using Horror.Player;
using Horror.World;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace Horror.Monsters
{
    /// <summary>
    /// L'Écoute : un monstre AVEUGLE qui chasse au son.
    ///
    /// Elle ne voit rien. Elle réagit aux bruits émis via NoiseSystem (pas, voix, objets, portes).
    ///  - ERRANCE   : elle marche lentement d'un point de ronde à l'autre, ses clés tintent.
    ///  - ÉCOUTE    : un bruit faible ? Elle s'arrête net et tourne la tête vers lui.
    ///  - ENQUÊTE   : elle va voir là où elle a entendu quelque chose, puis fouille sur place.
    ///  - CHASSE    : bruit fort ou bruits répétés : elle FONCE vers le dernier bruit entendu.
    /// Elle attrape un joueur seulement si elle le touche : rester immobile et silencieux peut
    /// la faire passer juste à côté de toi.
    ///
    /// Déplacement : utilise un NavMeshAgent s'il y a un NavMesh (vraie map), sinon avance tout
    /// droit en glissant le long des murs (pratique pour la salle de test).
    /// Multijoueur (étape 6) : ce script ne tournera que chez l'hôte.
    /// </summary>
    public class BlindMonsterAI : MonoBehaviour
    {
        public enum State { Errance, Ecoute, Enquete, Chasse }

        [Header("Références")]
        [Tooltip("La tête du modèle (noeud \"Tete\" de LEcoute). Elle se tourne vers les bruits.")]
        public Transform head;
        [Tooltip("Points de ronde. Vide = elle erre autour de son point de départ.")]
        public Transform[] patrolPoints;
        [Tooltip("Son des clés en boucle (son volume suit la vitesse). Facultatif.")]
        public AudioSource keysLoop;
        [Tooltip("Cri joué quand elle passe en chasse. Facultatif.")]
        public AudioSource screamSource;
        [Tooltip("Son du screamer quand elle t'attrape (unique à ce monstre).")]
        public AudioClip jumpscareSound;

        [Header("Ouïe")]
        [Tooltip("Multiplie la portée de tous les bruits pour elle (1 = normal, 1.5 = ouïe très fine).")]
        public float hearingMultiplier = 1.2f;
        [Tooltip("Audibilité (0..1) au-dessus de laquelle elle passe directement en chasse.")]
        [Range(0f, 1f)] public float chaseThreshold = 0.55f;
        [Tooltip("Chaque bruit entendu remplit sa 'colère'. Au-dessus de 1 : chasse.")]
        public float alertGainPerNoise = 0.35f;
        [Tooltip("Vitesse à laquelle sa colère redescend (par seconde).")]
        public float alertDecay = 0.12f;

        [Header("Vitesses (m/s)")]
        public float patrolSpeed = 1.1f;
        public float investigateSpeed = 2.6f;
        [Tooltip("Vitesse de chasse. 6,2 était intenable pour un débutant ; ~4,9 : on galère, mais on peut s'en sortir.")]
        public float chaseSpeed = 5.8f;
        [Tooltip("La charge finale : tout près du dernier bruit (et bruit récent), elle fonce. De loin, on peut encore la semer.")]
        public float chargeSpeed = 8.5f;
        [Tooltip("Distance (m) au dernier bruit sous laquelle elle charge.")]
        public float chargeDistance = 7f;
        [Tooltip("En chasse, si elle n'entend plus rien depuis 1 s, elle ralentit (elle écoute en courant).")]
        [Range(0.3f, 1f)] public float unsureSpeedFactor = 0.72f;
        [Tooltip("Temps pendant lequel elle reste figée à écouter avant de bouger.")]
        public float listenFreeze = 0.9f;
        [Tooltip("Temps passé à fouiller l'endroit d'un bruit avant d'abandonner.")]
        public float searchTime = 4f;

        [Header("Te perdre (elle ne se fie qu'au son)")]
        [Tooltip("En chasse : sans rien entendre pendant ce temps (s), elle te perd et fouille le dernier endroit.")]
        public float chaseLoseTime = 2.5f;
        [Tooltip("En chasse : un bruit plus faible que ça (audibilité 0..1) ne la remet pas sur ta piste. Loin et doucement = semée.")]
        [Range(0f, 1f)] public float chaseMinAudibility = 0.12f;

        [Header("Après avoir attrapé quelqu'un")]
        [Tooltip("Elle reste penchée sur l'endroit (s) avant de repartir : les autres ont le temps de fuir.")]
        public float pauseAfterCatch = 4f;

        [Header("Attraper")]
        [Tooltip("Distance (m) entre ses mains tendues et la tête d'un joueur pour l'attraper.")]
        public float catchDistance = 1.3f;
        [Tooltip("Appelé quand elle attrape quelqu'un (jumpscare, son, écran noir...).")]
        public UnityEvent<GameObject> onCatch = new UnityEvent<GameObject>();
        [Tooltip("Renvoie le joueur attrapé à son point de départ (comportement par défaut).")]
        public bool respawnCaughtPlayer = true;

        [Header("Debug (lecture seule)")]
        public State state = State.Errance;
        [Range(0f, 2f)] public float alert;

        /// <summary>Changement d'état (ancien, nouveau) : la voix et les sons s'y abonnent.</summary>
        public event Action<State, State> StateChanged;
        /// <summary>Un bruit vient d'être entendu (le bruit, à quel point : 0..1).</summary>
        public event Action<NoiseEvent, float> NoiseHeard;
        /// <summary>Vitesse actuelle (m/s), pour caler les bruits de pas.</summary>
        public float CurrentSpeed => _currentSpeed;
        /// <summary>La tête du monstre (utilisée aussi par le jumpscare).</summary>
        public Transform Head => head;

        private NavMeshAgent _agent;
        private Vector3 _target;
        private Vector3 _home;
        private float _stateTimer;
        private float _lastNoiseTime = -100f;
        private Vector3 _lastNoisePos;
        private int _patrolIndex;
        private Quaternion _headBase;
        private Vector3 _lookTarget;
        private float _currentSpeed;
        private float _catchCooldown;
        private float _pauseTimer;

        /// <summary>Vrai pendant la pause après une prise (elle ne bouge pas et n'entend rien).</summary>
        public bool IsPaused => _pauseTimer > 0f;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _home = transform.position;
            _target = _home;
            if (head != null) _headBase = head.localRotation;
        }

        /// <summary>Tous les monstres actifs (pour la tension, les lumières qui grésillent...).</summary>
        public static readonly System.Collections.Generic.List<BlindMonsterAI> All = new System.Collections.Generic.List<BlindMonsterAI>();

        private void OnEnable() { NoiseSystem.Heard += OnNoise; All.Add(this); }
        private void OnDisable() { NoiseSystem.Heard -= OnNoise; All.Remove(this); }

        // ------------------------------------------------------------------ oreilles

        private void OnNoise(NoiseEvent e)
        {
            // La Salle de jeux n'a pas de micro : elle n'entend rien de ce qui s'y passe.
            if (SafeZone.Contains(e.position)) return;
            // Le territoire d'un autre monstre (la Laverie du Drap) : elle n'y entend rien, elle n'y va pas.
            if (Territory.IsForeign(e.position, "Ecoute")) return;

            Vector3 ears = head != null ? head.position : transform.position + Vector3.up * 2f;
            var boosted = e;
            boosted.radius *= hearingMultiplier;
            float heard = NoiseSystem.Audibility(boosted, ears);
            if (heard <= 0f || IsPaused) return;
            // en pleine course, un bruit trop faible ne la remet pas sur ta piste : on peut la semer en s'éloignant doucement
            if (state == State.Chasse && heard < chaseMinAudibility) return;
            NoiseHeard?.Invoke(e, heard);

            _lastNoisePos = e.position;
            _lastNoiseTime = Time.time;
            _lookTarget = e.position;
            alert = Mathf.Min(2f, alert + alertGainPerNoise * (0.5f + heard));

            if (heard >= chaseThreshold || alert >= 1f)
            {
                if (state != State.Chasse && screamSource != null) screamSource.Play();
                SetState(State.Chasse);
                _target = e.position;
            }
            else if (state == State.Errance)
            {
                SetState(State.Ecoute);   // elle se fige et tend l'oreille
            }
            else if (state == State.Enquete || state == State.Ecoute)
            {
                _target = e.position;     // nouveau bruit : elle corrige sa route
                if (state == State.Enquete) _stateTimer = 0f;
            }
            else if (state == State.Chasse)
            {
                _target = e.position;
            }
        }

        // ------------------------------------------------------------------ cerveau

        private void Update()
        {
            _stateTimer += Time.deltaTime;
            alert = Mathf.Max(0f, alert - alertDecay * Time.deltaTime);
            _catchCooldown -= Time.deltaTime;

            if (_pauseTimer > 0f)
            {
                // penchée sur l'endroit où elle a attrapé quelqu'un : immobile, sourde, le temps que les autres fuient
                _pauseTimer -= Time.deltaTime;
                MoveTowards(transform.position, 0f);
                if (_pauseTimer <= 0f) SetState(State.Errance);
                UpdateKeysSound();
                return;
            }

            switch (state)
            {
                case State.Errance:
                    MoveTowards(CurrentPatrolPoint(), patrolSpeed);
                    if (Arrived(CurrentPatrolPoint(), 0.6f)) NextPatrolPoint();
                    break;

                case State.Ecoute:
                    MoveTowards(transform.position, 0f); // figée
                    if (_stateTimer >= listenFreeze)
                    {
                        _target = _lastNoisePos;
                        SetState(State.Enquete);
                    }
                    break;

                case State.Enquete:
                    if (!Arrived(_target, 0.8f))
                    {
                        MoveTowards(_target, investigateSpeed);
                        _stateTimer = 0f;
                    }
                    else
                    {
                        MoveTowards(transform.position, 0f);
                        // Elle fouille : la tête balaie de gauche à droite
                        _lookTarget = transform.position + Quaternion.Euler(0f, Mathf.Sin(Time.time * 1.7f) * 70f, 0f) * transform.forward * 3f;
                        if (_stateTimer >= searchTime) SetState(State.Errance);
                    }
                    break;

                case State.Chasse:
                    float quiet = Time.time - _lastNoiseTime;
                    Vector3 gap = _target - transform.position; gap.y = 0f;
                    // près du dernier bruit, elle s'engage : elle fonce sur l'endroit (si tu t'es éloigné sans bruit, elle charge le vide)
                    float run = gap.magnitude < chargeDistance ? chargeSpeed
                              : quiet > 1f ? chaseSpeed * unsureSpeedFactor
                              : chaseSpeed;
                    MoveTowards(_target, run);
                    // Plus rien entendu : arrivée au dernier bruit, ou trop longtemps sans indice -> elle t'a perdu, elle fouille
                    if ((Arrived(_target, 1f) && quiet > 1.5f) || (quiet > chaseLoseTime && gap.magnitude > chargeDistance))
                    {
                        alert = 0.6f;
                        SetState(State.Enquete);
                        _stateTimer = 0f;
                    }
                    break;
            }

            TryCatchPlayers();
            UpdateKeysSound();
        }

        private void LateUpdate()
        {
            if (head == null) return;
            // Tourne la tête vers le dernier bruit (seulement autour de l'axe vertical, max 80°)
            Vector3 dir = _lookTarget - head.position;
            dir.y = 0f;
            float yaw = 0f;
            if ((dir.sqrMagnitude > 0.01f && Time.time - _lastNoiseTime < 4f) || state == State.Enquete)
                yaw = Mathf.Clamp(Vector3.SignedAngle(transform.forward, dir, Vector3.up), -80f, 80f);
            Quaternion targetRot = _headBase * Quaternion.Euler(0f, yaw, 0f);
            if (IsPaused) targetRot = _headBase * Quaternion.Euler(28f, 0f, 0f);   // tête penchée vers le sol
            float snap = Time.time - _lastNoiseTime < 0.2f ? 30f : 4f; // se tourne d'un coup vers un bruit
            head.localRotation = Quaternion.Slerp(head.localRotation, targetRot, 1f - Mathf.Exp(-snap * Time.deltaTime));
        }

        // ------------------------------------------------------------------ déplacement

        private void MoveTowards(Vector3 target, float speed)
        {
            target = SafeZone.KeepOutside(target);   // elle s'arrête au bord de la zone sûre
            _currentSpeed = Mathf.Lerp(_currentSpeed, speed, 1f - Mathf.Exp(-6f * Time.deltaTime));

            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.speed = _currentSpeed;
                _agent.acceleration = 30f;
                _agent.angularSpeed = 540f;
                if (speed <= 0f) _agent.isStopped = true;
                else
                {
                    _agent.isStopped = false;
                    _agent.SetDestination(target);
                }
                return;
            }

            // Sans NavMesh : de point de passage en point de passage (WaypointGraph) s'il y en a un,
            // sinon tout droit, en glissant le long des murs.
            OpenDoorsOnTheWay();
            Vector3 goal = WaypointGraph.Instance != null ? NextWaypoint(target) : target;
            Vector3 to = goal - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.01f || _currentSpeed < 0.01f) return;
            Vector3 dir = to.normalized;
            float step = Mathf.Min(_currentSpeed * Time.deltaTime, to.magnitude);
            Vector3 origin = transform.position + Vector3.up * 0.5f;
            if (Physics.SphereCast(origin, 0.35f, dir, out RaycastHit hit, step + 0.1f, NoiseSystem.occluderLayers, QueryTriggerInteraction.Ignore)
                && hit.transform != transform && !hit.transform.IsChildOf(transform))
            {
                Vector3 slide = Vector3.ProjectOnPlane(dir, hit.normal);
                slide.y = 0f;
                if (slide.sqrMagnitude < 0.04f)
                {
                    // presque de face contre le mur (le bord d'une porte) : elle longe le mur du côté de son but,
                    // au lieu de rester plantée là (elle restait bloquée dans le hall en pleine chasse)
                    // elle choisit un côté et s'y tient un moment, sinon elle hésite sur place (gros meuble pile en face)
                    Vector3 along = Vector3.Cross(Vector3.up, hit.normal);
                    along.y = 0f;
                    if (Time.time > _slideUntil)
                    {
                        float side = Vector3.Dot(along, to);
                        _slideSign = Mathf.Abs(side) > 0.05f ? Mathf.Sign(side) : (UnityEngine.Random.value < 0.5f ? -1f : 1f);
                    }
                    _slideUntil = Time.time + 0.8f;
                    slide = along * _slideSign;
                    if (slide.sqrMagnitude < 0.01f) return;
                }
                dir = slide.normalized;
            }
            transform.position += dir * step;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir, Vector3.up), 1f - Mathf.Exp(-8f * Time.deltaTime));
            SnapToGround();
        }

        // ------------------------------------------------------------------ navigation sans NavMesh

        private System.Collections.Generic.List<Vector3> _path;
        private int _pathIndex;
        private Vector3 _pathTarget;
        private float _repathAt;
        private float _slideSign = 1f, _slideUntil;

        /// <summary>Le prochain point à atteindre sur le chemin vers "target" (recalculé si la cible bouge).</summary>
        private Vector3 NextWaypoint(Vector3 target)
        {
            if (_path == null || (target - _pathTarget).sqrMagnitude > 1f || Time.time > _repathAt)
            {
                _path = WaypointGraph.Instance.Path(transform.position, target);
                _pathIndex = 0;
                _pathTarget = target;
                _repathAt = Time.time + 1.5f;
            }
            while (_pathIndex < _path.Count - 1 && Arrived(_path[_pathIndex], 0.7f)) _pathIndex++;
            return _path[_pathIndex];
        }

        /// <summary>Elle a toutes les clés : elle ouvre les portes du Protocole Verrou en passant.</summary>
        private void OpenDoorsOnTheWay()
        {
            foreach (LockDoor door in LockDoor.All)
            {
                if (door == null || door.IsOpen) continue;
                Vector3 d = door.transform.position - transform.position;
                d.y = 0f;
                if (d.magnitude < 1.8f && Mathf.Abs(door.transform.position.y - transform.position.y) < 2f) door.OpenByMonster();
            }
        }

        /// <summary>Suit le sol (escaliers, rampes) : sans NavMesh, personne ne le fait à sa place.</summary>
        private void SnapToGround()
        {
            if (Physics.Raycast(transform.position + Vector3.up * 1.2f, Vector3.down, out RaycastHit ground, 3f,
                                NoiseSystem.occluderLayers, QueryTriggerInteraction.Ignore))
            {
                Vector3 p = transform.position;
                if (ground.point.y - p.y > 0.35f) return;   // plus haut qu'une marche (un meuble) : elle ne grimpe pas dessus
                p.y = Mathf.Lerp(p.y, ground.point.y, 1f - Mathf.Exp(-15f * Time.deltaTime));
                transform.position = p;
            }
        }

        private bool Arrived(Vector3 point, float tolerance)
        {
            Vector3 d = point - transform.position;
            d.y = 0f;
            return d.magnitude <= tolerance;
        }

        private Vector3 CurrentPatrolPoint()
        {
            if (patrolPoints != null && patrolPoints.Length > 0 && patrolPoints[_patrolIndex] != null)
                return patrolPoints[_patrolIndex].position;
            return _target;
        }

        private void NextPatrolPoint()
        {
            if (patrolPoints != null && patrolPoints.Length > 0)
            {
                _patrolIndex = (_patrolIndex + 1) % patrolPoints.Length;
                return;
            }
            // Pas de points de ronde : un point au hasard autour de la maison
            Vector2 r = UnityEngine.Random.insideUnitCircle * 5f;
            _target = _home + new Vector3(r.x, 0f, r.y);
        }

        private void SetState(State next)
        {
            State previous = state;
            state = next;
            if (previous != next) StateChanged?.Invoke(previous, next);
            _stateTimer = 0f;
            if (next == State.Errance) NextPatrolPoint();
        }

        // ------------------------------------------------------------------ attraper

        private void TryCatchPlayers()
        {
            if (_catchCooldown > 0f) return;
            Vector3 reach = transform.position + transform.forward * 0.6f + Vector3.up * 1.3f;

            foreach (ArmLocomotion player in ArmLocomotion.All)
            {
                if (player == null || SafeZone.Contains(player.HeadPosition) || Territory.IsForeign(player.HeadPosition, "Ecoute")) continue;
                if (Vector3.Distance(reach, player.HeadPosition) > catchDistance) continue;

                _catchCooldown = 3f;
                onCatch.Invoke(player.gameObject);
                // Jumpscare s'il est installé sur le joueur (il s'occupe du respawn), sinon respawn direct
                Jumpscare scare = player.GetComponent<Jumpscare>();
                if (scare != null) scare.Trigger(this);
                else if (respawnCaughtPlayer) player.TeleportTo(player.SpawnPosition);
                alert = 0f;
                _pauseTimer = pauseAfterCatch;   // elle s'arrête, puis repart en errance
                break;
            }
        }

        private void UpdateKeysSound()
        {
            if (keysLoop == null) return;
            float t = Mathf.Clamp01(_currentSpeed / chaseSpeed);
            keysLoop.volume = Mathf.Lerp(0.05f, 1f, t);
            keysLoop.pitch = Mathf.Lerp(0.9f, 1.25f, t);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = state == State.Chasse ? Color.red : state == State.Enquete ? Color.yellow : Color.cyan;
            Gizmos.DrawWireSphere(_target, 0.4f);
            Gizmos.DrawLine(transform.position, _target);
        }
    }
}
