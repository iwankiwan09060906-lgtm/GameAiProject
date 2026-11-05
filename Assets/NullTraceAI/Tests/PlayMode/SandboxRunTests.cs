// 역할: 디스크에 저장된 AI Sandbox 씬이 실제로 움직이고, UI/문/대사 연결이 살아 있는지 검증해 증거를 남깁니다.
// 연결: SandboxBuilder가 만든 씬과 SandboxController를 사용합니다. 작은 독립 시나리오는 IntegrationTests에 있습니다.
// 읽는 순서: SavedSandboxRunsCapturesAndControlsWork(종합 실행) → Capture(내부 카메라 촬영)
// → TwentyAgentCpuSample(제한된 조건의 CPU 측정). 결과 파일은 IntegrationTests.Write와 같은 Results 폴더를 씁니다.
// 이 파일의 테스트는 창/마우스/키보드를 조작하지 않습니다. Unity 플레이 루프와 저장된 버튼 이벤트를 직접 사용합니다.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using NullTrace.AI.Demo;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace NullTrace.AI.Tests
{
    // [UnityTest] 메서드는 여러 프레임에 걸쳐 실행됩니다. yield는 검사를 잠시 멈추고 Unity가 다음 프레임을 돌게 합니다.
    public sealed class SandboxRunTests
    {
        // 실제 저장 씬을 입력으로 읽어 순찰→조작→교전→숨기기→초기화까지 확인합니다.
        // 결과는 Assert 통과 여부, 실행 기록 TXT, 순찰/교전 PNG입니다. 코드로만 만든 테스트 씬의 성공과 구분합니다.
        [UnityTest]
        public IEnumerator SavedSandboxRunsCapturesAndControlsWork()
        {
            // Build Settings에 등록된 씬 이름으로 읽습니다. 생성기가 씬을 제대로 저장/등록했는지도 함께 시험합니다.
            yield return SceneManager.LoadSceneAsync("AI Sandbox", LoadSceneMode.Single);
            var demo = Object.FindFirstObjectByType<SandboxController>();
            Assert.That(demo, Is.Not.Null);
            Assert.That(demo.Partner, Is.Not.Null);
            Assert.That(demo.DoorControl, Is.Not.Null, "Rebuild the saved sandbox to serialize the AiDoor2D connection.");
            Assert.That(demo.Agent.Radio, Is.SameAs(demo.Partner.Radio));
            Assert.That(demo.Partner.CurrentDuty, Is.EqualTo(EnemyDuty.Guard));
            // Play 상태에 들어갔다는 사실만으로는 움직인 증거가 아닙니다. 시작 시간·프레임·위치를 따로 보관합니다.
            Vector2 start = demo.Agent.transform.position;
            float startTime = Time.time;
            int startFrame = Time.frameCount;
            yield return new WaitForSeconds(1.2f);
            // 시간이 흘렀고 프레임이 진행됐으며 적 위치도 바뀌어야 통과합니다. 멈춘 화면의 캡처만으로 성공 처리하지 않습니다.
            Assert.That(Time.frameCount, Is.GreaterThan(startFrame + 2));
            Assert.That(Time.time - startTime, Is.GreaterThan(1));
            Assert.That(Vector2.Distance(demo.Agent.transform.position, start), Is.GreaterThan(0.4f));
            Assert.That(demo.Agent.State, Is.EqualTo(EnemyState.Calm));
            Assert.That(demo.Status.text, Does.Contain("Suspicion").And.Contain("Detection").And.Contain("B / square"));
            int run = demo.Agent.PatrolRunId;
            demo.StartPatrol();
            Assert.That(demo.Agent.PatrolRunId, Is.EqualTo(run), "An active finite patrol must not restart.");
            yield return Capture("sandbox-patrol.png");
            var proof = $"Unity={Application.unityVersion}\nGPU={SystemInfo.graphicsDeviceName}\n" +
                $"Start: time={startTime:F3}, frame={startFrame}, position={start}\n" +
                $"Patrol: time={Time.time:F3}, frame={Time.frameCount}, position={(Vector2)demo.Agent.transform.position}\n";
            // 버튼의 onClick을 호출해 씬에 저장된 연결을 검사합니다. 화면 좌표를 클릭하거나 사용자 입력 장치를 제어하지 않습니다.
            // EventSystem 1개와 GraphicRaycaster는 실제 사용자가 버튼을 누를 때 입력이 전달될 기본 구성입니다.
            var buttons = Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None);
            Assert.That(Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Object.FindFirstObjectByType<UnityEngine.UI.GraphicRaycaster>(), Is.Not.Null);
            buttons.Single(b => b.GetComponentInChildren<TMPro.TMP_Text>().text == "1 Dark").onClick.Invoke();
            Assert.That(demo.Player.Light, Is.EqualTo(LightLevel.Dark));
            buttons.Single(b => b.GetComponentInChildren<TMPro.TMP_Text>().text == "2 Dim").onClick.Invoke();
            Assert.That(demo.Player.Light, Is.EqualTo(LightLevel.Dim));
            buttons.Single(b => b.GetComponentInChildren<TMPro.TMP_Text>().text == "3 Bright").onClick.Invoke();
            Assert.That(demo.Player.Light, Is.EqualTo(LightLevel.Bright));
            // 문 그림만 바뀌는 것이 아니라 격자도 새로 만들어졌는지 Revision 증가로 확인합니다.
            int revision = demo.World.Revision;
            buttons.Single(b => b.GetComponentInChildren<TMPro.TMP_Text>().text == "E  /  Open or close AI door").onClick.Invoke();
            Assert.That(demo.DoorControl.IsOpen, Is.True);
            Assert.That(demo.Door.activeSelf, Is.True, "The door connector must remain active when its collider opens.");
            Assert.That(demo.World.Revision, Is.GreaterThan(revision));
            // 숨은 상태에서 낸 소음도 들을 수 있어야 합니다. 숨기기는 소음 발행 자체를 없애는 기능이 아닙니다.
            demo.ToggleHidden(); Assert.That(demo.Player.enabled, Is.False);
            demo.EmitNoise();
            yield return new WaitForSeconds(0.2f);
            Assert.That(demo.Agent.HeardNoiseCount, Is.GreaterThan(0));
            // 교전 검사는 초기화 후 순찰을 멈추고 적 오른쪽 2 월드 단위에 타겟을 놓아 재현 가능한 입력을 만듭니다.
            demo.Agent.PatrolPoints = Array.Empty<Transform>();
            demo.ResetSandbox();
            demo.Player.transform.position = demo.Agent.transform.position + Vector3.right * 2;
            float deadline = Time.time + 4;
            // 감지량 누적을 기다립니다. 공격이 오지 않는 오류가 있어도 영원히 기다리지 않도록 4초 제한을 둡니다.
            while (demo.AttackCount == 0 && Time.time < deadline) yield return null;
            Assert.That(demo.AttackCount, Is.GreaterThan(0));
            Assert.That(demo.Agent.IsTargetIdentified, Is.True);
            Assert.That(demo.SpeechCount, Is.GreaterThan(0));
            // 대사 횟수만 표시하는 것으로 끝내지 않고 실제 문장이 UI 문자열에 들어갔는지도 확인합니다.
            // 한글 모양이 제대로 보이는지는 아래 PNG를 별도로 시각 검수해야 합니다.
            Assert.That(demo.Events.text, Does.Contain(demo.LastSpeech), "Actual Korean speech must be rendered in the debug panel.");
            yield return Capture("sandbox-combat.png");
            proof += $"Combat: time={Time.time:F3}, frame={Time.frameCount}, position={(Vector2)demo.Agent.transform.position}, attacks={demo.AttackCount}, speech={demo.LastSpeech}\n";
            // 교전 중 H 숨기기를 재현합니다. 현재 타겟을 못 보게 돼도 마지막 관측 위치로 수색하는지 검사합니다.
            Vector2 lastClue = demo.Agent.LastKnownPosition;
            demo.ToggleHidden();
            deadline = Time.time + 2;
            while (demo.Agent.CurrentGoal != EnemyGoal.Search && Time.time < deadline) yield return null;
            Assert.That(demo.Agent.State, Is.EqualTo(EnemyState.Search));
            Assert.That(demo.Agent.LastKnownPosition, Is.EqualTo(lastClue));
            proof += $"Hidden: state={demo.Agent.State}, last clue={demo.Agent.LastKnownPosition}\n";
            // 판단 근거를 텍스트로 남겨 결과를 확인하는 사람이 시간·좌표·이벤트를 원본에서 대조할 수 있게 합니다.
            IntegrationTests.Write("sandbox-execution.txt", proof);
            demo.ResetSandbox();
            Assert.That(demo.AttackCount, Is.Zero);
            Assert.That(demo.Player.enabled, Is.True);
            Assert.That(demo.DoorControl.IsOpen, Is.False);
            LogAssert.NoUnexpectedReceived();
            // 다음 테스트에 이 씬의 벽/적이 남지 않게 비어 있는 씬을 활성화한 후 시연 씬을 내립니다.
            Scene saved = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("AfterSandbox"));
            yield return SceneManager.UnloadSceneAsync(saved);
        }

        // PNG 파일명을 받아 메인 카메라의 월드+UI 렌더 결과를 Results에 저장합니다.
        // 모니터 화면 촬영이 아니므로 사용자 포인터나 다른 앱 창이 이미지에 섞이지 않습니다. GPU 렌더링은 필요합니다.
        private static IEnumerator Capture(string file)
        {
            Camera camera = Camera.main;
            Assert.That(camera, Is.Not.Null);
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(UnityEngine.Rendering.GraphicsDeviceType.Null), "Capture needs a graphics device; omit -nographics.");
            // RenderTexture는 GPU가 그림을 그릴 임시 이미지입니다. 마지막 24는 깊이 버퍼 비트 수이며 색 채널 수가 아닙니다.
            var target = new RenderTexture(1280, 720, 24);
            // 카메라의 기존 출력 대상을 보관합니다. 촬영 후 돌려놓아 다음 프레임/검사에 임시 자원이 남지 않게 합니다.
            var previous = camera.targetTexture;
            camera.targetTexture = target;
            target.Create();
            Canvas.ForceUpdateCanvases();
            // Editor batchmode에서는 WaitForEndOfFrame이 다시 진행되지 않습니다.
            // 대신 몇 프레임을 넘겨 정상 URP 렌더 루프가 새 출력 대상에 그리도록 기다립니다.
            yield return null; yield return null; yield return null;
            var previousActive = RenderTexture.active;
            // ReadPixels가 읽을 GPU 이미지를 지정하고 CPU에서 PNG로 변환할 Texture2D에 복사합니다.
            RenderTexture.active = target;
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            texture.Apply();
            byte[] png = texture.EncodeToPNG();
            // 파일 크기는 단색/빈 결과를 의심할 수 있는 거친 검사입니다. UI 잘림·한글 모양까지 자동으로 보증하지는 않습니다.
            // 따라서 저장한 PNG를 사람이 보거나 이미지 도구로 확인하는 절차가 별도로 필요합니다.
            Assert.That(png.Length, Is.GreaterThan(10000), "Capture must contain scene/UI detail, not a flat color.");
            Directory.CreateDirectory(IntegrationTests.ResultDirectory);
            File.WriteAllBytes(Path.Combine(IntegrationTests.ResultDirectory, file), png);
            // 전역 활성 텍스처와 카메라 출력을 원복하고 GPU/Unity 임시 객체를 해제합니다.
            RenderTexture.active = previousActive;
            camera.targetTexture = previous;
            target.Release();
            Object.Destroy(target); Object.Destroy(texture);
        }

        // 20명 적의 Tick을 한꺼번에 처리하는 CPU 시간을 작은 순찰 맵에서 표본으로 측정합니다.
        // 타겟·외형·렌더링을 넣지 않으므로 전체 게임 FPS, 전투 부하, 모든 PC의 2ms 달성을 보장하는 테스트가 아닙니다.
        // 결과 TXT에 장비·격자·호출 방법·평균/상위 표본 시간을 함께 남겨 측정 조건을 숨기지 않습니다.
        [UnityTest]
        public IEnumerator TwentyAgentCpuSample()
        {
            var root = new GameObject("TwentyAgentMeasurement");
            var profile = ScriptableObject.CreateInstance<EnemyProfile>();
            // 한 회차가 측정 중 끝나 정지 상태 비용을 순찰 성능처럼 보고하지 않도록 충분히 긴 이동을 만듭니다.
            profile.MoveSpeed = 1f;
            // 이 테스트는 시간을 일시적으로 멈춥니다. 성공/실패와 관계없이 finally에서 원래 배율을 복구합니다.
            float oldScale = Time.timeScale;
            try
            {
                var world = new GameObject("Grid").AddComponent<GridWorld2D>();
                world.transform.SetParent(root.transform);
                world.Origin = new Vector2(-8, -6); world.Width = 32; world.Height = 24;
                world.CellSize = 0.5f; world.AgentRadius = 0.15f; world.ObstacleMask = 1 << 8;
                var wall = new GameObject("DetourWall"); wall.transform.SetParent(root.transform); wall.layer = 8;
                wall.AddComponent<BoxCollider2D>().size = new Vector2(0.6f, 5);
                world.Rebuild();
                var left = new GameObject("Left").transform; left.SetParent(root.transform); left.position = new Vector2(-5.25f, 0.25f);
                var right = new GameObject("Right").transform; right.SetParent(root.transform); right.position = new Vector2(5.25f, 0.25f);
                var agents = new List<EnemyAgent>();
                // 동일한 프로필/격자를 공유하는 적 20명을 만들고 좌우 순찰점을 줍니다.
                // 서로의 외형이나 적끼리 충돌은 없는 조건이며 중앙 벽을 우회하는 경로 비용을 포함합니다.
                for (int i = 0; i < 20; i++)
                {
                    var go = new GameObject("MeasuredEnemy" + i); go.SetActive(false); go.transform.SetParent(root.transform);
                    go.transform.position = new Vector2(-5.25f, -4.25f + (i % 10) * 0.5f);
                    var agent = go.AddComponent<EnemyAgent>();
                    agent.Profile = profile; agent.World = world; agent.PatrolPoints = new[] { right, left };
                    go.SetActive(true); agents.Add(agent);
                }
                // 자동 Update의 Time.deltaTime을 0으로 만들어 아래 수동 Tick과 진행이 겹치지 않게 합니다.
                // Tick에는 별도로 1/60초를 넣으므로 AI 내부 시간·이동은 계속 진행됩니다.
                Time.timeScale = 0;
                yield return null;
                // dt는 '모의 게임 시간이 한 번에 얼마나 흐르는가'이지 이 컴퓨터가 실제로 60fps라는 뜻이 아닙니다.
                const float dt = 1f / 60f;
                // 최초 경로 생성/코드 실행 준비의 영향을 줄이기 위해 120단계를 먼저 실행한 뒤 측정을 시작합니다.
                for (int frame = 0; frame < 120; frame++) foreach (var agent in agents) agent.Tick(dt);
                var samples = new List<double>();
                var watch = new Stopwatch();
                int activeAgentSteps = 0;
                // Stopwatch는 실제 경과 시간을 잽니다. 샘플 1개는 적 1명이 아니라 20명 전체의 Tick 합계(ms)입니다.
                // 리스트 저장/보고서 작성은 watch.Stop 뒤이므로 해당 샘플의 측정 구간에 들어가지 않습니다.
                for (int frame = 0; frame < 600; frame++)
                {
                    watch.Restart();
                    foreach (var agent in agents) agent.Tick(dt);
                    watch.Stop();
                    samples.Add(watch.Elapsed.TotalMilliseconds);
                    activeAgentSteps += agents.Count(a => a.CurrentDuty != EnemyDuty.Guard);
                }
                // 빠른 순서로 정렬해 중앙값과 95% 지점의 표본을 고릅니다. 평균만으로 가려지는 느린 순간도 함께 보고합니다.
                samples.Sort();
                // 설정 오류로 적이 아무 일도 안 해서 빠른 결과가 나온 것은 아닌지 기본 유효성과 경로 요청을 확인합니다.
                Assert.That(agents.All(a => a.ValidationError == null), Is.True);
                Assert.That(agents.Sum(a => a.PathRequestCount), Is.GreaterThanOrEqualTo(20));
                Assert.That(activeAgentSteps, Is.EqualTo(20 * 600), "Every measured NPC step must still belong to the finite patrol run.");
                // Tick 내부의 감각/FSM/경로/물리는 포함하지만 Unity의 Update 전달 비용과 실제 게임 전체는 포함하지 않습니다.
                // 이번 표본에서 타겟은 없으므로 복잡한 시각 감지/전투까지 같은 시간이라고 확대 해석하면 안 됩니다.
                IntegrationTests.Write("performance-20-agents.txt",
                    $"Unity {Application.unityVersion}; Editor batchmode; {SystemInfo.operatingSystem}\n" +
                    $"CPU: {SystemInfo.processorType}; logical cores: {SystemInfo.processorCount}\n" +
                    "20 active enemies; 32x24 grid; one detour wall; two patrol points; no target; no visuals.\n" +
                    "120 warmup + 600 measured steps; manual public Tick(1/60), timeScale=0 prevents automatic duplicate ticks.\n" +
                    $"Total 20-agent Tick CPU per step: mean={samples.Average():F4} ms; median={samples[300]:F4} ms; p95={samples[570]:F4} ms; max={samples[599]:F4} ms\n" +
                    $"Path requests (including warmup): {agents.Sum(a => a.PathRequestCount)}; allocation measurement omitted.\n" +
                    $"Finite-patrol active NPC steps: {activeAgentSteps}/12000; MoveSpeed=1 world unit/s.\n" +
                    "Includes perception/FSM/navigation/collision in Tick; excludes Update dispatch, rendering, player/build overhead. Not an FPS test or a universal 2 ms guarantee.\n");
            }
            // Assert가 실패해 예외가 나도 시간 배율과 만든 오브젝트는 정리하여 다음 검사에 영향을 남기지 않습니다.
            finally
            {
                Time.timeScale = oldScale;
                Object.DestroyImmediate(root); Object.DestroyImmediate(profile);
            }
            LogAssert.NoUnexpectedReceived();
        }
    }
}
