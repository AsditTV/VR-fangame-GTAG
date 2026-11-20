using Horror.Interaction;
using UnityEngine;

namespace Horror.Cosmetics
{
    /// <summary>
    /// Un présentoir de la boutique (dans la Salle de jeux) : l'objet tourne sur un socle,
    /// une étiquette en anglais donne son nom et son prix, un bouton permet d'acheter puis de porter.
    ///  - pas acheté : "PAPER CROWN — 150 coins"  → appuyer = acheter (si assez de pièces)
    ///  - acheté     : "OWNED — press to wear"    → appuyer = porter
    ///  - porté      : "WEARING — press to remove"
    /// </summary>
    public class ShopStand : MonoBehaviour
    {
        public CosmeticItem item;
        [Tooltip("Où poser l'aperçu de l'objet (il tourne sur lui-même).")]
        public Transform previewAnchor;
        [Tooltip("Texte de l'étiquette (TextMesh).")]
        public TextMesh label;
        public PressButton button;

        [Header("Sons")]
        public AudioClip buySound;
        public AudioClip deniedSound;

        private GameObject _preview;
        private AudioSource _audio;

        private void Awake()
        {
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.spatialBlend = 1f;
            _audio.maxDistance = 10f;
            _audio.playOnAwake = false;
        }

        private void OnEnable()
        {
            PlayerWallet.Changed += Refresh;
            if (button != null) button.onPressed.AddListener(OnPressed);
            if (item != null && item.prefab != null && previewAnchor != null && _preview == null)
            {
                _preview = Instantiate(item.prefab, previewAnchor);
                _preview.transform.localScale = Vector3.one * item.scale;
                foreach (Collider c in _preview.GetComponentsInChildren<Collider>()) Destroy(c);
            }
            Refresh();
        }

        private void OnDisable()
        {
            PlayerWallet.Changed -= Refresh;
            if (button != null) button.onPressed.RemoveListener(OnPressed);
        }

        private void Update()
        {
            if (_preview != null) _preview.transform.Rotate(0f, 35f * Time.deltaTime, 0f, Space.World);
        }

        private void OnPressed()
        {
            if (item == null) return;
            if (!PlayerWallet.Owns(item))
            {
                bool ok = PlayerWallet.TryBuy(item);
                Play(ok ? buySound : deniedSound);
                if (!ok) Flash("NOT ENOUGH COINS");
                return;
            }
            PlayerWallet.ToggleEquip(item);
            Play(buySound);
        }

        private float _flashUntil;
        private string _flashText;

        private void Flash(string text)
        {
            _flashText = text;
            _flashUntil = Time.time + 1.5f;
            Refresh();
            Invoke(nameof(Refresh), 1.6f);
        }

        private void Refresh()
        {
            if (label == null || item == null) return;
            string name = item.displayName.ToUpperInvariant();
            if (Time.time < _flashUntil) label.text = name + "\n" + _flashText;
            else if (PlayerWallet.IsEquipped(item)) label.text = name + "\nWEARING - press to remove";
            else if (PlayerWallet.Owns(item)) label.text = name + "\nOWNED - press to wear";
            else label.text = name + "\n" + item.price + " coins";
        }

        private void Play(AudioClip clip)
        {
            if (clip != null) _audio.PlayOneShot(clip);
        }
    }
}
