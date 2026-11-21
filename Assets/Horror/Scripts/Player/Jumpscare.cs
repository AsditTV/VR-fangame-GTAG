using System.Collections;
using Horror.Locomotion;
using Horror.Monsters;
using UnityEngine;
using UnityEngine.XR;

namespace Horror.Player
{
    /// <summary>
    /// Le jumpscare quand L'Écoute t'attrape :
    ///  1. son visage (une copie de sa tête) surgit à 35 cm de tes yeux et tremble, avec un son énorme ;
    ///  2. les manettes vibrent à fond ;
    ///  3. écran noir ;
    ///  4. tu réapparais dans la Salle de jeux (zone sûre), puis l'image revient doucement.
    /// À mettre sur l'objet "Joueur" (avec ArmLocomotion). Le monstre l'appelle tout seul.
    ///
    /// Confort VR : le visage reste fixe par rapport à la tête (pas de mouvement de caméra forcé),
    /// seul le visage bouge. Flashs rouges seulement (jamais blancs), désactivables (strobe).
    /// </summary>
    [RequireComponent(typeof(ArmLocomotion))]
    public class Jumpscare : MonoBehaviour
    {
        [Tooltip("Son du jumpscare (Assets/Horror/Audio/Jumpscare/jumpscare_sting).")]
        public AudioClip sting;
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("Durée pendant laquelle le visage reste devant toi (secondes).")]
        public float faceTime = 0.9f;
        public float fadeOutTime = 0.35f;
        public float blackTime = 0.6f;
        public float fadeInTime = 0.9f;

        /// <summary>Vrai pendant toute la séquence (pour bloquer d'autres actions).</summary>
        public bool IsPlaying { get; private set; }

        private ArmLocomotion _locomotion;
        private AudioSource _audio;
        private Renderer _fader;
        private Material _faderMat;

        private void Awake()
        {
            _locomotion = GetComponent<ArmLocomotion>();
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.spatialBlend = 0f;   // dans la tête, à fond
            _audio.playOnAwake = false;
        }

        /// <summary>Lance le jumpscare de L'Écoute (appelé par elle quand elle attrape le joueur).</summary>
        public void Trigger(BlindMonsterAI monster)
        {
            if (monster == null) { TriggerScreamer(null, null, null, 1f); return; }
            TriggerScreamer(monster.transform, monster.Head, monster.jumpscareSound, 0.5f);
        }

        /// <summary>Ancien appel : seulement une tête (gardé pour compatibilité).</summary>
        public void TriggerFace(Transform faceSource, float scale = 1f) => TriggerScreamer(faceSource, faceSource, null, scale);

        /// <summary>
        /// Le screamer : <paramref name="body"/> (le monstre entier, bras compris) fonce sur toi, sa tête
        /// <paramref name="face"/> pile devant tes yeux, sur fond noir, avec son cri à lui (<paramref name="scream"/>).
        /// </summary>
        public void TriggerScreamer(Transform body, Transform face, AudioClip scream, float scale)
        {
            if (!IsPlaying) StartCoroutine(Sequence(body, face, scream, scale));
        }

        [Header("Screamer")]
        [Tooltip("Flashs rouges pendant le screamer (jamais blancs). Décocher pour les joueurs sensibles.")]
        public bool strobe = true;
        [Tooltip("Distance de départ du visage avant qu'il fonce sur toi (m).")]
        public float lungeFrom = 1.6f;
        [Tooltip("Durée de la ruée du visage (s) : très court = plus violent.")]
        public float lungeTime = 0.14f;

        [Header("Le noir après la mort")]
        [Tooltip("Chuchotements d'enfant (un au hasard, jamais deux fois de suite). Assets/Horror/Audio/Death.")]
        public AudioClip[] deathWhispers = new AudioClip[0];
        [Tooltip("Sous-titres, dans le même ordre que deathWhispers.")]
        [TextArea] public string[] whisperLines = new string[0];
        [Tooltip("Le noir dure le temps du chuchotement, borné entre ces deux valeurs (s).")]
        public float minBlackTime = 2.2f, maxBlackTime = 3.2f;

        [Tooltip("Pour les vidéos : impose le murmure (index), -1 = au hasard.")]
        public int forceWhisper = -1;

        /// <summary>Vrai pendant que le sous-titre du murmure est affiché (le noir après la mort).</summary>
        public bool ShowingWhisper => _whisperText != null && _whisperText.gameObject.activeSelf;

        private TextMesh _whisperText;
        private int _lastWhisper = -1, _lastWhisper2 = -1;

        /// <summary>Calque réservé au screamer : pendant le jumpscare, la caméra ne voit que lui.</summary>
        public const int ScreamerLayer = 30;

        private IEnumerator Sequence(Transform body, Transform face, AudioClip scream, float scale)
        {
            IsPlaying = true;
            _locomotion.inputEnabled = false;
            Transform head = _locomotion.head;
            EnsureFader(head);
            Camera cam = head.GetComponent<Camera>();
            int oldMask = cam != null ? cam.cullingMask : 0;
            CameraClearFlags oldClear = cam != null ? cam.clearFlags : CameraClearFlags.Skybox;
            Color oldBg = cam != null ? cam.backgroundColor : Color.black;

            // la rature : ce que les autres voient (giclée et flaque d'encre, son), et l'annonce réseau
            Vector3 deathPos = transform.position;
            DeathMarks.Spawn(deathPos);
            Horror.Net.NetworkBootstrap.Session?.SendEvent(new Horror.Net.GameEventMessage
            {
                kind = Horror.Net.GameEventMessage.Kind.Mort,
                playerId = Horror.Net.NetworkBootstrap.Session.LocalPlayerId,
                position = deathPos,
            });

            // 0) coupure sèche au noir
            SetFade(1f, Color.black);
            yield return new WaitForSeconds(0.1f);

            // 1) le monstre entier, sur son calque ; plus rien d'autre n'existe (fond noir)
            GameObject clone = null; Transform cloneHead = null;
            if (body != null)
            {
                clone = Instantiate(body.gameObject);   // pas encore sous la tête : ses collisions ne touchent pas le joueur
                foreach (var c in clone.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
                var scripts = clone.GetComponentsInChildren<MonoBehaviour>(true);
                foreach (var c in scripts) c.enabled = false;                        // le cerveau de la copie s'éteint tout de suite
                for (int i = scripts.Length - 1; i >= 0; i--) Destroy(scripts[i]);   // à l'envers : les dépendants d'abord
                clone.transform.SetParent(head, false);
                foreach (var c in clone.GetComponentsInChildren<AudioSource>(true)) Destroy(c);
                foreach (var r in clone.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
                foreach (var t in clone.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = ScreamerLayer;
                clone.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);      // tourné vers toi
                clone.transform.localScale = Vector3.one * scale;
                if (face != null)
                    foreach (Transform t in clone.GetComponentsInChildren<Transform>(true))
                        if (t.name == face.name) { cloneHead = t; break; }
                if (cloneHead == null) cloneHead = clone.transform;
                // visage de cri s'il existe (Le Drap) : montré, le calme caché
                foreach (Transform t in clone.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "Visage_Cri") foreach (var r in t.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
                    else if (t.name == "Visage") foreach (var r in t.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                }
                var lightGo = new GameObject("Lumière du screamer") { layer = ScreamerLayer };
                lightGo.transform.SetParent(head, false);
                lightGo.transform.localPosition = new Vector3(0f, -0.35f, 0.15f);
                Light l = lightGo.AddComponent<Light>();
                l.type = LightType.Point; l.color = new Color(1f, 0.8f, 0.72f); l.intensity = 5f; l.range = 3f;
                l.cullingMask = 1 << ScreamerLayer;
                lightGo.transform.SetParent(clone.transform, true);
                // petite lumière de face, douce : un visage plat (le portrait de L'Écoute) restait noir sous la seule
                // lumière d'en bas
                var fillGo = new GameObject("Lumière de face du screamer") { layer = ScreamerLayer };
                fillGo.transform.SetParent(head, false);
                fillGo.transform.localPosition = new Vector3(0.04f, 0.16f, 0.06f);
                Light f = fillGo.AddComponent<Light>();
                f.type = LightType.Point; f.color = new Color(0.9f, 0.86f, 1f); f.intensity = 9f; f.range = 2f;
                f.cullingMask = 1 << ScreamerLayer;
                fillGo.transform.SetParent(clone.transform, true);
            }
            if (cam != null)
            {
                cam.cullingMask = 1 << ScreamerLayer;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
            }
            SetFade(0f, Color.black);
            AudioClip clip = scream != null ? scream : sting;
            if (clip != null) _audio.PlayOneShot(clip, volume);
            Haptics(1f, lungeTime + faceTime + 0.2f);

            // centre réel du visage (pas le pivot : celui du Doux est au cou) et point « avant » du visage
            Transform facePart = null;
            if (clone != null)
                foreach (string n in new[] { "Visage_Cri", "Portrait", "Masque", "Tete_Details", "Visage", "Tete_Creme" })
                {
                    foreach (Transform t in cloneHead.GetComponentsInChildren<Transform>(true))
                        if (t.name == n && t.GetComponentInChildren<Renderer>() != null) { facePart = t; break; }
                    if (facePart != null) break;
                }
            Vector3 Center(Transform t)
            {
                Bounds b = default; bool first = true;
                foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
                {
                    if (!r.enabled) continue;
                    if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
                }
                return first ? t.position : b.center;
            }
            // tourne la copie pour que le visage te regarde, quelle que soit la pose de la tête (elle écoutait de côté...)
            Quaternion baseRot = Quaternion.Euler(0f, 180f, 0f);
            if (clone != null)
            {
                clone.transform.localScale = Vector3.one * scale;
                clone.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                clone.transform.position += head.TransformPoint(new Vector3(0f, 0f, 1.5f)) - Center(cloneHead);   // d'abord devant toi
                // centre du CRÂNE (la plus grosse pièce de la tête), pas de tout : câbles et cornets le décalaient
                Vector3 hc = Center(cloneHead);
                float best = 0f;
                foreach (Renderer r in cloneHead.GetComponentsInChildren<Renderer>(true))
                {
                    if (facePart != null && r.transform.IsChildOf(facePart)) continue;
                    Vector3 sz = r.bounds.size;
                    float vol = sz.x * sz.y * sz.z;
                    if (vol > best) { best = vol; hc = r.bounds.center; }
                }
                Vector3 faceDir = facePart != null ? (Center(facePart) - hc) : clone.transform.forward;
                // visage plat (portrait de L'Écoute, masque...) : sa normale est bien plus fiable que les centres
                // (les cornets et le chignon décalent le centre de la tête : on finissait par voir sa nuque)
                if (facePart != null && FlatNormal(facePart, out Vector3 n))
                    faceDir = Vector3.Dot(n, Center(facePart) - hc) >= 0f ? n : -n;
                if (faceDir.sqrMagnitude > 1e-6f)
                {
                    Quaternion fix = Quaternion.FromToRotation(faceDir.normalized, (head.position - hc).normalized);
                    clone.transform.rotation = fix * clone.transform.rotation;
                    // garder la tête droite (pas à l'envers) : on remet le haut du visage vers le haut
                    Vector3 up = Vector3.ProjectOnPlane(cloneHead.up, (head.position - hc).normalized);
                    Vector3 want = Vector3.ProjectOnPlane(head.up, (head.position - hc).normalized);
                    if (up.sqrMagnitude > 1e-4f && want.sqrMagnitude > 1e-4f)
                        clone.transform.rotation = Quaternion.FromToRotation(up.normalized, want.normalized) * clone.transform.rotation;
                }
                baseRot = clone.transform.localRotation;
            }

            // place le clone pour que le CENTRE DU VISAGE soit au point voulu devant tes yeux
            void PlaceHead(Vector3 local, Quaternion extra, float s)
            {
                if (clone == null) return;
                clone.transform.localRotation = baseRot * extra;
                clone.transform.localScale = Vector3.one * s;
                Vector3 target = head.TransformPoint(local);
                clone.transform.position += target - Center(cloneHead);
            }

            // 2) la ruée : il fonce sur toi
            for (float t = 0f; t < lungeTime; t += Time.deltaTime)
            {
                float k = t / lungeTime; k = k * k * k;
                PlaceHead(new Vector3(0f, 0.0f, Mathf.Lerp(lungeFrom, 0.3f, k)), Quaternion.identity, scale * Mathf.Lerp(0.7f, 1.1f, k));
                yield return null;
            }

            // 3) le screamer : saccades, torsions, palpitations, flashs rouges
            float tt = 0f, nextSnap = 0f, nextFlash = 0f;
            bool flashOn = false;
            Quaternion snap = Quaternion.identity;
            while (tt < faceTime)
            {
                tt += Time.deltaTime;
                if (tt >= nextSnap)
                {
                    nextSnap = tt + Random.Range(0.03f, 0.07f);
                    snap = Quaternion.Euler(Random.Range(-9f, 9f), Random.Range(-7f, 7f), Random.Range(-14f, 14f));
                }
                PlaceHead(new Vector3(Random.Range(-0.02f, 0.02f), Random.Range(-0.015f, 0.015f), Mathf.Lerp(0.3f, 0.23f, tt / faceTime)),
                          snap, scale * (1.08f + 0.07f * Mathf.Sin(tt * 55f)));
                if (strobe && tt >= nextFlash)
                {
                    nextFlash = tt + 0.09f;
                    flashOn = !flashOn;
                    SetFade(flashOn ? 0.45f : 0f, new Color(0.55f, 0f, 0.02f));
                }
                yield return null;
            }

            // 4) coupure au noir, retour à la Salle de jeux pendant le noir
            SetFade(1f, Color.black);
            if (clone != null) Destroy(clone);
            _locomotion.TeleportTo(_locomotion.SpawnPosition);

            // 5) le noir : une voix d'enfant chuchote un morceau de l'histoire (sous-titré, devant les yeux)
            int w = PickWhisper();
            float black = blackTime;
            if (w >= 0)
            {
                AudioClip wc = deathWhispers[w];
                black = Mathf.Clamp(wc.length + 0.3f, minBlackTime, maxBlackTime);
                yield return new WaitForSeconds(0.35f);
                _audio.PlayOneShot(wc, 0.9f);   // la fin du murmure peut déborder sur le retour de l'image : c'est voulu
                if (cam != null && w < whisperLines.Length)
                {
                    ShowWhisper(head, whisperLines[w]);
                    SetFade(0f, Color.black);   // la caméra ne voit que le calque du screamer : noir + sous-titre
                }
                yield return new WaitForSeconds(black - 0.35f);
                SetFade(1f, Color.black);
                HideWhisper();
            }
            else yield return new WaitForSeconds(black);
            if (cam != null) { cam.cullingMask = oldMask; cam.clearFlags = oldClear; cam.backgroundColor = oldBg; }

            // 3) On revient dans la Salle de jeux
            _locomotion.inputEnabled = true;
            yield return Fade(1f, 0f, fadeInTime);
            IsPlaying = false;
        }

        /// <summary>Normale d'une pièce plate (son plus petit côté), en monde. Faux si la pièce n'est pas plate.</summary>
        private static bool FlatNormal(Transform part, out Vector3 normal)
        {
            normal = Vector3.forward;
            MeshFilter mf = part.GetComponentInChildren<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return false;
            Vector3 e = Vector3.Scale(mf.sharedMesh.bounds.size, mf.transform.lossyScale);
            e = new Vector3(Mathf.Abs(e.x), Mathf.Abs(e.y), Mathf.Abs(e.z));
            float min = Mathf.Min(e.x, Mathf.Min(e.y, e.z)), mid = e.x + e.y + e.z - min - Mathf.Max(e.x, Mathf.Max(e.y, e.z));
            if (min > 0.35f * mid) return false;   // pas assez plate (une tête de Doux en volume)
            Vector3 local = min == e.x ? Vector3.right : min == e.y ? Vector3.up : Vector3.forward;
            normal = mf.transform.TransformDirection(local).normalized;
            return true;
        }

        private int PickWhisper()
        {
            int n = deathWhispers != null ? deathWhispers.Length : 0;
            if (n == 0) return -1;
            if (forceWhisper >= 0 && forceWhisper < n) return forceWhisper;
            int pick = Random.Range(0, n);
            for (int tries = 0; tries < 8 && n > 2 && (pick == _lastWhisper || pick == _lastWhisper2); tries++) pick = Random.Range(0, n);
            _lastWhisper2 = _lastWhisper; _lastWhisper = pick;
            return deathWhispers[pick] != null ? pick : -1;
        }

        private void ShowWhisper(Transform head, string line)
        {
            if (_whisperText == null)
            {
                var go = new GameObject("Murmure (sous-titre)") { layer = ScreamerLayer };
                go.transform.SetParent(head, false);
                go.transform.localPosition = new Vector3(0f, -0.12f, 1.6f);
                _whisperText = go.AddComponent<TextMesh>();
                _whisperText.anchor = TextAnchor.MiddleCenter;
                _whisperText.alignment = TextAlignment.Center;
                _whisperText.characterSize = 0.012f;
                _whisperText.fontSize = 64;
                _whisperText.color = new Color(0.78f, 0.74f, 0.82f);
            }
            _whisperText.text = line;
            _whisperText.gameObject.SetActive(true);
        }

        private void HideWhisper() { if (_whisperText != null) _whisperText.gameObject.SetActive(false); }

        private IEnumerator Fade(float from, float to, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                SetFade(Mathf.Lerp(from, to, t / duration));
                yield return null;
            }
            SetFade(to);
        }

        private void SetFade(float alpha) => SetFade(alpha, Color.black);

        private void SetFade(float alpha, Color tint)
        {
            if (_faderMat == null) return;
            _faderMat.color = new Color(tint.r, tint.g, tint.b, alpha);
            _fader.enabled = alpha > 0.001f;
        }

        /// <summary>Un petit écran noir collé devant les yeux (sert de fondu).</summary>
        private void EnsureFader(Transform head)
        {
            if (_fader != null) return;
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Fondu au noir";
            DestroyImmediate(quad.GetComponent<Collider>());   // tout de suite : sinon le Rigidbody du joueur le voit (erreur PhysX)
            quad.transform.SetParent(head, false);
            quad.layer = ScreamerLayer;   // visible aussi quand la caméra ne voit plus que le screamer
            quad.transform.localPosition = new Vector3(0f, 0f, 0.05f);
            quad.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            _faderMat = new Material(shader) { renderQueue = 5000 };
            _fader = quad.GetComponent<Renderer>();
            _fader.sharedMaterial = _faderMat;
            _fader.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            SetFade(0f);
        }

        private static void Haptics(float amplitude, float duration)
        {
            InputDevices.GetDeviceAtXRNode(XRNode.LeftHand).SendHapticImpulse(0, amplitude, duration);
            InputDevices.GetDeviceAtXRNode(XRNode.RightHand).SendHapticImpulse(0, amplitude, duration);
        }
    }
}
