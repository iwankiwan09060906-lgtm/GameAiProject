using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace GameAi
{
    // 그리는 순서. 숫자가 클수록 위에 그린다. 모든 스프라이트의 sortingOrder를 여기서 정한다
    public static class SortOrder
    {
        public const int Floor = 0;
        public const int Decal = 5;
        public const int Wall = 10;
        public const int WallRim = 11;
        public const int Prop = 12;
        public const int Door = 13;
        public const int Corpse = 15;
        public const int Character = 20;
        public const int Held = 21;
        public const int Fx = 30;

        // 안개(탐색 기억)는 월드 위에 덮는다
        public const int Fog = 100;
    }

    // 맵 데이터를 Tilemap으로 그린다(FN-MAP-01)
    public sealed class MapLoader
    {
        readonly AssetCatalog catalog;

        // 같은 (그림, 색) 조합의 Tile을 하나만 만들어 재사용한다
        readonly Dictionary<(Sprite, Color), Tile> tiles = new Dictionary<(Sprite, Color), Tile>();

        public MapLoader(AssetCatalog catalog)
        {
            this.catalog = catalog;
        }

        public void Build(ParsedMap map, MapData data, GameConfig config, Transform root)
        {
            var gridObject = new GameObject("Grid", typeof(Grid));
            gridObject.transform.SetParent(root, false);

            // 바닥, 벽·창문, 소품(엄폐물·풀숲)을 서로 다른 레이어로 나눠 그린다
            GridMap grid = map.Grid;
            Tilemap floors = CreateLayer("Floor", gridObject.transform, SortOrder.Floor);
            Tilemap walls = CreateLayer("Walls", gridObject.transform, SortOrder.Wall);
            Tilemap props = CreateLayer("Props", gridObject.transform, SortOrder.Prop);

            Sprite wall = catalog.GetSprite("tile_wall");
            Sprite window = catalog.GetSprite("tile_window");
            Sprite[] coverLow = catalog.GetVariants("cover_low");
            Sprite[] coverHigh = catalog.GetVariants("cover_high");
            Sprite[] foliage = catalog.GetVariants("foliage");

            // FloorType 순서(Wood, Tile, Carpet, Outdoor)와 같은 순서여야 한다
            Sprite[] floorSprites =
            {
                catalog.GetSprite("tile_floor_wood"),
                catalog.GetSprite("tile_floor_tile"),
                catalog.GetSprite("tile_floor_carpet"),
                catalog.GetSprite("tile_floor_outdoor"),
            };

            // 전역광은 하나뿐이라 야외가 실내보다 더 어두운 것은 바닥 색으로 표현한다(야외/실내 비율)
            float outdoor = Mathf.Clamp01(data.ambient.outdoor / Mathf.Max(0.01f, data.ambient.indoor));
            var outdoorTint = new Color(outdoor, outdoor, outdoor, 1f);

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var cell = new Vector3Int(x, y, 0);
                    TileType type = grid.GetTile(x, y);

                    // 벽은 바닥 없이 벽 그림만 놓는다
                    if (type == TileType.Wall)
                    {
                        walls.SetTile(cell, GetTile(wall, Color.white));
                        continue;
                    }

                    // 나머지 칸은 모두 바닥을 깔고, 그 위에 창문·소품을 얹는다
                    FloorType floor = grid.GetFloor(x, y);
                    Color tint = floor == FloorType.Outdoor ? outdoorTint : Color.white;
                    floors.SetTile(cell, GetTile(floorSprites[(int)floor], tint));

                    switch (type)
                    {
                        case TileType.Window: walls.SetTile(cell, GetTile(window, Color.white)); break;
                        case TileType.CoverLow: props.SetTile(cell, GetTile(Pick(coverLow, x, y), Color.white)); break;
                        case TileType.CoverHigh: props.SetTile(cell, GetTile(Pick(coverHigh, x, y), Color.white)); break;
                        case TileType.Foliage: props.SetTile(cell, GetTile(Pick(foliage, x, y), outdoorTint)); break;
                    }
                }
            }

            CreateWallRim(grid, config, gridObject.transform);
        }

        // Tilemap 레이어 하나를 만든다. 조명을 받는 재질을 쓴다
        Tilemap CreateLayer(string name, Transform parent, int order)
        {
            var layer = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            layer.transform.SetParent(parent, false);
            var renderer = layer.GetComponent<TilemapRenderer>();
            renderer.sortingOrder = order;
            renderer.sharedMaterial = catalog.SpriteLit;
            return layer.GetComponent<Tilemap>();
        }

        // (그림, 색) 조합의 Tile을 돌려준다. 없으면 만들어 보관한다
        Tile GetTile(Sprite sprite, Color color)
        {
            var key = (sprite, color);
            if (tiles.TryGetValue(key, out Tile tile)) return tile;
            tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.color = color;
            tile.colliderType = Tile.ColliderType.None;
            tiles[key] = tile;
            return tile;
        }

        // 변형 그림 중 하나를 칸 좌표 해시로 고른다. 난수를 쓰지 않으므로 항상 같은 모습이다
        static Sprite Pick(Sprite[] variants, int x, int y)
        {
            int hash = (x * 73856093) ^ (y * 19349663);
            return variants[Mathf.Abs(hash % variants.Length)];
        }

        // 벽이 벽 아닌 칸과 맞닿은 변에만 1px 밝은 선을 그린다(ASSET_DIRECTION 1.1).
        // 칸마다 테두리를 그리면 격자가 보이므로, 맵 전체를 덮는 텍스처 한 장에 선만 그려 둔다
        void CreateWallRim(GridMap grid, GameConfig config, Transform parent)
        {
            int ppu = config.pixels_per_unit;
            int width = grid.Width * ppu;
            int height = grid.Height * ppu;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color32[width * height];
            Color32 rim = MathUtil.ParseColor(config.lighting.wall_rim_color, new Color(0.23f, 0.23f, 0.27f));

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    if (grid.GetTile(x, y) != TileType.Wall) continue;
                    int left = x * ppu;
                    int bottom = y * ppu;

                    // 왼쪽·오른쪽·아래·위 이웃이 벽이 아니면 그 변에 선을 긋는다
                    if (IsOpenSide(grid, x - 1, y)) DrawLine(pixels, width, left, bottom, 0, 1, ppu, rim);
                    if (IsOpenSide(grid, x + 1, y)) DrawLine(pixels, width, left + ppu - 1, bottom, 0, 1, ppu, rim);
                    if (IsOpenSide(grid, x, y - 1)) DrawLine(pixels, width, left, bottom, 1, 0, ppu, rim);
                    if (IsOpenSide(grid, x, y + 1)) DrawLine(pixels, width, left, bottom + ppu - 1, 1, 0, ppu, rim);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            var rimObject = new GameObject("WallRim", typeof(SpriteRenderer));
            rimObject.transform.SetParent(parent, false);
            var renderer = rimObject.GetComponent<SpriteRenderer>();
            renderer.sprite = Sprite.Create(texture, new Rect(0, 0, width, height), Vector2.zero, ppu);

            // 벽 그림자 안에 있어도 윤곽이 보이도록 조명을 받지 않게 한다
            renderer.sharedMaterial = catalog.SpriteUnlit;
            renderer.sortingOrder = SortOrder.WallRim;
        }

        // 맵 안의 벽 아닌 칸인지(맵 밖은 선을 긋지 않는다)
        static bool IsOpenSide(GridMap grid, int x, int y)
        {
            return grid.InBounds(x, y) && grid.GetTile(x, y) != TileType.Wall;
        }

        // 시작 픽셀에서 (stepX, stepY) 방향으로 length픽셀 선을 긋는다
        static void DrawLine(Color32[] pixels, int width, int x, int y, int stepX, int stepY, int length, Color32 color)
        {
            for (int i = 0; i < length; i++)
                pixels[(y + stepY * i) * width + x + stepX * i] = color;
        }
    }
}
