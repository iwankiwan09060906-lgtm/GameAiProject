using UnityEngine;

namespace GameAi
{
    // 플레이어 시야: 부채꼴로 레이를 쏴 닿은 칸을 표시한다
    public sealed class PlayerVision
    {
        struct MarkVisitor : ICellVisitor
        {
            public PlayerVision Owner;

            public void Visit(int x, int y)
            {
                Owner.Mark(x, y);
            }
        }

        readonly GridMap grid;
        readonly float fovDeg;
        readonly float viewM;
        readonly int rays;
        readonly bool[] visible;

        public PlayerVision(GridMap grid, float fovDeg, float viewM, int rays)
        {
            this.grid = grid;
            this.fovDeg = fovDeg;
            this.viewM = viewM;
            this.rays = Mathf.Max(2, rays);
            visible = new bool[grid.Width * grid.Height];
        }

        public bool IsVisible(int x, int y)
        {
            return grid.InBounds(x, y) && visible[grid.Index(x, y)];
        }

        public bool IsVisible(Vector2 world)
        {
            Vector2Int cell = grid.WorldToCell(world);
            return IsVisible(cell.x, cell.y);
        }

        public void Compute(Vector2 position, Vector2 aimDir)
        {
            System.Array.Clear(visible, 0, visible.Length);

            float center = MathUtil.AngleDeg(aimDir);
            var visitor = new MarkVisitor { Owner = this };
            for (int i = 0; i < rays; i++)
            {
                float angle = center - fovDeg / 2f + fovDeg * i / (rays - 1);
                Raycast.Cast(grid, position, MathUtil.FromAngleDeg(angle), viewM, RayMask.Sight, ref visitor);
            }

            RevealAdjacentBlockers();
        }

        void Mark(int x, int y)
        {
            if (grid.InBounds(x, y))
                visible[grid.Index(x, y)] = true;
        }

        // 스치듯 지나 레이가 닿지 못한 벽면이 구멍처럼 남지 않게, 보이는 칸 옆의 막힌 칸도 보이게 한다
        void RevealAdjacentBlockers()
        {
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    if (!visible[grid.Index(x, y)] || grid.BlocksRay(x, y, RayMask.Sight)) continue;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx;
                            int ny = y + dy;
                            if (grid.InBounds(nx, ny) && grid.BlocksRay(nx, ny, RayMask.Sight))
                                visible[grid.Index(nx, ny)] = true;
                        }
                    }
                }
            }
        }
    }
}
