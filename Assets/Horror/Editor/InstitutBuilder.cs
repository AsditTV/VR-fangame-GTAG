using System.Collections.Generic;
using System.IO;
using Horror.Audio;
using Horror.Cosmetics;
using Horror.Interaction;
using Horror.Locomotion;
using Horror.Lore;
using Horror.Monsters;
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
    public static class InstitutBuilder
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
            SetupNight(data);
            RoomData playroom = SetupRooms(data);
            CosmeticItem[] items = SafeZoneShopBuilder.EnsureItems();
            SetupShop(map, playroom, items);
            SetupMinitel();
            SetupDoors(data);
            LoreDocument[] docs = LoreSetupBuilder.LoadDocuments();
            SetupPapers(data, docs);
            SetupThrowables(data);
            SetupChildDoors(data);
            SetupJournal(playroom, docs);
            WaypointGraph graph = SetupNav(data);
            SetupMonster(data, graph);
            SetupPlayer(data, items);

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
            for (int i = 0; i < data.navPoints.Length; i++) g.points[i] = P(data.navPoints[i]);
            g.links = data.navLinks;
            g.roomCount = data.rooms.Length;
            return g;
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
            if (patrolDown.Count > 0)
            {
                GameObject second = MonsterTestBuilder.CreateMonster(startDown, patrolDown.ToArray());
                second.name += " (sous-sol)";
                second.AddComponent<MonsterAnimator>();
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
