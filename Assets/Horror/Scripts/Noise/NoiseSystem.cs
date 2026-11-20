using System;
using UnityEngine;

namespace Horror.Noise
{
    /// <summary>Les familles de bruit (le monstre peut réagir différemment à chacune).</summary>
    public enum NoiseType
    {
        Pas,        // impact des mains sur le sol / les murs
        Voix,       // le micro (chat de proximité)
        Objet,      // un objet lancé ou qui tombe
        Porte,      // une porte à boutons qui s'ouvre / se ferme
        Leurre      // un bruit volontaire (micro du Standard, réveil, boîte à musique...)
    }

    /// <summary>Un bruit qui vient de se produire dans le monde.</summary>
    public struct NoiseEvent
    {
        /// <summary>Où le bruit a eu lieu.</summary>
        public Vector3 position;
        /// <summary>Jusqu'à quelle distance (mètres) on l'entend, sans mur entre les deux.</summary>
        public float radius;
        public NoiseType type;
        /// <summary>Qui l'a fait (le joueur, l'objet...). Peut être null.</summary>
        public GameObject source;
        public float time;

        public NoiseEvent(Vector3 position, float radius, NoiseType type, GameObject source)
        {
            this.position = position;
            this.radius = radius;
            this.type = type;
            this.source = source;
            time = Time.time;
        }
    }

    /// <summary>
    /// Le "standard téléphonique" de tous les bruits du jeu : tout ce qui fait du bruit appelle
    /// NoiseSystem.Emit(), et tout ce qui écoute (les monstres, les dessins aux murs...) s'abonne
    /// à NoiseSystem.Heard.
    ///
    /// Pourquoi un seul point de passage ? Pour le multijoueur (étape 6) : l'IA des monstres ne
    /// tournera QUE chez l'hôte. Les autres joueurs enverront leurs bruits par le réseau, et l'hôte
    /// les ré-émettra ici. Il suffira de brancher le réseau sur ce fichier, sans toucher aux monstres.
    /// </summary>
    public static class NoiseSystem
    {
        /// <summary>Appelé pour chaque bruit émis dans cette instance du jeu.</summary>
        public static event Action<NoiseEvent> Heard;

        /// <summary>
        /// Branchement réseau (étape 6) : si non nul, les bruits locaux lui sont AUSSI passés
        /// pour être envoyés à l'hôte. Laisse vide en solo.
        /// </summary>
        public static Action<NoiseEvent> NetworkForwarder;

        /// <summary>Calque des murs qui étouffent le son (par défaut : tout sauf "Ignore Raycast").</summary>
        public static LayerMask occluderLayers = Physics.DefaultRaycastLayers;

        /// <summary>Un mur divise la distance d'écoute par ce facteur.</summary>
        public const float WallDamping = 0.45f;

        /// <summary>Émet un bruit. Ignore les bruits trop faibles.</summary>
        public static void Emit(Vector3 position, float radius, NoiseType type, GameObject source = null)
        {
            radius *= HideZone.MuffleAt(position);   // cachettes (lits du dortoir...) : bruit étouffé
            if (radius <= 0.05f) return;
            var e = new NoiseEvent(position, radius, type, source);
            Heard?.Invoke(e);
            NetworkForwarder?.Invoke(e);
        }

        /// <summary>
        /// À quel point un auditeur placé en "listener" entend ce bruit (0 = pas du tout, 1 = juste à côté).
        /// Un mur entre les deux réduit fortement la portée.
        /// </summary>
        public static float Audibility(NoiseEvent e, Vector3 listener)
        {
            float radius = e.radius;
            // On s'arrête 30 cm avant la source : une main posée au sol ne doit pas compter
            // le sol lui-même comme un "mur" entre le bruit et l'auditeur.
            Vector3 toListener = listener - e.position;
            Vector3 target = e.position + toListener.normalized * Mathf.Min(0.3f, toListener.magnitude * 0.5f);
            if (Physics.Linecast(listener, target, out RaycastHit hit, occluderLayers, QueryTriggerInteraction.Ignore))
            {
                // Le premier obstacle touché n'est pas la source elle-même : c'est un mur.
                if (e.source == null || !hit.transform.IsChildOf(e.source.transform))
                    radius *= WallDamping;
            }

            float distance = Vector3.Distance(listener, e.position);
            return Mathf.Clamp01(1f - distance / Mathf.Max(radius, 0.01f));
        }
    }
}
