// 역할: 외부 게임 에셋 없이 샌드박스 씬·적 프리팹·샘플 프로필·디버그 UI를 Unity Editor API로 만들어 저장합니다.
// 연결: Runtime의 EnemyAgent/EnemyTarget/GridWorld2D/NoiseBus와 Demo의 두 컴포넌트를 씬에서 연결합니다.
// Editor 전용 assembly에 있으므로 실제 게임 플레이 중에 씬을 만드는 코드가 아닙니다.
// 읽는 순서: ImportTextResources(최초 폰트 준비) → Build(전체 구성 순서) → 아래 도형/UI 생성 도우미.
// Build는 생성 씬과 Build Settings를 다시 저장합니다. 사용자가 따로 편집한 씬을 보존하는 명령은 아닙니다.
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using NullTrace.AI.Demo;

namespace NullTrace.AI.Editor
{
    // static 생성기는 GameObject에 붙이지 않습니다. 메뉴 또는 batchmode의 executeMethod로 직접 실행합니다.
    public static class SandboxBuilder
    {
        private const string Root = "Assets/NullTraceAI";
        // 한 번 준비한 도형·선 머티리얼·기본 폰트를 모든 오브젝트가 공유합니다. 벽마다 새 이미지가 필요하지 않습니다.
        private static Sprite square;
        private static Sprite disc;
        private static Material lineMaterial;
        private static TMP_FontAsset font;

        // 최초 준비용 배치 진입점입니다. 설치된 TMP 패키지에서 Essentials만 가져오고 완료 후 Editor를 종료합니다.
        // ImportResources는 비동기입니다. 함수가 돌아왔다고 가져오기가 끝난 것은 아니므로 완료 알림을 기다립니다.
        // 이 메서드는 직접 EditorApplication.Exit를 호출하므로 일반 편집 도중 실행하는 도구가 아닙니다.
        public static void ImportTextResources()
        {
            // 완료 콜백 안에서 바로 닫지 않고 다음 Editor 갱신으로 미뤄 가져오기 마무리 작업이 끝나게 합니다.
            // 이 진입점에 -quit을 함께 주면 콜백 전에 Editor가 닫혀 폰트가 저장되지 않을 수 있습니다.
            AssetDatabase.importPackageCompleted += _ => EditorApplication.delayCall += () =>
            {
                Debug.Log("NULLTRACE_TMP_RESOURCES_IMPORTED");
                EditorApplication.Exit(0);
            };
            // 실패는 메시지와 종료 코드 1로 전달합니다. 자동 실행이 실패를 성공처럼 처리하지 않게 하기 위함입니다.
            AssetDatabase.importPackageFailed += (_, error) =>
            {
                Debug.LogError(error);
                EditorApplication.Exit(1);
            };
            // 순서대로 Essentials=true, 예제=false, 대화상자=false입니다. 사용자 창이나 마우스 조작이 필요 없습니다.
            TMP_PackageResourceImporter.ImportResources(true, false, false);
        }

        // 필요한 에셋이 준비됐다는 전제에서 씬 전체를 생성하고 저장합니다. 입력은 위 Root 아래 기존 에셋입니다.
        // 결과는 AI Sandbox 씬, EnemySandbox 프리팹, 샘플 Profile과 도형/UI 에셋입니다.
        // 읽을 때 '리소스 준비 → 맵/카메라 → AI 연결 → UI → 저장'의 큰 순서로 보면 됩니다.
        [MenuItem("Null Trace/Rebuild generated sandbox")]
        public static void Build()
        {
            Directory.CreateDirectory(Root + "/Scenes");
            Directory.CreateDirectory(Root + "/Prefabs");
            Directory.CreateDirectory(Root + "/Demo/Generated");
            // Directory는 디스크에 폴더를 만들고 Refresh는 Unity의 에셋 목록에도 그 변경을 알려 줍니다.
            AssetDatabase.Refresh();
            // TMP 리소스도 전달 폴더 아래에 모읍니다. AssetDatabase.MoveAsset은 .meta/GUID 연결을 유지합니다.
            if (AssetDatabase.IsValidFolder("Assets/TextMesh Pro"))
            {
                string error = AssetDatabase.MoveAsset("Assets/TextMesh Pro", Root + "/Demo/TextMesh Pro");
                if (!string.IsNullOrEmpty(error)) throw new System.InvalidOperationException(error);
            }
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root + "/Demo/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            // 기본 폰트가 없으면 표시가 깨진 씬을 저장하는 대신 필요한 준비 단계를 명확히 알리고 중단합니다.
            if (!font) throw new System.InvalidOperationException("Run SandboxBuilder.ImportTextResources once before Build.");
            square = MakeSprite("Square", false);
            disc = MakeSprite("Disc", true);
            lineMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Demo/Generated/DebugLine.mat");
            // 이미 저장된 머티리얼은 재사용합니다. 같은 생성기를 다시 실행해도 새 머티리얼을 계속 만들지 않습니다.
            if (!lineMaterial)
            {
                lineMaterial = new Material(Shader.Find("Sprites/Default"));
                AssetDatabase.CreateAsset(lineMaterial, Root + "/Demo/Generated/DebugLine.mat");
            }
            // 벽만 선택할 레이어를 준비합니다. 8번이 다른 용도로 사용 중이면 덮어쓰지 않고 중단합니다.
            // SerializedObject는 Inspector에 저장되는 프로젝트 설정을 Unity가 인식하는 방식으로 수정하는 도구입니다.
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layer = tags.FindProperty("layers").GetArrayElementAtIndex(8);
            if (string.IsNullOrEmpty(layer.stringValue)) { layer.stringValue = "AIObstacle"; tags.ApplyModifiedProperties(); }
            if (layer.stringValue != "AIObstacle") throw new System.InvalidOperationException("Layer 8 is occupied; choose a free obstacle layer.");

            // 여기부터 빈 씬을 새로 구성합니다. Single은 현재 열린 씬을 대체하므로 생성 전용 작업에만 사용합니다.
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("SandboxCamera", typeof(Camera)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(4.5f, 0, -10);
            // XY 평면을 Z=-10에서 바라보는 원근 없는 카메라입니다. 오른쪽 UI 공간을 위해 중심을 약간 옮깁니다.
            // orthographicSize는 화면에 보이는 세로 범위의 절반(월드 단위)이지 픽셀 해상도가 아닙니다.
            camera.orthographic = true;
            camera.orthographicSize = 8.4f;
            camera.backgroundColor = new Color(0.035f, 0.055f, 0.08f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100;
            // 바닥은 그림만 있고, Wall로 만든 벽에는 Collider2D가 있습니다. 시야/소리/이동은 이 Collider를 검사합니다.
            var floor = Shape("Floor", Vector2.zero, new Vector2(18, 12), new Color(0.08f, 0.12f, 0.16f), false);
            floor.GetComponent<SpriteRenderer>().sortingOrder = -10;
            Wall("North", new Vector2(0, 6), new Vector2(18.5f, 0.5f));
            Wall("South", new Vector2(0, -6), new Vector2(18.5f, 0.5f));
            Wall("West", new Vector2(-9, 0), new Vector2(0.5f, 12));
            Wall("East", new Vector2(9, 0), new Vector2(0.5f, 12));
            Wall("DividerNorth", new Vector2(0, 3), new Vector2(0.5f, 6));
            Wall("DividerSouth", new Vector2(0, -3.75f), new Vector2(0.5f, 4.5f));
            // 문도 같은 벽 도형입니다. 아래에서 AiDoor2D를 연결해 Collider·잠금·격자를 함께 관리합니다.
            GameObject door = Wall("Door", new Vector2(0, -0.75f), new Vector2(0.5f, 1.5f));
            door.GetComponent<SpriteRenderer>().color = new Color(1, 0.65f, 0.2f);
            Wall("Cover", new Vector2(4.5f, 1), new Vector2(3, 0.75f));
            // 원점은 왼쪽 아래, 36×24칸에 한 칸 0.5 월드 단위이므로 전체 범위는 18×12입니다.
            // 1 << 8은 숫자 8을 레이어 번호로 쓰는 것이 아니라 '8번 레이어만 포함하는 비트 마스크'입니다.
            var world = new GameObject("GridWorld").AddComponent<GridWorld2D>();
            world.Origin = new Vector2(-9, -6);
            world.Width = 36; world.Height = 24; world.CellSize = 0.5f;
            world.AgentRadius = 0.15f; world.ObstacleMask = 1 << 8;
            // 계획에서는 일반문을 지나갈 수 있고, NPC가 실제로 다가오면 이 연결부를 통해 문을 엽니다.
            // E 버튼도 같은 API를 사용하므로 잠금·끼임 방지·Revision을 우회하지 않습니다.
            var doorControl = door.AddComponent<AiDoor2D>();
            doorControl.World = world; doorControl.DoorCollider = door.GetComponent<Collider2D>();
            // 모든 벽을 배치한 다음 샘플링해야 처음부터 벽을 피해 갈 수 있는 격자가 만들어집니다.
            world.Rebuild();
            var noise = new GameObject("NoiseBus").AddComponent<NoiseBus>();
            var radio = new GameObject("DelayedRadio").AddComponent<RadioChannel>();
            // 위치·생존·조도만 제공하는 테스트 대상입니다. 팀의 이동/전투 시스템은 이곳에 구현하지 않습니다.
            // Visual을 자식으로 분리하면 나중에 그림을 바꿔도 EnemyTarget이 붙은 위치 기준은 유지됩니다.
            var targetRoot = new GameObject("PlayerTarget_TEST_ONLY");
            targetRoot.transform.position = new Vector2(5, -3);
            var target = targetRoot.AddComponent<EnemyTarget>();
            Shape("Visual", Vector2.zero, Vector2.one * 0.5f, new Color(0.25f, 0.95f, 1f), true, targetRoot.transform);
            var profile = AssetDatabase.LoadAssetAtPath<EnemyProfile>(Root + "/Demo/SandboxEnemyProfile.asset");
            // 기존 프로필을 발견하면 사용자가 조절한 튜닝값을 그대로 씁니다. 없을 때만 기본 프로필을 생성합니다.
            if (!profile)
            {
                profile = ScriptableObject.CreateInstance<EnemyProfile>();
                AssetDatabase.CreateAsset(profile, Root + "/Demo/SandboxEnemyProfile.asset");
            }
            var enemyRoot = new GameObject("Enemy_Sandbox");
            // 컴포넌트를 붙이고 연결하는 동안 켜지지 않도록 합니다. 일반적인 런타임 생성에서도
            // OnEnable이 참조 연결보다 먼저 실행되지 않게 비활성 상태에서 준비하는 순서가 중요합니다.
            enemyRoot.SetActive(false);
            enemyRoot.transform.position = new Vector2(-5, -3);
            var enemy = enemyRoot.AddComponent<EnemyAgent>();
            enemy.Profile = profile;
            // 제공자 없이도 기본 대사가 나옵니다. 실제 모델은 팀에서 Provider를 붙일 때만 사용합니다.
            enemy.Dialogue = enemyRoot.AddComponent<EnemyDialogue>();
            var visual = Shape("Visual", Vector2.zero, Vector2.one * 0.55f, Color.green, true, enemyRoot.transform).GetComponent<SpriteRenderer>();
            var debug = enemyRoot.AddComponent<EnemyDebugView>();
            debug.Agent = enemy; debug.Visual = visual;
            debug.Facing = Line("Facing", enemyRoot.transform, Color.green, 0.06f);
            debug.Clue = Line("LastClue", enemyRoot.transform, new Color(0.8f, 0.5f, 1f, 0.6f), 0.025f);
            debug.Shot = Line("AttackRequest", enemyRoot.transform, new Color(1f, 0.3f, 0.2f), 0.06f);
            enemyRoot.SetActive(true);
            // 프리팹은 여러 씬에서 재사용할 원본이므로 특정 씬의 World/Noise/Target/순찰점을 넣지 않습니다.
            // 프리팹 저장 후 이 씬의 인스턴스에만 연결합니다. 이 생성기는 Editor에서 실행하며 플레이 Update는 돌지 않습니다.
            PrefabUtility.SaveAsPrefabAssetAndConnect(enemyRoot, Root + "/Prefabs/EnemySandbox.prefab", InteractionMode.AutomatedAction);
            enemy.World = world; enemy.Noise = noise; enemy.Target = target; enemy.Radio = radio;
            enemy.PatrolPoints = new[] { Patrol(new Vector2(-5, -3)), Patrol(new Vector2(-5, 3)), Patrol(new Vector2(-2, 3)), Patrol(new Vector2(-2, -3)) };
            // 원본 프리팹과 다른 이 씬만의 연결을 인스턴스 변경값으로 기록해 씬 재로드 후에도 유지합니다.
            PrefabUtility.RecordPrefabInstancePropertyModifications(enemy);

            // B는 오른쪽 초소를 지키므로 시작부터 A와 서로 다른 지식을 가집니다.
            // 같은 무전 채널을 쓰더라도 보고가 도착하기 전에는 상대의 기억을 읽을 수 없습니다.
            var partnerRoot = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/EnemySandbox.prefab"));
            partnerRoot.name = "Enemy_B_Guard";
            partnerRoot.transform.position = new Vector2(5, 3);
            var partner = partnerRoot.GetComponent<EnemyAgent>();
            partner.World = world; partner.Noise = noise; partner.Target = target; partner.Radio = radio;
            partner.AutoStartPatrol = false;
            partnerRoot.GetComponent<EnemyDebugView>().Visual.sprite = square;
            PrefabUtility.RecordPrefabInstancePropertyModifications(partner);
            PrefabUtility.RecordPrefabInstancePropertyModifications(partnerRoot.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(partnerRoot.GetComponent<EnemyDebugView>().Visual);

            // 마지막으로 시연 조작기와 UI를 연결합니다. 저장 씬을 Play하면 Awake/OnEnable이 이 연결값을 사용합니다.
            var controller = new GameObject("SandboxControls").AddComponent<SandboxController>();
            controller.Player = target; controller.Agent = enemy; controller.Partner = partner;
            controller.World = world; controller.Noise = noise; controller.Door = door;
            controller.DoorControl = doorControl;
            BuildUI(camera, controller);
            // 버튼 그림만 있어서는 클릭이 전달되지 않습니다. EventSystem과 Input System용 입력 모듈이 입력을 전달합니다.
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            EditorSceneManager.SaveScene(scene, Root + "/Scenes/AI Sandbox.unity");
            // 테스트가 씬 이름으로 로드할 수 있도록 등록합니다. 기존 Build Settings 목록을 교체하는 생성 전용 동작입니다.
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Root + "/Scenes/AI Sandbox.unity", true) };
            AssetDatabase.SaveAssets();
            Debug.Log("NULLTRACE_SANDBOX_SAVED: scene, profile, prefab, UI and generated shapes");
        }

        // 이름과 원 여부를 받아 32×32 PNG를 만들고 Unity Sprite로 가져옵니다. 반환값은 씬에서 공유할 Sprite입니다.
        // 파일이 이미 있으면 픽셀을 다시 쓰지 않고 가져오기 설정만 확인합니다.
        private static Sprite MakeSprite(string name, bool circle)
        {
            string path = Root + "/Demo/Generated/" + name + ".png";
            if (!File.Exists(path))
            {
                var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
                // 픽셀 중심(x+0.5, y+0.5)과 이미지 중심(16,16)의 거리를 사용합니다.
                // 원이면 반경 15픽셀 밖을 투명하게, 사각형이면 모두 흰색으로 채웁니다. 실제 색은 Renderer가 곱합니다.
                for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                    texture.SetPixel(x, y, circle && Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(16, 16)) > 15 ? Color.clear : Color.white);
                // 계산한 픽셀을 적용해 PNG로 저장한 뒤 편집 중에 만든 임시 Texture 객체를 정리합니다.
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
            }
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            // 32픽셀이 월드 1단위가 되므로 32×32 도형의 기본 크기는 1×1입니다.
            // Point/무압축은 단순 도형 테두리가 흐려지는 것을 피하는 시연 설정입니다.
            importer.spritePixelsPerUnit = 32;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // 도형의 이름·부모 기준 위치·크기·색·모양을 받아 SpriteRenderer가 있는 GameObject를 돌려줍니다.
        // 충돌은 추가하지 않으므로 바닥/표시점/외형에 공통으로 쓸 수 있습니다. 벽은 아래 Wall에서 충돌을 붙입니다.
        private static GameObject Shape(string name, Vector2 position, Vector2 scale, Color color, bool circle, Transform parent = null)
        {
            var go = new GameObject(name);
            // false는 새 부모 기준 로컬 좌표를 사용한다는 뜻입니다. 예: Visual의 (0,0)은 적 루트의 중심입니다.
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = circle ? disc : square;
            renderer.color = color;
            return go;
        }

        // 이름·위치·크기로 벽 도형을 만들고 장애물 레이어와 사각 Collider2D를 붙여 반환합니다.
        // 기본 1×1 Collider도 Transform의 scale을 따르므로 도형과 실제 벽의 크기가 맞습니다.
        private static GameObject Wall(string name, Vector2 position, Vector2 scale)
        {
            var go = Shape(name, position, scale, new Color(0.3f, 0.4f, 0.48f), false);
            go.layer = 8;
            go.AddComponent<BoxCollider2D>();
            return go;
        }

        // 순찰 목적지 좌표를 작은 점으로 표시하고 그 Transform을 반환합니다. 점 자체는 길을 막지 않습니다.
        private static Transform Patrol(Vector2 position)
        {
            var go = Shape("PatrolPoint", position, Vector2.one * 0.15f, new Color(0.2f, 0.4f, 0.3f), true);
            return go.transform;
        }

        // 부모·색·굵기(월드 단위)를 받아 두 점을 잇는 디버그 선을 만듭니다.
        // 실제 양 끝 좌표는 EnemyDebugView가 매 프레임 넣습니다. 이 도우미는 표시 자원만 준비합니다.
        private static LineRenderer Line(string name, Transform parent, Color color, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = lineMaterial;
            // 부모가 적과 함께 이동해도 좌표를 두 번 더하지 않도록 양 끝을 월드 좌표로 받습니다.
            line.positionCount = 2; line.useWorldSpace = true;
            line.startWidth = line.endWidth = width;
            line.startColor = line.endColor = color;
            line.sortingOrder = 5;
            return line;
        }

        // 카메라와 조작기를 받아 uGUI 패널을 만들고 텍스트 참조/버튼 명령을 연결합니다.
        // 완성 게임 HUD가 아니라 상태와 시험 입력을 모아 놓은 디버그 도구입니다.
        private static void BuildUI(Camera camera, SandboxController controller)
        {
            var canvas = new GameObject("DebugCanvas", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster)).GetComponent<Canvas>();
            // 카메라가 UI도 렌더하게 해야 RenderTexture 내부 캡처에 월드와 패널이 함께 들어갑니다.
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera; canvas.planeDistance = 1;
            var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            // UI 배치의 기준 해상도입니다. 다른 화면 크기에서는 CanvasScaler가 폭/높이를 균형 있게 반영합니다.
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            var panel = new GameObject("DebugPanel", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.VerticalLayoutGroup));
            panel.transform.SetParent(canvas.transform, false);
            var rect = panel.GetComponent<RectTransform>();
            // 두 NPC의 수치를 비교할 수 있게 오른쪽 34%를 패널로 사용합니다.
            rect.anchorMin = new Vector2(0.66f, 0); rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            panel.GetComponent<UnityEngine.UI.Image>().color = new Color(0.035f, 0.055f, 0.08f, 0.98f);
            var layout = panel.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 10, 10);
            layout.spacing = 4; layout.childControlHeight = true; layout.childForceExpandHeight = false;
            // 자식 폭을 레이아웃이 관리해야 기본 100픽셀 폭에 갇혀 버튼/텍스트가 불필요하게 줄바꿈되지 않습니다.
            layout.childControlWidth = true; layout.childForceExpandWidth = true;
            Text(panel.transform, "NULL TRACE / AI LAB", 23, 28, new Color(0.25f, 0.95f, 1));
            Text(panel.transform, "Finite patrol / personal memory / delayed radio", 14, 18, Color.white);
            controller.Status = Text(panel.transform, "Waiting for simulation...", 14, 238, Color.white);
            controller.Events = Text(panel.transform, "Requests", 14, 42, new Color(1, 0.8f, 0.4f));
            Button(panel.transform, "SPACE  /  Emit noise", controller.EmitNoise);
            // 조도 세 버튼만 가로로 묶습니다. 전체 패널의 세로 배치 안에 가로 배치 하나가 들어가는 구조입니다.
            var lights = new GameObject("LightLevels", typeof(RectTransform), typeof(UnityEngine.UI.HorizontalLayoutGroup), typeof(UnityEngine.UI.LayoutElement));
            lights.transform.SetParent(panel.transform, false);
            lights.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 30;
            var lightLayout = lights.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            lightLayout.spacing = 6;
            lightLayout.childControlWidth = lightLayout.childControlHeight = true;
            lightLayout.childForceExpandWidth = lightLayout.childForceExpandHeight = true;
            // PersistentListener는 씬에 저장되는 연결입니다. 단순 런타임 람다 등록과 달리 재로드 후에도 남습니다.
            // 같은 SetLight 함수에 서로 다른 정수 0/1/2를 전달해 조도 단계만 바꿉니다.
            UnityEditor.Events.UnityEventTools.AddIntPersistentListener(Button(lights.transform, "1 Dark", null).onClick, controller.SetLight, 0);
            UnityEditor.Events.UnityEventTools.AddIntPersistentListener(Button(lights.transform, "2 Dim", null).onClick, controller.SetLight, 1);
            UnityEditor.Events.UnityEventTools.AddIntPersistentListener(Button(lights.transform, "3 Bright", null).onClick, controller.SetLight, 2);
            Button(panel.transform, "H  /  Hide or reveal target", controller.ToggleHidden);
            Button(panel.transform, "E  /  Open or close AI door", controller.ToggleDoor);
            Button(panel.transform, "P  /  Start next patrol run (A)", controller.StartPatrol);
            Button(panel.transform, "R  /  Reset sandbox", controller.ResetSandbox);
            Text(panel.transform, "WASD / arrows: move cyan target\nA circle: patrol / B square: guard\nGreen calm / yellow inspect / purple search\nRed combat / blue return or clue wait / gray blocked", 13, 62, new Color(0.7f, 0.8f, 0.85f));
        }

        // 부모·문구·글자 크기·원하는 높이·색을 받아 TMP 텍스트를 만들고 갱신 가능한 참조를 반환합니다.
        // LayoutElement의 preferredHeight는 부모 LayoutGroup에 요청하는 높이이며 항상 절대 크기를 강제하지는 않습니다.
        private static TMP_Text Text(Transform parent, string content, int size, float height, Color color)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(UnityEngine.UI.LayoutElement));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = font; text.text = content; text.fontSize = size; text.color = color;
            // 글자 자체가 클릭을 가로채지 않게 해 뒤의 버튼 Image가 입력을 받도록 합니다.
            text.raycastTarget = false;
            text.overflowMode = TextOverflowModes.Overflow;
            go.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = height;
            return text;
        }

        // 부모·표시 문구·클릭 함수를 받아 버튼을 만들고 반환합니다. action이 null이면 호출자가 나중에 인자 있는 명령을 연결합니다.
        // UI Image가 클릭 영역, TMP 자식이 글자, Button 컴포넌트가 클릭 명령을 담당합니다.
        private static UnityEngine.UI.Button Button(Transform parent, string label, UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject("Button", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button), typeof(UnityEngine.UI.LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<UnityEngine.UI.Image>().color = new Color(0.12f, 0.22f, 0.29f);
            go.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 30;
            var button = go.GetComponent<UnityEngine.UI.Button>();
            // 이 연결도 씬에 저장합니다. Space/H/E/R 입력과 동일한 SandboxController 메서드를 사용합니다.
            if (action != null) UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, action);
            var text = Text(go.transform, label, 15, 36, Color.white);
            text.alignment = TextAlignmentOptions.Center;
            var rect = text.rectTransform;
            // 버튼 내부 글자가 버튼 전체를 채우게 늘린 뒤 가운데 정렬합니다. 글자는 클릭 판정을 방해하지 않습니다.
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return button;
        }
    }
}
