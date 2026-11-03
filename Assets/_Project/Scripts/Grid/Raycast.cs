using UnityEngine;

namespace GameAi
{
    // 레이가 어디에 닿았는지의 결과
    public struct RayHit
    {
        // 막는 칸에 닿았는지. false면 최대 거리까지 아무것도 없었다
        public bool Hit;

        // 닿은 칸
        public Vector2Int Cell;

        // 닿은 지점(칸의 경계)
        public Vector2 Point;

        // 시작점에서 닿은 지점까지의 거리
        public float Distance;
    }

    // 레이가 지나가는 칸을 하나씩 받아 보는 쪽. 시야가 "본 칸"을 표시할 때 쓴다.
    // 인터페이스를 구조체로 구현해 제네릭에 넘기면 호출이 인라인되고 메모리 할당이 없다
    public interface ICellVisitor
    {
        void Visit(int x, int y);
    }

    // 지나는 칸에 관심이 없을 때 쓰는 빈 구현
    public struct NoVisitor : ICellVisitor
    {
        public void Visit(int x, int y) { }
    }

    // 격자 위의 레이캐스트(DDA, Amanatides & Woo). 시야·총알·빛은 Physics2D 대신 모두 이것을 쓴다(CLAUDE.md).
    // 레이가 칸 경계를 넘을 때마다 다음 칸 하나만 검사하므로, 사거리 안의 칸 수에 비례해서만 일한다.
    public static class Raycast
    {
        public static RayHit Cast(GridMap grid, Vector2 origin, Vector2 dir, float maxDistance, RayMask mask)
        {
            var visitor = new NoVisitor();
            return Cast(grid, origin, dir, maxDistance, mask, ref visitor);
        }

        // dir은 정규화된 방향 벡터. 시작 칸은 막힘 검사를 하지 않는다(문 안에 서서 쏘는 경우 등).
        // 막는 칸을 만나면 그 칸에서 멈추고, 지나온 칸(멈춘 칸 포함)을 visitor로 알린다.
        public static RayHit Cast<TVisitor>(GridMap grid, Vector2 origin, Vector2 dir, float maxDistance, RayMask mask, ref TVisitor visitor)
            where TVisitor : struct, ICellVisitor
        {
            int x = Mathf.FloorToInt(origin.x);
            int y = Mathf.FloorToInt(origin.y);
            visitor.Visit(x, y);

            // 각 축으로 한 칸 나아가는 방향(-1, 0, +1)
            int stepX = dir.x > 0 ? 1 : dir.x < 0 ? -1 : 0;
            int stepY = dir.y > 0 ? 1 : dir.y < 0 ? -1 : 0;

            // tDelta: 한 칸을 가로지르는 데 필요한 레이의 길이. 축과 평행하면 영원히 안 넘으므로 무한대
            float tDeltaX = stepX != 0 ? Mathf.Abs(1f / dir.x) : float.PositiveInfinity;
            float tDeltaY = stepY != 0 ? Mathf.Abs(1f / dir.y) : float.PositiveInfinity;

            // tMax: 지금 칸을 벗어나 다음 칸 경계에 닿는 레이의 길이. 둘 중 작은 쪽 경계를 먼저 넘는다
            float tMaxX = stepX != 0 ? FirstCrossing(origin.x, x, stepX) / Mathf.Abs(dir.x) : float.PositiveInfinity;
            float tMaxY = stepY != 0 ? FirstCrossing(origin.y, y, stepY) / Mathf.Abs(dir.y) : float.PositiveInfinity;

            const float CornerEpsilon = 1e-4f;
            while (true)
            {
                float t;
                if (Mathf.Abs(tMaxX - tMaxY) < CornerEpsilon)
                {
                    // 정확히 모서리를 지나는 경우: 대각선으로 두 칸 경계를 동시에 넘는다.
                    // 양옆 두 칸이 모두 막혀 있으면 벽 사이 틈으로 빠져나가지 못하게 거기서 멈춘다.
                    t = tMaxX;
                    if (t > maxDistance) break;
                    if (grid.BlocksRay(x + stepX, y, mask) && grid.BlocksRay(x, y + stepY, mask))
                    {
                        visitor.Visit(x + stepX, y);
                        visitor.Visit(x, y + stepY);
                        return MakeHit(new Vector2Int(x + stepX, y), origin, dir, t);
                    }
                    x += stepX;
                    y += stepY;
                    tMaxX += tDeltaX;
                    tMaxY += tDeltaY;
                }
                else if (tMaxX < tMaxY)
                {
                    // 가로 경계를 먼저 넘는다
                    t = tMaxX;
                    if (t > maxDistance) break;
                    x += stepX;
                    tMaxX += tDeltaX;
                }
                else
                {
                    // 세로 경계를 먼저 넘는다
                    t = tMaxY;
                    if (t > maxDistance) break;
                    y += stepY;
                    tMaxY += tDeltaY;
                }

                visitor.Visit(x, y);
                if (grid.BlocksRay(x, y, mask))
                    return MakeHit(new Vector2Int(x, y), origin, dir, t);
            }

            // 사거리 안에 막는 칸이 없었다
            return new RayHit { Hit = false, Point = origin + dir * maxDistance, Distance = maxDistance };
        }

        // 시작점에서 진행 방향의 첫 칸 경계까지의 거리(축 방향 성분만)
        static float FirstCrossing(float origin, int cell, int step)
        {
            return step > 0 ? cell + 1 - origin : origin - cell;
        }

        static RayHit MakeHit(Vector2Int cell, Vector2 origin, Vector2 dir, float t)
        {
            return new RayHit { Hit = true, Cell = cell, Point = origin + dir * t, Distance = t };
        }
    }
}
