using UnityEngine;

namespace Horror.Net
{
    /// <summary>
    /// L'identifiant de cet appareil : aléatoire, créé une fois, gardé dans PlayerPrefs. Aucune donnée personnelle.
    /// La forme courte (8 caractères, affichée sur le tableau PLAYERS : « YOUR ID ») sert à désigner un joueur
    /// sans connaître son pseudo : par exemple pour la liste des modérateurs.
    /// </summary>
    public static class PlayerIdentity
    {
        private const string KeyUserId = "horror.net.userid";

        public static string LocalUserId
        {
            get
            {
                string id = PlayerPrefs.GetString(KeyUserId, "");
                if (string.IsNullOrEmpty(id)) { id = System.Guid.NewGuid().ToString("N"); PlayerPrefs.SetString(KeyUserId, id); PlayerPrefs.Save(); }
                return id;
            }
        }

        /// <summary>Les 8 premiers caractères, en majuscules (« 3FA94C1B »).</summary>
        public static string Short(string userId) =>
            string.IsNullOrEmpty(userId) ? "" : (userId.Length > 8 ? userId.Substring(0, 8) : userId).ToUpperInvariant();
    }
}
