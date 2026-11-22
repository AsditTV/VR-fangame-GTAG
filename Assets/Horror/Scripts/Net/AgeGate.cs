using System;
using UnityEngine;

namespace Horror.Net
{
    /// <summary>
    /// Tranche d'âge du compte Meta du joueur, et ce qu'elle autorise. Règle de Meta (Get Age Category API) :
    /// un compte ENFANT (10-12 ans) ne doit pas avoir accès aux fonctions sociales ; un âge INCONNU (hors ligne,
    /// API absente, échec) ne doit jamais bloquer le joueur.
    /// Ce que fait CALM FACES pour un compte enfant :
    ///  - chat vocal coupé dans les deux sens (son micro n'est pas envoyé, il n'entend pas les autres) ;
    ///  - le jeu en ligne reste possible seulement par code de salle (salle privée entre amis), sans voix.
    /// La tranche d'âge vient d'un « fournisseur » (MetaAgeProvider, quand le SDK Platform de Meta est installé) ;
    /// la dernière valeur connue est gardée (PlayerPrefs) pour les sessions hors ligne.
    /// Tests : argument « -age child|teen|adult » ou clé PlayerPrefs « horror.age.override » (0 = aucune).
    /// </summary>
    public static class AgeGate
    {
        public enum AgeGroup { Unknown = 0, Child = 1, Teen = 2, Adult = 3 }

        private const string KeyCache = "horror.age.cache", KeyOverride = "horror.age.override";

        /// <summary>Demande la tranche d'âge à la plateforme ; appelle le rappel avec le résultat (Unknown si échec).</summary>
        public static Action<Action<AgeGroup>> Provider;

        public static event Action Changed;

        private static AgeGroup _current = AgeGroup.Unknown;
        private static bool _loaded;

        public static AgeGroup Current
        {
            get
            {
                if (!_loaded) Load();
                AgeGroup forced = Forced();
                return forced != AgeGroup.Unknown ? forced : _current;
            }
        }

        public static bool IsChild => Current == AgeGroup.Child;

        /// <summary>Chat vocal permis (dans les deux sens) ?</summary>
        public static bool VoiceAllowed => !IsChild;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            Load();
            Refresh();
        }

        /// <summary>Redemande la tranche d'âge à la plateforme (au démarrage, et à chaque passage en ligne).</summary>
        public static void Refresh()
        {
            if (Provider == null) return;
            try { Provider(Set); }
            catch (Exception e) { Debug.LogWarning("[Horror] Tranche d'âge : appel impossible (" + e.Message + "), on garde la dernière connue."); }
        }

        /// <summary>Résultat de la plateforme. Unknown ne remplace pas une valeur déjà connue (hors ligne).</summary>
        public static void Set(AgeGroup group)
        {
            if (group == AgeGroup.Unknown) return;
            bool changed = group != _current;
            _current = group;
            PlayerPrefs.SetInt(KeyCache, (int)group);
            PlayerPrefs.Save();
            if (changed)
            {
                Debug.Log("[Horror] Tranche d'âge du compte : " + group + (IsChild ? " (chat vocal coupé)" : ""));
                Changed?.Invoke();
            }
        }

        private static void Load()
        {
            _loaded = true;
            _current = (AgeGroup)Mathf.Clamp(PlayerPrefs.GetInt(KeyCache, 0), 0, 3);
        }

        private static AgeGroup Forced()
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-age");
            if (i >= 0 && i + 1 < args.Length && Enum.TryParse(args[i + 1], true, out AgeGroup a)) return a;
            return (AgeGroup)Mathf.Clamp(PlayerPrefs.GetInt(KeyOverride, 0), 0, 3);
        }
    }
}
