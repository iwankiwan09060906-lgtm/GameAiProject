using NUnit.Framework;
using UnityEngine;

namespace GameAi.Tests
{
    // 격자 DDA 레이캐스트와 칸 종류별 차단 규칙(9.3 표)을 확인한다.
    // 시야·총알·빛이 같은 칸에서도 다르게 막히는지, 모서리·맵 밖 같은 경계 상황이 맞는지가 핵심이다
    public class GridTests
    {
        // 가운데 줄에 시험할 칸 하나를 둔 7x3 맵
        static GridMap Corridor(TileType middle, bool doorOpen = false)
        {
            var grid = new GridMap(7, 3);
            grid.SetTile(3, 1, middle);
            grid.SetDoorOpen(3, 1, doorOpen);
            return grid;
        }

        static RayHit CastRight(GridMap grid, RayMask mask)
        {
            return Raycast.Cast(grid, new Vector2(0.5f, 1.5f), Vector2.right, 6f, mask);
        }

        [TestCase(TileType.Wall, RayMask.Sight, true)]
        [TestCase(TileType.Wall, RayMask.Bullet, true)]
        [TestCase(TileType.Wall, RayMask.Light, true)]
        [TestCase(TileType.CoverHigh, RayMask.Sight, true)]
        [TestCase(TileType.CoverHigh, RayMask.Bullet, true)]
        [TestCase(TileType.CoverHigh, RayMask.Light, true)]
        [TestCase(TileType.CoverLow, RayMask.Sight, false)]
        [TestCase(TileType.CoverLow, RayMask.Bullet, true)]
        [TestCase(TileType.CoverLow, RayMask.Light, false)]
        [TestCase(TileType.Window, RayMask.Sight, false)]
        [TestCase(TileType.Window, RayMask.Bullet, true)]
        [TestCase(TileType.Window, RayMask.Light, false)]
        [TestCase(TileType.Foliage, RayMask.Sight, false)]
        [TestCase(TileType.Foliage, RayMask.Bullet, false)]
        [TestCase(TileType.Foliage, RayMask.Light, false)]
        public void 칸_종류별_레이_차단(TileType type, RayMask mask, bool blocked)
        {
            RayHit hit = CastRight(Corridor(type), mask);
            Assert.AreEqual(blocked, hit.Hit);
            if (blocked)
            {
                Assert.AreEqual(new Vector2Int(3, 1), hit.Cell);
                Assert.AreEqual(2.5f, hit.Distance, 1e-4f);
            }
        }

        [TestCase(RayMask.Sight)]
        [TestCase(RayMask.Bullet)]
        [TestCase(RayMask.Light)]
        public void 닫힌_문은_막고_열린_문은_통과(RayMask mask)
        {
            Assert.IsTrue(CastRight(Corridor(TileType.Door, false), mask).Hit);
            Assert.IsFalse(CastRight(Corridor(TileType.Door, true), mask).Hit);
        }

        [Test]
        public void 이동_차단_규칙()
        {
            Assert.IsFalse(TileRules.BlocksMove(TileType.Floor, false));
            Assert.IsTrue(TileRules.BlocksMove(TileType.Wall, false));
            Assert.IsTrue(TileRules.BlocksMove(TileType.Window, false));
            Assert.IsTrue(TileRules.BlocksMove(TileType.CoverLow, false));
            Assert.IsTrue(TileRules.BlocksMove(TileType.CoverHigh, false));
            Assert.IsTrue(TileRules.BlocksMove(TileType.Foliage, false));
            Assert.IsTrue(TileRules.BlocksMove(TileType.Door, false));
            Assert.IsFalse(TileRules.BlocksMove(TileType.Door, true));
        }

        [Test]
        public void 맵_밖으로_나가면_막힌다()
        {
            var grid = new GridMap(3, 3);
            RayHit hit = Raycast.Cast(grid, new Vector2(1.5f, 1.5f), Vector2.up, 10f, RayMask.Bullet);
            Assert.IsTrue(hit.Hit);
            Assert.AreEqual(1.5f, hit.Distance, 1e-4f);
            Assert.IsTrue(grid.BlocksMove(-1, 0));
        }

        [Test]
        public void 사거리_안에_막힘이_없으면_빗나간다()
        {
            RayHit hit = Raycast.Cast(Corridor(TileType.Wall), new Vector2(0.5f, 1.5f), Vector2.right, 2f, RayMask.Bullet);
            Assert.IsFalse(hit.Hit);
            Assert.AreEqual(2f, hit.Distance, 1e-4f);
        }

        [Test]
        public void 모서리_양옆이_막히면_대각선으로_빠져나가지_못한다()
        {
            var grid = new GridMap(4, 4);
            grid.SetTile(2, 1, TileType.Wall);
            grid.SetTile(1, 2, TileType.Wall);
            var dir = new Vector2(1f, 1f).normalized;
            Assert.IsTrue(Raycast.Cast(grid, new Vector2(1.5f, 1.5f), dir, 3f, RayMask.Sight).Hit);
        }

        [Test]
        public void 모서리_한쪽만_막히면_지나간다()
        {
            var grid = new GridMap(4, 4);
            grid.SetTile(2, 1, TileType.Wall);
            var dir = new Vector2(1f, 1f).normalized;
            Assert.IsFalse(Raycast.Cast(grid, new Vector2(1.5f, 1.5f), dir, 2f, RayMask.Sight).Hit);
        }

        [Test]
        public void 왼쪽과_아래쪽으로도_정확히_멈춘다()
        {
            var grid = new GridMap(5, 5);
            grid.SetTile(1, 2, TileType.Wall);
            grid.SetTile(2, 1, TileType.Wall);
            Assert.AreEqual(0.5f, Raycast.Cast(grid, new Vector2(2.5f, 2.5f), Vector2.left, 5f, RayMask.Sight).Distance, 1e-4f);
            Assert.AreEqual(0.5f, Raycast.Cast(grid, new Vector2(2.5f, 2.5f), Vector2.down, 5f, RayMask.Sight).Distance, 1e-4f);
        }

        [Test]
        public void 시작_칸은_막힘으로_치지_않는다()
        {
            var grid = Corridor(TileType.Door);
            Assert.IsFalse(Raycast.Cast(grid, new Vector2(3.5f, 1.5f), Vector2.right, 3f, RayMask.Bullet).Hit);
        }

        [Test]
        public void 시야선은_낮은_엄폐물을_넘고_벽은_못_넘는다()
        {
            Vector2 a = new Vector2(0.5f, 1.5f);
            Vector2 b = new Vector2(6.5f, 1.5f);
            Assert.IsTrue(LineOfSight.IsClear(Corridor(TileType.CoverLow), a, b, RayMask.Sight));
            Assert.IsFalse(LineOfSight.IsClear(Corridor(TileType.CoverLow), a, b, RayMask.Bullet));
            Assert.IsFalse(LineOfSight.IsClear(Corridor(TileType.Wall), a, b, RayMask.Sight));
        }

        [Test]
        public void 닫힌_문_칸_자체에는_닿는다()
        {
            var grid = Corridor(TileType.Door);
            Assert.IsTrue(LineOfSight.ReachesCell(grid, new Vector2(1.5f, 1.5f), new Vector2Int(3, 1), RayMask.Sight));
            Assert.IsFalse(LineOfSight.ReachesCell(grid, new Vector2(1.5f, 1.5f), new Vector2Int(5, 1), RayMask.Sight));
        }

        [Test]
        public void 월드_좌표와_칸_변환()
        {
            var grid = new GridMap(4, 4);
            Assert.AreEqual(new Vector2Int(2, 1), grid.WorldToCell(new Vector2(2.9f, 1.1f)));
            Assert.AreEqual(new Vector2(2.5f, 1.5f), grid.CellCenter(2, 1));
        }

        [Test]
        public void 반_미터_칸은_좌표가_절반이다()
        {
            var grid = new GridMap(8, 8, 0.5f);
            Assert.AreEqual(new Vector2Int(5, 2), grid.WorldToCell(new Vector2(2.9f, 1.1f)));
            Assert.AreEqual(new Vector2(1.25f, 0.75f), grid.CellCenter(2, 1));
            Assert.AreEqual(new Vector2(1.5f, 0.75f), grid.AreaCenter(new RectInt(2, 1, 2, 1)));
        }

        [Test]
        public void 반_미터_칸에서도_레이는_m_단위로_멈춘다()
        {
            // 칸 3(1.5 ~ 2.0m)이 벽. 0.25m에서 오른쪽으로 쏘면 1.25m 뒤 벽 경계에 닿는다
            var grid = new GridMap(8, 3, 0.5f);
            grid.SetTile(3, 1, TileType.Wall);
            RayHit hit = Raycast.Cast(grid, new Vector2(0.25f, 0.75f), Vector2.right, 3f, RayMask.Sight);
            Assert.IsTrue(hit.Hit);
            Assert.AreEqual(new Vector2Int(3, 1), hit.Cell);
            Assert.AreEqual(1.25f, hit.Distance, 1e-4f);
            Assert.AreEqual(1.5f, hit.Point.x, 1e-4f);

            RayHit miss = Raycast.Cast(grid, new Vector2(0.25f, 0.25f), Vector2.right, 2f, RayMask.Sight);
            Assert.IsFalse(miss.Hit);
            Assert.AreEqual(2f, miss.Distance, 1e-4f);
        }
    }
}
