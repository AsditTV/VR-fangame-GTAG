using UnityEngine;
using Horror.Interaction;

namespace Horror.World
{
    /// <summary>
    /// Le mur des modes dans la Salle de jeux (le « lobby ») : on appuie sur le mode qu'on veut jouer.
    ///  - EXPLORATION : le mode de base (fuir les monstres, trouver les papiers et les lieux, la fin du n°23) ;
    ///  - INK TAG et HIDE & SEEK : modes en ligne, prévus avec le multijoueur (Photon) : « ONLINE SOON ».
    /// Le choix est gardé (PlayerPrefs « horror.mode ») : le futur code réseau s'en servira pour choisir la salle.
    /// </summary>
    public class ModeSelector : MonoBehaviour
    {
        [System.Serializable]
        public class Mode
        {
            public string id;
            public string title;
            public string description;
            public bool available;
            public PressButton button;
        }

        public Mode[] modes = new Mode[0];
        public TextMesh screen;
        public AudioClip selectSound, deniedSound;

        public const string Key = "horror.mode";
        public static string Current => PlayerPrefs.GetString(Key, "exploration");

        private void OnEnable()
        {
            foreach (Mode m in modes)
            {
                Mode mm = m;
                if (m.button != null) m.button.onPressed.AddListener(() => Choose(mm));
            }
            Show(null);
        }

        private void Choose(Mode m)
        {
            if (!m.available)
            {
                if (deniedSound != null) AudioSource.PlayClipAtPoint(deniedSound, transform.position, 0.6f);
                Show(m.title + "\n\nONLINE SOON\n(play with friends)");
                return;
            }
            PlayerPrefs.SetString(Key, m.id);
            PlayerPrefs.Save();
            if (selectSound != null) AudioSource.PlayClipAtPoint(selectSound, transform.position, 0.7f);
            Show(null);
        }

        private void Show(string message)
        {
            if (screen == null) return;
            if (message != null) { screen.text = message; return; }
            string cur = Current;
            foreach (Mode m in modes)
                if (m.id == cur) screen.text = "MODE: " + m.title + "\n\n" + m.description + "\n\nLEAVE THROUGH THE DOOR";
        }
    }
}
