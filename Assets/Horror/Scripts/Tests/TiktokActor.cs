using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Horror.Player;

namespace Horror.Tests
{
    /// <summary>
    /// Outil de tournage (dev, à ne pas laisser dans un build) : une copie de la poupée du joueur qui « parle » face
    /// à la caméra. La bouche suit l'enveloppe d'un fichier WAV (la voix est remise au montage), une main fait
    /// coucou, la tête regarde la caméra et peut se retourner. Sert aux TikToks (« le joueur qui parle »).
    /// PathMover déplace un monstre d'un point à un autre (IA coupée), pour le faire passer derrière.
    /// </summary>
    public class TiktokActor : MonoBehaviour
    {
        public Transform headTarget, leftTarget, rightTarget;
        public Transform lookAt;
        public float eye = 1.05f;

        private MicMouth _mouth;
        private float _clock;                       // temps local (deltaTime cumulé) : Time.time dérape pendant l'enregistrement
        private float[] _env;
        private float _envStart = -1f;
        private float _waveUntil, _turnBack;
        private Vector3 _feet, _faceDir;

        /// <summary>Crée l'acteur aux pieds <paramref name="feet"/>, tourné vers <paramref name="faceToward"/>.</summary>
        public static TiktokActor Spawn(Vector3 feet, Vector3 faceToward)
        {
            DollAvatarRig local = null;
            foreach (DollAvatarRig d in FindObjectsByType<DollAvatarRig>()) if (d.hideOwnHead) { local = d; break; }
            if (local == null) { Debug.LogWarning("[Horror] TiktokActor : poupée du joueur introuvable."); return null; }
            var root = new GameObject("Acteur TikTok (dev)");
            var actor = root.AddComponent<TiktokActor>();
            actor._feet = feet;
            Vector3 f = faceToward - feet; f.y = 0f; actor._faceDir = f.sqrMagnitude > 0.01f ? f.normalized : Vector3.forward;
            actor.headTarget = new GameObject("Tête (cible)").transform; actor.headTarget.SetParent(root.transform, false);
            actor.leftTarget = new GameObject("Main G (cible)").transform; actor.leftTarget.SetParent(root.transform, false);
            actor.rightTarget = new GameObject("Main D (cible)").transform; actor.rightTarget.SetParent(root.transform, false);

            var holder = new GameObject("copie (inactive)");
            holder.SetActive(false);                                      // les scripts de la copie ne démarrent pas encore
            GameObject doll = Instantiate(local.gameObject, holder.transform);
            doll.name = "Poupée (acteur)";
            foreach (HandFingerAnimator h in doll.GetComponentsInChildren<HandFingerAnimator>(true)) DestroyImmediate(h);
            foreach (Collider c in doll.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
            foreach (Renderer rd in doll.GetComponentsInChildren<Renderer>(true)) rd.enabled = true;
            var rig = doll.GetComponent<DollAvatarRig>();
            if (rig != null) { rig.hideOwnHead = false; rig.leanSlide = null; rig.head = actor.headTarget; rig.leftHandTarget = actor.leftTarget; rig.rightHandTarget = actor.rightTarget; }
            var col = doll.GetComponent<DollColorizer>();
            if (col != null) col.isLocal = false;
            var mouthDummy = new GameObject("Bouche").transform;
            mouthDummy.SetParent(doll.transform, false);
            actor._mouth = doll.AddComponent<MicMouth>();
            actor._mouth.isLocal = false;
            actor._mouth.mouth = mouthDummy;
            doll.transform.SetParent(root.transform, false);
            Destroy(holder);
            actor.Pose(0f);
            return actor;
        }

        /// <summary>La bouche suit ce fichier WAV (PCM 16 bits) à partir de maintenant.</summary>
        public void Speak(string wavPath)
        {
            _env = Envelope(wavPath);
            _envStart = _clock;
        }

        public void Wave(float seconds) => _waveUntil = _clock + seconds;

        /// <summary>0 = regarde la caméra, 1 = se retourne (regarde derrière lui).</summary>
        public void TurnBack(float amount) => _turnBack = amount;

        private void Update()
        {
            _clock += Time.deltaTime;
            Pose(_clock);
            float open = 0f;
            if (_env != null && _envStart >= 0f)
            {
                int i = Mathf.FloorToInt((_clock - _envStart) * 60f);
                if (i >= 0 && i < _env.Length) open = Mathf.Clamp01(_env[i] * 9f);
            }
            _mouth.SetRemoteOpenness(open);
        }

        private float _turn;

        private void Pose(float t)
        {
            _turn = Mathf.Lerp(_turn, _turnBack, 1f - Mathf.Exp(-6f * Time.deltaTime));
            Vector3 right = Vector3.Cross(Vector3.up, _faceDir);
            Vector3 head = _feet + Vector3.up * eye + new Vector3(Mathf.Sin(t * 1.3f), Mathf.Sin(t * 2.1f) * 0.5f, 0f) * 0.01f;
            Vector3 toCam = lookAt != null ? (lookAt.position - head) : _faceDir;
            Vector3 look = Vector3.Slerp(toCam.normalized, Quaternion.AngleAxis(150f, Vector3.up) * _faceDir, _turn);
            headTarget.SetPositionAndRotation(head, Quaternion.LookRotation(look, Vector3.up));
            // mains : posées bas devant, un léger balancement ; coucou de la main droite
            Vector3 l = head - Vector3.up * 0.42f + _faceDir * 0.14f - right * 0.15f;
            Vector3 r = head - Vector3.up * 0.42f + _faceDir * 0.14f + right * 0.15f;
            l += Vector3.up * Mathf.Sin(t * 1.7f) * 0.015f;
            r += Vector3.up * Mathf.Sin(t * 1.9f + 1f) * 0.015f;
            if (_clock < _waveUntil)
                r = head + right * (0.28f + Mathf.Sin(t * 9f) * 0.08f) + Vector3.up * 0.05f + _faceDir * 0.12f;
            leftTarget.SetPositionAndRotation(l, Quaternion.LookRotation(_faceDir, Vector3.up));
            rightTarget.SetPositionAndRotation(r, Quaternion.LookRotation(_faceDir, Vector3.up));
        }

        /// <summary>Enveloppe RMS à 60 valeurs/s d'un WAV PCM 16 bits mono ou stéréo.</summary>
        private static float[] Envelope(string path)
        {
            byte[] b = File.ReadAllBytes(path);
            int ch = System.BitConverter.ToInt16(b, 22), sr = System.BitConverter.ToInt32(b, 24);
            int p = 12, dataStart = -1, dataLen = 0;
            while (p + 8 <= b.Length)
            {
                string id = System.Text.Encoding.ASCII.GetString(b, p, 4);
                int len = System.BitConverter.ToInt32(b, p + 4);
                if (id == "data") { dataStart = p + 8; dataLen = Mathf.Min(len, b.Length - dataStart); break; }
                p += 8 + len;
            }
            if (dataStart < 0) return new float[0];
            int frames = dataLen / (2 * ch), win = Mathf.Max(1, sr / 60);
            var env = new List<float>();
            for (int s = 0; s < frames; s += win)
            {
                double acc = 0; int n = 0;
                for (int k = s; k < Mathf.Min(frames, s + win); k++) { float v = System.BitConverter.ToInt16(b, dataStart + k * 2 * ch) / 32768f; acc += v * v; n++; }
                env.Add((float)System.Math.Sqrt(acc / Mathf.Max(1, n)));
            }
            return env.ToArray();
        }
    }

    /// <summary>Déplace un objet (un monstre) de a vers b en « seconds » secondes, tourné dans le sens de la marche.</summary>
    public class PathMover : MonoBehaviour
    {
        private Vector3 _a, _b; private float _t, _dur;

        public static PathMover Move(Transform t, Vector3 a, Vector3 b, float seconds)
        {
            var m = t.GetComponent<PathMover>();
            if (m == null) m = t.gameObject.AddComponent<PathMover>();   // pas de ?? : Unity renvoie un faux null
            m._a = a; m._b = b; m._t = 0f; m._dur = Mathf.Max(0.01f, seconds); m.enabled = true;
            t.position = a;
            Vector3 d = b - a; d.y = 0f;
            if (d.sqrMagnitude > 0.001f) t.rotation = Quaternion.LookRotation(d, Vector3.up);
            return m;
        }

        private void Update()
        {
            _t += Time.deltaTime;
            float k = Mathf.Clamp01(_t / _dur);
            transform.position = Vector3.Lerp(_a, _b, k);
            if (k >= 1f) enabled = false;
        }
    }
}

namespace Horror.Tests
{
    /// <summary>
    /// Séquenceur de tournage : exécute des actions à des instants donnés (temps de jeu cumulé). Toute une scène
    /// est programmée d'un coup et filmée en un seul plan : rien ne se passe « hors caméra » entre deux commandes.
    /// </summary>
    public class Timeline : UnityEngine.MonoBehaviour
    {
        private readonly System.Collections.Generic.List<(float t, System.Action a)> _cues = new System.Collections.Generic.List<(float, System.Action)>();
        private float _t;

        public Timeline At(float t, System.Action a) { _cues.Add((t, a)); return this; }

        private void Update()
        {
            _t += UnityEngine.Time.deltaTime;
            for (int i = _cues.Count - 1; i >= 0; i--)
            {
                if (_cues[i].t > _t) continue;
                System.Action a = _cues[i].a;
                _cues.RemoveAt(i);
                try { a(); } catch (System.Exception e) { UnityEngine.Debug.LogException(e); }
            }
            if (_cues.Count == 0) Destroy(gameObject);
        }
    }
}
