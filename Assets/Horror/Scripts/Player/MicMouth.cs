using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace Horror.Player
{
    /// <summary>
    /// Fait bouger la bouche du personnage selon le volume du micro.
    /// - Joueur local : lit le micro (isLocal = true).
    /// - Joueur distant : tu appelles SetRemoteOpenness() avec la valeur reçue par le réseau
    ///   (isLocal = false). Openness (0..1) est la valeur à envoyer.
    /// </summary>
    public class MicMouth : MonoBehaviour
    {
        [Header("Références")]
        public Transform mouth;

        [Header("Mode")]
        [Tooltip("Vrai = lit le micro. Faux = attend SetRemoteOpenness (autres joueurs).")]
        public bool isLocal = true;

        [Header("Réglages micro")]
        [Tooltip("Vide = micro par défaut de Windows (conseillé). Sinon, un morceau du nom du micro, " +
                 "ex. \"Oculus\" pour le micro du Quest via Link.")]
        public string deviceNameContains = "";
        [Tooltip("Plus haut = la bouche s'ouvre avec une voix plus faible.")]
        public float sensitivity = 40f;
        [Tooltip("Bruit de fond ignoré (souffle, ventilateur).")]
        public float noiseGate = 0.005f;
        [Range(1f, 40f)] public float attack = 25f;   // vitesse d'ouverture
        [Range(1f, 40f)] public float release = 12f;  // vitesse de fermeture

        [Header("Forme de la bouche")]
        public float closedScaleY = 0.35f;
        public float openScaleY = 2.2f;
        [Tooltip("Descend un peu quand elle s'ouvre (unités locales).")]
        public float openDropY = 0.08f;

        [Header("Debug")]
        public bool showDebug = false;

        /// <summary>0 = fermée, 1 = grande ouverte. À synchroniser en réseau plus tard.</summary>
        public float Openness { get; private set; }

        /// <summary>Volume brut du micro (RMS). Servira au monstre aveugle pour "entendre" la voix.</summary>
        public float RawLevel => _rawLevel;

        const int SampleWindow = 256;
        AudioClip _clip;
        string _device;
        float[] _buffer = new float[SampleWindow];
        Vector3 _baseScale, _basePos;
        float _rawLevel;
        float _remoteTarget;
        bool _micReady;
        bool _micStarted;      // vrai une fois Microphone.Start appelé avec succès
        float _nextMicRetry;
        bool _warnedNoMic;   // sur Quest autonome, on réessaie après l'autorisation du micro

        void Start()
        {
            if (mouth == null)
            {
                Debug.LogError("MicMouth : assigne le champ 'mouth'.", this);
                enabled = false;
                return;
            }
            _baseScale = mouth.localScale;
            _basePos = mouth.localPosition;
            if (isLocal) StartMic();
        }

        void StartMic()
        {
            _nextMicRetry = Time.time + 1f; // prochain essai dans 1 s si ça échoue
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                // La fenêtre d'autorisation s'affiche ; Update() réessaiera une fois acceptée.
                Permission.RequestUserPermission(Permission.Microphone);
                return;
            }
#endif
            if (Microphone.devices.Length == 0)
            {
                if (!_warnedNoMic) Debug.LogWarning("MicMouth : aucun micro détecté.");
                _warnedNoMic = true; // un seul avertissement, pas un par seconde
                return;
            }

            // null = micro PAR DÉFAUT du système (avant : le 1er de la liste, pas forcément le bon).
            _device = null;
            if (!string.IsNullOrEmpty(deviceNameContains))
            {
                foreach (string name in Microphone.devices)
                {
                    if (name.IndexOf(deviceNameContains, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _device = name;
                        break;
                    }
                }
                if (_device == null)
                    Debug.LogWarning($"MicMouth : aucun micro ne contient \"{deviceNameContains}\", " +
                                     "j'utilise le micro par défaut.");
            }

            _clip = Microphone.Start(_device, true, 1, 44100);
            _micStarted = _clip != null;
        }

        void Update()
        {
            float target;
            if (isLocal)
            {
                if (!_micStarted && Time.time >= _nextMicRetry)
                    StartMic();

                if (!_micReady)
                {
                    // On attend que le micro ait vraiment démarré.
                    if (_clip != null && Microphone.GetPosition(_device) > 0) _micReady = true;
                    else { Apply(0f); return; }
                }
                target = ReadMicLevel();
            }
            else
            {
                target = _remoteTarget;
            }

            float speed = target > Openness ? attack : release;
            Openness = Mathf.Lerp(Openness, target, 1f - Mathf.Exp(-speed * Time.deltaTime));
            Apply(Openness);
        }

        float ReadMicLevel()
        {
            // Le clip du micro est une boucle d'1 seconde : quand la tête d'écriture vient de
            // repartir au début, on lit la fin du clip (avant : renvoyait 0 → petit "clignement" de bouche).
            int pos = Microphone.GetPosition(_device) - SampleWindow;
            if (pos < 0) pos += _clip.samples;
            _clip.GetData(_buffer, pos); // GetData reboucle tout seul en fin de clip
            float sum = 0f;
            for (int i = 0; i < SampleWindow; i++) sum += _buffer[i] * _buffer[i];
            _rawLevel = Mathf.Sqrt(sum / SampleWindow);
            return Mathf.Clamp01((_rawLevel - noiseGate) * sensitivity);
        }

        void Apply(float open)
        {
            float sy = Mathf.Lerp(closedScaleY, openScaleY, open);
            mouth.localScale = new Vector3(_baseScale.x, _baseScale.y * sy, _baseScale.z);
            mouth.localPosition = _basePos + new Vector3(0f, -openDropY * open, 0f);
        }

        /// <summary>Pour un joueur distant : valeur 0..1 reçue du réseau.</summary>
        public void SetRemoteOpenness(float value) => _remoteTarget = Mathf.Clamp01(value);

        void OnDestroy()
        {
            if (_micStarted && Microphone.IsRecording(_device)) Microphone.End(_device);
        }

        void OnGUI()
        {
            if (!showDebug || !isLocal) return;
            GUI.Label(new Rect(10, 10, 400, 24), $"Micro : {(_micStarted ? (_device ?? "par défaut du système") : "aucun")}");
            GUI.Label(new Rect(10, 34, 400, 24), $"Niveau brut : {_rawLevel:F4}   Ouverture : {Openness:F2}");
            GUI.Box(new Rect(10, 60, 300 * Openness + 2, 14), GUIContent.none);
        }
    }
}
