using System.Collections.Generic;
using UnityEngine;

namespace GameAi
{
    // 직접 구현한 A*(7.15). 8방향, 직선 1·대각 1.414(칸 단위), 옥타일 휴리스틱, 대각 모서리 금지, 닫힌 문 +2.
    // 칸이 몸보다 작으면(0.5m 칸) 벽 옆 칸을 막아 몸이 벽에 끼지 않는 경로를 먼저 찾고, 없으면 여유 없이 다시 찾는다.
    public sealed class AStarPathfinder
    {
        const float Diagonal = 1.41421356f;
        public const float DoorCost = 2f;

        static readonly int[] Dx = { 1, -1, 0, 0, 1, 1, -1, -1 };
        static readonly int[] Dy = { 0, 0, 1, -1, 1, -1, 1, -1 };

        readonly GridMap grid;
        readonly bool[] walkableRaw;
        readonly bool[] walkableWide;
        readonly float[] g;
        readonly int[] parent;
        readonly int[] stamp;
        readonly bool[] closed;
        readonly MinHeap open;
        int searchId;

        public int Width { get { return grid.Width; } }
        public int Height { get { return grid.Height; } }
        // 몇 칸을 벽에서 띄우는지(몸 반경 기준)
        public int Clearance { get; }
        public int LastExpanded { get; private set; }

        public AStarPathfinder(GridMap grid, float agentRadius)
        {
            this.grid = grid;
            int n = grid.Width * grid.Height;
            walkableRaw = new bool[n];
            walkableWide = new bool[n];
            g = new float[n];
            parent = new int[n];
            stamp = new int[n];
            closed = new bool[n];
            open = new MinHeap(n);
            Clearance = Mathf.Max(0, Mathf.CeilToInt((agentRadius - grid.CellSize * 0.5f) / grid.CellSize - 1e-4f));
            Rebuild();
        }

        // 벽·엄폐물·창문·풀숲은 고정 장애물. 문은 열고 지나갈 수 있으니 통과 가능으로 본다
        static bool IsStatic(GridMap grid, int x, int y)
        {
            if (!grid.InBounds(x, y)) return true;
            TileType t = grid.GetTile(x, y);
            return t != TileType.Floor && t != TileType.Door;
        }

        public void Rebuild()
        {
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    int i = grid.Index(x, y);
                    walkableRaw[i] = !IsStatic(grid, x, y);
                    bool wide = walkableRaw[i];
                    for (int oy = -Clearance; oy <= Clearance && wide; oy++)
                        for (int ox = -Clearance; ox <= Clearance && wide; ox++)
                            if (IsStatic(grid, x + ox, y + oy) && grid.GetTile(x + ox, y + oy) != TileType.Door) wide = false;
                    walkableWide[i] = wide;
                }
            }
        }

        public bool IsWalkable(int x, int y, bool wide = true)
        {
            if (!grid.InBounds(x, y)) return false;
            return (wide ? walkableWide : walkableRaw)[grid.Index(x, y)];
        }

        // 막힌 칸이면 가까운 통과 가능 칸을 찾는다(반경 칸 수 안)
        public bool TryNearestWalkable(Vector2Int cell, int maxRadius, bool wide, out Vector2Int result)
        {
            for (int r = 0; r <= maxRadius; r++)
            {
                float best = float.MaxValue;
                result = cell;
                bool found = false;
                for (int oy = -r; oy <= r; oy++)
                {
                    for (int ox = -r; ox <= r; ox++)
                    {
                        if (Mathf.Max(Mathf.Abs(ox), Mathf.Abs(oy)) != r) continue;
                        int x = cell.x + ox, y = cell.y + oy;
                        if (!IsWalkable(x, y, wide)) continue;
                        float d = ox * ox + oy * oy;
                        if (d < best) { best = d; result = new Vector2Int(x, y); found = true; }
                    }
                }
                if (found) return true;
            }
            result = cell;
            return false;
        }

        // 경로(시작·도착 포함)를 path에 채운다. 실패하면 false와 빈 목록
        public bool FindPath(Vector2Int start, Vector2Int goal, List<Vector2Int> path)
        {
            path.Clear();
            if (Clearance > 0 && Search(start, goal, path, true)) return true;
            return Search(start, goal, path, false);
        }

        bool Search(Vector2Int start, Vector2Int goal, List<Vector2Int> path, bool wide)
        {
            bool[] walkable = wide ? walkableWide : walkableRaw;
            if (!grid.InBounds(start.x, start.y)) return false;
            if (!IsWalkable(start.x, start.y, wide) && !TryNearestWalkable(start, 2, wide, out start)) return false;
            if (!IsWalkable(goal.x, goal.y, wide) && !TryNearestWalkable(goal, 3, wide, out goal)) return false;

            searchId++;
            open.Clear();
            int startIndex = grid.Index(start.x, start.y);
            int goalIndex = grid.Index(goal.x, goal.y);
            Touch(startIndex);
            g[startIndex] = 0f;
            parent[startIndex] = -1;
            open.Push(startIndex, Heuristic(start, goal));
            LastExpanded = 0;

            while (open.Count > 0)
            {
                int current = open.Pop();
                if (closed[current]) continue;
                closed[current] = true;
                LastExpanded++;
                if (current == goalIndex)
                {
                    Build(goalIndex, path);
                    return true;
                }
                int cx = current % grid.Width, cy = current / grid.Width;
                for (int k = 0; k < 8; k++)
                {
                    int nx = cx + Dx[k], ny = cy + Dy[k];
                    if (!grid.InBounds(nx, ny)) continue;
                    int ni = grid.Index(nx, ny);
                    if (!walkable[ni]) continue;
                    bool diagonal = k >= 4;
                    // 모서리 규칙: 대각 이동 시 옆 두 칸 중 하나라도 막히면 통과 불가
                    if (diagonal && (!walkable[grid.Index(cx + Dx[k], cy)] || !walkable[grid.Index(cx, cy + Dy[k])])) continue;
                    float step = diagonal ? Diagonal : 1f;
                    if (grid.GetTile(nx, ny) == TileType.Door && !grid.IsDoorOpen(nx, ny)) step += DoorCost;
                    Touch(ni);
                    if (closed[ni]) continue;
                    float cost = g[current] + step;
                    if (cost >= g[ni]) continue;
                    g[ni] = cost;
                    parent[ni] = current;
                    open.Push(ni, cost + Heuristic(new Vector2Int(nx, ny), goal));
                }
            }
            return false;
        }

        void Touch(int i)
        {
            if (stamp[i] == searchId) return;
            stamp[i] = searchId;
            g[i] = float.MaxValue;
            closed[i] = false;
        }

        void Build(int goalIndex, List<Vector2Int> path)
        {
            for (int i = goalIndex; i >= 0; i = parent[i])
                path.Add(new Vector2Int(i % grid.Width, i / grid.Width));
            path.Reverse();
        }

        public static float Heuristic(Vector2Int a, Vector2Int b)
        {
            int dx = Mathf.Abs(a.x - b.x), dy = Mathf.Abs(a.y - b.y);
            return (dx + dy) + (Diagonal - 2f) * Mathf.Min(dx, dy);
        }

        // 경로 비용(칸 단위). 테스트용
        public float PathCost(List<Vector2Int> path)
        {
            float cost = 0f;
            for (int i = 1; i < path.Count; i++)
            {
                Vector2Int a = path[i - 1], b = path[i];
                cost += (a.x != b.x && a.y != b.y) ? Diagonal : 1f;
                if (grid.GetTile(b.x, b.y) == TileType.Door && !grid.IsDoorOpen(b.x, b.y)) cost += DoorCost;
            }
            return cost;
        }

        // 직접 구현한 이진 힙(최소값 우선)
        sealed class MinHeap
        {
            readonly int[] items;
            readonly float[] keys;
            public int Count { get; private set; }

            public MinHeap(int capacity)
            {
                items = new int[capacity * 8 + 8];
                keys = new float[capacity * 8 + 8];
            }

            public void Clear() { Count = 0; }

            public void Push(int item, float key)
            {
                if (Count >= items.Length) return;
                int i = Count++;
                items[i] = item;
                keys[i] = key;
                while (i > 0)
                {
                    int p = (i - 1) / 2;
                    if (keys[p] <= keys[i]) break;
                    Swap(i, p);
                    i = p;
                }
            }

            public int Pop()
            {
                int top = items[0];
                Count--;
                items[0] = items[Count];
                keys[0] = keys[Count];
                int i = 0;
                while (true)
                {
                    int l = i * 2 + 1, r = l + 1, m = i;
                    if (l < Count && keys[l] < keys[m]) m = l;
                    if (r < Count && keys[r] < keys[m]) m = r;
                    if (m == i) break;
                    Swap(i, m);
                    i = m;
                }
                return top;
            }

            void Swap(int a, int b)
            {
                int ti = items[a]; items[a] = items[b]; items[b] = ti;
                float tk = keys[a]; keys[a] = keys[b]; keys[b] = tk;
            }
        }
    }
}
