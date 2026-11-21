using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Horror.Interaction;
using Horror.Net;

namespace Horror.World
{
    /// <summary>
    /// Une ampoule nue du grenier, avec son cordon. Allumée, son halo est une zone que La Veilleuse ne peut pas
    /// franchir. Elle peut la « souffler » avec son éteignoir : l'ampoule GRÉSILLE 1,5 s (l'avertissement), puis
    /// s'éteint. Tirer le cordon (la toucher avec la main) la rallume.
    /// En ligne, chaque changement est envoyé à tous (événement Ampoule) : tout le monde voit les mêmes ampoules.
    /// </summary>
    public class AtticBulb : MonoBehaviour
    {
        public static readonly List<AtticBulb> All = new List<AtticBulb>();

        public Light bulbLight;
        public Renderer glass;
        public PressButton cord;
        [Tooltip("Rayon du halo protecteur (m, au sol).")]
        public float radius = 3.2f;
        [Tooltip("Allumée au début de la partie (la même chose sur toutes les machines).")]
        public bool startLit = true;
        [Tooltip("Faux = elle ne peut pas la souffler (l'ampoule du refuge, dans la cage d'arrivée).")]
        public bool snuffable = true;
        public AudioClip snuffSound, warnSound, relightSound, humLoop;

        public bool IsLit { get; private set; } = true;
        public bool Flickering { get; private set; }

        private AudioSource _hum;
        private float _baseIntensity, _relightLock;
        private Material _glassMat;
        private Color _emit;

        private void OnEnable()
        {
            if (!All.Contains(this)) All.Add(this);
            if (cord != null) cord.onPressed.AddListener(OnCord);
        }

        private void OnDisable()
        {
            All.Remove(this);
            if (cord != null) cord.onPressed.RemoveListener(OnCord);
        }

        private void Start()
        {
            if (bulbLight != null) _baseIntensity = bulbLight.intensity;
            if (glass != null)
            {
                _glassMat = glass.material;
                _emit = _glassMat.HasProperty("_EmissionColor") ? _glassMat.GetColor("_EmissionColor") : Color.white;
            }
            if (humLoop != null)
            {
                _hum = gameObject.AddComponent<AudioSource>();
                _hum.clip = humLoop; _hum.loop = true; _hum.spatialBlend = 1f; _hum.volume = 0.25f;
                _hum.rolloffMode = AudioRolloffMode.Linear; _hum.maxDistance = 6f;
                _hum.Play();
            }
            if (!startLit) SetLit(false, false);
        }

        /// <summary>Vrai si ce point est dans le halo d'une ampoule allumée (au sol, sans tenir compte des étages).</summary>
        public bool Covers(Vector3 p)
        {
            if (!IsLit) return false;
            Vector3 d = p - transform.position;
            if (d.y > 1f || d.y < -4.5f) return false;
            d.y = 0f;
            return d.sqrMagnitude < radius * radius;
        }

        public static bool IsLitAt(Vector3 p)
        {
            foreach (AtticBulb b in All) if (b != null && b.Covers(p)) return true;
            return false;
        }

        /// <summary>L'ampoule allumée qui couvre ce point (la plus proche), ou null.</summary>
        public static AtticBulb CoveringBulb(Vector3 p)
        {
            AtticBulb best = null; float bd = float.MaxValue;
            foreach (AtticBulb b in All)
            {
                if (b == null || !b.Covers(p)) continue;
                float d = (b.transform.position - p).sqrMagnitude;
                if (d < bd) { bd = d; best = b; }
            }
            return best;
        }

        /// <summary>La Veilleuse souffle l'ampoule : grésillement d'avertissement, puis noir.</summary>
        public void Snuff()
        {
            if (!IsLit || Flickering || !snuffable) return;
            StartCoroutine(SnuffRoutine());
        }

        private IEnumerator SnuffRoutine()
        {
            Flickering = true;
            if (warnSound != null) AudioSource.PlayClipAtPoint(warnSound, transform.position, 0.8f);
            for (float t = 0f; t < 1.5f; t += Time.deltaTime)
            {
                float on = Random.value > 0.35f ? 1f : 0.05f;
                if (bulbLight != null) bulbLight.intensity = _baseIntensity * on;
                yield return null;
            }
            Flickering = false;
            SetLit(false, true);
            if (snuffSound != null) AudioSource.PlayClipAtPoint(snuffSound, transform.position, 0.9f);
            _relightLock = Time.time + 0.8f;                             // pas de rallumage instantané
        }

        private void OnCord()
        {
            if (IsLit || Time.time < _relightLock) return;
            SetLit(true, true);
            if (relightSound != null) AudioSource.PlayClipAtPoint(relightSound, transform.position, 0.8f);
        }

        public void SetLit(bool lit, bool broadcast)
        {
            IsLit = lit;
            if (bulbLight != null) { bulbLight.enabled = lit; bulbLight.intensity = _baseIntensity; }
            if (_glassMat != null && _glassMat.HasProperty("_EmissionColor")) _glassMat.SetColor("_EmissionColor", lit ? _emit : Color.black);
            if (_hum != null) _hum.volume = lit ? 0.25f : 0f;
            INetworkSession s = NetworkBootstrap.Session;
            if (broadcast && s != null && s.IsOnline)
                s.SendEvent(new GameEventMessage { kind = GameEventMessage.Kind.Ampoule, value = Index, playerId = lit ? 1 : 0, position = transform.position });
        }

        /// <summary>Numéro stable (le même sur toutes les machines) : rang par position.</summary>
        public int Index
        {
            get
            {
                List<AtticBulb> sorted = Sorted();
                return sorted.IndexOf(this);
            }
        }

        private static List<AtticBulb> Sorted()
        {
            var l = new List<AtticBulb>(All);
            l.Sort((a, b) =>
            {
                Vector3 pa = a.transform.position, pb = b.transform.position;
                int c = Mathf.RoundToInt(pa.x * 10f).CompareTo(Mathf.RoundToInt(pb.x * 10f));
                return c != 0 ? c : Mathf.RoundToInt(pa.z * 10f).CompareTo(Mathf.RoundToInt(pb.z * 10f));
            });
            return l;
        }

        /// <summary>Un autre joueur a changé cette ampoule (réseau).</summary>
        public static void ApplyRemote(int index, bool lit)
        {
            List<AtticBulb> sorted = Sorted();
            if (index < 0 || index >= sorted.Count) return;
            AtticBulb b = sorted[index];
            if (b.IsLit == lit) return;
            b.StopAllCoroutines(); b.Flickering = false;
            b.SetLit(lit, false);
            AudioClip c = lit ? b.relightSound : b.snuffSound;
            if (c != null) AudioSource.PlayClipAtPoint(c, b.transform.position, 0.8f);
        }
    }
}
