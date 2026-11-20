using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace Horror.Locomotion
{
    /// <summary>
    /// Locomotion "aux bras" : le joueur se déplace en poussant sur le décor avec ses mains,
    /// comme dans les fan games de type Gorilla Tag. Pas de joystick.
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

        [Header("Mains")]
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
        [Tooltip("Rayon du corps.")]
        public float bodyRadius = 0.2f;
        [Tooltip("Petite marge pour ne pas coller le corps aux murs lors des déplacements.")]
        public float skinWidth = 0.01f;

        [Header("Élan / saut")]
        [Tooltip("Multiplie la vitesse au moment de lâcher. 1 = réaliste, plus = sauts plus amples.")]
        public float jumpMultiplier = 1.2f;
        [Tooltip("Vitesse maximale donnée par un saut (m/s).")]
        public float maxJumpSpeed = 7f;
        [Tooltip("Nombre d'images utilisées pour calculer la vitesse au moment de lâcher.")]
        [Range(2, 20)] public int velocityHistorySize = 6;

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

            if (matchPhysicsRateToHeadset)
            {
                float refreshRate = XRDevice.refreshRate; // 0 si aucun casque n'est connecté
                if (refreshRate > 30f)
                    Time.fixedDeltaTime = 1f / refreshRate;
            }
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

            // 1) Chaque main : est-elle appuyée quelque part ? Si oui, de combien le corps doit-il bouger ?
            Vector3 totalMove = Vector3.zero;
            int touchingCount = 0;
            StepHand(_left, ref totalMove, ref touchingCount);
            StepHand(_right, ref totalMove, ref touchingCount);

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
                    SetVelocity(Vector3.ClampMagnitude(launch, maxJumpSpeed));
                    ClearVelocityHistory();
                }
                _wasTouching = false;
            }

            // 3) Filet de sécurité : tombé hors de la map → retour au départ.
            if (_rb.position.y < respawnBelowY)
                TeleportTo(SpawnPosition);
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
            float height = Mathf.Max(bodyHeight, bodyRadius * 2f);

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

        private void OnValidate()
        {
            velocityHistorySize = Mathf.Max(2, velocityHistorySize);
            handRadius = Mathf.Max(0.01f, handRadius);
            bodyRadius = Mathf.Max(0.05f, bodyRadius);
        }
    }
}
