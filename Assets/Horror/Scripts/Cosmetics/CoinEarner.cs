using Horror.Locomotion;
using Horror.Player;
using Horror.World;
using UnityEngine;

namespace Horror.Cosmetics
{
    /// <summary>
    /// Gagner des pièces : en survivant HORS de la zone sûre (rester caché dans la Salle de jeux
    /// ne rapporte rien). Plus tard : bonus pour chaque papier de lore trouvé, et classement.
    /// À mettre sur l'objet "Joueur".
    /// </summary>
    [RequireComponent(typeof(ArmLocomotion))]
    public class CoinEarner : MonoBehaviour
    {
        [Tooltip("Pièces gagnées à chaque palier de survie.")]
        public int coinsPerTick = 2;
        [Tooltip("Secondes de survie hors zone sûre pour un palier.")]
        public float secondsPerTick = 30f;
        public AudioClip coinSound;

        private ArmLocomotion _locomotion;
        private Jumpscare _jumpscare;
        private float _timer;

        private void Awake()
        {
            _locomotion = GetComponent<ArmLocomotion>();
            _jumpscare = GetComponent<Jumpscare>();
        }

        private void Update()
        {
            bool inDanger = !SafeZone.Contains(_locomotion.HeadPosition) && (_jumpscare == null || !_jumpscare.IsPlaying);
            if (!inDanger) return;

            _timer += Time.deltaTime;
            if (_timer < secondsPerTick) return;
            _timer = 0f;
            PlayerWallet.AddCoins(coinsPerTick);
            if (coinSound != null) AudioSource.PlayClipAtPoint(coinSound, _locomotion.HeadPosition, 0.35f);
        }

        /// <summary>Bonus ponctuel (papier de lore trouvé, secret...).</summary>
        public void Bonus(int coins) => PlayerWallet.AddCoins(coins);
    }
}
