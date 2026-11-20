using System.IO;
using Horror.Cosmetics;
using Horror.Interaction;
using Horror.Locomotion;
using Horror.Monsters;
using Horror.Noise;
using Horror.Player;
using UnityEditor;
using UnityEngine;

namespace Horror.EditorTools
{
    /// <summary>
    /// Menu "Horror > 7 - Mettre le vrai personnage et animer le monstre" :
    ///  - remplace les boules de test par la poupée LeDessine (corps sous la tête, bras qui suivent les
    ///    mains en s'étirant, doigts sur gâchette/grip, bouche sur le micro, visage qui gribouille) ;
    ///  - ajoute l'animation saccadée de L'Écoute (MonsterAnimator) si elle est dans la scène.
    /// Il faut le package glTFast (sinon le modèle .glb n'est pas importé). Ctrl+Z annule.
    /// </summary>
    public static class AvatarSetupBuilder
    {
        private const string ModelPath = "Assets/Horror/Art/Character/LeDessine.glb";
        private const string BoilFolder = "Assets/Horror/Art/Face/Boil/";

        [MenuItem("Horror/7 - Mettre le vrai personnage et animer le monstre")]
        public static void Build()
        {
            Undo.SetCurrentGroupName("Personnage et animations");
            int group = Undo.GetCurrentGroup();

            BlindMonsterAI monster = Find<BlindMonsterAI>();
            if (monster != null && monster.GetComponent<MonsterAnimator>() == null)
                Undo.AddComponent<MonsterAnimator>(monster.gameObject);

            ArmLocomotion player = Find<ArmLocomotion>();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (player == null || model == null)
            {
                Undo.CollapseUndoOperations(group);
                EditorUtility.DisplayDialog("Personnage",
                    player == null ? "Pas de joueur : lance d'abord Horror > 3." :
                    "Le modèle " + ModelPath + " n'est pas importé.\nInstalle le package glTFast (Window > Package Manager).",
                    "OK");
                return;
            }
            if (player.GetComponentInChildren<DollAvatarRig>() != null)
            {
                Undo.CollapseUndoOperations(group);
                Debug.Log("[Horror] Le personnage est déjà en place. Animation du monstre ajoutée si besoin.");
                return;
            }

            var doll = (GameObject)PrefabUtility.InstantiatePrefab(model);
            Undo.RegisterCreatedObjectUndo(doll, "Poupée");
            doll.name = "Le Dessiné (corps du joueur)";
            doll.transform.SetParent(player.transform, false);
            SetLayer(doll, player.gameObject.layer);   // les mains ne s'accrochent pas à son propre corps

            DollAvatarRig rig = Undo.AddComponent<DollAvatarRig>(doll);
            rig.head = player.head;
            rig.leftHandTarget = player.leftHandVisual;
            rig.rightHandTarget = player.rightHandVisual;

            // Les boules de test deviennent des cibles invisibles (elles gardent leurs fonctions)
            foreach (Transform t in new[] { player.leftHandVisual, player.rightHandVisual })
                if (t != null && t.GetComponent<Renderer>() != null) { Undo.RecordObject(t.GetComponent<Renderer>(), "Cacher"); t.GetComponent<Renderer>().enabled = false; }

            // Doigts
            SetupFingers(doll, "Main_G", HandFingerAnimator.Side.Gauche, player.leftHandVisual);
            SetupFingers(doll, "Main_D", HandFingerAnimator.Side.Droite, player.rightHandVisual);

            // Bouche + micro (on réutilise le micro existant pour ne jamais ouvrir le micro deux fois)
            Transform bouche = FindDeep(doll.transform, "Bouche");
            MicMouth mic = player.GetComponentInChildren<MicMouth>();
            if (mic == null)
            {
                mic = Undo.AddComponent<MicMouth>(doll);
                VoiceNoise voice = Undo.AddComponent<VoiceNoise>(doll);
                voice.mic = mic;
                voice.mouthPoint = player.head;
            }
            Undo.RecordObject(mic, "Bouche");
            mic.mouth = bouche;
            mic.openDropY = 0.008f;   // en mètres sur le modèle 3D

            // Visage qui gribouille
            FaceScribble scribble = Undo.AddComponent<FaceScribble>(doll);
            scribble.mic = mic;
            Transform mask = FindDeep(doll.transform, "Masque_Papier");
            Transform mouthMesh = FindDeep(doll.transform, "Bouche_Dessin");
            scribble.faceRenderer = mask != null ? mask.GetComponent<Renderer>() : null;
            scribble.mouthRenderer = mouthMesh != null ? mouthMesh.GetComponent<Renderer>() : null;
            scribble.faceTalk = Load("face_parle_");
            scribble.faceScream = Load("face_cri_");
            scribble.mouthTalk = Load("mouth_parle_");

            // Cosmétiques : on accroche les points d'attache au vrai corps
            CosmeticWearer wearer = player.GetComponent<CosmeticWearer>();
            if (wearer != null)
            {
                Undo.RecordObject(wearer, "Attaches");
                wearer.head = Attach(doll, "Tete", "Attache cosmétique Tête", new Vector3(0f, 0.14f, 0f));
                wearer.neck = Attach(doll, "Cou", "Attache cosmétique Cou", Vector3.zero);
                wearer.chest = Attach(doll, "Corps", "Attache cosmétique Poitrine", new Vector3(-0.07f, 0.13f, 0.14f));
                wearer.back = Attach(doll, "Corps", "Attache cosmétique Dos", new Vector3(0f, 0.1f, -0.15f));
            }

            Undo.CollapseUndoOperations(group);
            Selection.activeGameObject = doll;
            Debug.Log("[Horror] Le Dessiné est ton corps. Play : regarde tes mains, appuie sur gâchette et grip, parle.");
        }

        private static void SetupFingers(GameObject doll, string handName, HandFingerAnimator.Side side, Transform grabberHost)
        {
            Transform hand = FindDeep(doll.transform, handName);
            if (hand == null) return;
            HandFingerAnimator fingers = Undo.AddComponent<HandFingerAnimator>(hand.gameObject);
            fingers.side = side;
            if (grabberHost != null)
            {
                HandGrabber grabber = grabberHost.GetComponent<HandGrabber>();
                if (grabber != null) { Undo.RecordObject(grabber, "Doigts"); grabber.fingers = fingers; }
            }
        }

        private static Transform Attach(GameObject doll, string boneName, string name, Vector3 offset)
        {
            Transform bone = FindDeep(doll.transform, boneName);
            if (bone == null) return null;
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, name);
            go.transform.SetParent(bone, false);
            go.transform.localPosition = offset;
            return go.transform;
        }

        private static Texture2D[] Load(string prefix)
        {
            var list = new System.Collections.Generic.List<Texture2D>();
            if (!Directory.Exists(BoilFolder)) return list.ToArray();
            foreach (string f in Directory.GetFiles(BoilFolder, prefix + "*.png"))
            {
                var t = AssetDatabase.LoadAssetAtPath<Texture2D>(f.Replace('\\', '/'));
                if (t != null) list.Add(t);
            }
            list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return list.ToArray();
        }

        private static Transform FindDeep(Transform root, string n)
        {
            if (root.name == n) return root;
            foreach (Transform c in root)
            {
                Transform f = FindDeep(c, n);
                if (f != null) return f;
            }
            return null;
        }

        private static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform t in go.transform) SetLayer(t.gameObject, layer);
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
