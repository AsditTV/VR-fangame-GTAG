using UnityEngine;

namespace Horror.Monsters
{
    /// <summary>
    /// Démarche procédurale pour n'importe quel monstre (sans os ni fichier d'animation) : marche et course.
    /// Le corps entier est rangé sous un pivot « Corps (animé) » qu'on fait rebondir, rouler et se pencher, sans
    /// toucher à la racine que l'IA déplace. Les épaules (Epaule_G/D), si elles existent, balancent à la marche et se
    /// tendent vers l'avant à la course (il veut t'attraper).
    ///  - MARCHE : petit rebond à chaque pas, roulis de côté (le Doux se dandine comme une peluche), bras ballants ;
    ///  - COURSE : grosse inclinaison en avant, rebond fort, bras tendus qui fouettent l'air.
    /// Comme L'Écoute, la pose n'est mise à jour que ~14 à 20 fois par seconde (image par image) : c'est voulu,
    /// le mouvement saccadé est plus dérangeant qu'un mouvement fluide. Le corps, lui, glisse en continu.
    /// </summary>
    public class GaitAnimator : MonoBehaviour
    {
        [Header("Vitesses de référence (m/s)")]
        public float walkSpeed = 1.2f;
        public float runSpeed = 5f;
        [Tooltip("Longueur d'un pas (m) : règle la cadence.")]
        public float stride = 1.1f;

        [Header("Allure")]
        public float bobWalk = 0.05f, bobRun = 0.14f;
        public float rollWalk = 4f, rollRun = 7f;
        public float leanWalk = 4f, leanRun = 20f;
        public float armSwing = 22f, armReachRun = 70f;
        [Tooltip("Poses par seconde (marche / course). 0 = fluide.")]
        public float poseRate = 14f, runPoseRate = 20f;

        private Transform _pivot, _armL, _armR;
        private Quaternion _armLBase, _armRBase;
        private Vector3 _lastPos;
        private float _speed, _phase, _poseTimer;

        public float Speed => _speed;

        private void Awake()
        {
            _pivot = new GameObject("Corps (animé)").transform;
            _pivot.SetParent(transform, false);
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform c = transform.GetChild(i);
                if (c != _pivot) c.SetParent(_pivot, true);
            }
            _armL = Find("Epaule_G"); _armR = Find("Epaule_D");
            if (_armL != null) _armLBase = _armL.localRotation;
            if (_armR != null) _armRBase = _armR.localRotation;
            _lastPos = transform.position;
        }

        private Transform Find(string n)
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true)) if (t.name == n) return t;
            return null;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Vector3 move = transform.position - _lastPos; move.y = 0f;
            _lastPos = transform.position;
            _speed = Mathf.Lerp(_speed, move.magnitude / dt, 1f - Mathf.Exp(-8f * dt));
            _phase += move.magnitude / Mathf.Max(0.1f, stride) * Mathf.PI;

            float walk = Mathf.Clamp01(_speed / Mathf.Max(0.1f, walkSpeed));
            float run = Mathf.Clamp01((_speed - walkSpeed) / Mathf.Max(0.1f, runSpeed - walkSpeed));
            float rate = Mathf.Lerp(poseRate, runPoseRate, run);
            _poseTimer += dt;
            if (rate > 0f && _poseTimer < 1f / rate) return;
            _poseTimer = 0f;

            float s = Mathf.Sin(_phase), b = Mathf.Abs(Mathf.Cos(_phase));
            float bob = Mathf.Lerp(bobWalk * walk, bobRun, run) * b;
            float roll = Mathf.Lerp(rollWalk * walk, rollRun, run) * s;
            float lean = Mathf.Lerp(leanWalk * walk, leanRun, run);
            _pivot.localPosition = Vector3.up * bob;
            _pivot.localRotation = Quaternion.Euler(lean + b * 2f * run, 0f, roll);

            // bras : ballants à la marche, tendus vers toi à la course (et qui fouettent)
            float swing = s * armSwing * walk * (1f - run);
            float reach = -armReachRun * run;
            float whip = Mathf.Sin(_phase * 2f) * 14f * run;
            if (_armL != null) _armL.localRotation = _armLBase * Quaternion.Euler(swing + reach + whip, 0f, -12f * run);
            if (_armR != null) _armR.localRotation = _armRBase * Quaternion.Euler(-swing + reach - whip, 0f, 12f * run);
        }
    }
}
