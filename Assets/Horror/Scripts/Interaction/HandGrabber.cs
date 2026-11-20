using Horror.Player;
using UnityEngine;
using UnityEngine.XR;

namespace Horror.Interaction
{
    /// <summary>
    /// Attraper avec le GRIP, lâcher pour lancer. À mettre sur la main visible (comme HandPresser).
    ///  - appuyer sur le grip près d'un objet Grabbable : la main le prend (il garde sa position dans la main) ;
    ///  - relâcher : il part avec la vitesse de ta main (lancer) ;
    ///  - les doigts (HandFingerAnimator) se referment autour, sans passer à travers.
    /// </summary>
    public class HandGrabber : MonoBehaviour
    {
        public XRNode node = XRNode.RightHand;
        [Tooltip("Rayon de détection autour de la main (m).")]
        public float reach = 0.1f;
        [Tooltip("Les doigts de cette main (facultatif).")]
        public HandFingerAnimator fingers;
        [Tooltip("Grip enfoncé au-delà : on attrape. En dessous de 'releaseThreshold' : on lâche.")]
        [Range(0f, 1f)] public float grabThreshold = 0.6f;
        [Range(0f, 1f)] public float releaseThreshold = 0.35f;

        public Grabbable Held { get; private set; }

        private readonly Vector3[] _posHistory = new Vector3[6];
        private readonly Quaternion[] _rotHistory = new Quaternion[6];
        private readonly float[] _timeHistory = new float[6];
        private int _historyIndex;
        private bool _gripWasDown;
        private readonly Collider[] _hits = new Collider[16];

        private void Update()
        {
            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            device.TryGetFeatureValue(CommonUsages.grip, out float grip);

            bool down = _gripWasDown ? grip > releaseThreshold : grip > grabThreshold;
            if (down && !_gripWasDown) TryGrab();
            else if (!down && _gripWasDown) Release();
            _gripWasDown = down;
        }

        private void LateUpdate()
        {
            // Historique des positions de la main (pour calculer la vitesse du lancer)
            _posHistory[_historyIndex] = transform.position;
            _rotHistory[_historyIndex] = transform.rotation;
            _timeHistory[_historyIndex] = Time.time;
            _historyIndex = (_historyIndex + 1) % _posHistory.Length;
        }

        private void TryGrab()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, reach, _hits, ~0, QueryTriggerInteraction.Collide);
            Grabbable best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Grabbable g = _hits[i].GetComponentInParent<Grabbable>();
                if (g == null || (g.Holder != null && g.Holder == this)) continue;
                float d = Vector3.Distance(transform.position, _hits[i].ClosestPoint(transform.position));
                if (d < bestDist) { bestDist = d; best = g; }
            }
            if (best == null) return;

            Held = best;
            best.Grab(this);
            best.transform.SetParent(transform, true);   // il garde sa position et son angle dans la main
            if (fingers != null) fingers.SetGrabCurl(best.gripCurl);
            device().SendHapticImpulse(0, 0.25f, 0.05f);
        }

        private void Release()
        {
            if (Held == null) return;
            Grabbable g = Held;
            Held = null;
            g.transform.SetParent(null, true);
            ComputeVelocity(out Vector3 v, out Vector3 w);
            g.Release(this, v, w);
            if (fingers != null) fingers.SetGrabCurl(1f);
        }

        /// <summary>Lâche l'objet sans le lancer (quand l'autre main le prend, par exemple).</summary>
        public void ForceRelease()
        {
            if (Held == null) return;
            Grabbable g = Held;
            Held = null;
            g.transform.SetParent(null, true);
            g.Release(this, Vector3.zero, Vector3.zero);
            if (fingers != null) fingers.SetGrabCurl(1f);
        }

        private void ComputeVelocity(out Vector3 velocity, out Vector3 angular)
        {
            int newest = (_historyIndex - 1 + _posHistory.Length) % _posHistory.Length;
            int oldest = _historyIndex;
            float dt = _timeHistory[newest] - _timeHistory[oldest];
            if (dt <= 0.0001f) { velocity = Vector3.zero; angular = Vector3.zero; return; }
            velocity = (_posHistory[newest] - _posHistory[oldest]) / dt;
            Quaternion delta = _rotHistory[newest] * Quaternion.Inverse(_rotHistory[oldest]);
            delta.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            angular = float.IsNaN(axis.x) ? Vector3.zero : axis * (angle * Mathf.Deg2Rad / dt);
        }

        private InputDevice device() => InputDevices.GetDeviceAtXRNode(node);

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.9f, 0.6f, 0.7f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, reach);
        }
    }
}
