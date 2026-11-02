using UnityEngine;

namespace GameAi
{
    public struct RayHit
    {
        public bool Hit;
        public Vector2Int Cell;
        public Vector2 Point;
        public float Distance;
    }

    // 레이가 지나는 칸을 받아 보는 쪽. 힙 할당을 막으려고 구조체로 넘긴다
    public interface ICellVisitor
    {
        void Visit(int x, int y);
    }

    public struct NoVisitor : ICellVisitor
    {
        public void Visit(int x, int y) { }
    }

    // 격자 DDA. 시야·총알·빛은 Physics2D 대신 모두 이것을 쓴다
    public static class Raycast
    {
        public static RayHit Cast(GridMap grid, Vector2 origin, Vector2 dir, float maxDistance, RayMask mask)
        {
            var visitor = new NoVisitor();
            return Cast(grid, origin, dir, maxDistance, mask, ref visitor);
        }

        // dir은 정규화된 벡터. 시작 칸은 검사하지 않는다(문 안에 서서 쏘는 경우 등).
        // 멈춘 칸까지 visitor로 알린다.
        public static RayHit Cast<TVisitor>(GridMap grid, Vector2 origin, Vector2 dir, float maxDistance, RayMask mask, ref TVisitor visitor)
            where TVisitor : struct, ICellVisitor
        {
            int x = Mathf.FloorToInt(origin.x);
            int y = Mathf.FloorToInt(origin.y);
            visitor.Visit(x, y);

            int stepX = dir.x > 0 ? 1 : dir.x < 0 ? -1 : 0;
            int stepY = dir.y > 0 ? 1 : dir.y < 0 ? -1 : 0;
            float tDeltaX = stepX != 0 ? Mathf.Abs(1f / dir.x) : float.PositiveInfinity;
            float tDeltaY = stepY != 0 ? Mathf.Abs(1f / dir.y) : float.PositiveInfinity;
            float tMaxX = stepX != 0 ? FirstCrossing(origin.x, x, stepX) / Mathf.Abs(dir.x) : float.PositiveInfinity;
            float tMaxY = stepY != 0 ? FirstCrossing(origin.y, y, stepY) / Mathf.Abs(dir.y) : float.PositiveInfinity;

            const float CornerEpsilon = 1e-4f;
            while (true)
            {
                float t;
                if (Mathf.Abs(tMaxX - tMaxY) < CornerEpsilon)
                {
                    // 정확히 모서리를 지날 때: 양옆이 모두 막혀 있으면 틈으로 빠져나가지 못한다
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
                    t = tMaxX;
                    if (t > maxDistance) break;
                    x += stepX;
                    tMaxX += tDeltaX;
                }
                else
                {
                    t = tMaxY;
                    if (t > maxDistance) break;
                    y += stepY;
                    tMaxY += tDeltaY;
                }

                visitor.Visit(x, y);
                if (grid.BlocksRay(x, y, mask))
                    return MakeHit(new Vector2Int(x, y), origin, dir, t);
            }

            return new RayHit { Hit = false, Point = origin + dir * maxDistance, Distance = maxDistance };
        }

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
