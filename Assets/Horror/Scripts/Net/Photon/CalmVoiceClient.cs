#if FUSION2 && FUSION_WEAVER   // compilé seulement quand Photon Fusion 2 + Photon Voice sont installés
using Photon.Voice.Fusion;
using Photon.Voice.Unity;
using UnityEngine;
using Horror.Player;

namespace Horror.Net
{
    /// <summary>
    /// Le chat vocal de proximité (Photon Voice), qui suit la salle Fusion : même salle, « _voice » en plus.
    /// - Mon micro : un Recorder (micro Unity), qui envoie mon identifiant stable avec ma voix ; il respecte le mode
    ///   micro du Minitel (ouvert / appuyer pour parler / coupé). Il fournit aussi le niveau de ma voix à MicMouth
    ///   (un seul programme peut ouvrir le micro sur Quest) : ma bouche et le bruit pour les monstres en dépendent.
    /// - Les voix des autres : une source 3D par joueur, inscrite dans VoiceSpeakers ; NetworkAvatars la colle sur
    ///   la tête de son avatar et applique couper / bloquer / mur.
    /// L'App ID Voice est lu par Photon dans PhotonAppSettings, jamais ici.
    /// </summary>
    public class CalmVoiceClient : FusionVoiceClient
    {
        private Recorder _recorder;
        private MicMouth _localMouth;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register() => PhotonFusionSession.AfterJoin += Attach;

        /// <summary>
        /// Posé sur l'objet du NetworkRunner une fois la salle rejointe : à son démarrage, la voix voit que Fusion est déjà
        /// dans une salle et rejoint « salle_voice » (si on la pose avant, elle attend un rappel que Fusion ne lui donne pas).
        /// </summary>
        private static void Attach(GameObject runnerGo, string myUserId)
        {
            var rec = runnerGo.AddComponent<Recorder>();
            rec.SourceType = Recorder.InputSourceType.Microphone;
            rec.MicrophoneType = Recorder.MicType.Unity;
            rec.UserData = myUserId;                                     // pour savoir à qui est cette voix
            rec.TransmitEnabled = true;
            rec.RecordWhenJoined = true;
            var vc = runnerGo.AddComponent<CalmVoiceClient>();
            vc._recorder = rec;
            vc.PrimaryRecorder = rec;
            MicMouth.ExternalLevel = () => rec != null && rec.LevelMeter != null ? rec.LevelMeter.CurrentAvgAmp : 0f;
        }

        protected override void Start()
        {
            base.Start();
            if (_recorder != null) AddRecorder(_recorder);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            MicMouth.ExternalLevel = null;                               // hors ligne : MicMouth rouvre son micro
        }

        protected override void Update()
        {
            base.Update();                                               // la boucle de Photon Voice (indispensable)
            if (_recorder == null) return;
            if (_localMouth == null)
                foreach (MicMouth m in FindObjectsByType<MicMouth>()) if (m.isLocal) { _localMouth = m; break; }
            // MicModeApplier décide (Minitel : ouvert / appuyer pour parler / coupé) ; la voix suit
            bool open = _localMouth == null || _localMouth.micEnabled;
            if (_recorder.TransmitEnabled != open) _recorder.TransmitEnabled = open;
        }

        protected override Speaker InstantiateSpeakerForRemoteVoice(int playerId, byte voiceId, object userData)
        {
            if (!(userData is string uid) || string.IsNullOrEmpty(uid))
                return base.InstantiateSpeakerForRemoteVoice(playerId, voiceId, userData);
            var go = new GameObject("Voix de " + uid.Substring(0, Mathf.Min(6, uid.Length)));
            go.transform.SetParent(transform, false);
            AudioSource src = go.AddComponent<AudioSource>();
            src.spatialBlend = 1f; src.dopplerLevel = 0f; src.playOnAwake = false;
            Speaker sp = go.AddComponent<Speaker>();
            sp.OnRemoteVoiceRemoveAction = s => { VoiceSpeakers.Unregister(uid); if (s != null) Destroy(s.gameObject); };
            VoiceSpeakers.Register(uid, src);
            return sp;
        }
    }
}
#endif
