using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Horror.EditorTools
{
    /// <summary>
    /// Performances Quest : une poupée = ~48 morceaux = ~48 appels de dessin. Ici on fusionne les morceaux d'un
    /// modèle immobile en un maillage par matière (3 à 6 appels au lieu de 48). Ce qui doit bouger (la tête des
    /// enfants du dortoir, qui te suit du regard) est laissé à part. L'aspect ne change pas.
    /// </summary>
    public static partial class InstitutBuilder
    {
        private static int _fusionCount;

        /// <summary>Fusionne les rendus de <paramref name="root"/> (sauf ceux sous <paramref name="keep"/>), par matière.</summary>
        private static void MergeModel(GameObject root, Transform keep = null)
        {
            if (root == null) return;
            var groups = new Dictionary<Material, List<CombineInstance>>();
            var victims = new List<MeshFilter>();
            Matrix4x4 toLocal = root.transform.worldToLocalMatrix;
            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(false))
            {
                if (mf.sharedMesh == null) continue;
                if (keep != null && mf.transform.IsChildOf(keep)) continue;
                MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || !mr.enabled) continue;
                Material[] mats = mr.sharedMaterials;
                for (int s = 0; s < mf.sharedMesh.subMeshCount && s < mats.Length; s++)
                {
                    if (mats[s] == null) continue;
                    if (!groups.TryGetValue(mats[s], out var list)) groups[mats[s]] = list = new List<CombineInstance>();
                    list.Add(new CombineInstance { mesh = mf.sharedMesh, subMeshIndex = s, transform = toLocal * mf.transform.localToWorldMatrix });
                }
                victims.Add(mf);
            }
            if (victims.Count < 4) return;                                   // pas la peine
            EnsureFolder("Assets/Horror/Art/Map/Genere/Fusion");
            int g = 0;
            foreach (var kv in groups)
            {
                var mesh = new Mesh { name = "Fusion_" + _fusionCount + "_" + g, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.CombineMeshes(kv.Value.ToArray(), true, true);
                mesh.RecalculateBounds();
                string path = "Assets/Horror/Art/Map/Genere/Fusion/" + mesh.name + ".asset";
                if (Load<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(mesh, path);
                var go = new GameObject("Fusion (" + kv.Key.name + ")");
                go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = kv.Key;
                g++;
            }
            foreach (MeshFilter mf in victims)
            {
                Object.DestroyImmediate(mf.GetComponent<MeshRenderer>());
                Object.DestroyImmediate(mf);
            }
            _fusionCount++;
        }

        private static void MergeDecorModels()
        {
            _fusionCount = 0;
            EnsureFolder("Assets/Horror/Art/Map/Genere/Fusion");
            foreach (string guid in AssetDatabase.FindAssets("t:Mesh", new[] { "Assets/Horror/Art/Map/Genere/Fusion" }))
                AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));        // fusions d'une construction précédente
            int before = _fusionCount;
            GameObject dorm = GameObject.Find("Les enfants du dortoir");
            if (dorm != null)
                foreach (Transform kid in dorm.transform)
                    if (kid.name.StartsWith("Enfant")) MergeModel(kid.gameObject, FindChild(kid, "Tete"));   // la tête tourne
            GameObject loge = GameObject.Find("La loge (Doux sans tête, peaux de Doux)");
            if (loge != null)
                foreach (Transform t in loge.transform)
                    if (t.name.StartsWith("Doux") || t.name.StartsWith("Peau") || t.name.StartsWith("Tête")) MergeModel(t.gameObject);
            GameObject inf = GameObject.Find("Infirmerie (mobilier)");
            if (inf != null)
                foreach (Transform t in inf.transform)
                    if (t.name.StartsWith("Enfant")) MergeModel(t.gameObject);
            Debug.Log($"[Horror] Poupées fusionnées (moins d'appels de dessin) : {_fusionCount - before} modèles.");
        }
    }
}
