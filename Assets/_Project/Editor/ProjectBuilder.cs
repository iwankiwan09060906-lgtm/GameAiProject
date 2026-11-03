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
    // Tools/Build Project 하나로 URP 2D 설정·에셋 카탈로그·씬·빌드 설정을 만든다(3번 규칙: 씬은 손으로 만들지 않는다).
    // 몇 번을 실행해도 같은 결과가 나온다
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
            // 에디터에서 직접 실행하면 저장하지 않은 씬이 날아가지 않게 먼저 묻는다
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            // TMP 기본 리소스(폰트·셰이더)가 있어야 글자를 그릴 수 있다.
            // 패키지 임포트는 비동기라 끝난 뒤 이 함수를 처음부터 다시 부른다
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

        // 프로젝트를 만든 뒤 Windows 실행 파일(Build/Game.exe)까지 만든다
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

            // 자동화(배치 모드)에서는 실패를 종료 코드로 알린다
            if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }

        // URP 2D 렌더러 에셋을 만들어 프로젝트의 렌더 파이프라인으로 지정한다.
        // 이미 있으면 새로 만들지 않고 그대로 쓴다
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

            // 기본값과 품질 단계마다 지정한다. 단계별 설정이 기본값을 덮어쓰기 때문에 모두 맞춰야 한다
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

        // 메인 메뉴 씬: 지금은 게임 씬으로 바로 넘기는 부트스트랩만 있다(Phase 7에서 메뉴로 교체)
        static void CreateMainMenuScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var bootstrap = new GameObject("MainMenuBootstrap");
            bootstrap.AddComponent<MainMenuBootstrap>();
            CreateCamera(null);
            EditorSceneManager.SaveScene(scene, MainMenuPath);
        }

        // 게임 씬: 카메라와 GameManager만 둔다. 나머지(맵, 플레이어, 적, UI)는 GameManager가 실행 중에 만든다
        static void CreateGameScene(GameConfig config)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Camera camera = CreateCamera(config);

            // 새 씬을 열면 앞서 받아 둔 에셋 참조가 끊기므로 여기서 다시 읽는다
            var catalog = AssetDatabase.LoadAssetAtPath<AssetCatalog>(AssetCatalogBuilder.CatalogPath);

            var managerObject = new GameObject("GameManager");
            var manager = managerObject.AddComponent<GameManager>();

            // private 필드는 코드로 직접 못 넣으므로 SerializedObject로 연결한다
            var serialized = new SerializedObject(manager);
            serialized.FindProperty("catalog").objectReferenceValue = catalog;
            serialized.FindProperty("worldCamera").objectReferenceValue = camera;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, GamePath);
        }

        // 정사영 카메라 + 픽셀 퍼펙트 카메라(960x540 기준, PPU 32)
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

        // 빌드에 넣을 씬과 순서(메인 메뉴가 먼저)
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
