using System;

namespace Horror.Net
{
    /// <summary>Le « réseau » du solo : on est seul, donc hôte. Rien n'est envoyé, rien n'est reçu.</summary>
    public class OfflineSession : INetworkSession
    {
        public bool IsOnline => false;
        public bool IsHost => true;
        public int LocalPlayerId => 0;
        public int PlayerCount => 1;

        public void Join(string mode, Action<bool> done) => done?.Invoke(true);
        public void Leave() { }

        public void SendPose(PlayerPose pose) { }
        public void SendNoise(NoiseMessage noise) { }
        public void SendMonster(MonsterState state) { }
        public void SendEvent(GameEventMessage e) { }

        // jamais déclenchés en solo (« add/remove » vides : pas d'avertissement « jamais utilisé »)
        public event Action<PlayerPose> PoseReceived { add { } remove { } }
        public event Action<NoiseMessage> NoiseReceived { add { } remove { } }
        public event Action<MonsterState> MonsterReceived { add { } remove { } }
        public event Action<GameEventMessage> EventReceived { add { } remove { } }
        public event Action<int> PlayerJoined { add { } remove { } }
        public event Action<int> PlayerLeft { add { } remove { } }
    }
}
