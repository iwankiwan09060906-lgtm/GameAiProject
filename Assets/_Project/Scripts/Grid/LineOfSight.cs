using UnityEngine;

namespace GameAi
{
    // 두 지점 사이가 트여 있는지 묻는 도구. Raycast를 한 번 쏘는 얇은 포장이다
    public static class LineOfSight
    {
        // a에서 b까지 mask에 막히는 칸이 없는지
        public static bool IsClear(GridMap grid, Vector2 a, Vector2 b, RayMask mask)
        {
            Vector2 delta = b - a;
            float distance = delta.magnitude;

            // 같은 지점이면 막힐 것이 없다(0으로 나누기 방지)
            if (distance < 1e-5f) return true;
            return !Raycast.Cast(grid, a, delta / distance, distance, mask).Hit;
        }

        // 목표 칸 자체가 막는 칸(닫힌 문 등)이어도, 그 칸까지 닿으면 보이는 것으로 친다.
        // 문 앞에서 "[SPACE] 열기"를 띄울 때, 벽 너머의 문은 제외하고 이 문은 고르기 위해 쓴다
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
