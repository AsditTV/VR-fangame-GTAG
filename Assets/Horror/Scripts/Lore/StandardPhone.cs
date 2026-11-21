using System.Collections;
using UnityEngine;
using Horror.Interaction;
using Horror.Noise;

namespace Horror.Lore
{
    /// <summary>
    /// Le téléphone du Standard : il sonne au hasard (toutes les 1,5 à 3,5 min). La sonnerie est un BRUIT :
    /// L'Écoute vient. Si tu décroches (le bouton) pendant qu'il sonne, une voix chuchote un morceau de
    /// l'histoire (le texte s'écrit à côté du combiné). Un choix risqué : le lore contre la sécurité.
    /// </summary>
    public class StandardPhone : MonoBehaviour
    {
        public PressButton button;
        public TextMesh text;
        public AudioClip ring, whisper;
        public Vector2 delayRange = new Vector2(90f, 210f);
        public float ringTime = 12f;
        [Tooltip("Portée du bruit de la sonnerie (m) : L'Écoute l'entend de loin.")]
        public float ringNoise = 13f;
        [TextArea] public string[] messages =
        {
            "...Mila? Mila, answer me.\nIt's too loud. I can't hear you...",
            "Room 23 does not exist.\nStop counting the doors, Armand.",
            "Odette flooded the laundry.\nCeleste won't go near the water.",
            "The Softies want to play.\nDon't look away. Don't look away.",
            "If you hear the keys,\nstop breathing.",
            "She erased her own eyes\nso she wouldn't see our faces.",
        };

        private AudioSource _audio;
        private bool _ringing;
        private int _next;

        private void Awake()
        {
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.spatialBlend = 1f; _audio.maxDistance = 25f; _audio.loop = true; _audio.playOnAwake = false;
            if (text != null) text.text = "";
        }

        private void OnEnable() { if (button != null) button.onPressed.AddListener(PickUp); StartCoroutine(Loop()); }
        private void OnDisable() { if (button != null) button.onPressed.RemoveListener(PickUp); }

        private IEnumerator Loop()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(delayRange.x, delayRange.y));
                yield return Ring();
            }
        }

        /// <summary>Le fait sonner tout de suite (tests).</summary>
        [ContextMenu("Faire sonner")]
        public void RingNow() { if (!_ringing) StartCoroutine(Ring()); }

        private IEnumerator Ring()
        {
            _ringing = true;
            _audio.clip = ring; _audio.loop = true; _audio.Play();
            for (float t = 0f; t < ringTime && _ringing; t += 1f)
            {
                NoiseSystem.Emit(transform.position, ringNoise, NoiseType.Leurre, gameObject);   // elle entend la sonnerie
                yield return new WaitForSeconds(1f);
            }
            if (_ringing) { _audio.Stop(); _ringing = false; }
        }

        private void PickUp()
        {
            if (!_ringing) return;
            _ringing = false;
            _audio.Stop();
            if (whisper != null) _audio.PlayOneShot(whisper, 0.9f);
            if (text != null) StartCoroutine(Write(messages[_next % messages.Length]));
            _next++;
        }

        private IEnumerator Write(string message)
        {
            text.text = "";
            foreach (char c in message)
            {
                text.text += c;
                yield return new WaitForSeconds(0.045f);
            }
            yield return new WaitForSeconds(7f);
            text.text = "";
        }
    }
}
