using System.Collections.Generic;
using UnityEngine;
using Horror.Locomotion;
using Horror.Noise;
using Horror.Player;

namespace Horror.Net
{
    /// <summary>
    /// En ligne : envoie MA pose (tête, mains, bouche) ~15 fois par seconde, et affiche les AUTRES joueurs.
    /// Chaque joueur distant est une copie de ma poupée (Le Dessiné) pilotée par sa pose reçue, à sa couleur,
    /// avec son pseudo au-dessus de la tête. Sa voix (Photon Voice) est collée sur sa tête : on l'entend en 3D,
    /// étouffée quand un mur est entre nous. Un joueur bloqué n'est ni affiché ni entendu ; un joueur coupé
    /// reste visible mais muet. Créé tout seul au chargement de la scène ; ne fait rien en solo.
    /// </summary>
    public class NetworkAvatars : MonoBehaviour
    {
        [Tooltip("Envois de pose par seconde (le Quest est en Wi-Fi : 15 suffisent).")]
        public float sendRate = 15f;
        [Tooltip("Lissage des poses reçues (plus grand = plus réactif).")]
        public float smoothing = 14f;

        private class Remote
        {
            public int id;
            public GameObject root;
            public Transform head, left, right;
            public PlayerPose target;
            public bool hasPose;
            public MicMouth mouth;
            public DollColorizer colorizer;
            public int color = -1;
            public TextMesh tag;
            public AudioSource voice;
            public AudioLowPassFilter muffle;
        }

        private readonly Dictionary<int, Remote> _remotes = new Dictionary<int, Remote>();
        private INetworkSession _session;
        private ArmLocomotion _local;
        private MicMouth _localMouth;
        private DollAvatarRig _localDoll;
        private float _nextSend;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (FindAnyObjectByType<NetworkAvatars>() == null) new GameObject("Avatars des autres joueurs (réseau)").AddComponent<NetworkAvatars>();
        }

        private void OnDestroy() => Subscribe(null);

        private void Subscribe(INetworkSession s)
        {
            if (_session != null) { _session.PoseReceived -= OnPose; _session.PlayerLeft -= OnLeft; }
            _session = s;
            if (_session != null) { _session.PoseReceived += OnPose; _session.PlayerLeft += OnLeft; }
        }

        private void Update()
        {
            INetworkSession s = NetworkBootstrap.Session;
            if (s != _session) { Subscribe(s); ClearAll(); }
            if (s == null || !s.IsOnline) { if (_remotes.Count > 0) ClearAll(); return; }
            if (_local == null) FindLocal();
            if (_local != null && Time.unscaledTime >= _nextSend)
            {
                _nextSend = Time.unscaledTime + 1f / Mathf.Max(1f, sendRate);
                s.SendPose(LocalPose(s));
            }
            float k = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
            Transform listener = Camera.main != null ? Camera.main.transform : null;
            foreach (Remote r in _remotes.Values) Animate(s, r, k, listener);
        }

        private void FindLocal()
        {
            _local = FindAnyObjectByType<ArmLocomotion>();
            foreach (MicMouth m in FindObjectsByType<MicMouth>()) if (m.isLocal) { _localMouth = m; break; }
            foreach (DollAvatarRig d in FindObjectsByType<DollAvatarRig>()) if (d.hideOwnHead) { _localDoll = d; break; }
        }

        private PlayerPose LocalPose(INetworkSession s)
        {
            Transform h = _local.head, l = _local.leftHandVisual != null ? _local.leftHandVisual : _local.leftController;
            Transform rr = _local.rightHandVisual != null ? _local.rightHandVisual : _local.rightController;
            return new PlayerPose
            {
                playerId = s.LocalPlayerId,
                head = h.position, headRotation = h.rotation,
                leftHand = l != null ? l.position : h.position, leftRotation = l != null ? l.rotation : h.rotation,
                rightHand = rr != null ? rr.position : h.position, rightRotation = rr != null ? rr.rotation : h.rotation,
                mouth = (byte)Mathf.RoundToInt(Mathf.Clamp01(_localMouth != null ? _localMouth.Openness : 0f) * 255f),
            };
        }

        private void OnPose(PlayerPose p)
        {
            if (!_remotes.TryGetValue(p.playerId, out Remote r)) r = Create(p.playerId);
            if (r == null) return;
            r.target = p;
            if (!r.hasPose) { Snap(r); r.hasPose = true; }
        }

        private void OnLeft(int playerId)
        {
            if (!_remotes.TryGetValue(playerId, out Remote r)) return;
            if (r.voice != null) r.voice.transform.SetParent(null, true);   // la voix appartient à Photon : on la rend
            if (r.root != null) Destroy(r.root);
            _remotes.Remove(playerId);
        }

        private void ClearAll()
        {
            foreach (int id in new List<int>(_remotes.Keys)) OnLeft(id);
        }

        // ------------------------------------------------------------------ création d'un avatar

        private Remote Create(int id)
        {
            var r = new Remote { id = id };
            r.root = new GameObject("Joueur distant " + id);
            r.root.transform.SetParent(transform, false);
            r.head = new GameObject("Tête (cible)").transform; r.head.SetParent(r.root.transform, false);
            r.left = new GameObject("Main G (cible)").transform; r.left.SetParent(r.root.transform, false);
            r.right = new GameObject("Main D (cible)").transform; r.right.SetParent(r.root.transform, false);
            if (_localDoll == null) FindLocal();
            if (_localDoll != null) BuildDoll(r); else BuildSimple(r);
            var tagGo = new GameObject("Pseudo");
            tagGo.transform.SetParent(r.head, false);
            tagGo.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            r.tag = tagGo.AddComponent<TextMesh>();
            r.tag.anchor = TextAnchor.MiddleCenter; r.tag.alignment = TextAlignment.Center;
            r.tag.characterSize = 0.012f; r.tag.fontSize = 64; r.tag.color = new Color(0.98f, 0.93f, 0.82f);
            Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f != null) { r.tag.font = f; tagGo.GetComponent<MeshRenderer>().sharedMaterial = f.material; }
            _remotes[id] = r;
            return r;
        }

        /// <summary>Copie de ma propre poupée, débarrassée de ce qui ne sert qu'au joueur local.</summary>
        private void BuildDoll(Remote r)
        {
            var holder = new GameObject("copie (inactive)");
            holder.SetActive(false);                                      // les scripts de la copie ne démarrent pas encore
            GameObject doll = Instantiate(_localDoll.gameObject, holder.transform);
            doll.name = "Poupée";
            foreach (HandFingerAnimator h in doll.GetComponentsInChildren<HandFingerAnimator>(true)) DestroyImmediate(h);   // lit MES manettes
            foreach (Collider c in doll.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
            foreach (Renderer rd in doll.GetComponentsInChildren<Renderer>(true)) rd.enabled = true;   // ma poupée cache SA tête : pas celle-ci
            var rig = doll.GetComponent<DollAvatarRig>();
            if (rig != null) { rig.hideOwnHead = false; rig.leanSlide = null; rig.head = r.head; rig.leftHandTarget = r.left; rig.rightHandTarget = r.right; }
            r.colorizer = doll.GetComponent<DollColorizer>();
            if (r.colorizer != null) r.colorizer.isLocal = false;
            var mouthDummy = new GameObject("Bouche (réseau)").transform;
            mouthDummy.SetParent(doll.transform, false);
            r.mouth = doll.AddComponent<MicMouth>();                     // FaceScribble anime le visage d'après lui
            r.mouth.isLocal = false;
            r.mouth.mouth = mouthDummy;
            doll.transform.SetParent(r.root.transform, false);
            Destroy(holder);
        }

        /// <summary>Si ma poupée est introuvable : une tête et deux mains, mais le joueur est visible.</summary>
        private static void BuildSimple(Remote r)
        {
            void Ball(Transform parent, float size)
            {
                GameObject g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(g.GetComponent<Collider>());
                g.transform.SetParent(parent, false);
                g.transform.localScale = Vector3.one * size;
            }
            Ball(r.head, 0.22f); Ball(r.left, 0.09f); Ball(r.right, 0.09f);
        }

        private static void Snap(Remote r)
        {
            r.head.SetPositionAndRotation(r.target.head, r.target.headRotation);
            r.left.SetPositionAndRotation(r.target.leftHand, r.target.leftRotation);
            r.right.SetPositionAndRotation(r.target.rightHand, r.target.rightRotation);
        }

        // ------------------------------------------------------------------ chaque image

        private void Animate(INetworkSession s, Remote r, float k, Transform listener)
        {
            if (r.root == null) return;
            string uid = s.PlayerUserId(r.id);
            bool blocked = VoiceModeration.IsBlocked(uid);
            if (r.root.activeSelf == blocked) r.root.SetActive(!blocked);
            if (!r.hasPose) return;
            r.head.position = Vector3.Lerp(r.head.position, r.target.head, k);
            r.head.rotation = Quaternion.Slerp(r.head.rotation, r.target.headRotation, k);
            r.left.position = Vector3.Lerp(r.left.position, r.target.leftHand, k);
            r.left.rotation = Quaternion.Slerp(r.left.rotation, r.target.leftRotation, k);
            r.right.position = Vector3.Lerp(r.right.position, r.target.rightHand, k);
            r.right.rotation = Quaternion.Slerp(r.right.rotation, r.target.rightRotation, k);
            if (r.mouth != null) r.mouth.SetRemoteOpenness(r.target.mouth / 255f);
            int color = s.PlayerColorIndex(r.id);
            if (r.colorizer != null && color != r.color) { r.color = color; r.colorizer.SetColor(GameSettings.Palette[Mathf.Clamp(color, 0, GameSettings.Palette.Length - 1)]); }
            if (r.tag != null)
            {
                r.tag.text = s.PlayerName(r.id) + (VoiceModeration.IsMuted(uid) ? "  (MUTED)" : "");
                if (listener != null) r.tag.transform.rotation = Quaternion.LookRotation(r.tag.transform.position - listener.position);
            }
            Voice(r, uid, listener);
        }

        private static void Voice(Remote r, string uid, Transform listener)
        {
            if (r.voice == null)
            {
                r.voice = VoiceSpeakers.Get(uid);
                if (r.voice == null) return;
                r.voice.transform.SetParent(r.head, false);              // la voix sort de sa tête
                r.voice.transform.localPosition = Vector3.zero;
                r.voice.spatialBlend = 1f; r.voice.dopplerLevel = 0f;
                r.voice.rolloffMode = AudioRolloffMode.Linear; r.voice.minDistance = 1.5f; r.voice.maxDistance = 25f;
                r.muffle = r.voice.GetComponent<AudioLowPassFilter>() ?? r.voice.gameObject.AddComponent<AudioLowPassFilter>();
            }
            r.voice.volume = VoiceModeration.ShouldHear(uid) ? 1f : 0f;
            if (r.muffle != null && listener != null)
            {
                // un mur entre nous : la voix est étouffée (comme pour les monstres)
                bool wall = Physics.Linecast(listener.position, r.head.position, out RaycastHit hit, NoiseSystem.occluderLayers, QueryTriggerInteraction.Ignore)
                            && !hit.transform.IsChildOf(r.root.transform);
                r.muffle.cutoffFrequency = Mathf.Lerp(r.muffle.cutoffFrequency, wall ? 1200f : 22000f, 0.2f);
            }
        }
    }
}
