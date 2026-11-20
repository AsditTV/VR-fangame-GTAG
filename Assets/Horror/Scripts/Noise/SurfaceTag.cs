using UnityEngine;

namespace Horror.Noise
{
    /// <summary>
    /// Force la matière d'un objet (sinon elle est devinée d'après le nom de l'objet ou de son matériau).
    /// Ex. : une caisse en métal nommée « Caisse » -> ajoute SurfaceTag = Metal.
    /// (Dans son propre fichier : Unity l'exige pour pouvoir l'ajouter à un objet.)
    /// </summary>
    public class SurfaceTag : MonoBehaviour
    {
        public SurfaceType type = SurfaceType.Bois;
    }
}
