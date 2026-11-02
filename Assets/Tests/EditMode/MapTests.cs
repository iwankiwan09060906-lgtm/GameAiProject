using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace GameAi.Tests
{
    public class MapTests
    {
        static ParsedMap LoadMap01(out MapData data)
        {
            data = MapJson.Parse(ConfigLoader.ReadText("maps/map_01.json"), "map_01");
            var errors = new List<string>();
            ParsedMap map = MapParser.Parse(data, errors);
            CollectionAssert.IsEmpty(errors);
            return map;
        }

        [Test]
        public void 줄_0이_맨_위고_문자가_칸으로_바뀐다()
        {
            var data = new MapData
            {
                id = "t",
                rows = new[]
                {
                    "#D#",
                    "PWc",
                    "1Cf",
                },
                floor_rows = new[] { "ooo", "wtk", "www" },
            };
            var errors = new List<string>();
            ParsedMap map = MapParser.Parse(data, errors);
            CollectionAssert.IsEmpty(errors);

            GridMap grid = map.Grid;
            Assert.AreEqual(TileType.Wall, grid.GetTile(0, 2));
            Assert.AreEqual(TileType.Door, grid.GetTile(1, 2));
            Assert.AreEqual(TileType.Window, grid.GetTile(1, 1));
            Assert.AreEqual(TileType.CoverLow, grid.GetTile(2, 1));
            Assert.AreEqual(TileType.CoverHigh, grid.GetTile(1, 0));
            Assert.AreEqual(TileType.Foliage, grid.GetTile(2, 0));
            Assert.AreEqual(FloorType.Outdoor, grid.GetFloor(0, 2));
            Assert.AreEqual(FloorType.Carpet, grid.GetFloor(2, 1));
            Assert.AreEqual(new Vector2(0.5f, 1.5f), map.PlayerStart);
            Assert.IsTrue(map.TryGetSpawn(1, out Vector2 spawn));
            Assert.AreEqual(new Vector2(0.5f, 0.5f), spawn);
            Assert.AreEqual(1, map.Doors.Count);
            Assert.AreEqual(2, map.Covers.Count);
        }

        [Test]
        public void floor_rows를_생략하면_나무_마루()
        {
            var data = new MapData { id = "t", rows = new[] { "P." } };
            ParsedMap map = MapParser.Parse(data, new List<string>());
            Assert.AreEqual(FloorType.Wood, map.Grid.GetFloor(1, 0));
        }

        [Test]
        public void 잘못된_맵은_오류를_낸다()
        {
            var errors = new List<string>();
            MapParser.Parse(new MapData { id = "t", rows = new[] { "..", "?" } }, errors);
            Assert.GreaterOrEqual(errors.Count, 2);

            errors.Clear();
            Assert.IsNull(MapParser.Parse(new MapData { id = "t" }, errors));
            Assert.AreEqual(1, errors.Count);
        }

        [Test]
        public void 순찰_중첩_배열을_읽는다()
        {
            const string Json = "{\"id\":\"t\",\"rows\":[\"P\"],\"enemies\":[{\"spawn\":1,\"type\":\"guard\",\"patrol\":[[1.5,2.5],[3.5,4.5]]},{\"spawn\":2,\"type\":\"rifleman\",\"patrol\": [ [5,6] ]}]}";
            MapData data = JsonUtility.FromJson<MapData>(MapJson.Normalize(Json));
            Assert.AreEqual(2, data.enemies.Length);
            Assert.AreEqual(new Vector2(3.5f, 4.5f), data.enemies[0].patrol[1].Position);
            Assert.AreEqual(new Vector2(5f, 6f), data.enemies[1].patrol[0].Position);
        }

        [Test]
        public void 맵1은_규격을_지킨다()
        {
            ParsedMap map = LoadMap01(out MapData data);
            Assert.AreEqual(40, map.Grid.Width);
            Assert.AreEqual(30, map.Grid.Height);
            Assert.AreEqual(4, data.enemies.Length);
            Assert.AreEqual(4, map.Spawns.Count);
            Assert.AreEqual(data.rows.Length, data.floor_rows.Length);
            foreach (string row in data.floor_rows)
                Assert.AreEqual(40, row.Length);

            int riflemen = 0;
            foreach (MapEnemy enemy in data.enemies)
            {
                if (enemy.type == "rifleman") riflemen++;
                Assert.IsTrue(map.TryGetSpawn(enemy.spawn, out _));
                Assert.GreaterOrEqual(enemy.patrol.Length, 2);
                Assert.LessOrEqual(enemy.patrol.Length, 5);
                foreach (MapPoint point in enemy.patrol)
                {
                    Vector2Int cell = map.Grid.WorldToCell(point.Position);
                    Assert.IsFalse(map.Grid.BlocksMove(cell.x, cell.y), "순찰 지점이 막힌 칸입니다: " + cell);
                }
            }
            Assert.AreEqual(1, riflemen);
        }

        [Test]
        public void 맵1의_조명은_실내에_있다()
        {
            ParsedMap map = LoadMap01(out MapData data);
            foreach (MapLight light in data.lights)
            {
                Vector2Int cell = map.Grid.WorldToCell(light.Position);
                Assert.AreNotEqual(TileType.Wall, map.Grid.GetTile(cell.x, cell.y));
                Assert.AreNotEqual(FloorType.Outdoor, map.Grid.GetFloor(cell.x, cell.y));
            }
        }

        [Test]
        public void 맵1_시작점에서_적이_보이지_않는다()
        {
            ParsedMap map = LoadMap01(out MapData data);
            foreach (EnemySpawn spawn in map.Spawns)
            {
                bool close = Vector2.Distance(map.PlayerStart, spawn.Position) <= 12f;
                Assert.IsFalse(close && LineOfSight.IsClear(map.Grid, map.PlayerStart, spawn.Position, RayMask.Sight));
            }
        }

        [Test]
        public void 벽은_큰_사각형으로_합치고_빠뜨리지_않는다()
        {
            ParsedMap map = LoadMap01(out _);
            List<RectInt> boxes = MapLighting.MergeShadowCells(map.Grid);

            var covered = new int[map.Grid.Width * map.Grid.Height];
            foreach (RectInt box in boxes)
                for (int y = box.yMin; y < box.yMax; y++)
                    for (int x = box.xMin; x < box.xMax; x++)
                        covered[map.Grid.Index(x, y)]++;

            int solid = 0;
            for (int y = 0; y < map.Grid.Height; y++)
            {
                for (int x = 0; x < map.Grid.Width; x++)
                {
                    TileType type = map.Grid.GetTile(x, y);
                    bool expected = type == TileType.Wall || type == TileType.CoverHigh;
                    Assert.AreEqual(expected ? 1 : 0, covered[map.Grid.Index(x, y)]);
                    if (expected) solid++;
                }
            }
            Assert.Less(boxes.Count, solid / 3);
        }

        sealed class FakeDoor : IInteractable
        {
            public Vector2 Position { get; set; }
            public Vector2Int Cell { get; set; }
            public string PromptAction { get { return "열기"; } }
            public bool CanInteract { get; set; } = true;
            public int Used;

            public void Interact(int actorId)
            {
                Used++;
            }
        }

        [Test]
        public void 상호작용은_거리_안의_가장_가까운_대상_하나()
        {
            var grid = new GridMap(6, 3);
            var near = new FakeDoor { Position = new Vector2(2.5f, 1.5f), Cell = new Vector2Int(2, 1) };
            var far = new FakeDoor { Position = new Vector2(0.5f, 1.5f), Cell = new Vector2Int(0, 1) };
            var system = new InteractionSystem(grid, 1.2f);
            system.Add(far);
            system.Add(near);

            system.Tick(new Vector2(1.8f, 1.5f), false, 0);
            Assert.AreSame(near, system.Current);

            system.Tick(new Vector2(4.5f, 1.5f), true, 0);
            Assert.IsNull(system.Current);
            Assert.AreEqual(0, near.Used);

            system.Tick(new Vector2(1.8f, 1.5f), true, 0);
            Assert.AreEqual(1, near.Used);
            Assert.AreEqual(0, far.Used);
        }

        [Test]
        public void 벽_너머_대상은_고르지_않는다()
        {
            var grid = new GridMap(4, 3);
            grid.SetTile(2, 1, TileType.Wall);
            var target = new FakeDoor { Position = new Vector2(3.5f, 1.5f), Cell = new Vector2Int(3, 1) };
            var system = new InteractionSystem(grid, 3f);
            system.Add(target);
            system.Tick(new Vector2(1.5f, 1.5f), false, 0);
            Assert.IsNull(system.Current);
        }
    }
}
