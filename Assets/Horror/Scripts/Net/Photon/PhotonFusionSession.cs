#if FUSION2   // compilé seulement quand le SDK Photon Fusion 2 est installé (Assets/Photon, hors du dépôt)
using System;
using System.Collections.Generic;
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
    ///
    /// Transport (après mesure : l'envoi « fiable » brut de Fusion perdait plus de 90 % de nos petits messages) :
    ///  - chaque joueur crée son objet réseau <see cref="NetRelay"/> : pose, pseudo, couleur, identifiant stable y sont
    ///    des propriétés synchronisées (compressées, envoyées quand elles changent) ;
    ///  - les monstres sont dans <see cref="MonsterRelay"/>, tenu par le master client ;
    ///  - bruits et événements ponctuels (dont « attrapé ») passent en RPC.
    /// Le reste du jeu ne voit que INetworkSession, comme avant. L'App ID est lu par Photon dans SA configuration
    /// (PhotonAppSettings), jamais dans ce code.
    /// </summary>
    public class PhotonFusionSession : INetworkSession
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register() => NetworkBootstrap.OnlineFactory = () => new PhotonFusionSession();

        /// <summary>La session en cours (les RPC des objets réseau la retrouvent ainsi).</summary>
        public static PhotonFusionSession Current { get; private set; }

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

        /// <summary>Appelé une fois la salle rejointe, avec l'objet du NetworkRunner et mon identifiant : la voix s'y accroche.</summary>
        public static Action<GameObject, string> AfterJoin;
        /// <summary>Mon identifiant dans cette session (celui de l'appareil ; un test peut en donner un autre).</summary>
        public string MyUserId { get; set; } = LocalUserId;

        private NetworkRunner _runner;
        private Callbacks _callbacks;
        private NetRelay _mine;
        private bool _joined;                                          // vrai une fois la salle rejointe ET mon objet créé
        private readonly List<int> _remote = new List<int>();
        private readonly Dictionary<int, Vector3> _lastHead = new Dictionary<int, Vector3>();

        public bool IsOnline => _runner != null && _runner.IsRunning;
        public bool IsHost => !IsOnline || _runner.IsSharedModeMasterClient;
        public int LocalPlayerId => IsOnline ? _runner.LocalPlayer.PlayerId : 0;
        public int PlayerCount => IsOnline ? _remote.Count + 1 : 1;
        public IReadOnlyList<int> RemotePlayers => _remote;

        private NetRelay RelayOf(int playerId)
        {
            foreach (NetRelay r in NetRelay.All) if (r != null && r.Object != null && r.Runner == _runner && r.OwnerId == playerId) return r;
            return null;
        }

        public string PlayerName(int playerId) { NetRelay r = RelayOf(playerId); string n = r != null ? r.Nick.ToString() : ""; return string.IsNullOrEmpty(n) ? "PLAYER " + playerId : n; }
        public string PlayerUserId(int playerId) { NetRelay r = RelayOf(playerId); return r != null ? r.UserId.ToString() : ""; }
        public int PlayerColorIndex(int playerId) { NetRelay r = RelayOf(playerId); return r != null ? r.ColorIndex : 0; }

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
            // nos rappels sur un objet À PART : Fusion inscrit tout seul les composants posés sur l'objet du runner
            var cbGo = new GameObject("Photon Fusion (rappels)");
            UnityEngine.Object.DontDestroyOnLoad(cbGo);
            _callbacks = cbGo.AddComponent<Callbacks>();
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
            Current = this;
            if (!SpawnRelays()) { Leave(); done?.Invoke(false); return; }
            _joined = true;
            Debug.Log($"[Horror] Photon : salle « {args.SessionName} » rejointe ({(IsHost ? "je fais tourner les monstres" : "invité")}).");
            AfterJoin?.Invoke(go, MyUserId);
            done?.Invoke(true);
        }

        private bool SpawnRelays()
        {
            NetworkObject relayPrefab = Resources.Load<NetworkObject>("Net/NetRelay");
            if (relayPrefab == null) { Debug.LogError("[Horror] Photon : Resources/Net/NetRelay introuvable (menu Horror/11)."); return false; }
            NetworkObject mine = _runner.Spawn(relayPrefab, Vector3.zero, Quaternion.identity, _runner.LocalPlayer, (r, o) =>
            {
                NetRelay relay = o.GetComponent<NetRelay>();
                relay.Nick = GameSettings.Name ?? "";
                relay.UserId = MyUserId;
                relay.ColorIndex = GameSettings.ColorIndex;
            });
            _mine = mine != null ? mine.GetComponent<NetRelay>() : null;
            EnsureMonsterRelay();
            return _mine != null;
        }

        /// <summary>Le master client crée l'objet des monstres s'il n'existe pas encore (Fusion le transmet s'il part).</summary>
        private void EnsureMonsterRelay()
        {
            if (!IsOnline || !_runner.IsSharedModeMasterClient || MonsterRelay.Instance != null) return;
            NetworkObject prefab = Resources.Load<NetworkObject>("Net/MonsterRelay");
            if (prefab != null) _runner.Spawn(prefab, Vector3.zero, Quaternion.identity);
        }

        public void Leave()
        {
            if (Current == this) Current = null;
            if (_runner != null)
            {
                if (_runner.IsRunning) _runner.Shutdown();
                UnityEngine.Object.Destroy(_runner.gameObject);
            }
            if (_callbacks != null) UnityEngine.Object.Destroy(_callbacks.gameObject);
            _runner = null; _callbacks = null; _mine = null; _joined = false;
            _remote.Clear(); _lastHead.Clear();
        }

        // ---------------------------------------------------------------- envoi

        public void SendPose(PlayerPose p)
        {
            if (_mine == null || _mine.Object == null || !_mine.Object.HasStateAuthority) return;
            _mine.Head = p.head; _mine.HeadRot = p.headRotation;
            _mine.LeftHand = p.leftHand; _mine.LeftRot = p.leftRotation;
            _mine.RightHand = p.rightHand; _mine.RightRot = p.rightRotation;
            _mine.Mouth = p.mouth; _mine.Dead = p.dead;
            // pseudo / couleur changés sur le Minitel pendant la partie
            string nick = GameSettings.Name ?? "";
            if (_mine.Nick.ToString() != nick) _mine.Nick = nick;
            if (_mine.ColorIndex != GameSettings.ColorIndex) _mine.ColorIndex = GameSettings.ColorIndex;
        }

        public void SendNoise(NoiseMessage n)
        {
            if (_mine != null && _mine.Object != null) _mine.RPC_Noise(n.position, n.radius, (byte)n.type);
        }

        public void SendMonster(MonsterState m)
        {
            MonsterRelay mr = MonsterRelay.Instance;
            if (mr == null || mr.Object == null || !mr.Object.HasStateAuthority || m.monsterId < 0 || m.monsterId >= mr.Monsters.Length) return;
            mr.Monsters.Set(m.monsterId, new MonsterNet { Position = m.position, Yaw = m.yaw });
            if (mr.Count < m.monsterId + 1) mr.Count = m.monsterId + 1;
        }

        public void SendEvent(GameEventMessage e)
        {
            if (_mine != null && _mine.Object != null) _mine.RPC_Event((byte)e.kind, e.playerId, e.value, e.position);
        }

        internal void OnRpcNoise(Vector3 position, float radius, byte type) =>
            NoiseReceived?.Invoke(new NoiseMessage { position = position, radius = radius, type = (NoiseType)type });

        internal void OnRpcEvent(byte kind, int playerId, int value, Vector3 position) =>
            EventReceived?.Invoke(new GameEventMessage { kind = (GameEventMessage.Kind)kind, playerId = playerId, value = value, position = position });

        // ---------------------------------------------------------------- chaque image

        /// <summary>Lit les objets réseau des autres : joueurs présents, leurs poses, les monstres (chez les invités).</summary>
        internal void Tick()
        {
            if (!IsOnline || !_joined) return;                          // pas pendant la connexion : Fusion n'est pas prêt à créer d'objet
            EnsureMonsterRelay();                                       // devenu master client : on reprend les monstres
            foreach (PlayerRef p in _runner.ActivePlayers)
            {
                if (p == _runner.LocalPlayer || _remote.Contains(p.PlayerId)) continue;
                _remote.Add(p.PlayerId);
                PlayerJoined?.Invoke(p.PlayerId);
            }
            for (int i = _remote.Count - 1; i >= 0; i--)
            {
                bool still = false;
                foreach (PlayerRef p in _runner.ActivePlayers) if (p.PlayerId == _remote[i]) { still = true; break; }
                if (still) continue;
                int gone = _remote[i];
                _remote.RemoveAt(i); _lastHead.Remove(gone);
                PlayerLeft?.Invoke(gone);
            }
            // leurs poses : seulement quand elles changent (le Docteur estime ainsi la vitesse de chacun)
            foreach (NetRelay r in NetRelay.All)
            {
                if (r == null || r.Object == null || r.Runner != _runner || r == _mine) continue;
                int id = r.OwnerId;
                if (!_remote.Contains(id)) continue;
                if (VoiceModeration.IsBlocked(r.UserId.ToString())) continue;   // joueur bloqué : on ne l'affiche pas
                Vector3 head = r.Head;
                if (_lastHead.TryGetValue(id, out Vector3 last) && last == head) continue;
                _lastHead[id] = head;
                PoseReceived?.Invoke(new PlayerPose
                {
                    playerId = id, head = head, headRotation = r.HeadRot, leftHand = r.LeftHand, rightHand = r.RightHand,
                    leftRotation = r.LeftRot, rightRotation = r.RightRot, dead = r.Dead, mouth = r.Mouth,
                });
            }
            // les monstres, chez les invités
            MonsterRelay mr = MonsterRelay.Instance;
            if (!IsHost && mr != null && mr.Object != null)
            {
                int n = Mathf.Min(mr.Count, mr.Monsters.Length);
                for (int i = 0; i < n; i++)
                {
                    MonsterNet m = mr.Monsters[i];
                    MonsterReceived?.Invoke(new MonsterState { monsterId = i, position = m.Position, yaw = m.Yaw, targetPlayerId = -1 });
                }
            }
        }

        /// <summary>Reçoit les rappels de Fusion (presque tous inutiles ici : tout passe par les objets réseau).</summary>
        private class Callbacks : MonoBehaviour, INetworkRunnerCallbacks
        {
            public PhotonFusionSession session;
            private void Update() => session?.Tick();
            public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
            public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
            public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }
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
