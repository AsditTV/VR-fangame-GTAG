using UnityEngine;

namespace Horror.Cosmetics
{
    /// <summary>Affiche la monnaie du joueur sur un panneau (texte en anglais) : "CRAYONS: 120".</summary>
    [RequireComponent(typeof(TextMesh))]
    public class CoinDisplay : MonoBehaviour
    {
        private TextMesh _text;

        private void Awake() => _text = GetComponent<TextMesh>();
        private void OnEnable() { PlayerWallet.Changed += Refresh; Refresh(); }
        private void OnDisable() => PlayerWallet.Changed -= Refresh;
        private void Refresh() => _text.text = GameNames.Currency + ": " + PlayerWallet.Coins;
    }
}
