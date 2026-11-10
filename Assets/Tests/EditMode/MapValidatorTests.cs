using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace GameAi.Tests
{
    // 9.3 맵 4종 제작 조건
    public class MapValidatorTests
    {
        static MapData Load(string id)
        {
            string path = Path.Combine(Application.streamingAssetsPath, "Config", "maps", id + ".json");
            Assert.IsTrue(File.Exists(path), id + " 파일이 없습니다");
            return MapJson.Parse(File.ReadAllText(path), id);
        }

        [TestCase("map_01", "eliminate", 4)]
        [TestCase("map_02", "steal", 6)]
        [TestCase("map_03", "steal_exit", 8)]
        [TestCase("map_04", "eliminate", 10)]
        public void 맵_제작_조건을_모두_만족한다(string id, string mission, int enemies)
        {
            MapData data = Load(id);
            MapValidator.Report report = MapValidator.Validate(data, mission);
            Assert.IsTrue(report.Ok, id + ": " + string.Join(" / ", report.Errors) + " (밝은 칸 " + (report.BrightRatio * 100f).ToString("F0") + "%)");
            Assert.AreEqual(enemies, report.Enemies, id + " 적 수");
            Assert.AreEqual(mission, data.mission);
        }

        [TestCase("map_02", 50, 40)]
        [TestCase("map_03", 60, 40)]
        [TestCase("map_04", 70, 50)]
        public void 맵_크기는_규격과_같다(string id, int width, int height)
        {
            MapData data = Load(id);
            Assert.AreEqual(width, data.rows[0].Length * data.cell_m, 1e-4f);
            Assert.AreEqual(height, data.rows.Length * data.cell_m, 1e-4f);
        }

        [Test]
        public void 소총병_수()
        {
            int Count(MapData d) { int n = 0; foreach (MapEnemy e in d.enemies) if (e.type == "rifleman") n++; return n; }
            Assert.AreEqual(2, Count(Load("map_02")));
            Assert.AreEqual(3, Count(Load("map_03")));
            Assert.AreEqual(4, Count(Load("map_04")));
        }

        [Test]
        public void 막힌_목표를_찾아낸다()
        {
            var data = new MapData
            {
                id = "t",
                mission = "steal",
                rows = new[] { "#######", "#P.#.T#", "#######" },
            };
            MapValidator.Report report = MapValidator.Validate(data, "steal");
            Assert.IsFalse(report.Ok);
            StringAssert.Contains("목표", string.Join(" ", report.Errors));
        }

        [Test]
        public void 같은_스폰_번호의_두번째_칸()
        {
            var data = new MapData { id = "t", rows = new[] { "#####", "#P9.#", "#.9.#", "#####" } };
            ParsedMap map = MapParser.Parse(data, new System.Collections.Generic.List<string>());
            Assert.IsTrue(map.TryGetSpawn(9, 0, out Vector2 a));
            Assert.IsTrue(map.TryGetSpawn(9, 1, out Vector2 b));
            Assert.AreNotEqual(a, b);
            Assert.IsFalse(map.TryGetSpawn(9, 2, out _));
        }

        [Test]
        public void 대화_데이터에_맵마다_브리핑과_디브리핑이_있다()
        {
            string json = File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Config", "dialogue.json"));
            var config = ConfigLoader.Parse<DialogueConfig>(json, "dialogue.json");
            foreach (string id in new[] { "map_01", "map_02", "map_03", "map_04" })
            {
                Assert.IsNotNull(config.Find(id + "_briefing"), id);
                Assert.IsNotNull(config.Find(id + "_debrief_success"), id);
                Assert.IsNotNull(config.Find(id + "_debrief_fail"), id);
            }
        }
    }
}
