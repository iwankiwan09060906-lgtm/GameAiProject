using UnityEngine;

namespace GameAi
{
    // 소리 세기 계산(7.6). 순수 함수
    public static class SoundPropagation
    {
        // 벽·닫힌 문·창문 덩어리를 센다. 0.5m 칸 맵에서 두꺼운 벽이 여러 칸이어도 "벽 하나"로 센다
        struct WallCounter : ICellVisitor
        {
            public GridMap Grid;
            public Vector2Int End;
            public int Walls;
            bool inside;

            public void Visit(int x, int y)
            {
                bool blocking = IsDamping(Grid, x, y) && !(x == End.x && y == End.y);
                if (blocking && !inside) Walls++;
                inside = blocking;
            }
        }

        public static bool IsDamping(GridMap grid, int x, int y)
        {
            if (!grid.InBounds(x, y)) return true;
            switch (grid.GetTile(x, y))
            {
                case TileType.Wall:
                case TileType.Window:
                    return true;
                case TileType.Door:
                    return !grid.IsDoorOpen(x, y);
                default:
                    return false;
            }
        }

        public static int CountWalls(GridMap grid, Vector2 source, Vector2 listener)
        {
            Vector2 delta = listener - source;
            float distance = delta.magnitude;
            if (distance < 1e-5f) return 0;
            var counter = new WallCounter { Grid = grid, End = grid.WorldToCell(listener) };
            // 시작 칸(소리가 난 자리)은 세지 않는다
            Vector2Int start = grid.WorldToCell(source);
            if (IsDamping(grid, start.x, start.y)) counter.Walls--;
            Raycast.Cast(grid, source, delta / distance, distance, RayMask.Sound, ref counter);
            return Mathf.Max(0, counter.Walls);
        }

        // 세기 = (1 - d/R) × wallMult^벽 수. 반경 밖이면 0
        public static float Intensity(GridMap grid, Vector2 source, float radius, Vector2 listener, float wallMult)
        {
            if (radius <= 0f) return 0f;
            float d = Vector2.Distance(source, listener);
            if (d > radius) return 0f;
            float strength = 1f - d / radius;
            int walls = CountWalls(grid, source, listener);
            return strength * Mathf.Pow(wallMult, walls);
        }
    }
}
