using Horror.Locomotion;
using Horror.Monsters;
using Horror.Noise;
using Horror.Player;
using UnityEditor;
using UnityEngine;

namespace Horror.EditorTools
{
    /// <summary>
    /// Menu "Horror > 4 - Ajouter L'Écoute (test) dans la scène" : ajoute à la scène OUVERTE
    /// (normalement la salle de test du menu 3) :
    ///  - le monstre aveugle avec son IA (modèle LEcoute.glb s'il est importé, sinon une silhouette) ;
    ///  - les bruits du joueur : pas (impacts des mains) et voix (micro) ;
    ///  - une tour de boîtes à faire tomber (bruit d'objets) ;
    ///  - les anneaux de bruit visibles dans le casque (pour comprendre ce qu'elle entend).
    /// Rien n'est supprimé ni écrasé. Ctrl+Z annule.
    /// </summary>
    public static class MonsterTestBuilder
    {
        private const string MonsterModelPath = "Assets/Horror/Art/Monsters/LEcoute.glb";

        [MenuItem("Horror/4 - Ajouter L'Écoute (test) dans la scène")]
        public static void AddMonsterTest()
        {
            ArmLocomotion player = FindPlayer();
            if (player == null)
            {
                EditorUtility.DisplayDialog("Pas de joueur",
                    "Je ne trouve pas de joueur (ArmLocomotion) dans la scène ouverte.\n\n" +
                    "Lance d'abord le menu Horror > 3 - Créer la salle de test VR.", "OK");
                return;
            }

            Undo.SetCurrentGroupName("Ajouter L'Écoute (test)");
            int group = Undo.GetCurrentGroup();

            SetupPlayerNoise(player);
            GameObject monster = CreateMonster(new Vector3(4.5f, 0f, 4.5f));
            CreateBoxTower(new Vector3(-1.5f, 0f, -1.5f));

#if UNITY_2022_2_OR_NEWER
            bool hasDebugView = Object.FindAnyObjectByType<NoiseDebugView>() != null;
#else
            bool hasDebugView = Object.FindObjectOfType<NoiseDebugView>() != null;
#endif
            if (!hasDebugView)
            {
                var debug = new GameObject("Anneaux de bruit (test)");
                debug.AddComponent<NoiseDebugView>();
                Undo.RegisterCreatedObjectUndo(debug, "Anneaux de bruit");
            }

            Undo.CollapseUndoOperations(group);
            Selection.activeGameObject = monster;
            Debug.Log("[Horror] L'Écoute est dans la scène. Play, puis fais du bruit avec tes mains ou ta voix.");
        }

        private static ArmLocomotion FindPlayer()
        {
#if UNITY_2022_2_OR_NEWER
            ArmLocomotion[] all = Object.FindObjectsByType<ArmLocomotion>(FindObjectsSortMode.None);
#else
            ArmLocomotion[] all = Object.FindObjectsOfType<ArmLocomotion>();
#endif
            return all.Length > 0 ? all[0] : null;
        }

        // ------------------------------------------------------------------ joueur

        private static void SetupPlayerNoise(ArmLocomotion player)
        {
            if (player.GetComponent<FootstepNoise>() == null)
                Undo.AddComponent<FootstepNoise>(player.gameObject);

            if (player.GetComponentInChildren<VoiceNoise>() != null) return;

            // Micro : MicMouth a besoin d'une "bouche" à animer -> une bouche invisible suffit ici
            Transform head = player.head != null ? player.head : player.transform;
            var voice = new GameObject("Voix (micro)");
            Undo.RegisterCreatedObjectUndo(voice, "Voix");
            voice.transform.SetParent(head, false);
            voice.layer = player.gameObject.layer;
            var mouth = new GameObject("Bouche (invisible)");
            mouth.transform.SetParent(voice.transform, false);

            MicMouth mic = voice.AddComponent<MicMouth>();
            mic.mouth = mouth.transform;
            VoiceNoise noise = voice.AddComponent<VoiceNoise>();
            noise.mic = mic;
            noise.mouthPoint = head;
        }

        // ------------------------------------------------------------------ monstre

        private static GameObject CreateMonster(Vector3 position)
        {
            var root = new GameObject("L'Écoute (test)");
            Undo.RegisterCreatedObjectUndo(root, "L'Écoute");
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0f, 225f, 0f); // tournée vers le centre de la pièce
            root.layer = 2; // Ignore Raycast : elle ne bloque pas ses propres "oreilles"

            Transform head = null;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(MonsterModelPath);
            if (model != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                instance.transform.SetParent(root.transform, false);
                head = FindDeep(instance.transform, "Tete");
            }
            else
            {
                Debug.LogWarning("[Horror] Modèle " + MonsterModelPath + " non importé (installe le package glTFast). " +
                                 "Je mets une silhouette à la place.");
                head = CreatePlaceholder(root.transform);
            }
            SetLayerRecursive(root, 2);

            BlindMonsterAI ai = root.AddComponent<BlindMonsterAI>();
            ai.head = head;

            // Points de ronde aux 4 coins de la pièce de test
            var points = new Transform[4];
            Vector3[] corners = { new Vector3(4.5f, 0, 4.5f), new Vector3(-4.5f, 0, 4.5f), new Vector3(-4.5f, 0, -4.5f), new Vector3(4.5f, 0, -4.5f) };
            var pointsRoot = new GameObject("Points de ronde (L'Écoute)");
            Undo.RegisterCreatedObjectUndo(pointsRoot, "Points de ronde");
            for (int i = 0; i < corners.Length; i++)
            {
                var p = new GameObject("Point " + (i + 1));
                p.transform.SetParent(pointsRoot.transform, false);
                p.transform.position = corners[i];
                points[i] = p.transform;
            }
            ai.patrolPoints = points;
            return root;
        }

        private static Transform CreatePlaceholder(Transform parent)
        {
            Material dark = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            dark.color = new Color(0.25f, 0.2f, 0.28f);

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Silhouette";
            body.transform.SetParent(parent, false);
            body.transform.localPosition = new Vector3(0f, 1.0f, 0f);
            body.transform.localScale = new Vector3(0.6f, 1.0f, 0.5f);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.GetComponent<Renderer>().sharedMaterial = dark;

            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Tete";
            head.transform.SetParent(parent, false);
            head.transform.localPosition = new Vector3(0f, 2.15f, 0.25f);
            head.transform.localScale = new Vector3(0.3f, 0.42f, 0.3f);
            Object.DestroyImmediate(head.GetComponent<Collider>());
            head.GetComponent<Renderer>().sharedMaterial = dark;
            return head.transform;
        }

        // ------------------------------------------------------------------ objets bruyants

        private static void CreateBoxTower(Vector3 position)
        {
            var root = new GameObject("Tour de boîtes (bruit d'objets)");
            Undo.RegisterCreatedObjectUndo(root, "Tour de boîtes");
            root.transform.position = position;
            for (int i = 0; i < 5; i++)
            {
                GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.name = "Boîte " + (i + 1);
                box.transform.SetParent(root.transform, false);
                box.transform.localScale = Vector3.one * 0.35f;
                box.transform.localPosition = new Vector3(Random.Range(-0.03f, 0.03f), 0.175f + i * 0.36f, 0f);
                box.transform.localRotation = Quaternion.Euler(0f, Random.Range(-15f, 15f), 0f);
                Rigidbody rb = box.AddComponent<Rigidbody>();
                rb.mass = 0.6f;
                box.AddComponent<ImpactNoise>();
            }
        }

        // ------------------------------------------------------------------ utilitaires

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                Transform found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursive(child.gameObject, layer);
        }
    }
}
