using System.Collections;
using UnityEngine;
using Horror.Locomotion;
using Horror.Noise;
using Horror.Player;

namespace Horror.Monsters
{
    /// <summary>
    /// Le Drap (Odette, la lingère) : vit sous l'eau savonneuse de la Laverie.
    ///
    /// Sa seule règle : il SENT LE SOL. Toute main posée sur le carrelage, tout joueur debout dans l'eau,
    /// tout objet qui tombe au fond, et il glisse sous l'eau jusque-là, puis surgit. Il n'entend pas la voix :
    /// dans la Laverie, on peut parler, mais il faut traverser par les machines, les étagères, les fils à linge.
    /// Il ne sort jamais de l'eau (son territoire) ; L'Écoute, elle, n'y entre pas.
    /// Astuce : lancer un objet au fond de l'eau l'attire ailleurs.
    /// </summary>
    public class SheetMonsterAI : MonoBehaviour
    {
        [Header("Zone")]
        public Bounds zone;                 // sa Laverie (coordonnées monde)
        public float floorY;                // hauteur du carrelage
        [Tooltip("Hauteur au-dessus du sol en dessous de laquelle un contact compte (m).")]
        public float floorTolerance = 0.45f;

        [Header("Corps")]
        public Transform face;              // la tête (copiée pour le jumpscare)
        [Tooltip("Profondeur sous le sol quand il est immergé : on ne voit qu'une bosse dans l'eau.")]
        public float sinkDepth = 1.95f;

        [Header("Réglages")]
        public float glideSpeed = 3.0f;
        public float riseTime = 0.35f;
        public float catchRadius = 1.2f;
        public float standTime = 1.4f;
        [Tooltip("Il se penche sur toi en surgissant (degrés).")]
        public float leanAngle = 22f;
        [Tooltip("Perché trop longtemps : sa tête émerge sous toi pour te fixer (secondes avant).")]
        public float stareAfter = 7f;
        public AudioClip sloshLoop;
        public AudioClip riseSound;

        public enum Etat { Dort, Glisse, Surgit }
        public Etat etat = Etat.Dort;

        private Vector3 _target;
        private float _nextWander, _cooldown, _scale = 1f;
        private bool _hasTarget;
        private AudioSource _loop;
        private readonly RaycastHit[] _hits = new RaycastHit[8];
        private Renderer[] _calmFace = new Renderer[0], _screamFace = new Renderer[0];
        private float _perchedFor;

        private void Start()
        {
            _scale = transform.lossyScale.y;
            Transform calm = FindDeep(transform, "Visage"), scream = FindDeep(transform, "Visage_Cri");
            if (calm != null) _calmFace = calm.GetComponentsInChildren<Renderer>(true);
            if (scream != null) _screamFace = scream.GetComponentsInChildren<Renderer>(true);
            ShowScream(false);
            transform.position = new Vector3(transform.position.x, floorY - sinkDepth * _scale, transform.position.z);
            if (sloshLoop != null)
            {
                _loop = gameObject.AddComponent<AudioSource>();
                _loop.clip = sloshLoop; _loop.loop = true; _loop.spatialBlend = 1f; _loop.maxDistance = 14f; _loop.volume = 0.25f;
                _loop.Play();
            }
        }

        private void OnEnable() => NoiseSystem.Heard += OnNoise;
        private void OnDisable() => NoiseSystem.Heard -= OnNoise;

        /// <summary>Un bruit qui touche le fond (main sur le carrelage, objet qui tombe) : il le sent.</summary>
        private void OnNoise(NoiseEvent e)
        {
            if (e.type == NoiseType.Voix) return;                       // il n'entend pas les voix
            if (!zone.Contains(e.position) || e.position.y > floorY + floorTolerance) return;
            Feel(e.position);
        }

        private void Feel(Vector3 p)
        {
            _target = new Vector3(p.x, floorY, p.z);
            _hasTarget = true;
            if (etat == Etat.Dort) etat = Etat.Glisse;
        }

        private void Update()
        {
            _cooldown -= Time.deltaTime;
            // un joueur debout dans l'eau (pas sur une machine) : la vibration ne s'arrête pas
            foreach (ArmLocomotion p in ArmLocomotion.All)
                if (p != null && p.inputEnabled && zone.Contains(p.HeadPosition) && StandsOnFloor(p)) Feel(p.HeadPosition);

            if (etat == Etat.Surgit) return;

            // perché sur une machine depuis trop longtemps : il vient sous toi et sort la tête pour te fixer
            ArmLocomotion perched = null;
            foreach (ArmLocomotion p in ArmLocomotion.All)
                if (p != null && p.inputEnabled && zone.Contains(p.HeadPosition) && !StandsOnFloor(p)) perched = p;
            _perchedFor = perched != null ? _perchedFor + Time.deltaTime : 0f;
            if (perched != null && !_hasTarget && _perchedFor > stareAfter && _cooldown <= 0f)
            {
                _perchedFor = 0f;
                StartCoroutine(Stare(perched));
                return;
            }
            Vector3 pos = transform.position;
            float speed = glideSpeed;
            if (!_hasTarget)
            {
                speed *= 0.25f;                                          // il rôde lentement
                if (Time.time > _nextWander)
                {
                    _nextWander = Time.time + Random.Range(3f, 7f);
                    _target = new Vector3(Random.Range(zone.min.x + 1f, zone.max.x - 1f), floorY, Random.Range(zone.min.z + 1f, zone.max.z - 1f));
                }
            }
            Vector3 flat = new Vector3(_target.x - pos.x, 0f, _target.z - pos.z);
            float d = flat.magnitude;
            if (d > 0.05f)
            {
                Vector3 step = flat / d * Mathf.Min(d, speed * Time.deltaTime);
                pos += step;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flat / d), 1f - Mathf.Exp(-4f * Time.deltaTime));
            }
            float bob = Mathf.Sin(Time.time * 2.1f) * 0.04f * _scale;      // la bosse ondule dans l'eau
            transform.position = new Vector3(Mathf.Clamp(pos.x, zone.min.x + 0.5f, zone.max.x - 0.5f), floorY - sinkDepth * _scale + bob,
                                             Mathf.Clamp(pos.z, zone.min.z + 0.5f, zone.max.z - 0.5f));
            if (_loop != null) _loop.volume = _hasTarget ? 0.55f : 0.25f;

            if (_hasTarget && d < 0.45f * _scale && _cooldown <= 0f) StartCoroutine(Rise());
        }

        /// <summary>Vrai si le joueur est debout sur le carrelage (et pas sur une machine, un fil, une étagère).</summary>
        private bool StandsOnFloor(ArmLocomotion p)
        {
            Rigidbody own = p.GetComponent<Rigidbody>();
            int n = Physics.RaycastNonAlloc(p.HeadPosition, Vector3.down, _hits, 3f * _scale, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue; Collider first = null;
            for (int i = 0; i < n; i++)
            {
                if (own != null && _hits[i].rigidbody == own) continue;
                if (_hits[i].distance < best) { best = _hits[i].distance; first = _hits[i].collider; }
            }
            return first != null && first.name.StartsWith("Laverie_Sol") && best < 1.3f;
        }

        private IEnumerator Rise()
        {
            etat = Etat.Surgit;
            _hasTarget = false;
            if (riseSound != null) AudioSource.PlayClipAtPoint(riseSound, _target, 1f);
            ShowScream(true);
            Vector3 from = transform.position, to = new Vector3(from.x, floorY - 0.15f * _scale, from.z);
            // il se tourne vers le joueur le plus proche et se penche sur lui en grandissant
            ArmLocomotion victim = Nearest(to);
            Quaternion r0 = transform.rotation, r1 = r0;
            if (victim != null)
            {
                Vector3 look = victim.HeadPosition - to; look.y = 0f;
                if (look.sqrMagnitude > 0.01f) r1 = Quaternion.LookRotation(look.normalized) * Quaternion.Euler(leanAngle, 0f, 0f);
            }
            Vector3 s0 = transform.localScale;
            for (float t = 0f; t < riseTime; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / riseTime);
                transform.position = Vector3.Lerp(from, to, k);
                transform.rotation = Quaternion.Slerp(r0, r1, k);
                transform.localScale = s0 * (1f + 0.12f * k);
                yield return null;
            }
            transform.position = to;
            foreach (ArmLocomotion p in ArmLocomotion.All)
            {
                if (p == null || !p.inputEnabled) continue;
                Vector3 h = p.HeadPosition;
                float dh = new Vector2(h.x - to.x, h.z - to.z).magnitude;
                if (dh < catchRadius * _scale && h.y < floorY + 2.2f * _scale)
                {
                    _cooldown = 4f;
                    Jumpscare js = p.GetComponent<Jumpscare>();
                    if (js != null) js.TriggerFace(face != null ? face : transform, 0.5f);
                    else p.TeleportTo(p.SpawnPosition);
                }
            }
            yield return new WaitForSeconds(standTime);
            for (float t = 0f; t < 0.9f; t += Time.deltaTime)                // il replonge
            {
                transform.position = Vector3.Lerp(to, from, t / 0.9f);
                yield return null;
            }
            transform.localScale = s0;
            transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized + Vector3.forward * 1e-4f);
            ShowScream(false);
            _cooldown = Mathf.Max(_cooldown, 1.5f);
            etat = _hasTarget ? Etat.Glisse : Etat.Dort;
        }

        /// <summary>Il glisse sous le joueur perché, sort la tête de l'eau, le fixe, puis replonge (sans attraper).</summary>
        private IEnumerator Stare(ArmLocomotion p)
        {
            etat = Etat.Surgit;
            Vector3 under = new Vector3(p.HeadPosition.x, transform.position.y, p.HeadPosition.z);
            under += (transform.position - under).normalized * 1.2f * _scale;    // à côté, pas pile dessous
            while ((new Vector3(transform.position.x, 0, transform.position.z) - new Vector3(under.x, 0, under.z)).magnitude > 0.15f)
            {
                transform.position = Vector3.MoveTowards(transform.position, under, glideSpeed * 0.6f * Time.deltaTime);
                yield return null;
            }
            Vector3 low = transform.position, peek = new Vector3(low.x, floorY - (2.3f - 0.75f) * _scale, low.z);   // seulement la tête dépasse
            for (float t = 0f; t < 1.6f; t += Time.deltaTime)
            {
                transform.position = Vector3.Lerp(low, peek, Mathf.SmoothStep(0f, 1f, t / 1.6f));
                Vector3 look = p.HeadPosition - transform.position; look.y = 0f;
                if (look.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look.normalized), 4f * Time.deltaTime);
                yield return null;
            }
            yield return new WaitForSeconds(2.5f);                                 // il te regarde
            for (float t = 0f; t < 1.2f; t += Time.deltaTime)
            {
                transform.position = Vector3.Lerp(peek, low, t / 1.2f);
                yield return null;
            }
            _cooldown = 3f;
            etat = Etat.Dort;
        }

        private void ShowScream(bool on)
        {
            foreach (Renderer r in _screamFace) if (r != null) r.enabled = on;
            foreach (Renderer r in _calmFace) if (r != null) r.enabled = !on;
        }

        private ArmLocomotion Nearest(Vector3 p)
        {
            ArmLocomotion best = null; float bd = float.MaxValue;
            foreach (ArmLocomotion a in ArmLocomotion.All)
            {
                if (a == null) continue;
                float d = (a.HeadPosition - p).sqrMagnitude;
                if (d < bd) { bd = d; best = a; }
            }
            return best;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }
    }
}
