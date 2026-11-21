#if FUSION2   // compilé seulement quand le SDK Photon Fusion 2 est installé (Assets/Photon, hors du dépôt)
using System;
using System.Collections.Generic;
using System.IO;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using Horror.Noise;
using Horror.Player;

namespace Horror.Net
{
    /// <summary>
    /// Le jeu en ligne avec Photon Fusion 2, en mode PARTAGÉ (Shared) : chaque joueur se connecte au cloud Photon,
    /// le premier arrivé de la salle (« master client ») fait tourner l'IA des monstres (IsHost). S'il part, Photon
    /// désigne un autre master client : les monstres continuent chez lui.
    /// Les messages du jeu (poses, bruits, monstres, événements) passent en données fiables Fusion
    /// (SendReliableDataToPlayer), sans objet réseau : rien à poser dans la scène.
    /// À l'arrivée, chacun envoie un « bonjour » : pseudo + identifiant STABLE (gardé sur l'appareil) : c'est lui
    /// qui sert à retenir les blocages d'une partie à l'autre (VoiceModeration).
    /// L'App ID est lu par Photon dans SA configuration (PhotonAppSettings), jamais dans ce code.
    /// </summary>
    public class PhotonFusionSession : INetworkSession
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register() => NetworkBootstrap.OnlineFactory = () => new PhotonFusionSession();

        private enum Msg : byte { Hello = 1, Pose, Noise, Monster, Event }

        private const string KeyUserId = "horror.net.userid";
        /// <summary>Identifiant stable de cet appareil (aléatoire, créé une fois). Pas de donnée personnelle.</summary>
        public static string LocalUserId
        {
            get
            {
                string id = PlayerPrefs.GetString(KeyUserId, "");
                if (string.IsNullOrEmpty(id)) { id = Guid.NewGuid().ToString("N"); PlayerPrefs.SetString(KeyUserId, id); PlayerPrefs.Save(); }
                return id;
            }
        }

        private NetworkRunner _runner;
        private Callbacks _callbacks;
        private readonly Dictionary<int, PlayerRef> _players = new Dictionary<int, PlayerRef>();
        private readonly Dictionary<int, string> _names = new Dictionary<int, string>(), _userIds = new Dictionary<int, string>();
        private readonly List<int> _remote = new List<int>();

        public bool IsOnline => _runner != null && _runner.IsRunning;
        public bool IsHost => !IsOnline || _runner.IsSharedModeMasterClient;
        public int LocalPlayerId => IsOnline ? _runner.LocalPlayer.PlayerId : 0;
        public int PlayerCount => IsOnline ? _remote.Count + 1 : 1;
        public IReadOnlyList<int> RemotePlayers => _remote;
        public string PlayerName(int playerId) => _names.TryGetValue(playerId, out string n) ? n : "PLAYER " + playerId;
        public string PlayerUserId(int playerId) => _userIds.TryGetValue(playerId, out string u) ? u : "";

        public event Action<PlayerPose> PoseReceived;
        public event Action<NoiseMessage> NoiseReceived;
        public event Action<MonsterState> MonsterReceived;
        public event Action<GameEventMessage> EventReceived;
        public event Action<int> PlayerJoined, PlayerLeft;

        public async void Join(string mode, Action<bool> done)
        {
            var go = new GameObject("Photon Fusion (réseau)");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _runner = go.AddComponent<NetworkRunner>();
            _runner.ProvideInput = false;
            _callbacks = go.AddComponent<Callbacks>();
            _callbacks.session = this;
            _runner.AddCallbacks(_callbacks);
            string code = GameSettings.RoomCode;
            var args = new StartGameArgs
            {
                GameMode = GameMode.Shared,
                SessionName = "calmfaces-" + mode + (string.IsNullOrEmpty(code) ? "" : "-" + code),
                PlayerCount = 6,
            };
            StartGameResult result;
            try { result = await _runner.StartGame(args); }
            catch (Exception e) { Debug.LogWarning("[Horror] Photon : connexion impossible : " + e.Message); done?.Invoke(false); return; }
            if (!result.Ok)
            {
                Debug.LogWarning("[Horror] Photon : salle refusée (" + result.ShutdownReason + "). App ID collé dans PhotonAppSettings ?");
                done?.Invoke(false);
                return;
            }
            Debug.Log($"[Horror] Photon : salle « {args.SessionName} » rejointe ({(IsHost ? "je fais tourner les monstres" : "invité")}).");
            BroadcastHello(null);
            done?.Invoke(true);
        }

        public void Leave()
        {
            if (_runner != null)
            {
                if (_runner.IsRunning) _runner.Shutdown();
                UnityEngine.Object.Destroy(_runner.gameObject);
            }
            _runner = null;
            _players.Clear(); _names.Clear(); _userIds.Clear(); _remote.Clear();
        }

        // ---------------------------------------------------------------- envoi

        public void SendPose(PlayerPose p) => Broadcast(Msg.Pose, w =>
        {
            Write(w, p.head); Write(w, p.headRotation); Write(w, p.leftHand); Write(w, p.rightHand);
            Write(w, p.leftRotation); Write(w, p.rightRotation); w.Write(p.dead);
        });

        // envoyé à tous : seul celui qui fait tourner les monstres l'utilise (NetworkBootstrap.OnRemoteNoise vérifie IsHost)
        public void SendNoise(NoiseMessage n) => Broadcast(Msg.Noise, w => { Write(w, n.position); w.Write(n.radius); w.Write((byte)n.type); });

        public void SendMonster(MonsterState m) => Broadcast(Msg.Monster, w =>
        {
            w.Write(m.monsterId); Write(w, m.position); w.Write(m.yaw); w.Write(m.state); w.Write(m.targetPlayerId);
        });

        public void SendEvent(GameEventMessage e) => Broadcast(Msg.Event, w => { w.Write((byte)e.kind); w.Write(e.playerId); w.Write(e.value); Write(w, e.position); });

        private void BroadcastHello(PlayerRef? only)
        {
            void Body(BinaryWriter w) { w.Write(GameSettings.Name ?? ""); w.Write(LocalUserId); }
            if (only.HasValue) Send(only.Value, Msg.Hello, Body); else Broadcast(Msg.Hello, Body);
        }

        private void Broadcast(Msg type, Action<BinaryWriter> body)
        {
            if (!IsOnline) return;
            foreach (PlayerRef p in _runner.ActivePlayers)
                if (p != _runner.LocalPlayer) Send(p, type, body);
        }

        private void Send(PlayerRef to, Msg type, Action<BinaryWriter> body)
        {
            using var ms = new MemoryStream(64);
            using var w = new BinaryWriter(ms);
            w.Write((byte)type);
            body(w);
            w.Flush();
            _runner.SendReliableDataToPlayer(to, ReliableKey.FromInts((int)type, 0, 0, 0), new ReadOnlySpan<byte>(ms.GetBuffer(), 0, (int)ms.Length));
        }

        // ---------------------------------------------------------------- réception

        internal void OnData(PlayerRef from, ReadOnlySpan<byte> data)
        {
            if (data.Length < 1) return;
            using var r = new BinaryReader(new MemoryStream(data.ToArray()));
            var type = (Msg)r.ReadByte();
            int id = from.PlayerId;
            switch (type)
            {
                case Msg.Hello:
                    _names[id] = GameSettings.Sanitize(r.ReadString());
                    _userIds[id] = r.ReadString();
                    break;
                case Msg.Pose:
                    if (VoiceModeration.IsBlocked(PlayerUserId(id))) return;   // joueur bloqué : on ne l'affiche pas
                    PoseReceived?.Invoke(new PlayerPose
                    {
                        playerId = id, head = ReadV(r), headRotation = ReadQ(r), leftHand = ReadV(r), rightHand = ReadV(r),
                        leftRotation = ReadQ(r), rightRotation = ReadQ(r), dead = r.ReadBoolean(),
                    });
                    break;
                case Msg.Noise:
                    NoiseReceived?.Invoke(new NoiseMessage { position = ReadV(r), radius = r.ReadSingle(), type = (NoiseType)r.ReadByte() });
                    break;
                case Msg.Monster:
                    MonsterReceived?.Invoke(new MonsterState { monsterId = r.ReadInt32(), position = ReadV(r), yaw = r.ReadSingle(), state = r.ReadByte(), targetPlayerId = r.ReadInt32() });
                    break;
                case Msg.Event:
                    EventReceived?.Invoke(new GameEventMessage { kind = (GameEventMessage.Kind)r.ReadByte(), playerId = r.ReadInt32(), value = r.ReadInt32(), position = ReadV(r) });
                    break;
            }
        }

        internal void OnJoined(PlayerRef p)
        {
            _players[p.PlayerId] = p;
            if (p == _runner.LocalPlayer) return;
            if (!_remote.Contains(p.PlayerId)) _remote.Add(p.PlayerId);
            BroadcastHello(p);                   // le nouveau venu apprend notre pseudo
            PlayerJoined?.Invoke(p.PlayerId);
        }

        internal void OnLeft(PlayerRef p)
        {
            _players.Remove(p.PlayerId); _names.Remove(p.PlayerId); _userIds.Remove(p.PlayerId);
            _remote.Remove(p.PlayerId);
            PlayerLeft?.Invoke(p.PlayerId);
        }

        private static void Write(BinaryWriter w, Vector3 v) { w.Write(v.x); w.Write(v.y); w.Write(v.z); }
        private static void Write(BinaryWriter w, Quaternion q) { w.Write(q.x); w.Write(q.y); w.Write(q.z); w.Write(q.w); }
        private static Vector3 ReadV(BinaryReader r) => new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        private static Quaternion ReadQ(BinaryReader r) => new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());

        /// <summary>Reçoit les rappels de Fusion et les transmet à la session (les autres ne servent pas ici).</summary>
        private class Callbacks : MonoBehaviour, INetworkRunnerCallbacks
        {
            public PhotonFusionSession session;
            public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) => session?.OnJoined(player);
            public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) => session?.OnLeft(player);
            public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) => session?.OnData(player, data);
            public void OnShutdown(NetworkRunner runner, ShutdownReason reason) => Debug.Log("[Horror] Photon : session fermée (" + reason + ").");
            public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) => Debug.LogWarning("[Horror] Photon : déconnecté (" + reason + ").");
            public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) => Debug.LogWarning("[Horror] Photon : échec de connexion (" + reason + ").");
            public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
            public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
            public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
#pragma warning disable CS0618   // obsolète chez Fusion 2.1, mais toujours exigé par l'interface
            public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
#pragma warning restore CS0618
            public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
            public void OnInput(NetworkRunner runner, NetworkInput input) { }
            public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
            public void OnConnectedToServer(NetworkRunner runner) { }
            public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
            public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
            public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
            public void OnSceneLoadDone(NetworkRunner runner) { }
            public void OnSceneLoadStart(NetworkRunner runner) { }
        }
    }
}
#endif
