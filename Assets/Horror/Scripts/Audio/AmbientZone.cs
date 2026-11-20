using System.Collections.Generic;
using UnityEngine;

namespace Horror.Audio
{
    /// <summary>
    /// Une zone d'ambiance sonore (une pièce ou un étage) : son fond en boucle et ses bruits ponctuels.
    /// Quand le joueur entre dedans, AmbientSoundscape fait un fondu vers cette ambiance.
    /// Exemples : sous-sol = bourdonnement + gouttes ; Salle de jeux = berceuse ; dortoir = grincements.
    /// </summary>
    public class AmbientZone : MonoBehaviour
    {
        [Tooltip("Taille de la zone (mètres), centrée sur cet objet.")]
        public Vector3 size = new Vector3(10f, 4f, 10f);
        [Tooltip("Si deux zones se chevauchent, la plus haute priorité gagne.")]
        public int priority = 0;

        [Header("Fond sonore en boucle")]
        public AudioClip loop;
        [Range(0f, 1f)] public float loopVolume = 0.5f;

        [Header("Bruits ponctuels autour du joueur")]
        public AudioClip[] oneShots;
        [Tooltip("Temps entre deux bruits (secondes, min / max).")]
        public Vector2 oneShotInterval = new Vector2(8f, 20f);
        [Range(0f, 1f)] public float oneShotVolume = 0.6f;

        [Tooltip("Coche pour la zone sûre : pas de battements de coeur ni de bruits inquiétants.")]
        public bool isSafe;

        public static readonly List<AmbientZone> All = new List<AmbientZone>();

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        public bool Contains(Vector3 point)
        {
            Vector3 local = transform.InverseTransformPoint(point);
            Vector3 half = size * 0.5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = isSafe ? new Color(0.6f, 0.9f, 0.7f, 0.6f) : new Color(0.7f, 0.6f, 0.9f, 0.6f);
            Gizmos.DrawWireCube(Vector3.zero, size);
        }
    }
}
