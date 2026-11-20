namespace Horror
{
    /// <summary>
    /// Les noms affichés dans le jeu, à UN seul endroit : si on change le nom du jeu ou de la monnaie,
    /// on ne modifie que ce fichier.
    ///
    /// Nom de travail : "Calm Faces" (tiré du lore : "Draw your calm face", "We wear our calm face").
    /// Monnaie : les crayons de couleur (les enfants dessinent leur visage aux crayons).
    /// Attention : avant la sortie, vérifier que le nom n'est pas déjà pris sur les stores (Meta, Steam).
    /// </summary>
    public static class GameNames
    {
        public const string Game = "CALM FACES";
        /// <summary>Nom de la monnaie au pluriel (affichage : "150 CRAYONS").</summary>
        public const string Currency = "CRAYONS";
        /// <summary>Au singulier ("1 CRAYON").</summary>
        public const string CurrencySingle = "CRAYON";

        /// <summary>"1 CRAYON", "150 CRAYONS".</summary>
        public static string Amount(int n) => n + " " + (n == 1 ? CurrencySingle : Currency);
    }
}
