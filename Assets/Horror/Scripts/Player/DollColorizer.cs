using System.Collections.Generic;
using UnityEngine;

namespace Horror.Player
{
    /// <summary>
    /// Teint le tissu de la poupée avec la couleur choisie sur le Minitel (corps, bras, jambes).
    /// La tête, le masque, les mains et les pièces rapiécées gardent leur couleur.
    /// À mettre sur le modèle LeDessine. Pour un autre joueur : SetColor() avec la couleur reçue du réseau.
    /// </summary>
    public class DollColorizer : MonoBehaviour
    {
        [Tooltip("Vrai = suit les réglages du joueur local. Faux = couleur donnée par le réseau.")]
        public bool isLocal = true;
        [Tooltip("Noms (début) des pièces à teindre.")]
        public string[] fabricParts = { "Corps", "BrasHaut_", "AvantBras_", "Epaule_Boule_" };   // pas de jambes

        private static readonly string[] ColorProps = { "_BaseColor", "_Color", "baseColorFactor" };
        private readonly List<Renderer> _targets = new List<Renderer>();
        private MaterialPropertyBlock _block;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
                foreach (string part in fabricParts)
                    if (r.gameObject.name.StartsWith(part) || (r.transform.parent != null && r.transform.parent.name.StartsWith(part)))
                    { _targets.Add(r); break; }
        }

        private void OnEnable()
        {
            if (!isLocal) return;
            GameSettings.Changed += ApplyLocal;
            ApplyLocal();
        }

        private void OnDisable()
        {
            if (isLocal) GameSettings.Changed -= ApplyLocal;
        }

        private void ApplyLocal() => SetColor(GameSettings.Color);

        public void SetColor(Color c)
        {
            foreach (Renderer r in _targets)
            {
                if (r == null || r.sharedMaterial == null) continue;
                r.GetPropertyBlock(_block);
                foreach (string p in ColorProps)
                    if (r.sharedMaterial.HasProperty(p)) _block.SetColor(p, c);
                r.SetPropertyBlock(_block);
            }
        }
    }
}
