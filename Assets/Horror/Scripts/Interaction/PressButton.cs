using UnityEngine;
using UnityEngine.Events;

namespace Horror.Interaction
{
    /// <summary>
    /// Un bouton physique qu'on enfonce avec le bout des doigts (HandPresser).
    /// Le bouton s'enfonce, fait "clic", vibre, puis appelle "onPressed".
    /// Sert à la boutique, aux portes du Protocole Verrou (étape 4) et aux touches du Minitel.
    /// Il lui faut un Collider (il est mis en déclencheur automatiquement).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class PressButton : MonoBehaviour
    {
        [Tooltip("La partie qui s'enfonce (vide = cet objet).")]
        public Transform cap;
        [Tooltip("Profondeur d'enfoncement (m), vers l'arrière du bouton (-Z local).")]
        public float pressDepth = 0.012f;
        [Tooltip("Délai minimum entre deux appuis (évite les doubles clics).")]
        public float cooldown = 0.4f;
        public AudioClip clickSound;
        [Range(0f, 1f)] public float clickVolume = 0.7f;

        public UnityEvent onPressed = new UnityEvent();

        private Vector3 _capRest;
        private float _nextAllowed;
        private float _pressedUntil;
        private AudioSource _audio;

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
            if (cap == null) cap = transform;
            _capRest = cap.localPosition;
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.spatialBlend = 1f;
            _audio.minDistance = 0.5f;
            _audio.maxDistance = 8f;
            _audio.playOnAwake = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            HandPresserTip tip = other.GetComponent<HandPresserTip>();
            if (tip == null || Time.time < _nextAllowed) return;
            _nextAllowed = Time.time + cooldown;
            _pressedUntil = Time.time + 0.15f;
            if (clickSound != null) _audio.PlayOneShot(clickSound, clickVolume);
            if (tip.owner != null) tip.owner.Buzz();
            onPressed.Invoke();
        }

        private void Update()
        {
            Vector3 target = Time.time < _pressedUntil ? _capRest - Vector3.forward * pressDepth : _capRest;
            cap.localPosition = Vector3.Lerp(cap.localPosition, target, 1f - Mathf.Exp(-30f * Time.deltaTime));
        }
    }
}
