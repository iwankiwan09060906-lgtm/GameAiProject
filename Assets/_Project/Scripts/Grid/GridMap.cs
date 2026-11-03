using UnityEngine;

namespace GameAi
{
    // 맵의 격자 데이터. 순수 C#이라 Unity 없이도 테스트할 수 있다.
    // 1칸 = 1m. 원점은 왼쪽 아래 칸의 왼쪽 아래 모서리, 칸 중심은 (x + 0.5, y + 0.5).
    // 1차원 배열 하나에 [y * Width + x] 순서로 담아 메모리 이동이 적고 빠르다.
    public sealed class GridMap
    {
        readonly TileType[] tiles;
        readonly FloorType[] floors;

        // 문 칸마다 열림 여부. 문이 아닌 칸은 항상 false
        readonly bool[] doorOpen;

        public int Width { get; }
        public int Height { get; }

        public GridMap(int width, int height)
        {
            Width = width;
            Height = height;
            tiles = new TileType[width * height];
            floors = new FloorType[width * height];
            doorOpen = new bool[width * height];
        }

        // (x, y)를 1차원 배열 번호로 바꾼다. 다른 클래스가 칸별 배열을 따로 만들 때도 같은 번호를 쓴다
        public int Index(int x, int y)
        {
            return y * Width + x;
        }

        public bool InBounds(int x, int y)
        {
            return x >= 0 && y >= 0 && x < Width && y < Height;
        }

        // 맵 밖은 벽으로 취급한다. 레이와 이동이 맵 밖으로 새지 않게 하는 안전장치
        public TileType GetTile(int x, int y)
        {
            return InBounds(x, y) ? tiles[Index(x, y)] : TileType.Wall;
        }

        public void SetTile(int x, int y, TileType type)
        {
            tiles[Index(x, y)] = type;
        }

        public FloorType GetFloor(int x, int y)
        {
            return InBounds(x, y) ? floors[Index(x, y)] : FloorType.Outdoor;
        }

        public void SetFloor(int x, int y, FloorType type)
        {
            floors[Index(x, y)] = type;
        }

        public bool IsDoorOpen(int x, int y)
        {
            return InBounds(x, y) && doorOpen[Index(x, y)];
        }

        public void SetDoorOpen(int x, int y, bool open)
        {
            doorOpen[Index(x, y)] = open;
        }

        // 걸어서 지나갈 수 없는 칸인지(맵 밖 포함)
        public bool BlocksMove(int x, int y)
        {
            if (!InBounds(x, y)) return true;
            int i = Index(x, y);
            return TileRules.BlocksMove(tiles[i], doorOpen[i]);
        }

        // 해당 종류의 레이(시야·총알·빛)를 막는 칸인지(맵 밖 포함)
        public bool BlocksRay(int x, int y, RayMask mask)
        {
            if (!InBounds(x, y)) return true;
            int i = Index(x, y);
            return TileRules.BlocksRay(tiles[i], doorOpen[i], mask);
        }

        // 월드 좌표가 속한 칸. 음수 좌표에서도 정확하도록 FloorToInt를 쓴다
        public Vector2Int WorldToCell(Vector2 world)
        {
            return new Vector2Int(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y));
        }

        public static Vector2 CellCenter(int x, int y)
        {
            return new Vector2(x + 0.5f, y + 0.5f);
        }

        public static Vector2 CellCenter(Vector2Int cell)
        {
            return CellCenter(cell.x, cell.y);
        }
    }
}
