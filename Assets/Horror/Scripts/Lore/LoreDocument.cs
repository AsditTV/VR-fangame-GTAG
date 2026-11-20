using UnityEngine;

namespace Horror.Lore
{
    /// <summary>
    /// Un document de lore (papier, lettre, coupure de journal, dessin...). Fichier du projet :
    /// clic droit > Create > Horror > Lore Document. Les images sont générées par Tools/Papers/make_papers.py.
    /// Les textes sont DANS l'image (en anglais) : rien à traduire dans le code.
    /// </summary>
    [CreateAssetMenu(menuName = "Horror/Lore Document", fileName = "NewLoreDocument")]
    public class LoreDocument : ScriptableObject
    {
        [Tooltip("Identifiant unique (sauvegarde des papiers trouvés). Ne pas changer après la sortie.")]
        public string id = "p01_brochure";
        [Tooltip("Titre court affiché dans le journal (anglais).")]
        public string title = "Brochure, 1974";
        [Tooltip("Ordre dans l'histoire (1 = premier papier à trouver).")]
        public int order = 1;
        public bool isSecret;
        public Texture2D front;
        [Tooltip("Verso (facultatif). Vide = papier vierge.")]
        public Texture2D back;
        [Tooltip("Taille du papier (m). A5 = 0.148 x 0.21.")]
        public Vector2 size = new Vector2(0.148f, 0.21f);
        [Tooltip("Pièces gagnées la première fois qu'on le trouve.")]
        public int coinReward = 25;
    }
}
