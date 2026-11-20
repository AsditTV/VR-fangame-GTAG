using UnityEngine;

namespace Horror.Player
{
    /// <summary>
    /// Fait du modèle "Le Dessiné" le corps du joueur :
    ///  - le corps se place sous la tête (le casque) et se tourne doucement dans la direction du regard ;
    ///  - les bras suivent les mains (IK à deux os : épaule → coude → main) ;
    ///  - si la main est plus loin que le bras, le bras de poupée S'ÉTIRE (style cartoon, et pratique
    ///    pour la locomotion aux bras qui demande des bras longs) ;
    ///  - pour le joueur local, la tête est cachée (sinon on voit l'intérieur de son propre masque).
    ///
    /// À mettre sur l'instance du modèle LeDessine (enfant de "Joueur"). Les cibles des mains sont les
    /// mains invisibles d'ArmLocomotion (celles qui restent collées aux murs).
    /// </summary>
    [DefaultExecutionOrder(200)]   // après ArmLocomotion (qui place les mains)
    public class DollAvatarRig : MonoBehaviour
    {
        [Header("Cibles")]
        [Tooltip("La caméra du casque.")]
        public Transform head;
        public Transform leftHandTarget;
        public Transform rightHandTarget;

        [Header("Réglages")]
        [Tooltip("Hauteur des yeux du personnage au-dessus de ses pieds (m). LeDessine : ~0.92.")]
        public float eyeHeight = 0.92f;
        [Tooltip("La tête est un peu derrière les yeux (m).")]
        public float headBack = 0.05f;
        [Tooltip("À partir de quel écart (degrés) le corps se retourne vers le regard.")]
        public float bodyTurnThreshold = 35f;
        public float bodyTurnSpeed = 4f;
        [Tooltip("Le corps penche quand on incline la tête (0 = jamais, 1 = autant que la tête).")]
        [Range(0f, 1f)] public float bodyLeanFactor = 0.7f;
        [Tooltip("Inclinaison maximale du corps (degrés).")]
        public float maxBodyLean = 28f;
        [Tooltip("Facultatif : la glissade (LeanSlide) fait aussi pencher le corps vers l'avant/arrière.")]
        public Horror.Locomotion.LeanSlide leanSlide;
        [Tooltip("Cacher sa propre tête (joueur local). Faux pour les autres joueurs.")]
        public bool hideOwnHead = true;
        [Tooltip("Rotation ajoutée aux mains pour les aligner sur les manettes (G puis D).")]
        public Vector3 leftHandRotation = new Vector3(-90f, 0f, 90f);
        public Vector3 rightHandRotation = new Vector3(-90f, 0f, -90f);

        private Transform[] _shoulder = new Transform[2], _elbow = new Transform[2], _hand = new Transform[2];
        private Transform[] _upperMesh = new Transform[2], _foreMesh = new Transform[2];
        private float[] _l1 = new float[2], _l2 = new float[2];
        private Vector3[] _upperScale = new Vector3[2], _foreScale = new Vector3[2];
        private Vector3[] _elbowLocal = new Vector3[2], _handLocal = new Vector3[2];
        private float _bodyYaw;
        private Vector2 _lean;

        private void Start()
        {
            string[] s = { "G", "D" };
            for (int i = 0; i < 2; i++)
            {
                _shoulder[i] = Find("Epaule_" + s[i]);
                _elbow[i] = Find("Coude_" + s[i]);
                _hand[i] = Find("Main_" + s[i]);
                _upperMesh[i] = Find("BrasHaut_" + s[i]);
                _foreMesh[i] = Find("AvantBras_" + s[i]);
                if (_shoulder[i] == null || _elbow[i] == null || _hand[i] == null) continue;
                _l1[i] = Vector3.Distance(_shoulder[i].position, _elbow[i].position);
                _l2[i] = Vector3.Distance(_elbow[i].position, _hand[i].position);
                _elbowLocal[i] = _elbow[i].localPosition;
                _handLocal[i] = _hand[i].localPosition;
                if (_upperMesh[i] != null) _upperScale[i] = _upperMesh[i].localScale;
                if (_foreMesh[i] != null) _foreScale[i] = _foreMesh[i].localScale;
            }

            if (hideOwnHead)
            {
                Transform tete = Find("Tete");
                if (tete != null)
                    foreach (Renderer r in tete.GetComponentsInChildren<Renderer>()) r.enabled = false;
            }
            if (head != null) _bodyYaw = head.eulerAngles.y;
        }

        private void LateUpdate()
        {
            if (head == null) return;

            // 1) Le corps sous la tête, tourné doucement vers le regard
            float headYaw = head.eulerAngles.y;
            float diff = Mathf.DeltaAngle(_bodyYaw, headYaw);
            if (Mathf.Abs(diff) > bodyTurnThreshold * 0.3f)
                _bodyYaw += diff * (1f - Mathf.Exp(-bodyTurnSpeed * Time.deltaTime)) * (Mathf.Abs(diff) > bodyTurnThreshold ? 2f : 0.5f);
            Quaternion yawRot = Quaternion.Euler(0f, _bodyYaw, 0f);

            // Le corps penche avec la tête (pivot : la tête, donc les pieds partent de l'autre côté)
            Vector3 bodyRight = yawRot * Vector3.right;
            float roll = Mathf.Asin(Mathf.Clamp(Vector3.Dot(head.up, bodyRight), -1f, 1f)) * Mathf.Rad2Deg;
            float side = Mathf.Clamp(roll * bodyLeanFactor, -maxBodyLean, maxBodyLean);
            float forward = leanSlide != null ? leanSlide.Lean.y * 14f : 0f;
            if (leanSlide != null && leanSlide.IsSliding) side += leanSlide.Lean.x * 6f;   // on se "couche" un peu dans la glisse
            _lean = Vector2.Lerp(_lean, new Vector2(side, forward), 1f - Mathf.Exp(-8f * Time.deltaTime));
            Quaternion bodyRot = yawRot * Quaternion.Euler(_lean.y, 0f, -_lean.x);

            transform.SetPositionAndRotation(head.position - bodyRot * Vector3.up * eyeHeight - bodyRot * Vector3.forward * headBack, bodyRot);

            // 2) Les bras
            SolveArm(0, leftHandTarget, Quaternion.Euler(leftHandRotation), 1f);
            SolveArm(1, rightHandTarget, Quaternion.Euler(rightHandRotation), -1f);
        }

        private void SolveArm(int i, Transform target, Quaternion handOffset, float side)
        {
            if (target == null || _shoulder[i] == null) return;
            Vector3 S = _shoulder[i].position;
            Vector3 T = target.position;
            float a = _l1[i], b = _l2[i];
            Vector3 toT = T - S;
            float d = toT.magnitude;
            if (d < 0.001f) return;
            Vector3 dir = toT / d;

            // Étirement cartoon si la main est trop loin
            float stretch = Mathf.Max(1f, d / (a + b) * 1.001f);
            a *= stretch; b *= stretch;
            float dc = Mathf.Clamp(d, Mathf.Abs(a - b) + 0.001f, a + b - 0.0001f);

            // Le coude part vers le bas et vers l'extérieur
            Vector3 bodyRight = transform.right * side;
            Vector3 pole = (Vector3.down * 0.7f + bodyRight * 0.5f - transform.forward * 0.2f).normalized;
            Vector3 bendAxis = Vector3.Cross(dir, pole);
            if (bendAxis.sqrMagnitude < 1e-6f) bendAxis = transform.forward;
            bendAxis.Normalize();
            float alpha = Mathf.Acos(Mathf.Clamp((a * a + dc * dc - b * b) / (2f * a * dc), -1f, 1f)) * Mathf.Rad2Deg;
            Vector3 upperDir = Quaternion.AngleAxis(-alpha, bendAxis) * dir;
            Vector3 elbowPos = S + upperDir * a;
            Vector3 foreDir = (T - elbowPos).normalized;

            // Les segments du modèle pendent selon leur -Y local
            _shoulder[i].rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.forward, upperDir).normalized + 1e-4f * Vector3.forward, -upperDir);
            _elbow[i].localPosition = _elbowLocal[i] * stretch;
            _elbow[i].rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.forward, foreDir).normalized + 1e-4f * Vector3.forward, -foreDir);
            _hand[i].localPosition = _handLocal[i] * stretch;
            if (_upperMesh[i] != null) _upperMesh[i].localScale = new Vector3(_upperScale[i].x, _upperScale[i].y * stretch, _upperScale[i].z);
            if (_foreMesh[i] != null) _foreMesh[i].localScale = new Vector3(_foreScale[i].x, _foreScale[i].y * stretch, _foreScale[i].z);

            // La main prend l'orientation de la manette
            _hand[i].rotation = target.rotation * handOffset;
        }

        private Transform Find(string n)
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
                if (t.name == n) return t;
            return null;
        }
    }
}
