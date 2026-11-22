#if CALM_META_PLATFORM
using Oculus.Platform;
using UnityEngine;

namespace Horror.Net
{
    /// <summary>
    /// Fournit à AgeGate la tranche d'âge du compte Meta (Get Age Category API du SDK Platform de Meta).
    /// Compilé seulement avec le symbole CALM_META_PLATFORM, ajouté quand le paquet « Meta XR Platform SDK » est
    /// installé et l'App ID réglé dans ses paramètres (Meta > Platform > Edit Settings ; jamais dans le code).
    /// Hors Quest, hors ligne ou en cas d'échec : Unknown, qui ne bloque rien (règle de Meta).
    /// </summary>
    public static class MetaAgeProvider
    {
        private static bool _ready, _initializing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register() => AgeGate.Provider = Ask;

        private static void Ask(System.Action<AgeGate.AgeGroup> done)
        {
            if (Application.isEditor) { done(AgeGate.AgeGroup.Unknown); return; }   // l'éditeur n'a pas de compte Meta
            if (_ready) { Query(done); return; }
            if (_initializing) return;
            _initializing = true;
            Core.AsyncInitialize().OnComplete(m =>
            {
                _initializing = false;
                if (m.IsError) { Debug.LogWarning("[Horror] SDK Platform de Meta : initialisation impossible."); done(AgeGate.AgeGroup.Unknown); return; }
                _ready = true;
                Query(done);
            });
        }

        private static void Query(System.Action<AgeGate.AgeGroup> done)
        {
            UserAgeCategory.Get().OnComplete(m =>
            {
                if (m.IsError || m.Data == null) { done(AgeGate.AgeGroup.Unknown); return; }
                switch (m.Data.AgeCategory)
                {
                    case AccountAgeCategory.Ch: done(AgeGate.AgeGroup.Child); break;
                    case AccountAgeCategory.Tn: done(AgeGate.AgeGroup.Teen); break;
                    case AccountAgeCategory.Ad: done(AgeGate.AgeGroup.Adult); break;
                    default: done(AgeGate.AgeGroup.Unknown); break;
                }
            });
        }
    }
}
#endif
