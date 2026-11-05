// 역할: GridMap의 칸 정보를 받아, 시작에서 목표까지 이동 비용이 가장 작은 경로를 계산한다.
// 연결: GridWorld2D가 지도를 준비하고 EnemyAgent가 이 함수를 호출한 뒤 반환된 경로를 따라 움직인다.
// 이 파일은 좌표 목록만 계산한다. 실제 이동 속도, 월드 좌표 변환, Collider2D 검사는 어댑터의 역할이다.
// 읽는 순서: GridMap.cs → TryFindPath의 탐색 흐름 → FindBestIndex의 선택 기준 → Heuristic의 계산식.
// NavigationTests.cs에는 벽 우회·모서리 차단 등의 예와 별도 Dijkstra 알고리즘을 이용한 비교가 있다.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace NullTrace.AI
{
    // 객체를 따로 만들지 않고 사용하는 길찾기 함수 모음이다. NavMesh 없이 A*를 직접 구현한다.
    // 지도·시작·목표를 입력받아 성공 여부와 경로를 돌려주며, 탐색 기록은 호출 사이에 공유하지 않는다.
    public static class AStarPathfinder
    {
        // 현재 칸에 더할 상대 좌표다. (0, 1)은 위로 한 칸, (1, 1)은 오른쪽 위로 한 칸이다.
        // 앞 4개가 직교 방향이므로 대각 이동을 끄면 앞부분만 사용한다. 배열의 순서도 항상 일정하다.
        private static readonly Vector2Int[] Directions =
        {
            new Vector2Int(0, 1), new Vector2Int(1, 0),
            new Vector2Int(0, -1), new Vector2Int(-1, 0),
            new Vector2Int(1, 1), new Vector2Int(1, -1),
            new Vector2Int(-1, -1), new Vector2Int(-1, 1)
        };

        // grid와 칸 번호 start/goal, 결과를 받을 path를 입력한다. allowDiagonal의 기본값은 true다.
        // 성공하면 true와 [시작, ..., 목표]를 반환한다. 막힘·범위 밖·연결된 길 없음은 false와 빈 목록이다.
        // path는 기존 내용을 지우고 같은 목록에 결과를 채운다. null 인자는 호출 코드의 오류라 예외다.
        // 한 번의 탐색이 끝난 뒤 지도 상태를 바꾸고 다시 호출할 수 있다. 탐색 도중의 동시 수정은 지원하지 않는다.
        public static bool TryFindPath(GridMap grid, Vector2Int start, Vector2Int goal,
            List<Vector2Int> path, bool allowDiagonal = true)
        {
            // 기존 호출자는 추가 구역 제한 없이 같은 알고리즘을 사용한다. 공개 API와 기본 대각 설정을 유지한다.
            return TryFindPath(grid, start, goal, path, allowDiagonal, null);
        }

        // canTraverse는 '이 칸에서 저 칸으로 몸 전체가 통과 가능한가'를 호출자가 알려 주는 검사 함수다.
        // null이면 기존 동작이다. 시작/목표에는 (cell, cell)을 전달해 그 자리에 설 수 있는지도 확인한다.
        // 검사 함수는 한 탐색 동안 같은 입력에 같은 결과를 내야 하며, 지도나 영역을 도중에 바꾸면 안 된다.
        // 제한을 탐색 중 적용하므로 최단 후보가 구역 밖이어도 내부에 다른 우회로가 있으면 계속 찾아본다.
        public static bool TryFindPath(GridMap grid, Vector2Int start, Vector2Int goal,
            List<Vector2Int> path, bool allowDiagonal, Func<Vector2Int, Vector2Int, bool> canTraverse)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            // 지난번에는 성공했어도 이번에는 실패할 수 있다. 이전 경로로 계속 이동하지 않도록 먼저 비운다.
            path.Clear();
            if (grid == null)
                throw new ArgumentNullException(nameof(grid));
            // 범위 밖도 IsWalkable이 false로 처리한다. 아래의 '시작=목표'보다 먼저 검사해야
            // 벽 속의 같은 칸을 입력했을 때 이동 가능한 경로라고 잘못 성공하지 않는다.
            if (!grid.IsWalkable(start) || !grid.IsWalkable(goal))
                return false;
            if (canTraverse != null && (!canTraverse(start, start) || !canTraverse(goal, goal)))
                return false;
            if (start == goal)
            {
                // 이동 횟수는 0이지만 경로는 존재한다. 출발 칸 한 개를 반환해 성공 경로의 형식을 지킨다.
                path.Add(start);
                return true;
            }

            // open(열린 목록): 발견했지만 아직 이웃을 조사하지 않은 후보들이다. 처음에는 시작 칸만 있다.
            // closed(닫힌 목록): 최단 비용을 확정하고 조사를 마친 칸들이다. 다시 조사하는 낭비를 막는다.
            // costs는 칸별 g, 즉 시작에서 그 칸까지 지금까지 발견한 최소 이동 비용을 기억한다.
            // parents는 '그 칸에 가장 싸게 도달하려면 직전에 어느 칸을 거쳤나'를 기억한다.
            // 매번 새로 만들므로 문이 닫혔다 열리거나 다른 적이 호출해도 이전 탐색 기록이 섞이지 않는다.
            var open = new List<Vector2Int> { start };
            var closed = new HashSet<Vector2Int>();
            var costs = new Dictionary<Vector2Int, long> { [start] = 0 };
            var parents = new Dictionary<Vector2Int, Vector2Int>();

            while (open.Count > 0)
            {
                // 후보 중 예상 총비용 f=g+h가 가장 작은 칸을 꺼낸다. h는 남은 비용의 낙관적인 추정이다.
                // 예: 시작 (0,0), 현재 (1,1), 목표 (3,2), 대각 허용인 빈 지도라면
                // g=14, h=대각 1회 14+직교 1회 10=24이므로 f=38이다. 자세한 동점 규칙은 아래에 있다.
                int bestIndex = FindBestIndex(open, costs, goal, allowDiagonal);
                Vector2Int current = open[bestIndex];
                open.RemoveAt(bestIndex);
                // 목표를 '처음 발견했을 때'가 아니라 '최선 후보로 꺼냈을 때' 끝내야 최단 경로가 확정된다.
                if (current == goal)
                {
                    // parents는 앞으로 갈 칸이 아니라 바로 전 칸을 가리킨다.
                    // S→A→G였다면 G, A, S 순서로 모은 뒤 Reverse로 S, A, G 순서로 뒤집는다.
                    path.Add(current);
                    while (current != start)
                    {
                        current = parents[current];
                        path.Add(current);
                    }
                    path.Reverse();
                    return true;
                }

                // 이 구현의 h는 일관적인 휴리스틱이므로 꺼낸 칸을 더 싼 경로로 다시 열 필요가 없다.
                // 따라서 확정된 칸은 closed에 넣고, 남은 이웃 중 이동 가능한 후보만 조사한다.
                closed.Add(current);
                int directionCount = allowDiagonal ? Directions.Length : 4;
                for (int i = 0; i < directionCount; i++)
                {
                    Vector2Int direction = Directions[i];
                    Vector2Int next = current + direction;
                    if (!grid.IsWalkable(next) || closed.Contains(next))
                        continue;

                    // x와 y가 동시에 변하면 대각 이동이다. (0,0)→(1,1)에서는 도착 (1,1)뿐 아니라
                    // 옆의 (1,0)과 (0,1)도 모두 비어 있어야 한다. 하나만 벽이어도 모서리를 스치며 못 지난다.
                    bool diagonal = direction.x != 0 && direction.y != 0;
                    if (diagonal &&
                        (!grid.IsWalkable(current + new Vector2Int(direction.x, 0)) ||
                         !grid.IsWalkable(current + new Vector2Int(0, direction.y))))
                        continue;

                    // 물리 벽의 모서리 규칙과 이동 목적의 구역 규칙은 둘 다 지킨다.
                    // 구역 검사는 실제 대각 구간에 적용한다. 지나가지 않는 옆 칸의 중심까지 구역 안일 필요는 없다.
                    if (canTraverse != null && !canTraverse(current, next))
                        continue;

                    // 비용은 초나 미터가 아닌 경로 비교용 점수다. 직교 한 칸을 10으로 두고,
                    // 더 긴 대각 한 칸은 약 10×√2를 정수로 나타낸 14를 쓴다. 두 직교 이동의 20보다 싸다.
                    // 현재 칸까지의 g에 한 걸음 비용을 더한다. long은 긴 경로의 누적값에 여유를 준다.
                    long cost = costs[current] + (diagonal ? 14 : 10);
                    bool discovered = costs.TryGetValue(next, out long previousCost);
                    // 이미 비용 30인 길을 찾았는데 새 길도 30이거나 40이면 바꾸지 않는다.
                    // 더 싼 길만 채택하므로, 동점 때 부모가 계속 바뀌며 결과가 흔들리는 일도 막는다.
                    if (discovered && cost >= previousCost)
                        continue;

                    // 예: 기존 40보다 새 길 30이 싸면 비용과 부모를 함께 고친다.
                    // 처음 찾은 칸만 open에 추가한다. 이미 후보였던 칸은 다음 선택 때 최신 costs로 비교된다.
                    costs[next] = cost;
                    parents[next] = current;
                    if (!discovered)
                        open.Add(next);
                }
            }

            // 모든 후보를 조사했는데 목표를 꺼내지 못했다. 벽이 길을 완전히 끊은 경우 등이 해당한다.
            // 경로는 성공할 때만 채웠으므로 처음 비운 상태 그대로 호출자에게 돌아간다.
            return false;
        }

        // 비어 있지 않은 open과 현재 g 기록을 받아, 다음에 조사할 칸의 '목록 안 위치'를 반환한다.
        // 비교 순서는 f, h, y, x의 오름차순이다. f가 같으면 목표에 가까운 후보(h가 작은 쪽)를 고른다.
        // f와 h도 같다면 (0,0)과 (0,2) 중 y가 작은 (0,0), 같은 y에서는 x가 작은 칸을 고른다.
        // 좌표까지 규칙으로 정하므로 지도·시작·목표·대각 설정이 같으면 경로가 매번 동일해진다.
        private static int FindBestIndex(List<Vector2Int> open,
            Dictionary<Vector2Int, long> costs, Vector2Int goal, bool allowDiagonal)
        {
            // shortcut: 열린 목록을 선형 탐색하며, 큰 맵의 반복 탐색이 병목이면 최소 힙으로 교체한다.
            int bestIndex = 0;
            long bestH = Heuristic(open[0], goal, allowDiagonal);
            long bestF = costs[open[0]] + bestH;
            for (int i = 1; i < open.Count; i++)
            {
                Vector2Int candidate = open[i];
                Vector2Int best = open[bestIndex];
                long h = Heuristic(candidate, goal, allowDiagonal);
                long f = costs[candidate] + h;
                // 현재 최선보다 총비용이 크거나, 총비용 동점에서 남은 추정 비용이 더 크면 제외한다.
                if (f > bestF || (f == bestF && h > bestH))
                    continue;
                // 비용이 모두 같을 때만 좌표 규칙을 적용한다. 비용이 더 싼 후보를 좌표 때문에 버리지 않는다.
                if (f == bestF && h == bestH &&
                    (candidate.y > best.y || (candidate.y == best.y && candidate.x >= best.x)))
                    continue;

                bestIndex = i;
                bestH = h;
                bestF = f;
            }
            return bestIndex;
        }

        // 현재 칸·목표·대각 허용 여부로부터 '장애물이 전혀 없다면 남은 최소 비용' h를 반환한다.
        // dx, dy는 칸 수 차이다. (0,0)→(3,2)라면 4방향은 직교 5회여서 50,
        // 8방향은 대각 2회와 직교 1회여서 14×2+10=38이다.
        // 식의 -6은 직교 두 번의 20을 대각 한 번의 14로 바꿀 때 절약되는 점수다.
        // 실제 벽은 길을 더 길게 만들 뿐이므로 이 값은 정답보다 크게 짐작하지 않는다(허용적 휴리스틱).
        // 또한 한 걸음 이동할 때 h의 감소량은 그 걸음 비용을 넘지 않는다(일관성).
        // 이 성질 덕분에 f가 최소인 칸을 꺼내 확정하면, 나중에 더 싼 경로가 나와 다시 열 필요가 없다.
        private static long Heuristic(Vector2Int cell, Vector2Int goal, bool allowDiagonal)
        {
            long dx = Math.Abs((long)cell.x - goal.x);
            long dy = Math.Abs((long)cell.y - goal.y);
            return allowDiagonal ? 10 * (dx + dy) - 6 * Math.Min(dx, dy) : 10 * (dx + dy);
        }
    }
}
