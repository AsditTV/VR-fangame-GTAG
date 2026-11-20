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
    ///  - 9 portes du Protocole Verrou avec boutons des deux côtés ;
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
        [System.Serializable] private class MapData
        {
            public Vector3 spawn; public float spawnYaw;
            public LightData[] lights; public DoorData[] doors; public RoomData[] rooms;
            public Vector3[] navPoints; public Vector2Int[] navLinks; public PaperData[] papers;
        }
#pragma warning restore 0649

        // Correspondance d'axes maquette (glTF) -> Unity, mesurée sur le modèle importé
        private static float s_sx = 1f, s_sz = 1f;
        private static Vector3 P(Vector3 v) => new Vector3(v.x * s_sx, v.y, v.z * s_sz);
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
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject map = (GameObject)PrefabUtility.InstantiatePrefab(model);
            map.name = "Institut (maquette)";
            map.isStatic = true;
            DetectAxes(map);
            SetupGeometry(map);
            SetupNight(data);
            RoomData playroom = SetupRooms(data);
            CosmeticItem[] items = SafeZoneShopBuilder.EnsureItems();
            SetupShop(map, playroom, items);
            SetupMinitel();
            SetupDoors(data);
            LoreDocument[] docs = LoreSetupBuilder.LoadDocuments();
            SetupPapers(data, docs);
            SetupThrowables(data);
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
                MeshCollider col = mf.gameObject.AddComponent<MeshCollider>();
                col.sharedMesh = mf.sharedMesh;
            }
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
                light.range = l.distance * 1.6f;
                light.shadows = LightShadows.None;   // performances en VR
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

            for (int i = 0; i < items.Length; i++)
            {
                // Mêmes emplacements que les présentoirs de la maquette
                Vector3 pedestal = P(new Vector3(8.6f + i * 1.2f, 0f, 9.0f));
                Vector3 inward = Flat(roomCenter - pedestal);
                var stand = new GameObject("Présentoir " + items[i].displayName);
                stand.transform.SetParent(root.transform, false);
                stand.transform.position = pedestal;

                GameObject label = SafeZoneShopBuilder.Label(stand.transform, items[i].displayName.ToUpper() + "\n" + GameNames.Amount(items[i].price),
                                                             Vector3.zero, 0.02f, new Color(0.95f, 0.92f, 0.86f));
                label.transform.position = pedestal + Vector3.up * 1.55f;
                label.transform.rotation = Quaternion.LookRotation(-inward);   // lisible depuis la pièce

                var btn = GameObject.CreatePrimitive(PrimitiveType.Cube);
                btn.name = "Bouton acheter (invisible, le visuel est dans la maquette)";
                btn.transform.SetParent(stand.transform, false);
                btn.transform.position = P(new Vector3(8.6f + i * 1.2f, 0.45f, 8.67f));
                btn.transform.rotation = Quaternion.LookRotation(inward);
                btn.transform.localScale = new Vector3(0.16f, 0.16f, 0.08f);
                btn.GetComponent<Renderer>().enabled = false;
                PressButton press = btn.AddComponent<PressButton>();
                press.clickSound = click;

                ShopStand shop = stand.AddComponent<ShopStand>();
                shop.item = items[i];
                shop.label = label.GetComponent<TextMesh>();
                shop.button = press;
                shop.buySound = coin;
                shop.deniedSound = denied;
            }

            // Compteur de pièces (mur est de la maquette)
            Vector3 coinsPos = P(new Vector3(14.8f, 1.7f, 6.0f));
            GameObject coins = SafeZoneShopBuilder.Label(root.transform, GameNames.Currency + ": 100", Vector3.zero, 0.04f, new Color(0.95f, 0.85f, 0.5f));
            coins.transform.position = coinsPos;
            coins.transform.rotation = Quaternion.LookRotation(-Flat(roomCenter - coinsPos));
            coins.AddComponent<CoinDisplay>();

            // Boîte de crayons de la maquette (mur est) : bouton invisible + compte à rebours au-dessus
            GameObject box = SafeZoneShopBuilder.BuildCrayonBox(root.transform, false);
            Vector3 boxPos = P(new Vector3(14.45f, 0f, 7.9f));
            Vector3 boxIn = Flat(roomCenter - boxPos);
            box.transform.position = boxPos;
            box.transform.rotation = Quaternion.LookRotation(boxIn);   // +Z local = vers la pièce
            Transform boxBtn = box.transform.Find("Bouton récolter");
            if (boxBtn != null) boxBtn.position = P(new Vector3(14.08f, 0.15f, 7.9f)) + boxIn * 0.03f;
        }

        /// <summary>Le Minitel de l'accueil (maquette : écran vert sur le comptoir du hall).</summary>
        private static void SetupMinitel()
        {
            var root = new GameObject("Minitel (réglages)");
            Vector3 toPlayer = Dir(new Vector3(0f, 0f, -1f));   // l'écran regarde le joueur devant le comptoir
            GameObject screen = SafeZoneShopBuilder.Label(root.transform, "", Vector3.zero, 0.0042f, new Color(0.45f, 1f, 0.6f));
            screen.name = "Écran";
            screen.transform.position = P(new Vector3(-4.2f, 1.4f, 2.795f));
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
                key.transform.position = P(new Vector3(-4.38f + i * 0.12f, 1.225f, 2.7f));
                key.transform.rotation = Quaternion.LookRotation(toPlayer);
                key.transform.localScale = new Vector3(0.08f, 0.04f, 0.08f);
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
                float height = d.top - d.bottom;
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
                panel.transform.localScale = new Vector3(d.width + 0.06f, height, 0.08f);
                panel.GetComponent<Renderer>().sharedMaterial = doorMat;

                var buttons = new List<PressButton>();
                foreach (float side in new[] { 1f, -1f })
                {
                    GameObject b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    b.name = side > 0 ? "Bouton (devant)" : "Bouton (derrière)";
                    b.transform.SetParent(door.transform, false);
                    b.transform.localPosition = new Vector3(-(d.width * 0.5f + 0.3f), 1.1f, 0.2f * side);
                    b.transform.localScale = new Vector3(0.14f, 0.18f, 0.06f);
                    b.GetComponent<Renderer>().sharedMaterial = buttonMat;
                    PressButton pb = b.AddComponent<PressButton>();
                    pb.clickSound = click;
                    buttons.Add(pb);
                }

                LockDoor lockDoor = door.AddComponent<LockDoor>();
                lockDoor.panel = panel.transform;
                lockDoor.width = d.width + 0.06f;
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
            var patrol = new List<Vector3>();
            Vector3 start = Vector3.zero;
            foreach (RoomData r in data.rooms)
            {
                if (r.zone == "SalleDeJeux" || r.zone == "Escalier") continue;
                patrol.Add(P(r.center));
                if (r.name == "Ecole" && r.max.z > 20f) start = P(new Vector3(0f, 0f, 27f));   // elle commence au fond du dortoir
            }
            GameObject monster = MonsterTestBuilder.CreateMonster(start, patrol.ToArray());
            monster.transform.rotation = Quaternion.LookRotation(Dir(Vector3.back));
            monster.AddComponent<MonsterAnimator>();
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
