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
    /// Tourne la vidéo « POV » en VRAI gameplay, à l'image près (outil de développement, pour TikTok) :
    /// le joueur avance en ramant dans le couloir (ses mains font du bruit), tape un mur (trop fort),
    /// se retourne : L'Écoute arrive en courant, l'attrape (vrai screamer), puis le noir chuchoté.
    /// Rien n'est truqué côté monstre : elle entend les bruits du joueur et réagit avec son IA normale.
    /// Les instants clés (bruits, chasse, screamer, murmure) sont écrits dans evenements.txt pour le montage.
    /// </summary>
    public class PovScareDirector : MonoBehaviour
    {
        public string folder;
        public Vector3 playerStart = new Vector3(-21f, 0.3f, 0f);
        public Vector3 walkDirection = Vector3.left;
        public Vector3 monsterStart = new Vector3(-7f, 0.03f, 0f);
        public int strokes = 4;
        public int whisper = 0;

        public bool Done { get; private set; }

        private readonly List<string> _events = new List<string>();
        private FrameRecorder _rec;

        private void Mark(string what) => _events.Add((_rec != null ? _rec.Frame : 0) + "\t" + what);

        public static PovScareDirector Begin(string folder)
        {
            var d = new GameObject("Réalisateur POV (dev)").AddComponent<PovScareDirector>();
            d.folder = folder;
            return d;
        }

        private IEnumerator Start()
        {
            ArmLocomotion player = FindAnyObjectByType<ArmLocomotion>();
            BlindMonsterAI monster = null;
            foreach (BlindMonsterAI m in FindObjectsByType<BlindMonsterAI>())
            {
                bool basement = m.name.Contains("sous-sol");
                m.enabled = !basement;            // une seule Écoute dans le plan
                if (!basement) monster = m;
            }
            foreach (MonoBehaviour b in FindObjectsByType<MonoBehaviour>())
                if (b.GetType().Name == "SoftieWatcher" || b.GetType().Name == "SheetMonsterAI") b.enabled = false;
            PlaytestBot bot = player.GetComponent<PlaytestBot>() ?? player.gameObject.AddComponent<PlaytestBot>();
            Jumpscare js = player.GetComponent<Jumpscare>();
            js.strobe = false;
            js.forceWhisper = whisper;
            Transform head = player.head;

            // mise en place : le joueur dos à elle, elle au bout du couloir, l'oreille tendue
            player.TeleportTo(playerStart);
            bot.Face(playerStart + walkDirection);
            monster.transform.SetPositionAndRotation(monsterStart, Quaternion.LookRotation(walkDirection));
            monster.state = BlindMonsterAI.State.Ecoute;
            monster.enabled = false;              // figée, l'oreille tendue, tant que le joueur n'a pas fait LE bruit de trop
            yield return new WaitForSeconds(0.4f);

            NoiseSystem.Heard += OnNoise;
            monster.StateChanged += (a, b) => Mark("etat " + b);
            monster.NoiseHeard += (e, h) => Mark("entendu " + h.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " " + monster.state + " @" + monster.transform.position.ToString("F1"));
            _rec = FrameRecorder.Ensure(folder, head.GetComponent<Camera>());
            _rec.fieldOfView = 96f;                 // grand angle « filmé au casque » : on voit ses mains
            yield return null;
            _rec.FollowHead();
            _rec.Wait(30f);
            Mark("debut");

            // 1) il avance, tête un peu baissée (on voit ses mains ramer)
            Vector3 ahead = player.transform.position + walkDirection * 6f + Vector3.down * 0.3f;
            bot.LookAt(ahead);
            bot.Row(Vector3.forward, strokes);
            while (bot.Busy)
            {
                head.rotation = Quaternion.LookRotation(head.position + walkDirection * 4f + Vector3.down * 1.3f - head.position);   // regard un peu baissé : on voit ses mains ramer
                yield return null;
            }

            // 2) trop de bruit : la main claque contre le mur
            Mark("claque");
            Vector3 wall = head.position + Vector3.Cross(Vector3.up, walkDirection) * 1.5f;
            if (Physics.Raycast(head.position, Vector3.Cross(Vector3.up, walkDirection), out RaycastHit wh, 3f, ~0, QueryTriggerInteraction.Ignore))
                wall = wh.point;
            monster.enabled = true;
            bot.Slap(false, wall);
            NoiseSystem.Emit(head.position, 28f, NoiseType.Objet, player.gameObject);   // une claque à pleine main sur le mur
            for (float t = 0f; t < 0.55f; t += Time.deltaTime) yield return null;

            // 3) il se retourne (vite, mais pas un saut d'image)
            Mark("retourne");
            Quaternion from = head.rotation;
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                Quaternion to = Quaternion.LookRotation(monster.Head.position - head.position);
                head.rotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, t / 0.5f));
                yield return null;
            }

            // 4) elle arrive : la tête la suit, un peu en retard (on recule d'effroi)
            float guard = 0f;
            while (!js.IsPlaying && guard < 6f)
            {
                guard += Time.deltaTime;
                Quaternion to = Quaternion.LookRotation(monster.Head.position - head.position);
                head.rotation = Quaternion.Slerp(head.rotation, to, 1f - Mathf.Exp(-8f * Time.deltaTime));
                yield return null;
            }
            Mark("screamer");

            // 5) le screamer, puis le noir chuchoté ; on coupe quand le sous-titre disparaît
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
