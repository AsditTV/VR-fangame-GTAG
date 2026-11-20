using System.Collections.Generic;
using UnityEngine;

namespace Horror.World
{
    /// <summary>
    /// Réseau de points de passage (un point par pièce, un point par porte) pour que les monstres
    /// trouvent leur chemin de pièce en pièce SANS NavMesh (aucun package à installer).
    /// Le menu "Horror > 8" le remplit à partir de la maquette. Dans la vue Scène, les points et
    /// les liens sont dessinés en rose.
    /// </summary>
    public class WaypointGraph : MonoBehaviour
    {
        [Tooltip("Positions des points (au sol).")]
        public Vector3[] points = new Vector3[0];
        [Tooltip("Liens entre points (indices).")]
        public Vector2Int[] links = new Vector2Int[0];

        public static WaypointGraph Instance { get; private set; }

        private List<int>[] _adj;

        private void Awake()
        {
            Instance = this;
            Build();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Build()
        {
            _adj = new List<int>[points.Length];
            for (int i = 0; i < points.Length; i++) _adj[i] = new List<int>();
            foreach (Vector2Int l in links)
            {
                if (l.x < 0 || l.y < 0 || l.x >= points.Length || l.y >= points.Length) continue;
                _adj[l.x].Add(l.y);
                _adj[l.y].Add(l.x);
            }
        }

        /// <summary>Le point le plus proche (en privilégiant ceux du même étage).</summary>
        public int Nearest(Vector3 pos)
        {
            int best = -1;
            float bestScore = float.MaxValue;
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 d = points[i] - pos;
                float score = new Vector2(d.x, d.z).sqrMagnitude + d.y * d.y * 9f;   // changer d'étage "coûte" cher
                if (score < bestScore) { bestScore = score; best = i; }
            }
            return best;
        }

        /// <summary>
        /// Chemin de "from" à "to" : liste de positions à suivre (le dernier élément est "to" lui-même).
        /// Algorithme de Dijkstra (le graphe est petit : quelques dizaines de points).
        /// </summary>
        public List<Vector3> Path(Vector3 from, Vector3 to)
        {
            var result = new List<Vector3>();
            if (_adj == null) Build();
            int a = Nearest(from), b = Nearest(to);
            if (a < 0 || b < 0) { result.Add(to); return result; }

            var dist = new float[points.Length];
            var prev = new int[points.Length];
            var done = new bool[points.Length];
            for (int i = 0; i < dist.Length; i++) { dist[i] = float.MaxValue; prev[i] = -1; }
            dist[a] = 0f;
            for (int step = 0; step < points.Length; step++)
            {
                int u = -1;
                float best = float.MaxValue;
                for (int i = 0; i < points.Length; i++)
                    if (!done[i] && dist[i] < best) { best = dist[i]; u = i; }
                if (u < 0 || u == b) break;
                done[u] = true;
                foreach (int v in _adj[u])
                {
                    float alt = dist[u] + Vector3.Distance(points[u], points[v]);
                    if (alt < dist[v]) { dist[v] = alt; prev[v] = u; }
                }
            }

            var stack = new Stack<Vector3>();
            for (int n = b; n != -1; n = prev[n])
            {
                stack.Push(points[n]);
                if (n == a) break;
            }
            // Si le premier point est derrière nous (on est déjà plus près du suivant), on le saute
            var path = new List<Vector3>(stack);
            if (path.Count >= 2 && Vector3.Distance(from, path[1]) < Vector3.Distance(path[0], path[1])) path.RemoveAt(0);
            path.Add(to);
            return path;
        }

        /// <summary>Points des pièces (pour les rondes) : ceux qui ne sont pas des portes.</summary>
        [Tooltip("Nombre de points qui sont des centres de pièces (au début de la liste).")]
        public int roomCount;

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.95f, 0.6f, 0.75f, 0.9f);
            for (int i = 0; i < points.Length; i++) Gizmos.DrawSphere(points[i] + Vector3.up * 0.1f, i < roomCount ? 0.3f : 0.15f);
            if (links == null) return;
            foreach (Vector2Int l in links)
                if (l.x < points.Length && l.y < points.Length)
                    Gizmos.DrawLine(points[l.x] + Vector3.up * 0.1f, points[l.y] + Vector3.up * 0.1f);
        }
    }
}
