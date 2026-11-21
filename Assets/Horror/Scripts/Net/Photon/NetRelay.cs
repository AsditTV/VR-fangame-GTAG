#if FUSION2   // compilé seulement quand le SDK Photon Fusion 2 est installé
using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace Horror.Net
{
    /// <summary>
    /// L'objet réseau d'UN joueur (un par joueur, créé par lui-même en arrivant : il en a l'autorité).
    /// Sa pose, son pseudo, sa couleur et son identifiant stable sont des propriétés SYNCHRONISÉES : Fusion les envoie
    /// compressées, seulement quand elles changent, sans file d'attente qui déborde. Les messages ponctuels (bruit,
    /// événement de partie) passent par des RPC.
    /// Pourquoi pas de simples messages : l'envoi « fiable » de Fusion (SendReliableDataToPlayer) est fait pour de gros
    /// fichiers ; mesuré ici, plus de 90 % de nos petits messages fréquents n'arrivaient pas.
    /// </summary>
    public class NetRelay : NetworkBehaviour
    {
        public static readonly List<NetRelay> All = new List<NetRelay>();

        [Networked] public Vector3 Head { get; set; }
        [Networked] public Quaternion HeadRot { get; set; }
        [Networked] public Vector3 LeftHand { get; set; }
        [Networked] public Quaternion LeftRot { get; set; }
        [Networked] public Vector3 RightHand { get; set; }
        [Networked] public Quaternion RightRot { get; set; }
        [Networked] public byte Mouth { get; set; }
        [Networked] public NetworkBool Dead { get; set; }
        [Networked] public NetworkString<_16> Nick { get; set; }
        [Networked] public NetworkString<_32> UserId { get; set; }
        [Networked] public int ColorIndex { get; set; }

        /// <summary>Le joueur à qui appartient cet objet.</summary>
        public int OwnerId => Object != null ? Object.StateAuthority.PlayerId : -1;

        public override void Spawned() { if (!All.Contains(this)) All.Add(this); }
        public override void Despawned(NetworkRunner runner, bool hasState) { All.Remove(this); }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_Noise(Vector3 position, float radius, byte type, RpcInfo info = default)
        {
            if (info.Source == Runner.LocalPlayer) return;          // l'expéditeur l'a déjà chez lui
            PhotonFusionSession.Current?.OnRpcNoise(position, radius, type);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_Event(byte kind, int playerId, int value, Vector3 position, RpcInfo info = default)
        {
            if (info.Source == Runner.LocalPlayer) return;
            PhotonFusionSession.Current?.OnRpcEvent(kind, playerId, value, position);
        }
    }
}
#endif
