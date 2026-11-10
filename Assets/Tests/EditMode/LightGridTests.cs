using NUnit.Framework;
using UnityEngine;

namespace GameAi.Tests
{
    // 7.17 밝기 격자
    public class LightGridTests
    {
        [Test]
        public void 전역광만_있으면_실내_0점15_야외_0점08()
        {
            var grid = new GridMap(4, 2);
            for (int x = 0; x < 4; x++) grid.SetFloor(x, 1, FloorType.Outdoor);
            var light = new LightGrid(grid, 0.15f, 0.08f);
            light.RecomputeAll();
            Assert.AreEqual(0.15f, light.At(0, 0), 1e-4f);
            Assert.AreEqual(0.08f, light.At(0, 1), 1e-4f);
        }

        [Test]
        public void 조명은_거리에_따라_선형으로_줄고_1에서_자른다()
        {
            var grid = new GridMap(12, 1);
            var light = new LightGrid(grid, 0.1f, 0.1f);
            light.AddLight(new Vector2(0.5f, 0.5f), 10f, 1f);
            light.RecomputeAll();
            Assert.AreEqual(1f, light.At(0, 0), 1e-4f);
            Assert.AreEqual(0.1f + 1f * (1f - 5f / 10f), light.At(5, 0), 1e-4f);
            Assert.AreEqual(0.1f, light.At(11, 0), 1e-4f, "반경 밖");
        }

        [Test]
        public void 벽과_닫힌_문_뒤는_조명이_더해지지_않는다()
        {
            var grid = new GridMap(8, 1);
            grid.SetTile(3, 0, TileType.Door);
            var light = new LightGrid(grid, 0.15f, 0.15f);
            light.AddLight(new Vector2(0.5f, 0.5f), 7f, 0.9f);
            light.RecomputeAll();
            Assert.AreEqual(0.15f, light.At(5, 0), 1e-4f);
            grid.SetTile(3, 0, TileType.Window);
            light.RecomputeAll();
            Assert.Greater(light.At(5, 0), 0.15f, "창문은 빛을 통과");
        }

        [Test]
        public void 문을_열면_부분_재계산이_전체_재계산과_같다()
        {
            var grid = new GridMap(20, 12);
            for (int y = 0; y < 12; y++) grid.SetTile(10, y, TileType.Wall);
            grid.SetTile(10, 5, TileType.Door);
            grid.SetTile(10, 6, TileType.Door);
            var a = new LightGrid(grid, 0.15f, 0.08f);
            a.AddLight(new Vector2(6f, 6f), 8f, 0.9f);
            a.AddLight(new Vector2(16f, 3f), 5f, 0.7f);
            a.AddLight(new Vector2(2f, 10f), 3f, 0.5f);
            a.RecomputeAll();
            grid.SetDoorOpen(10, 5, true);
            grid.SetDoorOpen(10, 6, true);
            a.OnDoorChanged(new Rect(10f, 5f, 1f, 2f));
            Assert.Less(a.LastRecomputedCells, 20 * 12, "전체가 아니라 일부만 계산");
            var b = new LightGrid(grid, 0.15f, 0.08f);
            b.AddLight(new Vector2(6f, 6f), 8f, 0.9f);
            b.AddLight(new Vector2(16f, 3f), 5f, 0.7f);
            b.AddLight(new Vector2(2f, 10f), 3f, 0.5f);
            b.RecomputeAll();
            for (int y = 0; y < 12; y++)
                for (int x = 0; x < 20; x++)
                    Assert.AreEqual(b.At(x, y), a.At(x, y), 1e-5f, "(" + x + "," + y + ")");
            Assert.Greater(a.At(12, 6), 0.15f, "문 너머가 밝아짐");
        }

        [Test]
        public void 조명을_끄면_그_범위만_어두워진다()
        {
            var grid = new GridMap(10, 1);
            var light = new LightGrid(grid, 0.15f, 0.15f);
            int id = light.AddLight(new Vector2(0.5f, 0.5f), 4f, 0.9f);
            light.RecomputeAll();
            Assert.Greater(light.At(1, 0), 0.5f);
            light.SetLight(id, false);
            Assert.AreEqual(0.15f, light.At(1, 0), 1e-4f);
            light.SetLight(id, true);
            Assert.Greater(light.At(1, 0), 0.5f);
        }

        [Test]
        public void 밝은_칸_비율()
        {
            var grid = new GridMap(10, 1);
            var light = new LightGrid(grid, 0.15f, 0.15f);
            light.AddLight(new Vector2(0.5f, 0.5f), 4f, 1f);
            light.RecomputeAll();
            // 칸 0~2: 1.15, 0.9, 0.65... ≥0.5 → 0,1,2 (3칸)
            Assert.AreEqual(0.3f, light.BrightRatio(0.5f), 1e-4f);
        }
    }
}
