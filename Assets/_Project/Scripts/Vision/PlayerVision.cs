using UnityEngine;

namespace GameAi
{
    // 플레이어 시야(FN-VIS-01): 마우스 방향 기준 90° 부채꼴, 12m. 벽·닫힌 문·높은 엄폐물 너머는 가린다.
    // 부채꼴 안쪽으로 레이를 여러 개 쏘고, 레이가 지난 칸을 "보이는 칸"으로 표시한다.
    public sealed class PlayerVision
    {
        // 레이가 지나는 칸을 PlayerVision에 표시하는 구조체. 구조체 + 제네릭이라 메모리 할당이 없다
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

        // 칸별로 지금 보이는지. 매 프레임 비우고 다시 채운다
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

        // position에서 aimDir 방향으로 시야를 다시 계산한다
        public void Compute(Vector2 position, Vector2 aimDir)
        {
            System.Array.Clear(visible, 0, visible.Length);

            // 조준 방향을 가운데로, 좌우 fov/2씩 균등하게 벌려 쏜다
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

        // 레이 사이로 스쳐 지나가 닿지 못한 벽면이 구멍처럼 남지 않게,
        // 보이는 빈 칸에 맞닿은 막힌 칸(벽 등)도 보이게 한다
        void RevealAdjacentBlockers()
        {
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    // 보이는 빈 칸에서만 이웃을 확인한다
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
