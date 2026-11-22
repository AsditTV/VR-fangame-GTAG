using System.Collections.Generic;
using System.IO;
using Horror.Audio;
using Horror.Cosmetics;
using Horror.Interaction;
using Horror.Locomotion;
using Horror.Lore;
using Horror.Monsters;
using Horror.Player;
using Horror.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Horror.EditorTools
{
    /// <summary>
    /// Menu "Horror > 8 - Construire l'Institut jouable (map complète)" : crée la scène
    /// Assets/Horror/Scenes/Institut.unity à partir de la maquette (Institut_Maquette.glb + _Unity.json) :
    ///  - collisions sur tout le décor (on grimpe partout aux bras), encre de la piscine traversable ;
    ///  - nuit : pas de soleil, brouillard noir, les lumières colorées de la maquette ;
    ///  - ambiances sonores par pièce (sous-sol, dortoir...), Salle de jeux = zone sûre + berceuse ;
    ///  - boutique (5 présentoirs), panneau des pièces, journal des papiers ;
    ///  - 14 portes du Protocole Verrou avec boutons des deux côtés ;
    ///  - salle des machines en boucle, chaises volantes, ventilateur, 22 petites portes qui frappent ;
    ///  - deux Écoutes : une en haut, une au sous-sol ;
    ///  - les 17 papiers de lore à leur place ;
    ///  - L'Écoute (voix, animation, navigation de pièce en pièce, ouvre les portes) ;
    ///  - le joueur (poupée, mains, glisse, jumpscare, pièces...).
    /// Il faut le package glTFast. Ne remplace jamais une scène existante.
    /// </summary>
    public static partial class InstitutBuilder
    {
        private const string ScenePath = "Assets/Horror/Scenes/Institut.unity";
        private const string ModelPath = "Assets/Horror/Art/Map/Institut_Maquette.glb";
        private const string DataPath = "Assets/Horror/Art/Map/Institut_Maquette_Unity.json";
        private const string Audio = "Assets/Horror/Audio/";

#pragma warning disable 0649   // champs remplis par JsonUtility
        [System.Serializable] private class LightData { public Vector3 pos; public string color; public float intensity; public float distance; }
        [System.Serializable] private class DoorData { public float x, z, width, bottom, top; public string axis; }
        [System.Serializable] private class RoomData { public string zone, name; public Vector3 min, max, center; }
        [System.Serializable] private class PaperData { public string id; public Vector3 pos, face; public float spin; }
        [System.Serializable] private class ChildDoorData { public int id; public string name; public Vector3 pos, face; }
        [System.Serializable] private class MapData
        {
            public Vector3 spawn; public float spawnYaw;
            public LightData[] lights; public DoorData[] doors; public RoomData[] rooms;
            public Vector3[] navPoints; public Vector2Int[] navLinks; public PaperData[] papers;
            public ChildDoorData[] childDoors;
            public float worldScale;   // la maquette est agrandie (pièces plus grandes) : coordonnées x worldScale
        }
#pragma warning restore 0649

        // Correspondance d'axes maquette (glTF) -> Unity, mesurée sur le modèle importé
        private static float s_sx = 1f, s_sz = 1f, s_w = 1f;
        /// <summary>Position maquette -> Unity (axes + agrandissement du monde).</summary>
        private static Vector3 P(Vector3 v) => new Vector3(v.x * s_sx, v.y, v.z * s_sz) * s_w;
        private static Vector3 Dir(Vector3 v) => new Vector3(v.x * s_sx, v.y, v.z * s_sz);

        [MenuItem("Horror/8 - Construire l'Institut jouable (map complète)")]
        public static void Build()
        {
            if (File.Exists(ScenePath))
            {
                if (EditorUtility.DisplayDialog("Institut", "La scène existe déjà :\n" + ScenePath + "\n\nJe ne l'écrase pas.", "L'ouvrir", "Annuler")
                    && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    EditorSceneManager.OpenScene(ScenePath);
                return;
            }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null || !File.Exists(DataPath))
            {
                EditorUtility.DisplayDialog("Institut", "Il manque la maquette importée.\nInstalle le package glTFast (Window > Package Manager), " +
                                                        "puis vérifie que " + ModelPath + " apparaît dans le projet.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            MapData data = JsonUtility.FromJson<MapData>(File.ReadAllText(DataPath));
            s_w = data.worldScale > 0f ? data.worldScale : 1f;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject map = (GameObject)PrefabUtility.InstantiatePrefab(model);
            map.name = "Institut (maquette)";
            map.isStatic = true;
            DetectAxes(map);
            SetupGeometry(map);
            SetupMovingProps(map);
            SetupSofties(map, data);
            SetupDormChildren();
            SetupNight(data);
            RoomData playroom = SetupRooms(data);
            CosmeticItem[] items = SafeZoneShopBuilder.EnsureItems();
            SetupShop(map, playroom, items);
            SetupMinitel();
            SetupDoors(data);
            LoreDocument[] docs = LoreSetupBuilder.LoadDocuments();
            SetupPapers(data, docs);
            SetupThrowables(data);
            SetupMusicBoxes(data);
            SetupHopital();
            SetupGrenier(data);
            SetupChildDoors(data);
            SetupJournal(playroom, docs);
            WaypointGraph graph = SetupNav(data);
            SetupLaverie(map);
            SetupLobbyAndGoals(data, playroom);
            SetupDecor(data);
            EnrichAmbience();
            SetupLoge(map);
            SetupStandardPhone();
            SetupChangingDrawing();
            SetupInkTrails(graph, playroom);
            SetupBlackout();
            SetupMonster(data, graph);
            SetupPlayer(data, items);
            SetupDeath();
            MergeDecorModels();
            MarkStaticDecor();
            SetupLook();
            new GameObject("Réseau (solo tant que Photon n'est pas installé)").AddComponent<Horror.Net.NetworkBootstrap>();
            new GameObject("Profil graphique (Quest : automatique)").AddComponent<Horror.Core.QuestQuality>();

            EnsureFolder("Assets/Horror/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[Horror] Institut jouable créé : " + ScenePath + ". Branche le casque et appuie sur Play.");
        }

        // ------------------------------------------------------------------ axes

        /// <summary>
        /// L'import glTF peut inverser un axe. On mesure : le couloir des chambres est vers +X dans la maquette
        /// (centre x ≈ 19) et l'aile école vers +Z (z ≈ 18). Si Unity les met de l'autre côté, on inverse.
        /// </summary>
        private static void DetectAxes(GameObject map)
        {
            s_sx = s_sz = 1f;
            Bounds? couloir = BoundsOf(map, "Couloir_parquet");
            Bounds? ecole = BoundsOf(map, "Ecole_parquet");
            if (couloir.HasValue && couloir.Value.center.x < 0f) s_sx = -1f;
            if (ecole.HasValue && ecole.Value.center.z < 0f) s_sz = -1f;
            Debug.Log("[Horror] Axes maquette -> Unity : X x" + s_sx + ", Z x" + s_sz);
        }

        private static Bounds? BoundsOf(GameObject root, string name)
        {
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                if (r.gameObject.name.StartsWith(name)) return r.bounds;
            return null;
        }

        // ------------------------------------------------------------------ décor

        private static void SetupGeometry(GameObject map)
        {
            foreach (MeshFilter mf in map.GetComponentsInChildren<MeshFilter>(true))
            {
                string n = mf.gameObject.name;
                mf.gameObject.isStatic = true;
                if (n.Contains("_Papier_") || n.Contains("Panneau_Pieces"))
                {   // remplacés par les vrais papiers à ramasser / le compteur de pièces
                    Renderer r = mf.GetComponent<Renderer>();
                    if (r != null) r.enabled = false;
                    continue;
                }
                if (n.StartsWith("Piscine_encre")) continue;   // l'encre ne porte pas : on s'y enfonce (2 m de noir)
                if (n.Contains("Cosmetique_") || n.Contains("Prix_")) continue;
                if (n.Contains("ChaiseVolante_") || n.Contains("Pales_Ventilateur")) continue;   // objets animés (SetupMovingProps)
                if (n.Contains("TeteDoux_") || n.Contains("DouxMarcheur_")) continue;           // les Doux (SetupSofties)
                MeshCollider col = mf.gameObject.AddComponent<MeshCollider>();
                col.sharedMesh = mf.sharedMesh;
            }
        }

        /// <summary>
        /// Chaises volantes et pales du ventilateur : dans la maquette, leurs sommets sont en coordonnées
        /// du monde (pivot à l'origine). On crée une copie avec un pivot au centre de l'objet pour pouvoir
        /// la faire tourner / tomber, et on cache l'original (le lien avec la maquette est gardé).
        /// </summary>
        private static void SetupMovingProps(GameObject map)
        {
            var root = new GameObject("Objets animés (chaises, ventilateur)");
            AudioClip[] clatter = LoadMany(Audio + "World", "chair_clatter_");
            AudioClip shiver = Load<AudioClip>(Audio + "World/chair_shiver.wav");
            foreach (MeshFilter mf in map.GetComponentsInChildren<MeshFilter>(true))
            {
                string n = mf.gameObject.name;
                bool chair = n.Contains("ChaiseVolante_");
                bool fan = n.Contains("Pales_Ventilateur");
                if (!chair && !fan) continue;
                Renderer src = mf.GetComponent<Renderer>();
                if (src == null) continue;

                var pivot = new GameObject(chair ? "Chaise volante" : "Pales du grand ventilateur");
                pivot.transform.SetParent(root.transform, false);
                pivot.transform.position = src.bounds.center;
                var copy = new GameObject("Maillage");
                copy.transform.SetPositionAndRotation(mf.transform.position, mf.transform.rotation);
                copy.transform.localScale = mf.transform.lossyScale;
                copy.transform.SetParent(pivot.transform, true);
                copy.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                copy.AddComponent<MeshRenderer>().sharedMaterials = src.sharedMaterials;
                src.enabled = false;

                if (fan)
                {
                    SlowSpinner spin = pivot.AddComponent<SlowSpinner>();
                    spin.axis = Dir(Vector3.right);   // l'axe du ventilateur est l'axe X de la maquette
                    continue;
                }
                MeshCollider col = copy.AddComponent<MeshCollider>();
                col.sharedMesh = mf.sharedMesh;
                col.convex = true;                    // obligatoire pour un objet physique
                Rigidbody rb = pivot.AddComponent<Rigidbody>();
                rb.mass = 2f;
                FloatingProp prop = pivot.AddComponent<FloatingProp>();
                prop.shiverSound = shiver;
                Horror.Noise.ImpactNoise noise = pivot.AddComponent<Horror.Noise.ImpactNoise>();
                noise.hitClips = clatter;
                noise.maxRadius = 14f;
            }
        }

        /// <summary>
        /// Les Doux du théâtre : têtes qui se tournent quand on ne regarde pas, et le marcheur
        /// (« 1, 2, 3, soleil »). Mêmes copies à pivot que les chaises.
        /// </summary>
        private static void SetupSofties(GameObject map, MapData data)
        {
            if (SetupSoftieModels(map, data)) return;   // nouveaux modèles Blender (Art/Monsters/Doux) si présents
            var heads = new Dictionary<string, List<MeshFilter>>();
            var walkerBody = new List<MeshFilter>();
            var walkerHead = new List<MeshFilter>();
            foreach (MeshFilter mf in map.GetComponentsInChildren<MeshFilter>(true))
            {
                string n = mf.gameObject.name;
                int i = n.IndexOf("TeteDoux_");
                if (i >= 0)
                {
                    string id = n.Substring(i + 9).Split('_')[0];
                    if (!heads.ContainsKey(id)) heads[id] = new List<MeshFilter>();
                    heads[id].Add(mf);
                }
                else if (n.Contains("DouxMarcheur_corps_")) walkerBody.Add(mf);
                else if (n.Contains("DouxMarcheur_tete_")) walkerHead.Add(mf);
            }
            if (heads.Count == 0 && walkerBody.Count == 0) return;

            var root = new GameObject("Les Doux (1, 2, 3, soleil)");
            Quaternion facing = Quaternion.LookRotation(Dir(Vector3.forward), Vector3.up);   // ils regardent le rideau
            foreach (var kv in heads)
            {
                Transform pivot = MakePivot(root.transform, "Tête de Doux " + kv.Key, kv.Value, CenterOf(kv.Value), facing);
                pivot.gameObject.AddComponent<SoftieWatcher>();
            }

            Bounds theatre = default;
            foreach (RoomData r in data.rooms)
                if (r.zone == "Theatre")
                {
                    Vector3 a = P(r.min), b = P(r.max);
                    theatre = new Bounds((a + b) * 0.5f, new Vector3(Mathf.Abs(b.x - a.x) - 0.6f, 20f, Mathf.Abs(b.z - a.z) - 0.6f));
                }
            if (walkerBody.Count > 0)
            {
                Vector3 c = CenterOf(walkerBody);
                Vector3 feet = new Vector3(c.x, MinYOf(walkerBody), c.z);
                Transform body = MakePivot(root.transform, "Le Doux qui avance", walkerBody, feet, facing);
                Transform head = walkerHead.Count > 0 ? MakePivot(body, "Tête", walkerHead, CenterOf(walkerHead), facing) : body;
                SoftieWatcher w = body.gameObject.AddComponent<SoftieWatcher>();
                w.head = head;
                w.canWalk = true;
                w.area = theatre;
                w.shuffleLoop = Load<AudioClip>(Audio + "World/softie_shuffle.wav");
                w.settleSound = Load<AudioClip>(Audio + "World/softie_settle.wav");
            }
        }

        // ------------------------------------------------------------------ dortoir : les autres enfants

        /// <summary>
        /// Six poupées comme le joueur, assises sur des lits du dortoir aux 23 lits, bras ballants.
        /// Leur tête se tourne vers toi quand tu ne regardes pas (SoftieWatcher sans marche) :
        /// en te retournant, tu les retrouves figées dans une autre position. Le lit sans masque
        /// (le tien, lit n° 5) reste vide. Positions : Tools/Map/build_institut.py (dortoir).
        /// </summary>
        private static void SetupDormChildren()
        {
            var proto = Load<GameObject>("Assets/Horror/Art/Character/LeDessine_PNJ.glb");
            if (proto == null) return;
            var root = new GameObject("Les enfants du dortoir");
            // (côté, rang) : côté -1 = rangée x = -6,3 (12 lits), côté 1 = rangée x = +6,3 (11 lits) ; jamais le lit vide
            int[,] beds = { { -1, 1 }, { -1, 3 }, { -1, 8 }, { 1, 2 }, { 1, 6 }, { 1, 9 } };
            for (int i = 0; i < beds.GetLength(0); i++)
            {
                int side = beds[i, 0], k = beds[i, 1];
                float x = side * 6.3f, z = 13.6f + k * 1.35f;
                // assise sur le bord du matelas (dessus à 0,5 m), tournée vers l'allée
                Vector3 seat = P(new Vector3(x - side * 0.45f, 0.5f, z));
                Quaternion face = Quaternion.LookRotation(Flat(Dir(new Vector3(-side, 0f, 0f))), Vector3.up)
                                  * Quaternion.Euler(0f, (i % 2 == 0 ? 12f : -9f), 0f);
                var doll = (GameObject)PrefabUtility.InstantiatePrefab(proto);
                doll.name = "Enfant du lit " + (side < 0 ? "G" : "D") + k;
                doll.transform.SetParent(root.transform, false);
                doll.transform.SetPositionAndRotation(seat - Vector3.up * 0.19f, face);   // le bas du sac posé sur le lit

                // bras qui pendent, mains vers les genoux
                foreach (string s in new[] { "G", "D" })
                {
                    Transform ep = FindChild(doll.transform, "Epaule_" + s);
                    Transform co = FindChild(doll.transform, "Coude_" + s);
                    if (ep != null) ep.rotation = face * Quaternion.Euler(-28f, 0f, s == "G" ? 8f : -8f);
                    if (co != null) co.localRotation = Quaternion.Euler(-35f, 0f, 0f);
                }
                Transform head = FindChild(doll.transform, "Tete");
                if (head != null) head.localRotation = Quaternion.Euler(0f, 0f, (i % 3 - 1) * 14f);   // tête penchée
                SoftieWatcher w = doll.AddComponent<SoftieWatcher>();
                w.head = head;
                w.range = 9f;
                w.headTurnSpeed = 90f;
            }

            // Cachettes : sur chacun des 23 lits, assis parmi les poupées, tes bruits sont étouffés (« faire la poupée »)
            var hides = new GameObject("Cachettes (lits du dortoir)");
            for (int side = -1; side <= 1; side += 2)
                for (int k = 0; k < 12; k++)
                {
                    if (side == 1 && k == 11) continue;   // 23 lits : 12 d'un côté, 11 de l'autre
                    var z = new GameObject("Lit " + (side < 0 ? "G" : "D") + k);
                    z.transform.SetParent(hides.transform, false);
                    z.transform.position = P(new Vector3(side * 6.3f, 0.95f, 13.6f + k * 1.35f));
                    Horror.Noise.HideZone hz = z.AddComponent<Horror.Noise.HideZone>();
                    hz.size = new Vector3(1.84f, 0.9f, 0.84f) * s_w;
                    hz.muffle = 0.35f;
                }
        }

        // ------------------------------------------------------------------ la Laverie (derrière la porte 23) et Le Drap

        /// <summary>
        /// La Laverie, derrière la porte 23 au bout du couloir des chambres (« il n'y avait que 22 chambres »).
        /// Salle carrelée inondée d'eau savonneuse : le territoire du Drap (Odette, la lingère), qui sent le sol.
        /// On la traverse par les machines, la table, les tuyaux et les fils à linge. L'Écoute n'y entre pas
        /// (l'eau dissout l'encre). Coordonnées en unités de la maquette (comme build_institut.py), x WORLD_SCALE.
        /// </summary>
        private static void SetupLaverie(GameObject map)
        {
            var root = new GameObject("Laverie (porte 23)");
            Material tile = MakeMat("Laverie_Carrelage", new Color(0.78f, 0.84f, 0.82f));
            Material wall = MakeMat("Laverie_Mur", new Color(0.62f, 0.70f, 0.70f));
            Material metal = MakeMat("Laverie_Machine", new Color(0.86f, 0.86f, 0.80f));
            Material glass = MakeMat("Laverie_Hublot", new Color(0.10f, 0.12f, 0.14f));
            Material wood = MakeMat("Laverie_Bois", new Color(0.55f, 0.42f, 0.36f));
            Material pipe = MakeMat("Laverie_Tuyau", new Color(0.50f, 0.58f, 0.55f));
            Material sheet = MakeMat("Laverie_Drap", new Color(0.88f, 0.90f, 0.93f));
            Material ink = MakeMat("Encre_Sol", new Color(0.012f, 0.008f, 0.016f));
            Material water = MakeMat("Laverie_Eau", new Color(0.55f, 0.63f, 0.66f));
            if (water.HasProperty("_Smoothness")) water.SetFloat("_Smoothness", 0.93f);
            const string Props = "Assets/Horror/Art/Map/Laverie/";
            Texture2D tileTex = Load<Texture2D>(Props + "laverie_carrelage.png"), wallTex = Load<Texture2D>(Props + "laverie_mur.png");
            if (tileTex != null) { tile.color = Color.white; tile.mainTexture = tileTex; tile.mainTextureScale = new Vector2(14f, 16f); }
            if (wallTex != null) { wall.color = Color.white; wall.mainTexture = wallTex; wall.mainTextureScale = new Vector2(12f, 5f); }
            Color[] enamel = { new Color(0.72f, 0.86f, 0.79f), new Color(0.92f, 0.76f, 0.80f), new Color(0.95f, 0.91f, 0.82f),
                               new Color(0.80f, 0.75f, 0.90f) };
            // habille une boîte de collision avec un objet Blender (la boîte garde sa collision, on cache son cube)
            GameObject Dress(GameObject box, string prop, Vector3 faceDir, int tint = -1)
            {
                var proto = Load<GameObject>(Props + prop + ".glb");
                if (proto == null) return null;
                Bounds b = box.GetComponent<Renderer>().bounds;
                box.GetComponent<Renderer>().enabled = false;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(proto);
                go.transform.SetParent(root.transform, false);
                go.transform.position = new Vector3(b.center.x, b.min.y, b.center.z);
                go.transform.rotation = Quaternion.LookRotation(Flat(Dir(faceDir)), Vector3.up);
                go.transform.localScale = Vector3.one * s_w;
                if (tint >= 0)
                    foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                    {
                        Material[] mats = r.sharedMaterials;
                        for (int i = 0; i < mats.Length; i++)
                            if (mats[i] != null && mats[i].name.StartsWith("Email")) mats[i] = Tinted(mats[i], "Laverie_Email_" + tint, enamel[tint % enamel.Length]);
                        r.sharedMaterials = mats;
                    }
                return go;
            }
            Color[] pastel = { new Color(0.88f, 0.70f, 0.75f), new Color(0.70f, 0.84f, 0.77f), new Color(0.78f, 0.71f, 0.86f),
                               new Color(0.91f, 0.84f, 0.63f), new Color(0.70f, 0.77f, 0.88f) };

            GameObject Box(string name, Material m, float x0, float x1, float y0, float y1, float z0, float z1, bool solid = true)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = name;
                go.transform.SetParent(root.transform, false);
                go.transform.position = P(new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f));
                go.transform.localScale = new Vector3(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0), Mathf.Abs(z1 - z0)) * s_w;
                go.GetComponent<Renderer>().sharedMaterial = m;
                if (!solid) Object.DestroyImmediate(go.GetComponent<Collider>());
                go.isStatic = true;
                return go;
            }
            GameObject Bar(string name, Material m, Vector3 a, Vector3 b, float r, bool solid = true)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = name;
                go.transform.SetParent(root.transform, false);
                Vector3 wa = P(a), wb = P(b);
                go.transform.position = (wa + wb) * 0.5f;
                go.transform.up = (wb - wa).normalized;
                go.transform.localScale = new Vector3(r * 2f * s_w, (wb - wa).magnitude * 0.5f, r * 2f * s_w);
                go.GetComponent<Renderer>().sharedMaterial = m;
                if (!solid) Object.DestroyImmediate(go.GetComponent<Collider>());
                go.isStatic = true;
                return go;
            }
            void Sign(string text, Vector3 pos, Vector3 faceDir, float size, Color c)
            {
                GameObject l = SafeZoneShopBuilder.Label(root.transform, text, Vector3.zero, size * 2.2f, c);   // lisible de loin en VR
                l.transform.position = P(pos);
                l.transform.rotation = Quaternion.LookRotation(-Flat(Dir(faceDir)));
            }

            // 1) on ouvre la porte 23 : le bloc noir qui bouchait l'ouverture est découpé (collision comprise)
            Vector3 c0 = P(new Vector3(30.85f, 0f, -0.55f)), c1 = P(new Vector3(31.25f, 2.2f, 0.55f));
            Bounds blocker = new Bounds((c0 + c1) * 0.5f, new Vector3(Mathf.Abs(c1.x - c0.x), Mathf.Abs(c1.y - c0.y), Mathf.Abs(c1.z - c0.z)));
            foreach (MeshFilter mf in map.GetComponentsInChildren<MeshFilter>(true))
                if (mf.gameObject.name.Contains("noir_cable")) CutOutBox(mf, blocker);

            // 2) le passage de service, avec une marche vers le rebord d'entrée
            Box("Laverie_Passage_carrelage", tile, 31.0f, 34.3f, -0.1f, 0f, -0.75f, 0.75f);
            Box("Laverie_Passage_mur_N", wall, 31.0f, 34.3f, 0f, 2.4f, 0.75f, 0.85f);
            Box("Laverie_Passage_mur_S", wall, 31.0f, 34.3f, 0f, 2.4f, -0.85f, -0.75f);
            Box("Laverie_Passage_plafond", wall, 31.0f, 34.3f, 2.4f, 2.5f, -0.85f, 0.85f);
            Box("Laverie_Marche_carrelage", tile, 33.6f, 34.3f, 0f, 0.4f, -0.7f, 0.7f);
            Sign("23\nLAUNDRY", new Vector3(30.95f, 2.0f, 0f), new Vector3(-1f, 0f, 0f), 0.016f, new Color(0.9f, 0.9f, 0.95f));

            // 3) la salle : sol carrelé (le sol du Drap), murs, plafond, eau savonneuse (sans collision)
            const float X0 = 34.3f, X1 = 47f, Z0 = -7f, Z1 = 7f, H = 5.5f;
            Box("Laverie_Sol_carrelage", tile, X0, X1, -0.1f, 0f, Z0, Z1);
            Box("Laverie_Mur_E", wall, X1, X1 + 0.15f, 0f, H, Z0, Z1);
            Box("Laverie_Mur_N", wall, X0, X1, 0f, H, Z1, Z1 + 0.15f);
            Box("Laverie_Mur_S", wall, X0, X1, 0f, H, Z0 - 0.15f, Z0);
            Box("Laverie_Mur_O_haut", wall, X0 - 0.15f, X0, 2.4f, H, -0.85f, 0.85f);
            Box("Laverie_Mur_O_n", wall, X0 - 0.15f, X0, 0f, H, 0.85f, Z1);
            Box("Laverie_Mur_O_s", wall, X0 - 0.15f, X0, 0f, H, Z0, -0.85f);
            Box("Laverie_Plafond", wall, X0, X1, H, H + 0.15f, Z0, Z1);
            Box("Laverie_Eau", water, X0, X1, 0f, 0.07f, Z0, Z1, solid: false);   // eau aux chevilles
            Box("Laverie_Rebord_entree_carrelage", tile, X0, X0 + 1.2f, 0f, 0.8f, -1.6f, 1.6f);

            // 4) les machines : deux îlots + une rangée contre chaque grand mur ; quelques sèche-linge empilés
            int n = 0;
            for (int row = 0; row < 4; row++)
            {
                float z = row == 0 ? -6.45f : row == 1 ? -3.2f : row == 2 ? 3.2f : 6.45f;
                for (int k = 0; k < 8; k++)
                {
                    float x = 36.0f + k * 1.25f + (row % 2) * 0.35f;
                    if (x > 45.5f) continue;
                    n++;
                    GameObject mach = Box("Laverie_Machine_" + n + "_metal", metal, x - 0.42f, x + 0.42f, 0f, 0.95f, z - 0.4f, z + 0.4f);
                    float face = (row < 2) ? 1f : -1f;
                    if (Dress(mach, "Machine", new Vector3(0f, 0f, face), n % 4) == null)
                        Bar("Laverie_Hublot_" + n, glass, new Vector3(x, 0.5f, z + face * 0.39f), new Vector3(x, 0.5f, z + face * 0.43f), 0.24f, solid: false);
                    if ((k + row) % 3 == 1)
                        Dress(Box("Laverie_SecheLinge_" + n + "_metal", metal, x - 0.4f, x + 0.4f, 0.95f, 1.85f, z - 0.38f, z + 0.38f),
                              "SecheLinge", new Vector3(0f, 0f, face), (n + 2) % 4);
                }
            }
            // la table à plier, au milieu (pieds dans l'eau)
            GameObject tableTop = Box("Laverie_Table_bois", wood, 37.4f, 43.6f, 0.82f, 0.9f, -0.55f, 0.55f);
            var tableLegs = new List<GameObject>();
            foreach (float tx in new[] { 37.6f, 40.5f, 43.4f })
                foreach (float tz in new[] { -0.45f, 0.45f })
                    tableLegs.Add(Box("Laverie_Pied_metal", pipe, tx - 0.04f, tx + 0.04f, 0f, 0.82f, tz - 0.04f, tz + 0.04f));
            var tableProto = Load<GameObject>(Props + "Table.glb");
            if (tableProto != null)
            {
                foreach (GameObject g in tableLegs) g.GetComponent<Renderer>().enabled = false;
                tableTop.GetComponent<Renderer>().enabled = false;
                var tableGo = (GameObject)PrefabUtility.InstantiatePrefab(tableProto);
                tableGo.transform.SetParent(root.transform, false);
                tableGo.transform.position = P(new Vector3(40.5f, 0f, 0f));
                tableGo.transform.localScale = Vector3.one * s_w;
            }

            // 5) prises en hauteur : tuyaux le long des murs, fils à linge en travers (on s'y suspend)
            foreach (float z in new[] { Z0 + 0.25f, Z1 - 0.25f })
            {
                Bar("Laverie_Tuyau_bas_metal", pipe, new Vector3(X0 + 0.3f, 2.0f, z), new Vector3(X1 - 0.3f, 2.0f, z), 0.08f);
                Bar("Laverie_Tuyau_haut_metal", pipe, new Vector3(X0 + 0.3f, 3.3f, z), new Vector3(X1 - 0.3f, 3.3f, z), 0.06f);
            }
            int sheetCount = 0;
            foreach (float x in new[] { 37.2f, 39.4f, 41.6f, 43.8f })
            {
                Bar("Laverie_Fil_a_linge", pipe, new Vector3(x, 2.75f, Z0 + 0.2f), new Vector3(x, 2.75f, Z1 - 0.2f), 0.025f);
                for (float z = Z0 + 1.5f; z < Z1 - 1.5f; z += 2.6f)
                {
                    if (++sheetCount % 3 == 0) continue;
                    GameObject sh = Box("Laverie_Drap_pendu", sheet, x - 0.01f, x + 0.01f, 1.55f, 2.72f, z - 0.55f, z + 0.55f);   // collision juste le temps de se placer
                    Dress(sh, "DrapPendu", Vector3.forward);
                    Object.DestroyImmediate(sh.GetComponent<Collider>());   // on passe à travers les draps
                }
            }

            // 6) le fond : étagère, rebord, machine 23 ouverte, et les 23 petites chemises sur un fil
            Box("Laverie_Rebord_fond_carrelage", tile, X1 - 1.4f, X1, 0f, 0.9f, -1.8f, 1.8f);
            foreach (float y in new[] { 1.7f, 2.5f })
                Box("Laverie_Etagere_bois", wood, X1 - 0.45f, X1, y, y + 0.06f, -3.2f, 3.2f);
            Box("Laverie_Machine_23_metal", metal, X1 - 1.25f, X1 - 0.4f, 0.9f, 1.85f, -0.42f, 0.42f);
            Box("Laverie_Machine_23_encre", ink, X1 - 1.27f, X1 - 1.24f, 1.05f, 1.6f, -0.25f, 0.25f, solid: false);
            Sign("No.23\nDO NOT OPEN", new Vector3(X1 - 1.3f, 1.75f, 0f), new Vector3(-1f, 0f, 0f), 0.009f, new Color(0.2f, 0.18f, 0.22f));
            Bar("Laverie_Fil_chemises", pipe, new Vector3(X1 - 0.7f, 3.0f, -6.5f), new Vector3(X1 - 0.7f, 3.0f, 6.5f), 0.02f, solid: false);
            for (int i = 0; i < 23; i++)
            {
                float z = -6.0f + i * (12.0f / 22f);
                Material m = i == 22 ? ink : MakeMat("Laverie_Chemise_" + (i % pastel.Length), pastel[i % pastel.Length]);
                Box("Laverie_Chemise_" + (i + 1), m, X1 - 0.72f, X1 - 0.68f, 2.55f, 2.95f, z - 0.17f, z + 0.17f, solid: false);
                if (i < 22) Sign((i + 1).ToString(), new Vector3(X1 - 0.75f, 2.75f, z), new Vector3(-1f, 0f, 0f), 0.006f, new Color(0.25f, 0.2f, 0.28f));
            }

            // 7) ce que les murs racontent (en anglais, comme le reste du jeu)
            Sign("STAFF ONLY.\nKEEP MY FLOOR DRY.\n- Odette", new Vector3(X0 + 0.05f, 3.0f, -2.6f), new Vector3(1f, 0f, 0f), 0.014f, new Color(0.25f, 0.28f, 0.3f));
            Sign("STAY ON THE MACHINES\nDON'T TOUCH THE WATER\nSHE FEELS IT", new Vector3(X0 + 0.05f, 1.6f, 2.4f), new Vector3(1f, 0f, 0f), 0.011f, new Color(0.95f, 0.95f, 0.9f));
            Sign("THE OTHER ONE CAN'T COME IN.\nTHE WATER WASHES HER INK OFF.", new Vector3(X1 - 0.05f, 3.8f, 0f), new Vector3(-1f, 0f, 0f), 0.014f, new Color(0.95f, 0.95f, 0.9f));
            Sign("I hid the 23rd one\nwhere she never listens.", new Vector3(X1 - 0.05f, 1.2f, -2.4f), new Vector3(-1f, 0f, 0f), 0.009f, new Color(0.2f, 0.25f, 0.45f));

            // 8) lumières : néons verdâtres qui grésillent, et une lampe rouge au-dessus de la machine 23
            foreach (Vector3 lp in new[] { new Vector3(37.5f, 4.6f, 0f), new Vector3(41f, 4.6f, -4f), new Vector3(41f, 4.6f, 4f), new Vector3(44.5f, 4.6f, 0f) })
            {
                var lg = new GameObject("Néon de la Laverie");
                lg.transform.SetParent(root.transform, false);
                lg.transform.position = P(lp);
                Light li = lg.AddComponent<Light>();
                li.type = LightType.Point; li.color = new Color(0.78f, 0.92f, 0.86f); li.intensity = 0.9f; li.range = 6.5f * s_w;
                lg.AddComponent<LightFlicker>();
            }
            var red = new GameObject("Lampe de secours (machine 23)");
            red.transform.SetParent(root.transform, false);
            red.transform.position = P(new Vector3(X1 - 1.0f, 2.6f, 0f));
            Light rl = red.AddComponent<Light>();
            rl.type = LightType.Point; rl.color = new Color(1f, 0.3f, 0.25f); rl.intensity = 0.7f; rl.range = 3.5f * s_w;

            // 9) l'ambiance sonore : l'eau qui goutte
            var amb = new GameObject("Clapotis");
            amb.transform.SetParent(root.transform, false);
            amb.transform.position = P(new Vector3(40.5f, 0.5f, 0f));
            AudioSource src = amb.AddComponent<AudioSource>();
            src.clip = Load<AudioClip>(Audio + "Ambience/drip_02.wav"); src.loop = true; src.spatialBlend = 1f; src.maxDistance = 18f; src.volume = 0.5f; src.playOnAwake = true;

            // 10) territoire du Drap (la salle + le passage) : L'Écoute ignore tout ce qui s'y passe
            Vector3 ta = P(new Vector3(31.15f, -0.5f, Z0)), tb = P(new Vector3(X1, H, Z1));
            var terr = new GameObject("Territoire du Drap");
            terr.transform.SetParent(root.transform, false);
            terr.transform.position = (ta + tb) * 0.5f;
            Territory t = terr.AddComponent<Territory>();
            t.owner = "Drap";
            t.size = new Vector3(Mathf.Abs(tb.x - ta.x), Mathf.Abs(tb.y - ta.y), Mathf.Abs(tb.z - ta.z));

            // 11) Le Drap
            var proto = Load<GameObject>("Assets/Horror/Art/Monsters/LeDrap.glb");
            if (proto != null)
            {
                var drap = (GameObject)PrefabUtility.InstantiatePrefab(proto);
                drap.name = "Le Drap (Odette)";
                drap.transform.SetParent(root.transform, false);
                drap.transform.position = P(new Vector3(40.5f, 0f, 0f));
                drap.transform.localScale = Vector3.one * 1.15f;
                SheetMonsterAI ai = drap.AddComponent<SheetMonsterAI>();
                Vector3 za = P(new Vector3(X0 + 0.6f, 0f, Z0 + 0.5f)), zb = P(new Vector3(X1 - 0.6f, 4f, Z1 - 0.5f));
                ai.zone = new Bounds((za + zb) * 0.5f, new Vector3(Mathf.Abs(zb.x - za.x), 8f, Mathf.Abs(zb.z - za.z)));
                ai.floorY = P(Vector3.zero).y;
                ai.face = FindChild(drap.transform, "Tete");
                ai.sloshLoop = Load<AudioClip>(Audio + "World/drap_eau_loop.wav");
                ai.riseSound = Load<AudioClip>(Audio + "World/drap_surgit.wav");
                ai.jumpscareSound = Load<AudioClip>(Audio + "Jumpscare/drap_cri.wav");
            }
        }

        /// <summary>Copie teinte d'une matière glTF (enregistrée à côté des autres matières de l'Institut).</summary>
        private static Material Tinted(Material source, string name, Color c)
        {
            EnsureFolder("Assets/Horror/Art/Materials");
            string path = "Assets/Horror/Art/Materials/" + name + ".mat";
            var mat = Load<Material>(path);
            if (mat != null) return mat;
            mat = new Material(source) { name = name };
            if (mat.HasProperty("baseColorFactor")) mat.SetColor("baseColorFactor", c);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        /// <summary>Retire d'un maillage de la maquette les triangles dont le centre est dans la boîte (et de sa collision).</summary>
        private static void CutOutBox(MeshFilter mf, Bounds box)
        {
            Mesh src = mf.sharedMesh;
            if (src == null) return;
            Vector3[] v = src.vertices;
            Mesh dst = Object.Instantiate(src);
            dst.name = src.name.EndsWith("_porte23") ? src.name : src.name + "_porte23";   // une seule copie, même après plusieurs découpes
            bool changed = false;
            for (int s = 0; s < src.subMeshCount; s++)
            {
                int[] tris = src.GetTriangles(s);
                var keep = new List<int>(tris.Length);
                for (int i = 0; i < tris.Length; i += 3)
                {
                    Vector3 c = mf.transform.TransformPoint((v[tris[i]] + v[tris[i + 1]] + v[tris[i + 2]]) / 3f);
                    if (box.Contains(c)) { changed = true; continue; }
                    keep.Add(tris[i]); keep.Add(tris[i + 1]); keep.Add(tris[i + 2]);
                }
                dst.SetTriangles(keep, s);
            }
            if (!changed) { Object.DestroyImmediate(dst); return; }
            int left = 0;
            for (int s = 0; s < dst.subMeshCount; s++) left += (int)dst.GetIndexCount(s);
            if (left == 0)
            {
                // tout le maillage était dans la zone : on cache l'objet (un collider vide ferait une erreur PhysX)
                Object.DestroyImmediate(dst);
                MeshCollider empty = mf.GetComponent<MeshCollider>();
                if (empty != null) Object.DestroyImmediate(empty);
                mf.gameObject.SetActive(false);
                return;
            }
            EnsureFolder("Assets/Horror/Art/Map/Genere");
            string path = "Assets/Horror/Art/Map/Genere/" + dst.name + ".asset";
            if (Load<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(dst, path);
            mf.sharedMesh = dst;
            MeshCollider col = mf.GetComponent<MeshCollider>();
            if (col != null) col.sharedMesh = dst;
        }

        // ------------------------------------------------------------------ l'étalonnage (la DA à l'écran)

        /// <summary>
        /// Étalonnage global (URP Volume) : pastel délavé qui tourne au malaise. Ombres lilas, hautes lumières
        /// pêche, couleurs un peu éteintes, vignette prune, grain de papier, halo rosé autour des lampes.
        /// Profil enregistré dans Art/Map/Genere/Institut_Etalonnage.asset (réglable à la main ensuite).
        /// </summary>
        private static void SetupLook()
        {
            EnsureFolder("Assets/Horror/Art/Map/Genere");
            string path = "Assets/Horror/Art/Map/Genere/Institut_Etalonnage.asset";
            if (Load<UnityEngine.Rendering.VolumeProfile>(path) != null) AssetDatabase.DeleteAsset(path);   // produit par ce menu
            var profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            T Add<T>() where T : UnityEngine.Rendering.VolumeComponent
            {
                T c = profile.Add<T>(true);
                c.name = typeof(T).Name;
                AssetDatabase.AddObjectToAsset(c, profile);
                return c;
            }
            var tone = Add<UnityEngine.Rendering.Universal.Tonemapping>();
            tone.mode.Override(UnityEngine.Rendering.Universal.TonemappingMode.Neutral);
            var color = Add<UnityEngine.Rendering.Universal.ColorAdjustments>();
            color.saturation.Override(-18f);
            color.contrast.Override(14f);
            color.postExposure.Override(0.45f);   // assez clair pour lire le décor dans le noir
            color.colorFilter.Override(new Color(0.98f, 0.94f, 1f));
            var split = Add<UnityEngine.Rendering.Universal.SplitToning>();
            split.shadows.Override(new Color(0.45f, 0.38f, 0.62f));      // ombres lilas
            split.highlights.Override(new Color(1f, 0.82f, 0.72f));      // lumières pêche
            split.balance.Override(-15f);
            var vig = Add<UnityEngine.Rendering.Universal.Vignette>();
            vig.color.Override(new Color(0.12f, 0.04f, 0.14f));
            vig.intensity.Override(0.3f);
            vig.smoothness.Override(0.55f);
            var grain = Add<UnityEngine.Rendering.Universal.FilmGrain>();
            grain.type.Override(UnityEngine.Rendering.Universal.FilmGrainLookup.Thin2);
            grain.intensity.Override(0.28f);
            grain.response.Override(0.75f);
            var bloom = Add<UnityEngine.Rendering.Universal.Bloom>();
            bloom.threshold.Override(0.85f);
            bloom.intensity.Override(0.55f);
            bloom.scatter.Override(0.6f);
            bloom.tint.Override(new Color(1f, 0.8f, 0.86f));
            var ca = Add<UnityEngine.Rendering.Universal.ChromaticAberration>();
            ca.intensity.Override(0.06f);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var go = new GameObject("Étalonnage (DA pastel inquiétante)");
            var vol = go.AddComponent<UnityEngine.Rendering.Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = profile;
            foreach (Camera cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
            {
                var data = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>()
                           ?? cam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                data.renderPostProcessing = true;
            }
        }

        // ------------------------------------------------------------------ lobby (modes) et but du mode Exploration

        /// <summary>
        /// Dans la Salle de jeux, mur sud : à droite de la porte, le mur des modes (EXPLORATION, INK TAG, HIDE & SEEK) ;
        /// à gauche, le tableau des objectifs (papiers, lieux). Les 14 lieux à découvrir viennent des pièces de la
        /// maquette (+ la Laverie). À la fin, la machine 23 de la Laverie s'éclaire.
        /// </summary>
        private static void SetupLobbyAndGoals(MapData data, RoomData playroom)
        {
            if (playroom == null) return;
            var root = new GameObject("Lobby et objectifs (Salle de jeux)");
            Vector3 roomCenter = P(playroom.center);
            AudioClip click = Load<AudioClip>(Audio + "UI/button_click.wav");
            AudioClip coin = Load<AudioClip>(Audio + "UI/coin.wav");
            AudioClip denied = Load<AudioClip>(Audio + "UI/denied.wav");
            Material panel = MakeMat("Lobby_Panneau", new Color(0.30f, 0.24f, 0.34f));
            Material btnMat = MakeMat("Lobby_Bouton", new Color(0.95f, 0.62f, 0.72f));
            Material soonMat = MakeMat("Lobby_Bouton_Bientot", new Color(0.55f, 0.55f, 0.6f));
            float wallZ = playroom.min.z + 0.18f;          // surface du mur à +0,155 (épaisseur du papier peint) : les panneaux posés devant

            // à plat contre le mur (avant : tourné vers le centre de la pièce, les grands panneaux s'enfonçaient en biais)
            Vector3 WallNormal(Vector3 maq) { Vector3 d = roomCenter - P(maq); d.x = 0f; d.y = 0f; return d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.forward; }
            GameObject Label(string text, Vector3 maq, float size, Color c)
            {
                GameObject l = SafeZoneShopBuilder.Label(root.transform, text, Vector3.zero, size, c);
                l.transform.position = P(maq);
                l.transform.rotation = Quaternion.LookRotation(-WallNormal(maq));
                return l;
            }
            GameObject Plate(string name, Vector3 maq, Vector3 size, Material m)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = name;
                go.transform.SetParent(root.transform, false);
                go.transform.position = P(maq);
                go.transform.rotation = Quaternion.LookRotation(WallNormal(maq));
                go.transform.localScale = size * s_w;
                go.GetComponent<Renderer>().sharedMaterial = m;
                return go;
            }

            // --- le mur des modes (à droite de la porte)
            var selector = root.AddComponent<ModeSelector>();
            Plate("Panneau des modes", new Vector3(18.4f, 1.55f, wallZ), new Vector3(3.6f, 2.1f, 0.04f), panel);
            Label("CHOOSE YOUR GAME", new Vector3(18.4f, 2.45f, wallZ + 0.04f), 0.03f, new Color(0.98f, 0.9f, 0.8f));
            GameObject screen = Label("", new Vector3(18.4f, 1.95f, wallZ + 0.04f), 0.014f, new Color(0.95f, 0.95f, 0.9f));
            selector.screen = screen.GetComponent<TextMesh>();
            selector.selectSound = coin; selector.deniedSound = denied;
            var defs = new (string id, string title, string desc, bool ok, float x)[]
            {
                ("exploration", "EXPLORATION", "Run from them. Find the papers.\nFind every place. Find the 23rd.", true, 17.2f),
                ("inktag", "INK TAG", "Caught? You turn to ink.\nYour noise calls them.", false, 18.4f),
                ("hideseek", "HIDE & SEEK", "One hides among the dolls.\nThe others listen.", false, 19.6f),
            };
            var modes = new List<ModeSelector.Mode>();
            foreach (var d in defs)
            {
                GameObject b = Plate("Bouton " + d.title, new Vector3(d.x, 1.0f, wallZ + 0.03f), new Vector3(0.22f, 0.22f, 0.08f), d.ok ? btnMat : soonMat);
                b.GetComponent<Collider>().isTrigger = true;
                PressButton press = b.AddComponent<PressButton>();
                press.clickSound = click;
                Label(d.title + (d.ok ? "" : "\n(soon)"), new Vector3(d.x, 1.3f, wallZ + 0.04f), 0.011f, new Color(0.95f, 0.92f, 0.86f));
                modes.Add(new ModeSelector.Mode { id = d.id, title = d.title, description = d.desc, available = d.ok, button = press });
            }
            selector.modes = modes.ToArray();

            // --- le tableau des objectifs (à gauche de la porte)
            Plate("Tableau des objectifs", new Vector3(10.2f, 1.9f, wallZ), new Vector3(2.6f, 1.9f, 0.04f), panel);
            GameObject board = Label("", new Vector3(10.2f, 2.0f, wallZ + 0.04f), 0.016f, new Color(0.98f, 0.93f, 0.82f));

            // --- le panneau de confort (sous les objectifs) : flashs, screamers doux
            Plate("Panneau de confort", new Vector3(10.2f, 0.62f, wallZ), new Vector3(2.0f, 0.55f, 0.04f), panel);
            GameObject comfortText = Label("", new Vector3(10.78f, 0.66f, wallZ + 0.04f), 0.009f, new Color(0.95f, 0.92f, 0.86f));
            var comfort = root.AddComponent<ComfortSettings>();
            comfort.display = comfortText.GetComponent<TextMesh>();
            comfort.toggleSound = click;
            PressButton ComfortButton(string name, float x)
            {
                GameObject b = Plate(name, new Vector3(x, 0.6f, wallZ + 0.03f), new Vector3(0.18f, 0.18f, 0.08f), btnMat);
                b.GetComponent<Collider>().isTrigger = true;
                PressButton pb = b.AddComponent<PressButton>();
                pb.clickSound = click;
                return pb;
            }
            comfort.flashesButton = ComfortButton("Bouton FLASHES", 9.9f);
            comfort.scaresButton = ComfortButton("Bouton SCARES", 9.6f);
            Label("FLASH", new Vector3(9.9f, 0.78f, wallZ + 0.04f), 0.009f, new Color(0.95f, 0.92f, 0.86f));
            Label("SCARE", new Vector3(9.6f, 0.78f, wallZ + 0.04f), 0.009f, new Color(0.95f, 0.92f, 0.86f));
            comfort.movesButton = ComfortButton("Bouton MOVES", 10.2f);   // 9.3 est pris par TUTO
            Label("MOVES", new Vector3(10.2f, 0.78f, wallZ + 0.04f), 0.009f, new Color(0.95f, 0.92f, 0.86f));

            // --- le tutoriel du premier lancement (panneau flottant) ; le bouton TUTO le relance ou le passe
            var coachGo = new GameObject("Tutoriel (premier lancement)");
            coachGo.transform.SetParent(root.transform, false);
            var coach = coachGo.AddComponent<TutorialCoach>();
            var tutoPanel = new GameObject("Panneau du tutoriel").transform;
            tutoPanel.SetParent(coachGo.transform, false);
            tutoPanel.position = roomCenter + Vector3.up * 1.5f;
            var back = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(back.GetComponent<Collider>());
            back.name = "Fond";
            back.transform.SetParent(tutoPanel, false);
            back.transform.localPosition = new Vector3(0f, 0f, 0.01f);
            back.transform.localScale = new Vector3(0.95f, 0.62f, 1f);
            back.GetComponent<Renderer>().sharedMaterial = MakeMat("Tutoriel_Fond", new Color(0.08f, 0.06f, 0.1f));
            GameObject tutoText = SafeZoneShopBuilder.Label(tutoPanel, "", Vector3.zero, 0.0055f, new Color(0.98f, 0.93f, 0.82f));
            tutoText.transform.localRotation = Quaternion.identity;
            coach.panel = tutoPanel;
            coach.text = tutoText.GetComponent<TextMesh>();
            coach.stepSound = coin;
            coach.replayButton = ComfortButton("Bouton TUTO", 9.3f);
            Label("TUTO", new Vector3(9.3f, 0.78f, wallZ + 0.04f), 0.009f, new Color(0.95f, 0.92f, 0.86f));
            // --- le tableau PLAYERS (sécurité du chat vocal) : couper, bloquer, signaler chaque joueur ; couper tout le monde
            Plate("Tableau des joueurs", new Vector3(13.6f, 1.6f, wallZ), new Vector3(3.4f, 1.95f, 0.04f), panel);
            var pboard = root.AddComponent<PlayersBoard>();
            pboard.header = Label("PLAYERS", new Vector3(13.6f, 2.38f, wallZ + 0.04f), 0.014f, new Color(0.98f, 0.9f, 0.8f)).GetComponent<TextMesh>();
            pboard.confirmSound = coin;
            Color small = new Color(0.95f, 0.92f, 0.86f);
            Label("MUTE", new Vector3(14.25f, 2.12f, wallZ + 0.04f), 0.009f, small);
            Label("BLOCK", new Vector3(14.65f, 2.12f, wallZ + 0.04f), 0.009f, small);
            Label("REPORT", new Vector3(15.05f, 2.12f, wallZ + 0.04f), 0.009f, small);
            var prow = new List<PlayersBoard.Row>();
            for (int i = 0; i < 6; i++)
            {
                float y = 1.93f - i * 0.22f;
                var rowRoot = new GameObject("Joueur " + (i + 1));
                rowRoot.transform.SetParent(root.transform, false);
                var row = new PlayersBoard.Row { root = rowRoot };
                GameObject lbl = Label("PLAYER", new Vector3(12.85f, y, wallZ + 0.04f), 0.012f, small);
                lbl.transform.SetParent(rowRoot.transform, true);
                row.label = lbl.GetComponent<TextMesh>();
                PressButton RowButton(string n, float x, Material m)
                {
                    GameObject b = Plate(n + " " + (i + 1), new Vector3(x, y, wallZ + 0.03f), new Vector3(0.15f, 0.15f, 0.07f), m);
                    b.GetComponent<Collider>().isTrigger = true;
                    b.transform.SetParent(rowRoot.transform, true);
                    PressButton pb = b.AddComponent<PressButton>();
                    pb.clickSound = click;
                    return pb;
                }
                row.mute = RowButton("Bouton MUTE", 14.25f, btnMat);
                row.block = RowButton("Bouton BLOCK", 14.65f, soonMat);
                row.report = RowButton("Bouton REPORT", 15.05f, MakeMat("Lobby_Bouton_Signaler", new Color(0.85f, 0.25f, 0.25f)));
                prow.Add(row);
            }
            pboard.rows = prow.ToArray();
            GameObject muteAll = Plate("Bouton MUTE ALL", new Vector3(13.6f, 0.82f, wallZ + 0.03f), new Vector3(0.2f, 0.2f, 0.08f), btnMat);
            muteAll.GetComponent<Collider>().isTrigger = true;
            pboard.muteAllButton = muteAll.AddComponent<PressButton>();
            pboard.muteAllButton.clickSound = click;
            pboard.muteAllLabel = Label("MUTE ALL: OFF", new Vector3(13.6f, 1.02f, wallZ + 0.04f), 0.011f, small).GetComponent<TextMesh>();

            var goals = root.AddComponent<ExplorationGoals>();
            goals.board = board.GetComponent<TextMesh>();
            goals.discoverSound = Load<AudioClip>(Audio + "Ambience/music_box_far.wav");

            // --- les lieux (pièces de la maquette regroupées par zone) + la Laverie
            var titles = new Dictionary<string, string>
            {
                { "Hall", "THE HALL" }, { "Couloir", "THE CORRIDOR" }, { "Ecole", "THE DORMITORY" }, { "Standard", "THE SWITCHBOARD" },
                { "Escalier", "THE STAIRS" }, { "Machines", "THE BOILER ROOM" }, { "Galerie", "THE GALLERY OF DOORS" },
                { "Ventilateur", "THE BIG FAN" }, { "Citerne", "THE INK TANK" }, { "Douches", "THE SHOWERS" },
                { "Piscine", "THE INK POOL" }, { "Foyer", "THE FOYER" }, { "Theatre", "THE THEATRE" }
            };
            var boxes = new Dictionary<string, List<Bounds>>();
            foreach (RoomData r in data.rooms)
            {
                if (r.zone == "SalleDeJeux" || !titles.ContainsKey(r.zone)) continue;
                Vector3 a = P(r.min), b = P(r.max);
                var bb = new Bounds((a + b) * 0.5f, new Vector3(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y) + 1f, Mathf.Abs(b.z - a.z)));
                if (!boxes.ContainsKey(r.zone)) boxes[r.zone] = new List<Bounds>();
                boxes[r.zone].Add(bb);
            }
            var places = new List<ExplorationGoals.Place>();
            foreach (var kv in boxes) places.Add(new ExplorationGoals.Place { id = kv.Key, title = titles[kv.Key], boxes = kv.Value.ToArray() });
            Vector3 la = P(new Vector3(34.3f, 0f, -7f)), lb = P(new Vector3(47f, 5.5f, 7f));
            places.Add(new ExplorationGoals.Place { id = "Laverie", title = "THE LAUNDRY",
                boxes = new[] { new Bounds((la + lb) * 0.5f, new Vector3(Mathf.Abs(lb.x - la.x), Mathf.Abs(lb.y - la.y), Mathf.Abs(lb.z - la.z))) } });
            places.Add(new ExplorationGoals.Place { id = "Infirmerie", title = "THE INFIRMARY", boxes = new[] { HopitalBounds() } });
            goals.places = places.ToArray();
            goals.totalPapers = data.papers != null && data.papers.Length > 0 ? data.papers.Length : 17;

            // --- la fin : la machine 23 s'éclaire, l'enfant n°23 (un masque blanc, sans visage) attend dedans
            var end = new GameObject("Fin : le 23e enfant (machine 23)");
            end.transform.position = P(new Vector3(47f - 0.85f, 1.35f, 0f));
            Light el = new GameObject("Lumière").AddComponent<Light>();
            el.transform.SetParent(end.transform, false);
            el.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            el.type = LightType.Point; el.color = new Color(0.85f, 0.9f, 1f); el.intensity = 1.6f; el.range = 4f * s_w;
            var mask = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mask.name = "Le masque du 23e (blanc, sans visage)";
            Object.DestroyImmediate(mask.GetComponent<Collider>());
            mask.transform.SetParent(end.transform, false);
            mask.transform.localScale = new Vector3(0.22f, 0.28f, 0.05f) * s_w;
            mask.GetComponent<Renderer>().sharedMaterial = MakeMat("Masque_Blanc", new Color(0.96f, 0.94f, 0.88f));
            GameObject msg = SafeZoneShopBuilder.Label(end.transform, "YOU FOUND ME.\nNOW SHE CAN'T HEAR ME.\n- 23", Vector3.zero, 0.02f, new Color(0.85f, 0.9f, 1f));
            msg.transform.position = P(new Vector3(47f - 0.06f, 2.4f, 0f));
            msg.transform.rotation = Quaternion.LookRotation(-Flat(Dir(new Vector3(-1f, 0f, 0f))));
            SetupEnvers(end, P(data.spawn));
            end.SetActive(false);
            goals.ending = end;
        }

        // ------------------------------------------------------------------ décor de toutes les pièces (DA)

        /// <summary>
        /// Habille chaque pièce de la maquette avec le décor Blender (Art/Map/Decor) : dessins d'enfants encadrés
        /// de travers sur les vrais murs (trouvés par rayon, jamais dans une porte), une guirlande de fanions en
        /// travers de la pièce, une peluche ou des cubes oubliés au sol (pas dans l'encre). Placement déterministe.
        /// </summary>
        private static void SetupDecor(MapData data)
        {
            const string D = "Assets/Horror/Art/Map/Decor/";
            GameObject[] frames = { Load<GameObject>(D + "Cadre_family.glb"), Load<GameObject>(D + "Cadre_friends.glb"),
                                    Load<GameObject>(D + "Cadre_lady.glb"), Load<GameObject>(D + "Cadre_pool.glb") };
            GameObject garland = Load<GameObject>(D + "Guirlande.glb");
            GameObject lamp = Load<GameObject>(D + "Applique.glb");
            GameObject[] toys = { Load<GameObject>(D + "Lapin.glb"), Load<GameObject>(D + "Cubes.glb") };
            if (frames[0] == null) return;
            Physics.SyncTransforms();
            var root = new GameObject("Décor (cadres, guirlandes, jouets)");
            var rnd = new System.Random(7);
            int nFrames = 0, nGarlands = 0, nToys = 0;

            GameObject Put(GameObject proto, Vector3 pos, Quaternion rot, Vector3 scale)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(proto);
                go.transform.SetParent(root.transform, false);
                go.transform.SetPositionAndRotation(pos, rot);
                go.transform.localScale = scale;
                go.isStatic = true;
                return go;
            }
            bool Wall(Vector3 from, Vector3 dir, float max, out RaycastHit hit)
            {
                if (!Physics.Raycast(from, dir, out hit, max, ~0, QueryTriggerInteraction.Ignore)) return false;
                if (Mathf.Abs(hit.normal.y) > 0.3f) return false;
                string n = hit.collider.name.ToLowerInvariant();
                return !(n.Contains("porte") || n.Contains("door") || n.Contains("bouton") || n.Contains("rideau"));
            }

            foreach (RoomData r in data.rooms)
            {
                Vector3 a = P(r.min), b = P(r.max);
                Vector3 lo = Vector3.Min(a, b), hi = Vector3.Max(a, b);
                Vector3 size = hi - lo;
                if (size.x < 2.5f || size.z < 2.5f) continue;                  // couloirs très étroits : rien
                float eye = lo.y + 1.55f * s_w;
                Vector3 c = new Vector3((lo.x + hi.x) * 0.5f, eye, (lo.z + hi.z) * 0.5f);

                // 1) cadres : deux par mur (décalés), sur le premier mur touché
                foreach (Vector3 dir in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
                {
                    Vector3 tangent = Vector3.Cross(Vector3.up, dir);
                    float half = Mathf.Abs(Vector3.Dot(size, tangent)) * 0.5f;
                    foreach (float off in new[] { -0.3f, 0.3f })
                    {
                        Vector3 from = c + tangent * off * half;
                        if (!Wall(from, dir, Mathf.Abs(Vector3.Dot(size, dir)) * 0.5f + 1.5f, out RaycastHit hit)) continue;
                        float sway = (float)(rnd.NextDouble() - 0.5) * 10f;
                        Put(frames[nFrames % frames.Length], hit.point + hit.normal * 0.025f + Vector3.up * (float)(rnd.NextDouble() * 0.25f),
                            Quaternion.LookRotation(hit.normal, Vector3.up) * Quaternion.Euler(0f, 0f, sway), Vector3.one * 1.3f);
                        nFrames++;
                    }
                }

                // 1 bis) une applique murale (lumière chaude qui grésille) sur le premier mur touché
                if (lamp != null && Wall(new Vector3(c.x, lo.y + 2.1f * s_w, c.z), Vector3.right, size.x * 0.5f + 1.5f, out RaycastHit lh))
                {
                    GameObject l = Put(lamp, lh.point + lh.normal * 0.01f, Quaternion.LookRotation(lh.normal, Vector3.up), Vector3.one * 1.4f);
                    var lg = new GameObject("Lumière de l'applique");
                    lg.transform.SetParent(l.transform, false);
                    lg.transform.position = lh.point + lh.normal * 0.3f;
                    Light li = lg.AddComponent<Light>();
                    li.type = LightType.Point; li.color = new Color(1f, 0.8f, 0.62f); li.intensity = 0.7f; li.range = 5f * s_w;
                    lg.AddComponent<LightFlicker>();
                }

                // 2) une guirlande dans la longueur de la pièce, sous le plafond
                if (garland != null)
                {
                    Vector3 axis = size.x >= size.z ? Vector3.right : Vector3.forward;
                    Vector3 top = new Vector3(c.x, Mathf.Min(hi.y - 0.6f * s_w, lo.y + 2.9f * s_w), c.z);
                    if (Wall(top, axis, 40f, out RaycastHit h1) && Wall(top, -axis, 40f, out RaycastHit h2))
                    {
                        float len = Vector3.Distance(h1.point, h2.point);
                        if (len > 2f && len < 30f)
                        {
                            Vector3 mid = (h1.point + h2.point) * 0.5f;
                            Put(garland, mid, Quaternion.FromToRotation(Vector3.right, (h1.point - h2.point).normalized),
                                new Vector3(len * 0.98f, 1.4f, 1.4f));
                            nGarlands++;
                        }
                    }
                }

                // 3) un jouet oublié au sol (pas dans l'encre de la piscine ni de la citerne)
                if (r.zone == "Piscine" || r.zone == "Citerne" || toys[0] == null) continue;
                for (int k = 0; k < 2; k++)
                {
                    Vector3 p = new Vector3(Mathf.Lerp(lo.x, hi.x, 0.2f + 0.6f * (float)rnd.NextDouble()), hi.y - 0.2f,
                                            Mathf.Lerp(lo.z, hi.z, 0.2f + 0.6f * (float)rnd.NextDouble()));
                    if (!Physics.Raycast(p, Vector3.down, out RaycastHit fh, size.y + 1f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (fh.normal.y < 0.85f) continue;
                    Put(toys[(nToys + k) % toys.Length], fh.point, Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f), Vector3.one * 1.3f);
                    nToys++;
                }
            }
            Debug.Log("[Horror] Décor : " + nFrames + " cadres, " + nGarlands + " guirlandes, " + nToys + " jouets.");
        }

        // ------------------------------------------------------------------ performances (Quest)

        /// <summary>Le décor qui ne bouge jamais est regroupé par Unity (static batching) : beaucoup moins d'appels de
        /// dessin, ce qui compte énormément sur le Quest. Règle sûre : seulement ce qui n'a ni script ni physique sur
        /// lui ou au-dessus de lui (monstres, portes, objets à prendre, miroir et sa poupée restent mobiles).</summary>
        private static void MarkStaticDecor()
        {
            string[] roots = { "Décor (cadres, guirlandes, jouets)", "L'Envers (zone secrète)",
                               "La loge (Doux sans tête, peaux de Doux)", "Laverie (porte 23)", "Infirmerie (mobilier)", "Grenier (décor)" };
            int n = 0;
            foreach (string name in roots)
            {
                GameObject root = GameObject.Find(name);
                if (root == null) continue;
                foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                {
                    bool moving = false;
                    for (Transform t = r.transform; t != null; t = t.parent)
                        if (t.GetComponent<MonoBehaviour>() != null || t.GetComponent<Rigidbody>() != null || t.GetComponent<Animator>() != null
                            || t.GetComponent<ParticleSystem>() != null) { moving = true; break; }
                    if (moving) continue;
                    var flags = GameObjectUtility.GetStaticEditorFlags(r.gameObject) | StaticEditorFlags.BatchingStatic;
                    GameObjectUtility.SetStaticEditorFlags(r.gameObject, flags);
                    n++;
                }
            }
            Debug.Log($"[Horror] Décor regroupé (static batching) : {n} objets.");
        }

        // ------------------------------------------------------------------ la mort (le noir chuchoté, la rature d'encre)

        /// <summary>Le noir après une mort (chuchotement d'enfant sous-titré) et la rature d'encre que voient les autres.
        /// Les phrases doivent suivre l'ordre de Tools/Audio/make_whispers.py (qui fabrique les murmures).</summary>
        private static void SetupDeath()
        {
            const string D = Audio + "Death/";
            string[] lines =
            {
                "You breathed too loud.", "She heard your heart.", "Mila hid under the bed.\nHide like Mila.",
                "Don't run.\nShe hears running.", "We were twenty-three.", "The Softies move\nwhen you look away.",
                "Odette is still in the water.", "Count the doors.\nOne is missing.", "Now you know\nwhy we whisper.",
                "Find my drawing.\nPlease.",
            };
            var clips = new List<AudioClip>();
            var subs = new List<string>();
            for (int i = 0; i < lines.Length; i++)
            {
                var c = Load<AudioClip>(D + $"murmure_{i + 1:00}.wav");
                if (c == null) continue;
                clips.Add(c); subs.Add(lines[i]);
            }
            foreach (Jumpscare js in Object.FindObjectsByType<Jumpscare>(FindObjectsInactive.Include))
            {
                js.deathWhispers = clips.ToArray();
                js.whisperLines = subs.ToArray();
            }
            // la voix : chuchoter ne s'entend pas, parler porte à quelques mètres, CRIER porte à 30 m (et fait accourir)
            foreach (Horror.Noise.VoiceNoise v in Object.FindObjectsByType<Horror.Noise.VoiceNoise>(FindObjectsInactive.Include))
            {
                v.whisperThreshold = 0.18f; v.talkRadius = 7f; v.screamRadius = 30f;
            }
            var go = new GameObject("Ratures (morts vues par les autres)");
            DeathMarks dm = go.AddComponent<DeathMarks>();
            dm.ink = MakeMat("Encre_Mort", new Color(0.05f, 0.03f, 0.07f));
            dm.ink.color = new Color(0.025f, 0.015f, 0.035f);
            dm.ink.SetFloat("_Smoothness", 0.3f);    // un peu luisante, pas un miroir (trop brillante, elle paraissait blanche)
            dm.ink.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(dm.ink);
            dm.sound = Load<AudioClip>(D + "encre_mort.wav");
        }

        // ------------------------------------------------------------------ ambiances complétées (sans musique)

        /// <summary>Complète les ambiances par pièce là où il n'y avait rien : dortoir (respirations d'enfants),
        /// Standard (lignes qui grésillent), couloir (horloge), hall (courant d'air), Laverie (eau). Pas de musique
        /// dans les zones dangereuses : on doit entendre les monstres.</summary>
        private static void EnrichAmbience()
        {
            const string Z = Audio + "Zones/";
            foreach (AmbientZone z in Object.FindObjectsByType<AmbientZone>(FindObjectsInactive.Include))
            {
                if (z.isSafe || z.loop != null) continue;
                string n = z.gameObject.name;
                if (n.Contains("Ecole")) { z.loop = Load<AudioClip>(Z + "dortoir_respirations.wav"); z.loopVolume = 0.35f; }
                else if (n.Contains("Standard")) { z.loop = Load<AudioClip>(Z + "standard_gresillement.wav"); z.loopVolume = 0.25f; }
                else if (n.Contains("Couloir")) { z.loop = Load<AudioClip>(Z + "couloir_horloge.wav"); z.loopVolume = 0.3f; }
                else if (n.Contains("Hall") || n.Contains("Foyer")) { z.loop = Load<AudioClip>(Z + "hall_vent.wav"); z.loopVolume = 0.35f; }
            }
            // la Laverie n'est pas une pièce de la maquette : sa zone d'ambiance
            Vector3 la = P(new Vector3(31.2f, 0f, -7f)), lb = P(new Vector3(47f, 5.5f, 7f));
            var go = new GameObject("Ambiance Laverie");
            go.transform.position = (la + lb) * 0.5f;
            AmbientZone lz = go.AddComponent<AmbientZone>();
            lz.size = new Vector3(Mathf.Abs(lb.x - la.x), Mathf.Abs(lb.y - la.y) + 1f, Mathf.Abs(lb.z - la.z));
            lz.priority = 5;
            lz.loop = Load<AudioClip>(Audio + "World/drap_eau_loop.wav"); lz.loopVolume = 0.3f;
            lz.oneShots = LoadMany(Audio + "Ambience", "drip_");
        }

        // ------------------------------------------------------------------ la loge : le Doux sans tête et les peaux

        /// <summary>La loge (derrière la scène) en nouveaux modèles : un Doux sans tête face au miroir, sa tête
        /// posée sur la coiffeuse, et les costumes vides (des « peaux » de Doux) pendus au portant. Les anciens
        /// sont découpés des maillages fusionnés de la maquette.</summary>
        private static void SetupLoge(GameObject map)
        {
            var proto = Load<GameObject>(DouxFolder + "Doux_Ours.glb");
            if (proto == null) return;
            // 1) découper les anciens (Doux assis + sa tête + 8 costumes)
            var boxes = new[] {
                MaqBox(25.9f, 27.3f, 1.0f, 3.4f, -25.9f, -24.5f), MaqBox(27.0f, 28.1f, 1.8f, 3.1f, -27.0f, -25.9f),
                MaqBox(16.0f, 22.2f, 1.6f, 3.0f, -26.7f, -26.1f) };
            foreach (MeshFilter mf in map.GetComponentsInChildren<MeshFilter>(true))
                if (mf.gameObject.name.Contains("Theatre_doux_") || mf.gameObject.name.Contains("Theatre_tissu_creme"))
                    foreach (Bounds b in boxes) CutOutBox(mf, b);
            var root = new GameObject("La loge (Doux sans tête, peaux de Doux)");
            // ampoules du miroir de loge (chaudes, qui grésillent) et une lampe nue au-dessus du portant
            foreach (Vector3 lp in new[] { new Vector3(26.9f, 2.6f, -25.6f), new Vector3(19f, 3.3f, -25.4f) })
            {
                var lg = new GameObject("Lumière de la loge");
                lg.transform.SetParent(root.transform, false);
                lg.transform.position = P(lp);
                Light li = lg.AddComponent<Light>();
                li.type = LightType.Point; li.color = new Color(1f, 0.78f, 0.6f); li.intensity = 1.1f; li.range = 6f * s_w;
                lg.AddComponent<LightFlicker>();
            }
            Quaternion facingMirror = Quaternion.LookRotation(Flat(Dir(Vector3.back)), Vector3.up);
            // 2) le Doux sans tête, debout devant le miroir
            GameObject headless = PlaceDoux(root.transform, "Doux sans tête (loge)", "Ours", 2, P(new Vector3(26.6f, 1.1f, -25.2f)), facingMirror);
            Transform t = FindChild(headless.transform, "Tete");
            if (t != null) t.gameObject.SetActive(false);
            // 3) sa tête, posée sur la coiffeuse (une 2e copie dont on ne garde que la tête)
            GameObject headOnly = PlaceDoux(root.transform, "Tête de Doux (coiffeuse)", "Ours", 2, Vector3.zero, Quaternion.identity);
            Transform h = FindChild(headOnly.transform, "Tete");
            foreach (Transform c in headOnly.transform) if (c != h) c.gameObject.SetActive(false);
            headOnly.transform.SetPositionAndRotation(P(new Vector3(27.55f, 1.94f, -26.45f)) - Vector3.up * 1.40f * s_w,
                                                     facingMirror * Quaternion.Euler(0f, 200f, 0f));
            // 4) les peaux de Doux pendues au portant (sans tête, pieds dans le vide)
            string[] kinds = { "Lapin", "Ours", "Chat", "Chien" };
            for (int k = 0; k < 8; k++)
            {
                float x = 16.5f + k * 0.72f;
                float sc = 0.8f;
                Vector3 hook = P(new Vector3(x, 2.85f, -26.4f));
                GameObject skin = PlaceDoux(root.transform, "Peau de Doux " + (k + 1), kinds[k % 4], k % 6,
                                            hook - Vector3.up * 1.40f * s_w * sc, facingMirror * Quaternion.Euler(0f, (k % 2 == 0 ? 8f : -8f), 0f));
                skin.transform.localScale = Vector3.one * s_w * sc;
                Transform sh = FindChild(skin.transform, "Tete");
                if (sh != null) sh.gameObject.SetActive(false);
            }
            SetupHauntedMirror(root.transform, facingMirror);
        }

        /// <summary>Le miroir de la coiffeuse : il reflète la loge... avec une poupée de plus derrière toi.</summary>
        private static void SetupHauntedMirror(Transform parent, Quaternion facingMirror)
        {
            var mirror = new GameObject("Miroir de la loge (hanté)");
            mirror.transform.SetParent(parent, false);
            // face au joueur qui regarde le miroir (le miroir regarde dans l'autre sens que lui)
            mirror.transform.SetPositionAndRotation(P(new Vector3(27.05f, 2.35f, -26.52f)), facingMirror * Quaternion.Euler(0f, 180f, 0f));
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Glace";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(mirror.transform, false);
            quad.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);        // le quad Unity regarde vers -Z
            quad.transform.localScale = new Vector3(0.62f, 0.93f, 1f);
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            EnsureFolder("Assets/Horror/Art/Materials");
            string mp = "Assets/Horror/Art/Materials/Institut_Miroir.mat";
            // on réutilise le fichier s'il existe (le recréer à chaque menu 8 le faisait changer dans git à chaque fois)
            var mat = Load<Material>(mp);
            if (mat == null) { mat = new Material(unlit != null ? unlit : Shader.Find("Unlit/Texture")) { name = "Institut_Miroir" }; AssetDatabase.CreateAsset(mat, mp); }
            mat.color = new Color(0.78f, 0.74f, 0.82f);                            // vieux tain un peu terne
            EditorUtility.SetDirty(mat);
            quad.GetComponent<Renderer>().sharedMaterial = mat;
            var proto = Load<GameObject>("Assets/Horror/Art/Character/LeDessine_PNJ.glb");
            GameObject ghost = proto != null ? (GameObject)PrefabUtility.InstantiatePrefab(proto) : null;
            if (ghost != null)
            {
                ghost.name = "La poupée du reflet";
                ghost.transform.SetParent(mirror.transform, false);
                Transform head = FindChild(ghost.transform, "Tete");
                if (head != null) head.localRotation = Quaternion.Euler(0f, 0f, 18f);   // tête penchée
                foreach (Collider c in ghost.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            }
            HauntedMirror hm = mirror.AddComponent<HauntedMirror>();
            hm.surface = quad.GetComponent<Renderer>();
            hm.ghost = ghost != null ? ghost.transform : null;
            hm.sting = Load<AudioClip>(Audio + "Ambience/child_giggle_far.wav");
        }

        private static Bounds MaqBox(float x0, float x1, float y0, float y1, float z0, float z1)
        {
            Vector3 a = P(new Vector3(x0, y0, z0)), b = P(new Vector3(x1, y1, z1));
            return new Bounds((a + b) * 0.5f, new Vector3(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y), Mathf.Abs(b.z - a.z)));
        }

        // ------------------------------------------------------------------ le téléphone du Standard

        private static void SetupStandardPhone()
        {
            var proto = Load<GameObject>("Assets/Horror/Art/Map/Decor/Telephone.glb");
            if (proto == null) return;
            Physics.SyncTransforms();
            Vector3 spot = P(new Vector3(-18.6f, 0f, 3.4f));
            if (Physics.Raycast(spot + Vector3.up * 3f, Vector3.down, out RaycastHit fh, 6f, ~0, QueryTriggerInteraction.Ignore)) spot = fh.point;
            var root = new GameObject("Téléphone du Standard");
            var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "Guéridon_bois";
            table.transform.SetParent(root.transform, false);
            table.transform.localScale = new Vector3(0.5f, 0.75f, 0.5f) * s_w;
            table.transform.position = spot + Vector3.up * 0.375f * s_w;
            table.GetComponent<Renderer>().sharedMaterial = MakeMat("Gueridon", new Color(0.55f, 0.42f, 0.38f));
            Vector3 top = spot + Vector3.up * 0.75f * s_w;
            var phone = (GameObject)PrefabUtility.InstantiatePrefab(proto);
            phone.transform.SetParent(root.transform, false);
            phone.transform.position = top;
            phone.transform.localScale = Vector3.one * 2.6f;   // bien visible sur le guéridon
            var btn = GameObject.CreatePrimitive(PrimitiveType.Cube);
            btn.name = "Décrocher (invisible)";
            btn.transform.SetParent(root.transform, false);
            btn.transform.position = top + Vector3.up * 0.2f;
            btn.transform.localScale = new Vector3(0.35f, 0.12f, 0.2f);
            btn.GetComponent<Renderer>().enabled = false;
            PressButton press = btn.AddComponent<PressButton>();
            GameObject txt = SafeZoneShopBuilder.Label(root.transform, "", Vector3.zero, 0.012f, new Color(0.9f, 0.88f, 0.95f));
            txt.transform.position = top + Vector3.up * 0.75f;
            txt.transform.rotation = Quaternion.LookRotation(Flat(Dir(Vector3.right)));
            StandardPhone sp = root.AddComponent<StandardPhone>();
            sp.button = press;
            sp.text = txt.GetComponent<TextMesh>();
            sp.ring = Load<AudioClip>(Audio + "World/telephone_sonnerie.wav");
            sp.whisper = Load<AudioClip>(Audio + "World/telephone_murmure.wav");
        }

        // ------------------------------------------------------------------ le dessin qui change (fond du dortoir)

        private static void SetupChangingDrawing()
        {
            const string D = "Assets/Horror/Art/Map/Decor/";
            string[] keys = { "family", "pool", "friends", "lady" };
            var mats = new List<Material>();
            foreach (string k in keys)
            {
                var proto = Load<GameObject>(D + "Cadre_" + k + ".glb");
                Transform d = proto != null ? FindChild(proto.transform, "Dessin") : null;
                if (d != null) mats.Add(d.GetComponent<Renderer>().sharedMaterial);
            }
            var frameProto = Load<GameObject>(D + "Cadre_family.glb");
            if (mats.Count < 2 || frameProto == null) return;
            Physics.SyncTransforms();
            Vector3 from = P(new Vector3(0f, 1.8f, 27.5f));
            if (!Physics.Raycast(from, Flat(Dir(Vector3.forward)), out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore)) return;
            var frame = (GameObject)PrefabUtility.InstantiatePrefab(frameProto);
            frame.name = "Le dessin qui change (fond du dortoir)";
            frame.transform.SetPositionAndRotation(hit.point + hit.normal * 0.03f, Quaternion.LookRotation(hit.normal, Vector3.up));
            frame.transform.localScale = Vector3.one * 3f;
            ChangingDrawing cd = frame.AddComponent<ChangingDrawing>();
            cd.target = FindChild(frame.transform, "Dessin").GetComponent<Renderer>();
            cd.versions = mats.ToArray();
            cd.scratch = Load<AudioClip>(Audio + "Ambience/creak_02.wav");
        }

        // ------------------------------------------------------------------ l'Envers (après la fin)

        /// <summary>
        /// L'Envers : l'Institut « vu de l'autre côté du dessin ». Une pièce de papier blanc où tout est tracé au
        /// crayon (contours noirs), avec les dessins des enfants en géant. On y entre par la machine 23 une fois
        /// la fin débloquée (bouton ENTER) ; un bouton ramène à la Salle de jeux. Aucun monstre : ici, on peut crier.
        /// </summary>
        private static void SetupEnvers(GameObject ending, Vector3 spawn)
        {
            var root = new GameObject("L'Envers (zone secrète)");
            Material paper = MakeMat("Envers_Papier", new Color(0.97f, 0.95f, 0.9f));
            Material pencil = MakeMat("Envers_Crayon", new Color(0.12f, 0.1f, 0.14f));
            // le papier luit un peu : l'Envers doit paraître blanc malgré l'étalonnage sombre du reste du jeu
            paper.EnableKeyword("_EMISSION"); paper.SetColor("_EmissionColor", new Color(0.62f, 0.6f, 0.56f));
            paper.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; EditorUtility.SetDirty(paper);   // None : URP retirerait _EMISSION
            Vector3 O = new Vector3(0f, 60f, 0f);   // très au-dessus (sous -30 m, le joueur est ramené au spawn)
            GameObject Box(string name, Material m, Vector3 c, Vector3 size, bool solid = true)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = name; go.transform.SetParent(root.transform, false);
                go.transform.position = P(O + c); go.transform.localScale = size * s_w;
                go.GetComponent<Renderer>().sharedMaterial = m;
                if (!solid) Object.DestroyImmediate(go.GetComponent<Collider>());
                return go;
            }
            void Line(Vector3 a, Vector3 b)   // un trait de crayon (un peu tremblé)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.transform.SetParent(root.transform, false);
                Vector3 wa = P(O + a), wb = P(O + b);
                go.transform.position = (wa + wb) * 0.5f; go.transform.up = (wb - wa).normalized;
                go.transform.localScale = new Vector3(0.05f * s_w, (wb - wa).magnitude * 0.5f, 0.05f * s_w);
                go.GetComponent<Renderer>().sharedMaterial = pencil;
            }
            void Sketch(Vector3 lo, Vector3 hi)   // une boîte dessinée seulement en contours
            {
                Vector3[] v = { new Vector3(lo.x, lo.y, lo.z), new Vector3(hi.x, lo.y, lo.z), new Vector3(hi.x, lo.y, hi.z), new Vector3(lo.x, lo.y, hi.z),
                                new Vector3(lo.x, hi.y, lo.z), new Vector3(hi.x, hi.y, lo.z), new Vector3(hi.x, hi.y, hi.z), new Vector3(lo.x, hi.y, hi.z) };
                int[,] e = { { 0, 1 }, { 1, 2 }, { 2, 3 }, { 3, 0 }, { 4, 5 }, { 5, 6 }, { 6, 7 }, { 7, 4 }, { 0, 4 }, { 1, 5 }, { 2, 6 }, { 3, 7 } };
                for (int i = 0; i < 12; i++) Line(v[e[i, 0]] + Vector3.up * 0.01f, v[e[i, 1]] + Vector3.right * 0.015f);
            }
            const float W = 8f, H = 6f;
            Box("Envers_Sol", paper, new Vector3(0f, -0.05f, 0f), new Vector3(2 * W, 0.1f, 2 * W));
            Box("Envers_Plafond", paper, new Vector3(0f, H, 0f), new Vector3(2 * W, 0.1f, 2 * W));
            Box("Envers_Mur_N", paper, new Vector3(0f, H / 2, W), new Vector3(2 * W, H, 0.1f));
            Box("Envers_Mur_S", paper, new Vector3(0f, H / 2, -W), new Vector3(2 * W, H, 0.1f));
            Box("Envers_Mur_E", paper, new Vector3(W, H / 2, 0f), new Vector3(0.1f, H, 2 * W));
            Box("Envers_Mur_O", paper, new Vector3(-W, H / 2, 0f), new Vector3(0.1f, H, 2 * W));
            Sketch(new Vector3(-W, 0f, -W), new Vector3(W, H, W));                         // la pièce elle-même, dessinée
            for (int i = 0; i < 6; i++) Sketch(new Vector3(-6f + i * 2.2f, 0f, 4.5f), new Vector3(-4.6f + i * 2.2f, 0.6f, 6.5f));   // des lits
            Sketch(new Vector3(-1.2f, 0f, -W), new Vector3(1.2f, 2.6f, -W + 0.2f));        // une porte qui ne s'ouvre pas
            Sketch(new Vector3(-1f, 0f, -1f), new Vector3(1f, 0.9f, 1f));                   // une table
            string[] keys = { "family", "friends", "lady", "pool" };
            Vector3[] spots = { new Vector3(-W + 0.06f, 3f, -3f), new Vector3(-W + 0.06f, 3f, 3f), new Vector3(W - 0.06f, 3f, -3f), new Vector3(W - 0.06f, 3f, 3f) };
            for (int i = 0; i < 4; i++)
            {
                var proto = Load<GameObject>("Assets/Horror/Art/Map/Decor/Cadre_" + keys[i] + ".glb");
                if (proto == null) continue;
                var f = (GameObject)PrefabUtility.InstantiatePrefab(proto);
                f.transform.SetParent(root.transform, false);
                Vector3 n = Flat(Dir(new Vector3(spots[i].x < 0 ? 1f : -1f, 0f, 0f)));
                f.transform.SetPositionAndRotation(P(O + spots[i]) - Vector3.up * 1.0f, Quaternion.LookRotation(n, Vector3.up));
                f.transform.localScale = Vector3.one * 6f;
            }
            GameObject msg = SafeZoneShopBuilder.Label(root.transform, "HERE, NOBODY LISTENS.\nHERE, WE CAN SCREAM.\n\n- MILA, AND THE 22 OTHERS", Vector3.zero, 0.05f, new Color(0.15f, 0.12f, 0.18f));
            msg.transform.position = P(O + new Vector3(0f, 3.6f, W - 0.08f));
            msg.transform.rotation = Quaternion.LookRotation(-Flat(Dir(Vector3.back)));
            foreach (Vector3 lp in new[] { new Vector3(-4f, 5f, 0f), new Vector3(4f, 5f, 0f), new Vector3(0f, 5f, -5f) })
            {
                var lg = new GameObject("Lumière de l'Envers"); lg.transform.SetParent(root.transform, false);
                lg.transform.position = P(O + lp);
                Light li = lg.AddComponent<Light>(); li.type = LightType.Point; li.color = new Color(1f, 0.97f, 0.92f); li.intensity = 3.5f; li.range = 14f * s_w;
            }
            // atmosphère propre : pas de brouillard noir, étalonnage clair (volume local prioritaire sur le global)
            var atmo = new GameObject("Atmosphère de l'Envers"); atmo.transform.SetParent(root.transform, false);
            atmo.transform.position = P(O + new Vector3(0f, H / 2, 0f));
            AtmosphereZone az = atmo.AddComponent<AtmosphereZone>();
            az.size = new Vector3(2 * W, H + 2f, 2 * W) * s_w;
            BoxCollider vb = atmo.AddComponent<BoxCollider>(); vb.isTrigger = true; vb.size = az.size;
            var vol = atmo.AddComponent<UnityEngine.Rendering.Volume>();
            vol.isGlobal = false; vol.priority = 10f; vol.blendDistance = 1f;
            string vpath = "Assets/Horror/Art/Map/Genere/Envers_Etalonnage.asset";
            if (Load<UnityEngine.Rendering.VolumeProfile>(vpath) != null) AssetDatabase.DeleteAsset(vpath);
            var vp = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
            AssetDatabase.CreateAsset(vp, vpath);
            var ec = vp.Add<UnityEngine.Rendering.Universal.ColorAdjustments>(true); ec.name = "ColorAdjustments"; AssetDatabase.AddObjectToAsset(ec, vp);
            ec.postExposure.Override(1.2f); ec.saturation.Override(-35f); ec.contrast.Override(25f); ec.colorFilter.Override(Color.white);
            var es = vp.Add<UnityEngine.Rendering.Universal.SplitToning>(true); es.name = "SplitToning"; AssetDatabase.AddObjectToAsset(es, vp);
            es.shadows.Override(Color.grey); es.highlights.Override(Color.grey);
            var ev = vp.Add<UnityEngine.Rendering.Universal.Vignette>(true); ev.name = "Vignette"; AssetDatabase.AddObjectToAsset(ev, vp);
            ev.intensity.Override(0.18f); ev.color.Override(new Color(0.3f, 0.28f, 0.3f));
            EditorUtility.SetDirty(vp);
            vol.sharedProfile = vp;
            var arrival = new GameObject("Arrivée dans l'Envers"); arrival.transform.SetParent(root.transform, false);
            arrival.transform.position = P(O + new Vector3(0f, 0.3f, -5.5f));
            // retour : un bouton crayonné près de la porte dessinée
            GameObject back = Box("Bouton retour", MakeMat("Lobby_Bouton", new Color(0.95f, 0.62f, 0.72f)), new Vector3(1.8f, 1.1f, -W + 0.08f), new Vector3(0.22f, 0.22f, 0.08f));
            back.GetComponent<Collider>().isTrigger = true;
            var backSpot = new GameObject("Retour Salle de jeux"); backSpot.transform.SetParent(root.transform, false); backSpot.transform.position = spawn;
            Teleporter tb = back.AddComponent<Teleporter>();
            tb.button = back.AddComponent<PressButton>();
            tb.destination = backSpot.transform;
            GameObject backTxt = SafeZoneShopBuilder.Label(root.transform, "BACK", Vector3.zero, 0.02f, new Color(0.15f, 0.12f, 0.18f));
            backTxt.transform.position = P(O + new Vector3(1.8f, 1.45f, -W + 0.08f));
            backTxt.transform.rotation = Quaternion.LookRotation(-Flat(Dir(Vector3.forward)));
            // entrée : dans la fin (machine 23), un bouton ENTER
            var enter = GameObject.CreatePrimitive(PrimitiveType.Cube);
            enter.name = "Bouton ENTER (machine 23)";
            enter.transform.SetParent(ending.transform, false);
            enter.transform.position = P(new Vector3(47f - 1.3f, 1.2f, 0.55f));
            enter.transform.localScale = new Vector3(0.08f, 0.22f, 0.22f) * s_w;
            enter.GetComponent<Renderer>().sharedMaterial = MakeMat("Lobby_Bouton", new Color(0.95f, 0.62f, 0.72f));
            enter.GetComponent<Collider>().isTrigger = true;
            Teleporter te = enter.AddComponent<Teleporter>();
            te.button = enter.AddComponent<PressButton>();
            te.destination = arrival.transform;
            GameObject enterTxt = SafeZoneShopBuilder.Label(ending.transform, "CRAWL IN", Vector3.zero, 0.014f, new Color(0.85f, 0.9f, 1f));
            enterTxt.transform.position = P(new Vector3(47f - 1.32f, 1.55f, 0.55f));
            enterTxt.transform.rotation = Quaternion.LookRotation(-Flat(Dir(new Vector3(-1f, 0f, 0f))));
        }

        // ------------------------------------------------------------------ traces et coupures

        /// <summary>
        /// Flaques et gouttes d'encre au sol le long du réseau de passage : on devine où passe L'Écoute
        /// (sa robe trempée goutte). Jamais dans la Salle de jeux. Un seul maillage (léger en VR).
        /// </summary>
        private static void SetupInkTrails(WaypointGraph g, RoomData playroom)
        {
            if (g == null || g.points == null || g.links == null) return;
            Bounds safe = default;
            if (playroom != null)
            {
                Vector3 a = P(playroom.min), b = P(playroom.max);
                safe = new Bounds((a + b) * 0.5f, new Vector3(Mathf.Abs(b.x - a.x) + 1f, 50f, Mathf.Abs(b.z - a.z) + 1f));
            }
            Physics.SyncTransforms();
            var rnd = new System.Random(23);
            var verts = new List<Vector3>();
            var tris = new List<int>();
            foreach (Vector2Int l in g.links)
            {
                if (l.x < 0 || l.y < 0 || l.x >= g.points.Length || l.y >= g.points.Length) continue;
                Vector3 a = g.points[l.x], b = g.points[l.y];
                float len = Vector3.Distance(a, b);
                int n = Mathf.FloorToInt(len / (0.8f * s_w));
                Vector3 dir = (b - a).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
                for (int i = 1; i < n; i++)
                {
                    if (rnd.NextDouble() > 0.45) continue;
                    Vector3 p = Vector3.Lerp(a, b, (float)i / n) + side * (float)(rnd.NextDouble() - 0.5) * 0.6f * s_w;
                    if (playroom != null && safe.Contains(p)) continue;
                    if (!Physics.Raycast(p + Vector3.up * 1.2f, Vector3.down, out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (hit.normal.y < 0.8f) continue;   // seulement sur le sol
                    float r = (0.04f + (float)rnd.NextDouble() * 0.07f) * s_w;
                    AddBlob(verts, tris, hit.point + Vector3.up * 0.006f, r, rnd);
                    if (rnd.NextDouble() < 0.5)            // une petite goutte à côté
                        AddBlob(verts, tris, hit.point + Vector3.up * 0.006f + (dir * 0.12f + side * 0.05f) * s_w, r * 0.3f, rnd);
                }
            }
            if (verts.Count == 0) return;
            var mesh = new Mesh { name = "Traces_Encre" };
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EnsureFolder("Assets/Horror/Art/Map/Genere");
            string path = "Assets/Horror/Art/Map/Genere/Traces_Encre.asset";
            if (Load<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);   // produit par ce menu : on le refait
            AssetDatabase.CreateAsset(mesh, path);
            var go = new GameObject("Traces d'encre (ronde de L'Écoute)");
            go.isStatic = true;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            Material ink = MakeMat("Encre_Sol", new Color(0.012f, 0.008f, 0.016f));
            ink.color = new Color(0.012f, 0.008f, 0.016f);                              // presque noire
            if (ink.HasProperty("_Smoothness")) ink.SetFloat("_Smoothness", 0.6f);    // encore humide, sans briller comme un miroir
            EditorUtility.SetDirty(ink);
            go.AddComponent<MeshRenderer>().sharedMaterial = ink;
        }

        /// <summary>Une flaque irrégulière (éventail de triangles), à plat sur le sol.</summary>
        private static void AddBlob(List<Vector3> verts, List<int> tris, Vector3 c, float r, System.Random rnd)
        {
            int start = verts.Count, rim = 20;
            float stretch = 1f + (float)rnd.NextDouble() * 0.8f, ang0 = (float)rnd.NextDouble() * Mathf.PI;
            verts.Add(c);
            for (int k = 0; k < rim; k++)
            {
                float a = k * Mathf.PI * 2f / rim;
                float rr = r * (0.8f + 0.25f * Mathf.Sin(a * 3f + ang0) + (float)rnd.NextDouble() * 0.15f);   // contour mou
                Vector3 local = new Vector3(Mathf.Cos(a) * rr * stretch, 0f, Mathf.Sin(a) * rr);
                verts.Add(c + Quaternion.Euler(0f, ang0 * Mathf.Rad2Deg, 0f) * local);
            }
            for (int k = 0; k < rim; k++)
            {
                tris.Add(start); tris.Add(start + 1 + (k + 1) % rim); tris.Add(start + 1 + k);
            }
        }

        private static void SetupBlackout()
        {
            var go = new GameObject("Coupures de courant");
            BlackoutDirector b = go.AddComponent<BlackoutDirector>();
            b.powerDown = Load<AudioClip>(Audio + "World/power_down.wav");
            b.powerUp = Load<AudioClip>(Audio + "World/power_up.wav");
        }

        // ------------------------------------------------------------------ Doux : modèles Blender

        private const string DouxFolder = "Assets/Horror/Art/Monsters/Doux/";
        // Le rang des Doux, dans les coordonnées de la maquette (Tools/Map/build_institut.py, théâtre).
        private static readonly string[] DouxKinds = { "Lapin", "Ours", "Chat", "Chien", "Lapin", "Ours" };
        private static readonly string[] DouxFurs = { "rose", "menthe", "lilas", "jaune", "bleu", "peche" };
        private static readonly string[] DouxColors = { "#E1B3BF", "#B3D6C6", "#C7B6DC", "#E9D7A0", "#B2C5E1", "#EBC1A8" };
        private static readonly float[] DouxRowX = { 16.8f, 18.6f, 20.4f, 24.1f, 25.9f, 27.7f };

        /// <summary>
        /// Pose les Doux en modèles séparés (tête sur pivot au cou), teint leur fourrure, et retire les anciens
        /// Doux de la maquette (têtes cachées, corps du rang découpés des maillages fusionnés : les costumes pendus
        /// et le Doux assis de la loge restent). Renvoie faux si les modèles ne sont pas dans le projet.
        /// </summary>
        private static bool SetupSoftieModels(GameObject map, MapData data)
        {
            if (Load<GameObject>(DouxFolder + "Doux_Lapin.glb") == null) return false;

            var root = new GameObject("Les Doux (1, 2, 3, soleil)");
            Quaternion facing = Quaternion.LookRotation(Dir(Vector3.forward), Vector3.up);   // ils regardent le rideau
            var spots = new List<Vector3>();
            for (int k = 0; k < DouxRowX.Length; k++)
            {
                Vector3 feet = P(new Vector3(DouxRowX[k], 1.1f, -19f - 0.25f * (k % 2)));
                float yaw = (k % 2 == 0 ? 6f : -6f) * s_sx * s_sz;
                GameObject d = PlaceDoux(root.transform, "Doux " + k + " (" + DouxKinds[k] + ")", DouxKinds[k], k, feet, facing * Quaternion.Euler(0f, yaw, 0f));
                SoftieWatcher w = d.AddComponent<SoftieWatcher>();
                w.head = FindChild(d.transform, "Tete");
                spots.Add(feet);
            }

            Bounds theatre = default;
            foreach (RoomData r in data.rooms)
                if (r.zone == "Theatre")
                {
                    Vector3 a = P(r.min), b = P(r.max);
                    theatre = new Bounds((a + b) * 0.5f, new Vector3(Mathf.Abs(b.x - a.x) - 0.6f, 20f, Mathf.Abs(b.z - a.z) - 0.6f));
                }
            Vector3 walkerFeet = P(new Vector3(22.25f, 1.1f, -18.4f));     // pile devant la fente du rideau
            GameObject walker = PlaceDoux(root.transform, "Le Doux qui avance", "Lapin", 0, walkerFeet, facing);
            SoftieWatcher ww = walker.AddComponent<SoftieWatcher>();
            ww.head = FindChild(walker.transform, "Tete");
            ww.canWalk = true;
            ww.area = theatre;
            ww.shuffleLoop = Load<AudioClip>(Audio + "World/softie_shuffle.wav");
            ww.settleSound = Load<AudioClip>(Audio + "World/softie_settle.wav");
            ww.jumpscareSound = Load<AudioClip>(Audio + "Jumpscare/doux_cri.wav");
            ww.jumpscareFaceScale = 0.3f;   // sa grosse tête doit tenir dans le champ de vision
            GaitAnimator wg = ww.gameObject.AddComponent<GaitAnimator>();   // il se dandine comme une peluche, puis trottine
            wg.walkSpeed = ww.walkSpeed * 0.45f; wg.runSpeed = ww.walkSpeed; wg.stride = 0.7f;
            wg.rollWalk = 10f; wg.rollRun = 14f; wg.bobWalk = 0.08f; wg.bobRun = 0.16f; wg.leanRun = 12f; wg.armSwing = 0f; wg.armReachRun = 0f;
            spots.Add(walkerFeet);

            // les anciens Doux de la maquette
            foreach (MeshFilter mf in map.GetComponentsInChildren<MeshFilter>(true))
            {
                string n = mf.gameObject.name;
                if (n.Contains("TeteDoux_") || n.Contains("DouxMarcheur_"))
                {
                    Renderer r = mf.GetComponent<Renderer>();
                    if (r != null) r.enabled = false;
                }
                else if (n.Contains("Theatre_doux_") || n.Contains("Theatre_tissu_creme"))
                    CutOutDoux(mf, spots);
            }
            return true;
        }

        private static GameObject PlaceDoux(Transform parent, string name, string kind, int fur, Vector3 feet, Quaternion rot)
        {
            var proto = Load<GameObject>(DouxFolder + "Doux_" + kind + ".glb");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(proto);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(feet, rot);
            go.transform.localScale = Vector3.one * s_w;     // même agrandissement que la maquette
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] != null && mats[i].name.StartsWith("Fourrure")) mats[i] = DouxFur(fur, mats[i]);
                r.sharedMaterials = mats;
            }
            return go;
        }

        /// <summary>Fourrure teinte (une matière par couleur, enregistrée à côté des autres matières de l'Institut).</summary>
        private static Material DouxFur(int fur, Material source)
        {
            EnsureFolder("Assets/Horror/Art/Materials");
            string path = "Assets/Horror/Art/Materials/Doux_Fourrure_" + DouxFurs[fur] + ".mat";
            var mat = Load<Material>(path);
            if (mat != null) return mat;
            ColorUtility.TryParseHtmlString(DouxColors[fur], out Color c);
            mat = new Material(source) { name = "Doux_Fourrure_" + DouxFurs[fur] };
            if (mat.HasProperty("baseColorFactor")) mat.SetColor("baseColorFactor", c);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        /// <summary>Retire d'un maillage fusionné de la maquette les triangles des anciens Doux du rang
        /// (colonne autour de chaque emplacement). Le résultat est enregistré en asset et sert aussi de collision.</summary>
        private static void CutOutDoux(MeshFilter mf, List<Vector3> spots)
        {
            Mesh src = mf.sharedMesh;
            if (src == null) return;
            float radius = 0.75f * s_w, height = 2.3f * s_w;
            Vector3[] v = src.vertices;
            Mesh dst = Object.Instantiate(src);
            dst.name = src.name + "_sansDoux";
            bool changed = false;
            for (int s = 0; s < src.subMeshCount; s++)
            {
                int[] tris = src.GetTriangles(s);
                var keep = new List<int>(tris.Length);
                for (int i = 0; i < tris.Length; i += 3)
                {
                    Vector3 c = mf.transform.TransformPoint((v[tris[i]] + v[tris[i + 1]] + v[tris[i + 2]]) / 3f);
                    bool inside = false;
                    foreach (Vector3 f in spots)
                    {
                        Vector2 d = new Vector2(c.x - f.x, c.z - f.z);
                        if (d.magnitude < radius && c.y > f.y - 0.1f && c.y < f.y + height) { inside = true; break; }
                    }
                    if (inside) changed = true;
                    else { keep.Add(tris[i]); keep.Add(tris[i + 1]); keep.Add(tris[i + 2]); }
                }
                dst.SetTriangles(keep, s);
            }
            if (!changed) { Object.DestroyImmediate(dst); return; }
            int left = 0;
            for (int s = 0; s < dst.subMeshCount; s++) left += (int)dst.GetIndexCount(s);
            if (left == 0)
            {
                // tout le maillage était dans la zone : on cache l'objet (un collider vide ferait une erreur PhysX)
                Object.DestroyImmediate(dst);
                MeshCollider empty = mf.GetComponent<MeshCollider>();
                if (empty != null) Object.DestroyImmediate(empty);
                mf.gameObject.SetActive(false);
                return;
            }
            EnsureFolder("Assets/Horror/Art/Map/Genere");
            string path = "Assets/Horror/Art/Map/Genere/" + dst.name + ".asset";
            if (Load<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);   // fichier produit par ce menu : on le refait
            AssetDatabase.CreateAsset(dst, path);
            mf.sharedMesh = dst;
            MeshCollider col = mf.GetComponent<MeshCollider>();
            if (col != null) col.sharedMesh = dst;
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        /// <summary>Copie des morceaux (sommets en coordonnées monde) sous un pivot placé où on veut ; cache les originaux.</summary>
        private static Transform MakePivot(Transform parent, string name, List<MeshFilter> parts, Vector3 pivotPos, Quaternion pivotRot)
        {
            var pivot = new GameObject(name);
            pivot.transform.SetParent(parent, false);
            pivot.transform.SetPositionAndRotation(pivotPos, pivotRot);
            foreach (MeshFilter mf in parts)
            {
                Renderer src = mf.GetComponent<Renderer>();
                var copy = new GameObject(mf.gameObject.name);
                copy.transform.SetPositionAndRotation(mf.transform.position, mf.transform.rotation);
                copy.transform.localScale = mf.transform.lossyScale;
                copy.transform.SetParent(pivot.transform, true);
                copy.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                MeshRenderer mr = copy.AddComponent<MeshRenderer>();
                if (src != null) { mr.sharedMaterials = src.sharedMaterials; src.enabled = false; }
            }
            return pivot.transform;
        }

        private static Vector3 CenterOf(List<MeshFilter> parts)
        {
            Bounds b = default;
            bool first = true;
            foreach (MeshFilter mf in parts)
            {
                Renderer r = mf.GetComponent<Renderer>();
                if (r == null) continue;
                if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
            }
            return b.center;
        }

        private static float MinYOf(List<MeshFilter> parts)
        {
            float y = float.MaxValue;
            foreach (MeshFilter mf in parts)
            {
                Renderer r = mf.GetComponent<Renderer>();
                if (r != null) y = Mathf.Min(y, r.bounds.min.y);
            }
            return y == float.MaxValue ? 0f : y;
        }

        private static void SetupNight(MapData data)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.035f, 0.03f, 0.045f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.055f;
            RenderSettings.fogColor = new Color(0.02f, 0.015f, 0.025f);
            RenderSettings.skybox = null;

            var root = new GameObject("Lumières de nuit");
            foreach (LightData l in data.lights)
            {
                var go = new GameObject("Lumière");
                go.transform.SetParent(root.transform, false);
                go.transform.position = P(l.pos);
                Light light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = ColorUtility.TryParseHtmlString(l.color, out Color c) ? c : Color.white;
                light.intensity = l.intensity * 3.2f;
                light.range = l.distance * 1.6f * s_w;
                light.shadows = LightShadows.None;   // performances en VR
                go.AddComponent<LightFlicker>();      // grésille quand L'Écoute passe tout près
            }
        }

        /// <summary>Ambiances par pièce + zone sûre. Renvoie la Salle de jeux.</summary>
        private static RoomData SetupRooms(MapData data)
        {
            RoomData playroom = null;
            var root = new GameObject("Ambiances");
            AudioClip basement = Load<AudioClip>(Audio + "Ambience/basement_loop.wav");
            AudioClip[] drips = LoadMany(Audio + "Ambience", "drip_");
            AudioClip[] creaks = LoadMany(Audio + "Ambience", "creak_");
            AudioClip giggle = Load<AudioClip>(Audio + "Ambience/child_giggle_far.wav");
            AudioClip musicBox = Load<AudioClip>(Audio + "Ambience/music_box_far.wav");

            foreach (RoomData r in data.rooms)
            {
                Vector3 a = P(r.min), b = P(r.max);
                Vector3 center = (a + b) * 0.5f;
                Vector3 size = new Vector3(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y), Mathf.Abs(b.z - a.z));
                var go = new GameObject("Ambiance " + r.name);
                go.transform.SetParent(root.transform, false);
                go.transform.position = center;
                AmbientZone zone = go.AddComponent<AmbientZone>();
                zone.size = size;

                if (r.zone == "SalleDeJeux")
                {
                    playroom = r;
                    zone.isSafe = true;
                    zone.priority = 10;
                    zone.loop = Load<AudioClip>(Audio + "SafeZone/lullaby_loop.wav");
                    zone.loopVolume = 0.45f;
                    SafeZone safe = go.AddComponent<SafeZone>();
                    safe.size = size;
                    var respawn = new GameObject("Point de réapparition");
                    respawn.transform.SetParent(go.transform, false);
                    respawn.transform.position = P(data.spawn);
                    safe.respawnPoint = respawn.transform;
                }
                else if (r.zone == "Theatre")
                {
                    // Pas de musique : le silence d'une salle vide, des grincements, et quelqu'un qui applaudit lentement
                    zone.oneShots = Concat(creaks, new[] { Load<AudioClip>(Audio + "World/slow_clap.wav"), musicBox });
                    zone.oneShotInterval = new Vector2(9f, 20f);
                }
                else if (r.zone == "Ventilateur")
                {
                    zone.loop = Load<AudioClip>(Audio + "World/fan_loop.wav");
                    zone.loopVolume = 0.55f;
                    zone.oneShots = creaks;
                }
                else if (r.min.y < -1f)   // sous-sol
                {
                    zone.loop = basement;
                    zone.loopVolume = 0.4f;
                    zone.oneShots = Concat(drips, creaks);
                }
                else if (r.zone == "Ecole")
                {
                    zone.oneShots = Concat(creaks, new[] { giggle, musicBox });
                    zone.oneShotInterval = new Vector2(7f, 16f);
                }
            }
            return playroom;
        }

        // ------------------------------------------------------------------ boutique

        private static void SetupShop(GameObject map, RoomData playroom, CosmeticItem[] items)
        {
            if (playroom == null) return;
            var root = new GameObject("Boutique (Salle de jeux)");
            AudioClip click = Load<AudioClip>(Audio + "UI/button_click.wav");
            AudioClip coin = Load<AudioClip>(Audio + "UI/coin.wav");
            AudioClip denied = Load<AudioClip>(Audio + "UI/denied.wav");
            Vector3 roomCenter = P(playroom.center);
            // Ordre des présentoirs dans la maquette (Tools/Character/build_cosmetics.py, CATALOG) : chaque objet
            // va sur SON présentoir (avant, l'ordre de la liste ne correspondait pas : on achetait autre chose
            // que l'objet exposé).
            string[] maquetteOrder = { "paper_crown", "party_hat", "ribbon_bow", "yarn_scarf", "moon_badge" };
            foreach (MeshFilter mf in map.GetComponentsInChildren<MeshFilter>(true))   // anciennes copies figées
                if (mf.gameObject.name.Contains("Cosmetique_"))
                {
                    Renderer r = mf.GetComponent<Renderer>();
                    if (r != null) r.enabled = false;
                }

            for (int i = 0; i < items.Length; i++)
            {
                int spot = System.Array.IndexOf(maquetteOrder, items[i].id);
                if (spot < 0) spot = i;
                float x = 10.6f + spot * 1.6f;
                Vector3 pedestal = P(new Vector3(x, 0f, 11.0f));
                Vector3 inward = Flat(roomCenter - pedestal);
                var stand = new GameObject("Présentoir " + items[i].displayName);
                stand.transform.SetParent(root.transform, false);
                stand.transform.position = pedestal;

                GameObject label = SafeZoneShopBuilder.Label(stand.transform, items[i].displayName.ToUpper() + "\n" + GameNames.Amount(items[i].price),
                                                             Vector3.zero, 0.02f, new Color(0.95f, 0.92f, 0.86f));
                label.transform.position = pedestal + Vector3.up * 1.55f * s_w;
                label.transform.rotation = Quaternion.LookRotation(-inward);   // lisible depuis la pièce

                var btn = GameObject.CreatePrimitive(PrimitiveType.Cube);
                btn.name = "Bouton acheter (invisible, le visuel est dans la maquette)";
                btn.transform.SetParent(stand.transform, false);
                btn.transform.position = P(new Vector3(x, 0.45f, 10.67f));
                btn.transform.rotation = Quaternion.LookRotation(inward);
                btn.transform.localScale = new Vector3(0.16f, 0.16f, 0.08f) * s_w;
                btn.GetComponent<Renderer>().enabled = false;
                PressButton press = btn.AddComponent<PressButton>();
                press.clickSound = click;

                ShopStand shop = stand.AddComponent<ShopStand>();
                shop.item = items[i];
                shop.label = label.GetComponent<TextMesh>();
                shop.button = press;
                // le vrai modèle, posé sur le présentoir (même taille que l'ancienne copie : x2,2), qui tourne
                float lift = items[i].slot == CosmeticSlot.Cou ? 0.46f : items[i].slot == CosmeticSlot.Poitrine ? 0.02f : 0f;
                var anchor = new GameObject("Aperçu " + items[i].id);
                anchor.transform.SetParent(stand.transform, false);
                anchor.transform.position = P(new Vector3(x, 0.95f + lift, 11.0f));
                anchor.transform.localScale = Vector3.one * (2.2f * s_w / Mathf.Max(0.01f, items[i].scale));
                shop.previewAnchor = anchor.transform;
                shop.buySound = coin;
                shop.deniedSound = denied;
            }

            // Compteur de pièces (mur est de la maquette)
            Vector3 coinsPos = P(new Vector3(20.8f, 1.7f, 7.0f));
            GameObject coins = SafeZoneShopBuilder.Label(root.transform, GameNames.Currency + ": 100", Vector3.zero, 0.04f, new Color(0.95f, 0.85f, 0.5f));
            coins.transform.position = coinsPos;
            coins.transform.rotation = Quaternion.LookRotation(-Flat(roomCenter - coinsPos));
            coins.AddComponent<CoinDisplay>();

            // Boîte de crayons de la maquette (mur est) : bouton invisible + compte à rebours au-dessus
            GameObject box = SafeZoneShopBuilder.BuildCrayonBox(root.transform, false);
            Vector3 boxPos = P(new Vector3(20.45f, 0f, 9.6f));
            Vector3 boxIn = Flat(roomCenter - boxPos);
            box.transform.position = boxPos;
            box.transform.rotation = Quaternion.LookRotation(boxIn);   // +Z local = vers la pièce
            Transform boxBtn = box.transform.Find("Bouton récolter");
            if (boxBtn != null) { boxBtn.position = P(new Vector3(20.08f, 0.15f, 9.6f)) + boxIn * 0.03f; boxBtn.localScale *= s_w; }
            Transform boxLabel = box.transform.Find("Texte : FREE " + GameNames.Currency);
            if (boxLabel != null) boxLabel.position = boxPos + Vector3.up * 1.75f * s_w;
        }

        /// <summary>Le Minitel des réglages : dans la Salle de jeux (zone sûre), sur le petit bureau du mur ouest.</summary>
        private static void SetupMinitel()
        {
            var root = new GameObject("Minitel (réglages)");
            Vector3 toPlayer = Dir(new Vector3(1f, 0f, 0f));    // l'écran regarde vers le centre de la Salle de jeux
            GameObject screen = SafeZoneShopBuilder.Label(root.transform, "", Vector3.zero, 0.0042f * s_w, new Color(0.45f, 1f, 0.6f));
            screen.name = "Écran";
            screen.transform.position = P(new Vector3(7.965f, 1.04f, 10.25f));
            screen.transform.rotation = Quaternion.LookRotation(-toPlayer);
            TextMesh tm = screen.GetComponent<TextMesh>();
            tm.fontSize = 48;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Left;

            Material keyMat = MakeMat("Touche Minitel", new Color(0.85f, 0.8f, 0.7f));
            AudioClip click = Load<AudioClip>(Audio + "UI/button_click.wav");
            string[] names = { "Touche <", "Touche >", "Touche OK", "Touche BACK" };
            var keys = new PressButton[4];
            for (int i = 0; i < 4; i++)
            {
                GameObject key = GameObject.CreatePrimitive(PrimitiveType.Cube);
                key.name = names[i];
                key.transform.SetParent(root.transform, false);
                key.transform.position = P(new Vector3(8.12f, 0.845f, 10.07f + i * 0.12f));
                key.transform.rotation = Quaternion.LookRotation(toPlayer);
                key.transform.localScale = new Vector3(0.08f, 0.04f, 0.08f) * s_w;
                key.GetComponent<Renderer>().sharedMaterial = keyMat;
                keys[i] = key.AddComponent<PressButton>();
                keys[i].clickSound = click;
                SafeZoneShopBuilder.Label(key.transform, names[i].Replace("Touche ", ""), new Vector3(0f, 0.8f, 0f), 0.05f, new Color(0.2f, 0.18f, 0.2f))
                    .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // inscription sur le dessus de la touche
            }
            MinitelTerminal minitel = root.AddComponent<MinitelTerminal>();
            minitel.screen = tm;
            minitel.left = keys[0]; minitel.right = keys[1]; minitel.ok = keys[2]; minitel.back = keys[3];
            minitel.beep = click;
        }

        private static void SetupJournal(RoomData playroom, LoreDocument[] docs)
        {
            if (playroom == null || docs == null) return;
            Vector3 pos = P(new Vector3(7.2f, 2.3f, 7.6f));   // mur ouest de la Salle de jeux
            GameObject board = SafeZoneShopBuilder.Label(null, "PAPERS FOUND 0 / 13", Vector3.zero, 0.016f, new Color(0.93f, 0.88f, 0.8f));
            board.name = "Journal des papiers";
            board.transform.position = pos;
            board.transform.rotation = Quaternion.LookRotation(-Flat(P(playroom.center) - pos));
            board.GetComponent<TextMesh>().anchor = TextAnchor.UpperCenter;
            board.AddComponent<JournalBoard>().documents = docs;
        }

        // ------------------------------------------------------------------ portes

        private static void SetupDoors(MapData data)
        {
            var root = new GameObject("Portes (Protocole Verrou)");
            Material doorMat = MakeMat("Porte", new Color(0.78f, 0.62f, 0.66f));
            Material buttonMat = MakeMat("Bouton porte", new Color(0.95f, 0.55f, 0.65f));
            AudioClip slide = Load<AudioClip>(Audio + "Doors/door_slide.wav");
            AudioClip lockClip = Load<AudioClip>(Audio + "Doors/door_lock.wav");
            AudioClip keys = Load<AudioClip>(Audio + "Doors/keys_unlock.wav");
            AudioClip denied = Load<AudioClip>(Audio + "UI/denied.wav");
            AudioClip click = Load<AudioClip>(Audio + "UI/button_click.wav");

            foreach (DoorData d in data.doors)
            {
                float height = (d.top - d.bottom) * s_w;
                float width = d.width * s_w;
                Vector3 pos = P(new Vector3(d.x, d.bottom, d.z));
                // L'axe X local de la porte = le long du mur (sens de coulissement)
                Vector3 along = d.axis == "z" ? Vector3.right : Vector3.forward;
                along *= SlideSide(d, data.doors);
                along = Dir(along);
                var door = new GameObject("Porte");
                door.transform.SetParent(root.transform, false);
                door.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(Vector3.Cross(along, Vector3.up), Vector3.up));

                GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                panel.name = "Panneau";
                panel.transform.SetParent(door.transform, false);
                panel.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
                panel.transform.localScale = new Vector3(width + 0.06f, height, 0.08f);
                panel.GetComponent<Renderer>().sharedMaterial = doorMat;

                var buttons = new List<PressButton>();
                foreach (float side in new[] { 1f, -1f })
                {
                    GameObject b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    b.name = side > 0 ? "Bouton (devant)" : "Bouton (derrière)";
                    b.transform.SetParent(door.transform, false);
                    b.transform.localPosition = new Vector3(-(width * 0.5f + 0.3f), 1.1f, 0.2f * side);
                    b.transform.localScale = new Vector3(0.14f, 0.18f, 0.06f);
                    b.GetComponent<Renderer>().sharedMaterial = buttonMat;
                    PressButton pb = b.AddComponent<PressButton>();
                    pb.clickSound = click;
                    buttons.Add(pb);
                }

                LockDoor lockDoor = door.AddComponent<LockDoor>();
                lockDoor.panel = panel.transform;
                lockDoor.width = width + 0.06f;
                lockDoor.buttons = buttons.ToArray();
                lockDoor.slideSound = slide;
                lockDoor.lockSound = lockClip;
                lockDoor.monsterKeysSound = keys;
                lockDoor.deniedSound = denied;
                lockDoor.startOpen = true;   // au début, tout est ouvert : c'est toi qui fermes (et ça fait du bruit)
            }
        }

        /// <summary>De quel côté coulisser : à l'opposé de l'ouverture voisine la plus proche sur le même mur.</summary>
        private static float SlideSide(DoorData d, DoorData[] all)
        {
            float best = float.MaxValue, side = 1f;
            foreach (DoorData o in all)
            {
                if (o == d || o.axis != d.axis) continue;
                bool sameWall = d.axis == "z" ? Mathf.Abs(o.z - d.z) < 0.3f : Mathf.Abs(o.x - d.x) < 0.3f;
                if (!sameWall) continue;
                float delta = d.axis == "z" ? o.x - d.x : o.z - d.z;
                if (Mathf.Abs(delta) < best) { best = Mathf.Abs(delta); side = -Mathf.Sign(delta); }
            }
            return side;
        }

        // ------------------------------------------------------------------ papiers

        private static void SetupPapers(MapData data, LoreDocument[] docs)
        {
            if (docs == null) return;
            var byId = new Dictionary<string, LoreDocument>();
            foreach (LoreDocument doc in docs) byId[doc.id] = doc;
            var root = new GameObject("Papiers de lore");
            AudioClip pickup = Load<AudioClip>(Audio + "Papers/paper_pickup.wav");
            AudioClip found = Load<AudioClip>(Audio + "Papers/paper_found.wav");
            foreach (PaperData p in data.papers)
            {
                if (!byId.TryGetValue(p.id, out LoreDocument doc)) continue;
                Vector3 face = Dir(p.face).normalized;
                var go = new GameObject("Papier " + doc.order + " - " + doc.title);
                go.transform.SetParent(root.transform, false);
                Quaternion look = Quaternion.LookRotation(face, Mathf.Abs(face.y) > 0.9f ? Vector3.forward : Vector3.up);
                go.transform.SetPositionAndRotation(P(p.pos) + face * 0.004f, look * Quaternion.Euler(0f, 0f, p.spin));
                Rigidbody rb = go.AddComponent<Rigidbody>();
                rb.isKinematic = true;   // reste en place (même sur un mur) jusqu'à ce qu'on le prenne
                go.AddComponent<Grabbable>();
                LorePaper paper = go.AddComponent<LorePaper>();
                paper.document = doc;
                paper.pickupSound = pickup;
                paper.foundSound = found;
            }
        }

        /// <summary>Balles en caoutchouc à lancer pour faire diversion (une par pièce, sauf la zone sûre).</summary>
        private static void SetupThrowables(MapData data)
        {
            var root = new GameObject("Objets à lancer");
            Color[] colors = { new Color(0.93f, 0.7f, 0.76f), new Color(0.66f, 0.82f, 0.75f), new Color(0.72f, 0.78f, 0.92f), new Color(0.95f, 0.86f, 0.55f) };
            int k = 0;
            foreach (RoomData r in data.rooms)
            {
                if (r.zone == "SalleDeJeux" || r.zone == "Escalier") continue;
                GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                ball.name = "Balle en caoutchouc";
                ball.transform.SetParent(root.transform, false);
                Vector3 c = P(r.center);
                ball.transform.position = c + new Vector3(0.8f, 0.4f, 0.6f);
                ball.transform.localScale = Vector3.one * 0.14f;
                ball.GetComponent<Renderer>().sharedMaterial = MakeMat("Balle " + k, colors[k % colors.Length]);
                Rigidbody rb = ball.AddComponent<Rigidbody>();
                rb.mass = 0.25f;
                ball.AddComponent<Grabbable>().gripCurl = 0.7f;
                ball.AddComponent<Horror.Noise.ImpactNoise>().maxRadius = 16f;
                k++;
            }
        }

        /// <summary>Boîtes à musique leurres : une dans la Salle de jeux (à emporter : dans la zone sûre, elle
        /// n'entend rien), une au dortoir, une au Standard.</summary>
        private static void SetupMusicBoxes(MapData data)
        {
            var proto = Load<GameObject>("Assets/Horror/Art/Map/Decor/BoiteAMusique.glb");
            if (proto == null) return;
            var root = new GameObject("Boîtes à musique (leurres)");
            Physics.SyncTransforms();
            int n = 0;
            foreach (RoomData r in data.rooms)
            {
                if (r.zone != "SalleDeJeux" && r.zone != "Ecole" && r.zone != "Standard") continue;
                if (r.zone == "Ecole" && r.max.z - r.min.z < 12f) continue;       // le grand dortoir, pas le couloir
                Vector3 spot = P(r.center) + new Vector3(-1.1f, 0f, 0.9f);
                if (Physics.Raycast(spot + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 6f, ~0, QueryTriggerInteraction.Ignore))
                    spot = hit.point;
                var box = (GameObject)PrefabUtility.InstantiatePrefab(proto);
                box.name = "Boîte à musique (" + r.zone + ")";
                box.transform.SetParent(root.transform, false);
                box.transform.SetPositionAndRotation(spot + Vector3.up * 0.02f, Quaternion.Euler(0f, 25f + 40f * n, 0f));
                box.transform.localScale = Vector3.one * 1.4f;
                BoxCollider col = box.AddComponent<BoxCollider>();
                col.center = new Vector3(0f, 0.045f, 0f); col.size = new Vector3(0.17f, 0.09f, 0.12f);
                Rigidbody rb = box.AddComponent<Rigidbody>();
                rb.mass = 0.4f;
                box.AddComponent<Grabbable>().gripCurl = 0.55f;
                MusicBoxLure lure = box.AddComponent<MusicBoxLure>();
                lure.melody = Load<AudioClip>(Audio + "Ambience/music_box_far.wav");
                lure.snap = Load<AudioClip>(Audio + "World/boite_ressort_casse.wav");
                lure.dancer = FindChild(box.transform, "Danseuse");
                n++;
            }
            Debug.Log($"[Horror] Boîtes à musique leurres : {n}.");
        }

        /// <summary>Petites portes d'enfants : on y frappe parfois de l'autre côté (et ça attire le monstre).</summary>
        private static void SetupChildDoors(MapData data)
        {
            if (data.childDoors == null) return;
            var root = new GameObject("Portes d'enfants (toc toc)");
            AudioClip[] knocks = LoadMany(Audio + "World", "knock_");
            AudioClip[] voices = LoadMany(Audio + "World", "door_voice_");
            foreach (ChildDoorData d in data.childDoors)
            {
                var go = new GameObject("Porte de " + d.name);
                go.transform.SetParent(root.transform, false);
                go.transform.position = P(d.pos);
                ChildDoorKnock knock = go.AddComponent<ChildDoorKnock>();
                knock.childName = d.name;
                knock.knocks = knocks;
                knock.voices = voices;
            }
        }

        // ------------------------------------------------------------------ monstre et joueur

        private static WaypointGraph SetupNav(MapData data)
        {
            var go = new GameObject("Réseau de passage (monstres)");
            WaypointGraph g = go.AddComponent<WaypointGraph>();
            g.points = new Vector3[data.navPoints.Length];
            Physics.SyncTransforms();
            int moved = 0;
            for (int i = 0; i < data.navPoints.Length; i++)
            {
                Vector3 p0 = P(data.navPoints[i]);
                g.points[i] = FreeNavSpot(p0);
                if ((g.points[i] - p0).sqrMagnitude > 0.01f) moved++;
            }
            if (moved > 0) Debug.Log($"[Horror] Réseau de passage : {moved} point(s) sortis des meubles (ex. le pouf du hall).");
            g.links = data.navLinks;
            g.roomCount = data.rooms.Length;
            return g;
        }

        /// <summary>Un point de passage posé DANS un meuble (le pouf au centre du hall) bloquait les monstres :
        /// on le déplace au plus près, là où un monstre tient debout.</summary>
        private static Vector3 FreeNavSpot(Vector3 p)
        {
            bool Blocked(Vector3 q)
            {
                foreach (Collider c in Physics.OverlapCapsule(q + Vector3.up * 0.5f, q + Vector3.up * 1.6f, 0.4f, ~0, QueryTriggerInteraction.Ignore))
                    if (c.bounds.max.y > q.y + 0.35f) return true;   // le sol (plus bas) ne compte pas
                return false;
            }
            if (!Blocked(p)) return p;
            for (float r = 0.5f; r <= 5f; r += 0.5f)
                for (int a = 0; a < 16; a++)
                {
                    float ang = a * Mathf.PI / 8f;
                    Vector3 q = p + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * r;
                    if (!Physics.Raycast(q + Vector3.up * 0.4f, Vector3.down, out RaycastHit fh, 1f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    q.y = fh.point.y;
                    if (!Blocked(q)) return q;
                }
            return p;
        }

        /// <summary>L'Écoute garde ses bras (MonsterAnimator) ; on ajoute le poids du corps : rebond et roulis.</summary>
        private static void EcouteGait(GameObject monster)
        {
            BlindMonsterAI ai = monster.GetComponent<BlindMonsterAI>();
            GaitAnimator g = monster.AddComponent<GaitAnimator>();
            g.walkSpeed = ai != null ? ai.patrolSpeed : 1.1f; g.runSpeed = ai != null ? ai.chaseSpeed : 5.8f; g.stride = 1.2f;
            g.bobWalk = 0.04f; g.bobRun = 0.13f; g.rollWalk = 2.5f; g.rollRun = 5f; g.leanWalk = 0f; g.leanRun = 8f;
            g.armSwing = 0f; g.armReachRun = 0f;
        }

        private static void SetupMonster(MapData data, WaypointGraph graph)
        {
            // Deux Écoutes : une en haut (dortoir, couloirs, Standard), une au sous-sol (machines, galerie,
            // ventilateur, citerne, piscine). Chacune patrouille son étage, mais un bruit les attire partout.
            var patrolUp = new List<Vector3>();
            var patrolDown = new List<Vector3>();
            Vector3 startUp = Vector3.zero, startDown = Vector3.zero;
            foreach (RoomData r in data.rooms)
            {
                if (r.zone == "SalleDeJeux" || r.zone == "Escalier") continue;
                if (r.min.y < -1f) patrolDown.Add(P(r.center));
                else patrolUp.Add(P(r.center));
                if (r.name == "Ecole" && r.max.z > 20f) startUp = P(new Vector3(0f, 0f, 27f));   // au fond du dortoir
                if (r.zone == "Ventilateur") startDown = P(r.center);                            // devant le grand ventilateur
            }
            GameObject monster = MonsterTestBuilder.CreateMonster(startUp, patrolUp.ToArray());
            monster.transform.rotation = Quaternion.LookRotation(Dir(Vector3.back));
            monster.AddComponent<MonsterAnimator>();
            EcouteGait(monster);
            if (patrolDown.Count > 0)
            {
                GameObject second = MonsterTestBuilder.CreateMonster(startDown, patrolDown.ToArray());
                second.name += " (sous-sol)";
                second.AddComponent<MonsterAnimator>();
                EcouteGait(second);
            }
        }

        private static void SetupPlayer(MapData data, CosmeticItem[] items)
        {
            GameObject playerGo = TestRoomBuilder.BuildPlayer(P(data.spawn));
            float yawRad = data.spawnYaw * Mathf.Deg2Rad;
            Vector3 look = Dir(new Vector3(-Mathf.Sin(yawRad), 0f, -Mathf.Cos(yawRad)));   // convention de la maquette
            playerGo.transform.rotation = Quaternion.LookRotation(look, Vector3.up);
            ArmLocomotion loco = playerGo.GetComponent<ArmLocomotion>();
            MonsterTestBuilder.SetupPlayerNoise(loco);
            SafeZoneShopBuilder.SetupPlayer(loco, items);
            LoreSetupBuilder.SetupHands(loco);
            AvatarSetupBuilder.Build();   // la poupée, les doigts, la bouche, la glisse
            // Mannequin d'essai dans la Salle de jeux : pour tester qu'on grimpe sur la tête d'un joueur
            // (à retirer quand le multijoueur sera là).
            TestRoomBuilder.BuildClimbDummy(P(data.spawn) + Dir(new Vector3(1.6f, 0f, 1.2f)));
        }

        // ------------------------------------------------------------------ utilitaires

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
        }

        private static Material MakeMat(string name, Color color)
        {
            EnsureFolder("Assets/Horror/Art/Materials");
            string path = "Assets/Horror/Art/Materials/Institut_" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null || UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null) shader = Shader.Find("Standard");
            mat = new Material(shader) { color = color };
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);

        private static AudioClip[] LoadMany(string folder, string prefix)
        {
            var list = new List<AudioClip>();
            if (!Directory.Exists(folder)) return list.ToArray();
            foreach (string f in Directory.GetFiles(folder, prefix + "*.wav"))
            {
                AudioClip c = Load<AudioClip>(f.Replace('\\', '/'));
                if (c != null) list.Add(c);
            }
            return list.ToArray();
        }

        private static AudioClip[] Concat(AudioClip[] a, AudioClip[] b)
        {
            var list = new List<AudioClip>();
            if (a != null) list.AddRange(a);
            if (b != null) foreach (AudioClip c in b) if (c != null) list.Add(c);
            return list.ToArray();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
