using Horror.Monsters;
using Horror.Noise;
using UnityEngine;

namespace Horror.Audio
{
    /// <summary>
    /// Tous les sons de L'Écoute, synchronisés avec son cerveau (BlindMonsterAI) :
    ///  - quand elle n'entend rien : elle MURMURE en anglais ("I can hear you...", "Back to bed.") ;
    ///  - quand elle entend quelque chose : inspiration brusque + "Hm ?" ;
    ///  - quand elle passe en chasse : cri strident, puis pas lourds et rapides ;
    ///  - ses pas suivent sa vitesse, son trousseau de clés tinte plus fort quand elle court.
    /// Tous les sons sont en 3D : on les entend venir de sa position, de plus en plus fort.
    /// </summary>
    [RequireComponent(typeof(BlindMonsterAI))]
    public class MonsterVoice : MonoBehaviour
    {
        [Header("Sons (dossier Assets/Horror/Audio/Monster)")]
        public AudioClip[] whispers;
        public AudioClip[] hearReactions;
        public AudioClip chaseScream;
        public AudioClip[] footsteps;
        public AudioClip keysLoop;

        [Header("Murmures")]
        [Tooltip("Temps entre deux murmures quand elle erre (secondes, min / max).")]
        public Vector2 whisperInterval = new Vector2(5f, 12f);
        [Tooltip("Distance à laquelle on entend encore ses murmures (m). Courte = on les entend quand elle est déjà tout près.")]
        public float whisperRange = 12f;

        [Header("Pas")]
        [Tooltip("Longueur d'une enjambée (m). Plus court = pas plus fréquents.")]
        public float stride = 1.15f;
        public float footstepRange = 22f;

        private BlindMonsterAI _ai;
        private AudioSource _voice, _steps, _keys;
        private float _nextWhisper;
        private float _stepDistance;
        private Vector3 _lastPos;
        private int _lastWhisper = -1;

        private void Awake()
        {
            _ai = GetComponent<BlindMonsterAI>();
            Transform mouth = _ai.Head != null ? _ai.Head : transform;
            _voice = Make3DSource(mouth.gameObject, whisperRange);
            _steps = Make3DSource(gameObject, footstepRange);
            if (keysLoop != null)
            {
                _keys = Make3DSource(gameObject, 15f);
                _keys.clip = keysLoop;
                _keys.loop = true;
                _keys.Play();
                _ai.keysLoop = _keys; // le cerveau règle son volume selon la vitesse
            }
            _lastPos = transform.position;
            _nextWhisper = Time.time + Random.Range(whisperInterval.x, whisperInterval.y);
        }

        private void OnEnable()
        {
            if (_ai == null) _ai = GetComponent<BlindMonsterAI>();
            _ai.StateChanged += OnStateChanged;
        }

        private void OnDisable() => _ai.StateChanged -= OnStateChanged;

        private void OnStateChanged(BlindMonsterAI.State from, BlindMonsterAI.State to)
        {
            if (to == BlindMonsterAI.State.Ecoute)
                Say(Pick(hearReactions), 1f, 22f);
            else if (to == BlindMonsterAI.State.Chasse && chaseScream != null)
                Say(chaseScream, 1f, 40f); // le cri porte très loin
        }

        private void Update()
        {
            // Murmures quand elle erre ou fouille sans rien entendre
            bool calm = _ai.state == BlindMonsterAI.State.Errance || _ai.state == BlindMonsterAI.State.Enquete;
            if (calm && Time.time >= _nextWhisper && !_voice.isPlaying)
            {
                Say(PickWhisper(), Random.Range(0.75f, 1f), whisperRange);
                _nextWhisper = Time.time + Random.Range(whisperInterval.x, whisperInterval.y);
            }

            // Pas : un son toutes les "stride" mètres parcourus
            Vector3 delta = transform.position - _lastPos;
            delta.y = 0f;
            _stepDistance += delta.magnitude;
            _lastPos = transform.position;
            float currentStride = _ai.state == BlindMonsterAI.State.Chasse ? stride * 1.4f : stride;
            if (_stepDistance >= currentStride && footsteps != null && footsteps.Length > 0)
            {
                _stepDistance = 0f;
                float run = Mathf.Clamp01(_ai.CurrentSpeed / Mathf.Max(0.1f, _ai.chaseSpeed));
                _steps.pitch = Random.Range(0.9f, 1.05f) + run * 0.1f;
                _steps.PlayOneShot(Pick(footsteps), Mathf.Lerp(0.45f, 1f, run));
            }
        }

        private void Say(AudioClip clip, float volume, float range)
        {
            if (clip == null) return;
            _voice.maxDistance = range;
            _voice.Stop();
            _voice.clip = clip;
            _voice.volume = volume;
            _voice.pitch = Random.Range(0.93f, 1.02f);
            _voice.Play();
        }

        private AudioClip PickWhisper()
        {
            if (whispers == null || whispers.Length == 0) return null;
            int i;
            do { i = Random.Range(0, whispers.Length); } while (whispers.Length > 1 && i == _lastWhisper);
            _lastWhisper = i;
            return whispers[i];
        }

        private static AudioClip Pick(AudioClip[] clips) =>
            clips == null || clips.Length == 0 ? null : clips[Random.Range(0, clips.Length)];

        private static AudioSource Make3DSource(GameObject host, float maxDistance)
        {
            AudioSource s = host.AddComponent<AudioSource>();
            s.spatialBlend = 1f;                         // 100 % 3D
            s.rolloffMode = AudioRolloffMode.Logarithmic;
            s.minDistance = 1.5f;
            s.maxDistance = maxDistance;
            s.dopplerLevel = 0f;
            s.playOnAwake = false;
            return s;
        }
    }
}
