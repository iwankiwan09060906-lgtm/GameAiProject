using System.Collections.Generic;
using UnityEngine;

namespace GameAi
{
    // 칸별 밝기(7.17). 순수 클래스.
    // L = 바닥 종류별 전역광 + Σ 세기 × (1 − d/반경) (조명→칸 DDA가 벽·닫힌 문·높은 엄폐물에 막히지 않을 때), 0~1로 자른다.
    // 문·조명이 바뀌면 그 영향 범위만 다시 계산한다
    public sealed class LightGrid
    {
        public struct Source
        {
            public Vector2 Position;
            public float Radius;
            public float Intensity;
            public bool On;
        }

        readonly GridMap grid;
        readonly float indoor;
        readonly float outdoor;
        readonly List<Source> sources = new List<Source>();
        readonly float[] values;

        public int Count { get { return sources.Count; } }
        public int LastRecomputedCells { get; private set; }

        public LightGrid(GridMap grid, float ambientIndoor, float ambientOutdoor)
        {
            this.grid = grid;
            indoor = ambientIndoor;
            outdoor = ambientOutdoor;
            values = new float[grid.Width * grid.Height];
        }

        public int AddLight(Vector2 position, float radius, float intensity)
        {
            sources.Add(new Source { Position = position, Radius = radius, Intensity = intensity, On = true });
            return sources.Count - 1;
        }

        public Source Get(int id) { return sources[id]; }

        public float At(Vector2Int cell)
        {
            if (!grid.InBounds(cell.x, cell.y)) return 0f;
            return values[grid.Index(cell.x, cell.y)];
        }

        public float At(int x, int y) { return At(new Vector2Int(x, y)); }

        public void RecomputeAll()
        {
            LastRecomputedCells = 0;
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                    Compute(x, y);
        }

        void Compute(int x, int y)
        {
            float l = grid.GetFloor(x, y) == FloorType.Outdoor ? outdoor : indoor;
            Vector2 c = grid.CellCenter(x, y);
            for (int i = 0; i < sources.Count; i++)
            {
                Source s = sources[i];
                if (!s.On) continue;
                float d = Vector2.Distance(s.Position, c);
                if (d > s.Radius) continue;
                if (!LineOfSight.IsClear(grid, s.Position, c, RayMask.Light)) continue;
                l += s.Intensity * (1f - d / s.Radius);
            }
            values[grid.Index(x, y)] = Mathf.Clamp01(l);
            LastRecomputedCells++;
        }

        // 한 원 안의 칸만 다시 계산
        void RecomputeCircle(Vector2 center, float radius)
        {
            Vector2Int min = grid.WorldToCell(center - Vector2.one * radius);
            Vector2Int max = grid.WorldToCell(center + Vector2.one * radius);
            for (int y = Mathf.Max(0, min.y); y <= Mathf.Min(grid.Height - 1, max.y); y++)
                for (int x = Mathf.Max(0, min.x); x <= Mathf.Min(grid.Width - 1, max.x); x++)
                    if (Vector2.Distance(grid.CellCenter(x, y), center) <= radius + grid.CellSize) Compute(x, y);
        }

        // 문이 바뀌면: 그 문에서 반경 안에 있는 조명이 비추는 칸만 다시 계산
        public void OnDoorChanged(Rect doorArea)
        {
            LastRecomputedCells = 0;
            foreach (Source s in sources)
            {
                Vector2 closest = new Vector2(Mathf.Clamp(s.Position.x, doorArea.xMin, doorArea.xMax), Mathf.Clamp(s.Position.y, doorArea.yMin, doorArea.yMax));
                if (Vector2.Distance(closest, s.Position) <= s.Radius) RecomputeCircle(s.Position, s.Radius);
            }
        }

        // 조명을 끄거나 켜면: 그 조명 반경 안만 다시 계산(C)
        public void SetLight(int id, bool on)
        {
            if (id < 0 || id >= sources.Count) return;
            Source s = sources[id];
            if (s.On == on) return;
            s.On = on;
            sources[id] = s;
            LastRecomputedCells = 0;
            RecomputeCircle(s.Position, s.Radius);
        }

        // 벽이 아닌 칸 중 밝기 threshold 이상 비율(맵 제작 조건 30~60%)
        public float BrightRatio(float threshold)
        {
            int total = 0, bright = 0;
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    TileType t = grid.GetTile(x, y);
                    if (t == TileType.Wall || t == TileType.Window) continue;
                    total++;
                    if (values[grid.Index(x, y)] >= threshold) bright++;
                }
            return total > 0 ? (float)bright / total : 0f;
        }
    }
}
