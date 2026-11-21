using Horror.Player;
using UnityEngine;

namespace Horror.Interaction
{
    /// <summary>
    /// Le Minitel de l'accueil : l'ordinateur des réglages (un terminal de réglages dans le lobby, version 1985).
    /// Quatre grosses touches qu'on enfonce du doigt : ◄  ►  OK  BACK.
    /// Écrans (textes en anglais, vert sur noir) :
    ///   ROOM  : code de salle privée (le multijoueur arrive à l'étape 6 : le code est déjà mémorisé)
    ///   NAME  : pseudo, lettre par lettre (◄ ► choisir, OK ajouter, BACK effacer)
    ///   COLOR : couleur du tissu de ta poupée
    ///   MIC   : OPEN / PUSH TO TALK (A ou X) / MUTED
    ///   HELP  : rappel des commandes
    /// </summary>
    public class MinitelTerminal : MonoBehaviour
    {
        public TextMesh screen;
        public PressButton left, right, ok, back;
        public AudioClip beep;

        private enum Page { Menu, Room, Name, Color, Mic, Help }
        private static readonly string[] MenuItems = { "ROOM", "NAME", "COLOR", "MIC", "HELP" };
        private const string Letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 ";
        private const string Digits = "0123456789";

        private Page _page = Page.Menu;
        private int _cursor;          // élément du menu, ou lettre/chiffre choisi
        private string _draft = "";   // pseudo ou code en cours de saisie
        private AudioSource _audio;

        private void Awake()
        {
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.spatialBlend = 1f;
            _audio.maxDistance = 6f;
            _audio.playOnAwake = false;
        }

        private void OnEnable()
        {
            Hook(left, OnLeft); Hook(right, OnRight); Hook(ok, OnOk); Hook(back, OnBack);
            Render();
        }

        private void OnDisable()
        {
            Unhook(left, OnLeft); Unhook(right, OnRight); Unhook(ok, OnOk); Unhook(back, OnBack);
        }

        // ------------------------------------------------------------------ touches

        private void OnLeft() { Step(-1); }
        private void OnRight() { Step(+1); }

        private void Step(int d)
        {
            switch (_page)
            {
                case Page.Menu: _cursor = Wrap(_cursor + d, MenuItems.Length); break;
                case Page.Name: _cursor = Wrap(_cursor + d, Letters.Length); break;
                case Page.Room: _cursor = Wrap(_cursor + d, Digits.Length); break;
                case Page.Color: GameSettings.ColorIndex += d; break;
                case Page.Mic: GameSettings.Mic = (MicMode)Wrap((int)GameSettings.Mic + d, 3); break;
            }
            Beep(); Render();
        }

        private void OnOk()
        {
            switch (_page)
            {
                case Page.Menu:
                    _page = (Page)(_cursor + 1);
                    _cursor = 0;
                    _draft = _page == Page.Name ? GameSettings.Name : _page == Page.Room ? GameSettings.RoomCode : "";
                    break;
                case Page.Name:
                    if (_draft.Length < 12) _draft += Letters[_cursor];
                    GameSettings.Name = _draft;
                    break;
                case Page.Room:
                    if (_draft.Length < 6) _draft += Digits[_cursor];
                    GameSettings.RoomCode = _draft;
                    break;
                default:
                    _page = Page.Menu;
                    break;
            }
            Beep(); Render();
        }

        private void OnBack()
        {
            if ((_page == Page.Name || _page == Page.Room) && _draft.Length > 0)
            {
                _draft = _draft.Substring(0, _draft.Length - 1);
                if (_page == Page.Name) GameSettings.Name = _draft.Length > 0 ? _draft : "FACE";
                else GameSettings.RoomCode = _draft;
            }
            else
            {
                _page = Page.Menu;
                _cursor = 0;
            }
            Beep(); Render();
        }

        // ------------------------------------------------------------------ écran

        private void Render()
        {
            if (screen == null) return;
            var sb = new System.Text.StringBuilder();
            sb.Append("MARIVAUX TELEMATIQUE\n").Append(GameNames.Game).Append("\n--------------------\n");
            switch (_page)
            {
                case Page.Menu:
                    for (int i = 0; i < MenuItems.Length; i++)
                        sb.Append(i == _cursor ? "> " : "  ").Append(MenuItems[i]).Append('\n');
                    sb.Append("\n< > SELECT   OK ENTER");
                    break;
                case Page.Name:
                    sb.Append("NAME: ").Append(_draft).Append("_\n\nLETTER: < ").Append(Letters[_cursor] == ' ' ? "SPACE" : Letters[_cursor].ToString())
                      .Append(" >\n\nOK ADD   BACK ERASE");
                    break;
                case Page.Room:
                    sb.Append("PRIVATE ROOM CODE:\n").Append(_draft).Append("_\n\nDIGIT: < ").Append(Digits[_cursor])
                      .Append(" >\n\nONLINE ROOMS: COMING SOON\nOK ADD   BACK ERASE");
                    break;
                case Page.Color:
                    sb.Append("DOLL COLOR:\n\n< ").Append(GameSettings.PaletteNames[GameSettings.ColorIndex]).Append(" >\n\nOK DONE");
                    break;
                case Page.Mic:
                    string mode = GameSettings.Mic == MicMode.Open ? "OPEN" : GameSettings.Mic == MicMode.PushToTalk ? "PUSH TO TALK (A/X)" : "MUTED";
                    sb.Append("MICROPHONE:\n\n< ").Append(mode).Append(" >\n\n");
                    sb.Append(GameSettings.Mic == MicMode.Muted ? "SHE CAN'T HEAR YOU TALK.\nNOR CAN YOUR FRIENDS." : "SHE HEARS EVERYTHING.");
                    sb.Append("\n\nOK DONE");
                    break;
                case Page.Help:
                    sb.Append("PUSH THE FLOOR TO MOVE.\nTILT YOUR HEAD TO SLIDE.\nGRIP: GRAB / THROW.\nREAD PAPERS UP CLOSE.\nTHE PLAYROOM IS SAFE.\n\nDON'T SCREAM.\n\nOK BACK");
                    break;
            }
            screen.text = sb.ToString();
        }

        // ------------------------------------------------------------------ utilitaires

        private void Beep() { if (beep != null) _audio.PlayOneShot(beep, 0.5f); }
        private static int Wrap(int v, int n) => (v % n + n) % n;
        private static void Hook(PressButton b, UnityEngine.Events.UnityAction a) { if (b != null) b.onPressed.AddListener(a); }
        private static void Unhook(PressButton b, UnityEngine.Events.UnityAction a) { if (b != null) b.onPressed.RemoveListener(a); }
    }
}
