using Horror.Monsters;
using UnityEngine;

namespace Horror.World
{
    /// <summary>
    /// Les lumières grésillent quand L'Écoute passe près d'elles (plus fort quand elle chasse).
    /// C'est un avertissement : si la lampe au bout du couloir se met à clignoter, elle arrive.
    /// De temps en temps, une lampe grésille aussi toute seule (fausse alerte).
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class LightFlicker : MonoBehaviour
    {
        [Tooltip("Distance (m) à laquelle un monstre fait grésiller la lumière.")]
        public float radius = 7f;
        [Tooltip("Chance par seconde de grésiller sans raison.")]
        public float randomChancePerSecond = 0.004f;

        private Light _light;
        private float _base;
        private float _nextFlick;
        private float _nextScan;
        private float _near;            // 0 = rien, 1 = monstre collé à la lampe
        private float _randomUntil;

        private void Awake()
        {
            _light = GetComponent<Light>();
            _base = _light.intensity;
        }

        private void Update()
        {
            if (Time.time >= _nextScan)
            {
                _nextScan = Time.time + 0.2f;
                _near = 0f;
                foreach (BlindMonsterAI m in BlindMonsterAI.All)
                {
                    if (m == null) continue;
                    float d = Vector3.Distance(m.transform.position, transform.position);
                    float k = Mathf.Clamp01(1f - d / radius) * (m.state == BlindMonsterAI.State.Chasse ? 1f : 0.6f);
                    _near = Mathf.Max(_near, k);
                }
                if (_near <= 0f && Random.value < randomChancePerSecond * 0.2f) _randomUntil = Time.time + Random.Range(0.4f, 1.5f);
            }

            float level = Mathf.Max(_near, Time.time < _randomUntil ? 0.35f : 0f);
            if (level <= 0f)
            {
                _light.intensity = Mathf.MoveTowards(_light.intensity, _base, Time.deltaTime * _base * 4f);
                return;
            }
            if (Time.time < _nextFlick) return;
            _nextFlick = Time.time + Random.Range(0.03f, 0.14f);
            bool off = Random.value < 0.25f + 0.5f * level;
            _light.intensity = _base * (off ? Random.Range(0f, 0.15f) : Random.Range(0.55f, 1.05f));
        }
    }
}
