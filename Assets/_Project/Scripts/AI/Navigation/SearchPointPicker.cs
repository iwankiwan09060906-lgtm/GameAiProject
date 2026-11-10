using System.Collections.Generic;
using UnityEngine;

namespace GameAi
{
    // 수색 지점 고르기(7.7): 중심 반경 안에서 걸어서 닿는 칸 중 무작위 N곳을 가까운 순서로
    public static class SearchPointPicker
    {
        static readonly Queue<Vector2Int> Queue = new Queue<Vector2Int>();
        static readonly List<Vector2Int> Reach = new List<Vector2Int>();
        static readonly HashSet<int> Seen = new HashSet<int>();

        public static List<Vector2> Pick(AStarPathfinder nav, GridMap grid, Vector2 center, Vector2 from, float radius, int count, RandomSource rng)
        {
            var result = new List<Vector2>();
            Vector2Int start = grid.WorldToCell(center);
            if (!nav.IsWalkable(start.x, start.y) && !nav.TryNearestWalkable(start, 4, true, out start)) return result;

            // 반경 안 너비 우선 탐색(닫힌 문은 지나가지 않는다: 수색은 방 안 위주)
            Queue.Clear();
            Reach.Clear();
            Seen.Clear();
            Queue.Enqueue(start);
            Seen.Add(grid.Index(start.x, start.y));
            int limitCells = Mathf.CeilToInt(radius / grid.CellSize);
            while (Queue.Count > 0)
            {
                Vector2Int c = Queue.Dequeue();
                Reach.Add(c);
                for (int k = 0; k < 4; k++)
                {
                    int nx = c.x + (k == 0 ? 1 : k == 1 ? -1 : 0);
                    int ny = c.y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    if (!nav.IsWalkable(nx, ny)) continue;
                    if (grid.GetTile(nx, ny) == TileType.Door && !grid.IsDoorOpen(nx, ny)) continue;
                    if (Mathf.Abs(nx - start.x) > limitCells || Mathf.Abs(ny - start.y) > limitCells) continue;
                    if (Vector2.Distance(grid.CellCenter(nx, ny), center) > radius) continue;
                    if (!Seen.Add(grid.Index(nx, ny))) continue;
                    Queue.Enqueue(new Vector2Int(nx, ny));
                }
            }

            // 무작위 N곳(AI 난수원). 서로 1.5m 이상 떨어지게 몇 번 다시 뽑는다
            int tries = count * 8;
            while (result.Count < count && Reach.Count > 0 && tries-- > 0)
            {
                Vector2 p = grid.CellCenter(Reach[rng.Int(0, Reach.Count)]);
                bool tooClose = false;
                foreach (Vector2 q in result)
                    if (Vector2.Distance(p, q) < 1.5f) { tooClose = true; break; }
                if (!tooClose || tries < count) result.Add(p);
            }
            result.Sort((a, b) => Vector2.SqrMagnitude(a - from).CompareTo(Vector2.SqrMagnitude(b - from)));
            return result;
        }
    }
}
