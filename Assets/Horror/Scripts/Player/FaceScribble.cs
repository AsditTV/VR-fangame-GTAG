using UnityEngine;

namespace Horror.Player
{
    /// <summary>
    /// Fait "gribouiller" le visage dessiné selon la voix :
    ///  - silence : le dessin normal, immobile ;
    ///  - on parle : le trait tremble (images face_parle_0..2, comme un dessin animé fait main) ;
    ///  - on CRIE : les yeux se font rayer de gribouillis (images face_cri_0..2) et tout s'agite plus vite.
    ///
    /// Se branche sur MicMouth (même objet ou parent). Marche aussi pour les autres joueurs,
    /// puisque MicMouth.Openness est la valeur synchronisée en réseau.
    ///
    /// Compatible avec les deux versions du personnage :
    ///  - l'avatar 2D de l'étape 1 (SpriteRenderer) → glisse des Sprites ;
    ///  - le modèle 3D LeDessine (MeshRenderer du masque) → glisse des Textures.
    /// </summary>
    public class FaceScribble : MonoBehaviour
    {
        [Header("Source du son")]
        [Tooltip("Laisse vide : il cherche MicMouth sur cet objet ou ses parents.")]
        public MicMouth mic;

        [Header("Cibles (remplis celles qui existent)")]
        [Tooltip("Rendu du visage (masque 3D ou sprite).")]
        public Renderer faceRenderer;
        [Tooltip("Rendu de la bouche (facultatif).")]
        public Renderer mouthRenderer;

        [Header("Images (dossier Assets/Horror/Art/Face/Boil)")]
        public Texture2D faceIdle;
        public Texture2D[] faceTalk = new Texture2D[3];
        public Texture2D[] faceScream = new Texture2D[3];
        public Texture2D mouthIdle;
        public Texture2D[] mouthTalk = new Texture2D[3];

        [Header("Seuils (0..1 = ouverture de la bouche)")]
        [Tooltip("Au-dessus : le trait commence à trembler.")]
        [Range(0f, 1f)] public float talkThreshold = 0.12f;
        [Tooltip("Au-dessus : c'est un cri, les gribouillis apparaissent.")]
        [Range(0f, 1f)] public float screamThreshold = 0.8f;
        [Tooltip("Durée minimale au-dessus du seuil pour compter comme un cri (évite les faux cris).")]
        public float screamHoldTime = 0.25f;

        [Header("Vitesse d'animation (images / seconde)")]
        public float talkFps = 9f;
        public float screamFps = 15f;

        private enum Mode { Idle, Talk, Scream }

        private Mode _mode = Mode.Idle;
        private float _aboveScreamFor;
        private float _frameTimer;
        private int _frame;
        private MaterialPropertyBlock _block;

        // Sprites créés à partir des textures, pour l'avatar 2D
        private Sprite[] _faceTalkSprites, _faceScreamSprites, _mouthTalkSprites;
        private Sprite _faceIdleSprite, _mouthIdleSprite;

        // Noms de la texture principale selon le shader (URP, Built-in, glTFast)
        private static readonly string[] TextureProperties = { "_BaseMap", "_MainTex", "baseColorTexture" };

        private void Awake()
        {
            if (mic == null) mic = GetComponentInParent<MicMouth>();
            _block = new MaterialPropertyBlock();

            if (faceRenderer is SpriteRenderer faceSprite)
            {
                _faceIdleSprite = faceSprite.sprite;
                _faceTalkSprites = ToSprites(faceTalk, faceSprite.sprite);
                _faceScreamSprites = ToSprites(faceScream, faceSprite.sprite);
            }
            if (mouthRenderer is SpriteRenderer mouthSprite)
            {
                _mouthIdleSprite = mouthSprite.sprite;
                _mouthTalkSprites = ToSprites(mouthTalk, mouthSprite.sprite);
            }
        }

        private void Update()
        {
            if (mic == null) return;

            float level = mic.Openness;

            // Un cri = rester fort assez longtemps.
            _aboveScreamFor = level >= screamThreshold ? _aboveScreamFor + Time.deltaTime : 0f;
            Mode mode = _aboveScreamFor >= screamHoldTime ? Mode.Scream
                      : level >= talkThreshold ? Mode.Talk
                      : Mode.Idle;

            if (mode != _mode)
            {
                _mode = mode;
                _frameTimer = 0f;
                ShowFrame();
                return;
            }

            if (_mode == Mode.Idle) return;

            _frameTimer += Time.deltaTime;
            float frameTime = 1f / (_mode == Mode.Scream ? screamFps : talkFps);
            if (_frameTimer >= frameTime)
            {
                _frameTimer -= frameTime;
                // Jamais deux fois la même image d'affilée : c'est ce qui donne l'effet "trait vivant".
                _frame = (_frame + 1 + Random.Range(0, 2)) % 3;
                ShowFrame();
            }
        }

        private void ShowFrame()
        {
            switch (_mode)
            {
                case Mode.Idle:
                    SetFace(faceIdle, _faceIdleSprite);
                    SetMouth(mouthIdle, _mouthIdleSprite);
                    break;
                case Mode.Talk:
                    SetFace(Pick(faceTalk), PickSprite(_faceTalkSprites));
                    SetMouth(Pick(mouthTalk), PickSprite(_mouthTalkSprites));
                    break;
                case Mode.Scream:
                    SetFace(Pick(faceScream), PickSprite(_faceScreamSprites));
                    SetMouth(Pick(mouthTalk), PickSprite(_mouthTalkSprites));
                    break;
            }
        }

        // ------------------------------------------------------------------ application

        private void SetFace(Texture2D tex, Sprite sprite) => Apply(faceRenderer, tex, sprite);
        private void SetMouth(Texture2D tex, Sprite sprite) => Apply(mouthRenderer, tex, sprite);

        private void Apply(Renderer target, Texture2D tex, Sprite sprite)
        {
            if (target == null) return;

            if (target is SpriteRenderer sr)
            {
                if (sprite != null) sr.sprite = sprite;
                return;
            }

            if (tex == null || target.sharedMaterial == null) return;

            // MaterialPropertyBlock : change l'image de CE joueur sans toucher au matériau partagé.
            target.GetPropertyBlock(_block);
            foreach (string property in TextureProperties)
            {
                if (target.sharedMaterial.HasProperty(property))
                    _block.SetTexture(property, tex);
            }
            target.SetPropertyBlock(_block);
        }

        private Texture2D Pick(Texture2D[] frames)
        {
            if (frames == null || frames.Length == 0) return null;
            return frames[_frame % frames.Length];
        }

        private Sprite PickSprite(Sprite[] frames)
        {
            if (frames == null || frames.Length == 0) return null;
            return frames[_frame % frames.Length];
        }

        private static Sprite[] ToSprites(Texture2D[] textures, Sprite reference)
        {
            if (textures == null || reference == null) return null;
            var result = new Sprite[textures.Length];
            for (int i = 0; i < textures.Length; i++)
            {
                if (textures[i] == null) continue;
                // Même taille à l'écran et même point d'ancrage que le dessin d'origine.
                Vector2 pivot = new Vector2(reference.pivot.x / reference.rect.width,
                                            reference.pivot.y / reference.rect.height);
                result[i] = Sprite.Create(textures[i], new Rect(0, 0, textures[i].width, textures[i].height),
                                          pivot, reference.pixelsPerUnit);
            }
            return result;
        }
    }
}
