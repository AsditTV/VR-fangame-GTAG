using UnityEngine;
using Horror.Locomotion;

namespace Horror.Tests
{
    /// <summary>
    /// Outil de test (dev) : mesure le mouvement du joueur pendant un essai sans casque (PlaytestBot) :
    /// distance horizontale, hauteur max de la tête au-dessus du départ, temps passé en l'air (aucune main posée),
    /// hauteur des yeux au-dessus du sol au repos.
    /// </summary>
    public class MotionProbe : MonoBehaviour
    {
        public ArmLocomotion loco;
        public Vector3 start;
        public float startHeadY, maxHeadRise, airTime, longestAir, curAir, minEyeAboveGround = 99f, maxEyeAboveGround;

        public static MotionProbe Begin(ArmLocomotion l)
        {
            var p = l.gameObject.GetComponent<MotionProbe>();
            if (p == null) p = l.gameObject.AddComponent<MotionProbe>();
            p.loco = l; p.start = l.transform.position; p.startHeadY = l.head.position.y;
            p.maxHeadRise = 0f; p.airTime = 0f; p.longestAir = 0f; p.curAir = 0f; p.minEyeAboveGround = 99f; p.maxEyeAboveGround = 0f;
            return p;
        }

        private void FixedUpdate()
        {
            if (loco == null) return;
            maxHeadRise = Mathf.Max(maxHeadRise, loco.head.position.y - startHeadY);
            if (!loco.IsTouchingSurface) { airTime += Time.fixedDeltaTime; curAir += Time.fixedDeltaTime; longestAir = Mathf.Max(longestAir, curAir); }
            else curAir = 0f;
            if (Physics.Raycast(loco.head.position, Vector3.down, out RaycastHit hit, 5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                minEyeAboveGround = Mathf.Min(minEyeAboveGround, hit.distance);
                maxEyeAboveGround = Mathf.Max(maxEyeAboveGround, hit.distance);
            }
        }

        public string Report()
        {
            Vector3 d = loco.transform.position - start; d.y = 0f;
            return $"distance {d.magnitude:0.00} m | tête monte de {maxHeadRise:0.00} m | en l'air {airTime:0.0} s (plus long saut {longestAir:0.00} s) | yeux/sol {minEyeAboveGround:0.00}–{maxEyeAboveGround:0.00} m";
        }
    }
}
