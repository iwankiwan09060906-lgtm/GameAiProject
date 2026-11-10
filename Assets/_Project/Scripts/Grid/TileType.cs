namespace GameAi
{
    // 칸의 종류(9.3 표). 벽·문·창문·엄폐물이 이동·시야·총알·빛을 막는 규칙이 서로 다르다
    public enum TileType { Floor, Wall, Door, Window, CoverLow, CoverHigh, Foliage }

    // 바닥 그림 종류. 야외(Outdoor)는 전역광이 더 어둡고 야외 환경음이 켜진다
    public enum FloorType { Wood, Tile, Carpet, Outdoor }

    // 레이가 무엇에 막히는지 고르는 값. 같은 칸이라도 시야·총알·빛이 다르게 막힌다
    public enum RayMask { Sight, Bullet, Light, Sound }

    // 칸 종류별 차단 규칙을 한곳에 모았다. 규칙을 바꿀 때는 이 파일만 고치면 된다
    public static class TileRules
    {
        // 걸어서 지나갈 수 없는 칸인지. 열린 문과 바닥만 지나갈 수 있다
        public static bool BlocksMove(TileType type, bool doorOpen)
        {
            switch (type)
            {
                case TileType.Floor: return false;
                case TileType.Door: return !doorOpen;
                default: return true;
            }
        }

        // 레이를 막는 칸인지.
        // 벽·높은 엄폐물은 전부 막고, 닫힌 문도 전부 막는다.
        // 창문과 낮은 엄폐물은 총알만 막고 시야·빛은 통과시킨다.
        // 풀숲과 열린 문은 아무것도 막지 않는다.
        public static bool BlocksRay(TileType type, bool doorOpen, RayMask mask)
        {
            // 소리는 막히지 않고 감쇠만 한다(SoundPropagation이 칸을 센다)
            if (mask == RayMask.Sound) return false;
            switch (type)
            {
                case TileType.Wall:
                case TileType.CoverHigh:
                    return true;
                case TileType.Door:
                    return !doorOpen;
                case TileType.Window:
                case TileType.CoverLow:
                    return mask == RayMask.Bullet;
                default:
                    return false;
            }
        }
    }
}
