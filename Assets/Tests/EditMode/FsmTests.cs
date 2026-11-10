using NUnit.Framework;

namespace GameAi.Tests
{
    // 7.7 FSM 전환 표 모든 줄과 우선순위, 교전 행동 트리
    public class FsmTests
    {
        static BrainInput In(float gauge = 0f, bool visible = false, bool inRange = false, bool newClue = false,
            bool arrived = false, bool searchDone = false, bool dead = false)
        {
            return new BrainInput
            {
                Gauge = gauge, DetectAt = 100f, SuspectAt = 30f, PlayerVisible = visible, InRange = inRange,
                NewClue = newClue, ArrivedAtClue = arrived, SearchDone = searchDone, Dead = dead,
            };
        }

        static EnemyState Next(EnemyState s, BrainInput i) { return EnemyBrain.Next(s, i, out _); }

        [Test] public void 순찰_게이지30이면_조사() { Assert.AreEqual(EnemyState.Investigate, Next(EnemyState.Patrol, In(gauge: 30f))); }
        [Test] public void 순찰_새_단서면_조사() { Assert.AreEqual(EnemyState.Investigate, Next(EnemyState.Patrol, In(newClue: true))); }
        [Test] public void 순찰_아무것도_없으면_유지() { Assert.AreEqual(EnemyState.Patrol, Next(EnemyState.Patrol, In(gauge: 29f))); }
        [Test] public void 조사_발견_사거리_안이면_교전() { Assert.AreEqual(EnemyState.Engage, Next(EnemyState.Investigate, In(100f, true, true))); }
        [Test] public void 조사_발견_사거리_밖이면_추격() { Assert.AreEqual(EnemyState.Chase, Next(EnemyState.Investigate, In(100f, true, false))); }
        [Test] public void 조사_단서_도착_플레이어_없으면_수색() { Assert.AreEqual(EnemyState.Search, Next(EnemyState.Investigate, In(arrived: true))); }
        [Test] public void 조사_도착했어도_보이면_유지() { Assert.AreEqual(EnemyState.Investigate, Next(EnemyState.Investigate, In(50f, visible: true, arrived: true))); }
        [Test] public void 추격_사거리_안_시야_확보면_교전() { Assert.AreEqual(EnemyState.Engage, Next(EnemyState.Chase, In(100f, true, true))); }
        [Test] public void 추격_사거리_안이어도_안_보이면_유지() { Assert.AreEqual(EnemyState.Chase, Next(EnemyState.Chase, In(100f, false, true))); }
        [Test] public void 추격_놓쳐_게이지_100미만이면_수색() { Assert.AreEqual(EnemyState.Search, Next(EnemyState.Chase, In(99f))); }
        [Test] public void 교전_놓쳐_게이지_100미만이면_수색() { Assert.AreEqual(EnemyState.Search, Next(EnemyState.Engage, In(99f))); }
        [Test] public void 수색_새_단서면_조사() { Assert.AreEqual(EnemyState.Investigate, Next(EnemyState.Search, In(newClue: true))); }
        [Test] public void 수색_다시_보면_추격() { Assert.AreEqual(EnemyState.Chase, Next(EnemyState.Search, In(100f, true, false))); }
        [Test] public void 수색_다시_보면_사거리_안은_교전() { Assert.AreEqual(EnemyState.Engage, Next(EnemyState.Search, In(100f, true, true))); }
        [Test] public void 수색_10초_끝나면_순찰() { Assert.AreEqual(EnemyState.Patrol, Next(EnemyState.Search, In(searchDone: true))); }

        [TestCase(EnemyState.Patrol)]
        [TestCase(EnemyState.Investigate)]
        [TestCase(EnemyState.Chase)]
        [TestCase(EnemyState.Engage)]
        [TestCase(EnemyState.Search)]
        public void 모든_상태_체력0이면_사망(EnemyState s)
        {
            Assert.AreEqual(EnemyState.Dead, Next(s, In(100f, true, true, true, true, true, dead: true)));
        }

        [Test]
        public void 사망은_되돌아오지_않는다()
        {
            Assert.AreEqual(EnemyState.Dead, Next(EnemyState.Dead, In(100f, true, true)));
        }

        [Test]
        public void 우선순위_눈앞의_플레이어가_새_단서보다_먼저()
        {
            Assert.AreEqual(EnemyState.Chase, Next(EnemyState.Search, In(100f, true, false, newClue: true, searchDone: true)));
        }

        [Test]
        public void 우선순위_새_단서가_수색_종료보다_먼저()
        {
            Assert.AreEqual(EnemyState.Investigate, Next(EnemyState.Search, In(newClue: true, searchDone: true)));
        }

        [Test]
        public void 전환_이유를_남긴다()
        {
            EnemyBrain.Next(EnemyState.Search, In(searchDone: true), out string reason);
            Assert.AreEqual("search_timeout", reason);
            EnemyBrain.Next(EnemyState.Patrol, In(gauge: 30f), out reason);
            Assert.AreEqual("sight_gauge_30", reason);
            EnemyBrain.Next(EnemyState.Patrol, In(newClue: true), out reason);
            Assert.AreEqual("new_clue", reason);
        }

        [Test]
        public void 교전_트리_순서()
        {
            Assert.AreEqual(CombatAction.TakeCover, CombatTree.Decide(0.3f, 0.4f, false, false, false, true, true, true, true));
            Assert.AreEqual(CombatAction.Reload, CombatTree.Decide(1f, 0.4f, false, true, false, true, true, false, true));
            Assert.AreEqual(CombatAction.Approach, CombatTree.Decide(1f, 0.4f, false, false, false, false, true, false, true));
            Assert.AreEqual(CombatAction.Shoot, CombatTree.Decide(1f, 0.4f, false, false, false, true, true, false, true));
            Assert.AreEqual(CombatAction.MoveToLastSeen, CombatTree.Decide(1f, 0.4f, false, false, false, true, false, false, true));
            Assert.AreEqual(CombatAction.Shoot, CombatTree.Decide(0.3f, 0.4f, false, false, false, true, true, true, false), "엄폐 이동 끔");
        }
    }
}
