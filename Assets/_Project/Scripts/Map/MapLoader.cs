using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace GameAi
{
    // 그리는 순서. 숫자가 클수록 위에 그린다. 모든 스프라이트의 sortingOrder를 여기서 정한다
    public static class SortOrder
    {
        public const int Floor = 0;

        // 바닥에 붙은 장식(케이블·얼룩·바닥 표시)은 혈흔보다 아래
        public const int FloorProp = 3;
        public const int Decal = 5;
        public const int Wall = 10;
        public const int WallRim = 11;
        public const int Prop = 12;
        public const int Door = 13;

        // 책상 위 서류·모니터처럼 다른 소품 위에 얹는 장식
        public const int PropTop = 14;
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
        // 벽 옆 바닥에 까는 그늘의 픽셀별 진하기(벽에서 가까운 순서). 벽이 바닥 위로 솟아 보이게 한다
        static readonly byte[] WallShade = { 120, 80, 48, 24 };

        readonly AssetCatalog catalog;

        // 같은 (그림, 색) 조합의 Tile을 하나만 만들어 재사용한다
        readonly Dictionary<(Sprite, Color), Tile> tiles = new Dictionary<(Sprite, Color), Tile>();

        // 바닥·벽 그림을 칸 크기 조각으로 자른 것. (원본, 가로 번호, 세로 번호)마다 하나
        readonly Dictionary<(Sprite, int, int), Sprite> pieces = new Dictionary<(Sprite, int, int), Sprite>();

        int pixelsPerUnit;
        float cellSize;

        public MapLoader(AssetCatalog catalog)
        {
            this.catalog = catalog;
        }

        public void Build(ParsedMap map, MapData data, GameConfig config, Transform root)
        {
            GridMap grid = map.Grid;
            pixelsPerUnit = config.pixels_per_unit;
            cellSize = grid.CellSize;

            var gridObject = new GameObject("Grid", typeof(Grid));
            gridObject.transform.SetParent(root, false);
            gridObject.GetComponent<Grid>().cellSize = new Vector3(cellSize, cellSize, 0f);

            // 바닥, 벽·창문, 소품(엄폐물·풀숲)을 서로 다른 레이어로 나눠 그린다
            Tilemap floors = CreateLayer("Floor", gridObject.transform, SortOrder.Floor);
            Tilemap walls = CreateLayer("Walls", gridObject.transform, SortOrder.Wall);
            Tilemap props = CreateLayer("Props", gridObject.transform, SortOrder.Prop);

            Sprite wall = catalog.GetSprite("tile_wall");
            Sprite window = catalog.GetSprite("tile_window");
            Sprite[] coverLow = catalog.GetVariants("cover_low");
            Sprite[] coverHigh = catalog.GetVariants("cover_high");
            Sprite[] foliage = catalog.GetVariants("foliage");
            Sprite[] floorSprites = FloorSprites(data);

            // 장식 소품이 있는 맵은 엄폐물 칸(c·C)의 그림을 소품이 대신한다
            bool decorated = data.props != null && data.props.Length > 0;

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
                        walls.SetTile(cell, GetTile(Piece(wall, x, y), Color.white));
                        continue;
                    }

                    // 나머지 칸은 모두 바닥을 깔고, 그 위에 창문·소품을 얹는다
                    FloorType floor = grid.GetFloor(x, y);
                    Color tint = floor == FloorType.Outdoor ? outdoorTint : Color.white;
                    floors.SetTile(cell, GetTile(Piece(floorSprites[(int)floor], x, y), tint));

                    // 칸이 작으면 창문은 벽 조각 위에 오버레이(CreateWallOverlay)가 유리를 그린다
                    if (type == TileType.Window)
                    {
                        walls.SetTile(cell, GetTile(cellSize < 1f ? Piece(wall, x, y) : window, Color.white));
                        continue;
                    }
                    if (decorated) continue;
                    switch (type)
                    {
                        case TileType.CoverLow: props.SetTile(cell, GetScaledTile(Pick(coverLow, x, y), Color.white)); break;
                        case TileType.CoverHigh: props.SetTile(cell, GetScaledTile(Pick(coverHigh, x, y), Color.white)); break;
                        case TileType.Foliage: props.SetTile(cell, GetScaledTile(Pick(foliage, x, y), outdoorTint)); break;
                    }
                }
            }

            CreateWallOverlay(grid, config, gridObject.transform);
            if (decorated) CreateProps(data.props, root);
        }

        // FloorType 순서(Wood, Tile, Carpet, Outdoor)와 같은 순서의 바닥 그림. 맵이 정한 그림이 없으면 기본 그림
        Sprite[] FloorSprites(MapData data)
        {
            string[] defaults = { "tile_floor_wood", "tile_floor_tile", "tile_floor_carpet", "tile_floor_outdoor" };
            var result = new Sprite[defaults.Length];
            for (int i = 0; i < defaults.Length; i++)
            {
                string custom = data.floor_sprites != null && i < data.floor_sprites.Length ? data.floor_sprites[i] : null;
                Sprite sprite = string.IsNullOrEmpty(custom) ? null : catalog.GetSprite(custom);
                if (!string.IsNullOrEmpty(custom) && sprite == null)
                    Debug.LogWarning("[MapLoader] 바닥 그림이 없어 기본 그림을 씁니다: " + custom);
                result[i] = sprite != null ? sprite : catalog.GetSprite(defaults[i]);
            }
            return result;
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

        // 1m 그림을 칸 크기로 줄여 그리는 Tile(장식 없는 맵의 엄폐물·풀숲)
        Tile GetScaledTile(Sprite sprite, Color color)
        {
            Tile tile = GetTile(sprite, color);
            tile.transform = Matrix4x4.Scale(new Vector3(cellSize, cellSize, 1f));
            return tile;
        }

        // 바닥·벽 그림에서 (x, y) 칸에 해당하는 조각. 1m(32px) 그림이면 0.5m 칸 4개가 한 장을 나눠 쓰고,
        // 2m(64px) 그림이면 16칸이 나눠 써서 칸보다 큰 무늬가 이어진다. 칸이 1m면 그림 그대로
        Sprite Piece(Sprite source, int x, int y)
        {
            if (source == null) return null;
            int piecePixels = Mathf.RoundToInt(pixelsPerUnit * cellSize);
            Rect rect = source.textureRect;
            int across = Mathf.FloorToInt(rect.width / piecePixels);
            int down = Mathf.FloorToInt(rect.height / piecePixels);
            if (piecePixels <= 0 || across < 1 || down < 1 || (across == 1 && down == 1)) return source;

            int ix = x % across;
            int iy = y % down;
            var key = (source, ix, iy);
            if (pieces.TryGetValue(key, out Sprite piece)) return piece;
            var area = new Rect(rect.x + ix * piecePixels, rect.y + iy * piecePixels, piecePixels, piecePixels);
            piece = Sprite.Create(source.texture, area, new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.FullRect);
            pieces[key] = piece;
            return piece;
        }

        // 변형 그림 중 하나를 칸 좌표 해시로 고른다. 난수를 쓰지 않으므로 항상 같은 모습이다
        static Sprite Pick(Sprite[] variants, int x, int y)
        {
            int hash = (x * 73856093) ^ (y * 19349663);
            return variants[Mathf.Abs(hash % variants.Length)];
        }

        // 장식 소품. 그림만 있고 판정에는 쓰지 않는다. 바닥 장식은 혈흔 아래, 세운 물건은 혈흔 위, 책상 위 물건은 그 위에 그린다
        void CreateProps(MapProp[] list, Transform root)
        {
            var parent = new GameObject("Props").transform;
            parent.SetParent(root, false);
            var missing = new HashSet<string>();
            foreach (MapProp prop in list)
            {
                Sprite sprite = catalog.GetSprite(prop.sprite);
                if (sprite == null)
                {
                    if (missing.Add(prop.sprite))
                        Debug.LogWarning("[MapLoader] 소품 그림이 없어 건너뜁니다: " + prop.sprite);
                    continue;
                }
                var go = new GameObject(prop.sprite, typeof(SpriteRenderer));
                go.transform.SetParent(parent, false);
                go.transform.position = prop.Position;
                go.transform.rotation = Quaternion.Euler(0f, 0f, prop.rot);
                var renderer = go.GetComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sharedMaterial = catalog.SpriteLit;
                renderer.sortingOrder = prop.layer <= 0 ? SortOrder.FloorProp : prop.layer == 1 ? SortOrder.Prop : SortOrder.PropTop;
            }
        }

        // 맵 전체를 덮는 텍스처 한 장에 벽 윤곽선, 벽 옆 그늘, 작은 칸의 창문 유리를 그린다(ASSET_DIRECTION 1.1).
        // 칸마다 테두리를 그리면 격자가 보이므로, 벽이 벽 아닌 칸과 맞닿은 변에만 1px 밝은 선을 긋는다
        void CreateWallOverlay(GridMap grid, GameConfig config, Transform parent)
        {
            int cellPixels = Mathf.Max(1, Mathf.RoundToInt(config.pixels_per_unit * cellSize));
            int width = grid.Width * cellPixels;
            int height = grid.Height * cellPixels;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color32[width * height];
            Color32 rim = MathUtil.ParseColor(config.lighting.wall_rim_color, new Color(0.23f, 0.23f, 0.27f));
            Color32 glass = MathUtil.ParseColor(config.lighting.window_glass_color, new Color(0.11f, 0.17f, 0.22f));
            Color32 glint = MathUtil.ParseColor(config.lighting.window_glint_color, new Color(0.62f, 0.77f, 0.85f));

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    TileType type = grid.GetTile(x, y);
                    if (type != TileType.Wall && type != TileType.Window) continue;
                    int left = x * cellPixels;
                    int bottom = y * cellPixels;

                    // 1m 칸의 창문은 창문 타일 그림이 있으므로 그늘·윤곽만 그린다
                    if (type == TileType.Window && cellSize < 1f)
                        DrawGlass(grid, pixels, width, x, y, left, bottom, cellPixels, glass, glint);

                    // 왼쪽·오른쪽·아래·위 이웃이 벽이 아니면 그 변에 선을 긋고, 이웃 바닥 쪽에 그늘을 깐다
                    if (IsOpenSide(grid, x - 1, y))
                    {
                        DrawLine(pixels, width, left, bottom, 0, 1, cellPixels, rim);
                        Shade(pixels, width, left - 1, bottom, -1, 0, cellPixels);
                    }
                    if (IsOpenSide(grid, x + 1, y))
                    {
                        DrawLine(pixels, width, left + cellPixels - 1, bottom, 0, 1, cellPixels, rim);
                        Shade(pixels, width, left + cellPixels, bottom, 1, 0, cellPixels);
                    }
                    if (IsOpenSide(grid, x, y - 1))
                    {
                        DrawLine(pixels, width, left, bottom, 1, 0, cellPixels, rim);
                        Shade(pixels, width, left, bottom - 1, 0, -1, cellPixels);
                    }
                    if (IsOpenSide(grid, x, y + 1))
                    {
                        DrawLine(pixels, width, left, bottom + cellPixels - 1, 1, 0, cellPixels, rim);
                        Shade(pixels, width, left, bottom + cellPixels, 0, 1, cellPixels);
                    }
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            var rimObject = new GameObject("WallRim", typeof(SpriteRenderer));
            rimObject.transform.SetParent(parent, false);
            var renderer = rimObject.GetComponent<SpriteRenderer>();
            renderer.sprite = Sprite.Create(texture, new Rect(0, 0, width, height), Vector2.zero, config.pixels_per_unit);

            // 벽 그림자 안에 있어도 윤곽이 보이도록 조명을 받지 않게 한다
            renderer.sharedMaterial = catalog.SpriteUnlit;
            renderer.sortingOrder = SortOrder.WallRim;
        }

        // 맵 안의 벽 아닌 칸인지(맵 밖은 선을 긋지 않는다). 창문도 벽처럼 윤곽을 공유한다
        static bool IsOpenSide(GridMap grid, int x, int y)
        {
            if (!grid.InBounds(x, y)) return false;
            TileType type = grid.GetTile(x, y);
            return type != TileType.Wall && type != TileType.Window;
        }

        // 창문 칸: 어두운 유리 + 벽 방향으로 이어지는 가운데 2px 반사선
        static void DrawGlass(GridMap grid, Color32[] pixels, int width, int x, int y, int left, int bottom, int size, Color32 glass, Color32 glint)
        {
            bool horizontal = !IsOpenSide(grid, x - 1, y) || !IsOpenSide(grid, x + 1, y);
            int mid = size / 2;
            for (int j = 1; j < size - 1; j++)
            {
                for (int i = 1; i < size - 1; i++)
                {
                    int along = horizontal ? j : i;
                    bool line = along == mid || along == mid - 1;
                    pixels[(bottom + j) * width + left + i] = line ? glint : glass;
                }
            }
        }

        // 시작 픽셀에서 (stepX, stepY) 방향으로 length픽셀 선을 긋는다
        static void DrawLine(Color32[] pixels, int width, int x, int y, int stepX, int stepY, int length, Color32 color)
        {
            for (int i = 0; i < length; i++)
                pixels[(y + stepY * i) * width + x + stepX * i] = color;
        }

        // 벽 변을 따라 바깥쪽(outX, outY)으로 몇 픽셀 반투명 검정 그늘을 깐다. 이미 더 진한 그늘이 있으면 덮지 않는다
        static void Shade(Color32[] pixels, int width, int x, int y, int outX, int outY, int length)
        {
            int height = pixels.Length / width;
            for (int d = 0; d < WallShade.Length; d++)
            {
                for (int i = 0; i < length; i++)
                {
                    int px = x + outX * d + (outX == 0 ? i : 0);
                    int py = y + outY * d + (outY == 0 ? i : 0);
                    if (px < 0 || py < 0 || px >= width || py >= height) continue;
                    int index = py * width + px;
                    if (pixels[index].a >= WallShade[d]) continue;
                    pixels[index] = new Color32(0, 0, 0, WallShade[d]);
                }
            }
        }
    }
}
