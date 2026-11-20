using System;
using UnityEngine;

namespace Horror.Cosmetics
{
    /// <summary>
    /// Récompense périodique : 100 crayons toutes les 7 heures (voir Docs/ECONOMIE.md).
    /// La première récolte est disponible tout de suite.
    ///
    /// Sécurité : l'heure vient de l'appareil. Si l'horloge recule, le compte à rebours repart de
    /// maintenant (pas de récolte gratuite). Avancer l'horloge reste possible tant que l'heure ne vient
    /// pas d'un serveur : à remplacer par l'heure PlayFab avant la sortie (même interface).
    /// </summary>
    public static class TimedReward
    {
        public const int Amount = 100;
        public static readonly TimeSpan Cooldown = TimeSpan.FromHours(7);
        private const string Key = "horror.reward.last";

        public static event Action Claimed;

        private static DateTime? LastClaim
        {
            get
            {
                string s = PlayerPrefs.GetString(Key, "");
                return long.TryParse(s, out long ticks) ? new DateTime(ticks, DateTimeKind.Utc) : (DateTime?)null;
            }
            set
            {
                PlayerPrefs.SetString(Key, value.HasValue ? value.Value.Ticks.ToString() : "");
                PlayerPrefs.Save();
            }
        }

        /// <summary>Temps restant avant la prochaine récolte (zéro = disponible).</summary>
        public static TimeSpan Remaining
        {
            get
            {
                DateTime now = DateTime.UtcNow;
                DateTime? last = LastClaim;
                if (!last.HasValue) return TimeSpan.Zero;
                if (now < last.Value)             // horloge reculée : on repart de maintenant
                {
                    LastClaim = now;
                    return Cooldown;
                }
                TimeSpan left = last.Value + Cooldown - now;
                return left > TimeSpan.Zero ? left : TimeSpan.Zero;
            }
        }

        public static bool CanClaim => Remaining <= TimeSpan.Zero;

        /// <summary>Récolte si c'est l'heure. Renvoie vrai si des crayons ont été donnés.</summary>
        public static bool TryClaim()
        {
            if (!CanClaim) return false;
            LastClaim = DateTime.UtcNow;
            PlayerWallet.AddCoins(Amount);
            Claimed?.Invoke();
            return true;
        }

        /// <summary>"06:42:13".</summary>
        public static string Format(TimeSpan t) =>
            string.Format("{0:00}:{1:00}:{2:00}", (int)t.TotalHours, t.Minutes, t.Seconds);
    }
}
