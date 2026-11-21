using System.Collections.Generic;
using UnityEngine;

namespace Horror.Net
{
    /// <summary>
    /// En ligne, les monstres ne pensent qu'à UN endroit : chez celui qui fait tourner l'IA (NetworkBootstrap.RunsAI,
    /// le « master client » en mode partagé). Lui envoie la position et l'orientation de chaque monstre ~10 fois par
    /// seconde ; chez les autres, l'IA est éteinte et le monstre suit ces messages (lissé). La démarche
    /// (GaitAnimator) s'anime d'elle-même puisqu'elle lit le déplacement.
    /// Si celui qui faisait tourner l'IA part, Photon en désigne un autre : chez lui, l'IA se rallume.
    /// Limite actuelle : l'IA ne poursuit que le joueur de la machine où elle tourne (voir Docs/MULTIJOUEUR.md).
    /// </summary>
    public class MonsterSync : MonoBehaviour
    {
        public float sendRate = 10f;
        public float smoothing = 10f;

        private static readonly string[] AiTypes = { "BlindMonsterAI", "DoctorAI", "SoftieWatcher", "SheetMonsterAI" };
        private readonly List<MonoBehaviour> _ais = new List<MonoBehaviour>();
        private readonly Dictionary<int, MonsterState> _targets = new Dictionary<int, MonsterState>();
        private INetworkSession _session;
        private bool _aiOff;
        private static MonsterSync _instance;

        /// <summary>Numéro d'un monstre (le même sur toutes les machines), -1 si inconnu.</summary>
        public static int IndexOf(MonoBehaviour monster) => _instance != null ? _instance._ais.IndexOf(monster) : -1;
        /// <summary>Le monstre portant ce numéro, ou null.</summary>
        public static MonoBehaviour At(int index) => _instance != null && index >= 0 && index < _instance._ais.Count ? _instance._ais[index] : null;
        private float _nextSend;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (FindAnyObjectByType<MonsterSync>() == null) new GameObject("Synchro des monstres (réseau)").AddComponent<MonsterSync>();
        }

        private void Start()
        {
            _instance = this;
            // ordre STABLE (le même sur toutes les machines) : par chemin dans la hiérarchie
            foreach (MonoBehaviour mb in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
                if (System.Array.IndexOf(AiTypes, mb.GetType().Name) >= 0) _ais.Add(mb);
            _ais.Sort((a, b) => string.CompareOrdinal(Path(a.transform), Path(b.transform)));
        }

        private static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name + "#" + t.GetSiblingIndex();

        private void OnDestroy() { if (_session != null) _session.MonsterReceived -= OnState; }

        private void Update()
        {
            INetworkSession s = NetworkBootstrap.Session;
            if (s != _session)
            {
                if (_session != null) _session.MonsterReceived -= OnState;
                _session = s;
                if (_session != null) _session.MonsterReceived += OnState;
            }
            bool online = s != null && s.IsOnline;
            SetAi(!online || s.IsHost);
            if (!online) return;
            if (s.IsHost)
            {
                if (Time.unscaledTime < _nextSend) return;
                _nextSend = Time.unscaledTime + 1f / Mathf.Max(1f, sendRate);
                for (int i = 0; i < _ais.Count; i++)
                {
                    if (_ais[i] == null) continue;
                    Transform t = _ais[i].transform;
                    s.SendMonster(new MonsterState { monsterId = i, position = t.position, yaw = t.eulerAngles.y, targetPlayerId = -1 });
                }
                return;
            }
            float k = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
            foreach (var kv in _targets)
            {
                if (kv.Key < 0 || kv.Key >= _ais.Count || _ais[kv.Key] == null) continue;
                Transform t = _ais[kv.Key].transform;
                t.position = Vector3.Lerp(t.position, kv.Value.position, k);
                t.rotation = Quaternion.Slerp(t.rotation, Quaternion.Euler(0f, kv.Value.yaw, 0f), k);
            }
        }

        private void OnState(MonsterState m) => _targets[m.monsterId] = m;

        private void SetAi(bool on)
        {
            if (_aiOff == !on) return;
            _aiOff = !on;
            foreach (MonoBehaviour ai in _ais) if (ai != null) ai.enabled = on;
            if (!on) _targets.Clear();
        }
    }
}
