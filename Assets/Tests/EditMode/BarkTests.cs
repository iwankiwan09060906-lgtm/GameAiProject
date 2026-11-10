using System.Collections.Generic;
using NUnit.Framework;

namespace GameAi.Tests
{
    // 기본 대사 고르기·우선순위 식(7.18, 7.22)
    public class BarkTests
    {
        [Test]
        public void 이벤트_대사가_없으면_상태_계기로_대신한다()
        {
            var config = new BarkConfig();
            string line = BarkFallback.Pick(config, "SND_CAN", "주의", "rookie", null, new RandomSource(1), out string emotion);
            CollectionAssert.Contains(new[] { "뭐지, 방금 그 소리?", "누구 있어?" }, line);
            Assert.AreEqual("의심", emotion);
            line = BarkFallback.Pick(config, "SIG_INTRUDER", "높은 위협", "rookie", null, new RandomSource(1), out _);
            CollectionAssert.Contains(new[] { "저기 있다!", "침입자다!", "거기 서!" }, line);
        }

        [Test]
        public void 긴장도와_성격이_맞는_후보를_먼저_쓴다()
        {
            var config = new BarkConfig
            {
                barks = new[]
                {
                    new BarkEntry { when = "SND_DOOR", lines = new[] { "공통" } },
                    new BarkEntry { when = "SND_DOOR", lines = new[] { "신참용" }, persona = "rookie" },
                    new BarkEntry { when = "SND_DOOR", lines = new[] { "고참 수색" }, persona = "veteran", tension = "수색" },
                },
            };
            Assert.AreEqual("신참용", BarkFallback.Pick(config, "SND_DOOR", "평상", "rookie", null, new RandomSource(3), out _));
            Assert.AreEqual("고참 수색", BarkFallback.Pick(config, "SND_DOOR", "수색", "veteran", null, new RandomSource(3), out _));
            Assert.AreEqual("공통", BarkFallback.Pick(config, "SND_DOOR", "평상", "veteran", null, new RandomSource(3), out _));
        }

        [Test]
        public void 최근_대사와_다른_것을_고른다()
        {
            var config = new BarkConfig();
            var recent = new List<string> { "저기 있다!", "침입자다!" };
            for (int seed = 1; seed < 20; seed++)
                Assert.AreEqual("거기 서!", BarkFallback.Pick(config, "detect", "평상", "veteran", recent, new RandomSource(seed), out _));
        }

        [Test]
        public void 후보가_없으면_null()
        {
            var config = new BarkConfig { barks = new BarkEntry[0] };
            Assert.IsNull(BarkFallback.Pick(config, "SND_DOOR", "평상", "veteran", null, new RandomSource(1), out _));
        }

        [Test]
        public void 우선순위_식()
        {
            Assert.AreEqual(50f + 15f + 5f, BarkPicker.Score(5, Tension.HighThreat, true, 0));
            Assert.AreEqual(10f - 10f, BarkPicker.Score(1, Tension.Calm, false, 2));
        }

        [Test]
        public void 기본_대사는_AI_난수원을_쓰지_않는다()
        {
            Rng.Init(true, 42);
            uint before = new RandomSource(42).NextUInt();
            BarkFallback.Pick(new BarkConfig(), "detect", "평상", "veteran", null, Rng.Fx, out _);
            Assert.AreEqual(before, Rng.Ai.NextUInt());
        }
    }
}
