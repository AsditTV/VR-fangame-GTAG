using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using Horror.Locomotion;
using Horror.XR;

namespace Horror.EditorTools
{
    /// <summary>
    /// Menu "Horror > 3 - Créer la salle de test VR (locomotion)" : génère une petite pièce en blocs gris/pastel
    /// (sol, murs, caisses, rampe, poutre, pilier) et un joueur prêt à jouer avec la locomotion aux bras.
    ///
    /// C'est volontairement du "greybox" (formes simples) : il sert à régler les sensations,
    /// pas à être beau. La vraie map viendra des packs d'assets (étape 5).
    ///
    /// Sécurité : ne remplace JAMAIS une scène ou un matériau existant.
    /// </summary>
    public static class TestRoomBuilder
    {
        private const string ScenePath = "Assets/Horror/Scenes/SalleTestLocomotion.unity";
        private const string MaterialFolder = "Assets/Horror/Art/Materials/TestRoom";
        private const int IgnoreRaycastLayer = 2; // calque intégré à Unity

        [MenuItem("Horror/3 - Créer la salle de test VR (locomotion)")]
        public static void CreateTestRoom()
        {
            if (File.Exists(ScenePath))
            {
                bool open = EditorUtility.DisplayDialog(
                    "Salle de test déjà créée",
                    "La scène existe déjà :\n" + ScenePath +
                    "\n\nJe ne l'écrase pas. Pour la régénérer, supprime-la (ou renomme-la) d'abord.",
                    "L'ouvrir", "Annuler");
                if (open && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    EditorSceneManager.OpenScene(ScenePath);
                return;
            }

            // Propose d'enregistrer la scène actuelle avant d'en ouvrir une nouvelle.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // La caméra par défaut est remplacée par celle du joueur VR.
            GameObject defaultCamera = GameObject.FindWithTag("MainCamera");
            if (defaultCamera != null)
                Object.DestroyImmediate(defaultCamera);

            BuildRoom();
            BuildPlayer(new Vector3(0f, 0f, -3f));

            EnsureFolder("Assets/Horror/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Debug.Log("[Horror] Salle de test créée : " + ScenePath +
                      ". Branche le casque (Link), puis appuie sur Play.");
        }

        // ------------------------------------------------------------------ pièce

        private static void BuildRoom()
        {
            Material floor = GetOrCreateMaterial("Sol", new Color(0.55f, 0.50f, 0.62f));     // lilas grisé
            Material wall = GetOrCreateMaterial("Mur", new Color(0.78f, 0.86f, 0.80f));      // vert d'eau pâle
            Material props = GetOrCreateMaterial("Caisse", new Color(0.95f, 0.78f, 0.70f));  // pêche
            Material accent = GetOrCreateMaterial("Accent", new Color(0.62f, 0.75f, 0.92f)); // bleu layette

            GameObject root = new GameObject("Salle de test");

            // Sol + plafond + 4 murs : une pièce de 14 x 14 m, 6 m de haut.
            Block(root, "Sol", new Vector3(0f, -0.25f, 0f), new Vector3(14f, 0.5f, 14f), floor);
            Block(root, "Plafond", new Vector3(0f, 6.25f, 0f), new Vector3(14f, 0.5f, 14f), wall);
            Block(root, "Mur Nord", new Vector3(0f, 3f, 7.25f), new Vector3(14f, 6f, 0.5f), wall);
            Block(root, "Mur Sud", new Vector3(0f, 3f, -7.25f), new Vector3(14f, 6f, 0.5f), wall);
            Block(root, "Mur Est", new Vector3(7.25f, 3f, 0f), new Vector3(0.5f, 6f, 14f), wall);
            Block(root, "Mur Ouest", new Vector3(-7.25f, 3f, 0f), new Vector3(0.5f, 6f, 14f), wall);

            // Obstacles pour tester : monter, sauter, grimper, se suspendre.
            Block(root, "Caisse basse", new Vector3(-3f, 0.4f, 1f), new Vector3(1.2f, 0.8f, 1.2f), props);
            Block(root, "Caisse moyenne", new Vector3(-4.5f, 0.8f, 2.5f), new Vector3(1.2f, 1.6f, 1.2f), props);
            Block(root, "Caisse haute", new Vector3(-6f, 1.3f, 4.5f), new Vector3(1.5f, 2.6f, 1.5f), props);

            GameObject ramp = Block(root, "Rampe", new Vector3(3.5f, 0.9f, 2f), new Vector3(2f, 0.3f, 5f), accent);
            ramp.transform.rotation = Quaternion.Euler(-20f, 0f, 0f);

            Block(root, "Pilier (à escalader)", new Vector3(0f, 3f, 4f), new Vector3(0.8f, 6f, 0.8f), accent);
            Block(root, "Poutre (se suspendre)", new Vector3(3f, 2.6f, -2f), new Vector3(4f, 0.15f, 0.15f), props);
            Block(root, "Plateforme haute", new Vector3(5.5f, 3f, 5.5f), new Vector3(3f, 0.3f, 3f), floor);

            // Lumière d'appoint douce (la vraie ambiance sombre viendra à l'étape 5).
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.35f, 0.33f, 0.40f);
        }

        private static GameObject Block(GameObject parent, string name, Vector3 position, Vector3 size, Material material)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent.transform, false);
            cube.transform.localPosition = position;
            cube.transform.localScale = size;
            cube.isStatic = true;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            return cube;
        }

        // ------------------------------------------------------------------ joueur

        private static void BuildPlayer(Vector3 position)
        {
            GameObject player = new GameObject("Joueur");
            player.transform.position = position;
            player.layer = IgnoreRaycastLayer;

            Rigidbody rb = player.AddComponent<Rigidbody>();
            rb.mass = 60f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            player.AddComponent<CapsuleCollider>(); // dimensionné par ArmLocomotion à chaque image

            // Tête = caméra VR.
            GameObject head = new GameObject("Tête");
            head.tag = "MainCamera";
            head.layer = IgnoreRaycastLayer;
            head.transform.SetParent(player.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.6f, 0f); // valeur par défaut avant que le casque réponde
            Camera camera = head.AddComponent<Camera>();
            camera.nearClipPlane = 0.01f; // sinon les mains disparaissent quand on les approche du visage
            head.AddComponent<AudioListener>();
            head.AddComponent<XRNodePoseDriver>().node = XRNode.Head;

            Transform leftController = Controller(player, "Manette gauche", XRNode.LeftHand, new Vector3(-0.25f, 0.9f, 0.2f));
            Transform rightController = Controller(player, "Manette droite", XRNode.RightHand, new Vector3(0.25f, 0.9f, 0.2f));

            Material handMaterial = GetOrCreateMaterial("Main", new Color(0.96f, 0.93f, 0.86f));
            Transform leftVisual = HandVisual(player, "Main gauche (visuel)", handMaterial);
            Transform rightVisual = HandVisual(player, "Main droite (visuel)", handMaterial);

            ArmLocomotion locomotion = player.AddComponent<ArmLocomotion>();
            locomotion.head = head.transform;
            locomotion.leftController = leftController;
            locomotion.rightController = rightController;
            locomotion.leftHandVisual = leftVisual;
            locomotion.rightHandVisual = rightVisual;

            Selection.activeGameObject = player;
        }

        private static Transform Controller(GameObject player, string name, XRNode node, Vector3 defaultPosition)
        {
            GameObject controller = new GameObject(name);
            controller.layer = IgnoreRaycastLayer;
            controller.transform.SetParent(player.transform, false);
            controller.transform.localPosition = defaultPosition;
            controller.AddComponent<XRNodePoseDriver>().node = node;
            return controller.transform;
        }

        private static Transform HandVisual(GameObject player, string name, Material material)
        {
            // Simple boule pour l'instant ; on mettra de vraies mains plus tard.
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = name;
            sphere.layer = IgnoreRaycastLayer;
            sphere.transform.SetParent(player.transform, false);
            sphere.transform.localScale = Vector3.one * 0.12f;
            Object.DestroyImmediate(sphere.GetComponent<Collider>()); // la main visible ne doit rien percuter
            sphere.GetComponent<Renderer>().sharedMaterial = material;
            return sphere.transform;
        }

        // ------------------------------------------------------------------ utilitaires

        /// <summary>
        /// Renvoie le matériau s'il existe déjà (on ne l'écrase pas), sinon le crée.
        /// Compatible URP et pipeline intégré (Built-in).
        /// </summary>
        private static Material GetOrCreateMaterial(string name, Color color)
        {
            EnsureFolder(MaterialFolder);
            string path = MaterialFolder + "/TestRoom_" + name + ".mat";

            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null || UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null)
                shader = Shader.Find("Standard");

            Material material = new Material(shader);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color); // URP
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);         // Built-in
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.15f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.15f);

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
