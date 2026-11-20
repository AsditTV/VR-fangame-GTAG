using Horror.Cosmetics;
using Horror.Interaction;
using Horror.Locomotion;
using UnityEngine;

namespace Horror.Lore
{
    /// <summary>
    /// Un papier de lore posé dans la map : on l'attrape (grip), on l'approche de son visage pour le lire,
    /// on le retourne pour voir le verso. La première fois : il est ajouté au journal + des pièces.
    ///
    /// Lisible dans le noir : il n'y a pas de lampe torche, donc un papier tenu PRÈS DU VISAGE s'éclaire
    /// doucement de lui-même (assez pour lire, pas assez pour éclairer la pièce). Les papiers jamais
    /// trouvés luisent très faiblement pour qu'on les remarque dans l'obscurité.
    ///
    /// Le maillage (recto + verso) est créé tout seul à partir du LoreDocument.
    /// </summary>
    [RequireComponent(typeof(Grabbable))]
    public class LorePaper : MonoBehaviour
    {
        public LoreDocument document;

        [Header("Sons")]
        public AudioClip pickupSound;
        public AudioClip foundSound;

        [Header("Lecture dans le noir")]
        [Tooltip("Distance au visage (m) à partir de laquelle le papier s'éclaire pour être lu.")]
        public float readDistance = 0.5f;
        [Tooltip("Luminosité du papier quand on le lit.")]
        public float readGlow = 0.55f;
        [Tooltip("Lueur d'un papier jamais trouvé (pour le repérer dans le noir).")]
        public float undiscoveredGlow = 0.04f;

        private Material _front, _back;
        private Grabbable _grab;
        private AudioSource _audio;
        private float _glow;

        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private static readonly int EmissionMap = Shader.PropertyToID("_EmissionMap");

        private void Awake()
        {
            _grab = GetComponent<Grabbable>();
            _grab.gripCurl = 0.35f;
            _grab.throwMultiplier = 0.5f;   // un papier ne se lance pas loin
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.spatialBlend = 1f;
            _audio.maxDistance = 6f;
            _audio.playOnAwake = false;
            if (document != null) BuildMesh();
        }

        private void OnEnable() => _grab.onGrabbed.AddListener(OnGrabbed);
        private void OnDisable() => _grab.onGrabbed.RemoveListener(OnGrabbed);

        private void OnGrabbed(HandGrabber hand)
        {
            if (pickupSound != null) _audio.PlayOneShot(pickupSound, 0.8f);
            if (LoreJournal.MarkFound(document))
            {
                if (foundSound != null) _audio.PlayOneShot(foundSound, 0.6f);
                PlayerWallet.AddCoins(document.coinReward);
            }
        }

        private void Update()
        {
            if (_front == null) return;
            float target = LoreJournal.IsFound(document) ? 0f : undiscoveredGlow;
            if (_grab.Holder != null)
            {
                // Plus le papier est près du visage, plus il est lisible
                float d = DistanceToNearestHead();
                target = Mathf.Lerp(readGlow, 0.05f, Mathf.InverseLerp(readDistance * 0.5f, readDistance * 1.6f, d));
            }
            _glow = Mathf.MoveTowards(_glow, target, Time.deltaTime * 1.5f);
            SetGlow(_front, _glow);
            SetGlow(_back, _glow);
        }

        private float DistanceToNearestHead()
        {
            float best = float.MaxValue;
            foreach (ArmLocomotion p in ArmLocomotion.All)
                if (p != null) best = Mathf.Min(best, Vector3.Distance(p.HeadPosition, transform.position));
            return best;
        }

        private static void SetGlow(Material m, float k)
        {
            if (m == null) return;
            m.SetColor(EmissionColor, Color.white * k);
        }

        // ------------------------------------------------------------------ maillage

        private void BuildMesh()
        {
            Vector2 s = document.size;
            _front = MakeMaterial(document.front);
            _back = MakeMaterial(document.back != null ? document.back : null);
            Quad("Recto", s, Quaternion.identity, 0.0008f, _front);
            Quad("Verso", s, Quaternion.Euler(0f, 180f, 0f), -0.0008f, _back);

            // Collider fin pour l'attraper et le faire tomber sur le sol
            BoxCollider box = GetComponent<BoxCollider>();
            if (box == null) box = gameObject.AddComponent<BoxCollider>();
            box.size = new Vector3(s.x, s.y, 0.004f);
            Rigidbody rb = GetComponent<Rigidbody>();
            rb.mass = 0.02f;
#if UNITY_6000_0_OR_NEWER
            rb.linearDamping = 2.5f;     // le papier plane un peu en tombant
            rb.angularDamping = 2f;
#else
            rb.drag = 2.5f;
            rb.angularDrag = 2f;
#endif
        }

        private void Quad(string name, Vector2 size, Quaternion rot, float z, Material mat)
        {
            GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name;
            Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(transform, false);
            q.transform.localPosition = new Vector3(0f, 0f, z);
            // Le Quad de Unity regarde vers -Z : on le tourne pour que le recto regarde vers +Z
            q.transform.localRotation = rot * Quaternion.Euler(0f, 180f, 0f);
            q.transform.localScale = new Vector3(size.x, size.y, 1f);
            q.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private void OnDrawGizmos()
        {
            // Dans l'éditeur, le papier n'a pas encore de maillage : on montre sa place
            if (document == null || Application.isPlaying) return;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = document.isSecret ? new Color(0.9f, 0.5f, 0.6f) : new Color(0.95f, 0.9f, 0.75f);
            Gizmos.DrawCube(Vector3.zero, new Vector3(document.size.x, document.size.y, 0.002f));
        }

        private static Material MakeMaterial(Texture2D tex)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null || UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null) shader = Shader.Find("Standard");
            var m = new Material(shader);
            Color paper = new Color(0.92f, 0.89f, 0.82f);
            if (tex != null)
            {
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
                if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
                m.SetTexture(EmissionMap, tex);
            }
            else m.color = paper;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.05f);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.05f);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.SetColor(EmissionColor, Color.black);
            return m;
        }
    }
}
