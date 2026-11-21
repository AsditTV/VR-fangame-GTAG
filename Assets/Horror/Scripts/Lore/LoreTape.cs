using System.Collections.Generic;
using UnityEngine;
using Horror.Interaction;

namespace Horror.Lore
{
    /// <summary>
    /// Un magnétophone à cassette des années 70, oublié quelque part. Appuyer sur PLAY : la bande se lit (son 3D qui
    /// sort de l'appareil), les sous-titres s'affichent au-dessus. Ré-appuyer : stop. Une autre façon de découvrir le
    /// lore que les papiers : on ENTEND les gens de l'Institut (la Veilleuse, une patiente, le Docteur).
    /// Les cassettes écoutées sont retenues (PlayerPrefs) : TapesHeard / TapesTotal sur le tableau des objectifs.
    /// </summary>
    public class LoreTape : MonoBehaviour
    {
        public static readonly List<LoreTape> All = new List<LoreTape>();

        public string id = "cassette_1";
        public string label = "TAPE 1";
        public AudioClip clip;
        public PressButton playButton;
        public TextMesh subtitle;
        [TextArea] public string[] lines;
        public AudioClip clickSound;
        [Tooltip("Secondes de silence au début de la bande (avant la voix).")]
        public float leadIn = 0.55f;

        private AudioSource _src;
        private int _shown = -1;

        public static int TapesTotal => All.Count;
        public static int TapesHeard
        {
            get { int n = 0; foreach (LoreTape t in All) if (t != null && PlayerPrefs.GetInt("horror.tape." + t.id, 0) == 1) n++; return n; }
        }

        private void OnEnable()
        {
            if (!All.Contains(this)) All.Add(this);
            if (playButton != null) playButton.onPressed.AddListener(Toggle);
        }

        private void OnDisable()
        {
            All.Remove(this);
            if (playButton != null) playButton.onPressed.RemoveListener(Toggle);
        }

        private void Start()
        {
            _src = gameObject.AddComponent<AudioSource>();
            _src.clip = clip; _src.spatialBlend = 1f; _src.minDistance = 1.5f; _src.maxDistance = 12f;
            _src.rolloffMode = AudioRolloffMode.Linear; _src.playOnAwake = false;
            if (subtitle != null) subtitle.text = label + "\n[PLAY]";
        }

        public void Toggle()
        {
            if (clickSound != null) AudioSource.PlayClipAtPoint(clickSound, transform.position, 0.6f);
            if (_src == null || clip == null) return;
            if (_src.isPlaying) { _src.Stop(); if (subtitle != null) subtitle.text = label + "\n[PLAY]"; _shown = -1; return; }
            _src.Play();
            PlayerPrefs.SetInt("horror.tape." + id, 1); PlayerPrefs.Save();
        }

        private void Update()
        {
            if (_src == null || !_src.isPlaying || subtitle == null || lines == null || lines.Length == 0) return;
            // sous-titres : chaque ligne dure en proportion de sa longueur (la voix parle à vitesse régulière)
            float speech = Mathf.Max(0.1f, clip.length - 2f * leadIn), t = _src.time - leadIn;
            int total = 0; foreach (string l in lines) total += l.Length;
            int acc = 0, idx = lines.Length - 1;
            for (int i = 0; i < lines.Length; i++)
            {
                acc += lines[i].Length;
                if (t < speech * acc / total) { idx = i; break; }
            }
            if (t < 0f) idx = -1;
            if (idx != _shown) { _shown = idx; subtitle.text = idx < 0 ? label : "\"" + lines[idx] + "\""; }
        }
    }
}
