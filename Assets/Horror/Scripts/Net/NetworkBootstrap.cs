using System;
using UnityEngine;
using Horror.Noise;
using Horror.World;
using Horror.Player;

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
            if (OnlineFactory != null && Array.IndexOf(Environment.GetCommandLineArgs(), "-online") >= 0)
            { wantsOnline = true; mode = "test"; }                       // tests : CalmFaces.exe -online rejoint la salle « test »
            Session = wantsOnline ? OnlineFactory() : new OfflineSession();
            Session.NoiseReceived += OnRemoteNoise;
            Session.EventReceived += OnRemoteEvent;
            NoiseSystem.NetworkForwarder = Session.IsOnline ? ForwardNoise : null;
            if (wantsOnline)
                Session.Join(mode, ok =>
                {
                    if (ok) return;
                    Debug.LogWarning("[Horror] Salle en ligne impossible à rejoindre : on reste en solo.");
                    UseOffline();
                });
        }

        // ---------------------------------------------------------------- code de salle (Minitel)
        // Jouer à plusieurs : taper le même code de salle (4 à 6 chiffres) sur le Minitel. Le code stable 2,5 s, on rejoint
        // la salle « coop-CODE » ; code changé, on change de salle ; code effacé, retour en solo. Le code est gardé :
        // au lancement suivant, on retourne dans la même salle (l'effacer pour jouer seul).
        private string _seenCode, _joinedCode;
        private float _codeStableAt;
        private bool _joining;

        private void Update()
        {
            if (OnlineFactory == null || _joining) return;
            string code = GameSettings.RoomCode ?? "";
            if (code != _seenCode) { _seenCode = code; _codeStableAt = Time.unscaledTime + 2.5f; return; }
            if (Time.unscaledTime < _codeStableAt) return;
            bool want = code.Length >= 4;
            bool online = Session != null && Session.IsOnline;
            if (want && !online)
            {
                _joining = true;
                GoOnline("coop", ok =>
                {
                    _joining = false;
                    _joinedCode = ok ? code : null;
                    if (!ok) _codeStableAt = Time.unscaledTime + 15f;    // pas de réseau : on réessaie dans 15 s
                });
            }
            else if (online && _joinedCode != null && _joinedCode != code)
            {
                UseOffline();                                            // code changé ou effacé : on quitte la salle
                _joinedCode = null;
            }
        }

        private void OnDestroy()
        {
            if (Session == null) return;
            Session.NoiseReceived -= OnRemoteNoise;
            Session.EventReceived -= OnRemoteEvent;
            NoiseSystem.NetworkForwarder = null;
            Session.Leave();
            Session = new OfflineSession();
        }

        /// <summary>Passe en ligne maintenant (tests, ou futur bouton du lobby) : rejoint la salle du mode donné.</summary>
        public static void GoOnline(string mode, Action<bool> done = null)
        {
            if (OnlineFactory == null || (Session != null && Session.IsOnline)) { done?.Invoke(Session != null && Session.IsOnline); return; }
            if (Session != null) { Session.NoiseReceived -= OnRemoteNoise; Session.EventReceived -= OnRemoteEvent; }
            Session = OnlineFactory();
            Session.NoiseReceived += OnRemoteNoise;
            Session.EventReceived += OnRemoteEvent;
            NoiseSystem.NetworkForwarder = ForwardNoise;
            Session.Join(mode, ok =>
            {
                if (!ok)
                {
                    Session.NoiseReceived -= OnRemoteNoise; Session.EventReceived -= OnRemoteEvent;
                    Session.Leave(); Session = new OfflineSession(); NoiseSystem.NetworkForwarder = null;
                }
                done?.Invoke(ok);
            });
        }

        private void UseOffline()
        {
            Session.NoiseReceived -= OnRemoteNoise;
            Session.EventReceived -= OnRemoteEvent;
            Session.Leave();
            Session = new OfflineSession();
            NoiseSystem.NetworkForwarder = null;
        }

        private static void ForwardNoise(NoiseEvent e)
        {
            if (!Session.IsHost) Session.SendNoise(NoiseMessage.From(e));   // l'hôte entend déjà ses propres bruits
        }

        private static void OnRemoteEvent(GameEventMessage e)
        {
            // un autre joueur vient de mourir : on voit l'encre gicler là où il était
            if (e.kind == GameEventMessage.Kind.Mort) Horror.Player.DeathMarks.Spawn(e.position);
        }

        private static void OnRemoteNoise(NoiseMessage m)
        {
            if (Session.IsHost) NoiseSystem.ReceiveRemote(m.ToEvent());
        }

        /// <summary>Les monstres demandent ça avant de penser : faux chez un invité (ils suivent l'hôte).</summary>
        public static bool RunsAI => Session == null || Session.IsHost;
    }
}
