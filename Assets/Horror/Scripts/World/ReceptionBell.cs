using UnityEngine;
using Horror.Interaction;
using Horror.Noise;

namespace Horror.World
{
    /// <summary>
    /// La sonnette du comptoir de l'infirmerie : « ding ! ». Un bruit volontaire assez fort pour que le Docteur
    /// vienne voir (il n'entend que les gros bruits) : on l'attire à l'accueil pendant qu'on fouille ailleurs...
    /// mais il faudra repasser par là pour reprendre le monte-charge. Petite pause entre deux sonneries.
    /// </summary>
    public class ReceptionBell : MonoBehaviour
    {
        public PressButton button;
        public AudioClip ding;
        public float noiseRadius = 50f;   // un appel volontaire : il porte loin dans les couloirs
        public float cooldown = 6f;
        private float _next;

        private void OnEnable() { if (button != null) button.onPressed.AddListener(Ring); }
        private void OnDisable() { if (button != null) button.onPressed.RemoveListener(Ring); }

        [ContextMenu("Sonner")]
        public void Ring()
        {
            if (Time.time < _next) return;
            _next = Time.time + cooldown;
            if (ding != null) AudioSource.PlayClipAtPoint(ding, transform.position, 1f);
            NoiseSystem.Emit(transform.position, noiseRadius, NoiseType.Leurre, gameObject);
        }
    }
}
