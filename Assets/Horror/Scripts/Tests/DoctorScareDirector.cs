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
    /// Vidéo TikTok du Docteur, en VRAI gameplay (outil de développement) : allongé dans un lit de la salle des
    /// malades (caché, immobile), on l'entend fredonner ; il entre, sa lanterne balaie les lits, il passe sans nous
    /// voir. Puis notre main fait tomber la perfusion : fracas. On se redresse (on sort de la cachette), il se tait,
    /// claque du bec, fonce -> screamer -> noir chuchoté. Ses déplacements sont mis en scène (GoTo), mais ce qu'il
    /// voit, entend et attrape, c'est son IA normale. Instants clés dans evenements.txt (Tools/Trailer/make_docteur.py).
    /// </summary>
    public class DoctorScareDirector : MonoBehaviour
    {
        public string folder;
        public Vector3 bed = new Vector3(35f, -13f, -69.2f);
        public Vector3 doctorStart = new Vector3(30.5f, -14f, -54f);
        public Vector3[] route = { new Vector3(34f, -14f, -58.5f), new Vector3(42.5f, -14f, -64f), new Vector3(43.5f, -14f, -74f) };
        public Vector3 drip = new Vector3(37.2f, -14f, -68.6f);
        public int whisper = 8;
        public bool Done { get; private set; }

        private readonly List<string> _events = new List<string>();
        private FrameRecorder _rec;
        private Transform _head;
        private DoctorAI _doc;

        private void Mark(string what) => _events.Add((_rec != null ? _rec.Frame : 0) + "\t" + what);

        public static DoctorScareDirector Begin(string folder)
        {
            var d = new GameObject("Réalisateur Docteur (dev)").AddComponent<DoctorScareDirector>();
            d.folder = folder;
            return d;
        }

        private Quaternion LookAtDoc(float down = 0f) => Quaternion.LookRotation(_doc.head.position + Vector3.down * down - _head.position, Vector3.up);

        private IEnumerator Watch(float seconds, System.Func<bool> until, float lag = 2.5f)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                if (until != null && until()) yield break;
                _head.rotation = Quaternion.Slerp(_head.rotation, LookAtDoc(0.3f), 1f - Mathf.Exp(-lag * Time.deltaTime));
                yield return null;
            }
        }

        private IEnumerator Start()
        {
            ArmLocomotion player = FindAnyObjectByType<ArmLocomotion>();
            foreach (MonoBehaviour b in FindObjectsByType<MonoBehaviour>())
            {
                string tn = b.GetType().Name;
                if (tn == "BlindMonsterAI" || tn == "SoftieWatcher" || tn == "SheetMonsterAI" || tn == "BlackoutDirector") b.enabled = false;
            }
            _doc = FindAnyObjectByType<DoctorAI>();
            _doc.enabled = true;
            PlaytestBot bot = player.GetComponent<PlaytestBot>() ?? player.gameObject.AddComponent<PlaytestBot>();
            Jumpscare js = player.GetComponent<Jumpscare>();
            js.strobe = true; js.forceWhisper = whisper;
            _head = player.head;

            player.TeleportTo(bed);
            bot.Face(bed + Vector3.right);
            _head.localPosition = new Vector3(0f, 0.3f, 0f);                // allongé : la tête reste dans la cachette du lit
            var body = new List<Renderer>();                                 // la caméra serait dans son propre corps : on le masque
            foreach (Renderer r in player.GetComponentsInChildren<Renderer>()) if (r.enabled) { r.enabled = false; body.Add(r); }
            _doc.transform.SetPositionAndRotation(doctorStart, Quaternion.LookRotation(Vector3.back));
            yield return new WaitForSeconds(0.6f);

            _doc.StateChangedHook = s => Mark("etat " + s);
            _rec = FrameRecorder.Ensure(folder, _head.GetComponent<Camera>());
            _rec.fieldOfView = 92f; _rec.followSmoothing = 0.15f; _rec.followTurnSmoothing = 0.1f;
            yield return null;
            _head.rotation = Quaternion.LookRotation(new Vector3(44f, -12.2f, -66f) - _head.position);   // vers l'allée
            _rec.FollowHead(); _rec.Wait(90f);
            Mark("debut");

            // 1) il arrive : on l'entend fredonner, on fixe la porte
            _doc.GoTo(route[0]);
            yield return Watch(12f, () => _doc.IsNear(route[0]), 0.6f);
            Mark("entre");
            // 2) il longe l'allée : sa lanterne passe sur les lits... sur le nôtre
            for (int i = 1; i < route.Length; i++)
            {
                _doc.GoTo(route[i]);
                yield return Watch(12f, () => _doc.IsNear(route[i]), 2.2f);
                if (i == 1) Mark("passe");
            }
            Mark("loin");
            for (float t = 0f; t < 1.1f; t += Time.deltaTime) yield return null;   // on souffle

            // 3) la main heurte la perfusion
            FragileNoisy best = null; float bd = float.MaxValue;
            foreach (FragileNoisy f in FindObjectsByType<FragileNoisy>())
            {
                float d = (f.transform.position - drip).sqrMagnitude;
                if (d < bd) { bd = d; best = f; }
            }
            Quaternion toDrip = Quaternion.LookRotation(drip + Vector3.up * 0.7f - _head.position);
            for (float t = 0f; t < 0.35f; t += Time.deltaTime) { _head.rotation = Quaternion.Slerp(_head.rotation, toDrip, 1f - Mathf.Exp(-9f * Time.deltaTime)); yield return null; }
            if (best != null) best.KnockNow();
            Mark("fracas");
            for (float t = 0f; t < 0.7f; t += Time.deltaTime) { _head.rotation = Quaternion.Slerp(_head.rotation, toDrip * Quaternion.Euler(20f, 0f, 0f), 1f - Mathf.Exp(-4f * Time.deltaTime)); yield return null; }

            // 4) on se redresse (hors de la cachette) et on le cherche des yeux
            Mark("redresse");
            foreach (Renderer r in body) if (r != null) r.enabled = true;
            Vector3 low = _head.localPosition, up = new Vector3(0f, 1.15f, 0f);
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                _head.localPosition = Vector3.Lerp(low, up, Mathf.SmoothStep(0f, 1f, t / 0.5f));
                _head.rotation = Quaternion.Slerp(_head.rotation, LookAtDoc(), 1f - Mathf.Exp(-6f * Time.deltaTime));
                yield return null;
            }
            _rec.followTurnSmoothing = 0f;
            yield return Watch(14f, () => js.IsPlaying, 7f);
            _rec.followSmoothing = 0f;
            Mark("screamer");
            bool seen = false;
            for (float t = 0f; t < 8f; t += Time.deltaTime)
            {
                if (js.ShowingWhisper && !seen) { seen = true; Mark("murmure"); }
                if (seen && !js.ShowingWhisper) break;
                yield return null;
            }
            Mark("fin");
            File.WriteAllLines(Path.Combine(folder, "evenements.txt"), _events);
            Destroy(_rec.gameObject);
            js.forceWhisper = -1;
            _head.localPosition = new Vector3(0f, bot.headHeight, 0f);
            _doc.StateChangedHook = null;
            Done = true;
        }
    }
}
