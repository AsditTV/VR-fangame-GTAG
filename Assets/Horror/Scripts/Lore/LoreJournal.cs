using System;
using System.Collections.Generic;
using UnityEngine;

namespace Horror.Lore
{
    /// <summary>
    /// Le journal des papiers trouvés (sauvegardé sur l'appareil). Un papier trouvé une fois reste
    /// trouvé, même après avoir quitté le jeu. Le panneau de la Salle de jeux l'affiche.
    /// </summary>
    public static class LoreJournal
    {
        private const string Key = "horror.lore.found";
        private static HashSet<string> s_found;

        /// <summary>Un nouveau papier vient d'être trouvé.</summary>
        public static event Action<LoreDocument> Discovered;

        /// <summary>Bonus de progression (voir Docs/ECONOMIE.md).</summary>
        public const int StoryBonus = 1000;     // les 13 papiers de l'histoire
        public const int SecretsBonus = 2000;   // les 4 secrets (la vraie fin)

        /// <summary>Un palier vient d'être atteint (texte anglais, bonus).</summary>
        public static event Action<string, int> MilestoneReached;

        /// <summary>
        /// Vérifie les paliers (histoire complète, tous les secrets) et donne le bonus UNE seule fois.
        /// Appelé par le journal quand un papier est trouvé.
        /// </summary>
        public static void CheckMilestones(LoreDocument[] all)
        {
            if (all == null) return;
            int story = 0, storyFound = 0, secrets = 0, secretsFound = 0;
            foreach (LoreDocument d in all)
            {
                if (d == null) continue;
                if (d.isSecret) { secrets++; if (IsFound(d)) secretsFound++; }
                else { story++; if (IsFound(d)) storyFound++; }
            }
            Award("horror.lore.bonus.story", story > 0 && storyFound == story, "STORY COMPLETE", StoryBonus);
            Award("horror.lore.bonus.secrets", secrets > 0 && secretsFound == secrets, "ALL SECRETS FOUND", SecretsBonus);
        }

        private static void Award(string key, bool reached, string label, int bonus)
        {
            if (!reached || PlayerPrefs.GetInt(key, 0) == 1) return;
            PlayerPrefs.SetInt(key, 1);
            PlayerPrefs.Save();
            Horror.Cosmetics.PlayerWallet.AddCoins(bonus);
            MilestoneReached?.Invoke(label, bonus);
        }

        public static bool IsFound(LoreDocument doc) => doc != null && Found.Contains(doc.id);
        public static int Count => Found.Count;

        /// <summary>Marque le papier comme trouvé. Renvoie vrai si c'est la première fois.</summary>
        public static bool MarkFound(LoreDocument doc)
        {
            if (doc == null || Found.Contains(doc.id)) return false;
            Found.Add(doc.id);
            PlayerPrefs.SetString(Key, string.Join(",", Found));
            PlayerPrefs.Save();
            Discovered?.Invoke(doc);
            return true;
        }

        private static HashSet<string> Found
        {
            get
            {
                if (s_found == null)
                {
                    s_found = new HashSet<string>();
                    foreach (string id in PlayerPrefs.GetString(Key, "").Split(','))
                        if (!string.IsNullOrEmpty(id)) s_found.Add(id);
                }
                return s_found;
            }
        }
    }
}
