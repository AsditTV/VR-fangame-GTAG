using UnityEngine;
using Horror.Interaction;

namespace Horror.Player
{
    /// <summary>
    /// Options de confort (panneau « COMFORT » de la Salle de jeux), gardées d'une partie à l'autre :
    ///  - FLASHES : les flashs rouges des screamers (à couper pour les joueurs sensibles à la lumière) ;
    ///  - SCARES  : FULL (le « double coup » : arrêt silencieux puis saut, cri fort) ou SOFT (pas d'arrêt,
    ///    screamer plus court, cri moins fort). Le jeu reste effrayant, mais moins brutal.
    ///  - MOVES   : FAST (préréglage « Rapide » de ArmLocomotion : grands élans, glisse) ou CLASSIC (l'original).
    /// </summary>
    public class ComfortSettings : MonoBehaviour
    {
        public PressButton flashesButton, scaresButton, movesButton;
        public TextMesh display;
        public AudioClip toggleSound;

        private const string KeyFlashes = "horror.comfort.flashes", KeySoft = "horror.comfort.soft", KeyFast = "horror.comfort.fastmove";
        public static bool Flashes { get => PlayerPrefs.GetInt(KeyFlashes, 1) == 1; set { PlayerPrefs.SetInt(KeyFlashes, value ? 1 : 0); PlayerPrefs.Save(); } }
        public static bool SoftScares { get => PlayerPrefs.GetInt(KeySoft, 0) == 1; set { PlayerPrefs.SetInt(KeySoft, value ? 1 : 0); PlayerPrefs.Save(); } }
        public static bool FastMoves { get => PlayerPrefs.GetInt(KeyFast, 1) == 1; set { PlayerPrefs.SetInt(KeyFast, value ? 1 : 0); PlayerPrefs.Save(); } }

        private void OnEnable()
        {
            if (flashesButton != null) flashesButton.onPressed.AddListener(ToggleFlashes);
            if (scaresButton != null) scaresButton.onPressed.AddListener(ToggleScares);
            if (movesButton != null) movesButton.onPressed.AddListener(ToggleMoves);
        }

        private void OnDisable()
        {
            if (flashesButton != null) flashesButton.onPressed.RemoveListener(ToggleFlashes);
            if (scaresButton != null) scaresButton.onPressed.RemoveListener(ToggleScares);
            if (movesButton != null) movesButton.onPressed.RemoveListener(ToggleMoves);
        }

        private void Start() => ApplyAll();

        public void ToggleFlashes() { Flashes = !Flashes; ApplyAll(); Click(); }
        public void ToggleScares() { SoftScares = !SoftScares; ApplyAll(); Click(); }
        public void ToggleMoves() { FastMoves = !FastMoves; ApplyAll(); Click(); }

        private void Click() { if (toggleSound != null) AudioSource.PlayClipAtPoint(toggleSound, transform.position, 0.6f); }

        /// <summary>Applique les options à tous les joueurs (Jumpscare) de la scène.</summary>
        public void ApplyAll()
        {
            foreach (Jumpscare js in FindObjectsByType<Jumpscare>(FindObjectsInactive.Include)) Apply(js);
            foreach (Horror.Locomotion.ArmLocomotion loco in FindObjectsByType<Horror.Locomotion.ArmLocomotion>(FindObjectsInactive.Include)) ApplyMoves(loco);
            if (display != null)
                display.text = "COMFORT\n\nFLASHES  " + (Flashes ? "ON" : "OFF") + "\nSCARES   " + (SoftScares ? "SOFT" : "FULL") + "\nMOVES    " + (FastMoves ? "FAST" : "CLASSIC");
        }

        public static void ApplyMoves(Horror.Locomotion.ArmLocomotion loco)
        {
            if (loco.preset == Horror.Locomotion.ArmLocomotion.Preset.Personnalise) return;   // réglé à la main : on n'y touche pas
            loco.preset = FastMoves ? Horror.Locomotion.ArmLocomotion.Preset.Rapide : Horror.Locomotion.ArmLocomotion.Preset.Classique;
            loco.ApplyPreset();
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
