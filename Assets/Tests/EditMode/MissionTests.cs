using NUnit.Framework;

namespace GameAi.Tests
{
    // 7.9 같은 프레임 승패
    public class MissionResultTests
    {
        [Test] public void 목표_달성과_제한_시간_종료가_겹치면_성공() { Assert.AreEqual(MissionResult.Success, ResultEvaluator.Resolve(true, true, false)); }
        [Test] public void 탈출과_사망이_겹치면_실패() { Assert.AreEqual(MissionResult.Fail, ResultEvaluator.Resolve(true, false, true)); }
        [Test] public void 목표_달성과_사망이_겹치면_실패() { Assert.AreEqual(MissionResult.Fail, ResultEvaluator.Resolve(true, false, true)); }
        [Test] public void 사망만_있으면_실패() { Assert.AreEqual(MissionResult.Fail, ResultEvaluator.Resolve(false, false, true)); }
        [Test] public void 시간_종료만_있으면_실패() { Assert.AreEqual(MissionResult.Fail, ResultEvaluator.Resolve(false, true, false)); }
        [Test] public void 아무_일도_없으면_판정_없음() { Assert.AreEqual(MissionResult.None, ResultEvaluator.Resolve(false, false, false)); }
    }

    // 7.10 발각 횟수
    public class DetectionCountTests
    {
        [Test]
        public void 같은_시간대_여러_적의_발견은_1회()
        {
            var c = new DetectionCounter();
            c.OnDetection();
            c.OnDetection();
            c.Tick(false);
            c.OnDetection();
            Assert.AreEqual(1, c.Count);
        }

        [Test]
        public void 전원_순찰로_돌아간_뒤_다시_발견되면_추가()
        {
            var c = new DetectionCounter();
            c.OnDetection();
            c.Tick(true);
            c.OnDetection();
            Assert.AreEqual(2, c.Count);
        }

        [Test]
        public void 순찰_복귀_전에는_추가하지_않는다()
        {
            var c = new DetectionCounter();
            c.OnDetection();
            c.Tick(false);
            c.Tick(false);
            c.OnDetection();
            Assert.AreEqual(1, c.Count);
        }
    }

    // 7.11 기록 비교
    public class RecordsTests
    {
        static MissionRecord R(float t, int d, int k) { return new MissionRecord { clear_time_s = t, detections = d, kills = k }; }

        [Test] public void 기록이_없으면_새_기록() { Assert.IsTrue(RecordRules.IsBetter(R(100, 3, 4), null)); }
        [Test] public void 클리어_시간이_짧으면_더_좋다() { Assert.IsTrue(RecordRules.IsBetter(R(99.5f, 9, 9), R(100f, 0, 0))); }
        [Test] public void 시간이_같으면_발각이_적은_쪽() { Assert.IsTrue(RecordRules.IsBetter(R(100f, 1, 9), R(100f, 2, 0))); }
        [Test] public void 발각도_같으면_처치가_적은_쪽() { Assert.IsTrue(RecordRules.IsBetter(R(100f, 1, 2), R(100f, 1, 3))); }
        [Test] public void 모두_같으면_기존_유지() { Assert.IsFalse(RecordRules.IsBetter(R(100f, 1, 2), R(100f, 1, 2))); }
        [Test] public void 소수점_셋째_자리까지만_비교() { Assert.IsFalse(RecordRules.IsBetter(R(100.0004f, 1, 2), R(100.0001f, 1, 2))); }
        [Test] public void 반올림() { Assert.AreEqual(183.412f, RecordRules.Round3(183.41249f), 1e-4f); }
    }
}
