using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace Horror.XR
{
    /// <summary>
    /// Fait suivre à cet objet la position/rotation réelle d'un élément suivi par le casque
    /// (la tête, la manette gauche ou la manette droite).
    ///
    /// Pourquoi ce script plutôt qu'un package ? Il n'utilise que le module XR intégré à Unity :
    /// il marche avec OpenXR (Quest via Link) sans dépendre d'un package en particulier.
    /// Si tu utilises déjà un "XR Origin" (XR Interaction Toolkit) ou un "OVRCameraRig" (Meta XR),
    /// tu n'as PAS besoin de ce script : ces rigs suivent déjà la tête et les mains.
    ///
    /// À placer sur un enfant direct de l'objet "Joueur" (la position est appliquée en local).
    /// </summary>
    [DefaultExecutionOrder(-100)] // avant les autres scripts, pour qu'ils lisent une pose fraîche
    public class XRNodePoseDriver : MonoBehaviour
    {
        [Tooltip("Quel élément suivre : Head = casque, LeftHand / RightHand = manettes.")]
        public XRNode node = XRNode.Head;

        [Tooltip("Appliquer la position suivie.")]
        public bool trackPosition = true;

        [Tooltip("Appliquer la rotation suivie.")]
        public bool trackRotation = true;

        // Partagé entre toutes les instances : on ne règle l'origine "sol" qu'une seule fois.
        private static bool s_floorOriginSet;
        private static readonly List<XRInputSubsystem> s_subsystems = new List<XRInputSubsystem>();

        private void OnEnable()
        {
            // onBeforeRender = juste avant l'affichage : réduit la latence perçue des mains et de la tête.
            Application.onBeforeRender += ApplyPose;
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= ApplyPose;
        }

        private void Update()
        {
            if (!s_floorOriginSet)
                TrySetFloorOrigin();

            ApplyPose();
        }

        [BeforeRenderOrder(-100)]
        private void ApplyPose()
        {
            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            if (!device.isValid)
                return; // casque/manette pas encore détecté (ou éteint) : on garde la dernière pose

            if (trackPosition && device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position))
                transform.localPosition = position;

            if (trackRotation && device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation))
                transform.localRotation = rotation;
        }

        /// <summary>
        /// Demande au casque de mesurer les hauteurs depuis le SOL réel (et pas depuis la position
        /// de la tête au démarrage). Sans ça, la hauteur des mains par rapport au sol virtuel varie.
        /// </summary>
        private static void TrySetFloorOrigin()
        {
            SubsystemManager.GetSubsystems(s_subsystems);
            foreach (XRInputSubsystem subsystem in s_subsystems)
            {
                if (!subsystem.running)
                    continue;

                if ((subsystem.GetSupportedTrackingOriginModes() & TrackingOriginModeFlags.Floor) != 0)
                    subsystem.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);

                s_floorOriginSet = true; // même si "Floor" n'est pas supporté, on n'insiste pas
            }
        }
    }
}
