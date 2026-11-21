using UnityEngine;
using Horror.Interaction;

namespace Horror.Player
{
    /// <summary>
    /// Options de confort (panneau « COMFORT » de la Salle de jeux), gardées d'une partie à l'autre :
    ///  - FLASHES : les flashs rouges des screamers (à couper pour les joueurs sensibles à la lumière) ;
    ///  - SCARES  : FULL (le « double coup » : arrêt silencieux puis saut, cri fort) ou SOFT (pas d'arrêt,
    ///    screamer plus court, cri moins fort). Le jeu reste effrayant, mais moins brutal.
    /// </summary>
    public class ComfortSettings : MonoBehaviour
    {
        public PressButton flashesButton, scaresButton;
        public TextMesh display;
        public AudioClip toggleSound;

        private const string KeyFlashes = "horror.comfort.flashes", KeySoft = "horror.comfort.soft";
        public static bool Flashes { get => PlayerPrefs.GetInt(KeyFlashes, 1) == 1; set { PlayerPrefs.SetInt(KeyFlashes, value ? 1 : 0); PlayerPrefs.Save(); } }
        public static bool SoftScares { get => PlayerPrefs.GetInt(KeySoft, 0) == 1; set { PlayerPrefs.SetInt(KeySoft, value ? 1 : 0); PlayerPrefs.Save(); } }

        private void OnEnable()
        {
            if (flashesButton != null) flashesButton.onPressed.AddListener(ToggleFlashes);
            if (scaresButton != null) scaresButton.onPressed.AddListener(ToggleScares);
        }

        private void OnDisable()
        {
            if (flashesButton != null) flashesButton.onPressed.RemoveListener(ToggleFlashes);
            if (scaresButton != null) scaresButton.onPressed.RemoveListener(ToggleScares);
        }

        private void Start() => ApplyAll();

        public void ToggleFlashes() { Flashes = !Flashes; ApplyAll(); Click(); }
        public void ToggleScares() { SoftScares = !SoftScares; ApplyAll(); Click(); }

        private void Click() { if (toggleSound != null) AudioSource.PlayClipAtPoint(toggleSound, transform.position, 0.6f); }

        /// <summary>Applique les options à tous les joueurs (Jumpscare) de la scène.</summary>
        public void ApplyAll()
        {
            foreach (Jumpscare js in FindObjectsByType<Jumpscare>(FindObjectsInactive.Include)) Apply(js);
            if (display != null)
                display.text = "COMFORT\n\nFLASHES  " + (Flashes ? "ON" : "OFF") + "\nSCARES   " + (SoftScares ? "SOFT" : "FULL");
        }

        public static void Apply(Jumpscare js)
        {
            js.strobe = Flashes;
            bool soft = SoftScares;
            js.stareTime = soft ? 0f : 0.4f;
            js.faceTime = soft ? 0.5f : 0.9f;
            js.volume = soft ? 0.55f : 1f;
        }
    }
}
