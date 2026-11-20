using UnityEngine;

namespace Horror.Cosmetics
{
    /// <summary>Affiche les pièces du joueur sur un panneau (texte en anglais) : "COINS: 120".</summary>
    [RequireComponent(typeof(TextMesh))]
    public class CoinDisplay : MonoBehaviour
    {
        private TextMesh _text;

        private void Awake() => _text = GetComponent<TextMesh>();
        private void OnEnable() { PlayerWallet.Changed += Refresh; Refresh(); }
        private void OnDisable() => PlayerWallet.Changed -= Refresh;
        private void Refresh() => _text.text = "COINS: " + PlayerWallet.Coins;
    }
}
