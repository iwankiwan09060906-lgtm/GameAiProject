using UnityEngine;

namespace GameAi
{
    public static class LineOfSight
    {
        // a에서 b까지 막힘이 없는지
        public static bool IsClear(GridMap grid, Vector2 a, Vector2 b, RayMask mask)
        {
            Vector2 delta = b - a;
            float distance = delta.magnitude;
            if (distance < 1e-5f) return true;
            return !Raycast.Cast(grid, a, delta / distance, distance, mask).Hit;
        }

        // b 칸 자체가 막는 칸이어도(문 등) 그 칸까지 닿으면 보이는 것으로 친다
        public static bool ReachesCell(GridMap grid, Vector2 a, Vector2Int cell, RayMask mask)
        {
            Vector2 target = GridMap.CellCenter(cell);
            Vector2 delta = target - a;
            float distance = delta.magnitude;
            if (distance < 1e-5f) return true;
            RayHit hit = Raycast.Cast(grid, a, delta / distance, distance, mask);
            return !hit.Hit || hit.Cell == cell;
        }
    }
}
