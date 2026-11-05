// 역할: 문 계획과 실제 통과를 분리하고, 가까운 문만 열며, NPC가 낀 문을 닫지 않는 계약을 확인한다.
// 실제 Physics2D 조회를 쓰지만 씬/에셋을 저장하지 않는다. Unity 실행은 통합 검증 담당자가 수행한다.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NullTrace.AI.Tests
{
    public sealed class AiDoorTests
    {
        private GameObject root;
        private GridWorld2D world;
        private readonly Vector2 left = new Vector2(-2.5f, 0.5f);
        private readonly Vector2 right = new Vector2(2.5f, 0.5f);

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("AI door fixture");
            world = root.AddComponent<GridWorld2D>();
            world.Origin = new Vector2(-4, -4); world.Width = world.Height = 8;
            world.CellSize = 1; world.AgentRadius = 0.25f; world.ObstacleMask = 1 << 8;
            world.Rebuild();
        }
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            Physics2D.SyncTransforms();
            LogAssert.NoUnexpectedReceived();
        }
        private GameObject Child(string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(root.transform);
            return child;
        }
        private BoxCollider2D Wall(Vector2 position, Vector2 size)
        {
            var go = Child("Wall"); go.layer = 8; go.transform.position = position;
            var collider = go.AddComponent<BoxCollider2D>(); collider.size = size;
            Physics2D.SyncTransforms();
            return collider;
        }
        private AiDoor2D Door(float x = 0)
        {
            // 문 제어 오브젝트와 Collider를 나눠, Inspector에서 직접 연결한 다른 오브젝트의 문도 확인한다.
            var collider = Wall(new Vector2(x, 0), new Vector2(0.1f, 8));
            var controller = Child("Door controller"); controller.SetActive(false);
            var door = controller.AddComponent<AiDoor2D>();
            door.World = world; door.DoorCollider = collider;
            controller.SetActive(true);
            // EditMode에서는 OnEnable 자동 실행을 전제하지 않는다. 연결을 마친 실제 문으로 지도를 갱신한다.
            world.Rebuild();
            return door;
        }
        private bool FindPath(MovementConstraint2D area, List<Vector2Int> path)
        {
            Assert.That(world.TryGetCell(left, out var start), Is.True);
            Assert.That(world.TryGetCell(right, out var goal), Is.True);
            return AStarPathfinder.TryFindPath(world.Map, start, goal, path, true,
                (a, b) => area.AllowsSegment(world.CellCenter(a), world.CellCenter(b), world.AgentRadius)
                    && world.CanPlanTraverse(world.CellCenter(a), world.CellCenter(b)));
        }

        [Test]
        public void ClosedOpenableDoorIsPlannedWithoutOpeningOrAllowingActualMovement()
        {
            var door = Door(); int revision = world.Revision;
            var path = new List<Vector2Int>();
            Assert.That(FindPath(new MovementConstraint2D(world.Bounds), path), Is.True);
            Assert.That(world.CanPlanTraverse(left, right), Is.True);
            Assert.That(world.CanPlanTraverse(Vector2.zero, Vector2.zero), Is.True);
            Assert.That(world.CanPlanOccupy(Vector2.zero), Is.True);
            Assert.That(world.CanOccupy(Vector2.zero), Is.False);
            Assert.That(world.CanTraverse(left, right), Is.False);
            Assert.That(world.TryMove(left, right, out var reached), Is.False);
            Assert.That(reached.x, Is.LessThan(0)); Assert.That(world.CanOccupy(reached), Is.True);
            Assert.That(door.IsOpen, Is.False); Assert.That(world.Revision, Is.EqualTo(revision));
        }

        [Test]
        public void LockAndNpcPermissionChangesSynchronizePlanningAndNotifyAfterRevision()
        {
            var door = Door(); var path = new List<Vector2Int>(); int notices = 0, lastRevision = world.Revision;
            door.StateChanged += changed =>
            {
                notices++; Assert.That(changed, Is.SameAs(door));
                Assert.That(world.Revision, Is.GreaterThan(lastRevision)); lastRevision = world.Revision;
            };
            Assert.That(door.TrySetLocked(true), Is.True);
            Assert.That(FindPath(new MovementConstraint2D(world.Bounds), path), Is.False);
            Assert.That(world.CanPlanTraverse(left, right), Is.False);
            Assert.That(door.TrySetOpen(true), Is.False);
            Assert.That(world.TryOpenDoorAhead(new Vector2(-0.6f, 0.5f), new Vector2(-0.1f, 0.5f)), Is.False);
            Assert.That(door.TrySetLocked(false), Is.True);
            Assert.That(FindPath(new MovementConstraint2D(world.Bounds), path), Is.True);
            door.SetNpcCanOpen(false);
            Assert.That(FindPath(new MovementConstraint2D(world.Bounds), path), Is.False);
            Assert.That(world.CanPlanTraverse(left, right), Is.False);
            door.SetNpcCanOpen(true);
            Assert.That(world.CanPlanTraverse(left, right), Is.True);
            Assert.That(notices, Is.EqualTo(4));
        }

        [Test]
        public void OpensOnlyNearbyDoorInMovementDirectionAndInvalidatesOldPath()
        {
            var door = Door(); int revision = world.Revision;
            Assert.That(world.TryOpenDoorAhead(left, right), Is.False, "먼 목적지를 전달해도 문을 원격으로 열지 않는다.");
            var nearby = new Vector2(-0.6f, 0.5f);
            Assert.That(world.TryOpenDoorAhead(nearby, left), Is.False);
            Assert.That(world.TryOpenDoorAhead(nearby, nearby), Is.False);
            Assert.That(world.Revision, Is.EqualTo(revision));
            Assert.That(world.TryOpenDoorAhead(nearby, new Vector2(-0.1f, 0.5f)), Is.True);
            Assert.That(door.IsOpen, Is.True); Assert.That(door.DoorCollider.enabled, Is.False);
            Assert.That(world.Revision, Is.EqualTo(revision + 1));
            Assert.That(world.CanTraverse(left, right), Is.True);
            Assert.That(world.TryOpenDoorAhead(left, right), Is.False, "이미 열린 문은 새 개방으로 세지 않는다.");
        }

        [Test]
        public void TinyMovementStepStillOpensNearbyDoorWithoutRemoteOpening()
        {
            var door = Door();
            Assert.That(world.TryOpenDoorAhead(left, left + Vector2.right * 0.0001f), Is.False);
            var nearby = new Vector2(-0.6f, 0.5f);
            Assert.That(world.TryOpenDoorAhead(nearby, nearby + Vector2.left * 0.0001f), Is.False);
            Assert.That(world.TryOpenDoorAhead(nearby, nearby + Vector2.right * 0.0001f), Is.True,
                "높은 프레임률의 짧은 걸음에서도 가까운 문을 열어 물리 접촉 앞 교착을 막는다.");
            Assert.That(door.IsOpen, Is.True);
        }

        [Test]
        public void OneRequestDoesNotOpenEveryDoorAlongItsLine()
        {
            var first = Door(); var second = Door(0.25f);
            Assert.That(world.TryOpenDoorAhead(new Vector2(-0.4f, 0.5f), new Vector2(0.1f, 0.5f)), Is.True);
            Assert.That(first.IsOpen, Is.True); Assert.That(second.IsOpen, Is.False);
            Assert.That(world.CanTraverse(left, right), Is.False);
        }

        [Test]
        public void OrdinaryWallInFrontPreventsOpeningDoorBehindIt()
        {
            var door = Door(); Wall(new Vector2(-0.3f, 0), new Vector2(0.1f, 8)); world.Rebuild();
            Assert.That(world.CanPlanTraverse(left, right), Is.False);
            Assert.That(world.TryOpenDoorAhead(new Vector2(-0.9f, 0.5f), new Vector2(-0.4f, 0.5f)), Is.False);
            Assert.That(door.IsOpen, Is.False);
        }

        [Test]
        public void SeparateWallOverlappingDoorIsStillAnObstacle()
        {
            Door(); Wall(Vector2.zero, new Vector2(0.1f, 8)); world.Rebuild();
            Assert.That(world.CanPlanOccupy(Vector2.zero), Is.False);
            Assert.That(world.CanPlanTraverse(left, right), Is.False);
            Assert.That(FindPath(new MovementConstraint2D(world.Bounds), new List<Vector2Int>()), Is.False);
            Assert.That(world.TryOpenDoorAhead(new Vector2(-0.6f, 0.5f), new Vector2(-0.1f, 0.5f)), Is.False);
        }

        [Test]
        public void DisabledOrMissingDoorComponentLeavesOrdinaryObstacle()
        {
            var door = Door(); door.enabled = false;
            Assert.That(world.CanPlanTraverse(left, right), Is.False);
            Assert.That(FindPath(new MovementConstraint2D(world.Bounds), new List<Vector2Int>()), Is.False);
            Object.DestroyImmediate(door); world.Rebuild();
            Assert.That(world.CanPlanTraverse(left, right), Is.False);
        }

        [Test]
        public void ClosingOrLockingRejectsNpcBodyOverlapAndLeavesAllStateUnchanged()
        {
            var door = Door(); Assert.That(door.TrySetOpen(true), Is.True);
            var go = Child("NPC body"); go.SetActive(false);
            var agent = go.AddComponent<EnemyAgent>(); agent.World = world;
            // 이 테스트는 AI 판단을 실행하지 않는다. 행동 컴포넌트가 꺼져도 활성 오브젝트의 몸은 보호한다.
            agent.enabled = false; go.transform.position = new Vector2(0.2f, 0.5f); go.SetActive(true);
            int revision = world.Revision, notices = 0; door.StateChanged += _ => notices++;
            Assert.That(door.TrySetOpen(false), Is.False);
            Assert.That(door.TrySetLocked(true), Is.False);
            Assert.That(door.IsOpen, Is.True); Assert.That(door.IsLocked, Is.False);
            Assert.That(door.DoorCollider.enabled, Is.False);
            Assert.That(world.Revision, Is.EqualTo(revision)); Assert.That(notices, Is.Zero);
            go.transform.position = new Vector2(1, 0.5f);
            Assert.That(door.TrySetLocked(true), Is.True);
            Assert.That(door.IsOpen, Is.False); Assert.That(door.IsLocked, Is.True);
            Assert.That(world.CanPlanTraverse(left, right), Is.False);
            Assert.That(world.Revision, Is.EqualTo(revision + 1)); Assert.That(notices, Is.EqualTo(1));
        }

        [Test]
        public void OpenCloseAndRepeatedRequestsHaveConsistentRevision()
        {
            var door = Door(); int revision = world.Revision;
            Assert.That(door.TrySetOpen(false), Is.True);
            Assert.That(world.Revision, Is.EqualTo(revision));
            Assert.That(door.TrySetOpen(true), Is.True);
            Assert.That(door.TrySetOpen(true), Is.True);
            Assert.That(world.Revision, Is.EqualTo(revision + 1));
            Assert.That(door.TrySetOpen(false), Is.True);
            Assert.That(world.Revision, Is.EqualTo(revision + 2));
            Assert.That(world.CanTraverse(left, right), Is.False);
            Assert.That(world.CanPlanTraverse(left, right), Is.True);
        }

        [Test]
        public void PlanningStillChecksBodyWidthAndRegisteredArea()
        {
            Door(); Wall(new Vector2(-1, 0.4f), new Vector2(0.1f, 0.4f)); world.Rebuild();
            Assert.That(world.CanPlanTraverse(new Vector2(-2, 0), Vector2.zero), Is.False);
            var area = new MovementConstraint2D(new Rect(-4, -4, 8, 8), left, 2);
            Assert.That(FindPath(area, new List<Vector2Int>()), Is.False);
        }

        [Test]
        public void LockedDoorUsesExistingAStarDetourOnlyInsideAllowedArea()
        {
            var door = Door(); ((BoxCollider2D)door.DoorCollider).size = new Vector2(0.1f, 2);
            Assert.That(door.TrySetLocked(true), Is.True);
            Assert.That(FindPath(new MovementConstraint2D(world.Bounds), new List<Vector2Int>()), Is.True);
            // 양 끝은 이 복도 안이지만 문 위/아래의 우회로는 밖이다. 막혔다는 이유로 구역을 넓히지 않는다.
            var corridor = new MovementConstraint2D(new Rect(-4, -1, 8, 2));
            Assert.That(FindPath(corridor, new List<Vector2Int>()), Is.False);
            Assert.That(door.TrySetLocked(false), Is.True);
            Assert.That(FindPath(corridor, new List<Vector2Int>()), Is.True);
            Assert.That(door.IsOpen, Is.False);
        }

        [Test]
        public void DoorRegisteredToAnotherWorldIsNotIgnored()
        {
            var door = Door(); var other = Child("Other world").AddComponent<GridWorld2D>();
            other.Origin = world.Origin; other.Width = world.Width; other.Height = world.Height;
            other.CellSize = world.CellSize; other.AgentRadius = world.AgentRadius; other.ObstacleMask = world.ObstacleMask;
            door.World = other; other.Rebuild(); world.Rebuild();
            Assert.That(world.CanPlanTraverse(left, right), Is.False);
            Assert.That(other.CanPlanTraverse(left, right), Is.True);
            Assert.That(world.TryOpenDoorAhead(new Vector2(-0.6f, 0.5f), new Vector2(-0.1f, 0.5f)), Is.False);
            Assert.That(door.IsOpen, Is.False);
        }

        [Test]
        public void InvalidPositionsDoNotOpenDoorOrPassPlanningChecks()
        {
            var door = Door(); int revision = world.Revision;
            Assert.That(world.CanPlanTraverse(new Vector2(float.NaN, 0), right), Is.False);
            Assert.That(world.CanPlanTraverse(left, new Vector2(3.9f, 0)), Is.False);
            Assert.That(world.TryOpenDoorAhead(new Vector2(float.NaN, 0), right), Is.False);
            Assert.That(world.TryOpenDoorAhead(left, new Vector2(float.PositiveInfinity, 0)), Is.False);
            Assert.That(door.IsOpen, Is.False); Assert.That(world.Revision, Is.EqualTo(revision));
        }
    }
}
