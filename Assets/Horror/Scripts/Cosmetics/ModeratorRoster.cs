using UnityEngine;
using Horror.Net;

namespace Horror.Cosmetics
{
    /// <summary>
    /// Les modérateurs du jeu (« la Garde de nuit ») et leur cosmétique réservé : la casquette d'Armand, le veilleur
    /// de nuit de l'Institut. Fichier : Assets/Horror/Resources/ModeratorRoster.asset. On ajoute un modérateur en
    /// collant son « YOUR ID » (tableau PLAYERS de la Salle de jeux, 8 caractères) dans la liste, puis on recompile l'APK.
    ///
    /// LIMITE (à connaître) : l'identifiant d'un joueur est déclaré par son propre casque. Tant que l'authentification
    /// Photon par le compte Meta n'est pas branchée, un client modifié pourrait se faire passer pour un modérateur et
    /// porter la casquette. La casquette ne donne aucun pouvoir ni avantage : seulement l'apparence.
    /// </summary>
    [CreateAssetMenu(menuName = "Horror/Moderator Roster", fileName = "ModeratorRoster")]
    public class ModeratorRoster : ScriptableObject
    {
        [Tooltip("« YOUR ID » de chaque modérateur (8 caractères, majuscules ou minuscules).")]
        public string[] ids = new string[0];
        [Tooltip("La casquette du veilleur (créée par le menu Horror/Outils/Casquette du veilleur).")]
        public GameObject capPrefab;
        [Tooltip("Position de la casquette par rapport au point de vue (la tête) du joueur, en mètres.")]
        public Vector3 capOffset = new Vector3(0f, 0.105f, -0.045f);
        public Vector3 capRotation = new Vector3(-6f, 0f, 0f);
        public float capScale = 1.4f;   // réglé sur la tête de la poupée
        [Tooltip("Texte ajouté sous le pseudo d'un modérateur.")]
        public string tagLine = "NIGHT WATCH";

        private static ModeratorRoster _instance;
        private static bool _loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _instance = null; _loaded = false; }

        public static ModeratorRoster Instance
        {
            get
            {
                if (!_loaded) { _instance = Resources.Load<ModeratorRoster>("ModeratorRoster"); _loaded = true; }
                return _instance;
            }
        }

        public static bool IsModerator(string userId)
        {
            ModeratorRoster r = Instance;
            string shortId = PlayerIdentity.Short(userId);
            if (r == null || r.ids == null || shortId.Length < 8) return false;
            foreach (string id in r.ids)
                if (!string.IsNullOrWhiteSpace(id) && PlayerIdentity.Short(id.Trim()) == shortId) return true;
            return false;
        }

        /// <summary>Pose la casquette sur une tête (point de vue d'un joueur). Renvoie null si elle n'existe pas.</summary>
        public static GameObject AttachCap(Transform head)
        {
            ModeratorRoster r = Instance;
            if (r == null || r.capPrefab == null || head == null) return null;
            GameObject cap = Instantiate(r.capPrefab, head);
            cap.name = "Casquette du veilleur (modérateur)";
            cap.transform.localPosition = r.capOffset;
            cap.transform.localRotation = Quaternion.Euler(r.capRotation);
            cap.transform.localScale = Vector3.one * r.capScale;
            foreach (Collider c in cap.GetComponentsInChildren<Collider>(true)) Destroy(c);
            foreach (Light l in cap.GetComponentsInChildren<Light>(true)) Destroy(l);   // aucune lumière : pas d'avantage en jeu
            return cap;
        }
    }
}
