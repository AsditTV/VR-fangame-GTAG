using UnityEngine;

namespace Horror.Cosmetics
{
    /// <summary>Où se porte un cosmétique sur la poupée.</summary>
    public enum CosmeticSlot { Tete, Cou, Poitrine, Dos }

    /// <summary>
    /// Un cosmétique achetable (chapeau, noeud, écharpe...). C'est un fichier dans le projet :
    /// clic droit > Create > Horror > Cosmetic Item.
    /// Les textes visibles en jeu sont en ANGLAIS (displayName).
    /// </summary>
    [CreateAssetMenu(menuName = "Horror/Cosmetic Item", fileName = "NewCosmetic")]
    public class CosmeticItem : ScriptableObject
    {
        [Tooltip("Identifiant unique, ne jamais le changer après la sortie (sert à la sauvegarde).")]
        public string id = "paper_crown";
        [Tooltip("Nom affiché en jeu (anglais).")]
        public string displayName = "Paper Crown";
        [Tooltip("Prix en pièces (monnaie du jeu).")]
        public int price = 150;
        public CosmeticSlot slot = CosmeticSlot.Tete;
        [Tooltip("Le modèle 3D à porter. Son origine doit être le point d'attache (haut du crâne, cou...).")]
        public GameObject prefab;
        [Tooltip("Décalage / rotation / échelle sur le point d'attache.")]
        public Vector3 offset;
        public Vector3 rotation;
        public float scale = 1f;

        [Header("Argent réel (plus tard)")]
        [Tooltip("Identifiant du produit sur le store Meta / Steam. Vide = achetable seulement en pièces.")]
        public string storeProductId = "";
    }
}
