using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Horror.Locomotion;
using Horror.Monsters;
using Horror.Noise;
using Horror.Player;

namespace Horror.Tests
{
    /// <summary>
    /// Tourne la vidéo « POV » en VRAI gameplay, à l'image près (outil de développement, pour TikTok), en 4 temps :
    ///  1. on avance doucement dans le couloir (mains qui rament, vue stabilisée) ;
    ///  2. on jette un œil derrière soi : elle est là-bas, immobile, l'oreille tendue... on repart ;
    ///  3. la main claque contre le mur : un silence... puis ses clés qui approchent, on n'ose pas se retourner ;
    ///  4. on se retourne quand elle est tout près -> vrai screamer (son IA l'a amenée là) -> le noir chuchoté.
    /// Les instants clés sont écrits dans evenements.txt pour caler le son au montage (Tools/Trailer/make_pov.py).
    /// </summary>
    public class PovScareDirector : MonoBehaviour
    {
        public string folder;
        public Vector3 playerStart = new Vector3(-14f, 0.3f, 0f);
        public Vector3 walkDirection = Vector3.left;
        public Vector3 monsterStart = new Vector3(-9.2f, 0.03f, 0f);
        public int strokesBefore = 4, strokesAfter = 4;
        [Tooltip("Distance (m) à laquelle on se retourne enfin.")]
        public float turnDistance = 3.2f;
        public int whisper = 0;

        public bool Done { get; private set; }

        private readonly List<string> _events = new List<string>();
        private FrameRecorder _rec;
        private Transform _head;
        private ArmLocomotion _player;

        private void Mark(string what) => _events.Add((_rec != null ? _rec.Frame : 0) + "\t" + what);

        public static PovScareDirector Begin(string folder)
        {
            var d = new GameObject("Réalisateur POV (dev)").AddComponent<PovScareDirector>();
            d.folder = folder;
            return d;
        }

        /// <summary>Regard vers l'avant, un peu baissé, avec le petit balancement d'une vraie tête.</summary>
        private Quaternion LookAhead(float t, float down = 1.1f)
        {
            Vector3 target = _head.position + walkDirection * 4f + Vector3.down * down
                             + Vector3.Cross(Vector3.up, walkDirection) * (Mathf.Sin(t * 1.3f) * 0.25f);
            return Quaternion.LookRotation(target - _head.position);
        }

        private IEnumerator TurnTo(System.Func<Quaternion> target, float seconds)
        {
            Quaternion from = _head.rotation;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                _head.rotation = Quaternion.Slerp(from, target(), Mathf.SmoothStep(0f, 1f, t / seconds));
                yield return null;
            }
            _head.rotation = target();
        }

        private IEnumerator Walk(PlaytestBot bot, int strokes, float clock)
        {
            bot.Row(Vector3.forward, strokes);
            float t = clock;
            while (bot.Busy)
            {
                t += Time.deltaTime;
                _head.rotation = Quaternion.Slerp(_head.rotation, LookAhead(t), 1f - Mathf.Exp(-6f * Time.deltaTime));
                yield return null;
            }
        }

        private IEnumerator Start()
        {
            _player = FindAnyObjectByType<ArmLocomotion>();
            BlindMonsterAI monster = null;
            foreach (BlindMonsterAI m in FindObjectsByType<BlindMonsterAI>())
            {
                bool basement = m.name.Contains("sous-sol");
                m.enabled = !basement;
                if (!basement) monster = m;
            }
            foreach (MonoBehaviour b in FindObjectsByType<MonoBehaviour>())
                if (b.GetType().Name == "SoftieWatcher" || b.GetType().Name == "SheetMonsterAI") b.enabled = false;
            PlaytestBot bot = _player.GetComponent<PlaytestBot>();
            if (bot == null) bot = _player.gameObject.AddComponent<PlaytestBot>();
            Jumpscare js = _player.GetComponent<Jumpscare>();
            js.strobe = true;
            js.forceWhisper = whisper;
            _head = _player.head;

            // mise en place : dos à elle ; elle au début du couloir, figée, l'oreille tendue
            _player.TeleportTo(playerStart);
            bot.Face(playerStart + walkDirection);
            monster.transform.SetPositionAndRotation(monsterStart, Quaternion.LookRotation(walkDirection));
            monster.state = BlindMonsterAI.State.Ecoute;
            monster.enabled = false;
            yield return new WaitForSeconds(0.4f);

            NoiseSystem.Heard += OnNoise;
            monster.StateChanged += (a, b) => Mark("etat " + b);
            monster.NoiseHeard += (e, h) => Mark("entendu " + h.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " dist " + Vector3.Distance(monster.transform.position, e.position).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
            _rec = FrameRecorder.Ensure(folder, _head.GetComponent<Camera>());
            _rec.fieldOfView = 96f;
            _rec.followSmoothing = 0.22f;
            _rec.followTurnSmoothing = 0.08f;
            yield return null;
            _head.rotation = LookAhead(0f);
            _rec.FollowHead();
            _rec.Wait(60f);
            Mark("debut");

            // 1) on avance, doucement
            yield return Walk(bot, strokesBefore, 0f);

            // 2) un regard par-dessus l'épaule : elle est là-bas, immobile
            Mark("regard");
            for (float t = 0f; t < 0.35f; t += Time.deltaTime) yield return null;
            yield return TurnTo(() => Quaternion.LookRotation(monster.Head.position + Vector3.down * 0.2f - _head.position), 1.1f);
            Mark("la_voit");
            for (float t = 0f; t < 1.5f; t += Time.deltaTime) yield return null;
            yield return TurnTo(() => LookAhead(2f), 0.8f);
            Mark("repart");

            // 3) on repart... et la main claque contre le mur
            yield return Walk(bot, strokesAfter, 3f);
            Mark("claque");
            Vector3 side = Vector3.Cross(Vector3.up, walkDirection);
            Vector3 wall = _head.position + side * 1.5f;
            if (Physics.Raycast(_head.position, side, out RaycastHit wh, 3f, ~0, QueryTriggerInteraction.Ignore)) wall = wh.point;
            bot.Slap(false, wall);
            monster.enabled = true;
            NoiseSystem.Emit(_head.position, 28f, NoiseType.Objet, _player.gameObject);

            // ... on se fige, on n'ose pas se retourner, pendant qu'elle approche
            float clock = 5f, guard = 0f;
            while (Vector3.Distance(monster.transform.position, _player.transform.position) > turnDistance && guard < 6f)
            {
                guard += Time.deltaTime; clock += Time.deltaTime;
                Quaternion still = LookAhead(clock * 0.3f, 0.6f) * Quaternion.Euler(Random.Range(-0.4f, 0.4f), Random.Range(-0.4f, 0.4f), 0f);
                _head.rotation = Quaternion.Slerp(_head.rotation, still, 1f - Mathf.Exp(-4f * Time.deltaTime));
                yield return null;
            }

            // 4) on se retourne
            Mark("retourne");
            _rec.followTurnSmoothing = 0f;
            yield return TurnTo(() => Quaternion.LookRotation(monster.Head.position - _head.position), 0.35f);
            guard = 0f;
            while (!js.IsPlaying && guard < 4f)
            {
                guard += Time.deltaTime;
                _head.rotation = Quaternion.Slerp(_head.rotation, Quaternion.LookRotation(monster.Head.position - _head.position),
                                                  1f - Mathf.Exp(-10f * Time.deltaTime));
                yield return null;
            }
            _rec.followSmoothing = 0f;          // le screamer est collé à la tête : pas de retard de caméra
            Mark("screamer");

            bool seen = false;
            for (float t = 0f; t < 8f; t += Time.deltaTime)
            {
                if (js.ShowingWhisper && !seen) { seen = true; Mark("murmure"); }
                if (seen && !js.ShowingWhisper) break;
                yield return null;
            }
            Mark("fin");
            NoiseSystem.Heard -= OnNoise;
            File.WriteAllLines(Path.Combine(folder, "evenements.txt"), _events);
            Destroy(_rec.gameObject);
            js.forceWhisper = -1;
            Done = true;
        }

        private void OnNoise(NoiseEvent e) => Mark("bruit " + e.type + " " + e.radius.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
    }
}
