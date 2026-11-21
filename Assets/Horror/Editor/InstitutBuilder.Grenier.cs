using System.Collections.Generic;
using Horror.Audio;
using Horror.Interaction;
using Horror.Lore;
using Horror.Monsters;
using Horror.World;
using UnityEditor;
using UnityEngine;

namespace Horror.EditorTools
{
    /// <summary>
    /// Le Grenier, au-dessus du grand dortoir : le domaine de La Veilleuse, la surveillante de nuit.
    /// On y monte par un monte-charge au fond du dortoir. Plancher, charpente, caisses, vieux lits, poupées.
    /// Mécanique : des ampoules nues à cordon. Allumées, leur halo est un refuge (elle ne peut pas y entrer) ;
    /// elle les souffle avec son éteignoir (grésillement d'avertissement), il faut tirer les cordons pour les rallumer.
    /// Le lore s'y découvre autrement que par les papiers : une cassette, un projecteur de diapositives, des écrits.
    /// Coordonnées : repère du grenier (origine au centre du plancher, axes du monde).
    /// </summary>
    public static partial class InstitutBuilder
    {
        private const float GW = 16f, GL = 22f, GKnee = 1.4f, GRidge = 5.6f, GY = 14f;
        private const string GDir = "Assets/Horror/Art/Map/Grenier/";
        private static Vector3 _gO;

        /// <summary>Point du repère du grenier -> monde.</summary>
        private static Vector3 G(float x, float y, float z) => _gO + new Vector3(x, y, z);
        /// <summary>Hauteur du dessous du toit à cette abscisse (toit à deux pentes, faîte au milieu).</summary>
        private static float GRoofY(float x) => GRidge - (GRidge - GKnee) * Mathf.Abs(x) / (GW * 0.5f);

        private static Bounds GrenierBounds() => new Bounds(_gO + new Vector3(0f, GRidge * 0.5f, 0f), new Vector3(GW, GRidge + 0.6f, GL));

        private static void SetupGrenier(MapData data)
        {
            RoomData dorm = null;
            foreach (RoomData r in data.rooms)
                if (r.zone == "Ecole" && r.max.z - r.min.z >= 12f) { dorm = r; break; }
            if (dorm == null) { Debug.LogWarning("[Horror] Grenier : grand dortoir introuvable, grenier non construit."); return; }
            Vector3 c = P(dorm.center);
            _gO = new Vector3(c.x, GY, c.z);

            var root = new GameObject("Grenier (décor)");
            BuildGrenierShell(root.transform);
            FurnishGrenier(root.transform);
            var lamps = new GameObject("Grenier (ampoules)");
            SetupGrenierBulbs(lamps.transform);
            var lore = new GameObject("Grenier (lore : cassettes, diapositives)");
            SetupGrenierLore(lore.transform, root.transform);
            SetupGrenierAccess(lore.transform, dorm);
            SetupVeilleuse();

            Bounds b = GrenierBounds();
            var zone = new GameObject("Territoire de la Veilleuse (grenier)");
            zone.transform.position = b.center;
            Territory t = zone.AddComponent<Territory>();
            t.owner = "Veilleuse"; t.size = b.size;
            AmbientZone az = zone.AddComponent<AmbientZone>();
            az.size = b.size; az.priority = 6;
            az.loop = Load<AudioClip>(Audio + "Grenier/grenier_ambiance.wav"); az.loopVolume = 0.4f;
            az.oneShots = LoadMany(Audio + "Grenier", "plancher");
            az.oneShotInterval = new Vector2(10f, 22f);
            Debug.Log("[Horror] Grenier construit (au-dessus du dortoir, monte-charge au fond du dortoir).");
        }

        // ------------------------------------------------------------------ matériaux et boîtes

        private static Material GMat(string name, string tex, Vector2 tiling, Color tint)
        {
            EnsureFolder("Assets/Horror/Art/Materials");
            string path = "Assets/Horror/Art/Materials/Grenier_" + name + ".mat";
            var mat = Load<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Grenier_" + name };
                AssetDatabase.CreateAsset(mat, path);
            }
            var t = tex != null ? Load<Texture2D>(GDir + tex) : null;
            if (t != null) { mat.SetTexture("_BaseMap", t); mat.mainTexture = t; mat.SetTextureScale("_BaseMap", tiling); }
            mat.SetFloat("_Smoothness", 0.12f);
            mat.color = tint;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material GGlow(string name, Color baseColor, Color emission)
        {
            Material m = GMat(name, null, Vector2.one, baseColor);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.SetColor("_EmissionColor", emission);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static GameObject GBox(Transform parent, string name, Vector3 center, Vector3 size, Quaternion rot, Material m, bool solid = true)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            b.name = name;
            b.transform.SetParent(parent, false);
            b.transform.SetPositionAndRotation(center, rot);
            b.transform.localScale = size;
            b.GetComponent<Renderer>().sharedMaterial = m;
            if (!solid) Object.DestroyImmediate(b.GetComponent<Collider>());
            return b;
        }

        private static GameObject GCyl(Transform parent, string name, Vector3 center, Vector3 scale, Quaternion rot, Material m)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            b.name = name;
            Object.DestroyImmediate(b.GetComponent<Collider>());
            b.transform.SetParent(parent, false);
            b.transform.SetPositionAndRotation(center, rot);
            b.transform.localScale = scale;
            b.GetComponent<Renderer>().sharedMaterial = m;
            return b;
        }

        // ------------------------------------------------------------------ plancher, murs, toit, charpente

        private static void BuildGrenierShell(Transform root)
        {
            Material floor = GMat("Plancher", "grenier_plancher.png", new Vector2(4f, 6f), Color.white);
            Material wall = GMat("Mur", "grenier_mur.png", new Vector2(4f, 1.5f), Color.white);
            Material roof = GMat("Toit", "grenier_mur.png", new Vector2(6f, 3f), new Color(0.8f, 0.76f, 0.72f));
            Material beam = GMat("Poutre", "grenier_poutre.png", new Vector2(1f, 1f), new Color(0.85f, 0.8f, 0.75f));
            float hw = GW * 0.5f, hl = GL * 0.5f;

            // en dalles : chaque morceau ne reçoit que les ampoules proches (URP limite les lumières par objet)
            const int NX = 4, NZ = 4;
            float tx = (GW + 0.4f) / NX, tz = (GL + 0.4f) / NZ;
            for (int i = 0; i < NX; i++)
                for (int k = 0; k < NZ; k++)
                    GBox(root, "Plancher", G(-hw - 0.2f + tx * (i + 0.5f), -0.15f, -hl - 0.2f + tz * (k + 0.5f)), new Vector3(tx, 0.3f, tz), Quaternion.identity, floor);
            for (int k = 0; k < NZ; k++)
            {
                float zc = -hl + GL / NZ * (k + 0.5f);
                GBox(root, "Mur bas (droite)", G(hw + 0.1f, GKnee * 0.5f, zc), new Vector3(0.2f, GKnee, GL / NZ), Quaternion.identity, wall);
                GBox(root, "Mur bas (gauche)", G(-hw - 0.1f, GKnee * 0.5f, zc), new Vector3(0.2f, GKnee, GL / NZ), Quaternion.identity, wall);
            }
            GBox(root, "Pignon (dortoir)", G(0f, GRidge * 0.5f, -hl - 0.1f), new Vector3(GW + 0.4f, GRidge + 0.4f, 0.2f), Quaternion.identity, wall);
            GBox(root, "Pignon (lune)", G(0f, GRidge * 0.5f, hl + 0.1f), new Vector3(GW + 0.4f, GRidge + 0.4f, 0.2f), Quaternion.identity, wall);

            // les deux pentes du toit (le dessous passe par le haut des murs bas et le faîte)
            float run = hw, rise = GRidge - GKnee, len = Mathf.Sqrt(run * run + rise * rise) + 0.5f;
            float ang = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
            foreach (float side in new[] { -1f, 1f })
            {
                Quaternion rot = Quaternion.Euler(0f, 0f, side > 0 ? -ang : ang);
                Vector3 normalIn = rot * Vector3.down;                                   // vers l'intérieur
                for (int k = 0; k < NZ; k++)
                {
                    float zc = -hl - 0.2f + tz * (k + 0.5f);
                    Vector3 mid = G(side * run * 0.5f, (GRidge + GKnee) * 0.5f, zc) - normalIn * 0.12f;
                    GBox(root, side > 0 ? "Toit (droite)" : "Toit (gauche)", mid, new Vector3(len, 0.2f, tz), rot, roof);
                }
                // chevrons tous les 2 m, sous la pente (on peut s'y accrocher)
                for (float z = -hl + 1f; z <= hl - 0.9f; z += 2f)
                {
                    Vector3 m2 = G(side * run * 0.5f, (GRidge + GKnee) * 0.5f, z) + normalIn * 0.1f;
                    GBox(root, "Chevron", m2, new Vector3(len - 0.4f, 0.16f, 0.12f), rot, beam);
                }
            }
            GBox(root, "Faîtage", G(0f, GRidge - 0.15f, 0f), new Vector3(0.2f, 0.22f, GL), Quaternion.identity, beam);
            foreach (float side in new[] { -1f, 1f })                                     // pannes
            {
                float x = side * hw * 0.5f;
                GBox(root, "Panne", G(x, GRoofY(x) - 0.2f, 0f), new Vector3(0.14f, 0.18f, GL), Quaternion.identity, beam);
            }
            // poteaux (on peut se cacher derrière, mais elle aussi)
            foreach (Vector2 p in new[] { new Vector2(0f, -3.2f), new Vector2(0f, 1.2f) })
                GBox(root, "Poteau", G(p.x, GRidge * 0.5f, p.y), new Vector3(0.2f, GRidge, 0.2f), Quaternion.identity, beam);
        }

        // ------------------------------------------------------------------ mobilier : caisses, lits, poupées

        private static GameObject GCrate(Transform parent, float x, float z, float size, float yaw, float y = 0f)
        {
            Material m = GMat("Caisse", "grenier_poutre.png", new Vector2(1f, 1f), new Color(0.78f, 0.7f, 0.6f));
            return GBox(parent, "Caisse", G(x, y + size * 0.5f, z), Vector3.one * size, Quaternion.Euler(0f, yaw, 0f), m);
        }

        private static GameObject GPut(Transform parent, string path, Vector3 pos, float yaw, float scale, bool solid = true)
        {
            var proto = Load<GameObject>(path);
            if (proto == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(proto);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = Vector3.one * scale;
            if (solid) AddBoundsCollider(go.transform, go.transform, false);
            return go;
        }

        private static void FurnishGrenier(Transform p)
        {
            float hl = GL * 0.5f;
            // caisses, parfois empilées, surtout le long des murs bas (on s'y cache)
            float[,] crates =
            {
                { -6.2f, -8.2f, 1.0f, 10f, 0f }, { -6.4f, -8.0f, 0.6f, 35f, 1.0f }, { 6.1f, -7.6f, 0.9f, -20f, 0f },
                { 6.6f, -1.6f, 0.7f, 5f, 0f }, { -6.5f, 0.6f, 0.7f, -12f, 0f }, { -2.3f, -2.0f, 0.8f, 28f, 0f },
                { 2.6f, 2.0f, 0.9f, -8f, 0f }, { 6.2f, 6.0f, 0.7f, 18f, 0f }, { -6.3f, 9.8f, 0.8f, 4f, 0f },
                { 3.2f, 9.6f, 1.0f, -30f, 0f }, { 3.4f, 9.4f, 0.6f, -5f, 1.0f }, { -3.0f, 6.4f, 0.6f, 40f, 0f },
                { 0f, 5.0f, 0.75f, 0f, 0f },                                            // sous le projecteur
            };
            for (int i = 0; i < crates.GetLength(0); i++)
                GCrate(p, crates[i, 0], crates[i, 1], crates[i, 2], crates[i, 3], crates[i, 4]);

            // deux vieux lits d'infirmerie sous les pentes, et quelques jouets oubliés
            GPut(p, HDir + "LitHopital.glb", G(-6.2f, 0f, -3.6f), 0f, 1.15f);
            GPut(p, HDir + "LitHopital.glb", G(6.2f, 0f, 2.8f), 180f, 1.15f);
            GPut(p, "Assets/Horror/Art/Map/Decor/Lapin.glb", G(-2.3f, 0.8f, -2.0f), 150f, 1.2f, false);
            GPut(p, "Assets/Horror/Art/Map/Decor/Cubes.glb", G(1.4f, 0f, -6.8f), 20f, 1.2f, false);
            GPut(p, "Assets/Horror/Art/Map/Decor/BoiteAMusique.glb", G(3.4f, 1.6f, 9.4f), 200f, 1.2f, false);

            // les poupées des enfants du dortoir, assises sur les caisses, la tête penchée (rien ne bouge... ici)
            var proto = Load<GameObject>("Assets/Horror/Art/Character/LeDessine_PNJ.glb");
            if (proto != null)
            {
                Vector3[] seats = { G(6.6f, 0.7f, -1.6f), G(-6.5f, 0.7f, 0.6f), G(6.2f, 0.7f, 6.0f) };
                for (int i = 0; i < seats.Length; i++)
                {
                    var doll = (GameObject)PrefabUtility.InstantiatePrefab(proto);
                    doll.name = "Poupée du grenier " + (i + 1);
                    doll.transform.SetParent(p, false);
                    Vector3 face = new Vector3(-Mathf.Sign(seats[i].x - _gO.x), 0f, 0f);
                    doll.transform.SetPositionAndRotation(seats[i] - Vector3.up * 0.19f, Quaternion.LookRotation(face, Vector3.up) * Quaternion.Euler(0f, i * 17f - 15f, 0f));
                    Transform head = FindChild(doll.transform, "Tete");
                    if (head != null) head.localRotation = Quaternion.Euler(12f, 0f, (i - 1) * 22f);
                    foreach (string s in new[] { "G", "D" })
                    {
                        Transform ep = FindChild(doll.transform, "Epaule_" + s);
                        if (ep != null) ep.localRotation = Quaternion.Euler(-20f, 0f, s == "G" ? 10f : -10f);
                    }
                }
            }

            // la lucarne ronde du pignon : un clair de lune bleuté (la seule lumière qu'elle ne peut pas souffler)
            Material moon = GGlow("Lune", new Color(0.6f, 0.68f, 0.85f), new Color(0.45f, 0.55f, 0.85f));
            GCyl(p, "Lucarne", G(0f, 4.3f, hl - 0.02f), new Vector3(0.9f, 0.02f, 0.9f), Quaternion.Euler(90f, 0f, 0f), moon);
            var ml = new GameObject("Clair de lune");
            ml.transform.SetParent(p, false);
            ml.transform.SetPositionAndRotation(G(0f, 4.2f, hl - 0.4f), Quaternion.LookRotation(new Vector3(0f, -0.55f, -1f)));
            Light l = ml.AddComponent<Light>();
            l.type = LightType.Spot; l.spotAngle = 55f; l.range = 14f; l.intensity = 1.1f; l.color = new Color(0.5f, 0.6f, 0.95f); l.shadows = LightShadows.None;
        }

        // ------------------------------------------------------------------ les ampoules à cordon

        private static AtticBulb GBulb(Transform parent, float x, float z, bool lit, bool snuffable, float radius, float y = -1f, float wireTop = -1f)
        {
            float roofY = wireTop > 0f ? wireTop : GRoofY(x);
            if (y < 0f) y = Mathf.Min(2.7f, roofY - 0.6f);
            var go = new GameObject(snuffable ? "Ampoule à cordon" : "Ampoule de la cage (refuge)");
            go.transform.SetParent(parent, false);
            go.transform.position = G(x, y, z);
            Material black = MakeMat("Grenier_Fil", new Color(0.05f, 0.05f, 0.05f));
            Material brass = MakeMat("Grenier_Laiton", new Color(0.55f, 0.43f, 0.2f));
            Material glass = GGlow("Ampoule", new Color(1f, 0.9f, 0.7f), new Color(1f, 0.72f, 0.38f) * 6f);
            float wire = roofY - y;
            GCyl(go.transform, "Fil", G(x, y + wire * 0.5f, z), new Vector3(0.012f, wire * 0.5f, 0.012f), Quaternion.identity, black);
            GCyl(go.transform, "Douille", G(x, y + 0.09f, z), new Vector3(0.05f, 0.04f, 0.05f), Quaternion.identity, brass);
            var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bulb.name = "Verre"; Object.DestroyImmediate(bulb.GetComponent<Collider>());
            bulb.transform.SetParent(go.transform, false);
            bulb.transform.position = G(x, y, z);
            bulb.transform.localScale = new Vector3(0.13f, 0.17f, 0.13f);
            bulb.GetComponent<Renderer>().sharedMaterial = glass;

            Light l = go.AddComponent<Light>();
            l.type = LightType.Point; l.range = radius * 2.1f; l.intensity = 7f; l.color = new Color(1f, 0.78f, 0.5f); l.shadows = LightShadows.None;

            // le cordon : un fil qui pend jusqu'à hauteur de main, avec une perle au bout (la toucher = tirer)
            const float knobY = 1.45f;
            float cord = y - 0.06f - knobY;
            GCyl(go.transform, "Cordon", G(x + 0.05f, knobY + cord * 0.5f, z), new Vector3(0.006f, cord * 0.5f, 0.006f), Quaternion.identity, MakeMat("Grenier_Cordon", new Color(0.75f, 0.7f, 0.6f)));
            var knob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            knob.name = "Perle du cordon (la toucher)";
            knob.transform.SetParent(go.transform, false);
            knob.transform.position = G(x + 0.05f, knobY, z);
            knob.transform.localScale = Vector3.one * 0.06f;
            knob.GetComponent<Renderer>().sharedMaterial = brass;
            ((SphereCollider)knob.GetComponent<Collider>()).radius = 1.6f;              // plus facile à attraper
            PressButton pb = knob.AddComponent<PressButton>();
            pb.pressDepth = 0f; pb.cooldown = 0.8f;
            pb.clickSound = Load<AudioClip>(Audio + "UI/button_click.wav");

            AtticBulb ab = go.AddComponent<AtticBulb>();
            ab.bulbLight = l; ab.glass = bulb.GetComponent<Renderer>(); ab.cord = pb;
            ab.radius = radius; ab.startLit = lit; ab.snuffable = snuffable;
            const string A = Audio + "Grenier/";
            ab.snuffSound = Load<AudioClip>(A + "ampoule_souffle.wav");
            ab.warnSound = Load<AudioClip>(A + "ampoule_gresille.wav");
            ab.relightSound = Load<AudioClip>(A + "ampoule_rallume.wav");
            ab.humLoop = Load<AudioClip>(A + "ampoule_bourdon.wav");
            return ab;
        }

        private static void SetupGrenierBulbs(Transform p)
        {
            // (x, z, allumée au départ) : des halos qui ne se touchent pas, des trous noirs entre eux
            float[,] bulbs =
            {
                { -4f, -5.5f, 1f }, { 4f, -5.5f, 1f }, { 0f, -0.5f, 0f },
                { -5f, 3.5f, 1f }, { 5f, 3.5f, 0f }, { -4.5f, 8.5f, 0f }, { 4.5f, 8.5f, 1f },
            };
            for (int i = 0; i < bulbs.GetLength(0); i++)
                GBulb(p, bulbs[i, 0], bulbs[i, 1], bulbs[i, 2] > 0.5f, true, 2.2f);
            // la lampe de la cage d'arrivée : elle ne peut pas la souffler (le seul vrai refuge)
            GBulb(p, 0f, -GL * 0.5f + 1.6f, true, false, 1.6f, 2.6f, 3.15f);
        }

        // ------------------------------------------------------------------ lore : cassettes, projecteur, écrits

        private static readonly string[] TapeLines1 =
        {
            "Night log. November the second.", "Lights out at nine, as Madam wants.", "Number twelve cried again.",
            "I told her: the dark is kind.", "The dark keeps you quiet.", "If you see the light, you are awake.",
            "And awake children... get tested.",
        };
        private static readonly string[] TapeLines2 =
        {
            "If somebody hears this.", "She is coming up the attic stairs.", "I can hear the brass bell.",
            "She puts the bulbs out, one by one.", "Don't let them all go out.", "Pull the strings. Pull the strings!",
        };
        private static readonly string[] TapeLines3 =
        {
            "Dictation.", "Subject twenty three shows", "no fear response in the dark.", "Recommend transfer",
            "to the attic programme.", "The night monitor will watch her.", "She always keeps them... calm.",
        };

        /// <summary>Un magnétophone posé à <paramref name="pos"/>, tourné vers <paramref name="viewer"/>.</summary>
        private static LoreTape MakeTape(Transform parent, string id, string label, string clip, string[] lines, Vector3 pos, Vector3 viewer)
        {
            Vector3 f = viewer - pos; f.y = 0f;
            if (f.sqrMagnitude < 0.01f) f = Vector3.back;
            var root = new GameObject("Magnétophone (" + label + ")");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(-f.normalized, Vector3.up));   // l'avant (-Z) vers le joueur
            Material body = MakeMat("Grenier_Magneto", new Color(0.16f, 0.15f, 0.14f));
            Material dark = MakeMat("Grenier_Bobine", new Color(0.03f, 0.03f, 0.03f));
            Material silver = MakeMat("Grenier_Alu", new Color(0.62f, 0.62f, 0.6f));
            Transform t = root.transform;
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            b.name = "Boîtier"; b.transform.SetParent(t, false);
            b.transform.localPosition = new Vector3(0f, 0.06f, 0f); b.transform.localScale = new Vector3(0.34f, 0.12f, 0.22f);
            b.GetComponent<Renderer>().sharedMaterial = body;
            foreach (float sx in new[] { -0.08f, 0.08f })
            {
                var reel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                reel.name = "Bobine"; Object.DestroyImmediate(reel.GetComponent<Collider>());
                reel.transform.SetParent(t, false);
                reel.transform.localPosition = new Vector3(sx, 0.125f, 0.03f); reel.transform.localScale = new Vector3(0.085f, 0.006f, 0.085f);
                reel.GetComponent<Renderer>().sharedMaterial = dark;
            }
            var grille = GameObject.CreatePrimitive(PrimitiveType.Cube);
            grille.name = "Haut-parleur"; Object.DestroyImmediate(grille.GetComponent<Collider>());
            grille.transform.SetParent(t, false);
            grille.transform.localPosition = new Vector3(-0.06f, 0.06f, -0.111f); grille.transform.localScale = new Vector3(0.18f, 0.08f, 0.004f);
            grille.GetComponent<Renderer>().sharedMaterial = silver;
            var btn = GameObject.CreatePrimitive(PrimitiveType.Cube);
            btn.name = "Touche PLAY";
            btn.transform.SetParent(t, false);
            btn.transform.localPosition = new Vector3(0.11f, 0.13f, -0.075f); btn.transform.localScale = new Vector3(0.06f, 0.03f, 0.045f);
            btn.GetComponent<Renderer>().sharedMaterial = MakeMat("Lobby_Bouton", new Color(0.95f, 0.62f, 0.72f));
            PressButton pb = btn.AddComponent<PressButton>();
            pb.cooldown = 0.6f;
            GameObject sub = SafeZoneShopBuilder.Label(t, label + "\n[PLAY]", new Vector3(0f, 0.42f, 0f), 0.011f, new Color(0.95f, 0.92f, 0.85f));
            sub.transform.localRotation = Quaternion.identity;                              // lisible depuis l'avant
            LoreTape lt = root.AddComponent<LoreTape>();
            lt.id = id; lt.label = label; lt.lines = lines; lt.playButton = pb;
            lt.subtitle = sub.GetComponent<TextMesh>();
            lt.clip = Load<AudioClip>(Audio + "Lore/" + clip + ".wav");
            lt.clickSound = Load<AudioClip>(Audio + "UI/button_click.wav");
            return lt;
        }

        /// <summary>Une caisse au sol à <paramref name="floor"/>, un magnétophone dessus.</summary>
        private static void TapeOnCrate(Transform parent, string id, string label, string clip, string[] lines, Vector3 floor, Vector3 viewer)
        {
            Material m = GMat("Caisse", "grenier_poutre.png", new Vector2(1f, 1f), new Color(0.78f, 0.7f, 0.6f));
            Vector3 f = viewer - floor; f.y = 0f;
            GBox(parent, "Caisse (magnétophone)", floor + Vector3.up * 0.3f, new Vector3(0.55f, 0.6f, 0.45f),
                 f.sqrMagnitude > 0.01f ? Quaternion.LookRotation(f.normalized) : Quaternion.identity, m);
            MakeTape(parent, id, label, clip, lines, floor + Vector3.up * 0.6f, viewer);
        }

        private static void GBoard(Transform parent, string text, Vector3 pos, Vector3 normal, float size, bool paper, Color ink)
        {
            Quaternion rot = Quaternion.LookRotation(-normal, Vector3.up);
            if (paper)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.name = "Feuille"; Object.DestroyImmediate(q.GetComponent<Collider>());
                q.transform.SetParent(parent, false);
                q.transform.SetPositionAndRotation(pos + normal * 0.01f, rot);
                q.transform.localScale = new Vector3(0.75f, 0.6f, 1f);
                q.GetComponent<Renderer>().sharedMaterial = MakeMat("Hopital_Fiche", new Color(0.93f, 0.9f, 0.82f));
            }
            GameObject t = SafeZoneShopBuilder.Label(parent, text, Vector3.zero, size, ink);
            t.transform.SetPositionAndRotation(pos + normal * 0.025f, rot);
        }

        private static void SetupGrenierLore(Transform p, Transform decor)
        {
            float hw = GW * 0.5f, hl = GL * 0.5f;
            Color ink = new Color(0.16f, 0.14f, 0.2f), scrawl = new Color(0.45f, 0.1f, 0.12f);

            // cassette 2 (la patiente cachée), dans le coin du fond, sous une ampoule éteinte
            MakeTape(p, "cassette_2", "TAPE 2", "cassette_2_patiente", TapeLines2, G(-6.3f, 0.8f, 9.8f), G(-3f, 0f, 6f));

            // le règlement de nuit, près de la cage d'arrivée
            GBoard(p, "NIGHT RULES\n\n1. Lights out at nine.\n2. No crying after the bell.\n3. Children who are awake\n    are sent downstairs.\n4. Madam Night keeps the bell.",
                   G(2.8f, 1.7f, -hl + 0.01f), Vector3.forward, 0.011f, true, ink);
            GBoard(p, "PULL THE STRINGS", G(hw - 0.01f, 0.85f, 1.2f), Vector3.left, 0.04f, false, scrawl);
            GBoard(p, "SHE HATES THE LIGHT", G(-hw + 0.01f, 0.85f, -1.5f), Vector3.right, 0.04f, false, scrawl);
            GBoard(p, "she counts the bulbs\nIIII IIII II", G(-hw + 0.01f, 0.6f, 6.5f), Vector3.right, 0.025f, false, scrawl);
            GBoard(p, "dont let them\nall go out", G(hw - 0.01f, 0.7f, -6.8f), Vector3.left, 0.025f, false, scrawl);

            // le projecteur de diapositives, sur sa caisse, tourné vers le drap tendu au pignon
            Material sheetMat = GMat("Ecran", "grenier_drap.png", Vector2.one, Color.white);
            sheetMat.EnableKeyword("_EMISSION");
            var screen = GameObject.CreatePrimitive(PrimitiveType.Quad);
            screen.name = "Drap tendu (écran)"; Object.DestroyImmediate(screen.GetComponent<Collider>());
            screen.transform.SetParent(p, false);
            screen.transform.SetPositionAndRotation(G(0f, 2.15f, hl - 0.06f), Quaternion.identity);   // face visible vers -Z (la pièce)
            screen.transform.localScale = new Vector3(2.4f, 1.8f, 1f);
            screen.GetComponent<Renderer>().sharedMaterial = sheetMat;

            var proj = new GameObject("Projecteur de diapositives");
            proj.transform.SetParent(p, false);
            proj.transform.SetPositionAndRotation(G(0f, 0.75f, 5f), Quaternion.identity);
            Material body = MakeMat("Grenier_Projecteur", new Color(0.22f, 0.2f, 0.17f));
            GBox(proj.transform, "Boîtier", G(0f, 0.87f, 5f), new Vector3(0.3f, 0.22f, 0.36f), Quaternion.identity, body);
            GCyl(proj.transform, "Objectif", G(0f, 0.9f, 5.24f), new Vector3(0.09f, 0.07f, 0.09f), Quaternion.Euler(90f, 0f, 0f), MakeMat("Grenier_Bobine", new Color(0.03f, 0.03f, 0.03f)));
            GCyl(proj.transform, "Panier à diapos", G(0f, 1.0f, 4.98f), new Vector3(0.22f, 0.03f, 0.22f), Quaternion.identity, MakeMat("Grenier_Alu", new Color(0.62f, 0.62f, 0.6f)));
            GameObject btn = GBox(proj.transform, "Bouton (diapo suivante)", G(0.19f, 0.9f, 4.95f), new Vector3(0.05f, 0.05f, 0.05f), Quaternion.identity,
                                  MakeMat("Lobby_Bouton", new Color(0.95f, 0.62f, 0.72f)));
            PressButton pb = btn.AddComponent<PressButton>();
            pb.cooldown = 0.6f;
            var beamGo = new GameObject("Faisceau du projecteur");
            beamGo.transform.SetParent(proj.transform, false);
            beamGo.transform.SetPositionAndRotation(G(0f, 0.9f, 5.3f), Quaternion.LookRotation(G(0f, 2.15f, hl) - G(0f, 0.9f, 5.3f)));
            Light beam = beamGo.AddComponent<Light>();
            beam.type = LightType.Spot; beam.spotAngle = 26f; beam.range = 8f; beam.intensity = 2.2f; beam.color = new Color(1f, 0.95f, 0.82f); beam.shadows = LightShadows.None;

            GameObject cap = SafeZoneShopBuilder.Label(p, "SLIDES  [PRESS]", Vector3.zero, 0.016f, new Color(0.92f, 0.9f, 0.84f));
            cap.transform.SetPositionAndRotation(G(0f, 1.0f, hl - 0.08f), Quaternion.identity);
            SlideProjector sp = proj.AddComponent<SlideProjector>();
            const string D = "Assets/Horror/Art/Drawings/";
            sp.slides = new[] { Load<Texture2D>(D + "drawing_lady.png"), Load<Texture2D>(D + "drawing_friends.png"),
                                Load<Texture2D>(D + "drawing_family.png"), Load<Texture2D>(D + "drawing_pool.png") };
            sp.captions = new[]
            {
                "MADAM NIGHT, OUR NIGHT MONITOR.\n\"Lights out at nine.\"",
                "DORMITORY, 1974.\nThe children who slept well.",
                "VISITING DAY.\nNobody came for number 12.",
                "THE ONES WHO WOKE UP\nWERE SENT DOWNSTAIRS.",
            };
            sp.screen = screen.GetComponent<Renderer>(); sp.caption = cap.GetComponent<TextMesh>();
            sp.button = pb; sp.beam = beam;
            sp.clack = Load<AudioClip>(Audio + "UI/button_click.wav");

            // cassette 3 (la dictée du Docteur) : à l'infirmerie, aux archives
            Physics.SyncTransforms();
            Vector3 archives = new Vector3(21f, HFloor, -73f);
            Vector3 center = new Vector3(21f, HFloor, -75.5f);
            if (Physics.Raycast(archives + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 4f, ~0, QueryTriggerInteraction.Ignore) && hit.point.y > HFloor + 0.3f)
                MakeTape(p, "cassette_3", "TAPE 3", "cassette_3_docteur", TapeLines3, hit.point, center);
            else
                TapeOnCrate(p, "cassette_3", "TAPE 3", "cassette_3_docteur", TapeLines3, archives, center);
        }

        // ------------------------------------------------------------------ accès : monte-charge au fond du dortoir

        private static void SetupGrenierAccess(Transform parent, RoomData dorm)
        {
            Physics.SyncTransforms();
            Vector3 toEntrance = Flat(Dir(new Vector3(0f, 0f, -1f))).normalized;
            Vector3 start = P(new Vector3(dorm.center.x, 0f, dorm.max.z - 1.6f));
            Vector3 floor = new Vector3(start.x, P(dorm.min).y, start.z);
            bool found = false;
            // sur le côté du fond (l'Écoute du haut démarre au milieu du fond du dortoir : pas de sortie sur elle)
            Vector3 across = Vector3.Cross(Vector3.up, toEntrance).normalized;
            foreach (float off in new[] { 4.2f, -4.2f, 0f })
            for (float d = 0f; d < 6f && !found; d += 0.5f)
            {
                Vector3 c = start + across * off + toEntrance * d;
                if (!Physics.Raycast(new Vector3(c.x, P(dorm.min).y + 3f, c.z), Vector3.down, out RaycastHit h, 6f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (h.point.y > P(dorm.min).y + 0.15f) continue;                       // le sol, pas le dessus d'un meuble
                if (Physics.CheckBox(h.point + Vector3.up * 1.75f, new Vector3(1.4f, 1.5f, 1.4f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) continue;
                // et un passage dégagé devant l'ouverture (pas une table en travers)
                if (Physics.CheckBox(h.point + toEntrance * 2.1f + Vector3.up * 1f, new Vector3(1.3f, 0.75f, 0.75f), Quaternion.LookRotation(toEntrance), ~0, QueryTriggerInteraction.Ignore)) continue;
                floor = h.point; found = true;
            }
            if (!found) Debug.LogWarning("[Horror] Grenier : pas de place libre au fond du dortoir, monte-charge posé au fond.");
            float yaw = Quaternion.LookRotation(-toEntrance).eulerAngles.y;                 // ouverture vers l'entrée du dortoir
            GameObject low = Cage(parent, "Monte-charge (dortoir)", floor, yaw, "ATTIC  ^");
            GameObject high = Cage(parent, "Monte-charge (grenier)", G(0f, 0f, -GL * 0.5f + 1.6f), 180f, "DORMITORY  v");
            AudioClip grind = Load<AudioClip>(Audio + "Hopital/monte_charge.wav");
            void Link(GameObject from, GameObject to)
            {
                var btn = GameObject.CreatePrimitive(PrimitiveType.Cube);
                btn.name = "Bouton du monte-charge";
                btn.transform.SetParent(from.transform, false);
                btn.transform.localPosition = new Vector3(1.15f, 1.3f, 0.6f);
                btn.transform.localScale = new Vector3(0.12f, 0.2f, 0.2f);
                btn.GetComponent<Renderer>().sharedMaterial = MakeMat("Lobby_Bouton", new Color(0.95f, 0.62f, 0.72f));
                btn.GetComponent<Collider>().isTrigger = true;
                var arrive = new GameObject("Arrivée");
                arrive.transform.SetParent(to.transform, false);
                arrive.transform.localPosition = new Vector3(0f, 0.4f, 0f);
                Teleporter tp = btn.AddComponent<Teleporter>();
                tp.button = btn.AddComponent<PressButton>();
                tp.destination = arrive.transform;
                tp.sound = grind;
            }
            Link(low, high); Link(high, low);

            // cassette 1 (le journal de la Veilleuse) : dans le dortoir, juste à côté du monte-charge
            float toward = Vector3.Dot(P(dorm.center) - floor, Quaternion.Euler(0f, yaw, 0f) * Vector3.right) >= 0f ? 1f : -1f;   // du côté de l'allée
            Vector3 side = Quaternion.Euler(0f, yaw, 0f) * new Vector3(2.4f * toward, 0f, -0.4f);
            Vector3 spot = floor + side;
            if (Physics.Raycast(spot + Vector3.up * 2f, Vector3.down, out RaycastHit fh, 3f, ~0, QueryTriggerInteraction.Ignore)) spot = fh.point;
            TapeOnCrate(parent, "cassette_1", "TAPE 1", "cassette_1_veilleuse", TapeLines1, spot, floor + toEntrance * 3f);
        }

        // ------------------------------------------------------------------ La Veilleuse

        private static void SetupVeilleuse()
        {
            var proto = Load<GameObject>("Assets/Horror/Art/Monsters/LaVeilleuse.glb");
            if (proto == null) { Debug.LogWarning("[Horror] LaVeilleuse.glb introuvable."); return; }
            var v = (GameObject)PrefabUtility.InstantiatePrefab(proto);
            v.name = "La Veilleuse";
            v.transform.SetPositionAndRotation(G(0f, 0f, 6.5f), Quaternion.Euler(0f, 180f, 0f));
            VeilleuseAI ai = v.AddComponent<VeilleuseAI>();
            ai.head = FindChild(v.transform, "Tete");
            ai.face = ai.head;                                                   // le screamer cadre la tête : vue par en dessous, sous l'abat-jour, les yeux
            Bounds b = GrenierBounds();
            b.Expand(new Vector3(-0.4f, 0f, -0.4f));
            ai.zone = b;
            const string A = Audio + "Grenier/";
            ai.bellLoop = Load<AudioClip>(A + "veilleuse_clochette.wav");
            ai.warnBell = Load<AudioClip>(A + "veilleuse_repere.wav");
            ai.creak = Load<AudioClip>(A + "plancher.wav");
            ai.jumpscareSound = Load<AudioClip>(Audio + "Jumpscare/veilleuse_cri.wav");
            GaitAnimator gait = v.AddComponent<GaitAnimator>();                          // pas glissés, longue robe
            gait.walkSpeed = ai.wanderSpeed; gait.runSpeed = ai.huntSpeed; gait.stride = 1.2f;
            gait.bobWalk = 0.03f; gait.bobRun = 0.1f; gait.rollWalk = 2f; gait.rollRun = 5f; gait.leanRun = 14f; gait.armReachRun = 60f;
            // deux yeux pâles qui luisent sous l'abat-jour (ceux du modèle sont trop petits pour se voir dans le noir)
            Transform eyes = FindChild(v.transform, "Yeux");
            Renderer er = eyes != null ? eyes.GetComponentInChildren<Renderer>() : null;
            if (er != null && ai.head != null)
            {
                Material glow = GGlow("YeuxVeilleuse", new Color(0.95f, 0.93f, 0.85f), new Color(0.95f, 0.92f, 0.8f) * 2.5f);
                er.sharedMaterial = glow;
                foreach (float sx in new[] { -1f, 1f })
                {
                    var e = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    e.name = "Oeil luisant"; Object.DestroyImmediate(e.GetComponent<Collider>());
                    e.transform.SetParent(ai.head, true);
                    e.transform.position = er.bounds.center + v.transform.right * (0.036f * sx) + v.transform.forward * 0.004f;
                    e.transform.localScale = Vector3.one * 0.026f;
                    e.GetComponent<Renderer>().sharedMaterial = glow;
                }
            }
            // le visage de cri (comme Le Drap) : caché, le screamer l'affiche. Il jaillit de sous l'abat-jour, devant lui.
            Transform visage = FindChild(v.transform, "Visage");
            Renderer vr = visage != null ? visage.GetComponentInChildren<Renderer>() : null;
            const string CriTex = "Assets/Horror/Art/Monsters/Veilleuse_Visage_Cri.png";
            if (vr != null && ai.head != null && Load<Texture2D>(CriTex) != null)
            {
                var imp = AssetImporter.GetAtPath(CriTex) as TextureImporter;
                if (imp != null && !imp.alphaIsTransparency) { imp.alphaIsTransparency = true; imp.SaveAndReimport(); }
                string mp = "Assets/Horror/Art/Materials/Grenier_VisageCri.mat";
                var cm = Load<Material>(mp);
                if (cm == null) { cm = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Grenier_VisageCri" }; AssetDatabase.CreateAsset(cm, mp); }
                cm.SetTexture("_BaseMap", Load<Texture2D>(CriTex));
                cm.SetFloat("_AlphaClip", 1f); cm.SetFloat("_Cutoff", 0.5f); cm.EnableKeyword("_ALPHATEST_ON");
                cm.SetFloat("_Cull", 0f); cm.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                EditorUtility.SetDirty(cm);
                var cri = GameObject.CreatePrimitive(PrimitiveType.Quad);
                cri.name = "Visage_Cri"; Object.DestroyImmediate(cri.GetComponent<Collider>());
                cri.transform.SetParent(ai.head, true);
                Vector3 fwd = v.transform.forward;
                cri.transform.SetPositionAndRotation(vr.bounds.center + fwd * 0.42f + Vector3.down * 0.06f, Quaternion.LookRotation(-fwd, Vector3.up));
                cri.transform.localScale = new Vector3(0.34f, 0.4f, 1f);
                Renderer cr = cri.GetComponent<Renderer>();
                cr.sharedMaterial = cm; cr.enabled = false;                          // invisible en jeu
            }
            // une lueur froide, à peine : on devine sa silhouette dans le noir
            var g = new GameObject("Lueur de la Veilleuse");
            g.transform.SetParent(v.transform, false);
            g.transform.localPosition = new Vector3(0f, 1.8f, 0.5f);
            Light l = g.AddComponent<Light>();
            l.type = LightType.Point; l.range = 2.6f; l.intensity = 0.5f; l.color = new Color(0.6f, 0.7f, 1f); l.shadows = LightShadows.None;
        }
    }
}
