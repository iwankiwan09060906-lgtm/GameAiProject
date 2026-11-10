using NUnit.Framework;
using UnityEngine;

namespace GameAi.Tests
{
    // 인지 게이지(7.7)·의심도·긴장도(7.21)
    public class AwarenessTests
    {
        readonly GaugeConfig gauge = new GaugeConfig();
        readonly AiLightConfig light = new AiLightConfig();
        readonly SuspicionConfig suspicion = new SuspicionConfig();

        [Test]
        public void 게이지는_보이면_초당_40_오르고_100에서_멈춘다()
        {
            var g = new AwarenessGauge();
            g.Tick(1f, true, 1f, gauge);
            Assert.AreEqual(40f, g.Value, 1e-4f);
            g.Tick(5f, true, 1f, gauge);
            Assert.AreEqual(100f, g.Value);
        }

        [Test]
        public void 놓치면_1초_유지_후_초당_15_내린다()
        {
            var g = new AwarenessGauge();
            g.Set(60f);
            g.Tick(0.9f, false, 1f, gauge);
            Assert.AreEqual(60f, g.Value);
            g.Tick(0.2f, false, 1f, gauge);
            Assert.AreEqual(57f, g.Value, 1e-3f);
        }

        [Test]
        public void 배율_근접_달리기_웅크리기_밝기()
        {
            Assert.AreEqual(2f, AwarenessGauge.RiseMultiplier(2f, MoveMode.Walk, 0f, gauge, light), 1e-4f, "3m 이내는 ×2, 밝기 무시");
            Assert.AreEqual(1.5f, AwarenessGauge.RiseMultiplier(5f, MoveMode.Run, 1f, gauge, light), 1e-4f);
            Assert.AreEqual(0.5f, AwarenessGauge.RiseMultiplier(5f, MoveMode.Crouch, 1f, gauge, light), 1e-4f);
            Assert.AreEqual(0.3f, AwarenessGauge.RiseMultiplier(5f, MoveMode.Walk, 0f, gauge, light), 1e-4f, "어두우면 ×0.3");
            Assert.AreEqual(0.65f, AwarenessGauge.RiseMultiplier(5f, MoveMode.Walk, 0.5f, gauge, light), 1e-4f);
        }

        [Test]
        public void 시야_거리는_밝기에_따라_절반까지_준다()
        {
            Assert.AreEqual(10f, AwarenessGauge.EffectiveView(10f, 1f, light), 1e-4f);
            Assert.AreEqual(5f, AwarenessGauge.EffectiveView(10f, 0f, light), 1e-4f);
        }

        [Test]
        public void 어두운_곳은_멀리서_안_보이고_3m_안은_항상_보인다()
        {
            var grid = new GridMap(20, 5);
            Vector2 eye = new Vector2(0.5f, 2.5f);
            Assert.IsTrue(SightSensor.CanSee(grid, eye, Vector2.right, 90f, 10f, eye + Vector2.right * 8f, 1f, 3f, light));
            Assert.IsFalse(SightSensor.CanSee(grid, eye, Vector2.right, 90f, 10f, eye + Vector2.right * 8f, 0f, 3f, light));
            Assert.IsTrue(SightSensor.CanSee(grid, eye, Vector2.right, 90f, 10f, eye + Vector2.right * 2.5f, 0f, 3f, light));
            Assert.IsFalse(SightSensor.CanSee(grid, eye, Vector2.right, 90f, 10f, eye + Vector2.up * 2f + Vector2.left * 0.1f, 1f, 1f, light), "부채꼴 밖");
            grid.SetTile(4, 2, TileType.Wall);
            Assert.IsFalse(SightSensor.CanSee(grid, eye, Vector2.right, 90f, 10f, eye + Vector2.right * 8f, 1f, 3f, light), "벽 뒤");
        }

        [Test]
        public void 의심도는_기여의_합이고_같은_사건은_한번만()
        {
            var m = new SuspicionMeter();
            Assert.IsTrue(m.Add(1, "SND_DOOR", Vector2.zero, 25f, 0f, 15f, true, false));
            Assert.IsFalse(m.Add(1, "SND_DOOR", Vector2.zero, 20f, 0.1f, 15f, false, false), "무전으로 같은 사건");
            Assert.AreEqual(25f, m.Value, 1e-4f);
            Assert.IsTrue(m.Add(2, "SND_FOOTSTEP", Vector2.zero, 10f, 0.2f, 15f, true, false));
            Assert.AreEqual(35f, m.Value, 1e-4f);
        }

        [Test]
        public void 무전이_먼저_와도_직접이_크면_큰_쪽_하나만_남는다()
        {
            var m = new SuspicionMeter();
            m.Add(5, "SND_GUNSHOT", Vector2.zero, 48f, 0f, 15f, false, false);
            m.Add(5, "SND_GUNSHOT", Vector2.zero, 60f, 0f, 15f, true, false);
            Assert.AreEqual(60f, m.Value, 1e-4f);
            Assert.AreEqual(1, m.Records.Count);
        }

        [Test]
        public void 조용하면_3초_뒤부터_초당_6씩_줄고_수명이_끝나면_그_기여만_빠진다()
        {
            var m = new SuspicionMeter();
            m.Add(1, "SND_DOOR", Vector2.zero, 30f, 0f, 15f, true, false);
            m.Add(2, "CLU_CORPSE", Vector2.zero, 30f, 0f, -1f, true, false);
            m.Tick(2f, 1f, suspicion);
            Assert.AreEqual(60f, m.Value, 1e-4f);
            m.Tick(4f, 1f, suspicion);
            Assert.AreEqual(54f, m.Value, 1e-3f);
            m.Tick(15f, 0f, suspicion);
            Assert.AreEqual(1, m.Records.Count, "소리 수명 15초 만료");
            Assert.AreEqual("CLU_CORPSE", m.Records[0].Code);
        }

        [Test]
        public void 발견_중에는_90_이상을_유지한다()
        {
            var m = new SuspicionMeter();
            m.Add(1, "SND_FOOTSTEP", Vector2.zero, 10f, 0f, 15f, true, false);
            m.SetDetected(true, 90f);
            Assert.AreEqual(90f, m.Value);
            m.SetDetected(false, 90f);
            Assert.AreEqual(10f, m.Value, 1e-4f);
        }

        [Test]
        public void 조사할_단서는_최근_목격이_먼저_그다음_큰_기여()
        {
            var m = new SuspicionMeter();
            m.Add(1, "SND_GUNSHOT", new Vector2(9, 0), 60f, 0f, 15f, true, false);
            m.Add(2, "SND_DOOR", new Vector2(1, 0), 25f, 0f, 15f, true, false);
            Assert.AreEqual(1, m.BestClue(Vector2.zero).EventId);
            m.Add(3, "SIG_GLIMPSE", new Vector2(5, 0), 5f, 1f, 8f, true, true);
            Assert.AreEqual(3, m.BestClue(Vector2.zero).EventId);
            m.MarkHandled(3);
            Assert.AreEqual(1, m.BestClue(Vector2.zero).EventId);
        }

        [Test]
        public void 기록이_넘치면_처리된_것부터_버린다()
        {
            var m = new SuspicionMeter();
            for (int i = 0; i < SuspicionMeter.MaxRecords; i++) m.Add(i + 1, "SND_FOOTSTEP", Vector2.zero, 1f + i, 0f, 15f, true, false);
            m.MarkHandled(10);
            m.Add(100, "SND_DOOR", Vector2.zero, 0.5f, 0f, 15f, true, false);
            Assert.AreEqual(SuspicionMeter.MaxRecords, m.Records.Count);
            Assert.IsNull(m.Find(10));
            Assert.IsNotNull(m.Find(100));
        }

        [TestCase(0f, Tension.Calm)]
        [TestCase(9.9f, Tension.Calm)]
        [TestCase(10f, Tension.Caution)]
        [TestCase(30f, Tension.Searching)]
        [TestCase(50f, Tension.ActiveSearch)]
        [TestCase(70f, Tension.CombatReady)]
        [TestCase(90f, Tension.HighThreat)]
        public void 긴장도_6구간(float value, Tension expected)
        {
            Assert.AreEqual(expected, SuspicionMeter.TensionOf(value, suspicion.tension_bands));
        }

        [Test]
        public void 움직임_포착은_보는_동안_쌓인다()
        {
            var m = new SuspicionMeter();
            m.Add(7, "SIG_GLIMPSE", Vector2.zero, 2f, 0f, 8f, true, true);
            m.Accumulate(7, 5f, 100f, 0.5f, Vector2.one);
            Assert.AreEqual(7f, m.Value, 1e-4f);
            Assert.AreEqual(Vector2.one, m.Find(7).Position);
        }
    }
}
