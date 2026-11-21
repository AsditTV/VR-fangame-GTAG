using System.Collections;
using System.IO;
using UnityEngine;

namespace Horror.Tests
{
    /// <summary>
    /// Filme le VRAI jeu image par image (vidéos TikTok verticales) sans casque : une caméra 1080 x 1920 qui
    /// enregistre chaque image en JPG pendant que le temps du jeu avance exactement de 1/30 s par image
    /// (Time.captureFramerate) : la vidéo est fluide même si l'éditeur rame.
    ///  - Glide(...) : mouvement de caméra scripté dans la scène (travelling) ;
    ///  - FollowHead() : la caméra copie la tête du joueur (et son masque de rendu : les screamers sont filmés).
    /// Le son n'est pas capturé : on le remet au montage avec les mêmes fichiers du jeu (Tools/Trailer).
    /// Outil de développement : à ne pas laisser dans un build.
    /// </summary>
    public class FrameRecorder : MonoBehaviour
    {
        public string folder;
        public int width = 1080, height = 1920, fps = 30;
        public float fieldOfView = 78f;
        public Camera source;

        public int Frame { get; private set; }

        /// <summary>L'enregistreur de la scène, créé s'il n'existe pas encore.</summary>
        public static FrameRecorder Ensure(string folder, Camera source)
        {
            var r = FindAnyObjectByType<FrameRecorder>();
            return r != null ? r : Begin(folder, source);
        }
        public bool Busy => _glide != null;

        private Camera _cam;
        private RenderTexture _rt;
        private Texture2D _tex;
        private bool _follow;
        private Coroutine _glide;
        private int _skip;
        private int _mask; private CameraClearFlags _clear; private Color _bg;

        /// <summary>Crée l'enregistreur sur un objet de la scène et commence à filmer.</summary>
        public static FrameRecorder Begin(string folder, Camera source)
        {
            var go = new GameObject("Enregistreur vidéo (dev)");
            var r = go.AddComponent<FrameRecorder>();
            r.folder = folder; r.source = source;
            return r;
        }

        private void Start()
        {
            Directory.CreateDirectory(folder);
            Frame = Directory.GetFiles(folder, "f*.jpg").Length;   // reprend la numérotation si on refilme à la suite
            _rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            _tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            _cam = gameObject.AddComponent<Camera>();
            if (source != null) _cam.CopyFrom(source);
            _cam.targetTexture = _rt;
            _mask = _cam.cullingMask; _clear = _cam.clearFlags; _bg = _cam.backgroundColor;
            _cam.fieldOfView = fieldOfView;
            _cam.stereoTargetEye = StereoTargetEyeMask.None;
            _cam.nearClipPlane = 0.03f;
            var data = gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            Time.captureFramerate = fps;
            StartCoroutine(Capture());
        }

        private void OnDestroy()
        {
            Time.captureFramerate = 0;
            if (_rt != null) _rt.Release();
        }

        /// <summary>La caméra suit la tête du joueur (vue à la première personne, mains comprises).</summary>
        public void FollowHead() { _follow = true; _snap = true; StopGlide(); }

        [Tooltip("Stabilisation de la vue tête (s) : 0 = brut ; ~0,2 = comme un gimbal (la marche à la rame saccade).")]
        public float followSmoothing = 0f;
        [Tooltip("Stabilisation de l'orientation (s), plus courte : on garde la nervosité des regards.")]
        public float followTurnSmoothing = 0f;
        private bool _snap;

        /// <summary>Travelling de (from, regard fromLook) à (to, regard toLook) en « seconds » secondes de jeu.</summary>
        public void Glide(Vector3 from, Vector3 fromLook, Vector3 to, Vector3 toLook, float seconds)
        {
            _follow = false; StopGlide();
            _glide = StartCoroutine(GlideCo(from, fromLook, to, toLook, seconds));
        }

        /// <summary>Caméra fixe pendant « seconds » secondes.</summary>
        public void Hold(Vector3 at, Vector3 look, float seconds) => Glide(at, look, at, look, seconds);

        /// <summary>Attendre « seconds » secondes de jeu sans bouger (utile en FollowHead).</summary>
        public void Wait(float seconds) { StopGlide(); _glide = StartCoroutine(WaitCo(seconds)); }

        private void StopGlide()
        {
            if (_glide != null) StopCoroutine(_glide);
            _glide = null;
            _skip = 1;                                        // la 1re image d'un plan peut garder l'ancien cadrage
            if (_cam != null && !_follow) { _cam.cullingMask = _mask; _cam.clearFlags = _clear; _cam.backgroundColor = _bg; }
        }

        private IEnumerator WaitCo(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime) yield return null;
            _glide = null;
        }

        private IEnumerator GlideCo(Vector3 a, Vector3 al, Vector3 b, Vector3 bl, float seconds)
        {
            for (float t = 0f; t <= seconds; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, seconds <= 0f ? 1f : t / seconds);
                Vector3 p = Vector3.Lerp(a, b, k), l = Vector3.Lerp(al, bl, k);
                // petit balancement de tête : ça fait « filmé par quelqu'un », pas « caméra de surveillance »
                p += new Vector3(Mathf.Sin(t * 1.7f), Mathf.Sin(t * 2.3f) * 0.6f, 0f) * 0.012f;
                transform.SetPositionAndRotation(p, Quaternion.LookRotation(l - p, Vector3.up));
                yield return null;
            }
            _glide = null;
        }

        private void LateUpdate()
        {
            if (!_follow || source == null) return;
            Vector3 p = source.transform.position; Quaternion r = source.transform.rotation;
            if (!_snap)
            {
                if (followSmoothing > 0f)
                {
                    Vector3 sm = Vector3.Lerp(transform.position, p, 1f - Mathf.Exp(-Time.deltaTime / followSmoothing));
                    // jamais plus de 5 cm derrière la tête : sinon la caméra voit l'intérieur du corps du joueur
                    p += Vector3.ClampMagnitude(sm - p, 0.05f);
                }
                if (followTurnSmoothing > 0f) r = Quaternion.Slerp(transform.rotation, r, 1f - Mathf.Exp(-Time.deltaTime / followTurnSmoothing));
            }
            _snap = false;
            transform.SetPositionAndRotation(p, r);
            _cam.cullingMask = source.cullingMask;          // le screamer ne montre que le monstre, sur fond noir
            _cam.clearFlags = source.clearFlags;
            _cam.backgroundColor = source.backgroundColor;
        }

        private IEnumerator Capture()
        {
            var end = new WaitForEndOfFrame();
            while (true)
            {
                yield return end;
                if (_glide == null) continue;                    // on ne filme que pendant un plan
                if (_skip > 0) { _skip--; continue; }
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = _rt;
                _tex.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                RenderTexture.active = prev;
                File.WriteAllBytes(Path.Combine(folder, $"f{Frame:D5}.jpg"), _tex.EncodeToJPG(92));
                Frame++;
            }
        }
    }
}
