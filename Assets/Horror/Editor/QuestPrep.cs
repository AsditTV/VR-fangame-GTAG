using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Horror.EditorTools
{
    /// <summary>
    /// Menu 9, à lancer APRÈS le menu 8 (qui refait la scène) : calcule l'occlusion culling de l'Institut.
    /// Les pièces cachées derrière les murs ne sont plus dessinées (énorme gain sur Quest : depuis le hall,
    /// on dessinait toutes les pièces voisines). Les murs de la maquette cachent, tout le reste peut être caché.
    /// </summary>
    public static class QuestPrep
    {
        private const string ScenePath = "Assets/Horror/Scenes/Institut.unity";

        [MenuItem("Horror/9 - Préparer pour le Quest (occlusion culling, après le menu 8)")]
        public static void Bake()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            int occluders = 0, occludees = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                bool walls = root.name.StartsWith("Institut (maquette)");
                foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                {
                    var flags = GameObjectUtility.GetStaticEditorFlags(r.gameObject);
                    if (walls) { flags |= StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic; occluders++; }
                    else if ((flags & StaticEditorFlags.BatchingStatic) != 0) { flags |= StaticEditorFlags.OccludeeStatic; occludees++; }
                    GameObjectUtility.SetStaticEditorFlags(r.gameObject, flags);
                }
            }
            StaticOcclusionCulling.smallestOccluder = 1.5f;   // un mur, pas un pied de lit
            StaticOcclusionCulling.smallestHole = 0.25f;      // les portes d'enfants laissent voir
            StaticOcclusionCulling.backfaceThreshold = 100f;
            bool ok = StaticOcclusionCulling.Compute();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Horror] Occlusion culling {(ok ? "calculé" : "ÉCHEC")} : {occluders} murs qui cachent, {occludees} objets cachables.");
        }
    }
}
