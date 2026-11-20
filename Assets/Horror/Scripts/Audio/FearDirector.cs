using System.Collections;
using Horror.Monsters;
using Horror.World;
using UnityEngine;
using UnityEngine.XR;

namespace Horror.Audio
{
    /// <summary>
    /// Le « chef de la peur », sur la tête du joueur. Le jumpscare seul ne fait pas peur longtemps :
    /// ce qui fait peur, c'est l'ATTENTE. Ce script construit la tension :
    ///  1. Peur (0 à 1) = un monstre est proche, même s'il ne te chasse pas encore ;
    ///  2. le coeur bat dans les manettes (vibrations au rythme du coeur) et ta respiration s'emballe ;
    ///  3. le SILENCE : quand elle rôde tout près sans chasser, l'ambiance se coupe presque
    ///     (AmbientSoundscape lit Silence) — on n'entend plus que ses clés et son souffle ;
    ///  4. les fausses alertes : si tout est calme trop longtemps, un événement : des pas qui
    ///     approchent derrière toi puis s'arrêtent, un chuchotement à l'oreille, un rire d'enfant,
    ///     une porte du Protocole Verrou qui se ferme toute seule au loin...
    /// Rien ne se passe dans la Salle de jeux (zone sûre).
    /// </summary>
    public class FearDirector : MonoBehaviour
    {
        /// <summary>Peur actuelle du joueur local (0 = calme, 1 = elle est sur toi).</summary>
        public static float Fear { get; private set; }
        /// <summary>0 à 1 : à quel point couper l'ambiance (elle rôde tout près).</summary>
        public static float Silence { get; private set; }

        [Header("Proximité")]
        [Tooltip("Distance (m) à partir de laquelle un monstre commence à faire peur.")]
        public float dreadRange = 18f;

        [Header("Corps")]
        public AudioClip breathLoop;
        [Range(0f, 1f)] public float maxBreathVolume = 0.55f;
        [Tooltip("Vibrations des manettes au rythme du coeur.")]
        public bool heartbeatHaptics = true;
        [Range(0f, 1f)] public float hapticStrength = 0.35f;

        [Header("Fausses alertes")]
        [Tooltip("Temps de calme (secondes, min / max) avant un événement.")]
        public Vector2 calmBeforeEvent = new Vector2(70f, 140f);
        public AudioClip[] stepsBehind;      // pas lourds (Monster/step_*)
        public AudioClip[] earWhispers;      // chuchotements (door_voice_*, whisper_*)
        public AudioClip[] farSounds;        // rire d'enfant, boîte à musique...
        public AudioClip doorSlam;

        private AudioSource _breath;
        private float _calmTimer;
        private float _nextEventAt;
        private float _nextBeat;
        private bool _busy;

        private void Start()
        {
            _breath = gameObject.AddComponent<AudioSource>();
            _breath.clip = breathLoop;
            _breath.loop = true;
            _breath.spatialBlend = 0f;
            _breath.volume = 0f;
            if (breathLoop != null) _breath.Play();
            _nextEventAt = Random.Range(calmBeforeEvent.x, calmBeforeEvent.y);
        }

        private void Update()
        {
            Vector3 me = transform.position;
            bool safe = SafeZone.Contains(me);

            // 1) Peur et silence selon le monstre le plus inquiétant
            float fear = 0f, silence = 0f;
            foreach (BlindMonsterAI m in BlindMonsterAI.All)
            {
                if (m == null) continue;
                float d = Vector3.Distance(m.transform.position, me);
                float close = Mathf.Clamp01(1f - d / dreadRange);
                float weight = m.state == BlindMonsterAI.State.Chasse ? 1f
                             : m.state == BlindMonsterAI.State.Enquete ? 0.8f
                             : m.state == BlindMonsterAI.State.Ecoute ? 0.75f : 0.5f;
                fear = Mathf.Max(fear, close * weight);
                if (m.state != BlindMonsterAI.State.Chasse && d < 9f) silence = Mathf.Max(silence, 1f - d / 9f);
            }
            if (safe) { fear = 0f; silence = 0f; }
            Fear = Mathf.MoveTowards(Fear, fear, Time.deltaTime * (fear > Fear ? 1.2f : 0.25f));   // monte vite, redescend lentement
            Silence = Mathf.MoveTowards(Silence, silence, Time.deltaTime * 0.8f);

            // 2) Respiration et coeur dans les manettes
            if (_breath != null)
            {
                _breath.volume = Mathf.SmoothStep(0f, maxBreathVolume, Mathf.InverseLerp(0.25f, 1f, Fear));
                _breath.pitch = 0.9f + Fear * 0.3f;
            }
            if (heartbeatHaptics && Fear > 0.3f && Time.time >= _nextBeat)
            {
                float bpm = Mathf.Lerp(70f, 150f, Fear);
                _nextBeat = Time.time + 60f / bpm;
                StartCoroutine(Beat(hapticStrength * Fear));
            }

            // 3) Fausses alertes quand c'est trop calme
            if (safe || Fear > 0.1f || _busy) { _calmTimer = 0f; return; }
            _calmTimer += Time.deltaTime;
            if (_calmTimer >= _nextEventAt)
            {
                _calmTimer = 0f;
                _nextEventAt = Random.Range(calmBeforeEvent.x, calmBeforeEvent.y);
                StartCoroutine(RandomEvent());
            }
        }

        private static IEnumerator Beat(float amp)
        {
            Pulse(amp, 0.05f);                     // « poum »...
            yield return new WaitForSeconds(0.16f);
            Pulse(amp * 0.6f, 0.04f);              // ... « pa »
        }

        private static void Pulse(float amp, float dur)
        {
            InputDevices.GetDeviceAtXRNode(XRNode.LeftHand).SendHapticImpulse(0, amp, dur);
            InputDevices.GetDeviceAtXRNode(XRNode.RightHand).SendHapticImpulse(0, amp, dur);
        }

        // ------------------------------------------------------------------ fausses alertes

        private IEnumerator RandomEvent()
        {
            _busy = true;
            int pick = Random.Range(0, 4);
            if (pick == 0 && stepsBehind != null && stepsBehind.Length > 0) yield return StepsBehind();
            else if (pick == 1 && earWhispers != null && earWhispers.Length > 0) Whisper();
            else if (pick == 2 && CloseFarDoor()) { }
            else if (farSounds != null && farSounds.Length > 0)
                PlayAt(farSounds[Random.Range(0, farSounds.Length)], Behind(3.5f), 0.8f);
            yield return new WaitForSeconds(4f);
            _busy = false;
        }

        /// <summary>Des pas lourds qui approchent dans ton dos... puis plus rien. Personne.</summary>
        private IEnumerator StepsBehind()
        {
            int n = Random.Range(3, 6);
            for (int i = 0; i < n; i++)
            {
                float dist = Mathf.Lerp(7f, 2.2f, i / (float)(n - 1));
                PlayAt(stepsBehind[Random.Range(0, stepsBehind.Length)], Behind(dist), Mathf.Lerp(0.45f, 0.85f, i / (float)n));
                yield return new WaitForSeconds(Random.Range(0.55f, 0.8f));
            }
        }

        /// <summary>Un chuchotement tout près de l'oreille (gauche ou droite).</summary>
        private void Whisper()
        {
            Vector3 side = transform.right * (Random.value < 0.5f ? -0.5f : 0.5f) - transform.forward * 0.15f;
            PlayAt(earWhispers[Random.Range(0, earWhispers.Length)], transform.position + side, 0.7f, 0.3f);
        }

        /// <summary>Une porte du Protocole Verrou, ouverte, se ferme toute seule (entre 8 et 25 m).</summary>
        private bool CloseFarDoor()
        {
            foreach (LockDoor door in LockDoor.All)
            {
                if (door == null || !door.IsOpen) continue;
                float d = Vector3.Distance(door.transform.position, transform.position);
                if (d < 8f || d > 25f) continue;
                door.SetOpen(false);
                if (doorSlam != null) PlayAt(doorSlam, door.transform.position, 0.9f);
                return true;
            }
            return false;
        }

        private Vector3 Behind(float distance)
        {
            Vector3 back = -transform.forward;
            back.y = 0f;
            back = back.sqrMagnitude > 0.01f ? back.normalized : Vector3.back;
            back = Quaternion.Euler(0f, Random.Range(-35f, 35f), 0f) * back;
            return transform.position + back * distance + Vector3.down * 0.9f;
        }

        private static void PlayAt(AudioClip clip, Vector3 pos, float volume, float minDistance = 1f)
        {
            if (clip == null) return;
            var go = new GameObject("Fausse alerte");
            go.transform.position = pos;
            AudioSource s = go.AddComponent<AudioSource>();
            s.clip = clip;
            s.spatialBlend = 1f;
            s.minDistance = minDistance;
            s.maxDistance = 30f;
            s.volume = volume;
            s.Play();
            Destroy(go, clip.length + 0.2f);
        }
    }
}
