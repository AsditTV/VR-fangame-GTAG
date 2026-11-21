#if FUSION2   // compilé seulement quand le SDK Photon Fusion 2 est installé
using Fusion;
using UnityEngine;

namespace Horror.Net
{
    /// <summary>L'état d'un monstre tel qu'il passe sur le réseau.</summary>
    public struct MonsterNet : INetworkStruct
    {
        public Vector3 Position;
        public float Yaw;
    }

    /// <summary>
    /// Les monstres de la salle : un seul objet, tenu par le « master client » (celui qui fait tourner l'IA). Marqué
    /// « objet du master client » : s'il part, Fusion le confie au nouveau master, et les monstres continuent.
    /// </summary>
    public class MonsterRelay : NetworkBehaviour
    {
        public static MonsterRelay Instance { get; private set; }

        [Networked, Capacity(32)] public NetworkArray<MonsterNet> Monsters => default;
        [Networked] public int Count { get; set; }

        public override void Spawned() { Instance = this; }
        public override void Despawned(NetworkRunner runner, bool hasState) { if (Instance == this) Instance = null; }
    }
}
#endif
