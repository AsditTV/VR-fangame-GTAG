using System.IO;
using Horror.Interaction;
using Horror.Locomotion;
using Horror.Lore;
using Horror.Noise;
using Horror.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR;

namespace Horror.EditorTools
{
    /// <summary>
    /// Menu "Horror > 6 - Ajouter les papiers de lore + attraper/lancer" :
    ///  - crée les fichiers LoreDocument (Assets/Horror/Lore) à partir des images de Tools/Papers ;
    ///  - pose les 17 papiers dans la salle de test (sur les caisses, la plateforme, par terre...) ;
    ///  - ajoute le journal "PAPERS FOUND x / 13" dans la Salle de jeux ;
    ///  - accroche les dessins d'enfants au mur ;
    ///  - donne aux mains le pouvoir d'attraper (grip) et rend la tour de boîtes attrapable et lançable.
    /// Rien n'est écrasé. Ctrl+Z annule.
    /// </summary>
    public static class LoreSetupBuilder
    {
        private const string PapersFolder = "Assets/Horror/Art/Papers/";
        private const string DocsFolder = "Assets/Horror/Lore";
        private const string DrawFolder = "Assets/Horror/Art/Drawings/";
        private const string MatFolder = "Assets/Horror/Art/Materials/Drawings";

        // Champs remplis par JsonUtility (le compilateur ne le voit pas) : pas d'avertissement inutile
#pragma warning disable 0649
        [System.Serializable] private class PaperInfo { public string id; public string title; public string zone; public int order; public bool secret; public int[] size; public bool back; }
        [System.Serializable] private class PaperList { public PaperInfo[] items; }
#pragma warning restore 0649

        // Emplacements dans la salle de test (position, angle autour de la verticale)
        private static readonly (Vector3 pos, float yaw)[] Spots =
        {
            (new Vector3(0.6f, 0.01f, -3.4f), 20f),    // 1 : près du point de départ
            (new Vector3(-3.0f, 0.81f, 1.0f), -15f),   // sur la caisse basse
            (new Vector3(-4.5f, 1.61f, 2.5f), 30f),    // sur la caisse moyenne
            (new Vector3(2.0f, 0.01f, 1.0f), 80f),
            (new Vector3(-1.5f, 0.01f, 3.5f), -40f),
            (new Vector3(4.2f, 0.01f, -1.0f), 10f),
            (new Vector3(3.5f, 1.6f, 3.2f), 0f),      // au-dessus de la rampe (il glisse dessus)
            (new Vector3(-6.0f, 2.61f, 4.5f), 45f),    // en haut de la caisse haute (grimper !)
            (new Vector3(5.5f, 3.16f, 5.5f), -20f),    // sur la plateforme haute
            (new Vector3(-5.5f, 0.01f, -2.0f), 60f),
            (new Vector3(0.0f, 0.01f, 5.8f), 0f),
            (new Vector3(6.0f, 0.01f, 1.5f), -70f),
            (new Vector3(-2.5f, 0.01f, 6.2f), 15f),    // 13 : le papier au crayon, au fond
            (new Vector3(5.9f, 3.16f, 6.4f), 50f),     // secrets : en hauteur et dans les coins
            (new Vector3(-6.6f, 0.01f, 6.6f), 0f),
            (new Vector3(6.6f, 0.01f, -6.4f), 30f),
            (new Vector3(-6.2f, 2.61f, 3.9f), -10f),
        };

        [MenuItem("Horror/6 - Ajouter les papiers de lore + attraper et lancer")]
        public static void Build()
        {
            ArmLocomotion player = Find<ArmLocomotion>();
            if (player == null)
            {
                EditorUtility.DisplayDialog("Pas de joueur", "Lance d'abord Horror > 3 (salle de test).", "OK");
                return;
            }
            string json = PapersFolder + "papers.json";
            if (!File.Exists(json))
            {
                EditorUtility.DisplayDialog("Papiers introuvables", "Il manque " + json + ".\nLance Tools/Papers/make_papers.py.", "OK");
                return;
            }

            Undo.SetCurrentGroupName("Papiers de lore");
            int group = Undo.GetCurrentGroup();

            LoreDocument[] docs = EnsureDocuments(JsonUtility.FromJson<PaperList>("{\"items\":" + File.ReadAllText(json) + "}").items);
            PlacePapers(docs);
            AddJournal(docs);
            AddDrawings();
            SetupHands(player);
            MakeBoxesGrabbable();

            Undo.CollapseUndoOperations(group);
            Debug.Log("[Horror] " + docs.Length + " papiers posés. Grip pour attraper, approche le papier de ton visage pour le lire.");
        }

        /// <summary>Lit papers.json et renvoie les LoreDocument (créés si besoin). Null si les images manquent.</summary>
        internal static LoreDocument[] LoadDocuments()
        {
            string json = PapersFolder + "papers.json";
            if (!File.Exists(json)) return null;
            return EnsureDocuments(JsonUtility.FromJson<PaperList>("{\"items\":" + File.ReadAllText(json) + "}").items);
        }

        private static LoreDocument[] EnsureDocuments(PaperInfo[] infos)
        {
            if (!AssetDatabase.IsValidFolder(DocsFolder)) AssetDatabase.CreateFolder("Assets/Horror", "Lore");
            var docs = new LoreDocument[infos.Length];
            for (int i = 0; i < infos.Length; i++)
            {
                PaperInfo p = infos[i];
                string path = DocsFolder + "/" + p.id + ".asset";
                var doc = AssetDatabase.LoadAssetAtPath<LoreDocument>(path);
                if (doc == null)
                {
                    doc = ScriptableObject.CreateInstance<LoreDocument>();
                    doc.id = p.id;
                    doc.title = p.title;
                    doc.order = p.order;
                    doc.isSecret = p.secret;
                    doc.coinReward = p.secret ? 60 : 25;
                    doc.front = AssetDatabase.LoadAssetAtPath<Texture2D>(PapersFolder + p.id + ".jpg");
                    if (p.back) doc.back = AssetDatabase.LoadAssetAtPath<Texture2D>(PapersFolder + p.id + "_back.jpg");
                    float ratio = p.size != null && p.size.Length == 2 ? p.size[1] / (float)p.size[0] : 1.414f;
                    doc.size = new Vector2(0.148f, 0.148f * ratio);
                    AssetDatabase.CreateAsset(doc, path);
                }
                docs[i] = doc;
            }
            AssetDatabase.SaveAssets();
            return docs;
        }

        private static void PlacePapers(LoreDocument[] docs)
        {
            var root = new GameObject("Papiers de lore");
            Undo.RegisterCreatedObjectUndo(root, "Papiers");
            AudioClip pickup = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Horror/Audio/Papers/paper_pickup.wav");
            AudioClip found = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Horror/Audio/Papers/paper_found.wav");
            for (int i = 0; i < docs.Length; i++)
            {
                var (pos, yaw) = Spots[i % Spots.Length];
                var go = new GameObject("Papier " + docs[i].order + " - " + docs[i].title);
                go.transform.SetParent(root.transform, false);
                go.transform.position = pos + Vector3.up * 0.005f;
                go.transform.rotation = Quaternion.Euler(-90f, yaw, 0f);   // posé à plat, texte vers le haut
                go.AddComponent<Rigidbody>();
                go.AddComponent<Grabbable>();
                LorePaper paper = go.AddComponent<LorePaper>();
                paper.document = docs[i];
                paper.pickupSound = pickup;
                paper.foundSound = found;
            }
        }

        private static void AddJournal(LoreDocument[] docs)
        {
            SafeZone zone = Find<SafeZone>();
            var go = new GameObject("Journal des papiers");
            Undo.RegisterCreatedObjectUndo(go, "Journal");
            if (zone != null)
            {
                go.transform.SetParent(zone.transform, false);
                go.transform.localPosition = new Vector3(-3.4f, 2.0f, -1.95f);
            }
            else go.transform.position = new Vector3(0f, 1.8f, -6.9f);
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            TextMesh tm = go.AddComponent<TextMesh>();
            tm.characterSize = 0.018f;
            tm.fontSize = 64;
            tm.anchor = TextAnchor.UpperCenter;
            tm.color = new Color(0.93f, 0.88f, 0.8f);
#if UNITY_2022_2_OR_NEWER
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
            Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
            if (font != null) { tm.font = font; go.GetComponent<MeshRenderer>().sharedMaterial = font.material; }
            go.AddComponent<JournalBoard>().documents = docs;
        }

        private static void AddDrawings()
        {
            string[] names = { "drawing_lady", "drawing_friends", "drawing_pool", "drawing_family" };
            if (!AssetDatabase.IsValidFolder(MatFolder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Horror/Art/Materials")) AssetDatabase.CreateFolder("Assets/Horror/Art", "Materials");
                AssetDatabase.CreateFolder("Assets/Horror/Art/Materials", "Drawings");
            }
            var root = new GameObject("Dessins d'enfants (mur)");
            Undo.RegisterCreatedObjectUndo(root, "Dessins");
            for (int i = 0; i < names.Length; i++)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(DrawFolder + names[i] + ".png");
                if (tex == null) continue;
                string matPath = MatFolder + "/" + names[i] + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (shader == null || UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null) shader = Shader.Find("Standard");
                    mat = new Material(shader);
                    if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                    if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
                    AssetDatabase.CreateAsset(mat, matPath);
                }
                GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(q.GetComponent<Collider>());
                q.name = names[i];
                q.transform.SetParent(root.transform, false);
                float aspect = tex.width / (float)tex.height;
                q.transform.localScale = new Vector3(0.6f * aspect, 0.6f, 1f);
                q.transform.position = new Vector3(-4.5f + i * 2.6f, 1.7f + (i % 2) * 0.25f, 6.98f);   // mur du fond
                q.transform.rotation = Quaternion.Euler(0f, 0f, (i % 2 == 0 ? 3f : -4f));
                q.GetComponent<Renderer>().sharedMaterial = mat;
            }
        }

        internal static void SetupHands(ArmLocomotion player)
        {
            AddGrabber(player.leftHandVisual, XRNode.LeftHand);
            AddGrabber(player.rightHandVisual, XRNode.RightHand);
        }

        private static void AddGrabber(Transform hand, XRNode node)
        {
            if (hand == null || hand.GetComponent<HandGrabber>() != null) return;
            HandGrabber g = Undo.AddComponent<HandGrabber>(hand.gameObject);
            g.node = node;
            g.fingers = hand.GetComponentInChildren<Horror.Player.HandFingerAnimator>();
        }

        private static void MakeBoxesGrabbable()
        {
#if UNITY_2022_2_OR_NEWER
            ImpactNoise[] all = Object.FindObjectsByType<ImpactNoise>(FindObjectsSortMode.None);
#else
            ImpactNoise[] all = Object.FindObjectsOfType<ImpactNoise>();
#endif
            foreach (ImpactNoise n in all)
                if (n.GetComponent<Grabbable>() == null) Undo.AddComponent<Grabbable>(n.gameObject);
        }

        private static T Find<T>() where T : Object
        {
#if UNITY_2022_2_OR_NEWER
            return Object.FindAnyObjectByType<T>();
#else
            return Object.FindObjectOfType<T>();
#endif
        }
    }
}
