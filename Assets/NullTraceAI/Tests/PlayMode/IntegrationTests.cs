// 역할: 실제 Unity 플레이 루프와 Collider2D를 사용해 감각→상태→이동→출력 이벤트의 연결을 검사합니다.
// 연결: Runtime 전체를 작은 테스트용 맵에 묶습니다. 저장된 시연 씬 검사는 별도 SandboxRunTests가 담당합니다.
// 읽는 순서: SetUp/TearDown(매 검사 준비/정리) → Child/Wall/Until(도우미) → 이름별 검증 시나리오.
// [UnityTest]의 IEnumerator는 여러 프레임에 걸친 검사입니다. yield return null 동안 Unity가 다음 프레임을 실행합니다.
// Assert는 '이 조건이 사실이어야 통과'라는 검사입니다. 이 파일은 적의 행동을 구현하지 않고 구현 결과를 확인합니다.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NullTrace.AI.Tests
{
    // 각 테스트는 새 적·타겟·프로필·격자를 받습니다. 앞 테스트에서 본 대상이나 벽이 다음 결과에 섞이지 않게 합니다.
    public sealed partial class IntegrationTests
    {
        private GameObject root;
        private GridWorld2D world;
        private NoiseBus noise;
        private EnemyProfile profile;
        private EnemyTarget target;
        private EnemyAgent agent;
        private int attacks;

        // NUnit이 매 테스트 전에 호출합니다. 작은 맵과 빠른 테스트용 튜닝을 만들고 필수 참조를 연결합니다.
        // 입력 인자는 없으며, 결과는 이 클래스 필드에 보관된 서로 연결된 테스트 오브젝트들입니다.
        [SetUp]
        public void SetUp()
        {
            root = new GameObject("IntegrationFixture");
            world = Child("Grid").AddComponent<GridWorld2D>();
            world.Origin = new Vector2(-8, -6); world.Width = 32; world.Height = 24;
            world.CellSize = 0.5f; world.AgentRadius = 0.15f; world.ObstacleMask = 1 << 8;
            world.Rebuild();
            noise = Child("Noise").AddComponent<NoiseBus>();
            target = Child("Target").AddComponent<EnemyTarget>();
            target.transform.position = new Vector2(2, 0);
            profile = ScriptableObject.CreateInstance<EnemyProfile>();
            // 시야를 360도로 두어 방향 때문에 다른 검사가 우연히 실패하지 않게 합니다.
            // 짧은 대기/수색 시간은 테스트를 빠르게 반복하기 위한 값이며 최종 게임의 권장 밸런스가 아닙니다.
            profile.ViewAngle = 360; profile.DetectionGain = 5;
            profile.InspectDuration = 0.15f; profile.EpisodeDuration = 3f;
            profile.SearchRadius = 0.5f; profile.SearchPointDuration = 0.1f;
            profile.MoveSpeed = 4; profile.CombatMoveSpeed = 4;
            profile.AttackRange = 4; profile.AttackInterval = 0.1f; profile.RepathInterval = 0.05f;
            // PlayMode에서는 활성화 시 OnEnable이 실행됩니다. 참조를 먼저 채우고 켜야 준비되지 않은 적이 시작하지 않습니다.
            var go = Child("Enemy"); go.SetActive(false);
            agent = go.AddComponent<EnemyAgent>();
            agent.Profile = profile; agent.World = world; agent.Noise = noise; agent.Target = target;
            // 실제 공격 대신 이벤트 횟수를 셉니다. 대상 체력을 줄이지 않아도 '발사 요청이 있었는가'를 검증할 수 있습니다.
            agent.AttackRequested += _ => attacks++;
            attacks = 0;
            go.SetActive(true);
        }

        // 매 테스트가 끝나면 성공/실패와 관계없이 호출됩니다. 공통 부모와 임시 프로필을 지워 다음 테스트를 분리합니다.
        // 예상하지 못한 오류 로그도 확인하므로 화면에 예외가 났는데 Assert만 통과한 결과를 놓치지 않습니다.
        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(profile);
            Time.timeScale = 1;
            LogAssert.NoUnexpectedReceived();
        }

        // 이름을 받아 공통 root 아래 오브젝트를 만듭니다. root 하나를 지우면 벽/적/타겟도 함께 정리됩니다.
        private GameObject Child(string name)
        {
            var go = new GameObject(name); go.transform.SetParent(root.transform); return go;
        }

        // 월드 위치와 크기를 받아 실제 사각 Collider2D 벽을 만듭니다. 새 벽을 A* 격자에도 반영한 뒤 반환합니다.
        private GameObject Wall(Vector2 position, Vector2 size)
        {
            var go = Child("Wall"); go.layer = 8; go.transform.position = position;
            go.AddComponent<BoxCollider2D>().size = size;
            world.Rebuild(); return go;
        }

        // condition이 참이 될 때까지 최대 timeout 게임 초 동안 기다립니다. 제한 시간에 도달하면 message로 실패 이유를 알립니다.
        // 고정 프레임 수 대신 상태를 기다리므로 컴퓨터마다 프레임률이 달라도 검사 의도는 같습니다.
        private static IEnumerator Until(Func<bool> condition, float timeout, string message)
        {
            float end = Time.time + timeout;
            while (!condition() && Time.time < end) yield return null;
            Assert.That(condition(), Is.True, message);
        }

        // 보이는 대상 입력으로 경계/교전/요청이 나오고, 시야를 잃으면 옛 단서를 수색한 뒤 평시로 돌아오는지 검사합니다.
        // 최종 상태만 보지 않고 StateChanged 순서와 공격 횟수도 함께 확인해 중간 상태가 생략되지 않았는지 봅니다.
        [UnityTest]
        public IEnumerator VisionToCombatLossSearchAndCalm()
        {
            var states = new List<EnemyState> { agent.State };
            agent.StateChanged += (_, next) => states.Add(next);
            yield return Until(() => attacks > 0, 3, "Visible target must produce an attack request.");
            Assert.That(agent.Suspicion, Is.GreaterThanOrEqualTo(90), "Direct identification must raise personal suspicion.");
            Assert.That(agent.State, Is.EqualTo(EnemyState.Combat));
            Vector2 clue = agent.LastKnownPosition;
            // 살아 있는 대상을 멀리 옮겨 '시야 상실'만 만듭니다. AI는 새 위치 (20,20)를 보지 못했으므로
            // 그 좌표를 LastKnownPosition에 넣으면 안 됩니다. 아래 비교는 이전에 본 위치가 유지되는지 확인합니다.
            target.transform.position = new Vector2(20, 20);
            int requests = attacks;
            yield return Until(() => agent.State == EnemyState.Search, 2, "Lost sight must enter Search.");
            Assert.That(agent.LastKnownPosition, Is.EqualTo(clue));
            yield return Until(() => agent.State == EnemyState.Calm, 5, "Completed search must return to Calm.");
            Assert.That(attacks, Is.EqualTo(requests), "No attack may be requested after losing sight.");
            Assert.That(states, Does.Contain(EnemyState.Search));
            Write("fsm-flow.txt", string.Join(" -> ", states));
        }

        // 벽이 처음부터 있는 경우와 교전 도중 다시 생긴 경우를 모두 검사합니다.
        // '한 번 적을 봤으니 벽 뒤에서도 사격한다'는 오류는 두 번째 구간에서 잡힙니다.
        [UnityTest]
        public IEnumerator WallBlocksDetectionAndFireEvenAfterCombat()
        {
            var wall = Wall(new Vector2(1, 0), new Vector2(0.5f, 8));
            yield return new WaitForSeconds(0.5f);
            Assert.That(agent.IsTargetVisible, Is.False);
            Assert.That(agent.Detection, Is.Zero);
            Assert.That(attacks, Is.Zero);
            // 먼저 벽을 없애 정상 공격을 확인해야 이후 공격 중단이 단순한 미감지 때문이 아님을 구분할 수 있습니다.
            wall.SetActive(false); world.Rebuild();
            yield return Until(() => attacks > 0, 3, "Removing wall must permit perception and attack.");
            wall.SetActive(true); world.Rebuild();
            int before = attacks;
            yield return new WaitForSeconds(0.4f);
            Assert.That(agent.IsTargetVisible, Is.False);
            Assert.That(attacks, Is.EqualTo(before), "A wall restored during combat must immediately prevent fire.");
        }

        // 시각 입력을 끈 상태에서 소리만 줍니다. AI가 소리가 난 점을 조사하고 숨은 플레이어의 새 좌표를 따라가지 않는지 봅니다.
        // 검사 결과는 수색 상태·기억 좌표·이동 방향·공격 없음의 네 가지로 확인합니다.
        [UnityTest]
        public IEnumerator SoundInvestigatesPublishedPositionWithoutTrackingHiddenTarget()
        {
            target.enabled = false;
            Vector2 sound = new Vector2(3, 0);
            // 한 프레임을 기다려 Start/Update의 초기 준비를 마친 뒤 사건을 발행합니다.
            yield return null;
            // 충돌음은 최대 25점이라 확인(Inspect)만 시작합니다. 30점 이상 수색을 시험하려고 총성을 씁니다.
            noise.Publish(sound, 10, 1, NoiseKind.Gunshot);
            target.transform.position = new Vector2(-5, -4);
            yield return Until(() => agent.State == EnemyState.Search, 2, "Sound must lead to investigation.");
            Assert.That(Vector2.Distance(agent.LastKnownPosition, sound), Is.LessThan(0.001f));
            yield return Until(() => agent.transform.position.x > 0.5f, 2, "Enemy must move toward the sound.");
            Assert.That(agent.IsTargetVisible, Is.False);
            Assert.That(attacks, Is.Zero);
        }

        // 조도 거리표가 실제 IsTargetVisible 판단까지 연결되는지 검사합니다. 단순히 프로필 숫자만 확인하지 않습니다.
        // 예: 거리 5는 밝음(6)에서만, 거리 3은 약한 빛(4)까지 보이고 어둠(2)에서는 보이지 않아야 합니다.
        [UnityTest]
        public IEnumerator ThreeLightLevelsAffectActualPerception()
        {
            profile.DarkViewDistance = 2; profile.DimViewDistance = 4; profile.BrightViewDistance = 6;
            // 검사 중 적이 대상에게 다가가 거리 조건을 바꿔 버리지 않도록 이동을 매우 느리게 합니다.
            profile.MoveSpeed = profile.CombatMoveSpeed = 0.01f;
            target.transform.position = new Vector2(5, 0);
            target.Light = LightLevel.Dark;
            yield return null; yield return null;
            Assert.That(agent.IsTargetVisible, Is.False, "Dark: distance 5 exceeds 2.");
            target.Light = LightLevel.Dim;
            yield return null;
            Assert.That(agent.IsTargetVisible, Is.False, "Dim: distance 5 exceeds 4.");
            target.Light = LightLevel.Bright;
            yield return null;
            Assert.That(agent.IsTargetVisible, Is.True, "Bright: distance 5 is inside 6.");
            target.transform.position = new Vector2(3, 0); target.Light = LightLevel.Dim;
            yield return null;
            Assert.That(agent.IsTargetVisible, Is.True, "Dim: distance 3 is inside 4.");
            target.Light = LightLevel.Dark;
            yield return null;
            Assert.That(agent.IsTargetVisible, Is.False, "Dark: distance 3 exceeds 2.");
        }

        // A*가 경로를 반환하는 것에서 끝내지 않고, 적 Transform이 실제 벽 끝을 돌아 목적지까지 가는지 검사합니다.
        // 시각 추적을 끄고 순찰점 하나만 주므로 이동의 원인은 순찰 경로라고 해석할 수 있습니다.
        [UnityTest]
        public IEnumerator PatrolActuallyDetoursAroundCollider()
        {
            target.enabled = false;
            agent.transform.position = new Vector2(-3.25f, 0.25f);
            Wall(Vector2.zero, new Vector2(0.6f, 3));
            var patrol = Child("Destination").transform;
            patrol.position = new Vector2(3.25f, 0.25f);
            // 시작 위치/목적지를 바꾼 뒤 초기화해 이전 경로나 복귀 기준이 남지 않게 합니다.
            agent.PatrolPoints = new[] { patrol }; agent.ResetAgent();
            float greatestY = 0;
            int frames = 0;
            float startTime = Time.time;
            Vector2 start = agent.transform.position;
            // 목적지 근처까지 도착하거나 8초가 지나면 끝냅니다. 매 프레임 점유 가능 여부를 검사해
            // '벽을 관통해 목적지에 도착했어도 성공'으로 처리되는 일을 막습니다.
            while (Vector2.Distance(agent.transform.position, patrol.position) > 0.3f && Time.time - startTime < 8)
            {
                Assert.That(world.CanOccupy(agent.transform.position), Is.True, "Enemy intersected a wall.");
                greatestY = Mathf.Max(greatestY, Mathf.Abs(agent.transform.position.y));
                frames++; yield return null;
            }
            Assert.That(Vector2.Distance(agent.transform.position, patrol.position), Is.LessThan(0.3f));
            // 벽 높이가 3이므로 중심에서 위/아래로 1.5 이상 우회해야 합니다. 1.6은 끝을 돌아간 증거 기준입니다.
            Assert.That(greatestY, Is.GreaterThan(1.6f), "Path must go around the wall end.");
            Assert.That(frames, Is.GreaterThan(2));
            Write("movement-evidence.txt", $"start={start}; end={(Vector2)agent.transform.position}; elapsed={Time.time - startTime:F3}s; frames={frames}; maxAbsY={greatestY:F3}");
        }

        // 플레이어 교체·비활성·사망·삭제 같은 수명 변화를 넣어, 오래된 대상에 발사하거나 예외를 내지 않는지 검사합니다.
        // SetTarget은 명시적 교체 명령이고, enabled/IsAlive/Destroy는 서로 다른 '현재 대상 사용 불가' 상황입니다.
        [UnityTest]
        public IEnumerator TargetReplacementDisableDeathAndDestroyAreSafe()
        {
            yield return Until(() => attacks > 0, 3, "Initial target attack.");
            var replacement = Child("Replacement").AddComponent<EnemyTarget>();
            replacement.transform.position = new Vector2(-2, 0);
            // 반대편의 새 대상으로 바꿉니다. LastKnownPosition의 X가 음수가 되는지 봐서 실제 관측 대상도 바뀌었는지 확인합니다.
            agent.SetTarget(replacement);
            yield return Until(() => agent.IsTargetVisible && agent.LastKnownPosition.x < -1, 3, "Target replacement must update perception.");
            replacement.gameObject.SetActive(false);
            yield return null;
            Assert.That(agent.IsTargetVisible, Is.False);
            replacement.gameObject.SetActive(true); replacement.IsAlive = false;
            yield return null;
            int before = attacks;
            yield return new WaitForSeconds(0.2f);
            Assert.That(attacks, Is.EqualTo(before));
            replacement.IsAlive = true;
            // Destroy는 즉시 메모리를 지우는 함수가 아니므로 다음 프레임까지 기다려 실제 삭제 후의 접근도 시험합니다.
            UnityEngine.Object.Destroy(replacement.gameObject);
            yield return null; yield return null;
            agent.SetTarget(null);
            yield return null;
            Assert.That(agent.IsTargetVisible, Is.False);
        }

        // 나중에 팀원이 모델을 교체하는 상황을 재현합니다. Visual을 삭제해도 AI 루트는 남아 판단과 요청을 계속해야 합니다.
        // 선택적인 선 참조도 연결하지 않아 EnemyDebugView의 없는 참조 처리까지 함께 확인합니다.
        [UnityTest]
        public IEnumerator ReplacingVisualDoesNotInterruptLogicOrDebugView()
        {
            var visual = Child("Visual"); visual.transform.SetParent(agent.transform, false);
            var renderer = visual.AddComponent<SpriteRenderer>();
            var view = agent.gameObject.AddComponent<NullTrace.AI.Demo.EnemyDebugView>();
            view.Agent = agent; view.Visual = renderer;
            yield return Until(() => attacks > 0, 3, "Agent must work with a visual.");
            // 외형이 없는 구간에도 공격 횟수가 증가해야 로직이 그림에 의존하지 않는다는 뜻입니다.
            UnityEngine.Object.Destroy(visual);
            yield return null;
            int before = attacks;
            yield return new WaitForSeconds(0.2f);
            Assert.That(attacks, Is.GreaterThan(before), "Removing Visual cannot stop combat.");
            var replacement = Child("ReplacementVisual"); replacement.transform.SetParent(agent.transform, false);
            // 새 외형 참조를 뷰에 연결합니다. EnemyAgent의 필드나 FSM을 다시 만들 필요가 없어야 합니다.
            view.Visual = replacement.AddComponent<SpriteRenderer>();
            before = attacks;
            yield return new WaitForSeconds(0.2f);
            Assert.That(agent.IsTargetVisible, Is.True);
            Assert.That(agent.State, Is.EqualTo(EnemyState.Combat));
            Assert.That(attacks, Is.GreaterThan(before));
        }

        // 풀링처럼 버스/적을 여러 번 껐다 켠 뒤에도 소음 한 건에 구독자가 한 번만 반응하는지 확인합니다.
        // AI 상태는 대사 제한 등에 의해 중복이 가려질 수 있어 실제 등록 함수 목록도 함께 검사합니다.
        [UnityTest]
        public IEnumerator BusAndAgentReactivationDoNotDuplicateSubscriptions()
        {
            target.enabled = false;
            yield return null;
            // 각 전환 뒤 한 프레임을 주어 OnDisable/OnEnable과 플레이 루프가 반영되게 합니다.
            for (int i = 0; i < 4; i++)
            {
                noise.enabled = false; agent.enabled = false;
                yield return null;
                noise.enabled = true; agent.enabled = true;
                yield return null;
            }
            // 테스트에서만 reflection으로 이벤트의 내부 등록 목록을 읽습니다. 실전 게임 코드가 이 필드에 접근할 필요는 없습니다.
            // 같은 agent를 대상으로 하는 함수가 정확히 1개인지 세어 중복 구독을 직접 확인합니다.
            var eventField = typeof(NoiseBus).GetField("Emitted", BindingFlags.Instance | BindingFlags.NonPublic);
            var handlers = (Delegate)eventField.GetValue(noise);
            Assert.That(handlers?.GetInvocationList().Count(h => ReferenceEquals(h.Target, agent)), Is.EqualTo(1));
            noise.Publish(new Vector2(3, 0), 10, 1, NoiseKind.Gunshot);
            yield return Until(() => agent.CurrentGoal == EnemyGoal.Search, 1, "One subscription must hear one fresh event.");
            Assert.That(agent.HeardNoiseCount, Is.EqualTo(1));
            // 꺼진 버스에서 새 사건을 발행해도 기억 위치가 바뀌면 안 됩니다.
            noise.enabled = false;
            Vector2 remembered = agent.LastKnownPosition;
            noise.Publish(new Vector2(-4, 0), 10);
            yield return null;
            Assert.That(agent.LastKnownPosition, Is.EqualTo(remembered));
        }

        // 문이 닫혀 경로가 없던 적에게 문 열기+Rebuild를 적용하면 새 격자로 길을 찾고 실제 이동을 재개하는지 검사합니다.
        // 위/아래 벽이 맵 끝까지 이어지므로 닫힌 문을 다른 쪽으로 우회할 수 없는 조건입니다.
        [UnityTest]
        public IEnumerator DoorRebuildUnblocksPathAndAgentResumes()
        {
            target.enabled = false;
            agent.transform.position = new Vector2(-3.25f, 0.25f);
            Wall(new Vector2(0, 3.5f), new Vector2(0.6f, 5));
            Wall(new Vector2(0, -3.5f), new Vector2(0.6f, 5));
            var door = Wall(Vector2.zero, new Vector2(0.6f, 2));
            var patrol = Child("AcrossDoor").transform; patrol.position = new Vector2(3.25f, 0.25f);
            agent.PatrolPoints = new[] { patrol }; agent.ResetAgent();
            var path = new List<Vector2Int>();
            Assert.That(world.TryGetCell(agent.transform.position, out var from), Is.True);
            Assert.That(world.TryGetCell(patrol.position, out var to), Is.True);
            Assert.That(AStarPathfinder.TryFindPath(world.Map, from, to, path), Is.False);
            yield return new WaitForSeconds(0.4f);
            Assert.That(agent.transform.position.x, Is.LessThan(0));
            // Revision은 격자를 다시 만든 횟수에 따라 바뀌는 표식입니다. 적이 오래된 경로를 버릴 근거가 됩니다.
            int revision = world.Revision;
            door.SetActive(false); world.Rebuild();
            Assert.That(world.Revision, Is.GreaterThan(revision));
            // 탐색 함수의 성공과 실제 적 위치의 변화는 별개이므로 둘 다 확인합니다.
            Assert.That(AStarPathfinder.TryFindPath(world.Map, from, to, path), Is.True);
            yield return Until(() => agent.transform.position.x > 2.5f, 5, "Door opening must invalidate the failed route.");
        }

        // 파일명과 증거 문자열을 받아 프로젝트의 Results 폴더에 저장합니다. Assets 바깥이라 게임 에셋으로 가져오지 않습니다.
        // SandboxRunTests도 같은 저장 위치를 쓰도록 internal 도우미로 공유합니다.
        internal static void Write(string name, string text)
        {
            string directory = ResultDirectory;
            Directory.CreateDirectory(directory); File.WriteAllText(Path.Combine(directory, name), text);
        }

        // 이전 검증 원본을 보존합니다. 새 사양의 결과는 날짜별 폴더에만 씁니다.
        internal static string ResultDirectory => Path.GetFullPath(Path.Combine(Application.dataPath, "../Results/2026-11-06"));

        // 발행자의 Update가 NPC보다 먼저 실행되는 실제 프레임 순서를 재현합니다.
        // 발행 시각이 이전 프레임의 agent.GameTime보다 크다고 현재 소리를 버리면 이 검사가 실패합니다.
        [UnityTest]
        public IEnumerator NoisePublishedBeforeNpcUpdateInSameFrameIsHeard()
        {
            target.enabled = false;
            // SetUp과 첫 코루틴 사이에도 실제 Update가 실행될 수 있다. 소리만의 실험은 기존 목격을 비우고 시작한다.
            agent.ResetAgent();
            var publisher = Child("EarlyNoisePublisher").AddComponent<EarlyNoisePublisher>();
            publisher.Bus = noise;
            publisher.Position = agent.transform.position;
            yield return Until(() => publisher.Published, 1, "Early publisher must run.");
            yield return null;
            Assert.That(agent.HeardNoiseCount, Is.EqualTo(1));
            Assert.That(agent.Memory.Count, Is.EqualTo(1));
            Assert.That(agent.Suspicion, Is.GreaterThanOrEqualTo(49));
        }

        // 의심도 하락으로 수색 자격이 사라지는 경우다. 기억 자체의 만료와 구분해 검사한다.
        [UnityTest]
        public IEnumerator FallingSuspicionFromThirtyFiveToEightStopsSearch()
        {
            target.enabled = false;
            profile.EpisodeDuration = 20; profile.MoveSpeed = 0.01f;
            profile.SuspicionQuietSeconds = 0; profile.SuspicionDecayPerSecond = 6;
            noise.Publish(new Vector2(2, 0), 1000, 0.7f, NoiseKind.Gunshot);
            Assert.That(agent.Suspicion, Is.EqualTo(35).Within(0.1f));
            yield return Until(() => agent.CurrentGoal == EnemyGoal.Search, 1, "The 35-point clue must start search.");
            yield return Until(() => agent.Suspicion <= 8, 7, "The same clue must naturally decay to eight.");
            Assert.That(agent.Memory.Count, Is.GreaterThan(0), "The clue has not expired; eligibility changed.");
            Assert.That(agent.CurrentGoal, Is.Not.EqualTo(EnemyGoal.Search));
            Assert.That(attacks, Is.Zero);
        }

        // 마지막 방문은 완료했지만 초소에는 아직 도착하지 않은 시점에 사건을 발생시킨다.
        [UnityTest]
        public IEnumerator IncidentDuringPostReturnRestoresPostDutyAndCompletesOnlyAtPost()
        {
            target.enabled = false;
            agent.transform.position = new Vector2(0.25f, 0.25f);
            Vector2 post = agent.transform.position;
            var point = Child("FinalVisit").transform; point.position = new Vector2(2.25f, 0.25f);
            agent.PatrolPoints = new[] { point }; profile.EpisodeDuration = 0.5f;
            profile.PatrolPause = 0.05f; profile.MoveSpeed = 1;
            agent.ResetAgent();
            int completed = 0; agent.PatrolCompleted += () => completed++;
            yield return Until(() => agent.CurrentDuty == EnemyDuty.ReturnToPost && agent.transform.position.x < 1.8f,
                5, "The incident must happen on the post-return leg.");
            Vector2 departure = agent.transform.position;
            noise.Publish(departure + Vector2.up, 100, 1, NoiseKind.Gunshot);
            yield return Until(() => agent.EpisodeId != 0, 1, "Post duty must create an episode too.");
            Assert.That(agent.NextPatrolIndex, Is.EqualTo(1)); Assert.That(completed, Is.Zero);
            yield return Until(() => agent.IsRecoveryProtected, 5, "The NPC must return to its departure point.");
            Assert.That(agent.CurrentDuty, Is.EqualTo(EnemyDuty.ReturnToPost));
            Assert.That(agent.NextPatrolIndex, Is.EqualTo(1)); Assert.That(completed, Is.Zero);
            Assert.That(Vector2.Distance(departure, agent.transform.position), Is.LessThan(0.2f));
            yield return Until(() => completed == 1, 5, "Post arrival must finish the original patrol run.");
            Assert.That(agent.CurrentDuty, Is.EqualTo(EnemyDuty.Guard));
            Assert.That(Vector2.Distance(post, agent.transform.position), Is.LessThanOrEqualTo(0.15f));
            Assert.That(attacks, Is.Zero);
        }

        // B는 소음 버스와 시각 대상에 연결하지 않습니다. 따라서 B가 아는 시각은 무전 도착으로만 결정됩니다.
        [UnityTest]
        public IEnumerator RadioBecomesPersonalKnowledgeOnlyAfterDelivery()
        {
            target.enabled = false;
            var radio = Child("Radio").AddComponent<RadioChannel>();
            agent.Radio = radio;
            // 최초 플레이 프레임에 생긴 목격을 제거하고 새 무전망에 등록한다. 소리 사건 하나만 보낼 조건이다.
            agent.ResetAgent();
            var go = Child("RadioOnlyGuard"); go.SetActive(false);
            go.transform.position = new Vector2(-3, 0);
            var receiver = go.AddComponent<EnemyAgent>();
            receiver.Profile = profile; receiver.World = world; receiver.Radio = radio;
            receiver.AutoStartPatrol = false;
            go.SetActive(true);
            yield return null;
            noise.Publish(agent.transform.position, 4, 1, NoiseKind.Gunshot);
            Assert.That(agent.Memory.Count, Is.EqualTo(1));
            long originalId = agent.Memory.Entries.Single().Report.Id;
            Assert.That(receiver.Memory.Count, Is.Zero);
            yield return new WaitForSeconds(0.2f);
            Assert.That(receiver.Memory.Count, Is.Zero, "The 0.5-second radio delay must not expose shared knowledge early.");
            yield return Until(() => receiver.RadioReceivedCount == 1, 1, "Report must eventually reach its recipient.");
            Assert.That(receiver.Memory.Count, Is.EqualTo(1));
            Assert.That(receiver.HeardNoiseCount, Is.Zero);
            Assert.That(receiver.Memory.Entries.Single().Report.Id, Is.EqualTo(originalId));
            Assert.That(receiver.IsTargetIdentified, Is.False, "Radio is not direct identification or attack permission.");
        }
    }

    [DefaultExecutionOrder(-10000)]
    public sealed class EarlyNoisePublisher : MonoBehaviour
    {
        public NoiseBus Bus;
        public Vector2 Position;
        public bool Published { get; private set; }
        private void Update()
        {
            if (Published || !Bus) return;
            Published = true;
            Bus.Publish(Position, 4, 1, NoiseKind.Gunshot);
        }
    }
}
