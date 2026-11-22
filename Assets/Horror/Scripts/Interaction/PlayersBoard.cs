using System.Collections.Generic;
using UnityEngine;
using Horror.Net;

namespace Horror.Interaction
{
    /// <summary>
    /// Le tableau PLAYERS de la Salle de jeux : la liste des autres joueurs, avec pour chacun
    /// MUTE (tu ne l'entends plus), BLOCK (coupé et caché, retenu d'une partie à l'autre) et REPORT
    /// (signalement enregistré, le joueur est aussi bloqué). En bas : MUTE ALL (couper tout le monde).
    /// En solo, la liste est vide (« Nobody else here »). Les lignes sont posées par le menu 8.
    /// </summary>
    public class PlayersBoard : MonoBehaviour
    {
        [System.Serializable]
        public class Row
        {
            public TextMesh label;
            public PressButton mute, block, report;
            public GameObject root;
            [HideInInspector] public int playerId = -1;
        }

        public Row[] rows;
        public PressButton muteAllButton;
        public TextMesh header, muteAllLabel;
        public AudioClip confirmSound;
        [Tooltip("Raison envoyée avec un signalement depuis ce tableau.")]
        public string reportReason = "voice chat / behaviour (reported from the playroom board)";

        private float _nextRefresh;

        private void OnEnable()
        {
            VoiceModeration.Changed += Refresh;
            if (muteAllButton != null) muteAllButton.onPressed.AddListener(ToggleMuteAll);
            if (rows == null) return;
            for (int i = 0; i < rows.Length; i++)
            {
                int k = i;
                if (rows[i].mute != null) rows[i].mute.onPressed.AddListener(() => Mute(k));
                if (rows[i].block != null) rows[i].block.onPressed.AddListener(() => Block(k));
                if (rows[i].report != null) rows[i].report.onPressed.AddListener(() => ReportRow(k));
            }
        }

        private void OnDisable()
        {
            VoiceModeration.Changed -= Refresh;
            if (muteAllButton != null) muteAllButton.onPressed.RemoveListener(ToggleMuteAll);
            if (rows != null) foreach (Row r in rows) { r.mute?.onPressed.RemoveAllListeners(); r.block?.onPressed.RemoveAllListeners(); r.report?.onPressed.RemoveAllListeners(); }
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextRefresh) return;   // les joueurs arrivent et partent : on relit la liste
            _nextRefresh = Time.unscaledTime + 1f;
            Refresh();
        }

        private static INetworkSession S => NetworkBootstrap.Session;

        public void Refresh()
        {
            IReadOnlyList<int> players = S != null ? S.RemotePlayers : null;
            int n = players != null ? players.Count : 0;
            if (header != null) header.text = (n == 0 ? "PLAYERS\n\nNobody else here." : "PLAYERS (" + n + ")")
                                              + (AgeGate.VoiceAllowed ? "" : "\nVOICE CHAT OFF (ACCOUNT AGE)");   // compte enfant
            if (muteAllLabel != null) muteAllLabel.text = VoiceModeration.MuteEveryone ? "MUTE ALL: ON" : "MUTE ALL: OFF";
            if (rows == null) return;
            for (int i = 0; i < rows.Length; i++)
            {
                Row r = rows[i];
                bool used = i < n;
                r.playerId = used ? players[i] : -1;
                if (r.root != null) r.root.SetActive(used);
                if (!used || r.label == null) continue;
                string uid = S.PlayerUserId(r.playerId);
                string state = VoiceModeration.IsBlocked(uid) ? "  [BLOCKED]" : VoiceModeration.IsMuted(uid) ? "  [MUTED]" : "";
                r.label.text = Clean(S.PlayerName(r.playerId)) + state;
            }
        }

        private static string Clean(string name) => string.IsNullOrWhiteSpace(name) ? "PLAYER" : (name.Length > 14 ? name.Substring(0, 14) : name).ToUpperInvariant();

        private bool Target(int row, out string uid, out string name)
        {
            uid = name = null;
            if (rows == null || row < 0 || row >= rows.Length || rows[row].playerId < 0 || S == null) return false;
            uid = S.PlayerUserId(rows[row].playerId);
            name = S.PlayerName(rows[row].playerId);
            return !string.IsNullOrEmpty(uid);
        }

        public void Mute(int row) { if (Target(row, out string uid, out _)) VoiceModeration.SetMuted(uid, !VoiceModeration.IsMuted(uid)); }
        public void Block(int row) { if (Target(row, out string uid, out _)) VoiceModeration.SetBlocked(uid, !VoiceModeration.IsBlocked(uid)); }

        public void ReportRow(int row)
        {
            if (!Target(row, out string uid, out string name)) return;
            VoiceModeration.ReportPlayer(uid, name, reportReason);
            if (confirmSound != null) AudioSource.PlayClipAtPoint(confirmSound, transform.position, 0.6f);
            if (rows[row].label != null) rows[row].label.text = Clean(name) + "  REPORTED. THANK YOU.";
            _nextRefresh = Time.unscaledTime + 3f;   // laisse le message affiché 3 s
        }

        public void ToggleMuteAll() => VoiceModeration.MuteEveryone = !VoiceModeration.MuteEveryone;
    }
}
