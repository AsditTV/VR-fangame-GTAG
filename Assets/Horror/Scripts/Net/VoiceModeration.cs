using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Horror.Net
{
    /// <summary>
    /// Sécurité du chat vocal (exigée par les stores pour un jeu où des inconnus se parlent) :
    ///  - COUPER un joueur : tu ne l'entends plus (pour cette partie) ;
    ///  - BLOQUER : coupé ET retenu d'une partie à l'autre (son avatar est caché, sa voix aussi) ;
    ///  - SIGNALER : le signalement est enregistré (joueur, raison, heure) puis transmis par <see cref="ReportSender"/>.
    ///    Tant qu'aucun service n'est branché (Meta Platform SDK : rapport d'abus), il reste dans un fichier local.
    ///  - COUPER TOUT LE MONDE : un seul interrupteur, retenu.
    /// L'adaptateur Photon Voice lira <see cref="ShouldHear"/> pour régler le volume de chaque voix.
    /// Les identifiants sont ceux du COMPTE (stables), pas ceux de la session.
    /// </summary>
    public static class VoiceModeration
    {
        public static event Action Changed;

        /// <summary>Reçoit chaque signalement (à brancher sur le service de signalement du store). Vrai = transmis.</summary>
        public static Func<Report, bool> ReportSender;

        [Serializable]
        public struct Report
        {
            public string reportedUserId, reportedName, reason, utc;
            public bool sent;
        }

        [Serializable] private class Store { public List<string> blocked = new List<string>(); public List<Report> reports = new List<Report>(); }

        private const string KeyMuteAll = "horror.voice.muteall";
        private static readonly HashSet<string> _muted = new HashSet<string>();
        private static Store _store;

        private static string FilePath => Path.Combine(Application.persistentDataPath, "voice_moderation.json");

        private static Store Data
        {
            get
            {
                if (_store != null) return _store;
                try { _store = File.Exists(FilePath) ? JsonUtility.FromJson<Store>(File.ReadAllText(FilePath)) : null; }
                catch (Exception e) { Debug.LogWarning("[Horror] Liste de blocage illisible, on repart à zéro : " + e.Message); }
                return _store ??= new Store();
            }
        }

        private static void Save()
        {
            try { File.WriteAllText(FilePath, JsonUtility.ToJson(Data, true)); }
            catch (Exception e) { Debug.LogWarning("[Horror] Impossible d'enregistrer la liste de blocage : " + e.Message); }
        }

        public static bool MuteEveryone
        {
            get => PlayerPrefs.GetInt(KeyMuteAll, 0) == 1;
            set { PlayerPrefs.SetInt(KeyMuteAll, value ? 1 : 0); PlayerPrefs.Save(); Changed?.Invoke(); }
        }

        public static bool IsMuted(string userId) => !string.IsNullOrEmpty(userId) && _muted.Contains(userId);
        public static bool IsBlocked(string userId) => !string.IsNullOrEmpty(userId) && Data.blocked.Contains(userId);

        /// <summary>Doit-on entendre ce joueur ? (lu par la voix réseau)</summary>
        public static bool ShouldHear(string userId) => !MuteEveryone && !IsMuted(userId) && !IsBlocked(userId);

        public static void SetMuted(string userId, bool muted)
        {
            if (string.IsNullOrEmpty(userId)) return;
            if (muted) _muted.Add(userId); else _muted.Remove(userId);
            Changed?.Invoke();
        }

        public static void SetBlocked(string userId, bool blocked)
        {
            if (string.IsNullOrEmpty(userId)) return;
            if (blocked && !Data.blocked.Contains(userId)) Data.blocked.Add(userId);
            if (!blocked) Data.blocked.Remove(userId);
            Save();
            Changed?.Invoke();
        }

        /// <summary>Signale un joueur. Il est aussi bloqué : on n'a pas à continuer d'entendre quelqu'un qu'on signale.</summary>
        public static void ReportPlayer(string userId, string name, string reason)
        {
            if (string.IsNullOrEmpty(userId)) return;
            var r = new Report { reportedUserId = userId, reportedName = name, reason = reason, utc = DateTime.UtcNow.ToString("o") };
            try { r.sent = ReportSender != null && ReportSender(r); }
            catch (Exception e) { Debug.LogWarning("[Horror] Envoi du signalement impossible, gardé pour plus tard : " + e.Message); }
            Data.reports.Add(r);
            if (!Data.blocked.Contains(userId)) Data.blocked.Add(userId);
            Save();
            Changed?.Invoke();
        }

        public static int PendingReports { get { int n = 0; foreach (Report r in Data.reports) if (!r.sent) n++; return n; } }

        /// <summary>Oublie tout (tests).</summary>
        public static void ResetAll()
        {
            _muted.Clear();
            _store = new Store();
            Save();
            PlayerPrefs.DeleteKey(KeyMuteAll);
            Changed?.Invoke();
        }
    }
}
