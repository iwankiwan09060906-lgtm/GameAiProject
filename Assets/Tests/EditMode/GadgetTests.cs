using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace GameAi.Tests
{
    // 연막탄(7.4)·야간 투시 배터리(7.20)
    public class GadgetTests
    {
        [Test]
        public void 연막탄은_터질_때_소리_한번_10초간_시선을_가리고_사라진다()
        {
            WorldEvents.Reset(new EventTypeTable(new EventTypeConfig()));
            var grid = new GridMap(30, 9);
            var smoke = new SmokeGrenadeSystem(grid, 2, null, null, null);
            int sounds = 0;
            System.Action<WorldEvent> h = e => { if (e.code == "SND_DECOY") sounds++; };
            EventBus.Subscribe(h);
            Vector2 enemy = new Vector2(10.5f, 8f), player = new Vector2(10.5f, 1f);
            try
            {
                Assert.IsTrue(smoke.Throw(new Vector2(2.5f, 4.5f), new Vector2(10.5f, 4.5f)));
                Assert.IsFalse(smoke.BlocksSight(enemy, player), "날아가는 중에는 안 가림");
                for (int i = 0; i < 40; i++) smoke.Tick(0.05f);
                Assert.IsTrue(smoke.BlocksSight(enemy, player), "퍼진 연기가 시선을 가림");
                Assert.IsFalse(smoke.BlocksSight(new Vector2(20.5f, 8f), new Vector2(20.5f, 1f)), "연기 밖 시선");
                for (int i = 0; i < 220; i++) smoke.Tick(0.05f);
            }
            finally
            {
                EventBus.Unsubscribe(h);
            }
            Assert.AreEqual(1, sounds);
            Assert.AreEqual(1, smoke.Count);
            Assert.AreEqual(0, smoke.ActiveClouds);
            Assert.IsFalse(smoke.BlocksSight(enemy, player));
        }

        [Test]
        public void 연막탄은_최대_10m_벽_앞까지()
        {
            var grid = new GridMap(30, 5);
            var smoke = new SmokeGrenadeSystem(grid, 2, null, null, null);
            smoke.Throw(new Vector2(0.5f, 2.5f), new Vector2(25.5f, 2.5f));
            Assert.AreEqual(10.5f, smoke.LastLanding.x, 1e-3f);
            grid.SetTile(5, 2, TileType.Wall);
            smoke.Throw(new Vector2(0.5f, 2.5f), new Vector2(9.5f, 2.5f));
            Assert.Less(smoke.LastLanding.x, 5f);
            Assert.AreEqual(0, smoke.Count);
            Assert.IsFalse(smoke.Throw(new Vector2(0.5f, 2.5f), new Vector2(5f, 2.5f)), "다 쓰면 못 던짐");
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
