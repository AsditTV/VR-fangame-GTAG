using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace Horror.Net
{
    /// <summary>
    /// Envoie les signalements (bouton REPORT du tableau PLAYERS) au relais des modérateurs, qui les poste dans
    /// le salon Discord des modos. Un signalement à la fois, le plus ancien d'abord. S'il n'y a pas de réseau ou si
    /// le relais ne répond pas, on réessaie plus tard (de plus en plus espacé) : rien n'est perdu, tout reste dans le
    /// fichier local de VoiceModeration. Créé tout seul au chargement ; ne fait rien si l'adresse du relais est vide.
    /// </summary>
    public class ReportRelay : MonoBehaviour
    {
        [System.Serializable]
        private class Payload
        {
            public string game = "calm-faces";
            public string reportedUserId, reportedName, reporterUserId, reason, utc, mode, version;
        }

        private const float FirstDelay = 2f, MaxDelay = 300f;
        private float _nextTry, _delay = 15f;
        private bool _busy;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (FindAnyObjectByType<ReportRelay>() != null) return;
            var go = new GameObject("Relais des signalements");
            DontDestroyOnLoad(go);
            go.AddComponent<ReportRelay>();
        }

        private void OnEnable() { VoiceModeration.ReportQueued += SoonPlease; _nextTry = Time.unscaledTime + FirstDelay; }
        private void OnDisable() => VoiceModeration.ReportQueued -= SoonPlease;

        private void SoonPlease() { _nextTry = Mathf.Min(_nextTry, Time.unscaledTime + 0.5f); _delay = 15f; }

        private void Update()
        {
            if (_busy || Time.unscaledTime < _nextTry) return;
            string url = OnlineServices.ReportUrl;
            if (string.IsNullOrEmpty(url) || !VoiceModeration.NextPendingReport(out VoiceModeration.Report r))
            {
                _nextTry = Time.unscaledTime + 30f;   // rien à envoyer : on regarde de temps en temps
                return;
            }
            StartCoroutine(Send(url, r));
        }

        private IEnumerator Send(string url, VoiceModeration.Report r)
        {
            _busy = true;
            var p = new Payload
            {
                reportedUserId = r.reportedUserId, reportedName = r.reportedName, reporterUserId = r.reporterUserId,
                reason = r.reason, utc = r.utc, mode = r.mode, version = Application.version,
            };
            byte[] body = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(p));
            using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                req.uploadHandler = new UploadHandlerRaw(body);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = 15;
                yield return req.SendWebRequest();

                long code = req.responseCode;
                if (req.result == UnityWebRequest.Result.Success && code >= 200 && code < 300)
                {
                    VoiceModeration.MarkReportSent(r);
                    Debug.Log("[Horror] Signalement transmis aux modérateurs.");
                    _delay = 15f;
                    _nextTry = Time.unscaledTime + 1f;          // le suivant, s'il y en a
                }
                else if (code == 400)
                {
                    // refusé pour de bon (signalement mal formé) : inutile de réessayer en boucle
                    VoiceModeration.MarkReportSent(r);
                    Debug.LogWarning("[Horror] Signalement refusé par le relais (mal formé), abandonné.");
                    _nextTry = Time.unscaledTime + 1f;
                }
                else
                {
                    // pas de réseau, relais saturé (429) ou en panne : on réessaiera, de plus en plus espacé
                    _nextTry = Time.unscaledTime + _delay;
                    _delay = Mathf.Min(MaxDelay, _delay * 2f);
                    Debug.Log("[Horror] Signalement gardé pour plus tard (" + (code > 0 ? "code " + code : req.error) + ").");
                }
            }
            _busy = false;
        }
    }
}
