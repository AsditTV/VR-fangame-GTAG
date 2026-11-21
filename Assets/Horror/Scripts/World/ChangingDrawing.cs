using UnityEngine;
using Horror.Locomotion;

namespace Horror.World
{
    /// <summary>
    /// Un dessin d'enfant qui change quand personne ne le regarde (comme les Doux, mais en silence) :
    /// tu passes, tu te retournes, ce n'est plus le même dessin. Un petit grattement de crayon trahit le changement.
    /// </summary>
    public class ChangingDrawing : MonoBehaviour
    {
        public Renderer target;
        public Material[] versions = new Material[0];
        [Tooltip("Temps sans être regardé avant de changer (s).")]
        public float unseenTime = 6f;
        [Tooltip("Il ne change que si un joueur est assez près pour le remarquer (m).")]
        public float range = 9f;
        public float viewAngle = 55f;
        public AudioClip scratch;

        private int _index;
        private float _unseen;

        private void Update()
        {
            if (target == null || versions.Length < 2) return;
            bool near = false, seen = false;
            foreach (ArmLocomotion p in ArmLocomotion.All)
            {
                if (p == null || p.head == null) continue;
                Vector3 to = target.bounds.center - p.head.position;
                if (to.magnitude > range) continue;
                near = true;
                if (Vector3.Angle(p.head.forward, to) < viewAngle) seen = true;
            }
            _unseen = (near && !seen) ? _unseen + Time.deltaTime : 0f;
            if (_unseen < unseenTime) return;
            _unseen = 0f;
            _index = (_index + 1) % versions.Length;
            target.sharedMaterial = versions[_index];
            if (scratch != null) AudioSource.PlayClipAtPoint(scratch, target.bounds.center, 0.35f);
        }
    }
}
