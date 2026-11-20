using System.Text;
using UnityEngine;

namespace Horror.Lore
{
    /// <summary>
    /// Panneau du journal dans la Salle de jeux (texte en anglais) :
    ///   PAPERS FOUND 4 / 13
    ///   1. Brochure, 1974
    ///   2. ???
    /// Les secrets ne sont comptés qu'une fois trouvés (ils restent secrets).
    /// </summary>
    [RequireComponent(typeof(TextMesh))]
    public class JournalBoard : MonoBehaviour
    {
        [Tooltip("Tous les documents du jeu, dans l'ordre.")]
        public LoreDocument[] documents;

        private TextMesh _text;

        private void Awake() => _text = GetComponent<TextMesh>();
        private void OnEnable() { LoreJournal.Discovered += OnFound; Refresh(); }
        private void OnDisable() => LoreJournal.Discovered -= OnFound;
        private void OnFound(LoreDocument _) => Refresh();

        private void Refresh()
        {
            if (documents == null) return;
            int total = 0, found = 0, secrets = 0;
            var sb = new StringBuilder();
            foreach (LoreDocument d in documents)
            {
                if (d == null) continue;
                bool isFound = LoreJournal.IsFound(d);
                if (d.isSecret)
                {
                    if (isFound) secrets++;
                    continue;
                }
                total++;
                if (isFound) found++;
                sb.Append(d.order).Append(". ").Append(isFound ? d.title : "???").Append('\n');
            }
            string header = "PAPERS FOUND " + found + " / " + total;
            if (secrets > 0) header += "   SECRETS " + secrets;
            _text.text = header + "\n\n" + sb;
        }
    }
}
