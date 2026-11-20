using Horror.Noise;
using UnityEngine;
using UnityEngine.XR;

namespace Horror.Player
{
    /// <summary>
    /// Applique le mode micro choisi sur le Minitel :
    ///  - Open : micro toujours ouvert ;
    ///  - PushToTalk : micro ouvert seulement en maintenant A (manette droite) ou X (manette gauche) ;
    ///  - Muted : micro coupé.
    /// Coupe à la fois la bouche (MicMouth) et le bruit de la voix (VoiceNoise) : en muet, L'Écoute
    /// ne t'entend plus parler. À mettre sur le joueur.
    /// </summary>
    public class MicModeApplier : MonoBehaviour
    {
        private MicMouth[] _mics;
        private VoiceNoise[] _voices;
        private float _nextScan;

        private void Update()
        {
            if (Time.time >= _nextScan)   // la poupée peut être ajoutée après coup : on recherche de temps en temps
            {
                _mics = GetComponentsInChildren<MicMouth>(true);
                _voices = GetComponentsInChildren<VoiceNoise>(true);
                _nextScan = Time.time + 2f;
            }

            bool open;
            switch (GameSettings.Mic)
            {
                case MicMode.Muted: open = false; break;
                case MicMode.PushToTalk: open = Pressed(XRNode.RightHand) || Pressed(XRNode.LeftHand); break;
                default: open = true; break;
            }
            if (_mics != null) foreach (MicMouth m in _mics) if (m != null && m.isLocal) m.micEnabled = open;
            if (_voices != null) foreach (VoiceNoise v in _voices) if (v != null) v.voiceEnabled = open;
        }

        private static bool Pressed(XRNode node) =>
            InputDevices.GetDeviceAtXRNode(node).TryGetFeatureValue(CommonUsages.primaryButton, out bool down) && down;
    }
}
