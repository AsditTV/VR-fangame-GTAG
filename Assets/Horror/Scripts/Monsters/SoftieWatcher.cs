using Horror.Locomotion;
using Horror.Noise;
using Horror.Player;
using Horror.World;
using UnityEngine;

namespace Horror.Monsters
{
    /// <summary>
    /// Les Doux (les animateurs coincés dans leurs costumes) jouent à « 1, 2, 3, soleil » :
    /// ils ne bougent QUE quand personne ne les regarde.
    ///  - Tête seule (canWalk = faux) : la tête se tourne vers toi quand tu regardes ailleurs.
    ///    Tu te retournes : elle est figée... mais plus dans la même position.
    ///  - Le marcheur (canWalk = vrai) : il avance vers toi dès que tu détournes les yeux,
    ///    s'arrête net quand tu le regardes. S'il arrive à te toucher : jumpscare, retour à la zone sûre.
    ///    Il ne grimpe pas : en hauteur (balcon, passerelles) tu es à l'abri.
    /// Le pivot de l'objet doit être au centre de la tête (ou aux pieds pour le marcheur), +Z = son visage.
    /// </summary>
    public class SoftieWatcher : MonoBehaviour, IScreamer
    {
        [Tooltip("La tête qui se tourne (si vide : cet objet).")]
        public Transform head;
        public bool canWalk;
        [Tooltip("Zone où il a le droit d'aller (le théâtre). Taille nulle = partout.")]
        public Bounds area;

        [Header("Regard")]
        [Tooltip("Demi-angle du regard (degrés) : dans ce cône, et sans mur entre vous, il est « vu ».")]
        public float viewAngle = 50f;
        [Tooltip("Distance maximale à laquelle il s'intéresse à un joueur.")]
        public float range = 16f;
        public float headTurnSpeed = 140f;

        [Header("Marcheur")]
        public float walkSpeed = 3.6f;
        public float catchDistance = 1.2f;
        [Tooltip("Taille de la copie de la tête pendant le jumpscare (sa tête est énorme).")]
        public float jumpscareFaceScale = 0.55f;
        public AudioClip shuffleLoop;     // frottement de costume pendant qu'il bouge
        public AudioClip settleSound;
        [Tooltip("Son du screamer quand il t'attrape (unique aux Doux).")]
        public AudioClip jumpscareSound;     // « crrr » quand tu te retournes et qu'il s'est figé

        private Quaternion _headRest;
        private Vector3 _home;
        private Quaternion _homeRot;
        private AudioSource _shuffle;
        private bool _movedUnseen;
        private bool _wasSeen = true;
        private float _cooldown;
        private float _pause;
        [Tooltip("Après une prise, il reste planté là (s) avant de retourner à sa place.")]
        public float pauseAfterCatch = 4f;

        private void Start()
        {
            if (head == null) head = transform;
            _headRest = head.rotation;
            _home = transform.position;
            _homeRot = transform.rotation;
            if (canWalk && shuffleLoop != null)
            {
                _shuffle = gameObject.AddComponent<AudioSource>();
                _shuffle.clip = shuffleLoop;
                _shuffle.loop = true;
                _shuffle.spatialBlend = 1f;
                _shuffle.maxDistance = 12f;
                _shuffle.volume = 0f;
                _shuffle.Play();
            }
        }

        private void Update()
        {
            _cooldown -= Time.deltaTime;
            if (_pause > 0f)
            {
                // il reste debout sur l'endroit de la prise, puis rejoint son rang (les autres joueurs le voient)
                _pause -= Time.deltaTime;
                if (_shuffle != null) _shuffle.volume = 0f;
                if (_pause <= 0f)
                {
                    if (SeenByAnyone()) _pause = 0.25f;                         // jamais sous les yeux de quelqu'un
                    else transform.SetPositionAndRotation(_home, _homeRot);
                }
                return;
            }
            PlayerTarget target = NearestPlayer();
            bool seen = SeenByAnyone();

            if (seen)
            {
                if (!_wasSeen && _movedUnseen && settleSound != null)
                    AudioSource.PlayClipAtPoint(settleSound, head.position, 0.7f);
                _movedUnseen = false;
                if (_shuffle != null) _shuffle.volume = 0f;
                _wasSeen = true;
                return;                                    // figé
            }
            _wasSeen = false;
            if (target == null) { if (_shuffle != null) _shuffle.volume = 0f; return; }

            // La tête se tourne vers toi (autour de la verticale)
            Vector3 to = target.HeadPosition - head.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.01f)
            {
                Quaternion want = Quaternion.LookRotation(to.normalized, Vector3.up);
                if (!canWalk) want = Quaternion.RotateTowards(_headRest, want, 150f);   // pas de tête à 360°
                head.rotation = Quaternion.RotateTowards(head.rotation, want, headTurnSpeed * Time.deltaTime);
                _movedUnseen = true;
            }
            if (!canWalk) return;

            // Le marcheur avance (sans grimper : il ne quitte pas le sol)
            Vector3 flat = target.HeadPosition - transform.position;
            float heightAbove = flat.y;
            flat.y = 0f;
            float dist = flat.magnitude;
            bool reachable = heightAbove < 2.4f;           // en hauteur, il ne peut que rester en dessous
            if (dist > 0.6f)
            {
                Vector3 step = flat / dist * Mathf.Min(walkSpeed * Time.deltaTime, dist - 0.5f);
                Vector3 next = transform.position + step;
                if (area.size.sqrMagnitude < 0.01f || area.Contains(next))
                {
                    if (!Physics.SphereCast(transform.position + Vector3.up * 0.6f, 0.3f, step.normalized, out _, step.magnitude + 0.1f,
                                            NoiseSystem.occluderLayers, QueryTriggerInteraction.Ignore))
                        transform.position = next;
                    SnapToGround();
                }
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flat / dist, Vector3.up), 1f - Mathf.Exp(-10f * Time.deltaTime));
                _movedUnseen = true;
                if (_shuffle != null) _shuffle.volume = 0.8f;
            }
            else if (_shuffle != null) _shuffle.volume = 0f;

            // Distance au sol (le Doux est bien plus grand que nous : on ne compare pas les têtes)
            if (reachable && dist < catchDistance && heightAbove > -0.5f && _cooldown <= 0f)
                Catch(target);
        }

        public Transform ScreamBody => transform;
        public Transform ScreamFace => head;
        public AudioClip ScreamClip => jumpscareSound;
        public float ScreamScale => jumpscareFaceScale;

        private void Catch(PlayerTarget player)
        {
            _cooldown = 4f;
            player.Catch(this);
            _pause = pauseAfterCatch;                            // puis il retourne à sa place, dans le rang
        }

        private bool SeenByAnyone()
        {
            Vector3 me = head.position;
            float cos = Mathf.Cos(viewAngle * Mathf.Deg2Rad);
            foreach (PlayerTarget p in PlayerTargets.All)
            {
                if (p == null || p.head == null) continue;
                Vector3 to = me - p.HeadPosition;
                float d = to.magnitude;
                if (d > range * 1.5f) continue;
                if (d < 0.01f || Vector3.Dot(p.head.forward, to / d) < cos) continue;      // pas dans son regard
                if (Physics.Linecast(p.HeadPosition, me, out RaycastHit hit, NoiseSystem.occluderLayers, QueryTriggerInteraction.Ignore)
                    && !hit.transform.IsChildOf(transform) && (hit.point - me).sqrMagnitude > 0.5f) continue;   // un mur entre vous
                return true;
            }
            return false;
        }

        private PlayerTarget NearestPlayer()
        {
            PlayerTarget best = null;
            float bestD = range;
            foreach (PlayerTarget p in PlayerTargets.All)
            {
                if (p == null || SafeZone.Contains(p.HeadPosition)) continue;
                // Joueur en plein jumpscare (mains bloquées) : on l'ignore, sinon le Doux repart
                // vers lui au lieu de rester à sa place dans le rang.
                if (!p.inputEnabled) continue;
                float d = Vector3.Distance(p.HeadPosition, head.position);
                if (d < bestD) { bestD = d; best = p; }
            }
            return best;
        }

        private void SnapToGround()
        {
            if (Physics.Raycast(transform.position + Vector3.up * 1.2f, Vector3.down, out RaycastHit ground, 3f,
                                NoiseSystem.occluderLayers, QueryTriggerInteraction.Ignore))
            {
                Vector3 p = transform.position;
                p.y = Mathf.Lerp(p.y, ground.point.y, 1f - Mathf.Exp(-15f * Time.deltaTime));
                transform.position = p;
            }
        }
    }
}
