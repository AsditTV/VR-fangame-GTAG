using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Horror.EditorTools
{
    /// <summary>
    /// L'infirmerie habitée : feuilles et dessins d'enfants par terre, taches d'encre, peluches et cubes oubliés,
    /// dessins encadrés et griffonnages d'enfants aux murs (qui sont aussi des conseils pour survivre au Docteur),
    /// planning des tests, notes d'infirmière, fiches au pied des lits, toise avec les tailles des enfants.
    /// Placement déterministe (graine fixe) : la même infirmerie à chaque construction.
    /// </summary>
    public static partial class InstitutBuilder
    {
        private struct HWall { public Vector3 pos, normal; public int region; }

        /// <summary>Les murs vus depuis l'intérieur (centre de la case contre le mur, et la normale vers la salle).</summary>
        private static List<HWall> HWalls()
        {
            var list = new List<HWall>();
            var dirs = new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };
            for (int i = 0; i < GNX; i++)
                for (int k = 0; k < GNZ; k++)
                {
                    if (!HWalk(i, k) || _hReg[i, k] == -1) continue;
                    foreach (Vector2Int d in dirs)
                        if (HWallBetween(i, k, i + d.x, k + d.y) && !HWalk(i + d.x, k + d.y) || (HWalk(i + d.x, k + d.y) && HWallBetween(i, k, i + d.x, k + d.y)))
                        {
                            Vector3 c = new Vector3(GX0 + i + 0.5f, HFloor, GZ0 + k + 0.5f);
                            list.Add(new HWall { pos = c + new Vector3(d.x, 0f, d.y) * 0.5f, normal = new Vector3(-d.x, 0f, -d.y), region = _hReg[i, k] });
                        }
                }
            return list;
        }

        private static void DressHopital(Transform parent)
        {
            var root = new GameObject("Infirmerie (détails et lore)");
            root.transform.SetParent(parent, false);
            var rnd = new System.Random(2323);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            const string D = "Assets/Horror/Art/Map/Decor/";

            // matières : papier, dessins d'enfants (ceux des cadres), encre
            Material paper = MakeMat("Hopital_Feuille", new Color(0.92f, 0.9f, 0.84f));
            Material ink = MakeMat("Encre_Mort", new Color(0.025f, 0.015f, 0.035f));
            var drawings = new List<Material>();
            var frames = new List<GameObject>();
            foreach (string key in new[] { "family", "friends", "lady", "pool" })
            {
                var proto = Load<GameObject>(D + "Cadre_" + key + ".glb");
                if (proto == null) continue;
                frames.Add(proto);
                Transform dt = FindChild(proto.transform, "Dessin");
                if (dt != null) drawings.Add(dt.GetComponent<Renderer>().sharedMaterial);
            }
            GameObject Quad(string name, Material m, Vector3 pos, Quaternion rot, Vector2 size)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.name = name; Object.DestroyImmediate(q.GetComponent<Collider>());
                q.transform.SetParent(root.transform, false);
                q.transform.SetPositionAndRotation(pos, rot);
                q.transform.localScale = new Vector3(size.x, size.y, 1f);
                q.GetComponent<Renderer>().sharedMaterial = m;
                return q;
            }

            // 1) au sol : feuilles, dessins, taches d'encre (dans les salles ET les couloirs)
            int sheets = 0;
            foreach (HRoom h in HRooms)
            {
                int n = Mathf.Clamp(h.r.width * h.r.height / 18, 2, 14);
                for (int j = 0; j < n; j++)
                {
                    Vector3 p = new Vector3(R(h.r.xMin + 0.6f, h.r.xMax - 0.6f), HFloor + 0.006f + j * 0.0004f, R(h.r.yMin + 0.6f, h.r.yMax - 0.6f));
                    if (!HWalk(Mathf.FloorToInt(p.x) - GX0, Mathf.FloorToInt(p.z) - GZ0)) continue;
                    bool drawn = drawings.Count > 0 && rnd.NextDouble() < 0.35;
                    Quad(drawn ? "Dessin par terre" : "Feuille", drawn ? drawings[rnd.Next(drawings.Count)] : paper, p,
                         Quaternion.Euler(90f, R(0f, 360f), 0f), drawn ? new Vector2(0.42f, 0.32f) : new Vector2(0.3f, 0.4f));
                    sheets++;
                    if (rnd.NextDouble() < 0.25)                                                   // une tache d'encre
                        Quad("Tache d'encre", ink, p + new Vector3(R(-1f, 1f), 0.002f, R(-1f, 1f)), Quaternion.Euler(90f, R(0f, 360f), 0f), Vector2.one * R(0.3f, 0.9f));
                }
            }

            // 2) peluches et cubes oubliés (salle d'attente, salle des malades, chambre froide, couloirs)
            var lapin = Load<GameObject>(D + "Lapin.glb");
            var cubes = Load<GameObject>(D + "Cubes.glb");
            Vector3[] toys = { new Vector3(41f, 0, -38f), new Vector3(37f, 0, -60.5f), new Vector3(50.5f, 0, -71f), new Vector3(30.5f, 0, -95f),
                               new Vector3(29.4f, 0, -46f), new Vector3(58.4f, 0, -70f), new Vector3(11.6f, 0, -76f), new Vector3(26f, 0, -29.5f) };
            for (int j = 0; j < toys.Length; j++)
            {
                GameObject proto = j % 3 == 2 ? cubes : lapin;
                if (proto == null) continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(proto);
                go.name = j % 3 == 2 ? "Cubes oubliés" : "Peluche oubliée";
                go.transform.SetParent(root.transform, false);
                go.transform.SetPositionAndRotation(new Vector3(toys[j].x, HFloor, toys[j].z), Quaternion.Euler(0f, R(0f, 360f), 0f));
                go.transform.localScale = Vector3.one * 1.4f;
            }

            // 3) aux murs : dessins encadrés, et des griffonnages d'enfants (des conseils... et des peurs)
            List<HWall> walls = HWalls();
            var used = new List<Vector3>();
            bool FarFromOthers(Vector3 p, float d) { foreach (Vector3 u in used) if ((u - p).sqrMagnitude < d * d) return false; return true; }
            int framed = 0;
            for (int j = 0; j < walls.Count && framed < 26; j += 7)
            {
                HWall w = walls[(j * 37) % walls.Count];
                if (w.region == 1 || !FarFromOthers(w.pos, 5f) || frames.Count == 0) continue;
                var f = (GameObject)PrefabUtility.InstantiatePrefab(frames[framed % frames.Count]);
                f.name = "Dessin d'enfant (infirmerie)";
                f.transform.SetParent(root.transform, false);
                f.transform.SetPositionAndRotation(w.pos + w.normal * 0.02f + Vector3.up * R(1.3f, 1.9f), Quaternion.LookRotation(w.normal, Vector3.up) * Quaternion.Euler(0f, 0f, R(-8f, 8f)));
                f.transform.localScale = Vector3.one * 1.5f;
                used.Add(w.pos); framed++;
            }
            string[] scribbles =
            {
                "HE CAN'T SEE IN THE DARK", "HIDE IN THE BEDS", "DON'T DROP ANYTHING", "when he hums he hasn't seen you",
                "I WANT TO GO HOME", "the lamp sees everything", "BED 4 IS QUIET NOW", "we are not sick", "mama?",
                "DON'T SCREAM HERE", "he hears the glass break", "count to 23", "STOP HUMMING", "i saw mila"
            };
            Color crayon = new Color(0.58f, 0.2f, 0.32f);
            int sc = 0;
            for (int j = 3; j < walls.Count && sc < scribbles.Length; j += 11)
            {
                HWall w = walls[(j * 53) % walls.Count];
                if (!FarFromOthers(w.pos, 4f)) continue;
                GameObject t = SafeZoneShopBuilder.Label(root.transform, scribbles[sc], Vector3.zero, 0.022f, sc % 3 == 0 ? crayon : new Color(0.22f, 0.18f, 0.28f));
                t.name = "Griffonnage";
                t.transform.SetPositionAndRotation(w.pos + w.normal * 0.03f + Vector3.up * R(0.5f, 1.1f),
                                                   Quaternion.LookRotation(-w.normal, Vector3.up) * Quaternion.Euler(0f, 0f, R(-10f, 10f)));
                used.Add(w.pos); sc++;
            }

            // 4) le lore : planning, notes, fiches, toise
            Color typed = new Color(0.16f, 0.14f, 0.2f);
            void Note(string text, Vector3 pos, Vector3 facing, float size, Vector2 sheet)
            {
                Quad("Feuille punaisée", paper, pos + facing * 0.015f, Quaternion.LookRotation(-facing, Vector3.up), sheet);
                GameObject t = SafeZoneShopBuilder.Label(root.transform, text, Vector3.zero, size, typed);
                t.transform.SetPositionAndRotation(pos + facing * 0.03f, Quaternion.LookRotation(-facing, Vector3.up));
            }
            Note("TESTS - TODAY\n\n07  LISTENING   room 1\n12  COLD WATER  room 2\n16-21  STILLNESS  room 3\n23  SILENCE   ---\n\nQUIET IN THE CORRIDORS.",
                 new Vector3(24.05f, HFloor + 2.1f, -16f), Vector3.right, 0.02f, new Vector2(1.4f, 1.3f));
            Note("WAIT QUIETLY.\nNAMES ARE CALLED ONCE.", new Vector3(45.95f, HFloor + 2.2f, -35f), Vector3.left, 0.03f, new Vector2(1.5f, 0.8f));
            Note("ROOM 1 - HEARING\nCover the eyes. Ring the bell.\nWrite down how far she hears.", new Vector3(20.05f, HFloor + 1.9f, -27.5f), Vector3.right, 0.016f, new Vector2(1.1f, 0.7f));
            Note("ROOM 2 - COLD WATER\nFill the bath. Count to 300.\nDo not let her out.", new Vector3(20.05f, HFloor + 1.9f, -36.5f), Vector3.right, 0.016f, new Vector2(1.1f, 0.7f));
            Note("ROOM 3 - STILLNESS\nSew the costume shut.\nIf it moves, start again.", new Vector3(20.05f, HFloor + 1.9f, -44.5f), Vector3.right, 0.016f, new Vector2(1.1f, 0.7f));
            Note("NIGHT NOTES\nBed 4 cried all night.\nBed 4 is quiet now.\nThe Doctor says that is progress.\n- nurse A.", new Vector3(44f, HFloor + 2.1f, -80.95f), Vector3.forward, 0.018f, new Vector2(1.3f, 0.9f));
            Note("DO NOT OPEN\nUNTIL THEY STOP\nKNOCKING", new Vector3(31f, HFloor + 2f, -87.05f), Vector3.back, 0.03f, new Vector2(1.2f, 0.8f));
            // toise : les tailles des enfants (Mila, toute petite, tout en bas)
            var chart = Quad("Toise", MakeMat("Hopital_Toise", new Color(0.86f, 0.78f, 0.62f)), new Vector3(45.95f, HFloor + 1.1f, -29f), Quaternion.LookRotation(Vector3.right, Vector3.up), new Vector2(0.35f, 2.0f));
            string[] heights = { "CELESTE 1m31", "ODETTE 1m28", "SOFTIE 3 1m19", "SOFTIE 5 1m12", "MILA 0m98" };
            for (int j = 0; j < heights.Length; j++)
            {
                GameObject t = SafeZoneShopBuilder.Label(root.transform, "- " + heights[j], Vector3.zero, 0.011f, typed);
                t.transform.SetPositionAndRotation(new Vector3(45.92f, HFloor + 1.95f - j * 0.32f, -28.85f), Quaternion.LookRotation(Vector3.right, Vector3.up));
            }
            // fiches au pied des lits de la salle des malades
            string[] kids = { "No. 3", "No. 4", "No. 8", "No. 9", "No. 11", "No. 14", "No. 15", "No. 17", "No. 19", "No. 20", "No. 22", "No. 23 ?" };
            for (int side = 0; side < 2; side++)
                for (int k = 0; k < 6; k++)
                {
                    float x = side == 0 ? 36.4f : 51.6f, z = -57.5f - k * 3.9f;
                    Vector3 facing = side == 0 ? Vector3.right : Vector3.left;
                    Note(kids[side * 6 + k] + "\nNo visitors.", new Vector3(x + (side == 0 ? 0.1f : -0.1f), HFloor + 0.5f, z + 0.55f), facing, 0.012f, new Vector2(0.32f, 0.3f));   // sous la ligne des yeux
                }
            Debug.Log($"[Horror] Infirmerie habillée : {sheets} feuilles, {framed} dessins, {sc} griffonnages, notes et fiches.");
        }
    }
}
