using UnityEngine;
using Horror.Locomotion;

namespace Horror.World
{
    /// <summary>
    /// Change l'atmosphère (brouillard, lumière ambiante) tant que le joueur local est dans la boîte, puis la rend
    /// telle qu'elle était. Sert à l'Envers : un lieu de papier blanc, sans le brouillard noir de l'Institut.
    /// </summary>
    public class AtmosphereZone : MonoBehaviour
    {
        public Vector3 size = new Vector3(20f, 10f, 20f);
        public float fogDensity = 0.006f;
        public Color fogColor = new Color(0.95f, 0.93f, 0.9f);
        public Color ambient = new Color(0.55f, 0.53f, 0.5f);

        private bool _inside;
        private float _savedDensity;
        private Color _savedFog, _savedAmbient;

        private void Update()
        {
            bool now = false;
            var b = new Bounds(transform.position, size);
            foreach (ArmLocomotion p in ArmLocomotion.All)
                if (p != null && b.Contains(p.HeadPosition)) { now = true; break; }
            if (now == _inside) return;
            _inside = now;
            if (now)
            {
                _savedDensity = RenderSettings.fogDensity; _savedFog = RenderSettings.fogColor; _savedAmbient = RenderSettings.ambientLight;
                RenderSettings.fogDensity = fogDensity; RenderSettings.fogColor = fogColor; RenderSettings.ambientLight = ambient;
            }
            else
            {
                RenderSettings.fogDensity = _savedDensity; RenderSettings.fogColor = _savedFog; RenderSettings.ambientLight = _savedAmbient;
            }
        }

        private void OnDisable()
        {
            if (!_inside) return;
            _inside = false;
            RenderSettings.fogDensity = _savedDensity; RenderSettings.fogColor = _savedFog; RenderSettings.ambientLight = _savedAmbient;
        }

        private void OnDrawGizmosSelected() { Gizmos.color = Color.white; Gizmos.DrawWireCube(transform.position, size); }
    }
}
