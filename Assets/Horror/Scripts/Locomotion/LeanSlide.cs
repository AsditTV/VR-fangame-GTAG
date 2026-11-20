using UnityEngine;

namespace Horror.Locomotion
{
    /// <summary>
    /// Glisser en se penchant (ajoute du dynamisme à la locomotion aux bras) :
    ///  - incliner la TÊTE sur le côté (oreille vers l'épaule) = on glisse de ce côté ;
    ///  - pencher le HAUT DU CORPS (la tête s'avance ou recule par rapport à sa position habituelle)
    ///    = on glisse vers l'avant ou l'arrière.
    /// Ça marche quand on est au sol et qu'aucune main n'est accrochée. L'élan est gardé : on peut
    /// enchaîner un saut aux bras puis atterrir en glissant.
    ///
    /// Regarder vers le bas ne fait PAS glisser (sinon on partirait à chaque fois qu'on regarde ses pieds).
    /// À mettre sur l'objet "Joueur" (avec ArmLocomotion).
    /// </summary>
    [RequireComponent(typeof(ArmLocomotion))]
    public class LeanSlide : MonoBehaviour
    {
        [Header("Inclinaison de la tête (sur le côté)")]
        [Tooltip("En dessous de cet angle, rien ne se passe (degrés).")]
        public float rollDeadZone = 9f;
        [Tooltip("Angle pour glisser à pleine vitesse (degrés).")]
        public float rollFull = 30f;

        [Header("Pencher le corps (avant / arrière)")]
        [Tooltip("Distance dont la tête doit s'écarter de sa position habituelle pour glisser (m).")]
        public float leanDeadZone = 0.07f;
        public float leanFull = 0.22f;
        [Tooltip("Vitesse à laquelle la 'position habituelle' suit la tête (plus bas = elle se recale plus lentement).")]
        public float neutralFollow = 0.35f;

        [Header("Glisse")]
        public float acceleration = 7f;
        public float maxSlideSpeed = 3.2f;
        [Tooltip("Frottement au sol quand on glisse (bas = patinoire).")]
        public float slideFriction = 0.04f;
        public float normalFriction = 0.6f;
        public LayerMask groundLayers = Physics.DefaultRaycastLayers;

        /// <summary>Inclinaison actuelle (-1..1) : x = côté (droite +), y = avant (+). Utilisée par le corps de la poupée.</summary>
        public Vector2 Lean { get; private set; }
        public bool IsGrounded { get; private set; }
        public bool IsSliding { get; private set; }

        private ArmLocomotion _loco;
        private Rigidbody _rb;
        private CapsuleCollider _capsule;
        private Vector3 _neutralHead;   // position "habituelle" de la tête dans le repère du joueur
#if UNITY_6000_0_OR_NEWER
        private PhysicsMaterial _mat;
#else
        private PhysicMaterial _mat;
#endif

        private void Awake()
        {
            _loco = GetComponent<ArmLocomotion>();
            _rb = GetComponent<Rigidbody>();
            _capsule = GetComponent<CapsuleCollider>();
#if UNITY_6000_0_OR_NEWER
            _mat = new PhysicsMaterial("Glisse") { frictionCombine = PhysicsMaterialCombine.Minimum };
#else
            _mat = new PhysicMaterial("Glisse") { frictionCombine = PhysicMaterialCombine.Minimum };
#endif
            if (_capsule != null) _capsule.sharedMaterial = _mat;
        }

        private void Start()
        {
            if (_loco.head != null) _neutralHead = transform.InverseTransformPoint(_loco.head.position);
        }

        private void FixedUpdate()
        {
            Transform head = _loco.head;
            if (head == null) return;

            // --- 1) Mesure de l'inclinaison
            Vector3 fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.ProjectOnPlane(-head.up, Vector3.up); // regard tout en bas
            fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);

            // Roulis : la tête penche sur le côté (on ignore le fait de regarder en haut/en bas)
            // (positif = la tête penche vers la droite)
            float roll = Mathf.Asin(Mathf.Clamp(Vector3.Dot(head.up, right), -1f, 1f)) * Mathf.Rad2Deg;
            float side = Mathf.Clamp(Mathf.Sign(roll) * Mathf.InverseLerp(rollDeadZone, rollFull, Mathf.Abs(roll)), -1f, 1f);

            // Penché : écart de la tête par rapport à sa position habituelle (qui se recale doucement)
            Vector3 headLocal = transform.InverseTransformPoint(head.position);
            _neutralHead = Vector3.Lerp(_neutralHead, headLocal, 1f - Mathf.Exp(-neutralFollow * Time.fixedDeltaTime));
            Vector3 offset = transform.TransformVector(headLocal - _neutralHead);
            float forwardOffset = Vector3.Dot(offset, fwd);
            float sideOffset = Vector3.Dot(offset, right);
            float fwdLean = Mathf.Sign(forwardOffset) * Mathf.InverseLerp(leanDeadZone, leanFull, Mathf.Abs(forwardOffset));
            side = Mathf.Clamp(side + Mathf.Sign(sideOffset) * Mathf.InverseLerp(leanDeadZone, leanFull, Mathf.Abs(sideOffset)), -1f, 1f);
            Lean = new Vector2(side, fwdLean);

            // --- 2) Au sol ?
            IsGrounded = CheckGround();
            bool canSlide = IsGrounded && !_loco.IsTouchingSurface && _loco.inputEnabled;
            Vector3 dir = right * Lean.x + fwd * Lean.y;
            IsSliding = canSlide && dir.sqrMagnitude > 0.0001f;

            // --- 3) Glisse
            _mat.dynamicFriction = _mat.staticFriction = IsSliding ? slideFriction : normalFriction;
            if (!IsSliding) return;

            Vector3 v = GetVelocity();
            Vector3 horizontal = new Vector3(v.x, 0f, v.z);
            horizontal += Vector3.ClampMagnitude(dir, 1f) * acceleration * Time.fixedDeltaTime;
            horizontal = Vector3.ClampMagnitude(horizontal, Mathf.Max(maxSlideSpeed, new Vector3(v.x, 0f, v.z).magnitude * 0.98f));
            SetVelocity(new Vector3(horizontal.x, v.y, horizontal.z));
        }

        private bool CheckGround()
        {
            if (_capsule == null) return false;
            Bounds b = _capsule.bounds;
            float r = _capsule.radius * 0.9f;
            Vector3 origin = new Vector3(b.center.x, b.min.y + r + 0.02f, b.center.z);
            return Physics.SphereCast(origin, r, Vector3.down, out _, 0.12f, groundLayers, QueryTriggerInteraction.Ignore);
        }

        private Vector3 GetVelocity()
        {
#if UNITY_6000_0_OR_NEWER
            return _rb.linearVelocity;
#else
            return _rb.velocity;
#endif
        }

        private void SetVelocity(Vector3 v)
        {
#if UNITY_6000_0_OR_NEWER
            _rb.linearVelocity = v;
#else
            _rb.velocity = v;
#endif
        }
    }
}
