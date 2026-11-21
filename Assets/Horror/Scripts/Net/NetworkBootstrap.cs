using System;
using UnityEngine;
using Horror.Noise;
using Horror.World;

namespace Horror.Net
{
    /// <summary>
    /// Point d'entrée du réseau, posé dans la scène. Choisit la session (solo tant qu'aucun adaptateur en ligne
    /// n'est enregistré), rejoint la salle du mode choisi au lobby, et branche le système de bruit :
    ///  - mes bruits partent vers l'hôte (NoiseSystem.NetworkForwarder) ;
    ///  - les bruits des autres arrivent chez l'hôte et sont ré-émis localement (NoiseSystem.ReceiveRemote).
    /// Aucune clé ni App ID ici : Photon les lit dans SA configuration (PhotonAppSettings), jamais dans le code.
    /// </summary>
    public class NetworkBootstrap : MonoBehaviour
    {
        /// <summary>
        /// Fabrique de la session en ligne. L'adaptateur Photon (à écrire après installation du SDK) se déclare ici
        /// au démarrage, par exemple : NetworkBootstrap.OnlineFactory = () => new PhotonFusionSession();
        /// </summary>
        public static Func<INetworkSession> OnlineFactory;

        public static INetworkSession Session { get; private set; } = new OfflineSession();

        [Tooltip("Rejoindre une salle en ligne au démarrage si le mode choisi au lobby est un mode en ligne.")]
        public bool autoJoin = true;
        [Tooltip("Modes joués en ligne (les autres restent en solo).")]
        public string[] onlineModes = { "inktag", "hideseek" };

        private void Awake()
        {
            string mode = ModeSelector.Current;
            bool wantsOnline = autoJoin && OnlineFactory != null && Array.IndexOf(onlineModes, mode) >= 0;
            Session = wantsOnline ? OnlineFactory() : new OfflineSession();
            Session.NoiseReceived += OnRemoteNoise;
            NoiseSystem.NetworkForwarder = Session.IsOnline ? ForwardNoise : null;
            if (wantsOnline)
                Session.Join(mode, ok =>
                {
                    if (ok) return;
                    Debug.LogWarning("[Horror] Salle en ligne impossible à rejoindre : on reste en solo.");
                    UseOffline();
                });
        }

        private void OnDestroy()
        {
            if (Session == null) return;
            Session.NoiseReceived -= OnRemoteNoise;
            NoiseSystem.NetworkForwarder = null;
            Session.Leave();
            Session = new OfflineSession();
        }

        private void UseOffline()
        {
            Session.NoiseReceived -= OnRemoteNoise;
            Session.Leave();
            Session = new OfflineSession();
            NoiseSystem.NetworkForwarder = null;
        }

        private static void ForwardNoise(NoiseEvent e)
        {
            if (!Session.IsHost) Session.SendNoise(NoiseMessage.From(e));   // l'hôte entend déjà ses propres bruits
        }

        private static void OnRemoteNoise(NoiseMessage m)
        {
            if (Session.IsHost) NoiseSystem.ReceiveRemote(m.ToEvent());
        }

        /// <summary>Les monstres demandent ça avant de penser : faux chez un invité (ils suivent l'hôte).</summary>
        public static bool RunsAI => Session == null || Session.IsHost;
    }
}
