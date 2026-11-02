using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace GameAi
{
    // 맵 JSON (MASTER_PROMPT 9.3). JsonUtility로 읽으려고 필드 이름을 키와 맞춘다
    [Serializable]
    public class MapData
    {
        public int version = 2;
        public string id = "";
        public string name = "";
        public string mission = "eliminate";
        public float target_time_s = 300f;
        public MapAmbient ambient = new MapAmbient();
        public string[] rows = new string[0];
        [OptionalKey] public string[] floor_rows = new string[0];
        public MapLight[] lights = new MapLight[0];
        public MapEnemy[] enemies = new MapEnemy[0];
    }

    [Serializable]
    public class MapAmbient
    {
        public float indoor = 0.15f;
        public float outdoor = 0.08f;
    }

    [Serializable]
    public class MapLight
    {
        public float[] pos = { 0f, 0f };
        public float radius_m = 5f;
        public float intensity = 0.9f;

        public Vector2 Position { get { return new Vector2(pos[0], pos[1]); } }
    }

    [Serializable]
    public class MapEnemy
    {
        public int spawn;
        public string type = "guard";
        public MapPoint[] patrol = new MapPoint[0];
    }

    [Serializable]
    public class MapPoint
    {
        public float[] v = { 0f, 0f };

        public Vector2 Position { get { return new Vector2(v[0], v[1]); } }
    }

    public static class MapJson
    {
        // JsonUtility는 중첩 배열을 읽지 못하므로 "patrol": [[x,y],...]를 [{"v":[x,y]},...]로 바꾼다
        static readonly Regex PatrolBlock = new Regex("\"patrol\"\\s*:\\s*\\[((?:\\s*\\[[^\\]]*\\]\\s*,?)*)\\s*\\]");
        static readonly Regex Pair = new Regex("\\[([^\\]]*)\\]");

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

    public struct EnemySpawn
    {
        public int Number;
        public Vector2 Position;
    }

    public struct CoverObject
    {
        public Vector2Int Cell;
        public bool IsHigh;
    }

    public sealed class ParsedMap
    {
        public GridMap Grid;
        public Vector2 PlayerStart;
        public bool HasPlayerStart;
        public readonly List<EnemySpawn> Spawns = new List<EnemySpawn>();
        public readonly List<Vector2Int> Doors = new List<Vector2Int>();
        public readonly List<CoverObject> Covers = new List<CoverObject>();
        public readonly List<Vector2Int> Foliage = new List<Vector2Int>();
        public readonly List<Vector2> Targets = new List<Vector2>();
        public readonly List<Vector2> Exits = new List<Vector2>();

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

    // 문자 격자를 GridMap으로 바꾼다. rows[0]이 맨 위 줄이다
    public static class MapParser
    {
        public static ParsedMap Parse(MapData data, List<string> errors)
        {
            if (data.rows == null || data.rows.Length == 0)
            {
                errors.Add("맵 rows가 비어 있습니다: " + data.id);
                return null;
            }

            int height = data.rows.Length;
            int width = data.rows[0].Length;
            var map = new ParsedMap { Grid = new GridMap(width, height) };
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

                int y = height - 1 - row;
                for (int x = 0; x < width; x++)
                {
                    ParseCell(map, line[x], x, y, errors);
                    char floor = hasFloors && row < data.floor_rows.Length && x < data.floor_rows[row].Length ? data.floor_rows[row][x] : 'w';
                    map.Grid.SetFloor(x, y, ParseFloor(floor, errors));
                }
            }

            if (!map.HasPlayerStart) errors.Add("플레이어 시작 P가 없습니다: " + data.id);
            return map;
        }

        static void ParseCell(ParsedMap map, char c, int x, int y, List<string> errors)
        {
            GridMap grid = map.Grid;
            Vector2 center = GridMap.CellCenter(x, y);
            switch (c)
            {
                case '#': grid.SetTile(x, y, TileType.Wall); break;
                case '.': break;
                case 'D':
                    grid.SetTile(x, y, TileType.Door);
                    map.Doors.Add(new Vector2Int(x, y));
                    break;
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
