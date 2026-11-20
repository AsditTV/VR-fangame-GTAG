using System.Collections.Generic;
using Horror.Interaction;
using Horror.Noise;
using UnityEngine;

namespace Horror.World
{
    /// <summary>
    /// Porte du "Protocole Verrou" : un panneau qui coulisse dans le mur, commandé par un bouton de
    /// chaque côté (PressButton).
    ///  - elle FAIT DU BRUIT en bougeant : ouvrir une porte attire L'Écoute ;
    ///  - elle se referme toute seule après quelques secondes ;
    ///  - L'Écoute l'ouvre avec son trousseau quand elle passe (on entend les clés, puis la porte) ;
    ///  - "locked" = verrouillée : les boutons refusent (pour plus tard : le Veilleur, les énigmes).
    /// L'objet porte est placé au milieu de l'ouverture ; le panneau coulisse le long de son axe X local.
    /// </summary>
    public class LockDoor : MonoBehaviour
    {
        [Tooltip("Le panneau qui bouge (enfant de cet objet).")]
        public Transform panel;
        [Tooltip("Largeur de l'ouverture (m) : c'est la distance de coulissement.")]
        public float width = 2.4f;
        [Tooltip("Vitesse d'ouverture (m/s). Lente = plus de suspense.")]
        public float speed = 1.1f;
        [Tooltip("Se referme toute seule après ce délai (secondes). 0 = jamais.")]
        public float autoCloseDelay = 8f;
        public bool startOpen;
        [Tooltip("Verrouillée : les boutons ne marchent pas.")]
        public bool locked;

        [Header("Bruit")]
        [Tooltip("Portée du bruit quand elle bouge (m).")]
        public float noiseRadius = 14f;
        public AudioClip slideSound;
        public AudioClip lockSound;
        public AudioClip deniedSound;
        public AudioClip monsterKeysSound;

        [Header("Boutons (des deux côtés)")]
        public PressButton[] buttons;

        public static readonly List<LockDoor> All = new List<LockDoor>();

        public bool IsOpen => _targetOpen;

        private bool _targetOpen;
        private float _t;             // 0 = fermée, 1 = ouverte
        private float _openedAt;
        private Vector3 _closedLocal;
        private Rigidbody _panelBody;
        private AudioSource _audio;
        private bool _wasMoving;

        private void Awake()
        {
            if (panel == null && transform.childCount > 0) panel = transform.GetChild(0);
            if (panel != null)
            {
                _closedLocal = panel.localPosition;
                _panelBody = panel.GetComponent<Rigidbody>();
                if (_panelBody == null) _panelBody = panel.gameObject.AddComponent<Rigidbody>();
                _panelBody.isKinematic = true;   // c'est nous qui la bougeons
                _panelBody.interpolation = RigidbodyInterpolation.Interpolate;
            }
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.spatialBlend = 1f;
            _audio.minDistance = 1.5f;
            _audio.maxDistance = 20f;
            _audio.playOnAwake = false;
            _targetOpen = startOpen;
            _t = startOpen ? 1f : 0f;
            // Ouverte au départ : elle le reste tant que personne n'y touche (sinon toutes les portes
            // se refermeraient en même temps au bout de quelques secondes, avec un gros bruit)
            _openedAt = startOpen ? float.PositiveInfinity : 0f;
        }

        private void OnEnable()
        {
            All.Add(this);
            if (buttons != null)
                foreach (PressButton b in buttons) if (b != null) b.onPressed.AddListener(Toggle);
        }

        private void OnDisable()
        {
            All.Remove(this);
            if (buttons != null)
                foreach (PressButton b in buttons) if (b != null) b.onPressed.RemoveListener(Toggle);
        }

        /// <summary>Appuyer sur un bouton : ouvre ou ferme.</summary>
        public void Toggle()
        {
            if (locked)
            {
                if (deniedSound != null) _audio.PlayOneShot(deniedSound, 0.7f);
                return;
            }
            SetOpen(!_targetOpen);
        }

        /// <summary>L'Écoute ouvre la porte avec son trousseau (même verrouillée : elle a toutes les clés).</summary>
        public void OpenByMonster()
        {
            if (_targetOpen) return;
            if (monsterKeysSound != null) _audio.PlayOneShot(monsterKeysSound, 0.9f);
            SetOpen(true);
        }

        public void SetOpen(bool open)
        {
            if (open == _targetOpen) return;
            _targetOpen = open;
            if (open) _openedAt = Time.time;
            if (slideSound != null) _audio.PlayOneShot(slideSound, 0.8f);
            NoiseSystem.Emit(transform.position + Vector3.up * 1.2f, noiseRadius, NoiseType.Porte, gameObject);
        }

        private void Update()
        {
            if (_targetOpen && autoCloseDelay > 0f && Time.time - _openedAt > autoCloseDelay && !SomeoneInDoorway())
                SetOpen(false);
        }

        private void FixedUpdate()
        {
            if (panel == null) return;
            float target = _targetOpen ? 1f : 0f;
            bool moving = !Mathf.Approximately(_t, target);
            _t = Mathf.MoveTowards(_t, target, speed / Mathf.Max(0.1f, width) * Time.fixedDeltaTime);
            Vector3 local = _closedLocal + Vector3.right * (width * Mathf.SmoothStep(0f, 1f, _t));
            Vector3 world = transform.TransformPoint(local);
            if (_panelBody != null) _panelBody.MovePosition(world);
            else panel.position = world;

            if (_wasMoving && !moving && !_targetOpen && lockSound != null) _audio.PlayOneShot(lockSound, 0.8f);  // "clac" en fin de fermeture
            _wasMoving = moving;
        }

        /// <summary>Ne pas refermer la porte sur quelqu'un (joueur ou monstre) qui est dans l'encadrement.</summary>
        private bool SomeoneInDoorway()
        {
            Collider[] hits = Physics.OverlapBox(transform.position + Vector3.up * 1.2f, new Vector3(width * 0.5f, 1.2f, 0.6f),
                                                 transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            foreach (Collider c in hits)
            {
                if (panel != null && c.transform.IsChildOf(panel)) continue;
                if (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic) return true;   // joueur, objet lancé
                if (c.GetComponentInParent<Horror.Monsters.BlindMonsterAI>() != null) return true;
            }
            return false;
        }

        private void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = locked ? new Color(0.9f, 0.4f, 0.4f, 0.8f) : new Color(0.6f, 0.8f, 0.9f, 0.8f);
            Gizmos.DrawWireCube(Vector3.up * 1.2f, new Vector3(width, 2.4f, 0.1f));
        }
    }
}
