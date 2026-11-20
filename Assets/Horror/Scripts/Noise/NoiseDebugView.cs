using System.Collections.Generic;
using UnityEngine;

namespace Horror.Noise
{
    /// <summary>
    /// Outil de test : dessine un anneau au sol pour chaque bruit (visible dans le casque),
    /// de la taille de sa portée. Rose = voix, vert d'eau = pas, jaune pâle = objet.
    /// À mettre sur n'importe quel objet de la scène de test. À désactiver dans le vrai jeu.
    /// </summary>
    public class NoiseDebugView : MonoBehaviour
    {
        [Tooltip("Durée d'affichage d'un anneau (secondes).")]
        public float lifetime = 1.2f;
        public int segments = 48;

        private class Ring { public LineRenderer line; public float born; public float radius; }

        private readonly List<Ring> _rings = new List<Ring>();
        private readonly Stack<LineRenderer> _pool = new Stack<LineRenderer>();
        private Material _material;

        private void OnEnable()
        {
            NoiseSystem.Heard += OnNoise;
            if (_material == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                _material = new Material(shader);
            }
        }

        private void OnDisable() => NoiseSystem.Heard -= OnNoise;

        private void OnNoise(NoiseEvent e)
        {
            LineRenderer line = _pool.Count > 0 ? _pool.Pop() : CreateLine();
            line.gameObject.SetActive(true);
            Color c = e.type == NoiseType.Voix ? new Color(0.95f, 0.6f, 0.7f)
                    : e.type == NoiseType.Pas ? new Color(0.6f, 0.85f, 0.75f)
                    : new Color(0.95f, 0.9f, 0.6f);
            line.startColor = line.endColor = c;
            line.transform.position = e.position;
            _rings.Add(new Ring { line = line, born = Time.time, radius = e.radius });
        }

        private void Update()
        {
            for (int i = _rings.Count - 1; i >= 0; i--)
            {
                Ring r = _rings[i];
                float t = (Time.time - r.born) / lifetime;
                if (t >= 1f)
                {
                    r.line.gameObject.SetActive(false);
                    _pool.Push(r.line);
                    _rings.RemoveAt(i);
                    continue;
                }
                // L'anneau grandit jusqu'à la portée du bruit puis s'efface
                float radius = r.radius * Mathf.Sqrt(t);
                Color c = r.line.startColor;
                c.a = 1f - t;
                r.line.startColor = r.line.endColor = c;
                for (int s = 0; s < segments; s++)
                {
                    float a = s / (float)segments * Mathf.PI * 2f;
                    r.line.SetPosition(s, new Vector3(Mathf.Cos(a) * radius, 0.02f, Mathf.Sin(a) * radius));
                }
            }
        }

        private LineRenderer CreateLine()
        {
            var go = new GameObject("Anneau de bruit");
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = segments;
            line.widthMultiplier = 0.03f;
            line.sharedMaterial = _material;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return line;
        }
    }
}
