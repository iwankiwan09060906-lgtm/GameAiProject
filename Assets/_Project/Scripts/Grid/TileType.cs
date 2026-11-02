namespace GameAi
{
    public enum TileType { Floor, Wall, Door, Window, CoverLow, CoverHigh, Foliage }

    public enum FloorType { Wood, Tile, Carpet, Outdoor }

    // 레이가 무엇에 막히는지 고르는 값
    public enum RayMask { Sight, Bullet, Light }

    // MASTER_PROMPT 9.3 표. 칸 종류별 차단 규칙을 한곳에 모은다
    public static class TileRules
    {
        public static bool BlocksMove(TileType type, bool doorOpen)
        {
            switch (type)
            {
                case TileType.Floor: return false;
                case TileType.Door: return !doorOpen;
                default: return true;
            }
        }

        public static bool BlocksRay(TileType type, bool doorOpen, RayMask mask)
        {
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
