using Horror.Monsters;
using UnityEngine;

namespace Horror.Audio
{
    /// <summary>
    /// Chef d'orchestre de l'ambiance, à mettre sur la caméra du joueur (là où est l'AudioListener) :
    ///  - un fond sonore permanent (grondement grave + vent) ;
    ///  - un fondu vers l'ambiance de la zone où se trouve le joueur (AmbientZone) ;
    ///  - des bruits ponctuels placés AUTOUR du joueur, souvent derrière lui (grincements, gouttes,
    ///    boîte à musique lointaine, rire d'enfant...) ;
    ///  - des battements de coeur qui montent quand un monstre chasse tout près.
    /// </summary>
    public class AmbientSoundscape : MonoBehaviour
    {
        [Header("Fond permanent")]
        public AudioClip baseLoop;
        [Range(0f, 1f)] public float baseVolume = 0.35f;

        [Header("Bruits ponctuels par défaut (hors zone)")]
        public AudioClip[] defaultOneShots;
        public Vector2 defaultInterval = new Vector2(10f, 25f);

        [Header("Tension")]
        public AudioClip heartbeatLoop;
        [Tooltip("Distance (m) à laquelle un monstre en chasse fait battre le coeur.")]
        public float heartbeatRange = 14f;

        [Tooltip("Vitesse des fondus entre ambiances.")]
        public float fadeSpeed = 0.6f;

        private AudioSource _base, _zoneA, _zoneB, _heart;
        private AudioSource _current;
        private AmbientZone _zone;
        private float _nextOneShot;

        private void Start()
        {
            _base = Make2D(baseLoop, baseVolume);
            _zoneA = Make2D(null, 0f);
            _zoneB = Make2D(null, 0f);
            _heart = Make2D(heartbeatLoop, 0f);
            _current = _zoneA;
            _nextOneShot = Time.time + Random.Range(4f, 8f);
        }

        private void Update()
        {
            Vector3 me = transform.position;

            // 1) Quelle zone ? (la plus prioritaire qui contient le joueur)
            AmbientZone best = null;
            foreach (AmbientZone z in AmbientZone.All)
                if (z.Contains(me) && (best == null || z.priority > best.priority)) best = z;

            if (best != _zone)
            {
                _zone = best;
                AudioSource next = _current == _zoneA ? _zoneB : _zoneA;
                next.clip = best != null ? best.loop : null;
                if (next.clip != null) next.Play();
                _current = next;
            }

            float k = fadeSpeed * Time.deltaTime;
            float targetZone = _zone != null && _zone.loop != null ? _zone.loopVolume : 0f;
            float hush = 1f - 0.85f * FearDirector.Silence;   // elle rôde tout près : presque plus rien
            _current.volume = Mathf.MoveTowards(_current.volume, targetZone * hush, k);
            AudioSource other = _current == _zoneA ? _zoneB : _zoneA;
            other.volume = Mathf.MoveTowards(other.volume, 0f, k);
            if (other.volume <= 0f && other.isPlaying) other.Stop();

            bool safe = _zone != null && _zone.isSafe;
            _base.volume = Mathf.MoveTowards(_base.volume, (safe ? baseVolume * 0.15f : baseVolume) * hush, k);

            // 2) Bruits ponctuels autour du joueur
            if (!safe && FearDirector.Silence < 0.3f && Time.time >= _nextOneShot)
            {
                AudioClip[] pool = _zone != null && _zone.oneShots != null && _zone.oneShots.Length > 0 ? _zone.oneShots : defaultOneShots;
                Vector2 interval = _zone != null && _zone.oneShots != null && _zone.oneShots.Length > 0 ? _zone.oneShotInterval : defaultInterval;
                float vol = _zone != null ? _zone.oneShotVolume : 0.6f;
                if (pool != null && pool.Length > 0)
                {
                    // Souvent derrière le joueur : c'est là que ça fait le plus peur
                    Vector3 back = -transform.forward;
                    back.y = 0f;
                    Vector3 dir = Quaternion.Euler(0f, Random.Range(-110f, 110f), 0f) * (back.sqrMagnitude > 0.01f ? back.normalized : Vector3.back);
                    Vector3 pos = me + dir * Random.Range(5f, 13f) + Vector3.up * Random.Range(-1f, 1.5f);
                    AudioClip clip = pool[Random.Range(0, pool.Length)];
                    if (clip != null) PlayAt(clip, pos, vol);
                }
                _nextOneShot = Time.time + Random.Range(interval.x, interval.y);
            }

            // 3) Battements de coeur si un monstre chasse tout près
            float danger = 0f;
            foreach (BlindMonsterAI m in FindMonsters())
            {
                if (m.state != BlindMonsterAI.State.Chasse) continue;
                float d = Vector3.Distance(m.transform.position, me);
                danger = Mathf.Max(danger, 1f - d / heartbeatRange);
            }
            danger = Mathf.Max(danger, FearDirector.Fear * 0.75f);   // le coeur bat dès qu'elle est proche
            if (safe) danger = 0f;
            _heart.volume = Mathf.MoveTowards(_heart.volume, Mathf.Clamp01(danger) * 0.9f, Time.deltaTime * 1.5f);
            _heart.pitch = 1f + Mathf.Clamp01(danger) * 0.25f;
        }

        private BlindMonsterAI[] _monsters = new BlindMonsterAI[0];
        private float _nextMonsterScan;

        private BlindMonsterAI[] FindMonsters()
        {
            if (Time.time >= _nextMonsterScan)
            {
#if UNITY_2022_2_OR_NEWER
                _monsters = FindObjectsByType<BlindMonsterAI>(FindObjectsSortMode.None);
#else
                _monsters = FindObjectsOfType<BlindMonsterAI>();
#endif
                _nextMonsterScan = Time.time + 2f;
            }
            return _monsters;
        }

        private static void PlayAt(AudioClip clip, Vector3 pos, float volume)
        {
            var go = new GameObject("Bruit ambiant");
            go.transform.position = pos;
            AudioSource s = go.AddComponent<AudioSource>();
            s.clip = clip;
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Logarithmic;
            s.minDistance = 2f;
            s.maxDistance = 30f;
            s.volume = volume;
            s.pitch = Random.Range(0.9f, 1.05f);
            s.Play();
            Destroy(go, clip.length / s.pitch + 0.2f);
        }

        private AudioSource Make2D(AudioClip clip, float volume)
        {
            AudioSource s = gameObject.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = true;
            s.spatialBlend = 0f;
            s.volume = volume;
            s.playOnAwake = false;
            if (clip != null) s.Play();
            return s;
        }
    }
}
