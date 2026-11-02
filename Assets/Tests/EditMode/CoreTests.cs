using System.Collections.Generic;
using NUnit.Framework;

namespace GameAi.Tests
{
    public class CoreTests
    {
        struct Ping
        {
            public int Value;
        }

        [Test]
        public void 구독자에게_전달되고_해제하면_받지_않는다()
        {
            var received = new List<int>();
            System.Action<Ping> handler = e => received.Add(e.Value);

            EventBus.Subscribe(handler);
            EventBus.Publish(new Ping { Value = 1 });
            EventBus.Unsubscribe(handler);
            EventBus.Publish(new Ping { Value = 2 });

            CollectionAssert.AreEqual(new[] { 1 }, received);
            Assert.AreEqual(0, EventBus.SubscriberCount<Ping>());
        }

        [Test]
        public void 발행_중에_해제해도_안전하다()
        {
            int calls = 0;
            System.Action<Ping> second = e => calls++;
            System.Action<Ping> first = null;
            first = e =>
            {
                calls++;
                EventBus.Unsubscribe(first);
                EventBus.Unsubscribe(second);
            };

            EventBus.Subscribe(first);
            EventBus.Subscribe(second);
            EventBus.Publish(new Ping());
            EventBus.Publish(new Ping());

            Assert.AreEqual(1, calls);
            Assert.AreEqual(0, EventBus.SubscriberCount<Ping>());
        }

        [Test]
        public void 같은_시드면_같은_수열이다()
        {
            var a = new RandomSource(12345);
            var b = new RandomSource(12345);
            for (int i = 0; i < 100; i++)
                Assert.AreEqual(a.NextUInt(), b.NextUInt());
        }

        [Test]
        public void 범위를_지킨다()
        {
            var rng = new RandomSource(7);
            for (int i = 0; i < 1000; i++)
            {
                float value = rng.Value();
                Assert.GreaterOrEqual(value, 0f);
                Assert.Less(value, 1f);
                int number = rng.Int(2, 5);
                Assert.GreaterOrEqual(number, 2);
                Assert.Less(number, 5);
            }
        }

        [Test]
        public void 연출_난수를_써도_AI_난수는_같다()
        {
            Rng.Init(true, 777);
            uint first = Rng.Ai.NextUInt();

            Rng.Init(true, 777);
            for (int i = 0; i < 50; i++) Rng.Fx.NextUInt();
            Assert.AreEqual(first, Rng.Ai.NextUInt());
        }
    }
}
