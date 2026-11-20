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

        /// <summary>Lance le jumpscare (appelé par le monstre qui attrape le joueur).</summary>
        public void Trigger(BlindMonsterAI monster)
        {
            if (!IsPlaying) StartCoroutine(Sequence(monster != null ? monster.Head : null, 1f));
        }

        /// <summary>Même jumpscare avec n'importe quel visage (ex. la tête d'un Doux). scale : taille de la copie.</summary>
        public void TriggerFace(Transform faceSource, float scale = 1f)
        {
            if (!IsPlaying) StartCoroutine(Sequence(faceSource, scale));
        }

        [Header("Screamer")]
        [Tooltip("Flashs rouges pendant le screamer (jamais blancs). Décocher pour les joueurs sensibles.")]
        public bool strobe = true;
        [Tooltip("Distance de départ du visage avant qu'il fonce sur toi (m).")]
        public float lungeFrom = 1.6f;
        [Tooltip("Durée de la ruée du visage (s) : très court = plus violent.")]
        public float lungeTime = 0.14f;

        private IEnumerator Sequence(Transform faceSource, float scale)
        {
            IsPlaying = true;
            _locomotion.inputEnabled = false;
            Transform head = _locomotion.head;
            EnsureFader(head);

            // 0) coupure sèche au noir (une fraction de seconde de silence)
            SetFade(1f, Color.black);
            yield return new WaitForSeconds(0.12f);

            // 1) le visage apparaît au loin, éclairé par en dessous (sinon il serait invisible dans le noir)
            GameObject face = null;
            if (faceSource != null)
            {
                face = Instantiate(faceSource.gameObject, head);
                foreach (var c in face.GetComponentsInChildren<MonoBehaviour>()) Destroy(c);
                foreach (var c in face.GetComponentsInChildren<Collider>()) Destroy(c);
                face.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);      // tourné vers toi
                var lightGo = new GameObject("Lumière du screamer");
                lightGo.transform.SetParent(face.transform, false);
                lightGo.transform.localPosition = new Vector3(0f, -0.35f, -0.35f);
                Light l = lightGo.AddComponent<Light>();
                l.type = LightType.Point; l.color = new Color(1f, 0.82f, 0.75f); l.intensity = 6f; l.range = 2.5f;
            }
            SetFade(0f, Color.black);
            if (sting != null) _audio.PlayOneShot(sting, volume);
            Haptics(1f, lungeTime + faceTime + 0.2f);

            // 2) la ruée : il fonce sur toi (accélère jusqu'au bout)
            for (float t = 0f; t < lungeTime; t += Time.deltaTime)
            {
                float k = t / lungeTime;
                k = k * k * k;
                if (face != null)
                {
                    face.transform.localPosition = new Vector3(0f, 0.01f, Mathf.Lerp(lungeFrom, 0.32f, k));
                    face.transform.localScale = Vector3.one * scale * Mathf.Lerp(0.6f, 1.15f, k);
                }
                yield return null;
            }

            // 3) le screamer : saccades, torsions, palpitations, flashs rouges
            float tt = 0f, nextSnap = 0f, nextFlash = 0f;
            bool flashOn = false;
            Quaternion snap = Quaternion.identity;
            while (tt < faceTime)
            {
                tt += Time.deltaTime;
                if (face != null)
                {
                    if (tt >= nextSnap)
                    {
                        nextSnap = tt + Random.Range(0.03f, 0.07f);
                        snap = Quaternion.Euler(Random.Range(-9f, 9f), Random.Range(-7f, 7f), Random.Range(-14f, 14f));
                    }
                    face.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * snap;
                    face.transform.localPosition = new Vector3(Random.Range(-0.018f, 0.018f), 0.01f + Random.Range(-0.015f, 0.015f),
                                                               Mathf.Lerp(0.32f, 0.25f, tt / faceTime));
                    face.transform.localScale = Vector3.one * scale * (1.12f + 0.08f * Mathf.Sin(tt * 55f));
                }
                if (strobe && tt >= nextFlash)
                {
                    nextFlash = tt + 0.09f;
                    flashOn = !flashOn;
                    SetFade(flashOn ? 0.45f : 0f, new Color(0.55f, 0f, 0.02f));
                }
                yield return null;
            }

            // 4) coupure au noir d'un coup, puis retour à la Salle de jeux
            SetFade(1f, Color.black);
            if (face != null) Destroy(face);
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
