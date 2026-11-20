using UnityEngine;

namespace Horror.World
{
    /// <summary>
    /// Fait tourner un objet autour de son pivot (les pales du grand ventilateur).
    /// Un léger à-coup régulier donne l'impression d'une vieille machine fatiguée.
    /// </summary>
    public class SlowSpinner : MonoBehaviour
    {
        [Tooltip("Axe de rotation (repère monde).")]
        public Vector3 axis = Vector3.right;
        [Tooltip("Vitesse moyenne en degrés par seconde (120 = un tour en 3 s).")]
        public float degreesPerSecond = 120f;
        [Range(0f, 1f)] public float hitch = 0.25f;

        private void Update()
        {
            float wobble = 1f - hitch * Mathf.Max(0f, Mathf.Sin(Time.time * Mathf.PI * 2f / 3f));   // ralentit une fois par tour
            transform.Rotate(axis, degreesPerSecond * wobble * Time.deltaTime, Space.World);
        }
    }
}
