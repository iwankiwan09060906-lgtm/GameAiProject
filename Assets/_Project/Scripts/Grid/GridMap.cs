using UnityEngine;

namespace GameAi
{
    // 1칸 = 1m. 원점은 왼쪽 아래 칸의 왼쪽 아래 모서리, 칸 중심은 (x + 0.5, y + 0.5)
    public sealed class GridMap
    {
        readonly TileType[] tiles;
        readonly FloorType[] floors;
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

        public int Index(int x, int y)
        {
            return y * Width + x;
        }

        public bool InBounds(int x, int y)
        {
            return x >= 0 && y >= 0 && x < Width && y < Height;
        }

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

        public bool BlocksMove(int x, int y)
        {
            if (!InBounds(x, y)) return true;
            int i = Index(x, y);
            return TileRules.BlocksMove(tiles[i], doorOpen[i]);
        }

        public bool BlocksRay(int x, int y, RayMask mask)
        {
            if (!InBounds(x, y)) return true;
            int i = Index(x, y);
            return TileRules.BlocksRay(tiles[i], doorOpen[i], mask);
        }

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
