// 기존 '대상 없는 순찰 Tick' 표본을 보완합니다. 실제 Update에서 여러 적이 같은 대상을 발견하고
// 지도 변경·소음 폭주를 함께 처리합니다. 단순 도형의 렌더 비용은 측정 구간에 포함하지 않습니다.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace NullTrace.AI.Tests
{
    public sealed class CrowdLoadTests
    {
        [UnityTest] public IEnumerator TwentyNpcDiscoveryReplanningAndRadioLoad() { yield return Measure(20); }
        [UnityTest] public IEnumerator FortyNpcDiscoveryReplanningAndRadioLoad() { yield return Measure(40); }

        private static IEnumerator Measure(int count)
        {
            var root = new GameObject("CrowdLoad" + count);
            var profile = ScriptableObject.CreateInstance<EnemyProfile>();
            Sprite sprite = null;
            float oldScale = Time.timeScale, oldCaptureDelta = Time.captureDeltaTime;
            try
            {
                // 게임 시간은 프레임당 1/60초로 고정하지만 측정값은 Stopwatch의 실제 경과 시간을 씁니다.
                // 이 설정은 '이 컴퓨터가 60fps를 달성했다'는 뜻이 아닙니다. 수동 Agent.Tick은 호출하지 않습니다.
                Time.timeScale = 1; Time.captureDeltaTime = 1f / 60f;
                GameObject Child(string name)
                { var child = new GameObject(name); child.transform.SetParent(root.transform); return child; }
                var world = Child("World").AddComponent<GridWorld2D>();
                world.Origin = new Vector2(-10, -8); world.Width = 40; world.Height = 32;
                world.CellSize = 0.5f; world.AgentRadius = 0.15f; world.ObstacleMask = 1 << 8;
                var budget = world.gameObject.AddComponent<PathRequestBudget>();
                budget.RequestsPerFrame = 4;
                var obstacle = Child("ChangingCover"); obstacle.layer = 8;
                obstacle.AddComponent<BoxCollider2D>().size = new Vector2(0.6f, 1.5f);
                obstacle.SetActive(false);
                world.Rebuild();
                var noise = Child("Noise").AddComponent<NoiseBus>();
                var radio = Child("Radio").AddComponent<RadioChannel>();
                var target = Child("VisibleTarget").AddComponent<EnemyTarget>();
                target.transform.position = new Vector2(6, 0);
                // 작은 흰 사각형을 공유하고 색만 바꿉니다. 최종 모델/Animator 부하를 대신하지 않습니다.
                sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 2);
                target.gameObject.AddComponent<SpriteRenderer>().sprite = sprite;
                target.GetComponent<SpriteRenderer>().color = Color.cyan;
                var camera = Child("Camera").AddComponent<Camera>();
                camera.transform.position = new Vector3(0, 0, -10);
                camera.orthographic = true; camera.orthographicSize = 9;
                profile.ViewAngle = 360; profile.DetectionGain = 100;
                profile.DarkViewDistance = profile.DimViewDistance = profile.BrightViewDistance = 30;
                profile.PursuitRadius = 20; profile.EpisodeDuration = 300;
                profile.MoveSpeed = profile.CombatMoveSpeed = 0.2f;
                profile.AttackRange = 1; profile.SuspicionDecayPerSecond = 0;
                var agents = new EnemyAgent[count];
                int attackRequests = 0;
                for (int i = 0; i < count; i++)
                {
                    var go = Child("Npc" + i); go.SetActive(false);
                    go.transform.position = new Vector2(-6 + (i % 5) * 0.35f, -3.5f + (i / 5) * 0.8f);
                    var npc = go.AddComponent<EnemyAgent>();
                    npc.Profile = profile; npc.World = world; npc.Noise = noise; npc.Radio = radio;
                    npc.Target = target; npc.AutoStartPatrol = false;
                    npc.AttackRequested += _ => attackRequests++;
                    var visual = go.AddComponent<SpriteRenderer>(); visual.sprite = sprite; visual.color = Color.green;
                    agents[i] = npc; go.SetActive(true);
                }
                // 가장 이른 Update에서 시계를 시작하고 LateUpdate에서 멈춥니다. 그 사이의 모든 NPC 자동 Update와
                // 무전 전달·지도 갱신·시험 소음 발행을 포함하며, 표본 저장/검사와 렌더는 시계를 멈춘 뒤에 수행합니다.
                var probe = Child("UpdateWindowProbe").AddComponent<CrowdFrameProbe>();
                probe.Agents = agents; probe.World = world; probe.Budget = budget; probe.Radio = radio;
                probe.Noise = noise; probe.Obstacle = obstacle; probe.Target = target;
                while (probe.Samples.Count < 120) yield return null;
                Assert.That(probe.IdentifiedEver.All(value => value), Is.True, "Every NPC must actually identify the shared target during warmup.");
                Assert.That(agents.All(a => a.PathRequestCount > 0), Is.True, "FIFO requests must reach every NPC, not only the first four.");
                int[] before = agents.Select(a => a.PathRequestCount).ToArray();
                while (probe.Samples.Count < 720) yield return null;
                probe.enabled = false;
                var samples = probe.Samples.Skip(120).Take(600).ToArray();
                Assert.That(samples.Length, Is.EqualTo(600));
                Assert.That(samples.Max(s => s.PathRequests), Is.LessThanOrEqualTo(4), "One world may execute at most four A* requests in an actual frame.");
                Assert.That(probe.Samples.Max(s => s.PathPending), Is.GreaterThan(0), "The sample must actually exercise deferred requests.");
                Assert.That(samples.Max(s => s.MemoryMaximum), Is.LessThanOrEqualTo(8));
                Assert.That(samples.Max(s => s.MemoryMaximum), Is.EqualTo(8), "The noise burst must exercise the memory bound.");
                Assert.That(samples.Max(s => s.RadioPending), Is.LessThanOrEqualTo(radio.MaxPendingDeliveries));
                Assert.That(agents.All(a => a.ValidationError == null), Is.True);
                for (int i = 0; i < count; i++)
                    Assert.That(agents[i].PathRequestCount, Is.GreaterThan(before[i]), "Every NPC must replan after shared map changes.");

                // 계속 새 보고를 만드는 시각 입력을 끈 뒤, 대기열이 자연스럽게 비는지 확인합니다.
                // 컴포넌트를 꺼서 큐를 지우면 교착을 숨길 수 있으므로 채널과 NPC는 계속 활성화합니다.
                target.enabled = false;
                float deadline = Time.time + 3;
                while ((budget.PendingCount > 0 || radio.PendingCount > 0) && Time.time < deadline) yield return null;
                Assert.That(budget.PendingCount, Is.Zero, "The path queue must drain without clearing or disabling it.");
                Assert.That(radio.PendingCount, Is.Zero, "The radio queue must deliver or discard obsolete reports without deadlock.");

                double[] times = samples.Select(s => s.Milliseconds).OrderBy(v => v).ToArray();
                var report = new StringBuilder();
                report.AppendLine($"Unity={Application.unityVersion}; OS={SystemInfo.operatingSystem}");
                report.AppendLine($"CPU={SystemInfo.processorType}; logical cores={SystemInfo.processorCount}; GPU={SystemInfo.graphicsDeviceName}");
                report.AppendLine($"NPC={count}; grid=40x32; real shared EnemyTarget; shared sprite quads; automatic Update only; captureDeltaTime=1/60.");
                report.AppendLine("120 warmup + 600 measured frames; every 60 measured frames: toggle cover, Rebuild, publish 16 distinct noises.");
                report.AppendLine(FormattableString.Invariant($"Update-window elapsed ms: p50={times[300]:F4}; p95={times[570]:F4}; max={times[599]:F4}; mean={times.Average():F4}"));
                report.AppendLine($"Path requests/frame max={samples.Max(s => s.PathRequests)}; measured requests={samples.Sum(s => s.PathRequests)}; peak waiting={samples.Max(s => s.PathPending)}; waiting NPC-frames={samples.Sum(s => s.PathPending)}");
                report.AppendLine($"Peak radio pending={samples.Max(s => s.RadioPending)}; per-NPC memory max={samples.Max(s => s.MemoryMaximum)}; received reports={agents.Sum(a => a.RadioReceivedCount)}; attack events={attackRequests}");
                report.AppendLine("Includes Update dispatch/perception/Utility/BT/navigation/collision/radio/Rebuild. Excludes rendering/LateUpdate/record serialization. No allocation claim or fixed ms threshold.");
                report.AppendLine("20/40 are comparison samples, not an agreed NPC maximum. No final map/models/Animator/team combat/real Qwen. Not a final-game FPS guarantee.");
                IntegrationTests.Write($"crowd-load-{count}.txt", report.ToString());
                var rows = new StringBuilder("frame,update_window_ms,path_requests,path_pending,radio_pending,max_memory\n");
                foreach (var sample in samples)
                    rows.AppendLine(string.Join(",", sample.Frame, sample.Milliseconds.ToString("F6", CultureInfo.InvariantCulture), sample.PathRequests, sample.PathPending, sample.RadioPending, sample.MemoryMaximum));
                IntegrationTests.Write($"crowd-load-{count}.csv", rows.ToString());
            }
            finally
            {
                Time.timeScale = oldScale; Time.captureDeltaTime = oldCaptureDelta;
                Object.DestroyImmediate(root); Object.DestroyImmediate(profile);
                if (sprite) Object.DestroyImmediate(sprite);
            }
            LogAssert.NoUnexpectedReceived();
        }
    }

    // 순서를 명시하여 NPC Update 앞에서 측정을 시작합니다. LateUpdate는 모든 Update 뒤에 호출됩니다.
    [DefaultExecutionOrder(-20000)]
    public sealed class CrowdFrameProbe : MonoBehaviour
    {
        public EnemyAgent[] Agents;
        public GridWorld2D World;
        public PathRequestBudget Budget;
        public RadioChannel Radio;
        public NoiseBus Noise;
        public GameObject Obstacle;
        public EnemyTarget Target;
        public readonly List<CrowdFrameSample> Samples = new List<CrowdFrameSample>(720);
        public bool[] IdentifiedEver { get; private set; }
        private readonly Stopwatch watch = new Stopwatch();
        private int requestsBefore;

        private void Update()
        {
            if (Agents == null) return;
            if (IdentifiedEver == null) IdentifiedEver = new bool[Agents.Length];
            requestsBefore = Agents.Sum(a => a.PathRequestCount);
            watch.Restart();
            int frame = Samples.Count;
            if (frame >= 120 && frame < 720 && (frame - 120) % 60 == 0)
            {
                Obstacle.SetActive(!Obstacle.activeSelf);
                World.Rebuild();
                for (int i = 0; i < 16; i++)
                    Noise.Publish((Vector2)Target.transform.position + Vector2.up * (i * 0.01f), 30, 1, NoiseKind.Gunshot);
            }
        }

        private void LateUpdate()
        {
            // 코루틴에서 추가된 첫 프레임에는 Update 없이 LateUpdate부터 올 수 있다.
            // 측정을 시작한 온전한 프레임만 기록하여 초기화 오류와 반쪽 측정값을 함께 막는다.
            if (Agents == null || !watch.IsRunning) return;
            watch.Stop();
            for (int i = 0; i < Agents.Length; i++) IdentifiedEver[i] |= Agents[i].IsTargetIdentified;
            Samples.Add(new CrowdFrameSample
            {
                Frame = Time.frameCount, Milliseconds = watch.Elapsed.TotalMilliseconds,
                PathRequests = Agents.Sum(a => a.PathRequestCount) - requestsBefore,
                PathPending = Budget.PendingCount, RadioPending = Radio.PendingCount,
                MemoryMaximum = Agents.Max(a => a.Memory.Count)
            });
        }
    }

    public struct CrowdFrameSample
    {
        public int Frame, PathRequests, PathPending, RadioPending, MemoryMaximum;
        public double Milliseconds;
    }
}
