using Horror.Locomotion;
using UnityEngine;

namespace Horror.Player
{
    /// <summary>
    /// La « coque » solide d'un joueur : une sphère pour la tête et une capsule pour le corps, qui
    /// suivent le casque. Les MAINS DES AUTRES joueurs peuvent s'y appuyer (comme sur un mur) :
    /// on peut grimper sur la tête de quelqu'un et garder l'équilibre avec ses mains, et il te bloque.
    ///
    /// - Joueur local : les colliders sont enfants du Joueur, et ArmLocomotion ignore ses propres
    ///   colliders (tes mains ne s'accrochent jamais à ton propre corps).
    /// - Autres joueurs (réseau, plus tard) ou mannequin d'essai : même composant, avec un Rigidbody
    ///   cinématique (il est ajouté tout seul s'il n'y a pas d'ArmLocomotion).
    /// La coque est sur le calque Default pour que les mains (calques « grimpables ») la touchent.
    /// </summary>
    public class PlayerBodyShell : MonoBehaviour
    {
        [Tooltip("La tête (caméra du casque, ou tête de l'avatar distant).")]
        public Transform head;
        public float headRadius = 0.16f;
        public float bodyRadius = 0.17f;
        [Tooltip("Hauteur du corps sous la tête (le personnage n'a pas de jambes).")]
        public float bodyLength = 0.5f;
        [Tooltip("Calque de la coque (doit faire partie des calques « grimpables » des mains).")]
        public int shellLayer = 0;

        private Transform _headShell, _bodyShell;

        private void Start()
        {
            if (head == null) head = transform;
            _headShell = MakePart("Coque (tête)", true);
            _bodyShell = MakePart("Coque (corps)", false);

            ArmLocomotion local = GetComponent<ArmLocomotion>();
            if (local != null)
            {
                // Joueur local : la coque ne doit PAS porter son propre corps (elle le faisait tenir debout et glisser
                // au sol). Chaque pièce devient un objet cinématique à part, qui ne touche pas la capsule du joueur :
                // les mains des AUTRES s'y appuient toujours, mais pour moi ce sont mes mains qui me portent.
                foreach (Transform part in new[] { _headShell, _bodyShell })
                {
                    Rigidbody prb = part.gameObject.AddComponent<Rigidbody>();
                    prb.isKinematic = true;
                    prb.useGravity = false;
                    Collider pc = part.GetComponent<Collider>();
                    foreach (Collider own in GetComponents<Collider>()) Physics.IgnoreCollision(own, pc);
                }
                local.RefreshOwnColliders();   // mes mains ignorent mon corps
            }
            else if (GetComponent<Rigidbody>() == null)
            {
                Rigidbody rb = gameObject.AddComponent<Rigidbody>();   // avatar distant : bouge sans tomber
                rb.isKinematic = true;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
            }
            Follow();
        }

        private Transform MakePart(string name, bool isHead)
        {
            var go = new GameObject(name);
            go.layer = shellLayer;
            go.transform.SetParent(transform, false);
            if (isHead)
            {
                SphereCollider s = go.AddComponent<SphereCollider>();
                s.radius = headRadius;
            }
            else
            {
                CapsuleCollider c = go.AddComponent<CapsuleCollider>();
                c.radius = bodyRadius;
                c.height = bodyLength;
                c.direction = 1;
            }
            return go.transform;
        }

        private void FixedUpdate() => Follow();
        private void LateUpdate() => Follow();

        private void Follow()
        {
            if (_headShell == null || head == null) return;
            Vector3 h = head.position;
            _headShell.position = h;
            // Le corps pend sous la tête, à la verticale (même si on penche la tête)
            Vector3 fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            Quaternion yaw = fwd.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(fwd, Vector3.up) : Quaternion.identity;
            _bodyShell.SetPositionAndRotation(h - Vector3.up * (headRadius + bodyLength * 0.5f - 0.04f), yaw);
        }
    }
}
