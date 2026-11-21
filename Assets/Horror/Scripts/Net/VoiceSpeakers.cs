using System.Collections.Generic;
using UnityEngine;

namespace Horror.Net
{
    /// <summary>
    /// Annuaire des voix des autres joueurs : identifiant stable du joueur -> la source audio de sa voix.
    /// La voix réseau (Photon Voice) y inscrit chaque voix qui arrive ; NetworkAvatars la colle sur la tête de
    /// l'avatar (son 3D), coupe celles des joueurs coupés/bloqués et les étouffe derrière les murs.
    /// Aucune dépendance à Photon : le jeu compile et tourne sans.
    /// </summary>
    public static class VoiceSpeakers
    {
        private static readonly Dictionary<string, AudioSource> _byUser = new Dictionary<string, AudioSource>();

        public static void Register(string userId, AudioSource source)
        {
            if (string.IsNullOrEmpty(userId) || source == null) return;
            _byUser[userId] = source;
        }

        public static void Unregister(string userId)
        {
            if (!string.IsNullOrEmpty(userId)) _byUser.Remove(userId);
        }

        public static AudioSource Get(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return null;
            if (_byUser.TryGetValue(userId, out AudioSource s) && s != null) return s;
            _byUser.Remove(userId);
            return null;
        }

        public static IEnumerable<KeyValuePair<string, AudioSource>> All => _byUser;
    }
}
