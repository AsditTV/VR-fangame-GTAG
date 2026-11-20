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
    /// seul le visage tremble. Pas de flash blanc.
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

        private IEnumerator Sequence(Transform faceSource, float scale)
        {
            IsPlaying = true;
            _locomotion.inputEnabled = false;
            Transform head = _locomotion.head;
            EnsureFader(head);

            // 1) Le visage surgit
            GameObject face = null;
            if (faceSource != null)
            {
                face = Instantiate(faceSource.gameObject, head);
                foreach (var c in face.GetComponentsInChildren<MonoBehaviour>()) Destroy(c);
                foreach (var c in face.GetComponentsInChildren<Collider>()) Destroy(c);
                face.transform.localPosition = new Vector3(0f, -0.08f, 0.38f);
                face.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // tourné vers toi
                face.transform.localScale = Vector3.one * scale;
            }
            if (sting != null) _audio.PlayOneShot(sting, volume);
            Haptics(1f, faceTime);

            float t = 0f;
            while (t < faceTime)
            {
                t += Time.deltaTime;
                if (face != null)
                {
                    // tremblement + elle se rapproche un peu
                    face.transform.localPosition = new Vector3(Random.Range(-0.012f, 0.012f), -0.08f + Random.Range(-0.01f, 0.01f),
                                                               Mathf.Lerp(0.38f, 0.26f, t / faceTime));
                }
                yield return null;
            }

            // 2) Noir
            yield return Fade(0f, 1f, fadeOutTime);
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

        private void SetFade(float alpha)
        {
            if (_faderMat == null) return;
            _faderMat.color = new Color(0f, 0f, 0f, alpha);
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
