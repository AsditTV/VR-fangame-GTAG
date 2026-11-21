using System.Collections.Generic;
using Horror.Audio;
using Horror.Interaction;
using Horror.Monsters;
using Horror.Noise;
using Horror.World;
using UnityEditor;
using UnityEngine;

namespace Horror.EditorTools
{
    /// <summary>
    /// L'infirmerie du sous-sol : là où le Docteur « testait » les enfants. Une zone énorme (~50 x 90 m) sous la
    /// salle des machines, reliée par un monte-charge. Construite sur une grille de 1 m : on décrit les salles, les
    /// couloirs et les portes, et le code en déduit les murs (un seul maillage), les sols, les plafonds, les
    /// collisions et le réseau de passage du Docteur. Des couloirs en boucle permettent de casser sa ligne de vue.
    /// </summary>
    public static partial class InstitutBuilder
    {
        private const float HFloor = -14f, HHeight = 4.2f;
        private const int GX0 = 8, GZ0 = -104, GNX = 56, GNZ = 96;
        private const string HDir = "Assets/Horror/Art/Map/Hopital/";

        private struct HRoom
        {
            public string name, title; public RectInt r; public int region;
            public HRoom(string n, string t, int x0, int x1, int z0, int z1, int reg) { name = n; title = t; r = new RectInt(x0, z0, x1 - x0, z1 - z0); region = reg; }
            public Vector3 Center => new Vector3(r.x + r.width * 0.5f, HFloor, r.y + r.height * 0.5f);
        }

        private static readonly HRoom[] HRooms =
        {
            new HRoom("Monte-charge", "THE LIFT", 24, 36, -22, -10, 1),
            new HRoom("Couloir A", "", 28, 32, -87, -22, 0),
            new HRoom("Couloir B", "", 10, 60, -52, -48, 0),
            new HRoom("Couloir C", "", 10, 60, -87, -83, 0),
            new HRoom("Couloir D", "", 10, 14, -83, -52, 0),
            new HRoom("Couloir E", "", 56, 60, -83, -52, 0),
            new HRoom("Salle d'attente", "WAITING ROOM", 32, 46, -44, -26, 2),
            new HRoom("Cabinet 1", "ROOM 1", 20, 28, -31, -24, 3),
            new HRoom("Cabinet 2", "ROOM 2", 20, 28, -40, -33, 4),
            new HRoom("Cabinet 3", "ROOM 3", 20, 28, -48, -41, 5),
            new HRoom("Salle des malades", "THE WARD", 32, 56, -81, -54, 6),
            new HRoom("Bloc opératoire", "SURGERY", 14, 28, -68, -54, 7),
            new HRoom("Archives", "RECORDS", 14, 28, -81, -70, 8),
            new HRoom("Chambre froide", "COLD ROOM", 24, 38, -101, -87, 9),
            new HRoom("Isolement", "ISOLATION", 46, 54, -96, -87, 10),
        };

        // portes : petites zones (cellules de la salle, contre son bord) qui ne font jamais mur
        private static readonly RectInt[] HDoors =
        {
            new RectInt(28, -22, 4, 1),                                   // monte-charge -> couloir A
            new RectInt(32, -37, 1, 3),                                   // salle d'attente
            new RectInt(27, -29, 1, 2), new RectInt(27, -38, 1, 2), new RectInt(27, -46, 1, 2),   // cabinets
            new RectInt(27, -63, 1, 3), new RectInt(14, -62, 1, 3),       // bloc (A et D)
            new RectInt(27, -77, 1, 3), new RectInt(14, -78, 1, 3),       // archives (A et D)
            new RectInt(32, -60, 1, 3), new RectInt(32, -76, 1, 3), new RectInt(55, -69, 1, 3),   // salle des malades
            new RectInt(30, -88, 3, 1), new RectInt(49, -88, 2, 1),       // chambre froide, isolement
        };

        private static int[,] _hReg;      // -2 : vide ; -1 : porte ; sinon région

        private static bool HWalk(int i, int k) => i >= 0 && k >= 0 && i < GNX && k < GNZ && _hReg[i, k] != -2;

        private static bool HWallBetween(int i, int k, int i2, int k2)
        {
            if (!HWalk(i, k) || !HWalk(i2, k2)) return true;
            int a = _hReg[i, k], b = _hReg[i2, k2];
            return a != b && a != -1 && b != -1;
        }

        public static Bounds HopitalBounds()
        {
            return new Bounds(new Vector3(GX0 + GNX * 0.5f, HFloor + HHeight * 0.5f, GZ0 + GNZ * 0.5f), new Vector3(GNX, HHeight + 1f, GNZ));
        }

        private static void SetupHopital()
        {
            _hReg = new int[GNX, GNZ];
            for (int i = 0; i < GNX; i++) for (int k = 0; k < GNZ; k++) _hReg[i, k] = -2;
            foreach (HRoom h in HRooms)
                for (int x = h.r.xMin; x < h.r.xMax; x++) for (int z = h.r.yMin; z < h.r.yMax; z++) _hReg[x - GX0, z - GZ0] = h.region;
            foreach (RectInt d in HDoors)
                for (int x = d.xMin; x < d.xMax; x++) for (int z = d.yMin; z < d.yMax; z++)
                    if (HWalk(x - GX0, z - GZ0)) _hReg[x - GX0, z - GZ0] = -1;

            var root = new GameObject("Infirmerie (murs)");
            BuildHopitalShell(root.transform);
            var props = new GameObject("Infirmerie (mobilier)");
            FurnishHopital(props.transform);
            SetupHopitalLights(props.transform);
            SetupHopitalLore(props.transform);
            SetupLift(props.transform);
            SetupDoctor();

            Bounds b = HopitalBounds();
            var zone = new GameObject("Territoire du Docteur (infirmerie)");
            zone.transform.position = b.center;
            Territory t = zone.AddComponent<Territory>();
            t.owner = "Docteur"; t.size = b.size;
            AmbientZone az = zone.AddComponent<AmbientZone>();
            az.size = b.size; az.priority = 6;
            az.loop = Load<AudioClip>(Audio + "Hopital/hopital_bourdon.wav"); az.loopVolume = 0.35f;
            az.oneShots = LoadMany(Audio + "Ambience", "drip_");
            Debug.Log("[Horror] Infirmerie construite (sous la salle des machines, monte-charge).");
        }

        // ------------------------------------------------------------------ murs, sols, plafonds (grille)

        private static Material HMat(string name, string tex, Vector2 tiling)
        {
            EnsureFolder("Assets/Horror/Art/Materials");
            string path = "Assets/Horror/Art/Materials/Hopital_" + name + ".mat";
            var mat = Load<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Hopital_" + name };
                AssetDatabase.CreateAsset(mat, path);
            }
            var t = Load<Texture2D>(HDir + tex);
            if (t != null) { mat.SetTexture("_BaseMap", t); mat.mainTexture = t; }
            mat.SetFloat("_Smoothness", 0.25f);
            mat.color = Color.white;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void BuildHopitalShell(Transform root)
        {
            // runs de murs : (orientation, ligne, côté) -> segments fusionnés
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
            {
                int n = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
                uvs.Add(ua); uvs.Add(ub); uvs.Add(uc); uvs.Add(ud);
                tris.Add(n); tris.Add(n + 1); tris.Add(n + 2); tris.Add(n); tris.Add(n + 2); tris.Add(n + 3);
            }
            var colliders = new HashSet<(bool, int, int, int)>();   // (vertical, ligne, début, fin) pour les boîtes de collision

            // murs perpendiculaires à X (lignes x = constante), face vers la cellule qui les voit
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < GNX; i++)
                {
                    int start = -1;
                    for (int k = 0; k <= GNZ; k++)
                    {
                        bool wall = k < GNZ && HWalk(i, k) && HWallBetween(i, k, i + side, k);
                        if (wall && start < 0) start = k;
                        if (!wall && start >= 0)
                        {
                            float x = GX0 + i + (side > 0 ? 1 : 0);
                            float z0 = GZ0 + start, z1 = GZ0 + k, y0 = HFloor, y1 = HFloor + HHeight, L = (z1 - z0) / HHeight;
                            if (side > 0) Quad(new Vector3(x, y0, z0), new Vector3(x, y1, z0), new Vector3(x, y1, z1), new Vector3(x, y0, z1),
                                               new Vector2(0, 0), new Vector2(0, 1), new Vector2(L, 1), new Vector2(L, 0));
                            else Quad(new Vector3(x, y0, z1), new Vector3(x, y1, z1), new Vector3(x, y1, z0), new Vector3(x, y0, z0),
                                      new Vector2(0, 0), new Vector2(0, 1), new Vector2(L, 1), new Vector2(L, 0));
                            colliders.Add((true, GX0 + i + (side > 0 ? 1 : 0), GZ0 + start, GZ0 + k));
                            start = -1;
                        }
                    }
                }
            // murs perpendiculaires à Z
            for (int side = -1; side <= 1; side += 2)
                for (int k = 0; k < GNZ; k++)
                {
                    int start = -1;
                    for (int i = 0; i <= GNX; i++)
                    {
                        bool wall = i < GNX && HWalk(i, k) && HWallBetween(i, k, i, k + side);
                        if (wall && start < 0) start = i;
                        if (!wall && start >= 0)
                        {
                            float z = GZ0 + k + (side > 0 ? 1 : 0);
                            float x0 = GX0 + start, x1 = GX0 + i, y0 = HFloor, y1 = HFloor + HHeight, L = (x1 - x0) / HHeight;
                            if (side > 0) Quad(new Vector3(x1, y0, z), new Vector3(x1, y1, z), new Vector3(x0, y1, z), new Vector3(x0, y0, z),
                                               new Vector2(0, 0), new Vector2(0, 1), new Vector2(L, 1), new Vector2(L, 0));
                            else Quad(new Vector3(x0, y0, z), new Vector3(x0, y1, z), new Vector3(x1, y1, z), new Vector3(x1, y0, z),
                                      new Vector2(0, 0), new Vector2(0, 1), new Vector2(L, 1), new Vector2(L, 0));
                            colliders.Add((false, GZ0 + k + (side > 0 ? 1 : 0), GX0 + start, GX0 + i));
                            start = -1;
                        }
                    }
                }
            // Unity : la face visible est celle dont les sommets tournent dans le sens horaire vu de devant ;
            // nos quads étaient construits à l'envers (on voyait le vide de l'intérieur) : on retourne chaque triangle
            for (int i = 0; i < tris.Count; i += 3) { int tmp = tris[i + 1]; tris[i + 1] = tris[i + 2]; tris[i + 2] = tmp; }
            MeshObject(root, "Murs", verts, uvs, tris, HMat("Mur", "hopital_mur.png", Vector2.one));

            // sols et plafonds : une case par cellule marchable
            var fv = new List<Vector3>(); var fu = new List<Vector2>(); var ft = new List<int>();
            var cv = new List<Vector3>(); var cu = new List<Vector2>(); var ct = new List<int>();
            for (int i = 0; i < GNX; i++)
                for (int k = 0; k < GNZ; k++)
                {
                    if (!HWalk(i, k)) continue;
                    float x = GX0 + i, z = GZ0 + k;
                    int n = fv.Count;
                    fv.Add(new Vector3(x, HFloor, z)); fv.Add(new Vector3(x, HFloor, z + 1)); fv.Add(new Vector3(x + 1, HFloor, z + 1)); fv.Add(new Vector3(x + 1, HFloor, z));
                    fu.Add(new Vector2(x, z) * 0.5f); fu.Add(new Vector2(x, z + 1) * 0.5f); fu.Add(new Vector2(x + 1, z + 1) * 0.5f); fu.Add(new Vector2(x + 1, z) * 0.5f);
                    ft.Add(n); ft.Add(n + 1); ft.Add(n + 2); ft.Add(n); ft.Add(n + 2); ft.Add(n + 3);
                    float y = HFloor + HHeight;
                    n = cv.Count;
                    cv.Add(new Vector3(x, y, z)); cv.Add(new Vector3(x + 1, y, z)); cv.Add(new Vector3(x + 1, y, z + 1)); cv.Add(new Vector3(x, y, z + 1));
                    cu.Add(new Vector2(x, z) / 3f); cu.Add(new Vector2(x + 1, z) / 3f); cu.Add(new Vector2(x + 1, z + 1) / 3f); cu.Add(new Vector2(x, z + 1) / 3f);
                    ct.Add(n); ct.Add(n + 1); ct.Add(n + 2); ct.Add(n); ct.Add(n + 2); ct.Add(n + 3);
                }
            MeshObject(root, "Sol", fv, fu, ft, HMat("Sol", "hopital_carrelage.png", Vector2.one));
            MeshObject(root, "Plafond", cv, cu, ct, HMat("Plafond", "hopital_plafond.png", Vector2.one));

            // collisions : des boîtes (fiables pour la locomotion à la rame), pas les maillages fins
            var col = new GameObject("Collisions");
            col.transform.SetParent(root, false);
            foreach (var c in colliders)
            {
                var bc = col.AddComponent<BoxCollider>();
                float len = c.Item4 - c.Item3, mid = (c.Item3 + c.Item4) * 0.5f;
                bc.center = c.Item1 ? new Vector3(c.Item2, HFloor + HHeight * 0.5f, mid) : new Vector3(mid, HFloor + HHeight * 0.5f, c.Item2);
                bc.size = c.Item1 ? new Vector3(0.3f, HHeight, len) : new Vector3(len, HHeight, 0.3f);
            }
            foreach (HRoom h in HRooms)
            {
                var f = col.AddComponent<BoxCollider>();
                f.center = new Vector3(h.r.center.x, HFloor - 0.25f, h.r.center.y); f.size = new Vector3(h.r.width, 0.5f, h.r.height);
                var cc = col.AddComponent<BoxCollider>();
                cc.center = new Vector3(h.r.center.x, HFloor + HHeight + 0.25f, h.r.center.y); cc.size = new Vector3(h.r.width, 0.5f, h.r.height);
            }
            GameObjectUtility.SetStaticEditorFlags(root.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        }

        private static void MeshObject(Transform parent, string name, List<Vector3> v, List<Vector2> uv, List<int> t, Material m)
        {
            var mesh = new Mesh { name = "Infirmerie_" + name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.RecalculateTangents();
            EnsureFolder("Assets/Horror/Art/Map/Genere");
            string path = "Assets/Horror/Art/Map/Genere/Infirmerie_" + name + ".asset";
            if (Load<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = m;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        }

        // ------------------------------------------------------------------ mobilier

        private static GameObject HPut(Transform parent, string file, Vector3 pos, float yaw, bool solid = true, float scale = 1.4f)
        {
            var proto = Load<GameObject>(HDir + file + ".glb");
            if (proto == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(proto);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(new Vector3(pos.x, HFloor, pos.z), Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = Vector3.one * scale;
            if (solid) AddBoundsCollider(go.transform, go.transform, false);
            return go;
        }

        /// <summary>Une boîte de collision alignée sur le monde, autour des rendus de <paramref name="of"/>, posée sous <paramref name="on"/>.</summary>
        private static BoxCollider AddBoundsCollider(Transform on, Transform of, bool trigger, float grow = 0f)
        {
            Bounds b = default; bool first = true;
            foreach (Renderer r in of.GetComponentsInChildren<Renderer>(true))
            {
                if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
            }
            if (first) return null;
            var holder = new GameObject(trigger ? "Contact" : "Collision");
            holder.transform.SetParent(on, false);
            holder.transform.position = b.center;
            holder.transform.rotation = Quaternion.identity;
            var bc = holder.AddComponent<BoxCollider>();
            Vector3 s = holder.transform.lossyScale;
            bc.size = new Vector3((b.size.x + grow) / s.x, (b.size.y + grow) / s.y, (b.size.z + grow) / s.z);
            bc.isTrigger = trigger;
            return bc;
        }

        /// <summary>Un objet fragile : sa partie « Chute_* » devient physique et fait un fracas énorme si on la touche.</summary>
        private static void HFragile(Transform parent, string file, Vector3 pos, float yaw, string crash)
        {
            GameObject go = HPut(parent, file, pos, yaw, false);
            if (go == null) return;
            Transform chute = null;
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith("Chute_")) { chute = t; break; }
            foreach (Transform c in go.transform)                                       // le support reste solide
                if (c != chute && c.GetComponentInChildren<Renderer>() != null) AddBoundsCollider(go.transform, c, false);
            if (chute == null) return;
            AddBoundsCollider(chute, chute, false, -0.02f);
            AddBoundsCollider(chute, chute, true, 0.25f);
            FragileNoisy f = chute.gameObject.AddComponent<FragileNoisy>();
            f.crash = Load<AudioClip>(Audio + "Hopital/" + crash + ".wav");
        }

        private static void FurnishHopital(Transform p)
        {
            // le hall du monte-charge : comptoir, chaises, panneau
            var desk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            desk.name = "Comptoir"; desk.transform.SetParent(p, false);
            desk.transform.position = new Vector3(33.5f, HFloor + 0.6f, -16f); desk.transform.localScale = new Vector3(1.2f, 1.2f, 4f);
            desk.GetComponent<Renderer>().sharedMaterial = MakeMat("Hopital_Comptoir", new Color(0.66f, 0.82f, 0.76f));
            for (int k = 0; k < 4; k++) HPut(p, "ChaiseAttente", new Vector3(25.5f, 0, -13.5f - k * 1.2f), 90f);
            HFragile(p, "Plateau", new Vector3(33.5f, 0, -20.2f), 10f, "fracas_metal");

            // salle d'attente : rangées de petites chaises tournées vers le mur, un fauteuil roulant vide
            for (int row = 0; row < 3; row++)
                for (int k = 0; k < 6; k++)
                    HPut(p, "ChaiseAttente", new Vector3(36f + k * 1.4f, 0, -30f - row * 3.5f), 180f + (k == 3 && row == 1 ? 25f : 0f));
            HPut(p, "FauteuilRoulant", new Vector3(44f, 0, -41f), 220f);
            HFragile(p, "Bocaux", new Vector3(45.3f, 0, -28f), 270f, "fracas_verre");
            HFragile(p, "Plateau", new Vector3(34f, 0, -42.5f), 30f, "fracas_metal");

            // cabinets : un lit, un plateau, des bocaux, un rideau
            foreach (float z in new[] { -27.5f, -36.5f, -44.5f })
            {
                HPut(p, "LitHopital", new Vector3(22f, 0, z), 90f);
                HFragile(p, "Plateau", new Vector3(24.5f, 0, z + 2f), 0f, "fracas_metal");
                HFragile(p, "Bocaux", new Vector3(20.6f, 0, z - 2.2f), 90f, "fracas_verre");
                HPut(p, "Rideau", new Vector3(25.5f, 0, z - 0.5f), 90f);
            }

            // salle des malades : 2 rangées de lits, des rideaux entre (cachettes), des perfusions dans les allées
            var hides = new GameObject("Cachettes (lits de l'infirmerie)");
            hides.transform.SetParent(p, false);
            for (int side = 0; side < 2; side++)
                for (int k = 0; k < 6; k++)
                {
                    float x = side == 0 ? 35f : 53f, z = -57.5f - k * 3.9f;
                    HPut(p, "LitHopital", new Vector3(x, 0, z), side == 0 ? 90f : 270f);
                    var hz = new GameObject("Lit " + (side == 0 ? "O" : "E") + k);
                    hz.transform.SetParent(hides.transform, false);
                    hz.transform.position = new Vector3(x, HFloor + 0.9f, z);
                    hz.AddComponent<HideZone>().size = new Vector3(2.4f, 1.0f, 1.3f);
                    if (k < 5) HPut(p, "Rideau", new Vector3(x + (side == 0 ? 0.3f : -0.3f), 0, z - 1.95f), 0f);
                    if (k % 2 == 1) HFragile(p, "Perfusion", new Vector3(x + (side == 0 ? 2.2f : -2.2f), 0, z + 0.6f), k * 40f, "fracas_perfusion");
                }
            var doll = Load<GameObject>("Assets/Horror/Art/Character/LeDessine_PNJ.glb");
            if (doll != null)                                                             // deux enfants « en observation »
                foreach (Vector3 bed in new[] { new Vector3(35f, 0, -65.3f), new Vector3(53f, 0, -73.1f) })
                {
                    var d = (GameObject)PrefabUtility.InstantiatePrefab(doll);
                    d.name = "Enfant en observation"; d.transform.SetParent(p, false);
                    d.transform.SetPositionAndRotation(new Vector3(bed.x, HFloor + 0.75f, bed.z), Quaternion.Euler(-90f, bed.x < 40f ? 90f : 270f, 0f));
                }
            HFragile(p, "Plateau", new Vector3(41.5f, 0, -62.5f), 15f, "fracas_metal");   // pas au centre : le Docteur passe par là

            // bloc opératoire : la table sous la grande lampe, plateaux et bocaux tout autour
            HPut(p, "TableOperation", new Vector3(21f, 0, -61f), 0f);
            var lamp = HPut(p, "LampeOperatoire", new Vector3(21f, 0, -61f), 0f, false);
            if (lamp != null) lamp.transform.position = new Vector3(21f, HFloor + HHeight, -61f);
            HFragile(p, "Plateau", new Vector3(23.4f, 0, -59.5f), 0f, "fracas_metal");
            HFragile(p, "Plateau", new Vector3(18.6f, 0, -62.5f), 180f, "fracas_metal");
            HFragile(p, "Bocaux", new Vector3(15.3f, 0, -56f), 90f, "fracas_verre");
            HFragile(p, "Bocaux", new Vector3(26.6f, 0, -66.5f), 270f, "fracas_verre");
            HFragile(p, "Perfusion", new Vector3(23f, 0, -63f), 0f, "fracas_perfusion");

            // archives : classeurs, dossiers
            for (int k = 0; k < 6; k++) HPut(p, "Classeur", new Vector3(15.3f + (k % 3) * 0.9f, 0, k < 3 ? -71.2f : -79.6f), k < 3 ? 0f : 180f);
            HFragile(p, "Bocaux", new Vector3(26.5f, 0, -71.4f), 0f, "fracas_verre");

            // chambre froide : des étagères de bocaux, et des poupées assises contre le mur, en rang, qui attendent
            for (int k = 0; k < 4; k++) HFragile(p, "Bocaux", new Vector3(25.4f, 0, -90f - k * 2.5f), 90f, "fracas_verre");
            if (doll != null)
                for (int k = 0; k < 6; k++)
                {
                    var d = (GameObject)PrefabUtility.InstantiatePrefab(doll);
                    d.name = "Enfant qui attend son tour"; d.transform.SetParent(p, false);
                    d.transform.SetPositionAndRotation(new Vector3(37.2f, HFloor + 0.05f, -89.5f - k * 1.8f), Quaternion.Euler(0f, 270f, 0f));
                    Transform head = FindChild(d.transform, "Tete");
                    if (head != null) head.localRotation = Quaternion.Euler(0f, 0f, (k % 2 == 0 ? 1 : -1) * 22f);
                }
            HFragile(p, "Plateau", new Vector3(31f, 0, -97f), 0f, "fracas_metal");

            // couloirs : des obstacles fragiles qu'il faut contourner sans les frôler
            foreach (Vector3 c in new[] { new Vector3(30.8f, 0, -33f), new Vector3(29.2f, 0, -56f), new Vector3(30.8f, 0, -79f),
                                          new Vector3(20f, 0, -49f), new Vector3(46f, 0, -85.6f), new Vector3(12.8f, 0, -66f), new Vector3(57.2f, 0, -60f) })
                HFragile(p, ((int)c.z) % 2 == 0 ? "Perfusion" : "Plateau", c, c.x * 13f, ((int)c.z) % 2 == 0 ? "fracas_perfusion" : "fracas_metal");
            HPut(p, "FauteuilRoulant", new Vector3(58f, 0, -76f), 160f);
            HPut(p, "FauteuilRoulant", new Vector3(11.5f, 0, -54.5f), 20f);
        }

        // ------------------------------------------------------------------ lumières (néons qui grésillent, beaucoup de noir)

        private static void SetupHopitalLights(Transform p)
        {
            var root = new GameObject("Lumières de l'infirmerie");
            root.transform.SetParent(p, false);
            var rnd = new System.Random(23);
            void Neon(Vector3 at, float range, float intensity)
            {
                var g = new GameObject("Néon");
                g.transform.SetParent(root.transform, false);
                g.transform.position = new Vector3(at.x, HFloor + HHeight - 0.3f, at.z);
                Light l = g.AddComponent<Light>();
                l.type = LightType.Point; l.color = new Color(0.82f, 0.96f, 0.84f); l.range = range * 1.25f; l.intensity = intensity * 2.2f;   // néons verdâtres d'hôpital
                if (rnd.NextDouble() < 0.45) g.AddComponent<LightFlicker>();
            }
            foreach (HRoom h in HRooms)
            {
                if (h.region == 0) continue;
                if (h.name == "Chambre froide") continue;          // le noir complet : seulement sa lanterne
                Neon(h.Center, Mathf.Max(h.r.width, h.r.height) * 0.75f, h.name == "Bloc opératoire" ? 1.1f : 0.7f);
            }
            // couloirs : un néon tous les 12 m, un sur trois mort
            int n = 0;
            foreach (HRoom h in HRooms)
            {
                if (h.region != 0) continue;
                bool alongZ = h.r.height > h.r.width;
                int len = alongZ ? h.r.height : h.r.width;
                for (int s = 6; s < len; s += 12)
                {
                    if (n++ % 3 == 2) continue;
                    Vector3 at = alongZ ? new Vector3(h.r.center.x, 0, h.r.yMin + s) : new Vector3(h.r.xMin + s, 0, h.r.center.y);
                    Neon(at, 7f, 0.55f);
                }
            }
            // la grande lampe du bloc : un cône de lumière sur la table
            var spot = new GameObject("Lampe du bloc");
            spot.transform.SetParent(root.transform, false);
            spot.transform.SetPositionAndRotation(new Vector3(21f, HFloor + HHeight - 1.2f, -61f), Quaternion.Euler(90f, 0f, 0f));
            Light sl = spot.AddComponent<Light>();
            sl.type = LightType.Spot; sl.spotAngle = 60f; sl.range = 6f; sl.intensity = 3f; sl.color = new Color(1f, 0.95f, 0.85f);
            spot.AddComponent<LightFlicker>();
        }

        // ------------------------------------------------------------------ lore : les dossiers des enfants

        private static void SetupHopitalLore(Transform p)
        {
            var root = new GameObject("Dossiers des enfants (lore)");
            root.transform.SetParent(p, false);
            Color ink = new Color(0.16f, 0.14f, 0.2f);
            void Board(string text, Vector3 pos, Vector3 facing, float size = 0.022f)
            {
                var paper = GameObject.CreatePrimitive(PrimitiveType.Quad);
                paper.name = "Fiche"; Object.DestroyImmediate(paper.GetComponent<Collider>());
                paper.transform.SetParent(root.transform, false);
                paper.transform.position = pos + facing * 0.02f;
                paper.transform.rotation = Quaternion.LookRotation(-facing, Vector3.up);
                paper.transform.localScale = new Vector3(1.5f, 1.15f, 1f);
                paper.GetComponent<Renderer>().sharedMaterial = MakeMat("Hopital_Fiche", new Color(0.93f, 0.9f, 0.82f));
                GameObject t = SafeZoneShopBuilder.Label(root.transform, text, Vector3.zero, size, ink);
                t.transform.position = pos + facing * 0.04f;
                t.transform.rotation = Quaternion.LookRotation(-facing, Vector3.up);
            }
            Vector3 north = Vector3.forward, south = Vector3.back;
            Board("PATIENT 07 - CELESTE M., 9\nTEST: LISTENING\nEyes covered for 40 days.\nResult: hears a pin drop at 30 m.\nNote: she will not stop listening.", new Vector3(18f, HFloor + 2f, -80.95f), north);
            Board("PATIENT 12 - ODETTE B., 10\nTEST: WATER\nKept in the cold baths.\nResult: feels every ripple.\nNote: sent to the laundry.", new Vector3(23f, HFloor + 2f, -80.95f), north);
            Board("PATIENTS 16 TO 21 - 'THE SOFTIES'\nTEST: STILLNESS\nCostumes sewn shut.\nResult: they only move\nwhen no one is looking.", new Vector3(18f, HFloor + 2f, -70.05f), south);
            Board("PATIENT 23 - MILA ?\nTEST: -\nFile empty.\nSomeone tore the page out.", new Vector3(23f, HFloor + 2f, -70.05f), south);
            Board("THEY ARE NOT SICK.\nTHEY ARE UNFINISHED.\n\n- Dr.", new Vector3(14.05f, HFloor + 2.4f, -61f), Vector3.left * -1f, 0.04f);
            Board("QUIET PLEASE.\nTHE DOCTOR IS LISTENING\nFOR NOTHING BUT CRASHES.\nHE SEES WHAT HIS LAMP SEES.", new Vector3(35.95f, HFloor + 2.2f, -16f), Vector3.left, 0.026f);
            for (int k = 0; k < 7; k++)                                   // l'isolement : griffonné partout
            {
                GameObject s = SafeZoneShopBuilder.Label(root.transform, k % 2 == 0 ? "LET ME SEE AGAIN" : "i can hear you", Vector3.zero, 0.03f, new Color(0.35f, 0.12f, 0.16f));
                s.transform.position = new Vector3(46.05f, HFloor + 0.8f + k * 0.42f, -95f + k * 1.1f);
                s.transform.rotation = Quaternion.LookRotation(Vector3.left, Vector3.up) * Quaternion.Euler(0f, 0f, (k % 3 - 1) * 8f);
            }
        }

        // ------------------------------------------------------------------ le monte-charge (salle des machines <-> infirmerie)

        private static GameObject Cage(Transform parent, string name, Vector3 floor, float yaw, string label)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(floor, Quaternion.Euler(0f, yaw, 0f));
            Material metal = MakeMat("Hopital_Grille", new Color(0.55f, 0.6f, 0.6f));
            void Bar(Vector3 lp, Vector3 sz)
            {
                var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                b.name = "Grille"; b.transform.SetParent(root.transform, false);
                b.transform.localPosition = lp; b.transform.localScale = sz;
                b.GetComponent<Renderer>().sharedMaterial = metal;
            }
            const float W = 2.6f, Hh = 3.2f;
            foreach (float sx in new[] { -1f, 1f }) foreach (float sz in new[] { -1f, 1f }) Bar(new Vector3(sx * W / 2, Hh / 2, sz * W / 2), new Vector3(0.1f, Hh, 0.1f));
            Bar(new Vector3(0, Hh, 0), new Vector3(W, 0.12f, W));
            for (int k = 0; k < 7; k++)                                     // barreaux sur 3 côtés (devant ouvert)
            {
                float o = -W / 2 + (k + 0.5f) * W / 7;
                Bar(new Vector3(o, Hh / 2, W / 2), new Vector3(0.04f, Hh, 0.04f));
                Bar(new Vector3(-W / 2, Hh / 2, o), new Vector3(0.04f, Hh, 0.04f));
                Bar(new Vector3(W / 2, Hh / 2, o), new Vector3(0.04f, Hh, 0.04f));
            }
            Bar(new Vector3(0, 0.03f, 0), new Vector3(W, 0.06f, W));
            GameObject sign = SafeZoneShopBuilder.Label(root.transform, label, Vector3.zero, 0.03f, new Color(0.95f, 0.9f, 0.8f));
            sign.transform.localPosition = new Vector3(0f, Hh + 0.35f, -W / 2);
            sign.transform.localRotation = Quaternion.identity;   // lisible depuis l'ouverture de la cage
            return root;
        }

        private static void SetupLift(Transform parent)
        {
            Physics.SyncTransforms();
            // une place libre dans la salle des machines (au sol du sous-sol), près du centre
            Vector3 top = new Vector3(28f, -4.2f, -24f);
            bool found = false;
            for (float r = 0f; r < 12f && !found; r += 1.5f)
                for (int a = 0; a < 12 && !found; a++)
                {
                    Vector3 c = new Vector3(28f, 0f, -24f) + new Vector3(Mathf.Cos(a * 0.52f), 0f, Mathf.Sin(a * 0.52f)) * r;
                    if (!Physics.Raycast(new Vector3(c.x, 2f, c.z), Vector3.down, out RaycastHit h, 10f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (h.point.y > -3f) continue;
                    if (Physics.CheckBox(h.point + Vector3.up * 1.7f, new Vector3(1.5f, 1.5f, 1.5f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) continue;
                    top = h.point; found = true;
                }
            GameObject up = Cage(parent, "Monte-charge (salle des machines)", top, 0f, "INFIRMARY  v");
            GameObject down = Cage(parent, "Monte-charge (infirmerie)", new Vector3(30f, HFloor, -12.5f), 180f, "MACHINES  ^");
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
            Link(up, down); Link(down, up);
            if (!found) Debug.LogWarning("[Horror] Monte-charge : pas de place libre trouvée dans la salle des machines, posé au centre.");
        }

        // ------------------------------------------------------------------ le Docteur

        private static void SetupDoctor()
        {
            var proto = Load<GameObject>("Assets/Horror/Art/Monsters/LeDocteur.glb");
            if (proto == null) return;
            var doc = (GameObject)PrefabUtility.InstantiatePrefab(proto);
            doc.name = "Le Docteur";
            doc.transform.SetPositionAndRotation(new Vector3(24f, HFloor, -57f), Quaternion.Euler(0f, 200f, 0f));
            doc.transform.localScale = Vector3.one * 1.3f;
            DoctorAI ai = doc.AddComponent<DoctorAI>();
            ai.head = FindChild(doc.transform, "Tete");
            ai.lantern = FindChild(doc.transform, "Lanterne");
            if (ai.head != null)
            {
                var lg = new GameObject("Lumière de la lanterne (ce qu'il voit)");
                lg.transform.SetParent(ai.head, false);
                lg.transform.localPosition = new Vector3(0f, -0.1f, 0.2f);
                Light l = lg.AddComponent<Light>();
                l.type = LightType.Spot; l.spotAngle = ai.viewAngle * 2f; l.innerSpotAngle = ai.viewAngle; l.range = ai.viewRange;
                l.intensity = 3.2f; l.color = new Color(0.74f, 1f, 0.62f); l.shadows = LightShadows.None;
                ai.lanternLight = l;
            }
            if (ai.lantern != null)
            {
                var g = new GameObject("Lueur de la lanterne");
                g.transform.SetParent(ai.lantern, false);
                Light l = g.AddComponent<Light>();
                l.type = LightType.Point; l.range = 3f; l.intensity = 1.2f; l.color = new Color(0.74f, 1f, 0.62f);
            }
            const string A = Audio + "Hopital/";
            ai.humLoop = Load<AudioClip>(A + "docteur_fredonne.wav");
            ai.breathLoop = Load<AudioClip>(A + "docteur_souffle.wav");
            ai.beak = Load<AudioClip>(A + "docteur_bec.wav");
            ai.step = Load<AudioClip>(A + "docteur_pas.wav");
            ai.jumpscareSound = Load<AudioClip>(Audio + "Jumpscare/docteur_cri.wav");
            BuildDoctorGraph(ai);
        }

        private static void BuildDoctorGraph(DoctorAI ai)
        {
            var pts = new List<Vector3>();
            foreach (HRoom h in HRooms)
            {
                if (h.region != 0) { pts.Add(h.Center); continue; }
                bool alongZ = h.r.height > h.r.width;
                int len = alongZ ? h.r.height : h.r.width;
                for (int s = 2; s < len; s += 6)
                    pts.Add(alongZ ? new Vector3(h.r.center.x, HFloor, h.r.yMin + s) : new Vector3(h.r.xMin + s, HFloor, h.r.center.y));
            }
            foreach (RectInt d in HDoors)                                   // de part et d'autre de chaque porte
            {
                Vector2 c = d.center;
                bool vertical = d.width == 1;
                pts.Add(new Vector3(c.x + (vertical ? -1.2f : 0f), HFloor, c.y + (vertical ? 0f : -1.2f)));
                pts.Add(new Vector3(c.x + (vertical ? 1.2f : 0f), HFloor, c.y + (vertical ? 0f : 1.2f)));
            }
            pts.RemoveAll(q => !HWalk(Mathf.FloorToInt(q.x) - GX0, Mathf.FloorToInt(q.z) - GZ0));
            Physics.SyncTransforms();
            for (int i = 0; i < pts.Count; i++) pts[i] = FreeNavSpot(pts[i]);   // jamais un point de passage DANS un meuble
            var links = new List<Vector2Int>();
            for (int a = 0; a < pts.Count; a++)
                for (int b = a + 1; b < pts.Count; b++)
                    if ((pts[a] - pts[b]).magnitude < 14f && HClearLine(pts[a], pts[b])) links.Add(new Vector2Int(a, b));
            ai.points = pts.ToArray();
            ai.links = links.ToArray();
            Debug.Log($"[Horror] Docteur : {pts.Count} points de passage, {links.Count} liens.");
        }

        /// <summary>Peut-on marcher en ligne droite de a à b sur la grille (sans traverser de mur) ?</summary>
        private static bool HClearLine(Vector3 a, Vector3 b)
        {
            int pi = Mathf.FloorToInt(a.x) - GX0, pk = Mathf.FloorToInt(a.z) - GZ0;
            float len = (b - a).magnitude;
            for (float t = 0f; t <= len; t += 0.2f)
            {
                Vector3 q = Vector3.Lerp(a, b, t / len);
                // marge : le Docteur est large, on teste aussi un peu à gauche et à droite
                Vector3 side = Vector3.Cross(Vector3.up, (b - a).normalized) * 0.45f;
                foreach (Vector3 o in new[] { Vector3.zero, side, -side })
                    if (!HWalk(Mathf.FloorToInt(q.x + o.x) - GX0, Mathf.FloorToInt(q.z + o.z) - GZ0)) return false;
                int i = Mathf.FloorToInt(q.x) - GX0, k = Mathf.FloorToInt(q.z) - GZ0;
                if (i != pi || k != pk)
                {
                    if (i != pi && k != pk) { if (HWallBetween(pi, pk, i, pk) && HWallBetween(pi, pk, pi, k)) return false; }
                    else if (HWallBetween(pi, pk, i, k)) return false;
                    pi = i; pk = k;
                }
            }
            return true;
        }
    }
}
