using System.Linq;
using UnityEngine;
using Horror.Cosmetics;
using Horror.Locomotion;
using Horror.Lore;

namespace Horror.World
{
    /// <summary>
    /// Le but du mode Exploration (sans le dire trop fort) : trouver les papiers, découvrir tous les lieux de
    /// l'Institut. Quand tout est trouvé, la machine n°23 de la Laverie s'éclaire : l'enfant n°23 t'attend
    /// (la fin, une récompense). Le tableau de la Salle de jeux montre où on en est.
    /// Progression gardée sur l'appareil (PlayerPrefs), comme les papiers et les crayons.
    /// </summary>
    public class ExplorationGoals : MonoBehaviour
    {
        [System.Serializable]
        public class Place
        {
            public string id;          // clé de sauvegarde
            public string title;       // affiché en anglais (ex. THE LAUNDRY)
            public Bounds[] boxes;     // une ou plusieurs boîtes (monde)
        }

        public Place[] places = new Place[0];
        public TextMesh board;
        public int totalPapers = 17;
        [Tooltip("Ce qui apparaît à la fin (lumière, masque, message dans la machine 23). Inactif au départ.")]
        public GameObject ending;
        public AudioClip discoverSound;
        public int endingReward = 500;

        private const string PlaceKey = "horror.place.";
        private const string EndKey = "horror.fin23";
        private float _next;
        private TextMesh _popup;
        private float _popupUntil;

        private void Start() => Refresh();

        private void Update()
        {
            if (_popup != null)
            {
                bool show = Time.time < _popupUntil;
                _popup.gameObject.SetActive(show);
                if (show) _popup.color = new Color(0.95f, 0.92f, 0.86f, Mathf.Clamp01((_popupUntil - Time.time) / 0.6f));
            }
            if (Time.time < _next) return;
            _next = Time.time + 0.5f;
            foreach (ArmLocomotion p in ArmLocomotion.All)
            {
                if (p == null) continue;
                foreach (Place pl in places)
                {
                    if (PlayerPrefs.GetInt(PlaceKey + pl.id, 0) == 1) continue;
                    if (pl.boxes.Any(b => b.Contains(p.HeadPosition)))
                    {
                        PlayerPrefs.SetInt(PlaceKey + pl.id, 1);
                        PlayerPrefs.Save();
                        Announce(p, pl.title);
                        Refresh();
                    }
                }
            }
        }

        /// <summary>« NEW PLACE » qui flotte devant les yeux deux secondes (comme un titre de chapitre).</summary>
        private void Announce(ArmLocomotion p, string title)
        {
            if (discoverSound != null) AudioSource.PlayClipAtPoint(discoverSound, p.HeadPosition, 0.6f);
            if (_popup == null)
            {
                var go = new GameObject("Nouveau lieu");
                _popup = go.AddComponent<TextMesh>();
                _popup.anchor = TextAnchor.MiddleCenter;
                _popup.alignment = TextAlignment.Center;
                _popup.characterSize = 0.012f;
                _popup.fontSize = 96;
            }
            _popup.transform.SetParent(p.head, false);
            _popup.transform.localPosition = new Vector3(0f, -0.12f, 1.2f);
            _popup.transform.localRotation = Quaternion.identity;
            _popup.text = "- " + title + " -";
            _popupUntil = Time.time + 2.6f;
        }

        public int PlacesFound => places.Count(pl => PlayerPrefs.GetInt(PlaceKey + pl.id, 0) == 1);
        public bool Complete => PlacesFound >= places.Length && LoreJournal.Count >= totalPapers;

        public void Refresh()
        {
            bool done = Complete;
            if (board != null)
                board.text = "EXPLORATION\n\nPAPERS  " + Mathf.Min(LoreJournal.Count, totalPapers) + " / " + totalPapers +
                             "\nPLACES  " + PlacesFound + " / " + places.Length + "\n\n" +
                             (done ? "THE 23rd IS WAITING\nBEHIND DOOR 23" : "FIND EVERYTHING.\nSTAY QUIET.");
            if (ending != null) ending.SetActive(done);
            if (done && PlayerPrefs.GetInt(EndKey, 0) == 0)
            {
                PlayerPrefs.SetInt(EndKey, 1);
                PlayerWallet.AddCoins(endingReward);   // une seule fois
            }
        }

        private void OnEnable() => LoreJournal.Discovered += OnPaper;
        private void OnDisable() => LoreJournal.Discovered -= OnPaper;
        private void OnPaper(LoreDocument d) => Refresh();
    }
}
