using UnityEngine;
using UnityEngine.XR;

namespace Horror.Interaction
{
    /// <summary>
    /// Rend une main capable d'appuyer sur des boutons (boutique, portes du Protocole Verrou, Minitel).
    /// À mettre sur la main visible (la boule / le modèle de main). Ajoute une petite sphère
    /// "déclencheur" au bout des doigts.
    /// </summary>
    public class HandPresser : MonoBehaviour
    {
        [Tooltip("Main gauche ou droite (pour les vibrations).")]
        public XRNode node = XRNode.RightHand;
        [Tooltip("Rayon de la zone qui appuie (m).")]
        public float radius = 0.035f;
        [Tooltip("Décalage du bout des doigts par rapport au centre de la main.")]
        public Vector3 tipOffset = new Vector3(0f, 0f, 0.04f);

        private void Awake()
        {
            var tip = new GameObject("Bout des doigts (bouton)");
            tip.transform.SetParent(transform, false);
            tip.transform.localPosition = tipOffset;
            tip.layer = gameObject.layer;
            SphereCollider col = tip.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = radius;
            Rigidbody rb = tip.AddComponent<Rigidbody>();   // nécessaire pour que les déclencheurs fonctionnent
            rb.isKinematic = true;
            rb.useGravity = false;
            tip.AddComponent<HandPresserTip>().owner = this;
        }

        /// <summary>Petite vibration (appelée par le bouton pressé).</summary>
        public void Buzz(float amplitude = 0.3f, float duration = 0.04f) =>
            InputDevices.GetDeviceAtXRNode(node).SendHapticImpulse(0, amplitude, duration);
    }

}
