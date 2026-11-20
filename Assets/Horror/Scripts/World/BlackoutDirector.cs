using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Horror.Monsters;

namespace Horror.World
{
    /// <summary>
    /// Coupures de courant : de temps en temps, les lumières de l'Institut grésillent puis s'éteignent
    /// quelques secondes (la Salle de jeux garde sa lumière : c'est la zone sûre). Pendant le noir,
    /// L'Écoute entend mieux. Le retour du courant fait un « clac » qui... fait du bruit.
    ///
    /// Pas de minuterie visible : la coupure arrive quand on ne s'y attend pas. Pour tester :
    /// clic droit sur le composant > « Couper maintenant » (en Play).
    /// </summary>
    public class BlackoutDirector : MonoBehaviour
    {
        [Tooltip("Attente entre deux coupures (secondes, tirée au hasard entre les deux valeurs).")]
        public Vector2 delayRange = new Vector2(150f, 320f);
        [Tooltip("Durée du noir (secondes).")]
        public Vector2 durationRange = new Vector2(6f, 11f);
        [Tooltip("L'ouïe de L'Écoute est multipliée par ce facteur pendant le noir.")]
        public float hearingBoost = 1.6f;
        [Tooltip("Rayon du bruit fait par le retour du courant (m) : le « clac » des disjoncteurs.")]
        public float powerUpNoise = 9f;
        public AudioClip powerDown;
        public AudioClip powerUp;

        /// <summary>Vrai pendant une coupure.</summary>
        public static bool IsDark { get; private set; }

        private readonly List<Light> _lights = new List<Light>();
        private bool _running;

        private IEnumerator Start()
        {
            foreach (Light l in FindObjectsByType<Light>(FindObjectsInactive.Exclude))
                if (l.type != LightType.Directional && !SafeZone.Contains(l.transform.position)) _lights.Add(l);
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(delayRange.x, delayRange.y));
                yield return Blackout();
            }
        }

        [ContextMenu("Couper maintenant")]
        public void TriggerNow()
        {
            if (!_running) StartCoroutine(Blackout());
        }

        private IEnumerator Blackout()
        {
            if (_running) yield break;
            _running = true;
            PlayAtListener(powerDown, 0.8f);

            // 1) ça grésille de plus en plus
            for (float t = 0f; t < 1.4f; t += 0.06f)
            {
                foreach (Light l in _lights)
                    if (l != null) l.enabled = Random.value > t * 0.6f;
                yield return new WaitForSeconds(Random.Range(0.03f, 0.09f));
            }

            // 2) le noir ; elle entend mieux
            IsDark = true;
            foreach (Light l in _lights) if (l != null) l.enabled = false;
            var boosted = new List<BlindMonsterAI>(BlindMonsterAI.All);
            foreach (BlindMonsterAI m in boosted) if (m != null) m.hearingMultiplier *= hearingBoost;
            yield return new WaitForSeconds(Random.Range(durationRange.x, durationRange.y));

            // 3) le courant revient d'un coup (et ça s'entend)
            foreach (BlindMonsterAI m in boosted) if (m != null) m.hearingMultiplier /= hearingBoost;
            IsDark = false;
            PlayAtListener(powerUp, 0.9f);
            Light first = null;
            foreach (Light l in _lights)
                if (l != null)
                {
                    l.enabled = true;
                    if (first == null) first = l;
                }
            if (first != null)   // un seul « clac » (au premier disjoncteur) que L'Écoute peut entendre
                Horror.Noise.NoiseSystem.Emit(first.transform.position, powerUpNoise * 0.35f, Horror.Noise.NoiseType.Objet, first.gameObject);
            _running = false;
        }

        private static void PlayAtListener(AudioClip clip, float volume)
        {
            if (clip == null) return;
            AudioListener listener = FindAnyObjectByType<AudioListener>();
            AudioSource.PlayClipAtPoint(clip, listener != null ? listener.transform.position : Vector3.zero, volume);
        }
    }
}
