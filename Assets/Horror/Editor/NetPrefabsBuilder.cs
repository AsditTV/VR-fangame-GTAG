#if FUSION2   // seulement quand le SDK Photon Fusion 2 est installé
using Fusion;
using UnityEditor;
using UnityEngine;
using Horror.Net;

namespace Horror.EditorTools
{
    /// <summary>
    /// Menu 11 : crée les deux objets réseau du multijoueur dans Assets/Horror/Resources/Net/ et les inscrit dans la
    /// table des prefabs de Fusion :
    ///  - NetRelay : un par joueur (sa pose, son pseudo, sa couleur ; bruits et événements en RPC) ;
    ///  - MonsterRelay : les monstres, tenu par le master client (marqué « objet du master client » : s'il part,
    ///    Fusion le confie au suivant).
    /// À relancer seulement si ces fichiers ont été supprimés.
    /// </summary>
    public static class NetPrefabsBuilder
    {
        private const string Folder = "Assets/Horror/Resources/Net";

        [MenuItem("Horror/11 - Préparer les objets réseau (Photon)")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Horror/Resources")) AssetDatabase.CreateFolder("Assets/Horror", "Resources");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Horror/Resources", "Net");
            Make<NetRelay>("NetRelay", false);
            Make<MonsterRelay>("MonsterRelay", true);
            AssetDatabase.SaveAssets();
            EditorApplication.ExecuteMenuItem("Tools/Fusion/Rebuild Prefab Table");
            Debug.Log("[Horror] Objets réseau prêts : " + Folder + " (NetRelay, MonsterRelay), table Fusion reconstruite.");
        }

        private static void Make<T>(string name, bool masterClientObject) where T : NetworkBehaviour
        {
            string path = Folder + "/" + name + ".prefab";
            var go = new GameObject(name);
            NetworkObject no = go.AddComponent<NetworkObject>();
            go.AddComponent<T>();
            if (masterClientObject) no.Flags |= NetworkObjectFlags.MasterClientObject;
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }
    }
}
#endif
