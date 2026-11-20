using UnityEngine;
using UnityEngine.Events;

namespace Horror.Interaction
{
    /// <summary>
    /// Un objet qu'on peut attraper avec le bouton GRIP et lancer en le lâchant.
    /// Les objets lancés font du bruit en tombant s'ils ont aussi ImpactNoise : c'est l'arme du joueur
    /// pour détourner L'Écoute.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Grabbable : MonoBehaviour
    {
        [Tooltip("Jusqu'où les doigts se ferment autour de l'objet (0 = main ouverte, 1 = poing). Papier : 0.35, bouteille : 0.7.")]
        [Range(0f, 1f)] public float gripCurl = 0.6f;
        [Tooltip("Force du lancer (1 = vitesse réelle de la main).")]
        public float throwMultiplier = 1.15f;

        public UnityEvent<HandGrabber> onGrabbed = new UnityEvent<HandGrabber>();
        public UnityEvent<HandGrabber> onReleased = new UnityEvent<HandGrabber>();

        /// <summary>La main qui tient l'objet (null si personne).</summary>
        public HandGrabber Holder { get; private set; }
        public Rigidbody Body { get; private set; }

        private Collider[] _colliders;
        private bool[] _wasTrigger;

        private void Awake() => Body = GetComponent<Rigidbody>();

        internal void Grab(HandGrabber hand)
        {
            if (Holder != null) Holder.ForceRelease();   // passer d'une main à l'autre
            Holder = hand;
            Body.isKinematic = true;
            // Inventaire au moment de saisir (un collider a pu être ajouté après Awake, ex. LorePaper)
            _colliders = GetComponentsInChildren<Collider>();
            _wasTrigger = new bool[_colliders.Length];
            // Tenu en main : il ne pousse plus le corps du joueur
            for (int i = 0; i < _colliders.Length; i++)
            {
                _wasTrigger[i] = _colliders[i].isTrigger;
                _colliders[i].isTrigger = true;
            }
            onGrabbed.Invoke(hand);
        }

        internal void Release(HandGrabber hand, Vector3 velocity, Vector3 angularVelocity)
        {
            if (Holder != hand) return;
            Holder = null;
            for (int i = 0; i < _colliders.Length; i++) _colliders[i].isTrigger = _wasTrigger[i];
            Body.isKinematic = false;
#if UNITY_6000_0_OR_NEWER
            Body.linearVelocity = velocity * throwMultiplier;
#else
            Body.velocity = velocity * throwMultiplier;
#endif
            Body.angularVelocity = angularVelocity;
            onReleased.Invoke(hand);
        }
    }
}
