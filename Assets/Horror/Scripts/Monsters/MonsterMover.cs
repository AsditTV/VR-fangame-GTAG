using UnityEngine;
using Horror.Player;

namespace Horror.Monsters
{
    /// <summary>
    /// Outils de déplacement communs aux monstres (ils se bloquaient trop facilement) :
    ///  - un objet lancé (balle, boîte à musique, chaise...) ne bloque plus : le monstre le pousse et passe ;
    ///  - <see cref="StuckWatch"/> : détecte qu'un monstre veut avancer mais ne progresse plus ;
    ///  - <see cref="TryUnstick"/> : le replace sur un point de passage, mais seulement si aucun joueur ne le voit
    ///    (on ne voit jamais un monstre se téléporter).
    /// </summary>
    public static class MonsterMover
    {
        /// <summary>
        /// Vrai si l'obstacle touché doit vraiment arrêter le monstre. Un objet physique libre (non cinématique)
        /// est poussé dans la direction de marche et ne compte pas comme obstacle.
        /// </summary>
        public static bool Blocks(RaycastHit hit, Transform self, Vector3 dir)
        {
            if (hit.collider == null) return false;
            if (hit.transform == self || hit.transform.IsChildOf(self)) return false;
            Rigidbody rb = hit.rigidbody;
            if (rb != null && !rb.isKinematic && rb.mass < 40f && rb.GetComponent<DoctorCart>() == null)   // le chariot du Docteur reste un obstacle
            {
                rb.AddForce((dir + Vector3.up * 0.3f).normalized * 2.5f, ForceMode.VelocityChange);   // il l'écarte en passant
                return false;
            }
            return true;
        }

        /// <summary>Un joueur pourrait-il voir ce point ? (moins de 25 m et rien entre sa tête et le point).
        /// <paramref name="self"/> : le monstre lui-même, qui ne cache pas son propre corps.</summary>
        public static bool SeenByAnyPlayer(Vector3 point, Transform self = null)
        {
            foreach (PlayerTarget p in PlayerTargets.All)
            {
                if (p == null) continue;
                Vector3 eye = p.HeadPosition;
                if ((point - eye).sqrMagnitude > 25f * 25f) continue;
                Vector3 chest = point + Vector3.up * 1.2f;
                if (!Physics.Linecast(eye, chest, out RaycastHit hit, Horror.Noise.NoiseSystem.occluderLayers, QueryTriggerInteraction.Ignore)) return true;
                if (self != null && hit.transform.IsChildOf(self)) return true;   // c'est le monstre qu'on voit
            }
            return false;
        }

        /// <summary>Replace le monstre sur <paramref name="spot"/> si personne ne voit ni l'endroit de départ ni l'arrivée.</summary>
        public static bool TryUnstick(Transform monster, Vector3 spot)
        {
            if (SeenByAnyPlayer(monster.position, monster) || SeenByAnyPlayer(spot)) return false;
            monster.position = spot;
            return true;
        }

        /// <summary>Surveille la progression d'un monstre qui essaie d'avancer.</summary>
        public class StuckWatch
        {
            public float window = 1.5f;      // période de mesure (s)
            public float minProgress = 0.3f; // distance minimale à parcourir pendant la période (m)

            private Vector3 _checkpoint;
            private float _timer;
            private bool _init;

            /// <summary>Nombre de périodes d'affilée sans progrès (0 = tout va bien).</summary>
            public int Level { get; private set; }

            /// <summary>À appeler à chaque image. Renvoie vrai au moment où une nouvelle période « bloquée » est constatée.</summary>
            public bool Update(Vector3 position, bool wantsToMove, float dt)
            {
                if (!_init || !wantsToMove) { _checkpoint = position; _timer = 0f; _init = true; if (!wantsToMove) Level = 0; return false; }
                _timer += dt;
                if (_timer < window) return false;
                Vector3 d = position - _checkpoint; d.y = 0f;
                _checkpoint = position;
                _timer = 0f;
                if (d.magnitude >= minProgress) { Level = 0; return false; }
                Level++;
                return true;
            }

            public void Reset() { Level = 0; _timer = 0f; _init = false; }
        }
    }
}
