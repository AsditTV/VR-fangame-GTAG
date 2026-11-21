using System.Collections;
using UnityEngine;
using Horror.Interaction;
using Horror.Noise;

namespace Horror.World
{
    /// <summary>
    /// La boîte à musique leurre : prends-la, lance-la (ou pose-la) loin de toi. Une fois immobile, elle joue sa
    /// petite mélodie ~18 s : c'est un BRUIT (Leurre), L'Écoute y va au lieu de venir vers toi. Puis le ressort
    /// casse (un « tchac »), elle se tait, et réapparaît à sa place quelques minutes plus tard (si personne n'est à côté).
    /// La danseuse tourne pendant la musique.
    /// </summary>
    [RequireComponent(typeof(Grabbable))]
    public class MusicBoxLure : MonoBehaviour
    {
        public AudioClip melody, snap;
        public float playTime = 18f;
        [Tooltip("Portée du bruit de la mélodie (m).")]
        public float noiseRadius = 14f;
        public float respawnDelay = 150f;
        public Transform dancer;

        private Grabbable _grab;
        private AudioSource _audio;
        private Vector3 _home; private Quaternion _homeRot;
        private bool _armed, _playing, _spent;

        private void Awake()
        {
            _grab = GetComponent<Grabbable>();
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.spatialBlend = 1f; _audio.minDistance = 1.5f; _audio.maxDistance = 22f; _audio.loop = true; _audio.playOnAwake = false;
            _home = transform.position; _homeRot = transform.rotation;
        }

        private void OnEnable() { _grab.onGrabbed.AddListener(OnGrabbed); _grab.onReleased.AddListener(OnReleased); }
        private void OnDisable() { _grab.onGrabbed.RemoveListener(OnGrabbed); _grab.onReleased.RemoveListener(OnReleased); }

        // la prendre la remonte (un petit cliquetis serait idéal : la manivelle tourne toute seule)
        private void OnGrabbed(HandGrabber hand) { if (!_spent) _armed = true; }

        private void OnReleased(HandGrabber hand) { if (_armed && !_playing) StartCoroutine(Play()); }

        /// <summary>La lance tout de suite à sa place (tests).</summary>
        [ContextMenu("Jouer")]
        public void PlayNow() { if (!_playing) { _armed = true; StartCoroutine(Play()); } }

        private IEnumerator Play()
        {
            _playing = true;
            // attendre qu'elle se pose (ou 2 s au plus)
            for (float t = 0f; t < 2f; t += Time.deltaTime)
            {
                if (_grab.Holder != null) { _playing = false; yield break; }   // reprise en main : pas encore
                if (_grab.Body == null || _grab.Body.linearVelocity.sqrMagnitude < 0.04f) break;
                yield return null;
            }
            if (melody != null) { _audio.clip = melody; _audio.Play(); }
            float next = 0f;
            for (float t = 0f; t < playTime; t += Time.deltaTime)
            {
                if (t >= next) { next = t + 1f; NoiseSystem.Emit(transform.position, noiseRadius, NoiseType.Leurre, gameObject); }
                if (dancer != null) dancer.Rotate(Vector3.up, 140f * Time.deltaTime, Space.Self);
                _audio.pitch = Mathf.Lerp(1f, 0.82f, Mathf.Max(0f, t - playTime + 3f) / 3f);   // le ressort qui fatigue
                yield return null;
            }
            _audio.Stop(); _audio.pitch = 1f;
            if (snap != null) _audio.PlayOneShot(snap, 0.9f);
            NoiseSystem.Emit(transform.position, noiseRadius * 0.5f, NoiseType.Objet, gameObject);
            _spent = true; _armed = false; _playing = false;
            yield return new WaitForSeconds(respawnDelay);
            // retour à sa place, seulement si personne n'est tout près (on ne la voit pas réapparaître)
            while (Horror.Locomotion.ArmLocomotion.All.Exists(p => p != null && (p.HeadPosition - _home).sqrMagnitude < 36f))
                yield return new WaitForSeconds(5f);
            if (_grab.Body != null) { _grab.Body.linearVelocity = Vector3.zero; _grab.Body.angularVelocity = Vector3.zero; }
            transform.SetPositionAndRotation(_home, _homeRot);
            _spent = false;
        }
    }
}
