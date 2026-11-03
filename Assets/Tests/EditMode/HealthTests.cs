using NUnit.Framework;

namespace GameAi.Tests
{
    // 체력·방탄 계산(7.2): 방탄이 먼저 받고 남은 피해가 체력으로 넘어가며, 사망은 한 번만 확정된다
    public class HealthTests
    {
        [Test]
        public void 방탄이_먼저_줄어든다()
        {
            var health = new HealthModel(100, 50);
            int absorbed = health.Apply(20);
            Assert.AreEqual(20, absorbed);
            Assert.AreEqual(30, health.Armor);
            Assert.AreEqual(100, health.Hp);
        }

        [Test]
        public void 방탄을_넘는_피해는_체력으로_넘어간다()
        {
            var health = new HealthModel(100, 10);
            health.Apply(25);
            Assert.AreEqual(0, health.Armor);
            Assert.AreEqual(85, health.Hp);
        }

        [Test]
        public void 체력_0이면_사망하고_이후_피해는_무시한다()
        {
            var health = new HealthModel(30, 0);
            health.Apply(30);
            Assert.IsTrue(health.IsDead);
            Assert.AreEqual(0, health.Apply(10));
            Assert.AreEqual(0, health.Hp);
        }

        [Test]
        public void 초과_피해에도_체력은_음수가_되지_않는다()
        {
            var health = new HealthModel(10, 0);
            health.Apply(999);
            Assert.AreEqual(0, health.Hp);
            Assert.IsTrue(health.IsDead);
        }

        [Test]
        public void 영_이하_피해는_효과가_없다()
        {
            var health = new HealthModel(10, 5);
            health.Apply(0);
            health.Apply(-3);
            Assert.AreEqual(10, health.Hp);
            Assert.AreEqual(5, health.Armor);
        }
    }
}
