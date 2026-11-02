using UnityEngine;

namespace GameAi
{
    public static class MathUtil
    {
        // 벡터의 각도(도). +X가 0
        public static float AngleDeg(Vector2 v)
        {
            return Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
        }

        public static Vector2 FromAngleDeg(float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
        }

        public static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(r);
            float s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        // 원 위의 점에서 칸(축 정렬 사각형)까지 가장 가까운 점
        public static Vector2 ClosestPointOnCell(Vector2 p, int x, int y)
        {
            return new Vector2(Mathf.Clamp(p.x, x, x + 1), Mathf.Clamp(p.y, y, y + 1));
        }

        public static Color ParseColor(string hex, Color fallback)
        {
            return ColorUtility.TryParseHtmlString(hex, out Color c) ? c : fallback;
        }
    }
}
