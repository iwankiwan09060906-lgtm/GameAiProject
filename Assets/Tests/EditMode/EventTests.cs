using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace GameAi.Tests
{
    // events.json 22종, 맵의 깡통·평소 열린 문(7.21, 9.3, 9.11)
    public class EventTests
    {
        [Test]
        public void 기본_이벤트는_22종이고_분류별_8_8_6()
        {
            var table = new EventTypeTable(new EventTypeConfig());
            Assert.AreEqual(22, table.Count);
            int sound = 0, clue = 0, sight = 0;
            foreach (EventTypeDef def in table.All)
            {
                if (def.category == "sound") sound++;
                else if (def.category == "clue") clue++;
                else if (def.category == "sight") sight++;
            }
            Assert.AreEqual(8, sound);
            Assert.AreEqual(8, clue);
            Assert.AreEqual(6, sight);
        }

        [Test]
        public void 주요_수치가_문서와_같다()
        {
            var table = new EventTypeTable(new EventTypeConfig());
            Assert.AreEqual(60f, table.Get("SND_GUNSHOT").score);
            Assert.AreEqual(30f, table.Get("SND_GUNSHOT").score_silenced);
            Assert.AreEqual("instant", table.Get("SND_GUNSHOT").bark);
            Assert.AreEqual("qwen", table.Get("SND_GUNSHOT").bark_silenced);
            Assert.AreEqual(-1f, table.Get("CLU_CORPSE").lifetime_s);
            Assert.AreEqual(5, table.Get("CLU_CORPSE").priority);
            Assert.AreEqual(0.3f, table.Get("CLU_CASING").min_light);
            Assert.AreEqual(8f, table.Get("SND_CAN").radius_m);
            Assert.AreEqual(8f, table.Get("SIG_INTRUDER").lifetime_s);
        }

        [Test]
        public void 파일의_events_json이_22종을_모두_담는다()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "Config", "events.json");
            Assert.IsTrue(File.Exists(path), "events.json이 없습니다");
            var config = ConfigLoader.Parse<EventTypeConfig>(File.ReadAllText(path), "events.json");
            Assert.AreEqual(22, config.events.Length);
            var defaults = new EventTypeTable(new EventTypeConfig());
            foreach (EventTypeDef def in config.events)
            {
                EventTypeDef d = defaults.Get(def.code);
                Assert.IsNotNull(d, def.code);
                Assert.AreEqual(d.score, def.score, def.code);
                Assert.AreEqual(d.lifetime_s, def.lifetime_s, def.code);
                Assert.AreEqual(d.priority, def.priority, def.code);
            }
        }

        [Test]
        public void 빠진_코드는_기본값으로_채운다()
        {
            var config = new EventTypeConfig { events = new[] { new EventTypeDef { code = "SND_DOOR", score = 99 } } };
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var table = new EventTypeTable(config);
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
            Assert.AreEqual(22, table.Count);
            Assert.AreEqual(99f, table.Get("SND_DOOR").score);
        }

        [Test]
        public void 맵_문자_k는_깡통이고_바닥과_같다()
        {
            var data = new MapData { id = "t", rows = new[] { "####", "#Pk#", "####" } };
            var errors = new System.Collections.Generic.List<string>();
            ParsedMap map = MapParser.Parse(data, errors);
            Assert.IsEmpty(errors);
            Assert.AreEqual(1, map.Cans.Count);
            Assert.IsFalse(map.Grid.BlocksMove(2, 1));
        }

        [Test]
        public void 평소_열린_문과_스위치와_성격을_읽는다()
        {
            string json = "{\"id\":\"t\",\"rows\":[\"#P#\"],\"open_doors\": [[1.5, 0.5], [2.0, 1.0]]," +
                          "\"lights\":[{\"pos\":[1,1],\"radius_m\":3,\"intensity\":1,\"switch\":[2,2]}]," +
                          "\"enemies\":[{\"spawn\":1,\"type\":\"guard\",\"patrol\":[[1,1]],\"persona\":\"rookie\",\"callsign\":\"박 상병\"}]}";
            MapData data = MapJson.Parse(json, "t");
            Assert.AreEqual(2, data.open_doors.Length);
            Assert.AreEqual(new Vector2(1.5f, 0.5f), data.open_doors[0].Position);
            Assert.IsTrue(data.lights[0].HasSwitch);
            Assert.AreEqual(new Vector2(2f, 2f), data.lights[0].SwitchPosition);
            Assert.AreEqual("rookie", data.enemies[0].persona);
            Assert.AreEqual("박 상병", data.enemies[0].callsign);
            Assert.AreEqual(1, data.enemies[0].patrol.Length);
        }
    }
}
