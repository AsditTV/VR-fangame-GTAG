using UnityEngine;
using Horror.Interaction;
using Horror.Locomotion;

namespace Horror.Player
{
    /// <summary>
    /// Tutoriel du premier lancement, dans la Salle de jeux (zone sûre). Un panneau flotte devant le joueur et
    /// avance quand il a vraiment fait le geste :
    ///  1. avancer en poussant le sol avec les mains (2,5 m parcourus) ;
    ///  2. sauter en poussant fort vers le bas (le corps monte de 35 cm) ;
    ///  3. la voix (chuchoter / parler / crier) ; 4. la salle sûre et les modes (lus, quelques secondes chacun).
    /// Une fois fini, il ne revient plus (PlayerPrefs). Le bouton TUTO du panneau de confort le relance, ou le passe
    /// s'il est en cours. Textes en anglais, comme le reste du jeu.
    /// </summary>
    public class TutorialCoach : MonoBehaviour
    {
        public Transform panel;
        public TextMesh text;
        public PressButton replayButton;
        public AudioClip stepSound;
        [Tooltip("Distance devant les yeux (m).")]
        public float distance = 1.5f;

        private const string KeyDone = "horror.tuto.done";
        public static bool Done { get => PlayerPrefs.GetInt(KeyDone, 0) == 1; set { PlayerPrefs.SetInt(KeyDone, value ? 1 : 0); PlayerPrefs.Save(); } }

        private static readonly string[] Steps =
        {
            "WELCOME TO THE INSTITUTE\n\nYou have no legs.\nPut your hands on the floor\nand PUSH to move.",
            "GOOD.\n\nPush down HARD and fast\nwith both hands to JUMP.",
            "THEY HEAR YOU.\n\nWhisper: nothing.\nTalk: the close ones listen.\nSCREAM: they ALL come.",
            "THIS PLAYROOM IS SAFE.\n\nPick a game on the wall.\nFind the papers.\nFind the 23rd.",
        };

        public int Step { get; private set; } = -1;
        public bool Running => Step >= 0;

        private ArmLocomotion _loco;
        private Transform _head;
        private Vector3 _stepStart;
        private float _stepTime;

        private void OnEnable() { if (replayButton != null) replayButton.onPressed.AddListener(OnButton); }
        private void OnDisable() { if (replayButton != null) replayButton.onPressed.RemoveListener(OnButton); }

        private void Start()
        {
            _loco = FindAnyObjectByType<ArmLocomotion>();
            _head = _loco != null && _loco.head != null ? _loco.head : (Camera.main != null ? Camera.main.transform : null);
            if (Done) Hide(); else Begin();
        }

        private void OnButton() { if (Running) Finish(); else Begin(); }

        public void Begin()
        {
            Step = -1;
            if (panel != null) panel.gameObject.SetActive(true);
            Next();
            if (panel != null && _head != null) panel.position = Target();   // apparaît directement devant toi
        }

        /// <summary>Passe à l'étape suivante (public pour les tests).</summary>
        public void Next()
        {
            Step++;
            if (Step >= Steps.Length) { Finish(); return; }
            if (text != null) text.text = Steps[Step] + "\n\n(" + (Step + 1) + "/" + Steps.Length + ")";
            _stepStart = _loco != null ? _loco.transform.position : Vector3.zero;
            _stepTime = Time.time;
            if (stepSound != null && _head != null && Step > 0) AudioSource.PlayClipAtPoint(stepSound, _head.position, 0.5f);
        }

        private void Finish()
        {
            Done = true;
            Step = -1;
            Hide();
        }

        private void Hide() { if (panel != null) panel.gameObject.SetActive(false); }

        private Vector3 Target()
        {
            Vector3 fwd = Vector3.ProjectOnPlane(_head.forward, Vector3.up);
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.ProjectOnPlane(_head.up, Vector3.up);
            return _head.position + fwd.normalized * distance - Vector3.up * 0.1f;
        }

        private void Update()
        {
            if (!Running || _head == null) return;
            // le panneau suit le regard sans coller (il glisse doucement, pas de nausée)
            if (panel != null)
            {
                panel.position = Vector3.Lerp(panel.position, Target(), 1f - Mathf.Exp(-1.5f * Time.unscaledDeltaTime));
                Vector3 look = panel.position - _head.position; look.y = 0f;
                if (look.sqrMagnitude > 0.001f) panel.rotation = Quaternion.LookRotation(look, Vector3.up);
            }
            if (_loco == null) return;
            Vector3 moved = _loco.transform.position - _stepStart;
            float t = Time.time - _stepTime;
            switch (Step)
            {
                case 0: if (new Vector2(moved.x, moved.z).magnitude > 2.5f) Next(); break;   // il a avancé avec ses mains
                case 1: if (moved.y > 0.35f || t > 25f) Next(); break;                       // il a sauté (ou passe au bout de 25 s)
                case 2: if (t > 7f) Next(); break;
                case 3: if (t > 8f) Next(); break;
            }
        }
    }
}
