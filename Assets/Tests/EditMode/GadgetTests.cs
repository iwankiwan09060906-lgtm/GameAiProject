using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace GameAi.Tests
{
    // 유인탄(7.4)·야간 투시 배터리(7.20)
    public class GadgetTests
    {
        [Test]
        public void 유인탄은_2개이고_3초간_0점5초마다_금속음()
        {
            WorldEvents.Reset(new EventTypeTable(new EventTypeConfig()));
            var grid = new GridMap(30, 5);
            var decoy = new DecoySystem(grid, 2, null);
            var events = new List<WorldEvent>();
            System.Action<WorldEvent> h = e => { if (e.code == "SND_DECOY") events.Add(e); };
            EventBus.Subscribe(h);
            try
            {
                Assert.IsTrue(decoy.Throw(new Vector2(1.5f, 2.5f), new Vector2(6.5f, 2.5f)));
                Assert.IsFalse(decoy.Throw(new Vector2(1.5f, 2.5f), new Vector2(6.5f, 2.5f)), "비행·소음 중에는 못 던짐");
                for (int i = 0; i < 100; i++) decoy.Tick(0.05f);
            }
            finally
            {
                EventBus.Unsubscribe(h);
            }
            Assert.AreEqual(6, events.Count);
            Assert.AreEqual(10f, events[0].radius, 1e-4f);
            Assert.AreEqual(1, decoy.Count);
            Assert.IsFalse(decoy.Active);
        }

        [Test]
        public void 유인탄은_최대_10m_벽_앞까지()
        {
            var grid = new GridMap(30, 5);
            var decoy = new DecoySystem(grid, 2, null);
            decoy.Throw(new Vector2(0.5f, 2.5f), new Vector2(25.5f, 2.5f));
            Assert.AreEqual(10.5f, decoy.LandingPoint.x, 1e-3f);
            grid.SetTile(5, 2, TileType.Wall);
            var blocked = new DecoySystem(grid, 2, null);
            blocked.Throw(new Vector2(0.5f, 2.5f), new Vector2(9.5f, 2.5f));
            Assert.Less(blocked.LandingPoint.x, 5f);
        }

        [Test]
        public void 야간_투시는_켜면_초당_1_소모_끄면_0점5_충전_0이면_꺼짐()
        {
            var battery = new NightVisionBattery(60f, 0.5f);
            Assert.IsTrue(battery.Toggle());
            battery.Tick(10f);
            Assert.AreEqual(50f, battery.Seconds, 1e-4f);
            battery.Toggle();
            battery.Tick(10f);
            Assert.AreEqual(55f, battery.Seconds, 1e-4f);
            battery.Toggle();
            Assert.IsTrue(battery.Tick(60f), "0이면 자동으로 꺼짐");
            Assert.IsFalse(battery.On);
            Assert.AreEqual(0f, battery.Seconds);
            Assert.IsFalse(battery.Toggle(), "배터리 0이면 켤 수 없음");
        }
    }
}
