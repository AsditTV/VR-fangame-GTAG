using System.Collections.Generic;
using UnityEngine;

namespace Horror.World
{
    /// <summary>
    /// Zone sûre : la Salle de jeux (Playroom), là où on apparaît et réapparaît après s'être fait attraper.
    /// Lore : c'est la seule pièce de l'institut SANS micro relié au Standard. L'Écoute n'y entend rien,
    /// donc elle n'y va jamais. La boutique de cosmétiques est ici.
    ///
    /// Règles en jeu :
    ///  - les bruits faits ici sont ignorés par les monstres ;
    ///  - un monstre ne rentre pas dans la zone (il s'arrête au bord) ;
    ///  - un joueur à l'intérieur ne peut pas être attrapé.
    ///
    /// À placer sur un objet vide au centre de la pièce. Régle "Size" pour couvrir toute la pièce
    /// (la boîte verte dans la vue Scène).
    /// </summary>
    public class SafeZone : MonoBehaviour
    {
        [Tooltip("Taille de la zone (mètres), centrée sur cet objet.")]
        public Vector3 size = new Vector3(8f, 4f, 8f);
        [Tooltip("Point de réapparition des joueurs (vide = centre de la zone).")]
        public Transform respawnPoint;

        private static readonly List<SafeZone> s_all = new List<SafeZone>();

        private void OnEnable() => s_all.Add(this);
        private void OnDisable() => s_all.Remove(this);

        /// <summary>Vrai si ce point est dans une zone sûre.</summary>
        public static bool Contains(Vector3 point)
        {
            foreach (SafeZone zone in s_all)
                if (zone.ContainsLocal(point)) return true;
            return false;
        }

        /// <summary>Où faire réapparaître un joueur (null s'il n'y a pas de zone sûre dans la scène).</summary>
        public static Vector3? RespawnPosition
        {
            get
            {
                if (s_all.Count == 0) return null;
                SafeZone z = s_all[0];
                return z.respawnPoint != null ? z.respawnPoint.position : z.transform.position;
            }
        }

        /// <summary>
        /// Si "target" est dans une zone sûre, renvoie le point le plus proche juste à l'extérieur
        /// (pour qu'un monstre s'arrête au bord au lieu d'entrer).
        /// </summary>
        public static Vector3 KeepOutside(Vector3 target, float margin = 0.8f)
        {
            foreach (SafeZone zone in s_all)
            {
                if (!zone.ContainsLocal(target)) continue;
                Vector3 local = zone.transform.InverseTransformPoint(target);
                Vector3 half = zone.size * 0.5f;
                // On sort par le côté le plus proche (en X ou en Z)
                float dx = half.x - Mathf.Abs(local.x);
                float dz = half.z - Mathf.Abs(local.z);
                if (dx < dz) local.x = Mathf.Sign(local.x == 0 ? 1 : local.x) * (half.x + margin);
                else local.z = Mathf.Sign(local.z == 0 ? 1 : local.z) * (half.z + margin);
                return zone.transform.TransformPoint(local);
            }
            return target;
        }

        private bool ContainsLocal(Vector3 point)
        {
            Vector3 local = transform.InverseTransformPoint(point);
            Vector3 half = size * 0.5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }

        private void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.6f, 0.9f, 0.7f, 0.15f);
            Gizmos.DrawCube(Vector3.zero, size);
            Gizmos.color = new Color(0.6f, 0.9f, 0.7f, 0.8f);
            Gizmos.DrawWireCube(Vector3.zero, size);
        }
    }
}
