using UnityEngine;
using Horror.Interaction;

namespace Horror.Lore
{
    /// <summary>
    /// Un vieux projecteur de diapositives oublié au grenier. Chaque pression du bouton : clac, la diapo suivante
    /// s'affiche sur le drap tendu, avec sa légende tapée à la machine. Une autre façon de raconter le lore : par
    /// images (ici, l'histoire de la Veilleuse et de l'extinction des feux).
    /// </summary>
    public class SlideProjector : MonoBehaviour
    {
        public Texture2D[] slides;
        [TextArea] public string[] captions;
        public Renderer screen;
        public TextMesh caption;
        public PressButton button;
        public Light beam;
        public AudioClip clack;

        private int _index = -1;
        private Material _mat;

        private void OnEnable() { if (button != null) button.onPressed.AddListener(Next); }
        private void OnDisable() { if (button != null) button.onPressed.RemoveListener(Next); }

        private void Start()
        {
            if (screen != null)
            {
                _mat = screen.material;                                        // copie propre à l'écran
                _mat.EnableKeyword("_EMISSION");                              // le fichier du matériau ne l'a pas : sans lui, la diapo ne s'allume pas
                _mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            Show(-1);
        }

        public void Next()
        {
            if (slides == null || slides.Length == 0) return;
            _index = (_index + 1) % slides.Length;
            if (clack != null) AudioSource.PlayClipAtPoint(clack, transform.position, 0.7f);
            Show(_index);
        }

        private void Show(int i)
        {
            bool on = i >= 0;
            if (beam != null) beam.enabled = on;
            if (_mat != null)
            {
                Texture2D tex = on ? slides[i] : null;
                if (_mat.HasProperty("_BaseMap")) _mat.SetTexture("_BaseMap", tex);
                if (_mat.HasProperty("_MainTex")) _mat.SetTexture("_MainTex", tex);
                Color c = on ? Color.white : new Color(0.12f, 0.11f, 0.1f);
                if (_mat.HasProperty("_BaseColor")) _mat.SetColor("_BaseColor", c);
                if (_mat.HasProperty("_EmissionColor")) _mat.SetColor("_EmissionColor", on ? new Color(0.9f, 0.86f, 0.76f) : Color.black);
                if (on && _mat.HasProperty("_EmissionMap")) _mat.SetTexture("_EmissionMap", tex);
            }
            if (caption != null) caption.text = on && captions != null && i < captions.Length ? captions[i] : "SLIDES  [PRESS]";
        }
    }
}
