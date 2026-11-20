using System.Collections.Generic;
using System.IO;
using Horror.Audio;
using Horror.Cosmetics;
using Horror.Interaction;
using Horror.Locomotion;
using Horror.Monsters;
using Horror.Player;
using Horror.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR;

namespace Horror.EditorTools
{
    /// <summary>
    /// Menu "Horror > 5 - Ajouter la Salle de jeux (zone sûre + boutique + sons)" : complète la scène
    /// de test (menus 3 et 4) avec :
    ///  - la Salle de jeux : zone sûre où l'on apparaît et réapparaît, berceuse, le monstre n'y entre pas ;
    ///  - la boutique : 5 cosmétiques sur des présentoirs, boutons à presser du doigt, panneau des pièces ;
    ///  - le joueur : jumpscare, gain de pièces, cosmétiques portés, mains qui appuient, ambiance sonore ;
    ///  - le monstre (s'il est là) : murmures, réaction, cri, pas, clés.
    /// Textes en jeu en ANGLAIS. Rien n'est écrasé : les fichiers existants sont réutilisés. Ctrl+Z annule.
    /// </summary>
    public static class SafeZoneShopBuilder
    {
        private const string Audio = "Assets/Horror/Audio/";
        private const string ItemsFolder = "Assets/Horror/Cosmetics";
        private const string ModelsFolder = "Assets/Horror/Art/Cosmetics/";

        // id, nom (anglais), prix, emplacement
        private static readonly (string id, string name, int price, CosmeticSlot slot)[] Catalog =
        {
            // Prix : voir Docs/ECONOMIE.md (la couronne est l'objet de prestige)
            ("paper_crown", "Paper Crown", 5000, CosmeticSlot.Tete),
            ("party_hat", "Party Hat", 1000, CosmeticSlot.Tete),
            ("ribbon_bow", "Ribbon Bow", 600, CosmeticSlot.Tete),
            ("yarn_scarf", "Yarn Scarf", 1500, CosmeticSlot.Cou),
            ("moon_badge", "Moon Badge", 300, CosmeticSlot.Poitrine),
        };

        [MenuItem("Horror/Tests/Remettre la boîte de crayons à zéro (récolte dispo)")]
        private static void ResetCrayonBox()
        {
            PlayerPrefs.DeleteKey("horror.reward.last");
            PlayerPrefs.Save();
            Debug.Log("[Horror] Boîte de crayons : la récolte est de nouveau disponible.");
        }

        [MenuItem("Horror/5 - Ajouter la Salle de jeux (zone sûre + boutique + sons)")]
        public static void Build()
        {
            ArmLocomotion player = Find<ArmLocomotion>();
            if (player == null)
            {
                EditorUtility.DisplayDialog("Pas de joueur", "Lance d'abord Horror > 3 (salle de test).", "OK");
                return;
            }
            if (Find<SafeZone>() != null)
            {
                EditorUtility.DisplayDialog("Déjà fait", "Il y a déjà une zone sûre dans cette scène.", "OK");
                return;
            }

            Undo.SetCurrentGroupName("Salle de jeux");
            int group = Undo.GetCurrentGroup();

            CosmeticItem[] items = EnsureItems();
            BuildPlayroom(new Vector3(0f, 0f, -5f), items);
            SetupPlayer(player, items);
            BlindMonsterAI monster = Find<BlindMonsterAI>();
            if (monster != null) SetupMonsterVoice(monster);

            Undo.CollapseUndoOperations(group);
            Debug.Log("[Horror] Salle de jeux prête. Play : tu apparais dans la zone sûre, la boutique est contre le mur.");
        }

        // ------------------------------------------------------------------ salle de jeux

        private static void BuildPlayroom(Vector3 center, CosmeticItem[] items)
        {
            var root = new GameObject("Salle de jeux (zone sûre)");
            Undo.RegisterCreatedObjectUndo(root, "Salle de jeux");
            root.transform.position = center;

            SafeZone zone = root.AddComponent<SafeZone>();
            zone.size = new Vector3(9f, 4f, 4.6f);
            var respawn = new GameObject("Point de réapparition");
            respawn.transform.SetParent(root.transform, false);
            respawn.transform.localPosition = new Vector3(0f, 0f, 1.2f);
            zone.respawnPoint = respawn.transform;

            AmbientZone amb = root.AddComponent<AmbientZone>();
            amb.size = zone.size;
            amb.priority = 10;
            amb.isSafe = true;
            amb.loop = Load<AudioClip>(Audio + "SafeZone/lullaby_loop.wav");
            amb.loopVolume = 0.45f;

            // Tapis pastel au sol (repère visuel de la zone sûre)
            GameObject rug = Primitive(PrimitiveType.Cube, root.transform, "Tapis", new Vector3(0f, 0.01f, 0f),
                                       new Vector3(8.6f, 0.02f, 4.2f), new Color(0.93f, 0.82f, 0.86f));
            Object.DestroyImmediate(rug.GetComponent<Collider>());

            Label(root.transform, "PLAYROOM\nsafe zone", new Vector3(0f, 2.6f, -1.95f), 0.06f, new Color(0.95f, 0.9f, 0.82f));
            Label(root.transform, "no microphone in this room", new Vector3(0f, 2.15f, -1.95f), 0.03f, new Color(0.75f, 0.7f, 0.8f));

            GameObject coins = Label(root.transform, GameNames.Currency + ": 100", new Vector3(3.6f, 1.6f, -1.95f), 0.05f, new Color(0.95f, 0.85f, 0.5f));
            coins.AddComponent<CoinDisplay>();

            // Présentoirs de la boutique le long du mur
            AudioClip click = Load<AudioClip>(Audio + "UI/button_click.wav");
            AudioClip coin = Load<AudioClip>(Audio + "UI/coin.wav");
            AudioClip denied = Load<AudioClip>(Audio + "UI/denied.wav");
            for (int i = 0; i < items.Length; i++)
            {
                float x = -3.2f + i * 1.6f;
                var stand = new GameObject("Présentoir " + items[i].displayName);
                stand.transform.SetParent(root.transform, false);
                stand.transform.localPosition = new Vector3(x, 0f, -1.6f);

                Primitive(PrimitiveType.Cylinder, stand.transform, "Socle", new Vector3(0f, 0.35f, 0f),
                          new Vector3(0.5f, 0.35f, 0.5f), new Color(0.75f, 0.85f, 0.9f));
                var anchor = new GameObject("Aperçu");
                anchor.transform.SetParent(stand.transform, false);
                anchor.transform.localPosition = new Vector3(0f, 0.85f, 0f);
                anchor.transform.localScale = Vector3.one * 2f;   // aperçu agrandi pour bien le voir

                GameObject label = Label(stand.transform, items[i].displayName.ToUpper() + "\n" + GameNames.Amount(items[i].price),
                                         new Vector3(0f, 1.45f, 0f), 0.025f, new Color(0.95f, 0.92f, 0.86f));

                GameObject btn = Primitive(PrimitiveType.Cube, stand.transform, "Bouton (appuyer)", new Vector3(0f, 0.62f, 0.3f),
                                           new Vector3(0.14f, 0.14f, 0.06f), new Color(0.95f, 0.6f, 0.7f));
                PressButton press = btn.AddComponent<PressButton>();
                press.clickSound = click;

                ShopStand shop = stand.AddComponent<ShopStand>();
                shop.item = items[i];
                shop.previewAnchor = anchor.transform;
                shop.label = label.GetComponent<TextMesh>();
                shop.button = press;
                shop.buySound = coin;
                shop.deniedSound = denied;
            }

            // Boîte de crayons (100 crayons toutes les 7 h) contre le mur droit, face au centre
            GameObject box = BuildCrayonBox(root.transform, true);
            box.transform.localPosition = new Vector3(4.1f, 0f, 0.4f);
            box.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
        }

        /// <summary>
        /// Station "FREE CRAYONS" : bouton + panneau avec compte à rebours (CrayonBoxReward).
        /// Repère local : le joueur se tient du côté +Z. withVisual = false quand le visuel est déjà
        /// dans la maquette (Institut) : seuls le bouton (invisible) et le texte sont créés.
        /// </summary>
        internal static GameObject BuildCrayonBox(Transform parent, bool withVisual)
        {
            var root = new GameObject("Boîte de crayons (100 toutes les 7 h)");
            root.transform.SetParent(parent, false);

            if (withVisual)
            {
                // Carton jaune un peu de travers + crayons de couleur qui dépassent (pas de cube parfait)
                GameObject carton = Primitive(PrimitiveType.Cube, root.transform, "Carton", new Vector3(0f, 0.4f, 0f),
                                              new Vector3(0.9f, 0.8f, 0.6f), new Color(0.91f, 0.81f, 0.42f));
                carton.transform.localRotation = Quaternion.Euler(0f, 4f, 1.5f);
                Color[] cols = { new Color(0.95f, 0.72f, 0.78f), new Color(0.62f, 0.8f, 0.72f), new Color(0.6f, 0.72f, 0.88f),
                                 new Color(0.95f, 0.85f, 0.5f), new Color(0.85f, 0.62f, 0.72f) };
                for (int k = 0; k < cols.Length; k++)
                {
                    float h = 0.35f + 0.06f * ((k * 7) % 4);
                    GameObject c = Primitive(PrimitiveType.Cylinder, root.transform, "Crayon " + (k + 1),
                                             new Vector3(-0.34f + k * 0.17f, 0.8f + h * 0.5f, -0.05f),
                                             new Vector3(0.1f, h * 0.5f, 0.1f), cols[k]);
                    c.transform.localRotation = Quaternion.Euler(0f, 0f, (k % 2 == 0 ? -1f : 1f) * (3f + k * 1.5f));
                    Object.DestroyImmediate(c.GetComponent<Collider>());
                }
            }

            GameObject btn = Primitive(PrimitiveType.Cube, root.transform, "Bouton récolter", new Vector3(0f, 0.15f, 0.33f),
                                       new Vector3(0.14f, 0.14f, 0.06f), new Color(0.95f, 0.6f, 0.7f));
            if (!withVisual) btn.GetComponent<Renderer>().enabled = false;   // le bouton visible est dans la maquette
            PressButton press = btn.AddComponent<PressButton>();
            press.clickSound = Load<AudioClip>(Audio + "UI/button_click.wav");

            GameObject label = Label(root.transform, "FREE " + GameNames.Currency, new Vector3(0f, 1.75f, 0.1f), 0.02f,
                                     new Color(0.98f, 0.9f, 0.62f));
            CrayonBoxReward reward = root.AddComponent<CrayonBoxReward>();
            reward.button = press;
            reward.label = label.GetComponent<TextMesh>();
            reward.claimSound = Load<AudioClip>(Audio + "UI/coin.wav");
            reward.deniedSound = Load<AudioClip>(Audio + "UI/denied.wav");
            return root;
        }

        // ------------------------------------------------------------------ joueur

        internal static void SetupPlayer(ArmLocomotion player, CosmeticItem[] items)
        {
            GameObject go = player.gameObject;
            Transform head = player.head != null ? player.head : player.transform;

            Jumpscare scare = GetOrAdd<Jumpscare>(go);
            scare.sting = Load<AudioClip>(Audio + "Jumpscare/jumpscare_sting.wav");

            CoinEarner earner = GetOrAdd<CoinEarner>(go);
            earner.coinSound = Load<AudioClip>(Audio + "UI/coin.wav");

            CosmeticWearer wearer = GetOrAdd<CosmeticWearer>(go);
            wearer.catalog = items;
            wearer.head = Anchor(head, "Attache cosmétique Tête", new Vector3(0f, 0.12f, -0.02f));
            wearer.neck = Anchor(head, "Attache cosmétique Cou", new Vector3(0f, -0.2f, 0f));
            wearer.chest = Anchor(head, "Attache cosmétique Poitrine", new Vector3(0.06f, -0.35f, 0.08f));
            wearer.back = Anchor(head, "Attache cosmétique Dos", new Vector3(0f, -0.35f, -0.12f));

            AddPresser(player.leftHandVisual, XRNode.LeftHand);
            AddPresser(player.rightHandVisual, XRNode.RightHand);

            var footsteps = go.GetComponent<Horror.Noise.FootstepNoise>();
            if (footsteps != null && (footsteps.tapClips == null || footsteps.tapClips.Length == 0))
            {
                Undo.RecordObject(footsteps, "Sons de pas");
                footsteps.tapClips = LoadAll(Audio + "Player", "tap_");
            }
            if (footsteps != null && (footsteps.surfaces == null || footsteps.surfaces.Length == 0))
            {
                // Sons de pas par surface : Audio/Steps/<surface>_soft_01.wav, <surface>_hard_01.wav...
                Undo.RecordObject(footsteps, "Sons de pas par surface");
                var list = new List<Horror.Noise.FootstepNoise.SurfaceSounds>();
                foreach (Horror.Noise.SurfaceType st in System.Enum.GetValues(typeof(Horror.Noise.SurfaceType)))
                {
                    string key = st.ToString().ToLowerInvariant();
                    list.Add(new Horror.Noise.FootstepNoise.SurfaceSounds
                    {
                        surface = st,
                        soft = LoadAll(Audio + "Steps", key + "_soft_"),
                        hard = LoadAll(Audio + "Steps", key + "_hard_"),
                    });
                }
                footsteps.surfaces = list.ToArray();
            }

            if (head.GetComponent<AmbientSoundscape>() == null)
            {
                AmbientSoundscape amb = Undo.AddComponent<AmbientSoundscape>(head.gameObject);
                amb.baseLoop = Load<AudioClip>(Audio + "Ambience/drone_loop.wav");
                var shots = new List<AudioClip>();
                shots.AddRange(LoadAll(Audio + "Ambience", "creak_"));
                shots.AddRange(LoadAll(Audio + "Ambience", "drip_"));
                shots.Add(Load<AudioClip>(Audio + "Ambience/music_box_far.wav"));
                shots.Add(Load<AudioClip>(Audio + "Ambience/child_giggle_far.wav"));
                amb.defaultOneShots = shots.FindAll(c => c != null).ToArray();
                amb.heartbeatLoop = Load<AudioClip>(Audio + "Jumpscare/heartbeat_loop.wav");
            }

            if (head.GetComponent<FearDirector>() == null)
            {
                // La tension : coeur dans les manettes, souffle, silence quand elle rôde, fausses alertes
                FearDirector fear = Undo.AddComponent<FearDirector>(head.gameObject);
                fear.breathLoop = Load<AudioClip>(Audio + "Player/breath_loop.wav");
                fear.stepsBehind = LoadAll(Audio + "Monster", "step_");
                var whispers = new List<AudioClip>(LoadAll(Audio + "World", "door_voice_"));
                whispers.AddRange(LoadAll(Audio + "Monster", "whisper_"));
                fear.earWhispers = whispers.ToArray();
                fear.farSounds = new[] { Load<AudioClip>(Audio + "Ambience/child_giggle_far.wav"),
                                         Load<AudioClip>(Audio + "Ambience/music_box_far.wav") };
                fear.doorSlam = Load<AudioClip>(Audio + "Doors/door_lock.wav");
            }
        }

        private static void AddPresser(Transform hand, XRNode node)
        {
            if (hand == null || hand.GetComponent<HandPresser>() != null) return;
            HandPresser p = Undo.AddComponent<HandPresser>(hand.gameObject);
            p.node = node;
        }

        // ------------------------------------------------------------------ monstre

        public static void SetupMonsterVoice(BlindMonsterAI monster)
        {
            if (monster.GetComponent<MonsterVoice>() != null) return;
            MonsterVoice voice = Undo.AddComponent<MonsterVoice>(monster.gameObject);
            voice.whispers = LoadAll(Audio + "Monster", "whisper_");
            voice.hearReactions = LoadAll(Audio + "Monster", "hear_react_");
            voice.chaseScream = Load<AudioClip>(Audio + "Monster/scream_chase.wav");
            voice.footsteps = LoadAll(Audio + "Monster", "step_");
            voice.keysLoop = Load<AudioClip>(Audio + "Monster/keys_loop.wav");
        }

        // ------------------------------------------------------------------ cosmétiques (fichiers)

        internal static CosmeticItem[] EnsureItems()
        {
            if (!AssetDatabase.IsValidFolder(ItemsFolder))
                AssetDatabase.CreateFolder("Assets/Horror", "Cosmetics");
            var result = new CosmeticItem[Catalog.Length];
            for (int i = 0; i < Catalog.Length; i++)
            {
                var c = Catalog[i];
                string path = ItemsFolder + "/" + c.id + ".asset";
                var item = AssetDatabase.LoadAssetAtPath<CosmeticItem>(path);
                if (item == null)   // on ne remplace jamais un fichier existant (tu as peut-être changé le prix)
                {
                    item = ScriptableObject.CreateInstance<CosmeticItem>();
                    item.id = c.id;
                    item.displayName = c.name;
                    item.price = c.price;
                    item.slot = c.slot;
                    item.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelsFolder + c.id + ".glb");
                    AssetDatabase.CreateAsset(item, path);
                }
                result[i] = item;
            }
            AssetDatabase.SaveAssets();
            return result;
        }

        // ------------------------------------------------------------------ utilitaires

        internal static GameObject Label(Transform parent, string text, Vector3 localPos, float charSize, Color color)
        {
            var go = new GameObject("Texte : " + text.Split('\n')[0]);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // lisible depuis le centre de la pièce
            TextMesh tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.characterSize = charSize;
            tm.fontSize = 64;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
#if UNITY_2022_2_OR_NEWER
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
            Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
            if (font != null)
            {
                tm.font = font;
                go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            return go;
        }

        private static GameObject Primitive(PrimitiveType type, Transform parent, string name, Vector3 localPos, Vector3 scale, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null || UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null) shader = Shader.Find("Standard");
            var mat = new Material(shader) { color = color };
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        private static Transform Anchor(Transform parent, string name, Vector3 localPos)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing;
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go.transform;
        }

        private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);

        // Attention : dans l'éditeur, GetComponent renvoie un "faux null" quand le composant manque,
        // donc on teste avec == null (et pas avec ??).
        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T existing = go.GetComponent<T>();
            return existing != null ? existing : Undo.AddComponent<T>(go);
        }

        private static AudioClip[] LoadAll(string folder, string prefix)
        {
            var list = new List<AudioClip>();
            if (!Directory.Exists(folder)) return list.ToArray();
            foreach (string file in Directory.GetFiles(folder, prefix + "*.wav"))
            {
                AudioClip clip = Load<AudioClip>(file.Replace('\\', '/'));
                if (clip != null) list.Add(clip);
            }
            list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return list.ToArray();
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
