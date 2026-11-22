using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

namespace Horror.EditorTools
{
    /// <summary>
    /// Menu 10 : l'APK pour le Quest autonome (après les menus 8 et 9).
    ///  1. Configure OpenXR pour Android : chargeur OpenXR, prise en charge Meta Quest, manettes Touch (Quest 2/Pro/3),
    ///     rendu « Single Pass Instanced » (multiview : une seule passe pour les deux yeux, indispensable sur Quest) ;
    ///  2. Vérifie les réglages Android (identifiant, ARM64, IL2CPP, Vulkan, Android 12L minimum) ;
    ///  3. Compile <c>Builds/CalmFaces.apk</c> (dossier ignoré par Git).
    /// Installer sur le casque (mode développeur activé) : <c>adb install -r Builds/CalmFaces.apk</c>,
    /// ou Meta Quest Developer Hub.
    /// </summary>
    public static class QuestBuild
    {
        private const string ScenePath = "Assets/Horror/Scenes/Institut.unity";
        private const string ApkPath = "Builds/CalmFaces.apk";

        [MenuItem("Horror/10 - Construire l'APK Quest (après les menus 8 et 9)")]
        public static void Build()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                Debug.LogError("[Horror] Module Android absent : Unity Hub > Installs > 6000.6 > Add modules > Android Build Support (+ OpenJDK, SDK & NDK).");
                return;
            }
            if (!File.Exists(ScenePath)) { Debug.LogError("[Horror] Pas de scène : lance d'abord le menu 8 puis le 9."); return; }
            ConfigureXr();
            ConfigurePlayer();
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);   // long la 1re fois (textures en ASTC)

            // Chaque APK envoyé à Meta doit avoir un numéro de version (versionCode) plus grand que le précédent
            PlayerSettings.Android.bundleVersionCode = PlayerSettings.Android.bundleVersionCode + 1;
            Debug.Log($"[Horror] APK n° {PlayerSettings.Android.bundleVersionCode} (version {PlayerSettings.bundleVersion}).");
            // Meta refuse un APK signé avec la clé de debug (canaux ALPHA/BETA et store). La clé de publication se crée
            // une fois dans Player > Publishing Settings > Keystore Manager, HORS du dépôt ; ses mots de passe se tapent
            // dans Unity à chaque session, jamais dans le code.
            if (!PlayerSettings.Android.useCustomKeystore)
                Debug.LogWarning("[Horror] APK signé avec la clé de DEBUG : il s'installe sur ton casque, mais Meta le refusera (canal ALPHA). Voir Docs/ALPHA.md.");

            Directory.CreateDirectory(Path.GetDirectoryName(ApkPath));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary s = report.summary;
            if (s.result == BuildResult.Succeeded)
                Debug.Log($"[Horror] APK Quest construit : {ApkPath} ({s.totalSize / (1024f * 1024f):F0} Mo, {s.totalTime.TotalMinutes:F1} min, {s.totalWarnings} avertissements).");
            else
                Debug.LogError($"[Horror] APK Quest : ÉCHEC ({s.result}, {s.totalErrors} erreurs). Voir la console.");
        }

        [MenuItem("Horror/10b - Configurer OpenXR pour le Quest (sans compiler)")]
        public static void ConfigureOnly()
        {
            ConfigureXr();
            ConfigurePlayer();
        }

        /// <summary>OpenXR + Meta Quest pour Android (même chose que la fenêtre XR Plug-in Management, en un clic).</summary>
        private static void ConfigureXr()
        {
            EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.settingsKey, out XRGeneralSettingsPerBuildTarget perTarget);
            if (perTarget == null) { Debug.LogError("[Horror] Réglages XR introuvables (XR Plug-in Management)."); return; }
            if (!perTarget.HasSettingsForBuildTarget(BuildTargetGroup.Android)) perTarget.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.Android);
            if (!perTarget.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android)) perTarget.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
            XRManagerSettings manager = perTarget.ManagerSettingsForBuildTarget(BuildTargetGroup.Android);
            XRGeneralSettings general = perTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
            if (general != null) general.InitManagerOnStart = true;
            XRPackageMetadataStore.AssignLoader(manager, "UnityEngine.XR.OpenXR.OpenXRLoader", BuildTargetGroup.Android);

            UnityEditor.XR.OpenXR.Features.FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
            OpenXRSettings xr = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (xr == null) { Debug.LogError("[Horror] Réglages OpenXR Android introuvables."); return; }
            xr.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
            xr.latencyOptimization = OpenXRSettings.LatencyOptimization.PrioritizeInputPolling;   // mains plus réactives (recommandé par Meta)
            string enabled = "";
            foreach (var f in xr.GetFeatures())
            {
                string n = f.GetType().Name;
                // Meta Quest + manettes Touch (Quest 2 / Pro / 3)
                if (n == "MetaQuestFeature" || n == "OculusTouchControllerProfile" || n == "MetaQuestTouchPlusControllerProfile" || n == "MetaQuestTouchProControllerProfile")
                {
                    f.enabled = true;
                    enabled += n + " ";
                }
            }
            EditorUtility.SetDirty(xr);
            EditorUtility.SetDirty(perTarget);
            AssetDatabase.SaveAssets();
            Debug.Log("[Horror] OpenXR Android : chargeur OpenXR, rendu Single Pass Instanced, " + enabled.Trim() + ".");
        }

        private static void ConfigurePlayer()
        {
            var android = NamedBuildTarget.Android;
            if (PlayerSettings.GetApplicationIdentifier(android).Contains("unity.template"))
                PlayerSettings.SetApplicationIdentifier(android, "com.asdittv.calmfaces");
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            if (PlayerSettings.Android.minSdkVersion < AndroidSdkVersions.AndroidApiLevel32)
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.Vulkan });
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.Android.forceInternetPermission = true;          // pour Photon plus tard
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}
