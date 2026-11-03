using UnityEngine;

namespace GameAi
{
    // 여러 곳에서 쓰는 작은 수학 도구
    public static class MathUtil
    {
        // 벡터의 각도(도). +X(오른쪽)가 0도, 반시계가 +
        public static float AngleDeg(Vector2 v)
        {
            return Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
        }

        // 각도(도)를 길이 1의 방향 벡터로
        public static Vector2 FromAngleDeg(float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
        }

        // 벡터를 반시계로 degrees만큼 돌린다
        public static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(r);
            float s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        // 점 p에서 칸(축 정렬 사각형)의 가장 가까운 점. 원과 칸의 충돌을 풀 때 쓴다
        public static Vector2 ClosestPointOnCell(Vector2 p, int x, int y)
        {
            return new Vector2(Mathf.Clamp(p.x, x, x + 1), Mathf.Clamp(p.y, y, y + 1));
        }

        // "#RRGGBB" 글자를 색으로. 형식이 틀리면 fallback
        public static Color ParseColor(string hex, Color fallback)
        {
            return ColorUtility.TryParseHtmlString(hex, out Color c) ? c : fallback;
        }
    }
}
