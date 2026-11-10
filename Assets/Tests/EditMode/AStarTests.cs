using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace GameAi.Tests
{
    // 7.15 A*: 최단 경로, 대각 모서리 금지, 문 비용, 통과 불가 칸, 도달 불가, 몸체 여유
    public class AStarTests
    {
        static GridMap Grid(params string[] rows)
        {
            int h = rows.Length, w = rows[0].Length;
            var grid = new GridMap(w, h);
            for (int r = 0; r < h; r++)
                for (int x = 0; x < w; x++)
                {
                    int y = h - 1 - r;
                    switch (rows[r][x])
                    {
                        case '#': grid.SetTile(x, y, TileType.Wall); break;
                        case 'D': grid.SetTile(x, y, TileType.Door); break;
                        case 'W': grid.SetTile(x, y, TileType.Window); break;
                        case 'f': grid.SetTile(x, y, TileType.Foliage); break;
                        case 'C': grid.SetTile(x, y, TileType.CoverHigh); break;
                    }
                }
            return grid;
        }

        [Test]
        public void 빈_격자의_최단_경로_비용은_옥타일_거리()
        {
            GridMap grid = new GridMap(10, 10);
            var nav = new AStarPathfinder(grid, 0.4f);
            var path = new List<Vector2Int>();
            Assert.IsTrue(nav.FindPath(new Vector2Int(0, 0), new Vector2Int(5, 3), path));
            Assert.AreEqual(new Vector2Int(0, 0), path[0]);
            Assert.AreEqual(new Vector2Int(5, 3), path[path.Count - 1]);
            Assert.AreEqual(AStarPathfinder.Heuristic(new Vector2Int(0, 0), new Vector2Int(5, 3)), nav.PathCost(path), 1e-4f);
        }

        [Test]
        public void 벽을_돌아간다()
        {
            GridMap grid = Grid(
                ".....",
                ".###.",
                ".....");
            var nav = new AStarPathfinder(grid, 0.4f);
            var path = new List<Vector2Int>();
            Assert.IsTrue(nav.FindPath(new Vector2Int(2, 0), new Vector2Int(2, 2), path));
            foreach (Vector2Int c in path) Assert.AreNotEqual(TileType.Wall, grid.GetTile(c.x, c.y));
        }

        [Test]
        public void 대각_이동은_모서리를_통과하지_않는다()
        {
            GridMap grid = Grid(
                "..",
                "#.",
                "..");
            var nav = new AStarPathfinder(grid, 0.4f);
            var path = new List<Vector2Int>();
            // (0,0) → (0,2): (0,1)이 벽이라 (1,1)로 대각 이동하면 모서리를 스친다
            Assert.IsTrue(nav.FindPath(new Vector2Int(0, 0), new Vector2Int(0, 2), path));
            for (int i = 1; i < path.Count; i++)
            {
                Vector2Int a = path[i - 1], b = path[i];
                if (a.x != b.x && a.y != b.y)
                {
                    Assert.IsFalse(grid.BlocksMove(b.x, a.y), "모서리 통과");
                    Assert.IsFalse(grid.BlocksMove(a.x, b.y), "모서리 통과");
                }
            }
            Assert.AreEqual(5, path.Count);
        }

        [Test]
        public void 닫힌_문은_지나가되_비용이_2_더_든다()
        {
            GridMap grid = Grid(
                ".#.",
                ".D.",
                ".#.");
            var nav = new AStarPathfinder(grid, 0.4f);
            var path = new List<Vector2Int>();
            Assert.IsTrue(nav.FindPath(new Vector2Int(0, 1), new Vector2Int(2, 1), path));
            Assert.Contains(new Vector2Int(1, 1), path);
            Assert.AreEqual(2f + AStarPathfinder.DoorCost, nav.PathCost(path), 1e-4f);
            grid.SetDoorOpen(1, 1, true);
            Assert.AreEqual(2f, nav.PathCost(path), 1e-4f);
        }

        [Test]
        public void 창문_풀숲_엄폐물은_통과하지_않는다()
        {
            GridMap grid = Grid(
                ".#.",
                ".W.",
                ".#.");
            var nav = new AStarPathfinder(grid, 0.4f);
            var path = new List<Vector2Int>();
            Assert.IsFalse(nav.FindPath(new Vector2Int(0, 1), new Vector2Int(2, 1), path));
            Assert.IsEmpty(path);
            grid.SetTile(1, 1, TileType.Foliage);
            nav.Rebuild();
            Assert.IsFalse(nav.FindPath(new Vector2Int(0, 1), new Vector2Int(2, 1), path));
            grid.SetTile(1, 1, TileType.CoverHigh);
            nav.Rebuild();
            Assert.IsFalse(nav.FindPath(new Vector2Int(0, 1), new Vector2Int(2, 1), path));
        }

        [Test]
        public void 도달할_수_없으면_빈_경로()
        {
            GridMap grid = Grid(
                "..#..",
                "..#..",
                "..#..");
            var nav = new AStarPathfinder(grid, 0.4f);
            var path = new List<Vector2Int> { new Vector2Int(9, 9) };
            Assert.IsFalse(nav.FindPath(new Vector2Int(0, 1), new Vector2Int(4, 1), path));
            Assert.IsEmpty(path);
        }

        [Test]
        public void 반쪽_칸_맵은_벽에서_한_칸_띄운_경로를_찾는다()
        {
            var grid = new GridMap(12, 7, 0.5f);
            for (int x = 0; x < 12; x++) { grid.SetTile(x, 0, TileType.Wall); grid.SetTile(x, 6, TileType.Wall); }
            var nav = new AStarPathfinder(grid, 0.4f);
            Assert.AreEqual(1, nav.Clearance);
            var path = new List<Vector2Int>();
            Assert.IsTrue(nav.FindPath(new Vector2Int(1, 3), new Vector2Int(10, 3), path));
            foreach (Vector2Int c in path) Assert.IsTrue(c.y >= 2 && c.y <= 4, "벽 바로 옆 칸을 지나감: " + c);
        }

        [Test]
        public void 일미터_칸은_여유를_두지_않는다()
        {
            var nav = new AStarPathfinder(new GridMap(4, 4, 1f), 0.4f);
            Assert.AreEqual(0, nav.Clearance);
        }

        [Test]
        public void 같은_입력이면_같은_경로()
        {
            GridMap grid = Grid(
                "........",
                "..##....",
                "....##..",
                "........");
            var nav = new AStarPathfinder(grid, 0.4f);
            var a = new List<Vector2Int>();
            var b = new List<Vector2Int>();
            nav.FindPath(new Vector2Int(0, 0), new Vector2Int(7, 3), a);
            nav.FindPath(new Vector2Int(0, 0), new Vector2Int(7, 3), b);
            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void 수색_지점은_반경_안_도달_가능한_곳에서_가까운_순서()
        {
            GridMap grid = Grid(
                "..........",
                "..........",
                "####.#####",
                "..........");
            var nav = new AStarPathfinder(grid, 0.4f);
            var center = new Vector2(5f, 3.5f);
            List<Vector2> points = SearchPointPicker.Pick(nav, grid, center, center, 3f, 3, new RandomSource(7));
            Assert.AreEqual(3, points.Count);
            for (int i = 0; i < points.Count; i++)
            {
                Assert.LessOrEqual(Vector2.Distance(points[i], center), 3f + 1e-4f);
                Assert.IsFalse(grid.BlocksMove(grid.WorldToCell(points[i]).x, grid.WorldToCell(points[i]).y));
                if (i > 0) Assert.LessOrEqual(Vector2.Distance(points[i - 1], center), Vector2.Distance(points[i], center) + 1e-4f);
            }
        }
    }
}
