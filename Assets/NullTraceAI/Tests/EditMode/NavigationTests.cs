// 역할: GridMap과 AStarPathfinder가 약속한 결과를 내는지 확인하는 EditMode 테스트다.
// 연결: 실제 게임에서는 EnemyAgent가 이 API를 쓰지만, 여기서는 작은 격자를 직접 만들어 로직만 검사한다.
// 읽는 순서: 격자 생성·범위 검사 → 알려진 경로 비용 → 실패·반복 호출 → 무작위 Dijkstra 비교.
// 맨 아래 ValidateAndGetCost는 경로의 모양과 비용을 검사하고, DijkstraCost는 비교용 정답 비용을 구한다.
// [Test]는 한 사례, [TestCase(...)]는 괄호 속 값을 메서드 인자로 넣는 사례다. Assert는 기대 결과 확인이다.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace NullTrace.AI.Tests
{
    // 정상 경로뿐 아니라 잘못된 입력, 막힌 길, 같은 목록 재사용도 검사하는 테스트 묶음이다.
    // 게임 오브젝트나 실제 이동은 만들지 않는다. 물리 충돌체와의 연결은 별도 PlayMode 테스트가 담당한다.
    public sealed class NavigationTests
    {
        // 3×2 격자는 처음부터 전부 열려 있어야 한다. 배열의 기본값을 반대로 해석하는 실수를 잡는다.
        // 끝 칸을 닫아도 지도 안이라는 사실은 유지되고, 다시 열면 이동 가능 상태로 돌아와야 한다.
        [Test]
        public void GridStartsWalkableAndCanBeChanged()
        {
            var grid = new GridMap(3, 2);
            Assert.That(grid.Width, Is.EqualTo(3));
            Assert.That(grid.Height, Is.EqualTo(2));
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                    Assert.That(grid.IsWalkable(new Vector2Int(x, y)), Is.True);

            var cell = new Vector2Int(2, 1);
            grid.SetWalkable(cell, false);
            Assert.That(grid.Contains(cell), Is.True);
            Assert.That(grid.IsWalkable(cell), Is.False);
            grid.SetWalkable(cell, true);
            Assert.That(grid.IsWalkable(cell), Is.True);
        }

        // 입력한 너비·높이가 0 또는 음수이거나, 곱한 칸 수가 int 범위를 넘으면 예외여야 한다.
        // 50,000×50,000 사례는 큰 배열을 실제로 만들기 전에 곱셈 범위를 검사하는지 확인한다.
        [TestCase(0, 2)]
        [TestCase(2, 0)]
        [TestCase(-1, 2)]
        [TestCase(2, -1)]
        [TestCase(50000, 50000)]
        public void InvalidDimensionsThrow(int width, int height)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GridMap(width, height));
        }

        // 지도 밖 좌표를 여러 방향으로 넣는다. 조회는 false로 안전하게 끝나고 쓰기는 예외여야 한다.
        // 너비 3에서 x=3, 높이 2에서 y=2도 바깥이다. 끝 경계를 한 칸 더 허용하는 오류를 잡는다.
        [TestCase(-1, 0)]
        [TestCase(0, -1)]
        [TestCase(3, 0)]
        [TestCase(0, 2)]
        [TestCase(int.MaxValue, int.MinValue)]
        public void OutsideCellsAreNotWalkableAndCannotBeWritten(int x, int y)
        {
            var grid = new GridMap(3, 2);
            var cell = new Vector2Int(x, y);
            Assert.That(grid.Contains(cell), Is.False);
            Assert.That(grid.IsWalkable(cell), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => grid.SetWalkable(cell, true));
        }

        // (2,0)~(2,3)을 벽으로 만들면 (0,0)에서 (4,0)으로 갈 때 반드시 y=4까지 우회해야 한다.
        // 4방향은 직교 12회=120, 8방향은 직교 8회+대각 2회=108이다. 단순 도착뿐 아니라 최단성을 검사한다.
        [TestCase(false, 120)]
        [TestCase(true, 108)]
        public void FindsShortestDetourAroundWall(bool allowDiagonal, int expectedCost)
        {
            var grid = new GridMap(5, 5);
            for (int y = 0; y < 4; y++)
                grid.SetWalkable(new Vector2Int(2, y), false);

            var path = new List<Vector2Int>();
            var start = new Vector2Int(0, 0);
            var goal = new Vector2Int(4, 0);
            Assert.That(AStarPathfinder.TryFindPath(grid, start, goal, path, allowDiagonal), Is.True);
            Assert.That(ValidateAndGetCost(grid, path, start, goal, allowDiagonal), Is.EqualTo(expectedCost));
        }

        // 2×2에서 (0,0)→(1,1)을 검사한다. (1,0)과 (0,1)이 둘 다 열리면 대각 비용은 14다.
        // 하나라도 막히면 직교 두 번으로 돌아가 비용 20, 둘 다 막히면 실패(-1)해야 한다.
        // 도착 칸만 보고 벽 모서리를 대각선으로 통과하는 오류를 잡는다.
        [TestCase(false, false, 14)]
        [TestCase(true, false, 20)]
        [TestCase(false, true, 20)]
        [TestCase(true, true, -1)]
        public void DiagonalNeedsBothOrthogonalCellsOpen(bool blockRight, bool blockUp, int expectedCost)
        {
            var grid = new GridMap(2, 2);
            grid.SetWalkable(new Vector2Int(1, 0), !blockRight);
            grid.SetWalkable(new Vector2Int(0, 1), !blockUp);
            var path = new List<Vector2Int>();
            var start = new Vector2Int(0, 0);
            var goal = new Vector2Int(1, 1);
            bool found = AStarPathfinder.TryFindPath(grid, start, goal, path);
            Assert.That(found, Is.EqualTo(expectedCost >= 0));
            if (found)
                Assert.That(ValidateAndGetCost(grid, path, start, goal, true), Is.EqualTo(expectedCost));
            else
                Assert.That(path, Is.Empty);
        }

        // 장애물이 없는 (0,0)→(4,3)은 직교만 쓰면 7×10=70, 대각을 허용하면 3×14+10=52다.
        // allowDiagonal 인자가 무시되거나 두 이동 방식의 비용·휴리스틱이 섞이는 문제를 잡는다.
        [TestCase(false, 70)]
        [TestCase(true, 52)]
        public void MovementModeUsesCorrectShortestCost(bool allowDiagonal, int expectedCost)
        {
            var grid = new GridMap(5, 4);
            var path = new List<Vector2Int>();
            var start = new Vector2Int(0, 0);
            var goal = new Vector2Int(4, 3);
            Assert.That(AStarPathfinder.TryFindPath(grid, start, goal, path, allowDiagonal), Is.True);
            Assert.That(ValidateAndGetCost(grid, path, start, goal, allowDiagonal), Is.EqualTo(expectedCost));
        }

        // 같은 칸을 시작·목표로 넣으면 그 칸 하나로 성공해야 한다. 움직이지 않아도 경로는 존재한다.
        // 단, 그 칸이 벽이면 실패해야 한다. 경로에 미리 넣어 둔 (9,9)나 이전 성공 결과도 남으면 안 된다.
        [Test]
        public void SameStartAndGoalReturnsOneCellUnlessBlocked()
        {
            var grid = new GridMap(1, 1);
            var cell = new Vector2Int(0, 0);
            var path = new List<Vector2Int> { new Vector2Int(9, 9) };
            Assert.That(AStarPathfinder.TryFindPath(grid, cell, cell, path), Is.True);
            Assert.That(path, Is.EqualTo(new[] { cell }));
            grid.SetWalkable(cell, false);
            Assert.That(AStarPathfinder.TryFindPath(grid, cell, cell, path), Is.False);
            Assert.That(path, Is.Empty);
        }

        // 시작이나 목표가 지도 밖이면 false와 빈 목록이어야 한다. 예외를 던지는 경우와 구별한다.
        // 마지막 사례는 범위 밖의 같은 좌표 둘이다. '시작=목표'만 먼저 검사해 성공하는 실수를 막는다.
        [TestCase(-1, 0, 1, 1)]
        [TestCase(0, 0, 2, 1)]
        [TestCase(0, 2, 1, 1)]
        [TestCase(0, 0, 1, -1)]
        [TestCase(-1, -1, -1, -1)]
        public void OutsideEndpointsClearPreviousPath(int startX, int startY, int goalX, int goalY)
        {
            var path = new List<Vector2Int> { Vector2Int.zero };
            Assert.That(AStarPathfinder.TryFindPath(new GridMap(2, 2),
                new Vector2Int(startX, startY), new Vector2Int(goalX, goalY), path), Is.False);
            Assert.That(path, Is.Empty);
        }

        // true면 시작 칸을, false면 목표 칸을 벽으로 만든다. 어느 쪽이 막혀도 실패해야 한다.
        // 이전 경로가 남으면 적이 잘못 움직일 수 있으므로 반환값과 목록 초기화를 함께 확인한다.
        [TestCase(true)]
        [TestCase(false)]
        public void BlockedEndpointClearsPreviousPath(bool blockStart)
        {
            var grid = new GridMap(2, 2);
            var start = new Vector2Int(0, 0);
            var goal = new Vector2Int(1, 1);
            grid.SetWalkable(blockStart ? start : goal, false);
            var path = new List<Vector2Int> { start, goal };
            Assert.That(AStarPathfinder.TryFindPath(grid, start, goal, path), Is.False);
            Assert.That(path, Is.Empty);
        }

        // 3×1 복도에서 가운데 문을 열기→닫기→열기를 반복한다. 열린 때 비용 20, 닫힌 때는 길이 없다.
        // 같은 grid와 path를 재사용하므로, 이전 호출의 열린/닫힌 목록이나 출력 경로가 섞이면 잡힌다.
        [Test]
        public void RepeatedSearchSeesBlockedAndReopenedPassage()
        {
            var grid = new GridMap(3, 1);
            var path = new List<Vector2Int>();
            var start = new Vector2Int(0, 0);
            var goal = new Vector2Int(2, 0);
            for (int i = 0; i < 3; i++)
            {
                Assert.That(AStarPathfinder.TryFindPath(grid, start, goal, path), Is.True);
                Assert.That(ValidateAndGetCost(grid, path, start, goal, true), Is.EqualTo(20));
                grid.SetWalkable(new Vector2Int(1, 0), false);
                Assert.That(AStarPathfinder.TryFindPath(grid, start, goal, path), Is.False);
                Assert.That(path, Is.Empty);
                grid.SetWalkable(new Vector2Int(1, 0), true);
            }
        }

        // 5×3 지도의 중간 칸을 막으면 직선 비용 40이 우회 비용 60으로 늘고, 다시 열면 40으로 돌아와야 한다.
        // 단순 성공/실패뿐 아니라, 장애물이 사라져도 예전의 긴 경로를 계속 쓰는 문제를 확인한다.
        [Test]
        public void RepeatedSearchReplacesLongerRouteAfterObstacleChanges()
        {
            var grid = new GridMap(5, 3);
            var start = new Vector2Int(0, 1);
            var goal = new Vector2Int(4, 1);
            var path = new List<Vector2Int>();
            foreach (bool blocked in new[] { false, true, false })
            {
                grid.SetWalkable(new Vector2Int(2, 1), !blocked);
                Assert.That(AStarPathfinder.TryFindPath(grid, start, goal, path, false), Is.True);
                Assert.That(ValidateAndGetCost(grid, path, start, goal, false), Is.EqualTo(blocked ? 60 : 40));
            }
        }

        // 3×3의 가운데 (1,1)을 막고 왼쪽 (0,1)에서 오른쪽 (2,1)으로 간다. 위·아래 우회 비용은 같다.
        // f와 h가 같은 후보에서 y가 작은 쪽을 고르므로 y=0의 아래 경로가 매번 나와야 한다.
        // 대각을 켜도 가운데 벽의 모서리 때문에 지름길은 없다. 역방향 탐색을 끼워 넣어 상태 누수도 확인한다.
        [TestCase(false)]
        [TestCase(true)]
        public void EqualCostRoutesAreDeterministicAcrossOtherSearches(bool allowDiagonal)
        {
            var grid = new GridMap(3, 3);
            grid.SetWalkable(new Vector2Int(1, 1), false);
            var start = new Vector2Int(0, 1);
            var goal = new Vector2Int(2, 1);
            var expected = new[]
            {
                start, new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0), goal
            };
            var path = new List<Vector2Int>();
            for (int i = 0; i < 5; i++)
            {
                Assert.That(AStarPathfinder.TryFindPath(grid, start, goal, path, allowDiagonal), Is.True);
                Assert.That(path, Is.EqualTo(expected));
                Assert.That(AStarPathfinder.TryFindPath(grid, goal, start, path, !allowDiagonal), Is.True);
            }
        }

        // null은 '길 없음'이 아니라 필요한 객체를 전달하지 않은 호출 오류이므로 예외로 알린다.
        // grid만 null이어도 path는 먼저 비워져야 한다. path 자체가 null인 경우에는 비울 목록도 없다.
        [Test]
        public void NullArgumentsThrowAndValidOutputListIsCleared()
        {
            var path = new List<Vector2Int> { Vector2Int.zero };
            Assert.Throws<ArgumentNullException>(() =>
                AStarPathfinder.TryFindPath(null, Vector2Int.zero, Vector2Int.zero, path));
            Assert.That(path, Is.Empty);
            Assert.Throws<ArgumentNullException>(() =>
                AStarPathfinder.TryFindPath(new GridMap(1, 1), Vector2Int.zero, Vector2Int.zero, null));
        }

        // 사람이 고른 몇 가지 모양만으로 놓칠 수 있는 우회·막힘 조합을 무작위로 검사한다.
        // 고정 시드로 같은 지도와 질의 순서를 재현하며, 60개 지도×6쌍×2가지 이동 방식=720개 질의를 비교한다.
        // 정답 비용은 A*의 보조 함수를 쓰지 않는 Dijkstra로 구한다. 같은 비용의 경로 모양은 여러 개일 수 있다.
        // 따라서 경로 모양 전체 일치 대신 성공 여부·최단 비용·실제로 통행 가능한 경로인지를 검사한다.
        [TestCase(false)]
        [TestCase(true)]
        public void FixedSeedRandomGridsMatchIndependentDijkstra(bool allowDiagonal)
        {
            // Random은 반복문 밖에서 한 번 만든다. 매번 다시 만들면 같은 지도만 반복해서 검사하게 된다.
            var random = new System.Random(20261031);
            var grid = new GridMap(7, 6);
            var path = new List<Vector2Int>();
            for (int sample = 0; sample < 60; sample++)
            {
                // 같은 grid의 모든 칸을 다시 설정해 장애물 배치가 바뀐 뒤의 재탐색도 함께 검증한다.
                for (int y = 0; y < grid.Height; y++)
                    for (int x = 0; x < grid.Width; x++)
                        grid.SetWalkable(new Vector2Int(x, y), random.NextDouble() >= 0.28);

                for (int query = 0; query < 6; query++)
                {
                    var start = new Vector2Int(random.Next(grid.Width), random.Next(grid.Height));
                    var goal = new Vector2Int(random.Next(grid.Width), random.Next(grid.Height));
                    int expectedCost = DijkstraCost(grid, start, goal, allowDiagonal);
                    // 일부러 불가능한 좌표를 남겨 둔다. 성공·실패 어느 경우에도 입력 목록을 비워야 한다.
                    path.Add(new Vector2Int(-99, -99));
                    bool found = AStarPathfinder.TryFindPath(grid, start, goal, path, allowDiagonal);
                    // 실패 메시지에 지도 번호·질의 번호를 남겨 고정 시드에서 같은 상황을 다시 찾을 수 있게 한다.
                    string context = $"sample={sample}, query={query}, diagonal={allowDiagonal}, {start}->{goal}";
                    Assert.That(found, Is.EqualTo(expectedCost >= 0), context);
                    if (found)
                        Assert.That(ValidateAndGetCost(grid, path, start, goal, allowDiagonal),
                            Is.EqualTo(expectedCost), context);
                    else
                        Assert.That(path, Is.Empty, context);
                }
            }
        }

        // 기존 호출과 새 오버로드의 null 검사는 같은 경로를 내야 한다. 기존 AI/시연의 호출 호환성을 확인한다.
        [Test]
        public void NullTraversalPredicatePreservesOriginalPath()
        {
            var grid = new GridMap(5, 4);
            grid.SetWalkable(new Vector2Int(2, 1), false);
            var expected = new List<Vector2Int>();
            var actual = new List<Vector2Int>();
            var goal = new Vector2Int(4, 3);
            Assert.That(AStarPathfinder.TryFindPath(grid, Vector2Int.zero, goal, expected), Is.True);
            Assert.That(AStarPathfinder.TryFindPath(grid, Vector2Int.zero, goal, actual, true, null), Is.True);
            Assert.That(actual, Is.EqualTo(expected));
        }

        // 시작=목표도 구역 밖이면 실패해야 한다. 두 끝점의 제자리 구간 검사가 빠지는 오류를 잡는다.
        [TestCase(0, 0, 0, 0)]
        [TestCase(0, 0, 2, 0)]
        [TestCase(2, 0, 0, 0)]
        public void RestrictedEndpointClearsPath(int sx, int sy, int gx, int gy)
        {
            var path = new List<Vector2Int> { new Vector2Int(9, 9) };
            bool found = AStarPathfinder.TryFindPath(new GridMap(3, 1), new Vector2Int(sx, sy),
                new Vector2Int(gx, gy), path, true, (from, to) => from.x >= 1 && to.x >= 1);
            Assert.That(found, Is.False);
            Assert.That(path, Is.Empty);
        }

        // 벽의 위·아래 우회 비용은 같다. 기존 동점 규칙은 아래를 택하지만, 아래 구역이 금지되면 위를 찾아야 한다.
        // 경로 완성 뒤 단순 거부하는 방식으로는 이 검사를 통과하지 못한다. 탐색 중 제한을 적용해야 한다.
        [Test]
        public void TraversalConstraintFindsAlternativeInsideAllowedArea()
        {
            var grid = new GridMap(5, 5);
            for (int y = 1; y <= 3; y++) grid.SetWalkable(new Vector2Int(2, y), false);
            var start = new Vector2Int(0, 2);
            var goal = new Vector2Int(4, 2);
            var path = new List<Vector2Int>();
            Assert.That(AStarPathfinder.TryFindPath(grid, start, goal, path, false), Is.True);
            Assert.That(path.Exists(cell => cell.y == 0), Is.True);
            var area = new MovementConstraint2D(new Rect(-0.5f, 0.5f, 5f, 4f));
            Func<Vector2Int, Vector2Int, bool> allowed = (from, to) => area.AllowsSegment(from, to, 0.25f);
            Assert.That(AStarPathfinder.TryFindPath(grid, start, goal, path, false, allowed), Is.True);
            Assert.That(ValidateAndGetCost(grid, path, start, goal, false), Is.EqualTo(80));
            Assert.That(path.Exists(cell => cell.y == 4), Is.True);
            for (int i = 1; i < path.Count; i++) Assert.That(allowed(path[i - 1], path[i]), Is.True);
        }

        // 실제 대각 구간이 원 안이면 통과 가능하다. 지나가지 않는 직교 옆 칸의 구역 밖 여부는 무관하다.
        // 물리 벽이 있는 옆 칸은 기존 모서리 규칙대로 여전히 금지된다.
        [Test]
        public void DiagonalConstraintChecksActualSegmentAndKeepsWallCornerRule()
        {
            var grid = new GridMap(3, 3);
            var area = new MovementConstraint2D(new Rect(-1, -1, 4, 4), Vector2.one, 1f);
            var start = new Vector2Int(0, 1);
            var goal = new Vector2Int(1, 2);
            var path = new List<Vector2Int>();
            Func<Vector2Int, Vector2Int, bool> allowed = (from, to) => area.AllowsSegment(from, to, 0f);
            Assert.That(AStarPathfinder.TryFindPath(grid, start, goal, path, true, allowed), Is.True);
            Assert.That(ValidateAndGetCost(grid, path, start, goal, true), Is.EqualTo(14));
            grid.SetWalkable(new Vector2Int(0, 2), false);
            Assert.That(AStarPathfinder.TryFindPath(grid, start, goal, path, true, allowed), Is.True);
            Assert.That(ValidateAndGetCost(grid, path, start, goal, true), Is.EqualTo(20));
        }

        // 양 끝은 열려 있어도 중간 구간이 닫히면 실패한다. 같은 격자·목록으로 다시 열었을 때도 복구돼야 한다.
        [Test]
        public void ChangedTraversalRuleIsUsedByEachSearch()
        {
            var grid = new GridMap(3, 1);
            var path = new List<Vector2Int>();
            var goal = new Vector2Int(2, 0);
            bool doorOpen = true;
            Func<Vector2Int, Vector2Int, bool> allowed = (from, to) => from == to || doorOpen;
            Assert.That(AStarPathfinder.TryFindPath(grid, Vector2Int.zero, goal, path, false, allowed), Is.True);
            doorOpen = false;
            Assert.That(AStarPathfinder.TryFindPath(grid, Vector2Int.zero, goal, path, false, allowed), Is.False);
            Assert.That(path, Is.Empty);
            doorOpen = true;
            Assert.That(AStarPathfinder.TryFindPath(grid, Vector2Int.zero, goal, path, false, allowed), Is.True);
        }

        // 무작위 지도에 사각형·원·몸체 반경 제한을 추가해도 독립 Dijkstra와 최단 비용이 같아야 한다.
        // 한 방향만 허용하는 구간도 섞어 필터를 거꾸로 호출하거나 열린 목록을 잘못 재사용하는 오류를 검사한다.
        [TestCase(false)]
        [TestCase(true)]
        public void ConstrainedRandomGridsMatchIndependentDijkstra(bool allowDiagonal)
        {
            var random = new System.Random(20261105);
            var grid = new GridMap(7, 6);
            var path = new List<Vector2Int>();
            for (int sample = 0; sample < 40; sample++)
            {
                for (int y = 0; y < grid.Height; y++)
                    for (int x = 0; x < grid.Width; x++)
                        grid.SetWalkable(new Vector2Int(x, y), random.NextDouble() >= 0.2);
                var area = new MovementConstraint2D(new Rect(-0.5f, -0.5f, 7, 6),
                    new Vector2(random.Next(7), random.Next(6)), 2f + random.Next(4));
                Func<Vector2Int, Vector2Int, bool> allowed = (from, to) =>
                    area.AllowsSegment(from, to, 0.25f) && !(from.x == 3 && to.x == 2 && from.y % 2 == 0);
                for (int query = 0; query < 6; query++)
                {
                    var start = new Vector2Int(random.Next(7), random.Next(6));
                    var goal = new Vector2Int(random.Next(7), random.Next(6));
                    int expected = DijkstraCost(grid, start, goal, allowDiagonal, allowed);
                    bool found = AStarPathfinder.TryFindPath(grid, start, goal, path, allowDiagonal, allowed);
                    Assert.That(found, Is.EqualTo(expected >= 0), $"sample={sample}, query={query}");
                    if (!found) { Assert.That(path, Is.Empty); continue; }
                    Assert.That(ValidateAndGetCost(grid, path, start, goal, allowDiagonal), Is.EqualTo(expected));
                    for (int i = 1; i < path.Count; i++) Assert.That(allowed(path[i - 1], path[i]), Is.True);
                }
            }
        }

        // 성공했다고 받은 경로와 지도·시작·목표·대각 설정을 입력받아, 경로를 검사하고 총비용을 반환한다.
        // 시작·끝, 벽 통과, 제자리/먼 칸으로의 순간이동, 모서리 관통이 있으면 Assert가 그 테스트를 실패시킨다.
        private static int ValidateAndGetCost(GridMap grid, List<Vector2Int> path,
            Vector2Int start, Vector2Int goal, bool allowDiagonal)
        {
            Assert.That(path, Is.Not.Empty);
            Assert.That(path[0], Is.EqualTo(start));
            Assert.That(path[path.Count - 1], Is.EqualTo(goal));
            // 모든 걸음의 비용이 양수이므로 최단 경로가 같은 칸을 다시 돌 이유가 없다. 반복 방문도 검사한다.
            var visited = new HashSet<Vector2Int>();
            int cost = 0;
            for (int i = 0; i < path.Count; i++)
            {
                Assert.That(grid.IsWalkable(path[i]), Is.True);
                Assert.That(visited.Add(path[i]), Is.True, "경로에 순환이 없어야 합니다.");
                // 첫 칸은 출발점이다. 아직 이동하지 않았으므로 비용을 더하거나 이전 칸과 비교하지 않는다.
                if (i == 0)
                    continue;

                Vector2Int previous = path[i - 1];
                // 인접 칸이면 축별 차이가 0 또는 1이고, 두 축이 모두 0일 수는 없다.
                int dx = Math.Abs(path[i].x - previous.x);
                int dy = Math.Abs(path[i].y - previous.y);
                Assert.That(dx <= 1 && dy <= 1 && dx + dy > 0, Is.True);
                bool diagonal = dx == 1 && dy == 1;
                if (diagonal)
                {
                    // 이전 (0,0), 현재 (1,1)이라면 옆의 (0,1)과 (1,0)도 열린 칸인지 다시 검사한다.
                    Assert.That(allowDiagonal, Is.True);
                    Assert.That(grid.IsWalkable(new Vector2Int(previous.x, path[i].y)), Is.True);
                    Assert.That(grid.IsWalkable(new Vector2Int(path[i].x, previous.y)), Is.True);
                }
                cost += diagonal ? 14 : 10;
            }
            return cost;
        }

        // 같은 이동 규칙으로 최소 비용만 구하는 독립 비교용 구현이다. 경로가 없으면 -1을 반환한다.
        // A*와 달리 h를 쓰지 않고, 시작에서 실제로 쌓인 비용만 작은 순서로 칸을 확정한다.
        // A*의 후보 목록·부모 기록·휴리스틱을 재사용하지 않아, 그 부분의 오류를 함께 복제할 가능성을 줄인다.
        private static int DijkstraCost(GridMap grid, Vector2Int start, Vector2Int goal, bool allowDiagonal,
            Func<Vector2Int, Vector2Int, bool> canTraverse = null)
        {
            if (!grid.IsWalkable(start) || !grid.IsWalkable(goal))
                return -1;
            if (canTraverse != null && (!canTraverse(start, start) || !canTraverse(goal, goal)))
                return -1;

            // distance는 지금까지 찾은 비용, settled는 최단 비용 확정 여부다.
            // 아직 모르는 칸은 int.MaxValue를 무한대 표시로 쓰고 시작 칸만 0으로 둔다.
            var distance = new int[grid.Width, grid.Height];
            var settled = new bool[grid.Width, grid.Height];
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                    distance[x, y] = int.MaxValue;
            distance[start.x, start.y] = 0;

            while (true)
            {
                // 작은 테스트 지도이므로 모든 칸을 훑어 미확정 칸 중 가장 싼 곳을 고른다.
                // 이렇게 구현 구조도 A*의 열린 목록 선택과 분리한다. 게임용 최적화를 위한 코드는 아니다.
                int best = int.MaxValue;
                var current = new Vector2Int(-1, -1);
                for (int y = 0; y < grid.Height; y++)
                    for (int x = 0; x < grid.Width; x++)
                        if (!settled[x, y] && distance[x, y] < best)
                        {
                            current = new Vector2Int(x, y);
                            best = distance[x, y];
                        }

                // 유한한 비용의 후보가 없다면 시작에서 더 갈 곳이 없다. 무한대에 이동비를 더하지 않고 끝낸다.
                if (best == int.MaxValue)
                    return -1;
                if (current == goal)
                    return best;
                settled[current.x, current.y] = true;

                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        // -1~1의 상대 좌표를 조합한다. axes=0은 제자리, 1은 직교, 2는 대각이다.
                        // 런타임의 Directions 배열을 복사하지 않고 이웃을 만든다.
                        int axes = Math.Abs(dx) + Math.Abs(dy);
                        if (axes == 0 || (axes == 2 && !allowDiagonal))
                            continue;
                        var next = new Vector2Int(current.x + dx, current.y + dy);
                        if (!grid.IsWalkable(next) || settled[next.x, next.y])
                            continue;
                        if (axes == 2 && (!grid.IsWalkable(new Vector2Int(current.x + dx, current.y)) ||
                            !grid.IsWalkable(new Vector2Int(current.x, current.y + dy))))
                            continue;
                        if (canTraverse != null && !canTraverse(current, next))
                            continue;

                        // 현재 칸을 거치는 길이 기존 기록보다 싸면 낮춘다. 값이 큰 이전 경로를 고집하지 않는다.
                        distance[next.x, next.y] = Math.Min(distance[next.x, next.y], best + (axes == 2 ? 14 : 10));
                    }
                }
            }
        }
    }
}
