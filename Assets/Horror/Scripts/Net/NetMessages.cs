using UnityEngine;
using Horror.Noise;

namespace Horror.Net
{
    // Messages échangés en ligne. Volontairement petits (le Quest est en Wi-Fi) et indépendants de Photon :
    // l'adaptateur Fusion les copiera dans ses propres structures réseau (NetworkStruct), rien d'autre à changer.

    /// <summary>La pose d'un joueur (envoyée ~20 fois par seconde) : tête et deux mains.</summary>
    public struct PlayerPose
    {
        public int playerId;
        public Vector3 head;
        public Quaternion headRotation;
        public Vector3 leftHand, rightHand;
        public Quaternion leftRotation, rightRotation;
        public bool dead;
        public byte mouth;             // ouverture de la bouche 0..255 (la voix anime la poupée chez les autres)
    }

    /// <summary>L'état d'un monstre, calculé UNIQUEMENT chez l'hôte, et recopié chez les autres.</summary>
    public struct MonsterState
    {
        public int monsterId;          // index du monstre dans la scène (ordre stable, voir NetworkBootstrap)
        public Vector3 position;
        public float yaw;
        public byte state;             // BlindMonsterAI.State, ou équivalent pour les autres monstres
        public int targetPlayerId;     // -1 : personne
    }

    /// <summary>Un bruit fait par un joueur distant (voir NoiseSystem.ReceiveRemote).</summary>
    public struct NoiseMessage
    {
        public Vector3 position;
        public float radius;
        public NoiseType type;

        public static NoiseMessage From(NoiseEvent e) => new NoiseMessage { position = e.position, radius = e.radius, type = e.type };
        public NoiseEvent ToEvent() => new NoiseEvent(position, radius, type, null);
    }

    /// <summary>Un événement ponctuel de partie (mort, papier ramassé, porte, fin trouvée...).</summary>
    public struct GameEventMessage
    {
        public enum Kind : byte { Mort, Papier, Lieu, Porte, Fin, Coupure, Capture }   // Capture : value = n° du monstre (MonsterSync), playerId = la victime
        public Kind kind;
        public int playerId;
        public int value;              // numéro du papier, de la porte, etc.
        public Vector3 position;       // où (la mort : là où l'encre gicle chez les autres)
    }
}
