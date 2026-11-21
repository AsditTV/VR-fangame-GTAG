using System;
using System.Collections.Generic;
using UnityEngine;
using Horror.Locomotion;

namespace Horror.Player
{
    /// <summary>Ce qu'un monstre montre quand il attrape quelqu'un (le screamer joué chez la victime).</summary>
    public interface IScreamer
    {
        Transform ScreamBody { get; }
        Transform ScreamFace { get; }
        AudioClip ScreamClip { get; }
        float ScreamScale { get; }
    }

    /// <summary>
    /// Une proie pour les monstres : le joueur de CETTE machine (ArmLocomotion) ou un joueur distant (son avatar
    /// réseau). Les monstres ne parlent qu'à ça : en ligne, celui qui fait tourner l'IA voit et chasse tout le monde.
    /// Attraper un joueur distant ne joue rien ici : on lui envoie « attrapé » et le screamer se joue chez lui.
    /// </summary>
    public class PlayerTarget
    {
        public readonly ArmLocomotion local;
        public Transform head, root;
        public int netId = -1;
        public bool remoteDead;
        internal Vector3 remoteVelocity;

        public PlayerTarget(ArmLocomotion localPlayer) { local = localPlayer; head = localPlayer.head; root = localPlayer.transform; }
        public PlayerTarget(int playerId, Transform remoteHead, Transform remoteRoot) { netId = playerId; head = remoteHead; root = remoteRoot; }

        public bool IsLocal => local != null;
        public Vector3 HeadPosition => local != null ? local.HeadPosition : (head != null ? head.position : root.position);
        public Vector3 Velocity => local != null ? local.Velocity : remoteVelocity;
        /// <summary>Faux pendant un screamer / une réapparition : on ne l'attrape pas deux fois.</summary>
        public bool inputEnabled => local != null ? local.inputEnabled : !remoteDead;
        public Rigidbody Body => local != null ? local.GetComponent<Rigidbody>() : null;
        public bool Owns(Transform t) => t != null && root != null && t.IsChildOf(root);
        public GameObject gameObject => root != null ? root.gameObject : null;

        /// <summary>Un joueur distant vient d'être attrapé par ce monstre : le réseau le prévient (NetworkBootstrap).</summary>
        public static event Action<PlayerTarget, IScreamer> RemoteCaught;

        public void Catch(IScreamer monster)
        {
            if (local == null) { RemoteCaught?.Invoke(this, monster); return; }
            Jumpscare js = local.GetComponent<Jumpscare>();
            if (js != null) js.TriggerScreamer(monster.ScreamBody, monster.ScreamFace, monster.ScreamClip, monster.ScreamScale);
            else local.TeleportTo(local.SpawnPosition);
        }
    }

    /// <summary>Toutes les proies : les joueurs locaux (ArmLocomotion.All) puis les joueurs distants.</summary>
    public static class PlayerTargets
    {
        private static readonly List<PlayerTarget> _all = new List<PlayerTarget>();
        private static readonly List<PlayerTarget> _remotes = new List<PlayerTarget>();
        private static readonly Dictionary<ArmLocomotion, PlayerTarget> _locals = new Dictionary<ArmLocomotion, PlayerTarget>();

        public static IReadOnlyList<PlayerTarget> All
        {
            get
            {
                _all.Clear();
                foreach (ArmLocomotion a in ArmLocomotion.All)
                {
                    if (a == null) continue;
                    // cache vérifié : il ne doit jamais rendre la cible d'un joueur détruit (partie précédente dans l'éditeur)
                    if (!_locals.TryGetValue(a, out PlayerTarget t) || !ReferenceEquals(t.local, a) || t.local == null) _locals[a] = t = new PlayerTarget(a);
                    _all.Add(t);
                }
                foreach (PlayerTarget r in _remotes) if (r.root != null && r.root.gameObject.activeInHierarchy) _all.Add(r);
                return _all;
            }
        }

        /// <summary>Le joueur de cette machine (le premier), ou null.</summary>
        public static PlayerTarget Local
        {
            get { foreach (PlayerTarget t in All) if (t.IsLocal) return t; return null; }
        }

        public static void AddRemote(PlayerTarget t) { if (t != null && !_remotes.Contains(t)) _remotes.Add(t); }
        public static void RemoveRemote(PlayerTarget t) { _remotes.Remove(t); }
    }
}
