using UnityEngine;
using UnityEngine.XR;

namespace Horror.Player
{
    /// <summary>
    /// Plie les doigts d'une main selon les boutons de la manette :
    ///  - GÂCHETTE (index)          → plie l'index
    ///  - GRIP (bouton sur le côté) → plie le majeur et l'annulaire
    ///  - pouce posé sur A/B/X/Y ou le joystick → plie le pouce
    ///
    /// À mettre sur le noeud "Main_G" ou "Main_D" du modèle LeDessine.
    /// Les articulations sont trouvées toutes seules grâce à leur nom (Doigt_Index_1_G, etc.).
    ///
    /// Plus tard, le système d'objets (étape 4) appellera SetGrabCurl() pour que les doigts
    /// s'arrêtent autour de l'objet tenu au lieu de passer à travers.
    /// </summary>
    public class HandFingerAnimator : MonoBehaviour
    {
        public enum Side { Gauche, Droite }

        [Tooltip("Quelle main : change la manette lue et le suffixe des noms (_G ou _D).")]
        public Side side = Side.Gauche;

        [Header("Angles de pliage (degrés)")]
        [Tooltip("Angle maximal de chaque phalange. Si les doigts se plient vers l'arrière, inverse le signe.")]
        public float fingerCurlAngle = -80f;
        public float thumbCurlAngle = -55f;
        [Tooltip("Axe local de rotation des articulations (X sur le modèle LeDessine).")]
        public Vector3 curlAxis = Vector3.right;

        [Header("Réactivité")]
        [Tooltip("Vitesse à laquelle les doigts suivent les boutons. Plus haut = plus nerveux.")]
        [Range(1f, 40f)] public float speed = 18f;

        [Header("Valeurs actuelles (lecture seule, utile pour le réseau)")]
        [Range(0f, 1f)] public float indexCurl;
        [Range(0f, 1f)] public float gripCurl;
        [Range(0f, 1f)] public float thumbCurl;

        [Tooltip("Faux = main d'un AUTRE joueur : les valeurs viennent du réseau via SetRemoteCurls().")]
        public bool isLocal = true;

        // Phalanges : [doigt][articulation]
        private Transform[] _index, _middle, _ring, _thumb;
        private Quaternion[] _indexBase, _middleBase, _ringBase, _thumbBase;
        private float _grabLimit = 1f;
        private float _remoteIndex, _remoteGrip, _remoteThumb;

        private void Awake()
        {
            string s = side == Side.Gauche ? "_G" : "_D";
            _index = FindJoints("Index", s);
            _middle = FindJoints("Majeur", s);
            _ring = FindJoints("Annulaire", s);
            _thumb = FindJoints("Pouce", s);

            _indexBase = SaveRotations(_index);
            _middleBase = SaveRotations(_middle);
            _ringBase = SaveRotations(_ring);
            _thumbBase = SaveRotations(_thumb);

            if (_index.Length == 0 && _middle.Length == 0)
                Debug.LogWarning($"[HandFingerAnimator] Aucun doigt trouvé sous {name} (noms attendus : Doigt_Index_1{s}...).", this);
        }

        private void Update()
        {
            float targetIndex, targetGrip, targetThumb;

            if (isLocal)
            {
                InputDevice device = InputDevices.GetDeviceAtXRNode(side == Side.Gauche ? XRNode.LeftHand : XRNode.RightHand);
                device.TryGetFeatureValue(CommonUsages.trigger, out targetIndex);
                device.TryGetFeatureValue(CommonUsages.grip, out targetGrip);

                // Pouce : plié dès qu'il touche un bouton ou le joystick.
                bool touch = (device.TryGetFeatureValue(CommonUsages.primaryTouch, out bool a) && a)
                          || (device.TryGetFeatureValue(CommonUsages.secondaryTouch, out bool b) && b)
                          || (device.TryGetFeatureValue(CommonUsages.primary2DAxisTouch, out bool c) && c);
                targetThumb = touch ? 1f : 0f;
            }
            else
            {
                targetIndex = _remoteIndex;
                targetGrip = _remoteGrip;
                targetThumb = _remoteThumb;
            }

            float k = 1f - Mathf.Exp(-speed * Time.deltaTime);
            indexCurl = Mathf.Lerp(indexCurl, Mathf.Min(targetIndex, _grabLimit), k);
            gripCurl = Mathf.Lerp(gripCurl, Mathf.Min(targetGrip, _grabLimit), k);
            thumbCurl = Mathf.Lerp(thumbCurl, Mathf.Min(targetThumb, _grabLimit), k);

            Apply(_index, _indexBase, indexCurl, fingerCurlAngle);
            Apply(_middle, _middleBase, gripCurl, fingerCurlAngle);
            Apply(_ring, _ringBase, gripCurl, fingerCurlAngle);
            Apply(_thumb, _thumbBase, thumbCurl, thumbCurlAngle);
        }

        /// <summary>
        /// Limite le pliage (0..1) quand la main tient un objet : 1 = libre, 0.5 = doigts à moitié fermés.
        /// </summary>
        public void SetGrabCurl(float maxCurl) => _grabLimit = Mathf.Clamp01(maxCurl);

        /// <summary>Pour la main d'un autre joueur : valeurs reçues par le réseau (0..1).</summary>
        public void SetRemoteCurls(float index, float grip, float thumb)
        {
            _remoteIndex = Mathf.Clamp01(index);
            _remoteGrip = Mathf.Clamp01(grip);
            _remoteThumb = Mathf.Clamp01(thumb);
        }

        // ------------------------------------------------------------------ utilitaires

        private void Apply(Transform[] joints, Quaternion[] baseRotations, float curl, float maxAngle)
        {
            for (int i = 0; i < joints.Length; i++)
            {
                // La 2e phalange plie un peu plus que la 1re : ça donne un poing plus naturel.
                float weight = i == 0 ? 0.85f : 1.1f;
                joints[i].localRotation = baseRotations[i] * Quaternion.AngleAxis(curl * maxAngle * weight, curlAxis);
            }
        }

        private Transform[] FindJoints(string finger, string suffix)
        {
            Transform j1 = FindDeep(transform, $"Doigt_{finger}_1{suffix}");
            Transform j2 = FindDeep(transform, $"Doigt_{finger}_2{suffix}");
            if (j1 == null) return new Transform[0];
            return j2 == null ? new[] { j1 } : new[] { j1, j2 };
        }

        private static Quaternion[] SaveRotations(Transform[] joints)
        {
            var result = new Quaternion[joints.Length];
            for (int i = 0; i < joints.Length; i++) result[i] = joints[i].localRotation;
            return result;
        }

        private static Transform FindDeep(Transform root, string targetName)
        {
            if (root.name == targetName) return root;
            foreach (Transform child in root)
            {
                Transform found = FindDeep(child, targetName);
                if (found != null) return found;
            }
            return null;
        }
    }
}
