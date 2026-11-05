// 역할: 지도 끝과 실제 Collider2D가 전 구간 이동을 막는지 검사한다. EditMode에서 임시 벽을 만들고 정리한다.
// 연결: GridWorld2D.CanTraverse/TryMove와 MovementConstraint2D를 A*의 구간 검사에 함께 적용한다.
// 읽는 순서: 작은 월드 준비 → 끝점·몸체 폭 → 닫혔다 열린 문 → 영역 밖 우회 거부.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NullTrace.AI.Tests
{
    public sealed class GridWorldTraversalTests
    {
        private GameObject root;
        private GridWorld2D world;

        // 매번 새 8×8 지도를 만들고 레이어 8만 벽으로 취급한다. 이전 검사의 물리 상태와 분리한다.
        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Navigation traversal fixture");
            world = root.AddComponent<GridWorld2D>();
            world.Origin = new Vector2(-4, -4);
            world.Width = world.Height = 8;
            world.CellSize = 1f;
            world.AgentRadius = 0.25f;
            world.ObstacleMask = 1 << 8;
            world.Rebuild();
            Assert.That(world.IsReady, Is.True);
        }

        // 테스트가 실패해도 만든 오브젝트를 지우고 물리 좌표를 동기화해 다음 검사에 벽을 남기지 않는다.
        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(root);
            Physics2D.SyncTransforms();
            LogAssert.NoUnexpectedReceived();
        }

        // 중심과 크기를 입력받아 테스트 소유의 벽을 만든다. 실제 장면 파일이나 에셋은 변경하지 않는다.
        private BoxCollider2D Wall(Vector2 center, Vector2 size)
        {
            var wall = new GameObject("Traversal wall");
            wall.transform.SetParent(root.transform);
            wall.transform.position = center;
            wall.layer = 8;
            var collider = wall.AddComponent<BoxCollider2D>();
            collider.size = size;
            Physics2D.SyncTransforms();
            return collider;
        }

        // Bounds는 월드 좌표다. 통과 조회는 오브젝트를 움직이지 않으며, 빈 길과 유효한 제자리 구간은 성공한다.
        [Test]
        public void ClearSegmentsAndStationaryPointAreAllowedWithoutMovingWorld()
        {
            Assert.That(world.Bounds, Is.EqualTo(new Rect(-4, -4, 8, 8)));
            Vector3 original = world.transform.position;
            Assert.That(world.CanTraverse(new Vector2(-3, -3), new Vector2(3, 3)), Is.True);
            Assert.That(world.CanTraverse(Vector2.zero, Vector2.zero), Is.True);
            Assert.That(world.transform.position, Is.EqualTo(original));
        }

        // 양 끝은 빈 공간이어도 사이의 얇은 벽을 통과하면 안 된다. 부분 이동은 완전 통과와 구분한다.
        [Test]
        public void ClearEndpointsDoNotPermitCrossingMiddleWall()
        {
            Wall(Vector2.zero, new Vector2(0.1f, 2f));
            var start = new Vector2(-2, 0);
            var end = new Vector2(2, 0);
            Assert.That(world.CanOccupy(start) && world.CanOccupy(end), Is.True);
            Assert.That(world.CanTraverse(start, end), Is.False);
            Assert.That(world.TryMove(start, end, out Vector2 reached), Is.False);
            Assert.That(reached.x, Is.GreaterThan(start.x).And.LessThan(0f));
            Assert.That(world.CanOccupy(reached), Is.True, "부분 이동 끝점에서도 몸 전체가 벽 밖이어야 한다.");
        }

        // 중심 선은 벽을 비껴도 NPC의 반지름 0.25가 벽 아래쪽 y=0.2에 걸린다. 선분 시야 검사로 대체하면 실패한다.
        [Test]
        public void BodyWidthBlocksRouteWhoseCenterLineIsClear()
        {
            Wall(new Vector2(0, 0.4f), new Vector2(0.1f, 0.4f));
            var start = new Vector2(-2, 0);
            var end = new Vector2(2, 0);
            Assert.That(world.HasLineOfSight(start, end), Is.True);
            Assert.That(world.CanTraverse(start, end), Is.False);
        }

        // 닫히기 전 격자가 열려 있어도 실제 이동은 새 벽을 거부한다. 문을 다시 열면 실제 통과 조회도 복구된다.
        [Test]
        public void DoorChangesAreCheckedAgainstLivePhysics()
        {
            var door = Wall(Vector2.zero, new Vector2(0.1f, 2f));
            door.enabled = false;
            world.Rebuild();
            var start = new Vector2(-2, 0);
            var end = new Vector2(2, 0);
            Assert.That(world.CanTraverse(start, end), Is.True);
            door.enabled = true;
            Physics2D.SyncTransforms();
            Assert.That(world.Map.IsWalkable(new Vector2Int(3, 3)), Is.True);
            Assert.That(world.CanTraverse(start, end), Is.False);
            door.enabled = false;
            Physics2D.SyncTransforms();
            Assert.That(world.CanTraverse(start, end), Is.True);
        }

        // 전체 지도에는 벽 끝을 도는 길이 있지만 추적 사각형 안에는 없다. 바깥 우회로를 성공으로 반환하면 안 된다.
        [Test]
        public void AStarRejectsDetourOutsideAllowedRegion()
        {
            Wall(Vector2.zero, new Vector2(0.2f, 4f));
            world.Rebuild();
            Assert.That(world.TryGetCell(new Vector2(-2.5f, 0.5f), out Vector2Int start), Is.True);
            Assert.That(world.TryGetCell(new Vector2(2.5f, 0.5f), out Vector2Int goal), Is.True);
            var path = new List<Vector2Int>();
            Assert.That(AStarPathfinder.TryFindPath(world.Map, start, goal, path), Is.True);
            var area = new MovementConstraint2D(new Rect(-4, -2, 8, 4));
            Func<Vector2Int, Vector2Int, bool> allowed = (from, to) =>
                area.AllowsSegment(world.CellCenter(from), world.CellCenter(to), world.AgentRadius)
                && world.CanTraverse(world.CellCenter(from), world.CellCenter(to));
            Assert.That(AStarPathfinder.TryFindPath(world.Map, start, goal, path, true, allowed), Is.False);
            Assert.That(path, Is.Empty);
        }

        // 중심은 맵 안이어도 몸이 튀어나오면 거부한다. NaN·무한대·재구축 전 설정 변경도 통과시키지 않는다.
        [Test]
        public void InvalidOrOutOfBoundsSegmentsAreRejected()
        {
            Assert.That(world.CanTraverse(Vector2.zero, new Vector2(3.9f, 0)), Is.False);
            Assert.That(world.CanTraverse(new Vector2(3.9f, 0), Vector2.zero), Is.False);
            Assert.That(world.CanTraverse(new Vector2(float.NaN, 0), Vector2.zero), Is.False);
            Assert.That(world.CanTraverse(Vector2.zero, new Vector2(0, float.PositiveInfinity)), Is.False);
            world.CellSize = 0.5f;
            Assert.That(world.CanTraverse(Vector2.zero, Vector2.zero), Is.False);
        }

        // 아주 짧은 이동을 무조건 성공시키던 예외가 경계를 우회하면 안 된다. 정상적인 짧은 이동은 정확한 끝점을 준다.
        [Test]
        public void TinyMovesStillValidateTheirDestination()
        {
            var edge = new Vector2(3.75f, 0);
            Assert.That(world.TryMove(edge, new Vector2(3.7500005f, 0), out Vector2 reached), Is.False);
            Assert.That(reached, Is.EqualTo(edge));
            var nearby = new Vector2(0.0000001f, 0);
            Assert.That(world.TryMove(Vector2.zero, nearby, out reached), Is.True);
            Assert.That(reached, Is.EqualTo(nearby));
        }
    }
}
