using System.Collections.Generic;
using UnityEngine;

namespace Horror.Noise
{
    /// <summary>Les matières sur lesquelles on tape avec les mains (son + portée du bruit différents).</summary>
    public enum SurfaceType { Bois, Carrelage, Beton, Metal, Tissu, Encre, Platre }

    /// <summary>
    /// Devine la matière touchée. Les morceaux de la maquette s'appellent « Pièce_matière »
    /// (ex. Couloir_parquet, Machines_metal_gris, Hall_moquette_rose) : le nom suffit.
    /// </summary>
    public static class Surface
    {
        // mot-clé dans le nom -> matière (le premier qui correspond gagne, donc l'ordre compte)
        private static readonly (string key, SurfaceType type)[] Keywords =
        {
            ("encre", SurfaceType.Encre), ("ink", SurfaceType.Encre),
            ("metal", SurfaceType.Metal), ("rouille", SurfaceType.Metal), ("laiton", SurfaceType.Metal),
            ("passerelle", SurfaceType.Metal), ("grille", SurfaceType.Metal),
            ("moquette", SurfaceType.Tissu), ("tissu", SurfaceType.Tissu), ("velours", SurfaceType.Tissu),
            ("rideau", SurfaceType.Tissu), ("matelas", SurfaceType.Tissu), ("tapis", SurfaceType.Tissu), ("costume", SurfaceType.Tissu),
            ("carrelage", SurfaceType.Carrelage), ("damier", SurfaceType.Carrelage), ("lino", SurfaceType.Carrelage),
            ("beton", SurfaceType.Beton), ("concrete", SurfaceType.Beton),
            ("parquet", SurfaceType.Bois), ("bois", SurfaceType.Bois), ("moulure", SurfaceType.Bois),
            ("planche", SurfaceType.Bois), ("scene", SurfaceType.Bois), ("wood", SurfaceType.Bois),
            ("mur_", SurfaceType.Platre), ("plafond", SurfaceType.Platre), ("papier", SurfaceType.Platre),
        };

        private static readonly Dictionary<int, SurfaceType> Cache = new Dictionary<int, SurfaceType>();

        /// <summary>Matière par défaut quand rien ne correspond.</summary>
        public static SurfaceType fallback = SurfaceType.Bois;

        public static SurfaceType Of(Collider c)
        {
            if (c == null) return fallback;
            int id = c.GetInstanceID();
            if (Cache.TryGetValue(id, out SurfaceType t)) return t;

            SurfaceTag tag = c.GetComponentInParent<SurfaceTag>();
            if (tag != null) t = tag.type;
            else if (!Guess(c.gameObject.name, out t))
            {
                Renderer r = c.GetComponent<Renderer>();
                if (r == null || r.sharedMaterial == null || !Guess(r.sharedMaterial.name, out t)) t = fallback;
            }
            Cache[id] = t;
            return t;
        }

        private static bool Guess(string name, out SurfaceType type)
        {
            string n = name.ToLowerInvariant();
            foreach (var (key, st) in Keywords)
                if (n.Contains(key)) { type = st; return true; }
            type = fallback;
            return false;
        }

        /// <summary>
        /// Portée du bruit selon la matière : le métal résonne (on t'entend de loin), la moquette étouffe.
        /// C'est un choix de jeu : marcher sur les tapis pour être discret.
        /// </summary>
        public static float Loudness(SurfaceType t)
        {
            switch (t)
            {
                case SurfaceType.Metal: return 1.6f;
                case SurfaceType.Carrelage: return 1.25f;
                case SurfaceType.Encre: return 1.1f;
                case SurfaceType.Bois: return 1f;
                case SurfaceType.Beton: return 0.9f;
                case SurfaceType.Platre: return 0.8f;
                case SurfaceType.Tissu: return 0.45f;
                default: return 1f;
            }
        }
    }
}
