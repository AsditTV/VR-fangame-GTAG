using System;
using UnityEngine;

namespace Horror.Player
{
    /// <summary>Comment le micro est utilisé (réglé sur le Minitel).</summary>
    public enum MicMode
    {
        /// <summary>Toujours ouvert : les autres t'entendent... et L'Écoute aussi.</summary>
        Open,
        /// <summary>Appuyer sur A ou X pour parler.</summary>
        PushToTalk,
        /// <summary>Coupé : L'Écoute ne t'entend plus parler, mais tu ne peux plus prévenir les autres.</summary>
        Muted
    }

    /// <summary>
    /// Réglages du joueur, sauvegardés sur l'appareil : pseudo, couleur de la poupée, mode micro,
    /// dernier code de salle privée. Modifiés depuis le Minitel. Au multijoueur (étape 6), le pseudo
    /// et la couleur seront envoyés aux autres joueurs.
    /// </summary>
    public static class GameSettings
    {
        public static event Action Changed;

        /// <summary>Couleurs pastel proposées (tissu de la poupée).</summary>
        public static readonly Color[] Palette =
        {
            new Color(0.70f, 0.63f, 0.78f), // lilas (par défaut)
            new Color(0.85f, 0.65f, 0.70f), // rose poudré
            new Color(0.65f, 0.80f, 0.72f), // menthe
            new Color(0.66f, 0.74f, 0.88f), // bleu layette
            new Color(0.93f, 0.84f, 0.62f), // jaune paille
            new Color(0.92f, 0.74f, 0.64f), // pêche
            new Color(0.78f, 0.78f, 0.74f), // gris souris
            new Color(0.58f, 0.52f, 0.62f), // prune délavée
        };
        public static readonly string[] PaletteNames = { "LILAC", "DUSTY ROSE", "MINT", "BABY BLUE", "STRAW", "PEACH", "MOUSE GREY", "FADED PLUM" };

        public static string Name
        {
            get
            {
                // Premier lancement : un pseudo tiré au hasard UNE fois, puis gardé
                if (!PlayerPrefs.HasKey("horror.name")) { PlayerPrefs.SetString("horror.name", "FACE" + UnityEngine.Random.Range(10, 99)); PlayerPrefs.Save(); }
                return PlayerPrefs.GetString("horror.name");
            }
            set { PlayerPrefs.SetString("horror.name", Sanitize(value)); Save(); }
        }

        public static int ColorIndex
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt("horror.color", 0), 0, Palette.Length - 1);
            set { PlayerPrefs.SetInt("horror.color", (value % Palette.Length + Palette.Length) % Palette.Length); Save(); }
        }

        public static Color Color => Palette[ColorIndex];

        public static MicMode Mic
        {
            get => (MicMode)PlayerPrefs.GetInt("horror.mic", (int)MicMode.Open);
            set { PlayerPrefs.SetInt("horror.mic", (int)value); Save(); }
        }

        public static string RoomCode
        {
            get => PlayerPrefs.GetString("horror.room", "");
            set { PlayerPrefs.SetString("horror.room", value ?? ""); Save(); }
        }

        /// <summary>Pseudo : 12 caractères max, lettres/chiffres/espace seulement (le filtre des gros mots viendra avec le serveur).</summary>
        public static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "FACE";
            var sb = new System.Text.StringBuilder();
            foreach (char c in s.ToUpperInvariant())
                if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == ' ')
                    if (sb.Length < 12) sb.Append(c);
            return sb.Length == 0 ? "FACE" : sb.ToString();
        }

        private static void Save()
        {
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
