using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace Horror.Locomotion
{
    /// <summary>
    /// Locomotion "aux bras" : le joueur se déplace en poussant sur le décor avec ses mains,
    /// locomotion aux bras (on pousse le sol avec les mains). Pas de joystick.
    ///
    /// Principe, en une phrase : quand une main touche une surface, elle reste "collée" à l'endroit
    /// du contact, et c'est le CORPS qui bouge à la place de la main. Pousser le sol vers le bas
    /// soulève donc le corps ; tirer la main vers le bas sur un mur fait grimper.
    /// Quand on lâche toutes les surfaces, le corps garde l'élan des derniers mouvements (le saut).
    ///
    /// Mise en place (le menu Horror > 3 - Créer la salle de test VR le fait pour toi) :
    ///  - ce script est sur l'objet "Joueur", avec un Rigidbody et un CapsuleCollider ;
    ///  - la tête (caméra) et les deux manettes sont des ENFANTS de "Joueur" et suivent le casque ;
    ///  - le Joueur est sur le calque "Ignore Raycast" pour que ses mains ne se cognent pas à son propre corps.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    public class ArmLocomotion : MonoBehaviour
    {
        [Header("Rig XR (enfants de cet objet)")]
        [Tooltip("La caméra du casque.")]
        public Transform head;
        [Tooltip("L'objet qui suit la manette gauche.")]
        public Transform leftController;
        [Tooltip("L'objet qui suit la manette droite.")]
        public Transform rightController;

        [Header("Mains visibles (facultatif)")]
        [Tooltip("Modèle de la main gauche. Il reste collé au mur au lieu de passer à travers.")]
        public Transform leftHandVisual;
        [Tooltip("Modèle de la main droite.")]
        public Transform rightHandVisual;

        public enum Preset { Classique, Rapide, Personnalise }

        [Header("Mains")]
        [Tooltip("Classique : sauts mesurés (la version d'origine). Rapide : grands élans, mains qui accrochent plus " +
                 "et glissent le long des murs (pour courir sur les parois). Personnalisé : les valeurs ci-dessous.")]
        public Preset preset = Preset.Rapide;

        [Tooltip("Rayon de la 'boule' de collision de chaque main, en mètres.")]
        public float handRadius = 0.07f;
        [Tooltip("Calques sur lesquels les mains peuvent s'appuyer. Le calque du joueur doit être EXCLU.")]
        public LayerMask climbableLayers = Physics.DefaultRaycastLayers;
        [Tooltip("Distance max entre la tête et une main (évite les bras infinis).")]
        public float maxArmLength = 1.4f;
        [Tooltip("Si la vraie manette s'éloigne de plus de ça de la main collée, la main se décroche.")]
        public float unstickDistance = 0.3f;
        [Tooltip("Décoller la main de la surface de plus de ça (vers l'extérieur) la libère.")]
        public float releaseDistance = 0.015f;

        [Header("Corps")]
        [Tooltip("Hauteur du corps sous les yeux. Plus c'est grand, plus le sol virtuel est bas " +
                 "(et plus il faut se pencher pour le toucher). ~0.9 = mains qui touchent le sol bras tendus.")]
        public float bodyHeight = 0.9f;
        [Tooltip("Hauteur de la COLLISION du corps (sous les yeux). Trop courte (0,45), les testeurs « s'enfonçaient » dans le " +
                 "sol dès qu'ils levaient les mains ; 0,85 : debout, les mains pendantes effleurent le sol et le portent.")]
        public float bodyColliderHeight = 0.85f;
        [Tooltip("Rayon du corps.")]
        public float bodyRadius = 0.2f;
        [Tooltip("Les BRAS ont une hitbox (épaule → poignet, sans les doigts) : un bras ne traverse plus un mur, il repousse " +
                 "le corps. Les mains gardent leurs propres boules de contact.")]
        public bool armHitbox = true;
        [Tooltip("Rayon de la hitbox des bras (m).")]
        public float armRadius = 0.055f;
        [Tooltip("Petite marge pour ne pas coller le corps aux murs lors des déplacements.")]
        public float skinWidth = 0.01f;

        [Header("Élan / saut")]
        [Tooltip("Multiplie la vitesse au moment de lâcher. 1 = réaliste, plus = sauts plus amples.")]
        public float jumpMultiplier = 1.2f;
        [Tooltip("Vitesse maximale donnée par un saut (m/s).")]
        public float maxJumpSpeed = 7f;
        [Tooltip("Nombre d'images utilisées pour calculer la vitesse au moment de lâcher.")]
        [Range(2, 20)] public int velocityHistorySize = 6;
        [Tooltip("En dessous de cette vitesse au lâcher, pas d'élan (évite de dériver après un geste lent).")]
        public float minLaunchSpeed = 0.35f;
        [Tooltip("Frottement du corps sur le sol. Bas (Rapide) : l'élan continue en glissant un peu à l'atterrissage, " +
                 "comme dans les jeux de bras ; 0,6 : le corps freine net.")]
        [Range(0f, 1f)] public float bodyFriction = 0.6f;
        [Tooltip("Glisse d'une main appuyée le long de la surface (0 = collée, 0,05 = glisse un peu : on « court » sur les murs).")]
        [Range(0f, 0.3f)] public float surfaceSlide = 0f;

        [Header("Physique")]
        [Tooltip("Cale la fréquence de la physique sur celle du casque (72/90/120 Hz). " +
                 "Sans ça, Unity calcule à 50 Hz et les mains paraissent saccadées.")]
        public bool matchPhysicsRateToHeadset = true;
        [Tooltip("Sous cette hauteur, le joueur est renvoyé à son point de départ (chute hors de la map).")]
        public float respawnBelowY = -30f;

        [Header("Vibrations et impacts")]
        [Tooltip("Vitesse minimale de la main pour compter comme un impact (vibration + bruit).")]
        public float minImpactSpeed = 0.5f;
        [Range(0f, 1f)] public float hapticStrength = 0.35f;
        public float hapticDuration = 0.05f;

        /// <summary>
        /// Appelé quand une main tape une surface : (position, vitesse de l'impact en m/s, main gauche ?).
        /// Servira à l'étape 3 : le monstre aveugle entendra ces bruits de "pas".
        /// </summary>
        public event Action<Vector3, float, bool> HandImpact;

        /// <summary>Pareil, avec la surface touchée (pour les bruits de pas selon le sol : bois, carrelage...).</summary>
        public event Action<Vector3, float, bool, Collider> HandImpactOn;

        /// <summary>Tous les joueurs présents (les monstres s'en servent pour savoir qui ils touchent).</summary>
        public static readonly List<ArmLocomotion> All = new List<ArmLocomotion>();

        /// <summary>Vrai si au moins une main est appuyée sur une surface.</summary>
        public bool IsTouchingSurface => _left.touching || _right.touching;

        /// <summary>Vitesse actuelle du corps (m/s).</summary>
        public Vector3 Velocity => GetVelocity();

        // ------------------------------------------------------------------ état interne

        /// <summary>Tout ce qu'on doit retenir pour une main.</summary>
        private class Hand
        {
            public bool isLeft;
            public XRNode node;
            public Transform controller;
            public Transform visual;
            public Vector3 position;      // position "virtuelle" de la main (en monde)
            public bool touching;
            public Vector3 surfaceNormal;
            public Collider surface;
            public float lastHapticTime = -1f;
        }

        private Rigidbody _rb;
        private CapsuleCollider _body;
        private readonly Hand _left = new Hand { isLeft = true, node = XRNode.LeftHand };
        private readonly Hand _right = new Hand { isLeft = false, node = XRNode.RightHand };

        private Vector3[] _velocityHistory;
        private int _velocityIndex;
        private int _velocityCount;
        private bool _wasTouching;
        private Vector3 _spawnPosition;

        // ------------------------------------------------------------------ cycle de vie Unity

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _body = GetComponent<CapsuleCollider>();

            // Le joueur ne doit jamais basculer : seules les mains le déplacent.
            _rb.freezeRotation = true;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;           // mouvement fluide à l'écran
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // pas de traversée de mur à grande vitesse

            _body.direction = 1; // capsule verticale (axe Y)
            ApplyPreset();
            RefreshOwnColliders();
            _velocityHistory = new Vector3[Mathf.Max(2, velocityHistorySize)];
            _spawnPosition = transform.position;
        }

        private void Start()
        {
            _left.controller = leftController;
            _left.visual = leftHandVisual;
            _right.controller = rightController;
            _right.visual = rightHandVisual;

            if (head == null || leftController == null || rightController == null)
            {
                Debug.LogError("[ArmLocomotion] Il manque la tête ou une manette dans l'inspecteur.", this);
                enabled = false;
                return;
            }

            ResetHands();
            ApplyBodyFriction();   // après les Awake (LeanSlide pose son matériau dans le sien)

            if (matchPhysicsRateToHeadset)
            {
                float refreshRate = HeadsetRefreshRate(); // 0 si aucun casque n'est connecté
                if (refreshRate > 30f)
                    Time.fixedDeltaTime = 1f / refreshRate;
            }
        }

        /// <summary>Fréquence d'affichage du casque (Hz), 0 si aucun écran XR ne tourne.
        /// Remplace XRDevice.refreshRate, obsolète dans Unity 6.</summary>
        private static float HeadsetRefreshRate()
        {
            var displays = new List<XRDisplaySubsystem>();
            SubsystemManager.GetSubsystems(displays);
            foreach (XRDisplaySubsystem d in displays)
                if (d.running && d.TryGetDisplayRefreshRate(out float hz)) return hz;
            return 0f;
        }

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        /// <summary>Position de la tête du joueur (là où le monstre doit l'attraper).</summary>
        public Vector3 HeadPosition => head != null ? head.position : transform.position;

        /// <summary>Où réapparaître : la zone sûre s'il y en a une, sinon le point de départ.</summary>
        public Vector3 SpawnPosition => Horror.World.SafeZone.RespawnPosition ?? _spawnPosition;

        /// <summary>Faux pendant un jumpscare : les mains ne déplacent plus le corps.</summary>
        public bool inputEnabled = true;

        private void FixedUpdate()
        {
            UpdateBodyCollider();
            KeepHeadOutOfWalls();

            // 1) Chaque main : est-elle appuyée quelque part ? Si oui, de combien le corps doit-il bouger ?
            Vector3 totalMove = Vector3.zero;
            int touchingCount = 0;
            StepHand(_left, ref totalMove, ref touchingCount);
            StepHand(_right, ref totalMove, ref touchingCount);
            if (armHitbox && inputEnabled) PushArmsOutOfWalls();

            if (touchingCount > 0)
            {
                // 2a) Au moins une main appuyée : c'est elle qui porte le corps, pas de gravité.
                Vector3 desired = totalMove / touchingCount;
                Vector3 move = SweepBody(desired);

                SetGravity(false);
                SetVelocity(Vector3.zero);
                _rb.MovePosition(_rb.position + move);

                RecordVelocity(move / Time.fixedDeltaTime);
                _wasTouching = true;
            }
            else
            {
                // 2b) Plus aucune main : la gravité reprend la main.
                SetGravity(true);

                if (_wasTouching)
                {
                    // On vient de lâcher : on donne au corps l'élan du geste (= le saut).
                    Vector3 launch = AverageRecordedVelocity() * jumpMultiplier;
                    if (launch.magnitude < minLaunchSpeed) launch = Vector3.zero;   // un geste lent ne fait pas « flotter »
                    SetVelocity(Vector3.ClampMagnitude(launch, maxJumpSpeed));
                    ClearVelocityHistory();
                }
                _wasTouching = false;
            }

            // 3) Filets de sécurité : tombé hors de la map (ou dans le vide sous le sol) → dernier endroit sûr.
            RememberSafeSpot(touchingCount > 0);
            if (_rb.position.y < respawnBelowY)
                TeleportTo(SpawnPosition);
            else if (FallingIntoVoid())
                TeleportTo(_hasSafe ? _safeSpot : SpawnPosition);
        }

        // ------------------------------------------------------------------ anti-traversée

        // ------------------------------------------------------------------ hitbox des bras

        private CapsuleCollider _armProbe;
        private readonly Collider[] _armHits = new Collider[12];

        /// <summary>
        /// Chaque bras est une capsule de l'épaule (estimée sous la tête) jusqu'au poignet. Si elle rentre dans le décor,
        /// le corps est repoussé hors du mur et la vitesse qui allait dans le mur est retirée : on ne passe plus le bras
        /// à travers une paroi, on peut s'y appuyer. Rien n'est fait pour un bras dont la main est déjà posée (c'est la
        /// main qui commande alors : sinon le bras et la main se contrediraient et le corps tremblerait).
        /// </summary>
        private void PushArmsOutOfWalls()
        {
            if (_armProbe == null)
            {
                var go = new GameObject("Hitbox des bras (sonde)");
                go.transform.SetParent(transform, false);
                go.layer = gameObject.layer;
                _armProbe = go.AddComponent<CapsuleCollider>();
                _armProbe.isTrigger = true;    // ne sert qu'aux calculs de chevauchement
                _armProbe.direction = 2;       // le long de l'axe Z local
                RefreshOwnColliders();
            }
            Vector3 headP = PhysicsPosition(head);
            Vector3 fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (fwd.sqrMagnitude < 1e-4f) fwd = transform.forward;
            Vector3 right = Vector3.Cross(Vector3.up, fwd.normalized);
            Vector3 push = Vector3.zero;
            foreach (Hand hand in new[] { _left, _right })
            {
                if (hand.touching) continue;
                Vector3 shoulder = headP - Vector3.up * 0.24f + right * (hand.isLeft ? -0.17f : 0.17f);
                Vector3 wrist = hand.position;
                Vector3 seg = wrist - shoulder;
                float len = seg.magnitude;
                if (len < 0.2f) continue;
                Vector3 dir = seg / len;
                // de 15 % du bras (près de l'épaule, déjà couverte par le corps) jusqu'avant la main (couverte par sa boule)
                Vector3 a = shoulder + dir * (len * 0.15f);
                Vector3 b = wrist - dir * (handRadius + 0.02f);
                float l = Vector3.Distance(a, b);
                if (l < 0.05f) continue;
                Vector3 center = (a + b) * 0.5f;
                Quaternion rot = Quaternion.LookRotation(dir);
                _armProbe.radius = armRadius;
                _armProbe.height = l + armRadius * 2f;
                int n = Physics.OverlapCapsuleNonAlloc(a, b, armRadius, _armHits, climbableLayers, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n; i++)
                {
                    Collider c = _armHits[i];
                    if (c == null || _own.Contains(c)) continue;
                    if (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic) continue;   // objets libres : ignorés
                    if (Physics.ComputePenetration(_armProbe, center, rot, c, c.transform.position, c.transform.rotation,
                                                   out Vector3 pdir, out float pdist) && pdist > 0.002f)
                    {
                        // seulement les murs et obstacles de côté : le sol et le plafond sont l'affaire des mains, de la tête et
                        // du corps (sinon l'avant-bras touche le sol avant la main et soulève le corps : la marche ne marche plus)
                        if (Mathf.Abs(pdir.y) > 0.5f) continue;
                        pdir.y = 0f;
                        Vector3 p = pdir.normalized * pdist;
                        // on garde la plus grande poussée dans chaque direction (pas de double comptage)
                        if (Mathf.Abs(p.x) > Mathf.Abs(push.x)) push.x = p.x;
                        if (Mathf.Abs(p.y) > Mathf.Abs(push.y)) push.y = p.y;
                        if (Mathf.Abs(p.z) > Mathf.Abs(push.z)) push.z = p.z;
                    }
                }
            }
            if (push.sqrMagnitude < 1e-6f) return;
            push = Vector3.ClampMagnitude(push, 0.08f);   // jamais de grand saut d'un coup
            _rb.MovePosition(_rb.position + SweepBody(push));
            Vector3 v = GetVelocity();
            Vector3 n2 = push.normalized;
            float into = Vector3.Dot(v, -n2);
            if (into > 0f) SetVelocity(v + n2 * into);   // la vitesse qui rentrait dans le mur disparaît
        }

        private Vector3 _lastHead;
        private bool _hasLastHead;
        private Vector3 _safeSpot;
        private bool _hasSafe;
        private float _nextSafeCheck;

        /// <summary>
        /// La tête ne traverse jamais un mur ni un sol : si, d'une image à l'autre, elle passerait à travers (le joueur
        /// avance dans sa pièce, se penche brusquement, ou le corps est poussé), tout le joueur est repoussé du bon côté.
        /// C'est ce qui faisait passer un testeur sous la map en marchant.
        /// </summary>
        private void KeepHeadOutOfWalls()
        {
            Vector3 headNow = PhysicsPosition(head);
            if (_hasLastHead)
            {
                Vector3 d = headNow - _lastHead;
                float len = d.magnitude;
                if (len > 1e-4f && len < 3f && CastWorld(_lastHead, 0.06f, d / len, len, out RaycastHit hit, ignorePlayers: true))
                {
                    Vector3 allowed = _lastHead + d / len * Mathf.Max(0f, hit.distance - 0.02f);
                    _rb.position += allowed - headNow;
                    SetVelocity(Vector3.zero);
                    headNow = allowed;
                }
            }
            _lastHead = headNow;
            _hasLastHead = true;
        }

        /// <summary>Retient régulièrement une position sûre (les mains sur une surface, ou le corps posé sur un sol).</summary>
        private void RememberSafeSpot(bool handsDown)
        {
            if (Time.time < _nextSafeCheck) return;
            _nextSafeCheck = Time.time + 0.5f;
            bool grounded = CastWorld(PhysicsPosition(head), bodyRadius * 0.5f, Vector3.down, bodyColliderHeight + 1.2f, out _);
            if (handsDown || grounded) { _safeSpot = _rb.position; _hasSafe = true; }
        }

        private float _voidTime;

        /// <summary>En train de tomber et plus rien sous la tête sur 60 m, pendant plus d'une demi-seconde : on est passé
        /// sous la map. (Le délai évite une téléportation intempestive au-dessus d'un trou du décor.)</summary>
        private bool FallingIntoVoid()
        {
            bool voidNow = GetVelocity().y < -4f && !CastWorld(PhysicsPosition(head), 0.05f, Vector3.down, 60f, out _);
            _voidTime = voidNow ? _voidTime + Time.fixedDeltaTime : 0f;
            if (_voidTime < 0.5f) return false;
            _voidTime = 0f;
            return true;
        }

        /// <summary>Lancer de sphère dans le décor, en ignorant ses propres colliders et les déclencheurs.</summary>
        private bool CastWorld(Vector3 from, float radius, Vector3 dir, float distance, out RaycastHit best, bool ignorePlayers = false)
        {
            best = default;
            int n = Physics.SphereCastNonAlloc(from, radius, dir, _hits, distance, climbableLayers, QueryTriggerInteraction.Ignore);
            float bestDist = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                RaycastHit h = _hits[i];
                if (h.collider == null || _own.Contains(h.collider)) continue;
                if (h.rigidbody != null && !h.rigidbody.isKinematic) continue;   // un objet qu'on tient ou qui roule ne compte pas
                // la coque d'un AUTRE joueur (ou du mannequin) ne repousse pas la tête : sinon, à-coups en grimpant sur lui
                if (ignorePlayers && h.collider.GetComponentInParent<Horror.Player.PlayerBodyShell>() != null) continue;
                if (h.distance <= 0f && h.point == Vector3.zero) continue;
                if (h.distance < bestDist) { bestDist = h.distance; best = h; }
            }
            return bestDist < float.MaxValue;
        }

        private void LateUpdate()
        {
            // Les mains visibles : collées à la surface si la main touche, sinon sur la vraie manette.
            PlaceVisual(_left);
            PlaceVisual(_right);
        }

        // ------------------------------------------------------------------ API publique

        /// <summary>Téléporte le joueur (respawn, changement de zone...). Les mains sont décrochées.</summary>
        public void TeleportTo(Vector3 position)
        {
            _rb.position = position;
            transform.position = position;
            SetVelocity(Vector3.zero);
            ClearVelocityHistory();
            _wasTouching = false;
            _hasLastHead = false;
            ResetHands();
        }

        // ------------------------------------------------------------------ logique des mains

        private void StepHand(Hand hand, ref Vector3 totalMove, ref int touchingCount)
        {
            if (!inputEnabled)
            {
                hand.touching = false;
                hand.position = PhysicsPosition(hand.controller);
                return;
            }
            Vector3 target = ClampToArmLength(PhysicsPosition(hand.controller), PhysicsPosition(head));

            if (hand.touching)
            {
                Vector3 offset = target - hand.position;
                bool pulledAway = Vector3.Dot(offset, hand.surfaceNormal) > releaseDistance;
                bool tooFar = offset.magnitude > unstickDistance;

                if (pulledAway || tooFar)
                {
                    // La main quitte la surface : elle redevient libre.
                    hand.touching = false;
                    hand.position = target;
                    return;
                }

                // Glisse : la main appuyée suit un peu la manette le long de la surface (jamais à travers).
                if (surfaceSlide > 0f)
                    hand.position += Vector3.ProjectOnPlane(offset, hand.surfaceNormal) * surfaceSlide;

                // La main reste collée : le corps compense le mouvement de la manette.
                totalMove += hand.position - target;
                touchingCount++;
                return;
            }

            // Main libre : on "balaye" le trajet depuis la dernière position pour voir si elle tape quelque chose.
            Vector3 from = hand.position;
            Vector3 path = target - from;
            float distance = path.magnitude;

            if (distance > 0.0001f && CastHand(from, path / distance, distance, out RaycastHit hit))
            {
                hand.position = from + path / distance * hit.distance;
                hand.surfaceNormal = hit.normal;
                hand.surface = hit.collider;
                hand.touching = true;

                totalMove += hand.position - target;
                touchingCount++;

                OnHandHit(hand, distance / Time.fixedDeltaTime);
            }
            else
            {
                hand.position = target;
            }
        }

        private void OnHandHit(Hand hand, float impactSpeed)
        {
            if (impactSpeed < minImpactSpeed)
                return;

            // Vibration proportionnelle à la force du coup (avec un petit délai pour ne pas spammer).
            if (Time.time - hand.lastHapticTime > 0.08f)
            {
                float amplitude = Mathf.Clamp01(hapticStrength * impactSpeed / 2f);
                InputDevices.GetDeviceAtXRNode(hand.node).SendHapticImpulse(0, amplitude, hapticDuration);
                hand.lastHapticTime = Time.time;
            }

            HandImpact?.Invoke(hand.position, impactSpeed, hand.isLeft);
            HandImpactOn?.Invoke(hand.position, impactSpeed, hand.isLeft, hand.surface);
        }

        // ------------------------------------------------------------------ mains : quoi toucher

        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private readonly HashSet<Collider> _own = new HashSet<Collider>();

        /// <summary>
        /// À rappeler si on ajoute des colliders au joueur (ex. PlayerBodyShell) : les mains ne doivent
        /// jamais s'accrocher à son PROPRE corps, mais elles peuvent grimper sur celui des autres joueurs.
        /// </summary>
        public void RefreshOwnColliders()
        {
            _own.Clear();
            foreach (Collider c in GetComponentsInChildren<Collider>(true)) _own.Add(c);
        }

        /// <summary>
        /// Comme Physics.SphereCast, mais en ignorant ses propres colliders. Comme SphereCast, on ignore
        /// aussi ce qui touche déjà la sphère au départ (sinon une main qui vient de se décoller se recolle).
        /// </summary>
        private bool CastHand(Vector3 from, Vector3 dir, float distance, out RaycastHit best)
        {
            best = default;
            int n = Physics.SphereCastNonAlloc(from, handRadius, dir, _hits, distance, climbableLayers, QueryTriggerInteraction.Ignore);
            float bestDist = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                RaycastHit h = _hits[i];
                if (h.collider == null || _own.Contains(h.collider)) continue;
                if (h.distance <= 0f && h.point == Vector3.zero) continue;   // déjà en contact au départ
                if (h.distance < bestDist) { bestDist = h.distance; best = h; }
            }
            return bestDist < float.MaxValue;
        }

        private Vector3 ClampToArmLength(Vector3 controllerPosition, Vector3 headPosition)
        {
            Vector3 fromHead = controllerPosition - headPosition;
            return headPosition + Vector3.ClampMagnitude(fromHead, maxArmLength);
        }

        /// <summary>
        /// Position d'un enfant du joueur, calculée depuis la position PHYSIQUE du corps.
        /// Avec l'interpolation activée, la position affichée (transform) est légèrement en retard
        /// sur la physique : s'en servir dans FixedUpdate ferait trembler les mains.
        /// </summary>
        private Vector3 PhysicsPosition(Transform child)
        {
            return _rb.position + (child.position - transform.position);
        }

        private void PlaceVisual(Hand hand)
        {
            if (hand.visual == null)
                return;

            // Ici on veut la position AFFICHÉE (interpolée), donc on lit directement les transforms.
            hand.visual.position = hand.touching
                ? hand.position
                : ClampToArmLength(hand.controller.position, head.position);
            hand.visual.rotation = hand.controller.rotation;
        }

        private void ResetHands()
        {
            foreach (Hand hand in new[] { _left, _right })
            {
                hand.touching = false;
                hand.position = hand.controller != null ? PhysicsPosition(hand.controller) : _rb.position;
            }
        }

        // ------------------------------------------------------------------ corps

        /// <summary>
        /// Le corps est une capsule accrochée sous la tête : si le joueur marche dans sa pièce,
        /// le corps le suit ; s'il s'accroupit, la capsule touche le sol et le relève.
        /// </summary>
        private void UpdateBodyCollider()
        {
            Vector3 headLocal = transform.InverseTransformPoint(head.position);
            float height = Mathf.Max(bodyColliderHeight, bodyRadius * 2f);

            _body.radius = bodyRadius;
            _body.height = height;
            // Le haut de la capsule est centré sur les yeux : la tête ne rentre pas dans les murs.
            _body.center = headLocal - Vector3.up * (height * 0.5f - bodyRadius);
        }

        /// <summary>
        /// Vérifie que le corps peut faire ce déplacement sans traverser un mur. S'il bute,
        /// il glisse le long de la surface (une seule fois, c'est suffisant ici).
        /// </summary>
        private Vector3 SweepBody(Vector3 move)
        {
            Vector3 result = Vector3.zero;
            Vector3 remaining = move;

            for (int i = 0; i < 2 && remaining.sqrMagnitude > 1e-8f; i++)
            {
                float distance = remaining.magnitude;
                Vector3 direction = remaining / distance;

                if (!_rb.SweepTest(direction, out RaycastHit hit, distance + skinWidth, QueryTriggerInteraction.Ignore))
                {
                    result += remaining;
                    break;
                }

                float allowed = Mathf.Max(0f, hit.distance - skinWidth);
                result += direction * allowed;
                // Ce qui reste du mouvement, projeté le long du mur.
                remaining = Vector3.ProjectOnPlane(direction * (distance - allowed), hit.normal);
            }

            return result;
        }

        // ------------------------------------------------------------------ historique de vitesse

        private void RecordVelocity(Vector3 velocity)
        {
            _velocityHistory[_velocityIndex] = velocity;
            _velocityIndex = (_velocityIndex + 1) % _velocityHistory.Length;
            _velocityCount = Mathf.Min(_velocityCount + 1, _velocityHistory.Length);
        }

        private Vector3 AverageRecordedVelocity()
        {
            if (_velocityCount == 0)
                return Vector3.zero;

            Vector3 sum = Vector3.zero;
            for (int i = 0; i < _velocityCount; i++)
                sum += _velocityHistory[i];
            return sum / _velocityCount;
        }

        private void ClearVelocityHistory()
        {
            _velocityIndex = 0;
            _velocityCount = 0;
        }

        // ------------------------------------------------------------------ compatibilité Unity 2022 / Unity 6

        // Unity 6 a renommé Rigidbody.velocity en linearVelocity : on gère les deux.
        private Vector3 GetVelocity()
        {
            if (_rb == null) return Vector3.zero;
    #if UNITY_6000_0_OR_NEWER
            return _rb.linearVelocity;
    #else
            return _rb.velocity;
    #endif
        }

        private void SetVelocity(Vector3 velocity)
        {
    #if UNITY_6000_0_OR_NEWER
            _rb.linearVelocity = velocity;
    #else
            _rb.velocity = velocity;
    #endif
        }

        private void SetGravity(bool on)
        {
            if (_rb.useGravity != on)
                _rb.useGravity = on;
        }

        /// <summary>Valeurs du préréglage choisi (Personnalisé : on garde celles de l'inspecteur).</summary>
        public void ApplyPreset()
        {
            switch (preset)
            {
                case Preset.Classique:
                    jumpMultiplier = 1.2f; maxJumpSpeed = 7f; velocityHistorySize = 6;
                    handRadius = 0.07f; unstickDistance = 0.3f; releaseDistance = 0.015f; surfaceSlide = 0f;
                    bodyColliderHeight = bodyHeight; bodyFriction = 0.6f;
                    break;
                case Preset.Rapide:
                    // comme les jeux de bras : grands élans, mains plus « larges » qui accrochent mieux, petite glisse pour
                    // courir sur les murs. Corps à 0,85 (0,45 faisait s'enfoncer dans le sol au premier test des joueurs).
                    jumpMultiplier = 1.35f; maxJumpSpeed = 8.5f; velocityHistorySize = 5;
                    handRadius = 0.085f; unstickDistance = 0.45f; releaseDistance = 0.02f; surfaceSlide = 0.01f;   // glisse très légère
                    bodyColliderHeight = 0.85f; bodyFriction = 0.12f;
                    break;
            }
            ApplyBodyFriction();
            // la glisse en se penchant (LeanSlide) faisait « glisser au lieu de marcher » : coupée en Rapide
            var lean = GetComponent<LeanSlide>();
            if (lean != null && preset != Preset.Personnalise) lean.enabled = preset == Preset.Classique;
            if (_velocityHistory == null || _velocityHistory.Length != Mathf.Max(2, velocityHistorySize))
            {
                _velocityHistory = new Vector3[Mathf.Max(2, velocityHistorySize)];
                ClearVelocityHistory();
            }
        }

#if UNITY_6000_0_OR_NEWER
        private PhysicsMaterial _bodyMat;
#else
        private PhysicMaterial _bodyMat;
#endif

        /// <summary>Matériau physique du corps (frottement réglable). Appliqué aussi au démarrage, après les autres scripts.</summary>
        private void ApplyBodyFriction()
        {
            if (!Application.isPlaying) return;
            if (_body == null) _body = GetComponent<CapsuleCollider>();
            if (_body == null) return;
            if (preset == Preset.Classique && GetComponent<LeanSlide>() != null) return;   // LeanSlide gère son propre frottement
#if UNITY_6000_0_OR_NEWER
            if (_bodyMat == null) _bodyMat = new PhysicsMaterial("Corps du joueur") { frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Minimum };
#else
            if (_bodyMat == null) _bodyMat = new PhysicMaterial("Corps du joueur") { frictionCombine = PhysicMaterialCombine.Minimum, bounceCombine = PhysicMaterialCombine.Minimum };
#endif
            _bodyMat.dynamicFriction = _bodyMat.staticFriction = bodyFriction;
            _bodyMat.bounciness = 0f;
            _body.sharedMaterial = _bodyMat;
        }

        private void OnValidate()
        {
            ApplyPreset();
            velocityHistorySize = Mathf.Max(2, velocityHistorySize);
            handRadius = Mathf.Max(0.01f, handRadius);
            bodyRadius = Mathf.Max(0.05f, bodyRadius);
        }
    }
}
