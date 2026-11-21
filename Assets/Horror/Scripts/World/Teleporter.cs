using UnityEngine;
using Horror.Interaction;
using Horror.Locomotion;

namespace Horror.World
{
    /// <summary>
    /// Un passage : appuyer sur le bouton emmène le joueur le plus proche ailleurs (ex. entrer dans la machine 23
    /// pour descendre dans l'Envers, puis en revenir). Fondu au noir court pour ne pas brusquer en VR.
    /// </summary>
    public class Teleporter : MonoBehaviour
    {
        public PressButton button;
        public Transform destination;
        public AudioClip sound;

        private void OnEnable() { if (button != null) button.onPressed.AddListener(Go); }
        private void OnDisable() { if (button != null) button.onPressed.RemoveListener(Go); }

        public void Go()
        {
            if (destination == null) return;
            ArmLocomotion best = null; float bd = float.MaxValue;
            foreach (ArmLocomotion p in ArmLocomotion.All)
            {
                if (p == null) continue;
                float d = (p.HeadPosition - transform.position).sqrMagnitude;
                if (d < bd) { bd = d; best = p; }
            }
            if (best == null) return;
            if (sound != null) AudioSource.PlayClipAtPoint(sound, best.HeadPosition, 0.8f);
            best.TeleportTo(destination.position);
        }
    }
}
