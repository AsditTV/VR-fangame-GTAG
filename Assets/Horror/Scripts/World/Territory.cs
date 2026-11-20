using System.Collections.Generic;
using UnityEngine;

namespace Horror.World
{
    /// <summary>
    /// Territoire d'un monstre : une boîte qui lui appartient. Les AUTRES monstres ignorent ce qui s'y passe
    /// (ils n'y entendent rien, n'y attrapent personne) : chaque monstre règne sur sa zone.
    /// Ex. la Laverie appartient au Drap : L'Écoute n'y entre pas (l'eau dissout son encre).
    /// </summary>
    public class Territory : MonoBehaviour
    {
        [Tooltip("Identifiant du monstre propriétaire (ex. « Drap », « Ecoute »).")]
        public string owner = "Drap";
        [Tooltip("Taille de la boîte (m), centrée sur cet objet.")]
        public Vector3 size = new Vector3(10f, 6f, 10f);

        private static readonly List<Territory> All = new List<Territory>();
        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        public bool Contains(Vector3 world)
        {
            Vector3 p = transform.InverseTransformPoint(world);
            return Mathf.Abs(p.x) <= size.x * 0.5f && Mathf.Abs(p.y) <= size.y * 0.5f && Mathf.Abs(p.z) <= size.z * 0.5f;
        }

        /// <summary>Vrai si ce point est dans le territoire d'un AUTRE monstre que <paramref name="monster"/>.</summary>
        public static bool IsForeign(Vector3 world, string monster)
        {
            foreach (Territory t in All)
                if (t != null && t.owner != monster && t.Contains(world)) return true;
            return false;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.5f, 0.75f, 1f, 0.3f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, size);
        }
    }
}
