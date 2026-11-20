using System.Collections.Generic;
using UnityEngine;

namespace Horror.Monsters
{
    /// <summary>
    /// Animation procédurale de L'Écoute (pas besoin d'os ni de fichiers d'animation) :
    /// on fait bouger directement les pièces du modèle (Buste, Epaule, Coude, Doigt, Trousseau, Cable).
    ///  - ERRANCE  : marche lente, buste qui tangue, bras qui ballottent ;
    ///  - ÉCOUTE   : figée, les bras se lèvent un peu, les doigts se crispent ;
    ///  - ENQUÊTE  : elle tâtonne, bras tendus devant elle qui balaient de gauche à droite ;
    ///  - CHASSE   : penchée en avant, bras tendus, doigts qui griffent l'air, foulées rapides.
    /// Astuce d'horreur : la pose n'est mise à jour que 12 fois par seconde (comme de l'image par image),
    /// ce qui donne un mouvement saccadé et dérangeant. Le corps, lui, glisse en continu.
    /// </summary>
    [DefaultExecutionOrder(100)]   // après BlindMonsterAI (qui tourne la tête)
    [RequireComponent(typeof(BlindMonsterAI))]
    public class MonsterAnimator : MonoBehaviour
    {
        [Tooltip("Images par seconde de la pose (12 = saccadé et inquiétant, 60 = fluide).")]
        public float poseRate = 12f;
        public float chasePoseRate = 18f;
        [Tooltip("Longueur d'une foulée (m) pour caler le balancement sur la marche.")]
        public float stride = 1.15f;
        [Tooltip("Petits tics nerveux de la tête.")]
        public bool headTwitches = true;

        private BlindMonsterAI _ai;
        private Transform _bust, _head, _keys;
        private Transform[] _shoulders = new Transform[2], _elbows = new Transform[2];
        private readonly List<Transform> _fingers = new List<Transform>();
        private readonly List<Transform> _cables = new List<Transform>();
        private readonly Dictionary<Transform, Quaternion> _base = new Dictionary<Transform, Quaternion>();
        private Vector3 _bustBasePos;

        private float _phase, _poseTimer, _lastSpeed;
        private Vector3 _lastPos;
        private Quaternion _twitch = Quaternion.identity;
        private float _nextTwitch;
        private float _reach;   // 0 = bras ballants, 1 = bras tendus devant
        private float[] _cableVel = new float[0];
        private float[] _cableAngle = new float[0];

        private void Awake()
        {
            _ai = GetComponent<BlindMonsterAI>();
            _bust = FindDeep("Buste");
            _head = _ai.head != null ? _ai.head : FindDeep("Tete");
            _keys = FindDeep("Trousseau");
            string[] sides = { "G", "D" };
            for (int i = 0; i < 2; i++)
            {
                _shoulders[i] = FindDeep("Epaule_" + sides[i]);
                _elbows[i] = FindDeep("Coude_" + sides[i]);
            }
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("Doigt_") && !t.name.EndsWith("_Mesh") && !t.name.Contains("Jointure") && !t.name.Contains("Ongle"))
                    _fingers.Add(t);
                else if (t.name.StartsWith("Cable_") && !t.name.EndsWith("_Mesh"))
                    _cables.Add(t);
            }
            foreach (Transform t in Animated()) if (t != null) _base[t] = t.localRotation;
            if (_bust != null) _bustBasePos = _bust.localPosition;
            _cableVel = new float[_cables.Count];
            _cableAngle = new float[_cables.Count];
            _lastPos = transform.position;
        }

        private IEnumerable<Transform> Animated()
        {
            yield return _bust; yield return _keys;
            foreach (var t in _shoulders) yield return t;
            foreach (var t in _elbows) yield return t;
            foreach (var t in _fingers) yield return t;
            foreach (var t in _cables) yield return t;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            Vector3 move = transform.position - _lastPos;
            move.y = 0f;
            _lastPos = transform.position;
            float speed = dt > 0f ? move.magnitude / dt : 0f;
            _lastSpeed = Mathf.Lerp(_lastSpeed, speed, 1f - Mathf.Exp(-8f * dt));
            _phase += move.magnitude / stride * Mathf.PI;   // un demi-tour de phase par foulée

            bool chase = _ai.state == BlindMonsterAI.State.Chasse;
            float targetReach = chase ? 1f : _ai.state == BlindMonsterAI.State.Enquete ? 0.75f : _ai.state == BlindMonsterAI.State.Ecoute ? 0.35f : 0f;
            _reach = Mathf.MoveTowards(_reach, targetReach, dt * 2.5f);

            // Câbles : petit pendule continu (pas saccadé, sinon ça fait faux)
            for (int i = 0; i < _cables.Count; i++)
            {
                float force = -_cableAngle[i] * 40f - _cableVel[i] * 4f + _lastSpeed * Mathf.Sin(_phase * 2f + i) * 2f;
                _cableVel[i] += force * dt;
                _cableAngle[i] += _cableVel[i] * dt;
                if (_base.TryGetValue(_cables[i], out Quaternion b))
                    _cables[i].localRotation = b * Quaternion.Euler(_cableAngle[i] * 25f, 0f, _cableAngle[i] * 10f);
            }

            // Pose saccadée
            _poseTimer += dt;
            float rate = chase ? chasePoseRate : poseRate;
            if (_poseTimer < 1f / rate) { ApplyTwitch(dt); return; }
            _poseTimer = 0f;

            float run = Mathf.Clamp01(_lastSpeed / Mathf.Max(0.1f, _ai.chaseSpeed));
            float walk = Mathf.Clamp01(_lastSpeed / Mathf.Max(0.1f, _ai.patrolSpeed));
            float s = Mathf.Sin(_phase), c = Mathf.Cos(_phase);

            if (_bust != null)
            {
                float lean = 6f * walk + 16f * run;
                Rot(_bust, lean + Mathf.Abs(s) * 3f * walk, 0f, s * 4f * walk);
                _bust.localPosition = _bustBasePos + Vector3.up * (Mathf.Abs(c) * 0.03f * walk);
            }

            float t = Time.time;
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? 1f : -1f;
                // Bras ballants : balancier opposé aux jambes
                float swing = s * side * (18f * walk + 10f * run);
                // Tâtonnement : les bras tendus balaient lentement
                float grope = Mathf.Sin(t * 1.3f + i * 1.7f) * 22f * (_ai.state == BlindMonsterAI.State.Enquete ? 1f : 0.3f);
                float pitch = Mathf.Lerp(swing, -42f + grope * 0.4f, _reach);
                float yaw = Mathf.Lerp(0f, -grope * side * 0.6f, _reach);
                // bras tendus vers l'avant ET écartés (sinon ils se croisent devant le visage)
                float spread = side * (22f + 10f * run) * _reach;
                Rot(_shoulders[i], pitch, yaw, spread);
                Rot(_elbows[i], Mathf.Lerp(-6f - Mathf.Abs(s) * 8f, -18f + Mathf.Sin(t * 7f + i) * 6f * run, _reach), 0f, 0f);
            }

            // Doigts : se crispent quand elle écoute, griffent l'air quand elle chasse
            float clench = _ai.state == BlindMonsterAI.State.Ecoute ? 0.7f : 0f;
            for (int k = 0; k < _fingers.Count; k++)
            {
                float claw = Mathf.Sin(t * (chase ? 14f : 3f) + k * 1.3f) * (chase ? 22f : 8f);
                Rot(_fingers[k], -clench * 40f + claw, 0f, 0f);
            }

            if (_keys != null) Rot(_keys, s * 25f * (walk + run), 0f, c * 10f * (walk + run));
            ApplyTwitch(dt);
        }

        private void ApplyTwitch(float dt)
        {
            if (!headTwitches || _head == null) return;
            if (Time.time > _nextTwitch)
            {
                _nextTwitch = Time.time + Random.Range(0.8f, 3.5f);
                _twitch = Quaternion.Euler(Random.Range(-7f, 7f), Random.Range(-10f, 10f), Random.Range(-9f, 9f));
            }
            _twitch = Quaternion.Slerp(_twitch, Quaternion.identity, 1f - Mathf.Exp(-5f * dt));
            _head.localRotation = _head.localRotation * _twitch;
        }

        private void Rot(Transform t, float x, float y, float z)
        {
            if (t == null || !_base.TryGetValue(t, out Quaternion b)) return;
            t.localRotation = b * Quaternion.Euler(x, y, z);
        }

        private Transform FindDeep(string n)
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
                if (t.name == n) return t;
            return null;
        }
    }
}
