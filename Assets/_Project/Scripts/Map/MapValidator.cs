using System.Collections.Generic;
using UnityEngine;

namespace GameAi
{
    // 맵 제작 조건 검사(9.3): 도달 가능성, 목표·탈출 개수, 순찰 지점, 밝은 칸 비율 30~60%, 어두운 순찰 구간, 시작점 시야
    public static class MapValidator
    {
        public sealed class Report
        {
            public readonly List<string> Errors = new List<string>();
            public float BrightRatio;
            public int Enemies;
            public bool Ok { get { return Errors.Count == 0; } }
        }

        public static Report Validate(MapData data, string mission, float agentRadius = 0.4f, float playerView = 12f)
        {
            var report = new Report();
            var parseErrors = new List<string>();
            ParsedMap map = MapParser.Parse(data, parseErrors);
            foreach (string e in parseErrors) report.Errors.Add("형식: " + e);
            if (map == null) return report;
            GridMap grid = map.Grid;
            report.Enemies = data.enemies.Length;

            // 목표·탈출 개수
            if ((mission == "steal" || mission == "steal_exit") && map.Targets.Count != 1) report.Errors.Add("탈취 맵은 T가 정확히 1개여야 합니다: " + map.Targets.Count);
            if (mission == "steal_exit" && map.Exits.Count != 1) report.Errors.Add("탈출 맵은 X가 정확히 1개여야 합니다: " + map.Exits.Count);

            // 시작점에서 걸어서 닿는 칸(문은 열고 지나간다)
            var nav = new AStarPathfinder(grid, agentRadius);
            bool[] reach = Flood(grid, nav, grid.WorldToCell(map.PlayerStart));
            void Need(Vector2 p, string what)
            {
                Vector2Int c = grid.WorldToCell(p);
                if (!grid.InBounds(c.x, c.y) || !reach[grid.Index(c.x, c.y)]) report.Errors.Add(what + "에 시작점에서 닿을 수 없습니다: " + p);
            }
            foreach (Vector2 t in map.Targets) Need(t, "목표");
            foreach (Vector2 x in map.Exits) Need(x, "탈출 지점");

            var uses = new Dictionary<int, int>();
            foreach (MapEnemy enemy in data.enemies)
            {
                uses.TryGetValue(enemy.spawn, out int used);
                uses[enemy.spawn] = used + 1;
                if (!map.TryGetSpawn(enemy.spawn, used, out Vector2 spawn))
                {
                    report.Errors.Add("적 스폰 번호가 맵에 없습니다: " + enemy.spawn);
                    continue;
                }
                Need(spawn, "적 " + enemy.spawn + " 스폰");
                if (enemy.patrol.Length < 2 || enemy.patrol.Length > 5) report.Errors.Add("적 " + enemy.spawn + " 순찰 지점은 2~5개여야 합니다: " + enemy.patrol.Length);
                foreach (MapPoint point in enemy.patrol)
                {
                    Vector2Int c = grid.WorldToCell(point.Position);
                    if (grid.BlocksMove(c.x, c.y) && grid.GetTile(c.x, c.y) != TileType.Door) report.Errors.Add("적 " + enemy.spawn + " 순찰 지점이 막힌 칸입니다: " + point.Position);
                    Need(point.Position, "적 " + enemy.spawn + " 순찰 지점");
                }
                // 시작점 시야(12m) 안에 적이 없다
                if (Vector2.Distance(spawn, map.PlayerStart) <= playerView && LineOfSight.IsClear(grid, map.PlayerStart, spawn, RayMask.Sight))
                    report.Errors.Add("시작점 12m 시야 안에 적 " + enemy.spawn + "이 있습니다");
            }

            // 밝기
            var light = new LightGrid(grid, data.ambient.indoor, data.ambient.outdoor);
            foreach (MapLight l in data.lights) light.AddLight(l.Position, l.radius_m, l.intensity);
            light.RecomputeAll();
            report.BrightRatio = light.BrightRatio(0.5f);
            if (report.BrightRatio < 0.3f || report.BrightRatio > 0.6f)
                report.Errors.Add("밝기 0.5 이상 칸 비율이 30~60% 밖입니다: " + (report.BrightRatio * 100f).ToString("F0") + "%");

            // 순찰 경로 중 어두운 구간
            bool darkSegment = false;
            foreach (MapEnemy enemy in data.enemies)
                for (int i = 0; i < enemy.patrol.Length && !darkSegment; i++)
                {
                    Vector2 a = enemy.patrol[i].Position, b = enemy.patrol[(i + 1) % enemy.patrol.Length].Position;
                    for (float t = 0f; t <= 1f; t += 0.1f)
                        if (light.At(grid.WorldToCell(Vector2.Lerp(a, b, t))) < 0.5f) { darkSegment = true; break; }
                }
            if (data.enemies.Length > 0 && !darkSegment) report.Errors.Add("순찰 경로에 어두운 구간이 없습니다");
            return report;
        }

        static bool[] Flood(GridMap grid, AStarPathfinder nav, Vector2Int start)
        {
            var reach = new bool[grid.Width * grid.Height];
            if (!nav.IsWalkable(start.x, start.y, false)) return reach;
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            reach[grid.Index(start.x, start.y)] = true;
            while (queue.Count > 0)
            {
                Vector2Int c = queue.Dequeue();
                for (int k = 0; k < 4; k++)
                {
                    int nx = c.x + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = c.y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    if (!nav.IsWalkable(nx, ny, false)) continue;
                    int i = grid.Index(nx, ny);
                    if (reach[i]) continue;
                    reach[i] = true;
                    queue.Enqueue(new Vector2Int(nx, ny));
                }
            }
            return reach;
        }
    }
}
