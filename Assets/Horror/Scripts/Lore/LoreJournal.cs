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
