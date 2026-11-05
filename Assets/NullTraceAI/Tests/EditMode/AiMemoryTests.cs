// 역할: 단서 중복·신선도·감쇠와 Utility/행동 트리의 계약을 작은 입력으로 검증한다.
// 장면 이동은 AiAgentTests, 생성기 timeout은 DialogueTests가 맡는다.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace NullTrace.AI.Tests
{
    public sealed class AiMemoryTests
    {
        private static ClueReport Sound(long id = 1, float confidence = 1, double expires = 15)
            => new ClueReport(id, ClueKind.Sound, NoiseKind.Gunshot, Vector2.right, 0, 0, expires, 0, 9, 0, 1, confidence);
        private static ClueReport Sight(int revision, double observed, Vector2 position, float confidence = 1, bool identified = true)
            => new ClueReport(10, ClueKind.Sighting, NoiseKind.Footstep, position, 0, observed, observed + 8, 3, 9, revision, 1, confidence, identified);

        [Test]
        public void DuplicateDoesNotAddSuspicionOrRefreshQuietTime()
        {
            var memory = new EnemyMemory();
            Assert.That(memory.Receive(Sound(), 0), Is.True);
            Assert.That(memory.Receive(Sound(), 4), Is.False);
            memory.Advance(4, 4);
            Assert.That(memory.Suspicion, Is.EqualTo(44).Within(0.001));
            Assert.That(memory.LastInformationAt, Is.Zero);
            Assert.That(memory.Count, Is.EqualTo(1));
        }
        [Test]
        public void IndirectConfidenceChangeIsIgnoredWithoutReopeningProcessedClue()
        {
            var memory = new EnemyMemory();
            memory.Receive(Sound(confidence: 0.4f), 0);
            memory.Advance(4, 4); // 20 중 14점만 남는다.
            memory.MarkProcessed(1);
            memory.Receive(Sound(confidence: 0.8f), 4);
            Assert.That(memory.Suspicion, Is.EqualTo(14).Within(0.001));
            Assert.That(memory.TryGet(1, out var clue) && clue.Processed, Is.True);
            Assert.That(memory.InformationVersion, Is.EqualTo(1));
        }
        [Test]
        public void OldHighConfidenceCannotOverwriteNewerObservation()
        {
            var memory = new EnemyMemory();
            memory.Receive(Sight(2, 2, Vector2.up, 0.5f), 2);
            memory.Receive(Sight(1, 1, Vector2.down, 1), 3);
            memory.TryGet(10, out var clue);
            Assert.That(clue.Report.Position, Is.EqualTo(Vector2.up));
            Assert.That(clue.Report.Confidence, Is.EqualTo(0.5f));
            Assert.That(clue.Contribution, Is.EqualTo(45));
        }
        [Test]
        public void DirectSightReplacesIndirectContributionAndSurvivesLostSight()
        {
            var memory = new EnemyMemory();
            memory.Receive(Sight(1, 1, Vector2.right, 0.5f), 1);
            memory.Receive(Sight(2, 2, Vector2.right), 2, true, 0.1f);
            Assert.That(memory.Suspicion, Is.EqualTo(90));
            memory.Advance(6, 4);
            Assert.That(memory.Suspicion, Is.EqualTo(84).Within(0.001));
            memory.Advance(10, 4);
            Assert.That(memory.Count, Is.Zero);
            Assert.That(memory.Suspicion, Is.Zero);
        }
        [Test]
        public void PreIdentificationVisualContributionIsAtMostTwentyPerSecond()
        {
            var memory = new EnemyMemory();
            memory.Receive(Sight(1, 0.25, Vector2.right, identified: false), 0.25, true, 0.25f);
            memory.Receive(Sight(2, 0.5, Vector2.right, identified: false), 0.5, true, 0.25f);
            Assert.That(memory.Suspicion, Is.EqualTo(10));
            Assert.That(memory.Count, Is.EqualTo(1));
        }
        [Test]
        public void ContributionsDecayProportionallyAndExpiryRemovesOnlyItsEvent()
        {
            var memory = new EnemyMemory();
            memory.Receive(Sound(1, 1, 5), 0);
            memory.Receive(Sound(2, 0.5f), 0);
            memory.Advance(4, 4);
            memory.TryGet(1, out var first); memory.TryGet(2, out var second);
            Assert.That(first.Contribution / second.Contribution, Is.EqualTo(2).Within(0.001));
            Assert.That(memory.Suspicion, Is.EqualTo(69).Within(0.001));
            memory.Advance(5, 0);
            Assert.That(memory.Count, Is.EqualTo(1));
            Assert.That(memory.Suspicion, Is.EqualTo(23).Within(0.001));
        }
        [Test]
        public void ExpiredFutureAndMalformedReportsAreRejected()
        {
            var memory = new EnemyMemory();
            Assert.That(memory.Receive(Sound(), 15), Is.False);
            Assert.That(memory.Receive(Sight(1, 5, Vector2.zero), 1), Is.False);
            Assert.That(memory.Receive(Sound(confidence: float.NaN), 0), Is.False);
            Assert.That(memory.Receive(Sight(1, 0, new Vector2(float.PositiveInfinity, 0)), 0), Is.False);
            Assert.That(memory.Count, Is.Zero);
        }
        [Test]
        public void SameRevisionCannotMoveObservationOrExtendExpiry()
        {
            var memory = new EnemyMemory();
            memory.Receive(Sight(2, 2, Vector2.up, 0.5f), 2);
            memory.Receive(Sight(2, 3, Vector2.down, 1), 3);
            memory.TryGet(10, out var clue);
            Assert.That(clue.Report.ObservedAt, Is.EqualTo(2));
            Assert.That(clue.Report.ExpiresAt, Is.EqualTo(10));
            Assert.That(clue.Report.Position, Is.EqualTo(Vector2.up));
        }
        [Test]
        public void CompletionSeparatesInspectFromSearchAndPreservesUnrelatedEvents()
        {
            var memory = new EnemyMemory(); memory.Receive(Sound(1), 0); memory.Receive(Sound(2), 0);
            memory.MarkInspected(1); memory.TryGet(1, out var first);
            Assert.That(first.Inspected, Is.True); Assert.That(first.Processed, Is.False);
            memory.MarkProcessed(1); memory.TryGet(2, out var second);
            Assert.That(first.Processed, Is.True); Assert.That(second.Processed, Is.False);
        }
        [Test]
        public void InvestigationReportStoresScopeWithoutClearingLaterThreatOrResettingDecay()
        {
            var memory = new EnemyMemory(); memory.Receive(Sound(), 0);
            var clear = new ClueReport(2, ClueKind.Investigation, NoiseKind.Footstep, Vector2.zero,
                2, 2, 17, 9, 9, 0, 0, 1, false, 2);
            memory.Receive(clear, 2);
            memory.TryGet(1, out var sound); memory.TryGet(2, out var result);
            Assert.That(sound.Processed, Is.False); Assert.That(result.Processed, Is.True);
            Assert.That(result.Report.InvestigationRadius, Is.EqualTo(2)); Assert.That(result.Contribution, Is.Zero);
            Assert.That(memory.InformationVersion, Is.EqualTo(1)); Assert.That(memory.LastInformationAt, Is.Zero);
        }
        private static ClueReport RecentSound(long id, double at)
            => new ClueReport(id, ClueKind.Sound, NoiseKind.Footstep, Vector2.right, at, at, at + 15, 0, 9, 0, 1, 1);
        [Test]
        public void FloodIsBoundedAndCurrentDirectSightSurvivesWeakSounds()
        {
            var memory = new EnemyMemory(); memory.Receive(Sight(1, 0, Vector2.up), 0, true, 0.1f);
            for (int i = 1; i <= 100; i++) memory.Receive(RecentSound(100 + i, i * 0.01), i * 0.01);
            Assert.That(memory.Count, Is.EqualTo(EnemyMemory.Capacity));
            Assert.That(memory.RetiredCount, Is.EqualTo(EnemyMemory.RetiredCapacity));
            Assert.That(memory.TryGet(10, out var sight) && sight.Direct, Is.True);
            Assert.That(memory.TryGet(200, out _), Is.True);
        }
        [Test]
        public void ProcessedRecordIsEvictedBeforeOlderUnprocessedClue()
        {
            var memory = new EnemyMemory();
            for (int i = 1; i <= 8; i++) memory.Receive(RecentSound(i, i * 0.1), i * 0.1);
            memory.MarkProcessed(8);
            memory.Receive(RecentSound(9, 1), 1);
            Assert.That(memory.TryGet(8, out _), Is.False); Assert.That(memory.TryGet(1, out _), Is.True);
            memory.Receive(RecentSound(10, 1.1), 1.1);
            Assert.That(memory.TryGet(1, out _), Is.False); Assert.That(memory.Count, Is.EqualTo(8));
        }
        [Test]
        public void EvictedDuplicateCannotRestoreScoreOrReopenInvestigation()
        {
            var memory = new EnemyMemory();
            var first = RecentSound(1, 0);
            memory.Receive(first, 0); memory.MarkProcessed(1);
            for (int i = 2; i <= 9; i++) memory.Receive(RecentSound(i, i * 0.1), i * 0.1);
            int version = memory.InformationVersion;
            Assert.That(memory.Receive(first, 1, true), Is.False);
            Assert.That(memory.TryGet(1, out _), Is.False); Assert.That(memory.InformationVersion, Is.EqualTo(version));
        }
        [Test]
        public void ForgottenVeryOldPacketCannotEvictRecentUnprocessedClue()
        {
            var memory = new EnemyMemory();
            for (int i = 1; i <= 50; i++) memory.Receive(RecentSound(i, i * 0.01), i * 0.01);
            Assert.That(memory.Receive(RecentSound(1, 0.01), 1), Is.False);
            Assert.That(memory.Count, Is.EqualTo(8)); Assert.That(memory.TryGet(50, out _), Is.True);
        }
        [Test]
        public void SameObservationDirectConfirmationReplacesIndirectWithoutAddingOrReopening()
        {
            var memory = new EnemyMemory(); memory.Receive(Sight(1, 1, Vector2.right, 0.4f), 1);
            memory.MarkProcessed(10);
            Assert.That(memory.Receive(Sight(1, 1, Vector2.right), 1, true, 0.1f), Is.True);
            Assert.That(memory.Suspicion, Is.EqualTo(90)); Assert.That(memory.Count, Is.EqualTo(1));
            memory.TryGet(10, out var clue); Assert.That(clue.Processed, Is.True); Assert.That(clue.Direct, Is.True);
            Assert.That(memory.InformationVersion, Is.EqualTo(1));
            Assert.That(memory.Receive(Sight(1, 1, Vector2.right), 2), Is.False);
        }
        [Test]
        public void CurrentDirectSightRejectsIndirectReplacementButFreshSightAfterRemovalCanReturn()
        {
            var memory = new EnemyMemory(); memory.Receive(Sight(1, 1, Vector2.right), 1, true, 0.1f);
            Assert.That(memory.Receive(Sight(2, 2, Vector2.left), 2), Is.False);
            memory.Remove(10);
            Assert.That(memory.Receive(Sight(1, 1, Vector2.right), 2), Is.False);
            Assert.That(memory.Receive(Sight(3, 3, Vector2.up), 3, true, 0.1f), Is.True);
            memory.TryGet(10, out var fresh); Assert.That(fresh.Report.Position, Is.EqualTo(Vector2.up));
        }
        [Test]
        public void ReceivePrunesExpiredCluesBeforeApplyingCapacity()
        {
            var memory = new EnemyMemory();
            for (int i = 1; i <= 8; i++) memory.Receive(RecentSound(i, 0), 0);
            memory.Receive(RecentSound(9, 16), 16);
            Assert.That(memory.Count, Is.EqualTo(1)); Assert.That(memory.TryGet(9, out _), Is.True);
        }
        [Test]
        public void UtilityUsesHoldAdvantageAndDeterministicTieButNeverKeepsInvalidGoal()
        {
            var current = new EnemyCandidate(EnemyGoal.Inspect, 7, 30);
            var choices = new List<EnemyCandidate> { current, new EnemyCandidate(EnemyGoal.Search, 2, 45) };
            Assert.That(EnemyUtility.Choose(choices, current, true, 0.2).Goal, Is.EqualTo(EnemyGoal.Inspect));
            Assert.That(EnemyUtility.Choose(choices, current, true, 0.6).Goal, Is.EqualTo(EnemyGoal.Search));
            choices.RemoveAt(0);
            Assert.That(EnemyUtility.Choose(choices, current, true, 0).Goal, Is.EqualTo(EnemyGoal.Search));
            choices.Add(new EnemyCandidate(EnemyGoal.Search, 1, 45));
            Assert.That(EnemyUtility.Choose(choices, current, false, 0).ClueId, Is.EqualTo(1));
            Assert.That(EnemyUtility.Choose(choices, choices[0], true, 10).ClueId, Is.EqualTo(2));
        }
        [Test]
        public void BehaviorTreeRemembersRunningStepAndResetCancelsItsProcedure()
        {
            int plan = 0, move = 0, finish = 0;
            var sequence = new BehaviorSequence(new BehaviorAction(dt => { plan++; return BehaviorStatus.Success; }),
                new BehaviorAction(dt => ++move < 2 ? BehaviorStatus.Running : BehaviorStatus.Success),
                new BehaviorAction(dt => { finish++; return BehaviorStatus.Success; }));
            Assert.That(sequence.Tick(0.1f), Is.EqualTo(BehaviorStatus.Running));
            Assert.That(sequence.Tick(0.1f), Is.EqualTo(BehaviorStatus.Success));
            Assert.That(plan, Is.EqualTo(1)); Assert.That(finish, Is.EqualTo(1));
            sequence.Reset(); sequence.Tick(0.1f);
            Assert.That(plan, Is.EqualTo(2));
        }
    }
}
