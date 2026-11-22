using UnityEngine;

namespace Horror.Net
{
    /// <summary>
    /// Adresses des services en ligne du jeu. Fichier : Assets/Horror/Resources/OnlineServices.asset.
    /// Ce ne sont PAS des secrets (une adresse de relais est publique) : le secret, l'adresse du webhook Discord,
    /// reste dans Cloudflare et n'entre jamais dans le jeu. Vide = le service est coupé (les signalements restent
    /// enregistrés dans le casque et partiront quand l'adresse sera remplie).
    /// </summary>
    [CreateAssetMenu(menuName = "Horror/Online Services", fileName = "OnlineServices")]
    public class OnlineServices : ScriptableObject
    {
        [Tooltip("Adresse du relais des signalements, se termine par /report (voir Tools/Relay/LISEZMOI.md).")]
        public string reportUrl = "";

        private static OnlineServices _instance;
        private static bool _loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _instance = null; _loaded = false; }

        public static OnlineServices Instance
        {
            get
            {
                if (!_loaded) { _instance = Resources.Load<OnlineServices>("OnlineServices"); _loaded = true; }
                return _instance;
            }
        }

        public static string ReportUrl => Instance != null ? (Instance.reportUrl ?? "").Trim() : "";
    }
}
