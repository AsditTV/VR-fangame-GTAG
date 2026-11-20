using System;
using System.Collections.Generic;
using UnityEngine;

namespace Horror.Cosmetics
{
    /// <summary>
    /// Le porte-monnaie du joueur : pièces, cosmétiques achetés, cosmétiques portés.
    /// Sauvegardé sur l'appareil (PlayerPrefs).
    ///
    /// ATTENTION : une sauvegarde locale se triche facilement. C'est très bien pour tester, mais
    /// pour le vrai jeu (et surtout avec de l'argent réel), les pièces et les achats devront être
    /// gardés sur un serveur (PlayFab, prévu avec le classement). Cette classe est faite pour être
    /// remplacée à ce moment-là sans toucher au reste.
    /// </summary>
    public static class PlayerWallet
    {
        private const string CoinsKey = "horror.coins";
        private const string OwnedKey = "horror.owned";
        private const string EquippedKey = "horror.equipped.";

        /// <summary>Appelé à chaque changement (pièces, achat, équipement).</summary>
        public static event Action Changed;

        private static HashSet<string> s_owned;

        public static int Coins => PlayerPrefs.GetInt(CoinsKey, 100); // 100 crayons offerts au départ

        public static void AddCoins(int amount)
        {
            if (amount == 0) return;
            PlayerPrefs.SetInt(CoinsKey, Mathf.Max(0, Coins + amount));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static bool Owns(CosmeticItem item) => item != null && Owned.Contains(item.id);

        /// <summary>Achète avec des pièces. Renvoie faux si pas assez de pièces ou déjà possédé.</summary>
        public static bool TryBuy(CosmeticItem item)
        {
            if (item == null || Owns(item) || Coins < item.price) return false;
            PlayerPrefs.SetInt(CoinsKey, Coins - item.price);
            Owned.Add(item.id);
            SaveOwned();
            Changed?.Invoke();
            return true;
        }

        public static string EquippedId(CosmeticSlot slot) => PlayerPrefs.GetString(EquippedKey + slot, "");

        public static bool IsEquipped(CosmeticItem item) => item != null && EquippedId(item.slot) == item.id;

        /// <summary>Porte l'objet (ou le retire s'il est déjà porté). Un seul objet par emplacement.</summary>
        public static void ToggleEquip(CosmeticItem item)
        {
            if (!Owns(item)) return;
            PlayerPrefs.SetString(EquippedKey + item.slot, IsEquipped(item) ? "" : item.id);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        private static HashSet<string> Owned
        {
            get
            {
                if (s_owned == null)
                {
                    s_owned = new HashSet<string>();
                    foreach (string id in PlayerPrefs.GetString(OwnedKey, "").Split(','))
                        if (!string.IsNullOrEmpty(id)) s_owned.Add(id);
                }
                return s_owned;
            }
        }

        private static void SaveOwned()
        {
            PlayerPrefs.SetString(OwnedKey, string.Join(",", Owned));
            PlayerPrefs.Save();
        }
    }
}
