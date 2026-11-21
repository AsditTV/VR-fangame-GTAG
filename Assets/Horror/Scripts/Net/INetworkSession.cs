using System;
using System.Collections.Generic;

namespace Horror.Net
{
    /// <summary>
    /// Ce dont le jeu a besoin du réseau, sans dépendre de Photon. Aujourd'hui : OfflineSession (solo).
    /// Demain : un adaptateur Photon Fusion (PhotonFusionSession, voir Docs/MULTIJOUEUR.md) qui implémente
    /// cette interface. Le reste du jeu ne parle qu'à cette interface.
    ///
    /// Règle d'or : l'IA des monstres ne tourne QUE chez l'hôte (IsHost). Les autres reçoivent leur état.
    /// </summary>
    public interface INetworkSession
    {
        bool IsOnline { get; }
        bool IsHost { get; }
        int LocalPlayerId { get; }
        int PlayerCount { get; }

        /// <summary>Les AUTRES joueurs de la salle (identifiants réseau de la session).</summary>
        IReadOnlyList<int> RemotePlayers { get; }
        /// <summary>Pseudo affiché d'un joueur.</summary>
        string PlayerName(int playerId);
        /// <summary>Identifiant STABLE du compte (le même d'une partie à l'autre) : sert à retenir les joueurs bloqués.</summary>
        string PlayerUserId(int playerId);

        /// <summary>Rejoint (ou crée) une salle pour ce mode (ModeSelector.Current : « exploration », « inktag »...).</summary>
        void Join(string mode, Action<bool> done);
        void Leave();

        void SendPose(PlayerPose pose);
        void SendNoise(NoiseMessage noise);           // joueur -> hôte
        void SendMonster(MonsterState state);         // hôte -> tous
        void SendEvent(GameEventMessage e);

        event Action<PlayerPose> PoseReceived;
        event Action<NoiseMessage> NoiseReceived;
        event Action<MonsterState> MonsterReceived;
        event Action<GameEventMessage> EventReceived;
        event Action<int> PlayerJoined, PlayerLeft;
    }
}
