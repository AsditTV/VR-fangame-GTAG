using System.Collections.Generic;
using UnityEngine;

namespace Horror.Noise
{
    /// <summary>
    /// Cachette : une boîte invisible où les bruits portent beaucoup moins loin.
    /// Dans l'Institut : les lits du dortoir. Assis parmi les enfants-poupées, sur un matelas mou,
    /// tu « fais la poupée » : tes coups de main et ta voix sont étouffés et L'Écoute passe à côté.
    /// NoiseSystem.Emit applique l'étouffement automatiquement (aucun réglage côté monstre).
    /// </summary>
    public class HideZone : MonoBehaviour
    {
        [Tooltip("Taille de la boîte (m), dans le repère de cet objet.")]
        public Vector3 size = new Vector3(1.8f, 1.0f, 0.9f);
        [Tooltip("Multiplie la portée des bruits faits dedans (0,3 = trois fois moins loin).")]
        [Range(0.05f, 1f)] public float muffle = 0.35f;

        private static readonly List<HideZone> All = new List<HideZone>();

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        /// <summary>Vrai si le point est dans cette cachette.</summary>
        public bool Contains(Vector3 world)
        {
            Vector3 p = transform.InverseTransformPoint(world);
            return Mathf.Abs(p.x) <= size.x * 0.5f && Mathf.Abs(p.y) <= size.y * 0.5f && Mathf.Abs(p.z) <= size.z * 0.5f;
        }

        /// <summary>Facteur d'étouffement au point donné (1 = pas caché).</summary>
        public static float MuffleAt(Vector3 world)
        {
            float k = 1f;
            foreach (HideZone z in All)
                if (z != null && z.Contains(world)) k = Mathf.Min(k, z.muffle);
            return k;
        }

        /// <summary>Vrai si ce point (la tête d'un joueur, par exemple) est caché quelque part.</summary>
        public static bool IsHidden(Vector3 world) => MuffleAt(world) < 1f;

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.6f, 0.9f, 0.7f, 0.35f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, size);
        }
    }
}
