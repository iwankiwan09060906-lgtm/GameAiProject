using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace GameAi.EditorTools
{
    // Tools/Build Project 하나로 URP 2D 설정·카탈로그·씬·빌드 설정을 만든다
    public static class ProjectBuilder
    {
        const string SettingsFolder = "Assets/Settings";
        const string PipelinePath = SettingsFolder + "/UniversalRP.asset";
        const string RendererPath = SettingsFolder + "/Renderer2D.asset";
        const string ScenesFolder = "Assets/_Project/Scenes";
        const string MainMenuPath = ScenesFolder + "/MainMenu.unity";
        const string GamePath = ScenesFolder + "/Game.unity";
        const string TmpFolder = "Assets/TextMesh Pro";
        const string TmpPackage = "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";
        const string BuildOutput = "Build/Game.exe";

        [MenuItem("Tools/Build Project")]
        public static void BuildProject()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            // 패키지 임포트는 비동기라 끝나면 이어서 다시 실행한다
            if (!AssetDatabase.IsValidFolder(TmpFolder))
            {
                AssetDatabase.importPackageCompleted += ResumeAfterTextMeshPro;
                AssetDatabase.ImportPackage(Path.GetFullPath(TmpPackage), false);
                return;
            }

            var config = ConfigLoader.Load<GameConfig>("game.json");
            EnsureRenderPipeline();
            AssetCatalogBuilder.Build();
            CreateMainMenuScene();
            CreateGameScene(config);
            RegisterScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[ProjectBuilder] 프로젝트 생성을 마쳤습니다.");
        }

        [MenuItem("Tools/Build Windows Player")]
        public static void BuildWindowsPlayer()
        {
            BuildProject();
            var options = new BuildPlayerOptions
            {
                scenes = new[] { MainMenuPath, GamePath },
                locationPathName = BuildOutput,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log("[ProjectBuilder] 빌드 결과: " + report.summary.result + ", 오류 " + report.summary.totalErrors);
            if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }

        static void EnsureRenderPipeline()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                Directory.CreateDirectory(SettingsFolder);
                var renderer = AssetDatabase.LoadAssetAtPath<Renderer2DData>(RendererPath);
                if (renderer == null)
                {
                    renderer = ScriptableObject.CreateInstance<Renderer2DData>();
                    AssetDatabase.CreateAsset(renderer, RendererPath);
                }
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;
            int current = QualitySettings.GetQualityLevel();
            for (int level = 0; level < QualitySettings.names.Length; level++)
            {
                QualitySettings.SetQualityLevel(level, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(current, false);
        }

        static void ResumeAfterTextMeshPro(string packageName)
        {
            AssetDatabase.importPackageCompleted -= ResumeAfterTextMeshPro;
            BuildProject();
        }

        static void CreateMainMenuScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var bootstrap = new GameObject("MainMenuBootstrap");
            bootstrap.AddComponent<MainMenuBootstrap>();
            CreateCamera(null);
            EditorSceneManager.SaveScene(scene, MainMenuPath);
        }

        static void CreateGameScene(GameConfig config)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Camera camera = CreateCamera(config);

            // 새 씬을 열면 앞서 받은 참조가 끊기므로 여기서 다시 읽는다
            var catalog = AssetDatabase.LoadAssetAtPath<AssetCatalog>(AssetCatalogBuilder.CatalogPath);

            var managerObject = new GameObject("GameManager");
            var manager = managerObject.AddComponent<GameManager>();
            var serialized = new SerializedObject(manager);
            serialized.FindProperty("catalog").objectReferenceValue = catalog;
            serialized.FindProperty("worldCamera").objectReferenceValue = camera;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, GamePath);
        }

        static Camera CreateCamera(GameConfig config)
        {
            config = config ?? new GameConfig();
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.position = new Vector3(0f, 0f, -10f);

            var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = config.camera_ortho_size;
            camera.backgroundColor = Color.black;
            camera.clearFlags = CameraClearFlags.SolidColor;
            go.AddComponent<UniversalAdditionalCameraData>();

            var pixelPerfect = go.AddComponent<PixelPerfectCamera>();
            pixelPerfect.assetsPPU = config.pixels_per_unit;
            pixelPerfect.refResolutionX = config.pixel_reference_resolution[0];
            pixelPerfect.refResolutionY = config.pixel_reference_resolution[1];
            pixelPerfect.gridSnapping = PixelPerfectCamera.GridSnapping.UpscaleRenderTexture;
            return camera;
        }

        static void RegisterScenes()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainMenuPath, true),
                new EditorBuildSettingsScene(GamePath, true),
            };
        }
    }
}
