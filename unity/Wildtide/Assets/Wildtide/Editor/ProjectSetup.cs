using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Wildtide.EditorTools
{
    /// <summary>
    /// One-click (and first-open) setup: mobile player settings for iOS and Android, a URP asset, and the Main scene.
    /// Run again any time from the Wildtide menu.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        public const string BundleId = "com.witclad.wildtide";
        public const string ScenePath = "Assets/Scenes/Main.unity";
        const string SettingsFolder = "Assets/Wildtide/Settings";
        const string DoneKey = "Wildtide.SetupDone";

        static ProjectSetup()
        {
            // Run once per project after the first import finishes.
            EditorApplication.delayCall += () =>
            {
                if (Application.isBatchMode) return; // CI builds call Run() themselves (see CiBuild)
                if (PlayerSettings.productName == "Wildtide" && File.Exists(ScenePath) && GraphicsSettings.defaultRenderPipeline != null) return;
                if (SessionState.GetBool(DoneKey, false)) return;
                SessionState.SetBool(DoneKey, true);
                Run();
            };
        }

        [MenuItem("Wildtide/Set Up Project for iOS + Android")]
        public static void Run()
        {
            ConfigurePlayer();
            ConfigureRenderPipeline();
            ConfigureScene();
            AssetDatabase.SaveAssets();
            Debug.Log("Wildtide: project set up for iOS and Android. Open Assets/Scenes/Main.unity and press Play.");
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Witclad";
            PlayerSettings.productName = "Wildtide";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.colorSpace = ColorSpace.Linear;

            // Landscape only, either way up.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            // iOS
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.buildNumber = "1";
            PlayerSettings.statusBarHidden = true;

            // Android: Google Play requires 64-bit, which needs IL2CPP.
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, BundleId);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.bundleVersionCode = 1;
        }

        static void ConfigureRenderPipeline()
        {
            if (GraphicsSettings.defaultRenderPipeline != null) return;
            if (!AssetDatabase.IsValidFolder(SettingsFolder)) AssetDatabase.CreateFolder("Assets/Wildtide", "Settings");
            var rendererPath = SettingsFolder + "/Wildtide Renderer.asset";
            var pipelinePath = SettingsFolder + "/Wildtide URP.asset";

            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, rendererPath);
            }
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                pipeline.shadowDistance = 90f; // the isometric camera sits 40 units back from the player
                pipeline.msaaSampleCount = 4;
                AssetDatabase.CreateAsset(pipeline, pipelinePath);
            }
            GraphicsSettings.defaultRenderPipeline = pipeline;
            // Make sure no quality level overrides it with a different (or missing) pipeline.
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(current, false);
        }

        static void ConfigureScene()
        {
            if (!File.Exists(ScenePath))
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
                // Empty on purpose: GameRoot builds everything from code when you press Play.
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            if (SceneManager.GetActiveScene().path != ScenePath) EditorSceneManager.OpenScene(ScenePath);
        }
    }
}
