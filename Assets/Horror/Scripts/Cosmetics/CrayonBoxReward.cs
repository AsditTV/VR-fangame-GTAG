using Horror.Interaction;
using UnityEngine;

namespace Horror.Cosmetics
{
    /// <summary>
    /// La boîte de crayons de la Salle de jeux : on appuie sur son bouton pour récolter 100 crayons
    /// toutes les 7 heures. Le panneau affiche (en anglais) :
    ///   FREE CRAYONS / READY! PRESS     ou     FREE CRAYONS / NEXT IN 06:42:13
    /// </summary>
    public class CrayonBoxReward : MonoBehaviour
    {
        public PressButton button;
        public TextMesh label;
        public AudioClip claimSound;
        public AudioClip deniedSound;

        private AudioSource _audio;
        private float _nextRefresh;

        private void Awake()
        {
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.spatialBlend = 1f;
            _audio.maxDistance = 8f;
            _audio.playOnAwake = false;
        }

        private void OnEnable() { if (button != null) button.onPressed.AddListener(OnPressed); Refresh(); }
        private void OnDisable() { if (button != null) button.onPressed.RemoveListener(OnPressed); }

        private void OnPressed()
        {
            bool ok = TimedReward.TryClaim();
            AudioClip clip = ok ? claimSound : deniedSound;
            if (clip != null) _audio.PlayOneShot(clip);
            Refresh();
        }

        private void Update()
        {
            if (Time.time < _nextRefresh) return;
            _nextRefresh = Time.time + 0.5f;   // le compte à rebours avance chaque seconde
            Refresh();
        }

        private void Refresh()
        {
            if (label == null) return;
            string title = "FREE " + GameNames.Currency + " (+" + TimedReward.Amount + ")";
            label.text = TimedReward.CanClaim ? title + "\nREADY! PRESS THE BUTTON"
                                              : title + "\nNEXT IN " + TimedReward.Format(TimedReward.Remaining);
        }
    }
}
