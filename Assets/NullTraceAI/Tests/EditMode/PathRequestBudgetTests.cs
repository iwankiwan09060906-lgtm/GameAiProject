// 한꺼번에 경로를 요청해도 예산을 넘지 않고, 기다린 적의 순서와 취소가 실제로 지켜지는지 검사한다.
using NUnit.Framework;
using UnityEngine;

namespace NullTrace.AI.Tests
{
    public sealed class PathRequestBudgetTests
    {
        private GameObject root;
        private GridWorld2D world;
        private PathRequestBudget budget;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Path budget test");
            world = root.AddComponent<GridWorld2D>();
            budget = root.AddComponent<PathRequestBudget>();
            budget.RequestsPerFrame = 1;
        }
        [TearDown] public void TearDown() => Object.DestroyImmediate(root);
        private EnemyAgent Agent()
        {
            var item = new GameObject("Waiting enemy"); item.transform.SetParent(root.transform);
            var agent = item.AddComponent<EnemyAgent>(); agent.World = world;
            return agent;
        }

        [Test]
        public void FloodIsLimitedAndEarlierWaiterCannotBeOvertaken()
        {
            var a = Agent(); var b = Agent(); var c = Agent();
            Assert.That(budget.TryAcquire(a, 10), Is.True);
            Assert.That(budget.TryAcquire(b, 10), Is.False);
            Assert.That(budget.TryAcquire(c, 10), Is.False);
            Assert.That(budget.TryAcquire(c, 10), Is.False, "같은 NPC는 대기열에 두 번 등록되지 않는다.");
            Assert.That(budget.PendingCount, Is.EqualTo(2));
            Assert.That(budget.TryAcquire(a, 11), Is.False, "먼저 호출된 적도 이미 기다린 적을 앞지를 수 없다.");
            Assert.That(budget.TryAcquire(c, 11), Is.False);
            Assert.That(budget.TryAcquire(b, 11), Is.True);
            Assert.That(budget.RequestsThisTick, Is.EqualTo(1));
            Assert.That(budget.TryAcquire(c, 12), Is.True);
            Assert.That(budget.TryAcquire(a, 13), Is.True);
            Assert.That(budget.PendingCount, Is.Zero);
        }

        [Test]
        public void CancelledDisabledDestroyedAndChangedWorldWaitersDoNotBlockQueue()
        {
            var a = Agent(); var b = Agent(); var c = Agent();
            Assert.That(budget.TryAcquire(a, 1), Is.True);
            budget.TryAcquire(b, 1); budget.TryAcquire(c, 1); budget.Cancel(b);
            Assert.That(budget.TryAcquire(c, 2), Is.True);
            budget.TryAcquire(b, 2); budget.TryAcquire(a, 2); b.enabled = false;
            Assert.That(budget.TryAcquire(a, 3), Is.True);
            b.enabled = true; b.World = world;
            budget.TryAcquire(b, 3); budget.TryAcquire(c, 3); Object.DestroyImmediate(b.gameObject);
            Assert.That(budget.TryAcquire(c, 4), Is.True);
            budget.TryAcquire(a, 4); budget.TryAcquire(c, 4); a.World = null;
            Assert.That(budget.TryAcquire(c, 5), Is.True);
        }

        [Test]
        public void MapRebuildEveryFrameDoesNotStarveWaitingAgents()
        {
            world.ObstacleMask = 1 << 8; world.Rebuild();
            var profile = ScriptableObject.CreateInstance<EnemyProfile>();
            try
            {
                var goal = new GameObject("Shared patrol point"); goal.transform.SetParent(root.transform);
                goal.transform.position = new Vector2(2.25f, 0.25f);
                var agents = new EnemyAgent[8];
                for (int i = 0; i < agents.Length; i++)
                {
                    var agent = Agent(); agent.transform.position = new Vector2(-2.25f, 0.25f);
                    agent.Profile = profile; agent.PatrolPoints = new[] { goal.transform }; agent.ResetAgent();
                    agents[i] = agent;
                }
                // 계산 하나/프레임인데 지도는 매 프레임 바뀐다. 이미 기다린 표를 재발급하면 교착할 수 있다.
                for (int frame = 0; frame < agents.Length * 2; frame++)
                {
                    world.Rebuild();
                    foreach (var agent in agents) agent.Tick(1f / 60f);
                    Assert.That(budget.RequestsThisTick, Is.LessThanOrEqualTo(1));
                }
                foreach (var agent in agents) Assert.That(agent.PathRequestCount, Is.GreaterThan(0));
            }
            finally { Object.DestroyImmediate(profile); }
        }

        [Test]
        public void WorldsHaveIndependentLimitsAndDisableDropsOldTickets()
        {
            var a = Agent(); var b = Agent();
            Assert.That(budget.TryAcquire(a, 1), Is.True);
            budget.TryAcquire(b, 1);
            var other = new GameObject("Second world"); other.transform.SetParent(root.transform);
            var otherWorld = other.AddComponent<GridWorld2D>(); var otherBudget = other.AddComponent<PathRequestBudget>();
            b.World = otherWorld;
            Assert.That(otherBudget.TryAcquire(b, 1), Is.True);
            // EditMode는 게임의 자동 수명 주기를 가정하지 않는다. 비활성화 정리 호출을 명시한다.
            typeof(PathRequestBudget).GetMethod("OnDisable", System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic).Invoke(budget, null);
            Assert.That(budget.PendingCount, Is.Zero);
            Assert.That(budget.TryAcquire(a, 1), Is.True);
            budget.RequestsPerFrame = 0;
            Assert.That(budget.TryAcquire(a, 1), Is.False, "설정 오류도 무제한 계산으로 바뀌지 않는다.");
        }
    }
}
