// 역할: 무전 지연·개인별 지식·재구독 및 재전송 중복을 검증한다.
using NUnit.Framework;
using UnityEngine;

namespace NullTrace.AI.Tests
{
    public sealed class AiRadioTests
    {
        private GameObject root;
        private RadioChannel channel;
        private EnemyAgent sender, receiver;
        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Radio fixture"); channel = root.AddComponent<RadioChannel>();
            sender = AddAgent("Sender"); receiver = AddAgent("Receiver");
            channel.Register(sender); channel.Register(receiver);
        }
        private EnemyAgent AddAgent(string name)
        {
            var go = new GameObject(name); go.transform.SetParent(root.transform);
            var agent = go.AddComponent<EnemyAgent>(); agent.Radio = channel; agent.ResetAgent(); return agent;
        }
        [TearDown] public void TearDown() => Object.DestroyImmediate(root);
        private static ClueReport Report(double time, long id = 77)
            => new ClueReport(id, ClueKind.Sound, NoiseKind.Gunshot, Vector2.right, time, time, time + 15, 0, 1, 0, 1, 1);
        [Test]
        public void ReceiverKnowsNothingUntilDeliveryAndRetransmissionCannotAddScore()
        {
            double start = sender.GameTime;
            Assert.That(channel.Send(sender, Report(start), start), Is.True);
            Assert.That(receiver.Memory.Count, Is.Zero);
            channel.Deliver(start + 0.49); Assert.That(receiver.Memory.Count, Is.Zero);
            channel.Deliver(start + 0.5); Assert.That(receiver.Memory.Count, Is.EqualTo(1));
            Assert.That(receiver.Memory.Suspicion, Is.EqualTo(50)); Assert.That(receiver.IsTargetIdentified, Is.False);
            Assert.That(channel.Send(sender, Report(start), start + 1), Is.True);
            channel.Deliver(start + 1.5);
            Assert.That(receiver.Memory.Suspicion, Is.EqualTo(50)); Assert.That(receiver.RadioReceivedCount, Is.EqualTo(1));
            Assert.That(sender.Memory.Count, Is.Zero); // 수신은 자동 재전송하지 않는다.
        }
        [Test]
        public void ResetDiscardsQueuedDeliveryAndNewListenerDoesNotReadOldQueue()
        {
            double start = sender.GameTime; channel.Send(sender, Report(start), start);
            receiver.ResetAgent();
            var newcomer = AddAgent("Late listener");
            channel.Deliver(start + 1);
            Assert.That(receiver.Memory.Count, Is.Zero); Assert.That(newcomer.Memory.Count, Is.Zero);
        }
        [Test]
        public void ExpiredPacketNeverRestartsOriginalLifetime()
        {
            double start = sender.GameTime; channel.DeliveryDelay = 20;
            channel.Send(sender, Report(start), start); channel.Deliver(start + 20);
            Assert.That(receiver.Memory.Count, Is.Zero);
        }
        [Test]
        public void SendRateAndInactiveReceiverDoNotLeakKnowledge()
        {
            double start = sender.GameTime;
            Assert.That(channel.Send(sender, Report(start), start), Is.True);
            Assert.That(channel.Send(sender, Report(start, 78), start + 0.1), Is.False);
            receiver.enabled = false; channel.Deliver(start + 1);
            Assert.That(receiver.Memory.Count, Is.Zero);
        }
        [Test]
        public void WholeChannelQueueHasHardLimitAndDropsExcessNewRecipients()
        {
            var second = AddAgent("Second"); var third = AddAgent("Third"); var fourth = AddAgent("Fourth");
            channel.MaxPendingDeliveries = 3;
            double start = sender.GameTime; channel.Send(sender, Report(start), start);
            Assert.That(channel.PendingCount, Is.EqualTo(3)); Assert.That(channel.DroppedCount, Is.EqualTo(1));
            channel.Deliver(start + 0.5);
            Assert.That(receiver.Memory.Count + second.Memory.Count + third.Memory.Count + fourth.Memory.Count, Is.EqualTo(3));
        }
        [Test]
        public void InFlightSameEventKeepsOneSnapshotAndOriginalDelay()
        {
            double start = sender.GameTime; channel.DeliveryDelay = 1; channel.SendInterval = 0.05f;
            var first = new ClueReport(77, ClueKind.Sighting, NoiseKind.Footstep, Vector2.right,
                start, start, start + 8, 3, 1, 1, 1, 1, true);
            var latest = new ClueReport(77, ClueKind.Sighting, NoiseKind.Footstep, Vector2.up,
                start, start + 0.9, start + 8.9, 3, 1, 2, 1, 1, true);
            channel.Send(sender, first, start); channel.Send(sender, latest, start + 0.9);
            Assert.That(channel.PendingCount, Is.EqualTo(1)); Assert.That(channel.CoalescedCount, Is.EqualTo(1));
            channel.Deliver(start + 1);
            receiver.Memory.TryGet(77, out var clue);
            Assert.That(clue.Report.Position, Is.EqualTo(Vector2.right)); Assert.That(clue.Report.Revision, Is.EqualTo(1));
            channel.Send(sender, latest, start + 1.1); channel.Deliver(start + 2.1);
            Assert.That(clue.Report.Position, Is.EqualTo(Vector2.up)); Assert.That(clue.Report.Revision, Is.EqualTo(2));
        }
        [Test]
        public void RepeatedNpcDeliverCallsDoNotRescanUnchangedQueue()
        {
            double start = sender.GameTime; channel.Send(sender, Report(start), start);
            for (int i = 0; i < 40; i++) channel.Deliver(start + 0.1);
            Assert.That(channel.DeliveryScanCount, Is.Zero);
            for (int i = 0; i < 40; i++) channel.Deliver(start + 0.5);
            Assert.That(channel.DeliveryScanCount, Is.EqualTo(1));
            channel.Send(sender, Report(start, 78), start + 1);
            for (int i = 0; i < 40; i++) channel.Deliver(start + 1);
            Assert.That(channel.DeliveryScanCount, Is.EqualTo(1));
            channel.Deliver(start + 1.5); Assert.That(channel.DeliveryScanCount, Is.EqualTo(2));
        }
        [Test]
        public void LoweringLimitTrimsTailAndZeroDelayStillDeliversNewInformation()
        {
            AddAgent("Second"); AddAgent("Third");
            double start = sender.GameTime; channel.Send(sender, Report(start), start);
            channel.MaxPendingDeliveries = 1; channel.Deliver(start);
            Assert.That(channel.PendingCount, Is.EqualTo(1)); Assert.That(channel.DroppedCount, Is.EqualTo(2));
            channel.Deliver(start + 1);
            channel.DeliveryDelay = 0; channel.Send(sender, Report(start, 78), start + 1);
            channel.Deliver(start + 1); Assert.That(channel.PendingCount, Is.Zero);
        }
    }
}
