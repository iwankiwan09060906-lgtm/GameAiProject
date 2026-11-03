using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace GameAi
{
    // 맵 JSON의 모양(9.3). JsonUtility로 읽으려고 필드 이름을 키와 똑같이 맞췄다
    [Serializable]
    public class MapData
    {
        public int version = 2;
        public string id = "";
        public string name = "";
        public string mission = "eliminate";
        public float target_time_s = 300f;
        public MapAmbient ambient = new MapAmbient();

        // 한 칸의 크기(m). 생략하면 1m. 0.5면 벽 두께가 0.5m가 되고 격자는 가로·세로 2배가 된다
        [OptionalKey] public float cell_m = 1f;

        // 문자 격자. rows[0]이 맨 위(y가 가장 큰) 줄이다
        public string[] rows = new string[0];

        // 바닥 종류 격자. rows와 같은 크기이고, 생략하면 전부 나무 마루
        [OptionalKey] public string[] floor_rows = new string[0];

        // 바닥 종류(FloorType 순서: 나무·타일·카펫·야외)마다 쓸 그림 이름. 비우면 tile_floor_* 기본 그림
        [OptionalKey] public string[] floor_sprites = new string[0];
        public MapLight[] lights = new MapLight[0];
        public MapEnemy[] enemies = new MapEnemy[0];

        // 그림만 있는 장식 소품. 이동·시야 판정은 rows의 c·C 칸이 맡고, 이 목록은 화면에만 쓴다
        [OptionalKey] public MapProp[] props = new MapProp[0];
    }

    // 장식 소품 하나
    [Serializable]
    public class MapProp
    {
        // AssetCatalog 그림 이름(deco_*)
        public string sprite = "";

        // [x, y] 그림 중심의 월드 좌표(m)
        public float[] pos = { 0f, 0f };

        // 반시계 회전 각도(도)
        public float rot;

        // 0이면 바닥에 붙은 것(케이블·얼룩, 혈흔 아래), 1이면 세워진 물건(혈흔 위, 캐릭터 아래), 2면 물건 위에 얹은 것(책상 위 서류)
        public int layer = 1;

        public Vector2 Position { get { return new Vector2(pos[0], pos[1]); } }
    }

    // 전역광 밝기. 실내와 야외가 다르다
    [Serializable]
    public class MapAmbient
    {
        public float indoor = 0.15f;
        public float outdoor = 0.08f;
    }

    // 천장등 하나
    [Serializable]
    public class MapLight
    {
        // [x, y] 월드 좌표(m)
        public float[] pos = { 0f, 0f };
        public float radius_m = 5f;
        public float intensity = 0.9f;

        // "#RRGGBB". 비우면 game.json의 lamp_color
        public string color = "";

        // 모니터 불빛처럼 작은 빛은 그림자를 끄고 위치 표시 점도 그리지 않는다
        public bool no_shadows;

        public Vector2 Position { get { return new Vector2(pos[0], pos[1]); } }
    }

    // 맵에 놓을 적 하나. spawn은 격자의 숫자 칸 번호와 짝이다
    [Serializable]
    public class MapEnemy
    {
        public int spawn;
        public string type = "guard";
        public MapPoint[] patrol = new MapPoint[0];
    }

    // 순찰 지점 하나. JsonUtility가 [[x,y]] 같은 중첩 배열을 못 읽어서 {"v":[x,y]}로 한 번 감싼다
    [Serializable]
    public class MapPoint
    {
        public float[] v = { 0f, 0f };

        public Vector2 Position { get { return new Vector2(v[0], v[1]); } }
    }

    // 맵 JSON 글자를 MapData로 바꾼다
    public static class MapJson
    {
        // "patrol": [[x,y],[x,y]] 덩어리와, 그 안의 [x,y] 한 쌍을 찾는 정규식
        static readonly Regex PatrolBlock = new Regex("\"patrol\"\\s*:\\s*\\[((?:\\s*\\[[^\\]]*\\]\\s*,?)*)\\s*\\]");
        static readonly Regex Pair = new Regex("\\[([^\\]]*)\\]");

        // 파일 형식은 문서 그대로 두고, 읽기 직전에만 [[x,y]]를 [{"v":[x,y]}]로 바꾼다
        public static string Normalize(string json)
        {
            return PatrolBlock.Replace(json, m =>
                "\"patrol\": [" + Pair.Replace(m.Groups[1].Value, p => "{\"v\":[" + p.Groups[1].Value + "]}") + "]");
        }

        public static MapData Parse(string json, string label)
        {
            return ConfigLoader.Parse<MapData>(Normalize(json), label);
        }
    }

    // 적이 시작할 칸
    public struct EnemySpawn
    {
        public int Number;
        public Vector2 Position;
    }

    // 엄폐물 한 칸. IsHigh면 높은 엄폐물(C)
    public struct CoverObject
    {
        public Vector2Int Cell;
        public bool IsHigh;
    }

    // 문자 격자를 해석한 결과: 격자 데이터와, 격자 밖에서 따로 쓰는 위치 목록
    public sealed class ParsedMap
    {
        public GridMap Grid;
        public Vector2 PlayerStart;
        public bool HasPlayerStart;
        public readonly List<EnemySpawn> Spawns = new List<EnemySpawn>();

        // 문 하나가 차지하는 칸들. 가로나 세로로 이어진 D 칸이 문 하나다
        public readonly List<RectInt> Doors = new List<RectInt>();
        public readonly List<CoverObject> Covers = new List<CoverObject>();
        public readonly List<Vector2Int> Foliage = new List<Vector2Int>();
        public readonly List<Vector2> Targets = new List<Vector2>();
        public readonly List<Vector2> Exits = new List<Vector2>();

        // 스폰 번호의 위치를 찾는다. 없으면 false
        public bool TryGetSpawn(int number, out Vector2 position)
        {
            foreach (var s in Spawns)
            {
                if (s.Number != number) continue;
                position = s.Position;
                return true;
            }
            position = Vector2.zero;
            return false;
        }
    }

    // 문자 격자를 GridMap으로 바꾼다. rows[0]이 맨 위 줄이라서 y를 뒤집어 읽는다
    public static class MapParser
    {
        // 오류는 errors에 모아서 돌려주고, 읽을 수 있는 만큼은 계속 읽는다. rows가 아예 없으면 null
        public static ParsedMap Parse(MapData data, List<string> errors)
        {
            if (data.rows == null || data.rows.Length == 0)
            {
                errors.Add("맵 rows가 비어 있습니다: " + data.id);
                return null;
            }

            int height = data.rows.Length;
            int width = data.rows[0].Length;
            if (data.cell_m <= 0f)
            {
                errors.Add("cell_m은 0보다 커야 합니다: " + data.id);
                data.cell_m = 1f;
            }
            var map = new ParsedMap { Grid = new GridMap(width, height, data.cell_m) };
            bool hasFloors = data.floor_rows != null && data.floor_rows.Length > 0;
            if (hasFloors && data.floor_rows.Length != height)
                errors.Add("floor_rows 줄 수가 rows와 다릅니다: " + data.id);

            for (int row = 0; row < height; row++)
            {
                string line = data.rows[row];
                if (line.Length != width)
                {
                    errors.Add("rows 폭이 다릅니다: " + row + "행 " + line.Length + "칸");
                    continue;
                }

                // 첫 줄(row 0)이 맨 위이므로 y = 높이 - 1 - row
                int y = height - 1 - row;
                for (int x = 0; x < width; x++)
                {
                    ParseCell(map, line[x], x, y, errors);
                    char floor = hasFloors && row < data.floor_rows.Length && x < data.floor_rows[row].Length ? data.floor_rows[row][x] : 'w';
                    map.Grid.SetFloor(x, y, ParseFloor(floor, errors));
                }
            }

            if (!map.HasPlayerStart) errors.Add("플레이어 시작 P가 없습니다: " + data.id);
            GroupDoors(map);
            return map;
        }

        // 이어진 D 칸을 문 하나로 묶는다. 왼쪽 아래부터 훑어 오른쪽으로 이어지면 가로 문, 아니면 위로 이어진 세로 문
        static void GroupDoors(ParsedMap map)
        {
            GridMap grid = map.Grid;
            var used = new bool[grid.Width * grid.Height];
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    if (used[grid.Index(x, y)] || grid.GetTile(x, y) != TileType.Door) continue;
                    int width = 1;
                    int height = 1;
                    while (IsFreeDoor(grid, used, x + width, y)) width++;
                    if (width == 1)
                        while (IsFreeDoor(grid, used, x, y + height)) height++;

                    for (int j = 0; j < height; j++)
                        for (int i = 0; i < width; i++)
                            used[grid.Index(x + i, y + j)] = true;
                    map.Doors.Add(new RectInt(x, y, width, height));
                }
            }
        }

        static bool IsFreeDoor(GridMap grid, bool[] used, int x, int y)
        {
            return grid.InBounds(x, y) && grid.GetTile(x, y) == TileType.Door && !used[grid.Index(x, y)];
        }

        // 문자 한 칸을 해석한다(9.3 표)
        static void ParseCell(ParsedMap map, char c, int x, int y, List<string> errors)
        {
            GridMap grid = map.Grid;
            Vector2 center = grid.CellCenter(x, y);
            switch (c)
            {
                case '#': grid.SetTile(x, y, TileType.Wall); break;
                case '.': break;
                case 'D': grid.SetTile(x, y, TileType.Door); break;
                case 'W': grid.SetTile(x, y, TileType.Window); break;
                case 'c':
                    grid.SetTile(x, y, TileType.CoverLow);
                    map.Covers.Add(new CoverObject { Cell = new Vector2Int(x, y), IsHigh = false });
                    break;
                case 'C':
                    grid.SetTile(x, y, TileType.CoverHigh);
                    map.Covers.Add(new CoverObject { Cell = new Vector2Int(x, y), IsHigh = true });
                    break;
                case 'f':
                    grid.SetTile(x, y, TileType.Foliage);
                    map.Foliage.Add(new Vector2Int(x, y));
                    break;

                // P, T, X, 숫자는 바닥 위에 놓이는 표시일 뿐이라 칸 종류는 바닥 그대로 둔다
                case 'P':
                    map.PlayerStart = center;
                    map.HasPlayerStart = true;
                    break;
                case 'T': map.Targets.Add(center); break;
                case 'X': map.Exits.Add(center); break;
                default:
                    if (c >= '1' && c <= '9')
                        map.Spawns.Add(new EnemySpawn { Number = c - '0', Position = center });
                    else
                        errors.Add("알 수 없는 맵 문자 '" + c + "': (" + x + ", " + y + ")");
                    break;
            }
        }

        // 바닥 문자 한 칸을 해석한다
        static FloorType ParseFloor(char c, List<string> errors)
        {
            switch (c)
            {
                case 'w': return FloorType.Wood;
                case 't': return FloorType.Tile;
                case 'k': return FloorType.Carpet;
                case 'o': return FloorType.Outdoor;
                default:
                    errors.Add("알 수 없는 바닥 문자 '" + c + "'");
                    return FloorType.Wood;
            }
        }
    }
}
