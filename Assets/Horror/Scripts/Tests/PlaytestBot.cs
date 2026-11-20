using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Horror.Locomotion;

namespace Horror.Tests
{
    /// <summary>
    /// « Joueur robot » pour tester le jeu SANS casque, en mode Play dans l'éditeur.
    ///
    /// Sans casque, XRNodePoseDriver ne touche plus à la Tête ni aux Manettes : ce script les
    /// bouge à la place, comme le ferait un vrai joueur (ramer au sol, taper un mur, appuyer sur
    /// un bouton, regarder quelque chose). Le reste du jeu (locomotion, bruit, monstres, portes)
    /// réagit exactement comme avec le casque, ce qui permet de vérifier la logique.
    ///
    /// Ne fait rien tout seul : on l'ajoute sur l'objet "Joueur" pendant un test et on appelle
    /// ses méthodes (depuis un script d'éditeur ou la console de test). À ne pas laisser dans une scène.
    /// </summary>
    [RequireComponent(typeof(ArmLocomotion))]
    public class PlaytestBot : MonoBehaviour
    {
        [Tooltip("Hauteur de la tête simulée (m). Une poupée d'environ 1 m : tête vers 1,1 m.")]
        public float headHeight = 1.2f;

        /// <summary>Vrai tant qu'une action est en cours.</summary>
        public bool Busy { get; private set; }

        /// <summary>Journal des actions (pour relire ce qui s'est passé).</summary>
        public readonly List<string> log = new List<string>();

        private ArmLocomotion _loco;
        private Transform _head, _left, _right;

        // Position de repos des manettes, dans le repère du joueur.
        private static readonly Vector3 RestLeft = new Vector3(-0.25f, 0.8f, 0.25f);
        private static readonly Vector3 RestRight = new Vector3(0.25f, 0.8f, 0.25f);

        private void Awake()
        {
            _loco = GetComponent<ArmLocomotion>();
            _head = _loco.head;
            _left = _loco.leftController;
            _right = _loco.rightController;
            _head.localPosition = new Vector3(0f, headHeight, 0f);
            _left.localPosition = RestLeft;
            _right.localPosition = RestRight;
        }

        // ------------------------------------------------------------------ actions publiques

        /// <summary>Rame au sol avec les deux mains pour avancer dans la direction donnée (repère du joueur).</summary>
        public void Row(Vector3 localDirection, int strokes = 4) => Run(RowCo(localDirection.normalized, strokes));

        /// <summary>Tape fort une surface avec une main (bruit d'impact).</summary>
        public void Slap(bool leftHand, Vector3 worldPoint) => Run(SlapCo(leftHand ? _left : _right, worldPoint));

        /// <summary>Approche lentement la main droite d'un point (bouton, touche du Minitel...) puis la retire.</summary>
        public void Press(Vector3 worldPoint) => Run(PressCo(worldPoint));

        /// <summary>Appuie sur un bouton en arrivant dans son axe : la main se place devant la face
        /// (côté <paramref name="faceNormal"/>), puis pousse vers l'intérieur. Marche aussi pour une touche
        /// tournée vers le haut (Minitel) ou un bouton bas (boîte de crayons).</summary>
        public void PressAlong(Vector3 worldPoint, Vector3 faceNormal) => Run(PressAlongCo(worldPoint, faceNormal.normalized));

        /// <summary>Tourne la tête vers un point (sert aussi pour les Doux : regarder / détourner les yeux).</summary>
        public void LookAt(Vector3 worldPoint)
        {
            Vector3 dir = worldPoint - _head.position;
            if (dir.sqrMagnitude > 1e-6f) _head.rotation = Quaternion.LookRotation(dir);
            Note("regarde " + worldPoint);
        }

        /// <summary>Tourne tout le corps (le joueur pivote) vers une direction horizontale.</summary>
        public void Face(Vector3 worldPoint)
        {
            Vector3 flat = worldPoint - transform.position; flat.y = 0f;
            if (flat.sqrMagnitude > 1e-6f)
            {
                // Le Rigidbody interpolé réécrit transform.rotation : il faut tourner le Rigidbody aussi.
                Quaternion q = Quaternion.LookRotation(flat);
                Rigidbody rb = GetComponent<Rigidbody>();
                if (rb != null) rb.rotation = q;
                transform.rotation = q;
            }
            _head.localRotation = Quaternion.identity;
            Note("se tourne vers " + worldPoint);
        }

        // ------------------------------------------------------------------ coroutines

        private void Run(IEnumerator routine)
        {
            StopAllCoroutines();
            StartCoroutine(Wrap(routine));
        }

        private IEnumerator Wrap(IEnumerator routine)
        {
            Busy = true;
            yield return routine;
            Busy = false;
        }

        private IEnumerator RowCo(Vector3 dir, int strokes)
        {
            Vector3 start = transform.position;
            Vector3 side = Vector3.Cross(Vector3.up, dir).normalized * 0.25f;
            for (int i = 0; i < strokes; i++)
            {
                // 1) mains devant, en l'air
                Vector3 frontL = -side + dir * 0.45f + Vector3.up * 0.5f, frontR = side + dir * 0.45f + Vector3.up * 0.5f;
                yield return MoveBoth(frontL, frontR, 0.15f);
                // 2) on pose les mains au sol (un peu sous le niveau du sol pour être sûr de toucher)
                yield return MoveBoth(frontL - Vector3.up * 0.6f, frontR - Vector3.up * 0.6f, 0.12f);
                // 3) on tire vers l'arrière : la main reste collée, c'est le corps qui avance
                yield return MoveBoth(-side - dir * 0.35f - Vector3.up * 0.1f, side - dir * 0.35f - Vector3.up * 0.1f, 0.3f);
                // 4) on relève les mains (ça décolle)
                yield return MoveBoth(-side - dir * 0.3f + Vector3.up * 0.6f, side - dir * 0.3f + Vector3.up * 0.6f, 0.12f);
            }
            yield return MoveBoth(RestLeft, RestRight, 0.2f);
            Note($"rame x{strokes} : déplacé de {Vector3.Distance(start, transform.position):0.00} m");
        }

        private IEnumerator SlapCo(Transform hand, Vector3 worldPoint)
        {
            Vector3 rest = hand == _left ? RestLeft : RestRight;
            Vector3 target = transform.InverseTransformPoint(worldPoint);
            Vector3 approach = Vector3.Lerp(rest, target, 0.5f);
            yield return MoveOne(hand, approach, 0.2f);
            // coup rapide : on vise un peu au-delà de la surface (~4 m/s)
            Vector3 beyond = target + (target - approach).normalized * 0.1f;
            yield return MoveOne(hand, beyond, 0.06f);
            yield return MoveOne(hand, approach, 0.15f);
            yield return MoveOne(hand, rest, 0.2f);
            Note("tape " + worldPoint);
        }

        private IEnumerator PressCo(Vector3 worldPoint)
        {
            // Comme un vrai joueur : la main monte à hauteur du bouton, en retrait, puis pousse tout droit.
            Vector3 target = transform.InverseTransformPoint(worldPoint);
            Vector3 flat = target - RestRight; flat.y = 0f;
            Vector3 push = flat.sqrMagnitude > 1e-6f ? flat.normalized : Vector3.forward;
            yield return MoveOne(_right, target - push * 0.2f, 0.3f);
            yield return MoveOne(_right, target + push * 0.05f, 0.25f);
            yield return new WaitForSeconds(0.2f);
            yield return MoveOne(_right, RestRight, 0.3f);
            Note("appuie sur " + worldPoint);
        }

        private IEnumerator PressAlongCo(Vector3 worldPoint, Vector3 faceNormal)
        {
            Vector3 front = transform.InverseTransformPoint(worldPoint + faceNormal * 0.2f);
            Vector3 inside = transform.InverseTransformPoint(worldPoint - faceNormal * 0.05f);
            // le poignet tourne : les doigts (avant de la manette) pointent vers le bouton
            _right.rotation = Quaternion.LookRotation(-faceNormal, Mathf.Abs(faceNormal.y) > 0.9f ? transform.forward : Vector3.up);
            // la main fait le tour par au-dessus pour ne pas accrocher le meuble en chemin
            yield return MoveOne(_right, new Vector3(front.x, Mathf.Max(front.y, RestRight.y), front.z), 0.25f);
            yield return MoveOne(_right, front, 0.2f);
            yield return MoveOne(_right, inside, 0.25f);
            yield return new WaitForSeconds(0.2f);
            yield return MoveOne(_right, front, 0.2f);
            yield return MoveOne(_right, RestRight, 0.25f);
            _right.localRotation = Quaternion.identity;
            Note("appuie (axe) sur " + worldPoint);
        }

        private IEnumerator MoveBoth(Vector3 toLeft, Vector3 toRight, float seconds)
        {
            Vector3 fromL = _left.localPosition, fromR = _right.localPosition;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / seconds);
                _left.localPosition = Vector3.Lerp(fromL, toLeft, k);
                _right.localPosition = Vector3.Lerp(fromR, toRight, k);
                yield return null;
            }
            _left.localPosition = toLeft;
            _right.localPosition = toRight;
        }

        private IEnumerator MoveOne(Transform hand, Vector3 to, float seconds)
        {
            Vector3 from = hand.localPosition;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                hand.localPosition = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / seconds));
                yield return null;
            }
            hand.localPosition = to;
        }

        private void Note(string message)
        {
            string line = $"[{Time.time:0.0}s] {message}";
            log.Add(line);
            Debug.Log("[PlaytestBot] " + line);
        }
    }
}
