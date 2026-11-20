#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Horror.Player;

namespace Horror.EditorTools
{
    /// <summary>
    /// Menu "Horror" : construit le prefab du visage et une scène de test, en un clic.
    /// </summary>
    public static class HorrorSetupMenu
    {
        const string FacePng = "Assets/Horror/Art/Face/face.png";
        const string MouthPng = "Assets/Horror/Art/Face/mouth.png";
        const string PrefabDir = "Assets/Horror/Prefabs";
        const string PrefabPath = PrefabDir + "/FaceAvatar.prefab";
        const string SceneDir = "Assets/Horror/Scenes";
        const string ScenePath = SceneDir + "/FaceTest.unity";

        // Position de la bouche dans le dessin (calculée depuis ton croquis, 100 pixels = 1 unité).
        const float MouthLocalX = -0.245f;
        const float MouthLocalY = -0.69f;

        [MenuItem("Horror/1 - Créer l'avatar visage")]
        public static void BuildFacePrefab()
        {
            Sprite face = ImportAsSprite(FacePng);
            Sprite mouth = ImportAsSprite(MouthPng);
            if (face == null || mouth == null)
            {
                Debug.LogError("Horror : face.png ou mouth.png introuvable dans " + "Assets/Horror/Art/Face/");
                return;
            }

            EnsureFolder(PrefabDir);

            var root = new GameObject("FaceAvatar");
            // Le dessin fait ~5,2 unités de large : on le réduit à ~26 cm (taille de tête).
            root.transform.localScale = Vector3.one * 0.05f;

            var faceGo = new GameObject("Face");
            faceGo.transform.SetParent(root.transform, false);
            var faceSr = faceGo.AddComponent<SpriteRenderer>();
            faceSr.sprite = face;
            faceSr.sortingOrder = 0;

            var mouthGo = new GameObject("Mouth");
            mouthGo.transform.SetParent(root.transform, false);
            mouthGo.transform.localPosition = new Vector3(MouthLocalX, MouthLocalY, -0.01f);
            var mouthSr = mouthGo.AddComponent<SpriteRenderer>();
            mouthSr.sprite = mouth;
            mouthSr.sortingOrder = 1;

            var mic = root.AddComponent<MicMouth>();
            mic.mouth = mouthGo.transform;

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Horror : prefab créé -> " + PrefabPath);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        [MenuItem("Horror/2 - Créer la scène de test (bouche)")]
        public static void BuildTestScene()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                BuildFacePrefab();
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (prefab == null) return;
            }

            EnsureFolder(SceneDir);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.1f, 0.16f);
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = new Vector3(0f, 1.6f, 0f);

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var face = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            face.transform.position = new Vector3(0f, 1.6f, 0.6f);
            var mic = face.GetComponent<MicMouth>();
            if (mic != null) mic.showDebug = true;

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("Horror : scène créée -> " + ScenePath + "  (appuie sur Play et parle)");
        }

        static Sprite ImportAsSprite(string path)
        {
            if (!File.Exists(path)) return null;
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) { AssetDatabase.ImportAsset(path); imp = AssetImporter.GetAtPath(path) as TextureImporter; }
            if (imp == null) return null;

            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = 100f;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.filterMode = FilterMode.Bilinear;
            imp.textureCompression = TextureImporterCompression.Uncompressed;

            var settings = new TextureImporterSettings();
            imp.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            imp.SetTextureSettings(settings);
            imp.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace("\\", "/");
            string name = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
