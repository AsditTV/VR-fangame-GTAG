using UnityEditor;
using UnityEngine;
using Horror.Cosmetics;

namespace Horror.EditorTools
{
    /// <summary>
    /// Crée la casquette d'Armand, le veilleur de nuit (cosmétique réservé aux modérateurs) et la liste des
    /// modérateurs (Assets/Horror/Resources/ModeratorRoster.asset). Ne remplace jamais un fichier existant :
    /// pour refaire la casquette, supprimer d'abord le prefab à la main.
    /// Casquette plate de gardien des années 80 : calotte bleu nuit, bandeau noir, visière vernie, cordon de laiton,
    /// insigne « NIGHT WATCH » avec une clé (Tools/Map/make_insigne_veilleur.py). Aucune lumière.
    /// </summary>
    public static class NightWatchCapBuilder
    {
        private const string PrefabPath = "Assets/Horror/Cosmetics/night_watch_cap.prefab";
        private const string RosterPath = "Assets/Horror/Resources/ModeratorRoster.asset";
        private const string BadgeTex = "Assets/Horror/Art/Cosmetics/Insigne_Veilleur.png";
        private const string Mats = "Assets/Horror/Art/Materials/";

        [MenuItem("Horror/Outils/Casquette du veilleur (modérateurs)")]
        public static void Build()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) prefab = BuildPrefab();
            else Debug.Log("[Horror] Casquette du veilleur : le prefab existe déjà, gardé tel quel.");

            var roster = AssetDatabase.LoadAssetAtPath<ModeratorRoster>(RosterPath);
            if (roster == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Horror/Resources")) AssetDatabase.CreateFolder("Assets/Horror", "Resources");
                roster = ScriptableObject.CreateInstance<ModeratorRoster>();
                roster.capPrefab = prefab;
                AssetDatabase.CreateAsset(roster, RosterPath);
                Debug.Log("[Horror] Liste des modérateurs créée (vide) : " + RosterPath);
            }
            else if (roster.capPrefab == null) { roster.capPrefab = prefab; EditorUtility.SetDirty(roster); }
            AssetDatabase.SaveAssets();
        }

        private static GameObject BuildPrefab()
        {
            var imp = AssetImporter.GetAtPath(BadgeTex) as TextureImporter;
            if (imp != null && !imp.alphaIsTransparency) { imp.alphaIsTransparency = true; imp.mipmapEnabled = true; imp.SaveAndReimport(); }

            Material navy = Mat("Veilleur_Casquette", new Color(0.07f, 0.09f, 0.16f), 0.2f, 0f);
            Material black = Mat("Veilleur_Visiere", new Color(0.03f, 0.03f, 0.035f), 0.75f, 0f);
            Material brass = Mat("Veilleur_Laiton", new Color(0.78f, 0.6f, 0.28f), 0.6f, 0.85f);
            Material badge = Mat("Veilleur_Insigne", Color.white, 0.45f, 0.5f);
            badge.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(BadgeTex));
            badge.SetFloat("_AlphaClip", 1f); badge.SetFloat("_Cutoff", 0.5f); badge.EnableKeyword("_ALPHATEST_ON");
            EditorUtility.SetDirty(badge);

            var root = new GameObject("night_watch_cap");
            // l'origine est le haut du crâne ; +Z = devant
            Part(PrimitiveType.Cylinder, root, "Bandeau", new Vector3(0f, 0.018f, 0f), new Vector3(0.205f, 0.018f, 0.205f), Vector3.zero, black);
            Part(PrimitiveType.Cylinder, root, "Calotte", new Vector3(0f, 0.064f, 0.004f), new Vector3(0.2f, 0.028f, 0.2f), Vector3.zero, navy);
            Part(PrimitiveType.Cylinder, root, "Plateau", new Vector3(0f, 0.096f, 0.012f), new Vector3(0.24f, 0.008f, 0.235f), new Vector3(-4f, 0f, 0f), navy);
            Part(PrimitiveType.Cylinder, root, "Visière", new Vector3(0f, 0.006f, 0.095f), new Vector3(0.19f, 0.005f, 0.12f), new Vector3(16f, 0f, 0f), black);
            Part(PrimitiveType.Cube, root, "Cordon", new Vector3(0f, 0.03f, 0.104f), new Vector3(0.17f, 0.008f, 0.008f), Vector3.zero, brass);
            Part(PrimitiveType.Sphere, root, "Bouton G", new Vector3(-0.088f, 0.03f, 0.06f), Vector3.one * 0.016f, Vector3.zero, brass);
            Part(PrimitiveType.Sphere, root, "Bouton D", new Vector3(0.088f, 0.03f, 0.06f), Vector3.one * 0.016f, Vector3.zero, brass);
            // l'insigne : un quad tourné vers l'avant (sa face visible regarde -Z par défaut), un peu incliné comme la calotte
            Part(PrimitiveType.Quad, root, "Insigne", new Vector3(0f, 0.066f, 0.1045f), new Vector3(0.05f, 0.0625f, 1f), new Vector3(-8f, 180f, 0f), badge);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            Debug.Log("[Horror] Casquette du veilleur créée : " + PrefabPath);
            return prefab;
        }

        private static void Part(PrimitiveType type, GameObject root, string name, Vector3 pos, Vector3 scale, Vector3 euler, Material m)
        {
            GameObject g = GameObject.CreatePrimitive(type);
            g.name = name;
            Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(root.transform, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = Quaternion.Euler(euler);
            g.transform.localScale = scale;
            var r = g.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;   // petit objet : pas d'ombre (Quest)
        }

        private static Material Mat(string name, Color c, float smooth, float metal)
        {
            string path = Mats + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;   // déjà là : on le garde (peut-être retouché à la main)
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }
    }
}
