using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Horror.Core
{
    /// <summary>
    /// Profil graphique « Quest » : appliqué tout seul au lancement sur Android (Quest autonome), jamais sur PC.
    /// Le post-traitement est le plus gros coût caché sur Quest : on coupe le grain, l'aberration chromatique et le
    /// HDR, on allège le halo et on garde l'étalonnage pastel (couleurs, vignette) qui fait la DA.
    /// Pour l'essayer sur PC : cocher forceOnPc sur l'objet « Profil graphique ».
    /// </summary>
    public class QuestQuality : MonoBehaviour
    {
        public bool forceOnPc;
        [Tooltip("Rendu interne (1 = natif). 0,9 gagne ~20 % de pixels si le Quest peine encore.")]
        [Range(0.7f, 1.2f)] public float renderScale = 1f;

        public static bool Active { get; private set; }

        private bool _hdr; private float _scale; private int _msaa; private float _shadow; private UniversalRenderPipelineAsset _urp;

        private void Awake()
        {
            Active = Application.platform == RuntimePlatform.Android || forceOnPc;
            if (!Active) return;
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                _urp = urp; _hdr = urp.supportsHDR; _scale = urp.renderScale; _msaa = urp.msaaSampleCount; _shadow = urp.shadowDistance;
                urp.supportsHDR = false;
                urp.renderScale = renderScale;
                urp.msaaSampleCount = 4;            // les bords nets comptent plus en VR que les effets
                urp.shadowDistance = Mathf.Min(urp.shadowDistance, 15f);
            }
            foreach (Volume v in FindObjectsByType<Volume>(FindObjectsInactive.Include))
            {
                VolumeProfile p = v.profile;      // copie locale : l'asset du projet n'est pas modifié
                if (p == null) continue;
                if (p.TryGet(out FilmGrain g)) g.active = false;
                if (p.TryGet(out ChromaticAberration ca)) ca.active = false;
                if (p.TryGet(out Bloom b)) { b.intensity.Override(Mathf.Min(b.intensity.value, 0.3f)); b.highQualityFiltering.Override(false); }
            }
            QualitySettings.vSyncCount = 0;       // le Quest cadence lui-même
            Debug.Log("[Horror] Profil graphique Quest appliqué (sans grain, sans aberration, sans HDR).");
        }

        // dans l'éditeur, l'asset URP modifié en Play le resterait : on remet les réglages PC en sortant
        private void OnDestroy()
        {
            if (_urp == null) return;
            _urp.supportsHDR = _hdr; _urp.renderScale = _scale; _urp.msaaSampleCount = _msaa; _urp.shadowDistance = _shadow;
        }
    }
}
