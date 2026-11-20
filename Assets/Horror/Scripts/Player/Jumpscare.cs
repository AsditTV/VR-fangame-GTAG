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
                Vector3 hc = Center(cloneHead);
                Vector3 faceDir = facePart != null ? (Center(facePart) - hc) : clone.transform.forward;
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

            // 4) coupure au noir, on rend le monde à la caméra, retour à la Salle de jeux
            SetFade(1f, Color.black);
            if (clone != null) Destroy(clone);
            if (cam != null) { cam.cullingMask = oldMask; cam.clearFlags = oldClear; cam.backgroundColor = oldBg; }
            _locomotion.TeleportTo(_locomotion.SpawnPosition);
            yield return new WaitForSeconds(blackTime);

            // 3) On revient dans la Salle de jeux
            _locomotion.inputEnabled = true;
            yield return Fade(1f, 0f, fadeInTime);
            IsPlaying = false;
        }

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
            Destroy(quad.GetComponent<Collider>());
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
