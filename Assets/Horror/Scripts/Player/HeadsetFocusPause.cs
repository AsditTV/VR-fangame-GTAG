using System;
using System.Collections.Generic;
using UnityEngine;
using Horror.Locomotion;
using Horror.Net;

namespace Horror.Player
{
    /// <summary>
    /// Exigence du store Meta : quand le joueur ouvre le menu du casque (bouton Meta) ou retire le casque,
    /// le jeu perd le « focus ». Alors :
    ///  - en SOLO, le jeu se met en pause (temps figé, sons coupés) : les monstres ne t'attrapent pas pendant que
    ///    tu règles ton volume ;
    ///  - EN LIGNE, on ne peut pas figer le monde des autres : seul ton personnage arrête de bouger ;
    ///  - dans les deux cas, tes mains sont cachées (le système affiche les siennes) et les bras ne déplacent plus.
    /// Créé tout seul au chargement de la scène : rien à poser à la main.
    /// Dans l'éditeur, cliquer hors de la fenêtre Game ne met pas en pause (sinon les tests s'arrêtent) :
    /// cocher <see cref="pauseInEditor"/> pour l'essayer.
    /// </summary>
    public class HeadsetFocusPause : MonoBehaviour
    {
        public static bool Paused { get; private set; }
        public static event Action<bool> PausedChanged;

        [Tooltip("Mettre aussi en pause dans l'éditeur quand la fenêtre perd le focus.")]
        public bool pauseInEditor;

        private float _timeScale = 1f;
        private bool _frozeTime;
        private readonly List<Renderer> _hidden = new List<Renderer>();
        private readonly List<ArmLocomotion> _stopped = new List<ArmLocomotion>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (FindAnyObjectByType<HeadsetFocusPause>() != null) return;
            var go = new GameObject("Pause (menu du casque)");
            DontDestroyOnLoad(go);
            go.AddComponent<HeadsetFocusPause>();
        }

        private void OnApplicationFocus(bool focus) => SetPaused(!focus);
        private void OnApplicationPause(bool pause) => SetPaused(pause);

        /// <summary>Met en pause (ou reprend). Public pour les tests.</summary>
        public void SetPaused(bool pause)
        {
            if (Application.isEditor && !pauseInEditor && pause) return;
            if (pause == Paused) return;
            Paused = pause;
            bool online = NetworkBootstrap.Session != null && NetworkBootstrap.Session.IsOnline;
            if (pause)
            {
                if (!online) { _timeScale = Time.timeScale; Time.timeScale = 0f; _frozeTime = true; }
                AudioListener.pause = !online;
                foreach (ArmLocomotion loco in FindObjectsByType<ArmLocomotion>())
                {
                    if (loco.inputEnabled) { loco.inputEnabled = false; _stopped.Add(loco); }
                    Hide(loco.leftHandVisual); Hide(loco.rightHandVisual);
                }
            }
            else
            {
                if (_frozeTime) { Time.timeScale = _timeScale > 0f ? _timeScale : 1f; _frozeTime = false; }
                AudioListener.pause = false;
                foreach (ArmLocomotion loco in _stopped) if (loco != null) loco.inputEnabled = true;
                _stopped.Clear();
                foreach (Renderer r in _hidden) if (r != null) r.enabled = true;
                _hidden.Clear();
            }
            PausedChanged?.Invoke(pause);
        }

        private void Hide(Transform hand)
        {
            if (hand == null) return;
            foreach (Renderer r in hand.GetComponentsInChildren<Renderer>())
                if (r.enabled) { r.enabled = false; _hidden.Add(r); }
        }
    }
}
