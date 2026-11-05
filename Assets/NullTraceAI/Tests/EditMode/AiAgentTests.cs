// 역할: 실제 감각·물리·기억·목표·순찰 복원 계약을 함께 검증한다.
// 각 테스트는 새 월드를 만들고 수동 게임 시간을 진행한다. Unity의 Update는 동시에 사용하지 않는다.
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.TestTools;

namespace NullTrace.AI.Tests
{
    public sealed class AiAgentTests
    {
        private GameObject root;
        private EnemyAgent agent;
        private EnemyTarget target;
        private EnemyProfile profile;
        private GridWorld2D world;
        private NoiseBus noise;
        private int attacks;
        [SetUp]
        public void SetUp()
        {
            root = new GameObject("AI fixture");
            world = Child("World").AddComponent<GridWorld2D>();
            world.Origin = new Vector2(-6, -6); world.Width = world.Height = 24;
            world.CellSize = 0.5f; world.AgentRadius = 0.15f; world.ObstacleMask = 1 << 8; world.Rebuild();
            noise = Child("Noise").AddComponent<NoiseBus>();
            target = Child("Target").AddComponent<EnemyTarget>(); target.transform.position = new Vector2(0.75f, 0.25f);
            profile = ScriptableObject.CreateInstance<EnemyProfile>();
            profile.ViewAngle = 360; profile.DetectionGain = 1; profile.AttackRange = 5;
            profile.RepathInterval = 0.1f; profile.PatrolPause = 0.05f; profile.AttackInterval = 0.4f;
            var enemy = Child("Enemy"); enemy.SetActive(false); enemy.transform.position = new Vector2(-2.75f, 0.25f);
            agent = enemy.AddComponent<EnemyAgent>(); agent.Profile = profile; agent.World = world;
            agent.Target = target; agent.Noise = noise; agent.AttackRequested += _ => attacks++;
            enemy.SetActive(true); agent.ResetAgent(); attacks = 0;
        }
        [TearDown]
        public void TearDown()
        { Object.DestroyImmediate(root); Object.DestroyImmediate(profile); Physics2D.SyncTransforms(); LogAssert.NoUnexpectedReceived(); }
        private GameObject Child(string name)
        { var go = new GameObject(name); go.transform.SetParent(root.transform); return go; }
        private GameObject Wall(Vector2 position, Vector2 size)
        {
            var wall = Child("Wall"); wall.layer = 8; wall.transform.position = position;
            wall.AddComponent<BoxCollider2D>().size = size; world.Rebuild(); return wall;
        }
        private void Advance(float seconds)
        { for (int i = 0; i < Mathf.CeilToInt(seconds / 0.05f); i++) agent.Tick(0.05f); }
        private void Until(System.Func<bool> condition, float maximum = 15)
        {
            for (int i = 0; i < maximum / 0.05f && !condition(); i++) agent.Tick(0.05f);
            Assert.That(condition(), Is.True, $"시간 안에 조건 미달: {agent.CurrentGoal}, {agent.PursuitPhase}, {agent.PathError}");
        }
        private void HideTarget() { target.gameObject.SetActive(false); }
        private void Sound(Vector2 where, NoiseKind kind = NoiseKind.Gunshot, float radius = 100)
            => noise.PublishAt(where, radius, agent.GameTime, 1, kind);
        private Transform Point(Vector2 position)
        { var point = Child("Patrol point").transform; point.position = position; return point; }
        private void Route(params Vector2[] positions)
        {
            var points = new Transform[positions.Length];
            for (int i = 0; i < points.Length; i++) points[i] = Point(positions[i]);
            agent.PatrolPoints = points; agent.ResetAgent();
        }

        [Test]
        public void GradualIdentificationCreatesEpisodeBeforeStationaryCombat()
        {
            string firstLine = null;
            agent.SpeechRequested += line => { if (firstLine == null) firstLine = line; };
            Advance(0.4f);
            Assert.That(agent.Suspicion, Is.LessThan(10)); Assert.That(agent.EpisodeId, Is.Zero);
            // 10점 도달 순간과 일반 판단 시점은 다르다. 0.2초 판단 주기/0.5초 유지 후,
            // 직접 식별이 끝나는 1초보다 이른 제한 시간 안에 확인 행동으로 진입해야 한다.
            Until(() => agent.CurrentGoal == EnemyGoal.Inspect, 0.5f);
            Assert.That(agent.Suspicion, Is.GreaterThanOrEqualTo(10));
            Assert.That(agent.IsTargetIdentified, Is.False); Assert.That(attacks, Is.Zero);
            Assert.That(agent.CurrentGoal, Is.EqualTo(EnemyGoal.Inspect));
            Assert.That(firstLine, Does.Contain("움직임"), "직접 시각 단서를 소리라고 설명하면 안 된다.");
            Vector2 anchor = agent.EpisodeAnchor; double deadline = agent.EpisodeDeadline; int id = agent.EpisodeId;
            Advance(0.7f);
            Assert.That(attacks, Is.GreaterThan(0)); Assert.That(agent.CurrentGoal, Is.EqualTo(EnemyGoal.Attack));
            Assert.That(agent.EpisodeId, Is.EqualTo(id)); Assert.That(agent.EpisodeAnchor, Is.EqualTo(anchor));
            Assert.That(agent.EpisodeDeadline, Is.EqualTo(deadline)); Assert.That(agent.Suspicion, Is.GreaterThanOrEqualTo(90));
        }
        [Test]
        public void LightControlsRangeAndOcclusionPreservesHistoricalContributionWithoutAttacking()
        {
            target.Light = LightLevel.Dark; Advance(0.3f); Assert.That(agent.IsTargetVisible, Is.False);
            target.Light = LightLevel.Bright; Advance(1.2f); Assert.That(attacks, Is.GreaterThan(0));
            Vector2 remembered = target.transform.position;
            Wall(new Vector2(-1, 0.25f), new Vector2(0.5f, 12));
            target.transform.position = new Vector2(4, 2); int before = attacks;
            agent.Tick(0.05f);
            Assert.That(agent.IsTargetVisible, Is.False); Assert.That(attacks, Is.EqualTo(before));
            Assert.That(agent.LastKnownPosition, Is.EqualTo(remembered)); Assert.That(agent.Suspicion, Is.GreaterThan(80));
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void DeathDisableAndDestroyCancelAttackImmediatelyButKeepHistoricalSight(int mode)
        {
            Advance(1.2f); int before = attacks; Vector2 remembered = target.transform.position;
            if (mode == 0) target.IsAlive = false;
            else if (mode == 1) target.gameObject.SetActive(false);
            else Object.DestroyImmediate(target.gameObject);
            agent.Tick(0.05f);
            Assert.That(attacks, Is.EqualTo(before)); Assert.That(agent.IsTargetVisible, Is.False);
            Assert.That(agent.HasKnownPosition, Is.True); Assert.That(agent.LastKnownPosition, Is.EqualTo(remembered));
        }
        [Test]
        public void AttackCooldownStaysInsideGoalAndSurvivesGoalChanges()
        {
            Advance(1.2f); Assert.That(agent.CurrentGoal, Is.EqualTo(EnemyGoal.Attack));
            int before = attacks; agent.Tick(0.05f);
            Assert.That(agent.CurrentGoal, Is.EqualTo(EnemyGoal.Attack)); Assert.That(attacks, Is.EqualTo(before));
            HideTarget(); agent.Tick(0.05f); target.gameObject.SetActive(true); agent.Tick(0.05f);
            Assert.That(attacks, Is.EqualTo(before));
            Advance(0.4f); Assert.That(attacks, Is.GreaterThan(before));
        }
        [Test]
        public void TargetReplacementNeedsFreshIdentification()
        {
            Advance(1.2f); int before = attacks;
            var replacement = Child("Replacement").AddComponent<EnemyTarget>(); replacement.transform.position = new Vector2(1, 1);
            agent.SetTarget(replacement); agent.Tick(0.05f);
            Assert.That(agent.Detection, Is.LessThan(0.1f)); Assert.That(attacks, Is.EqualTo(before));
            Assert.That(agent.Memory.TryGet(target.ObservationId, out _), Is.False);
        }
        [Test]
        public void NoiseRemembersImpulseAndResetDoesNotDuplicateSubscription()
        {
            HideTarget(); agent.ResetAgent(); agent.ResetAgent();
            Vector2 position = new Vector2(0.75f, 0.25f); Sound(position); agent.Tick(0.05f);
            Assert.That(agent.HeardNoiseCount, Is.EqualTo(1)); Assert.That(agent.LastKnownPosition, Is.EqualTo(position));
            Assert.That(agent.CurrentGoal, Is.EqualTo(EnemyGoal.Search));
            target.transform.position = new Vector2(5, 5); agent.Tick(0.05f);
            Assert.That(agent.LastKnownPosition, Is.EqualTo(position));
        }
        [Test]
        public void WallsAttenuateSoundPerListener()
        {
            HideTarget(); profile.HearingThreshold = 0.6f;
            Wall(new Vector2(-1, 0), new Vector2(0.5f, 12)); Sound(Vector2.zero);
            Assert.That(agent.HeardNoiseCount, Is.Zero);
        }
        [Test]
        public void InvalidAndZeroTimeFreezeTimersButLongFrameDoesNotExtendDeadline()
        {
            HideTarget(); Sound(new Vector2(1, 0.25f)); agent.Tick(0.05f);
            double time = agent.GameTime, deadline = agent.EpisodeDeadline; Vector3 position = agent.transform.position;
            agent.Tick(0); agent.Tick(-1); agent.Tick(float.NaN); agent.Tick(float.PositiveInfinity);
            Assert.That(agent.GameTime, Is.EqualTo(time)); Assert.That(agent.transform.position, Is.EqualTo(position));
            agent.Tick(30);
            Assert.That(agent.EpisodeDeadline == deadline || agent.EpisodeId == 0, Is.True);
            Assert.That(agent.CurrentGoal, Is.EqualTo(EnemyGoal.ReturnToAnchor));
            Assert.That(Vector2.Distance(position, agent.transform.position), Is.LessThanOrEqualTo(profile.MoveSpeed * 0.25f + 0.01f));
        }
        [Test]
        public void NewCluesAndCombatDoNotResetEpisodeBudget()
        {
            HideTarget(); Sound(new Vector2(1, 0.25f)); Advance(0.2f);
            int id = agent.EpisodeId; double deadline = agent.EpisodeDeadline; Vector2 anchor = agent.EpisodeAnchor;
            Sound(new Vector2(2, 1), NoiseKind.Door); target.gameObject.SetActive(true); Advance(1.5f);
            Assert.That(agent.EpisodeId, Is.EqualTo(id)); Assert.That(agent.EpisodeDeadline, Is.EqualTo(deadline));
            Assert.That(agent.EpisodeAnchor, Is.EqualTo(anchor));
        }
        [Test]
        public void ReturnLatchHoldsUntilArrivalThenDirectSightBypassesWeakClueDelay()
        {
            HideTarget(); profile.EpisodeDuration = 1.5f; profile.MoveSpeed = 1;
            Vector2 home = agent.transform.position; Sound(new Vector2(2, 0.25f));
            Until(() => agent.IsReturning, 3);
            int id = agent.EpisodeId; Sound(new Vector2(-4, 1), NoiseKind.Gunshot);
            long pendingId = 0; foreach (var clue in agent.Memory.Entries) if (clue.Report.Position == new Vector2(-4, 1)) pendingId = clue.Report.Id;
            target.gameObject.SetActive(true); int before = attacks;
            agent.Tick(0.05f);
            Assert.That(agent.EpisodeId, Is.EqualTo(id)); Assert.That(agent.IsReturning, Is.True);
            Until(() => agent.IsRecoveryProtected, 5);
            Assert.That(Vector2.Distance(home, agent.transform.position), Is.LessThanOrEqualTo(0.15f));
            Assert.That(agent.EpisodeId, Is.Zero); Assert.That(attacks, Is.EqualTo(before));
            Assert.That(agent.Memory.TryGet(pendingId, out var waiting) && !waiting.Processed, Is.True);
            Until(() => attacks > before, 0.5f);
            Assert.That(agent.IsRecoveryProtected, Is.True); Assert.That(agent.EpisodeId, Is.GreaterThan(id));
        }
        [Test]
        public void FinitePatrolCompletesOnlyAtPostAndRequiresExternalRestart()
        {
            HideTarget(); Vector2 post = agent.transform.position;
            Route(new Vector2(-1.75f, 0.25f), new Vector2(-0.75f, 0.25f));
            int completed = 0; agent.PatrolCompleted += () => completed++;
            Assert.That(agent.StartPatrol(), Is.False);
            Until(() => completed == 1, 10);
            Assert.That(agent.NextPatrolIndex, Is.EqualTo(2)); Assert.That(agent.CurrentDuty, Is.EqualTo(EnemyDuty.Guard));
            Assert.That(Vector2.Distance(post, agent.transform.position), Is.LessThanOrEqualTo(0.15f));
            Advance(2); Assert.That(completed, Is.EqualTo(1)); Assert.That(agent.StartPatrol(), Is.True);
        }
        [Test]
        public void WaypointArrivalWinsSameTickAsClueAndRestoresNextUnfinishedVisit()
        {
            HideTarget(); Vector2 initial = agent.transform.position;
            Route(initial, new Vector2(0.25f, 0.25f), new Vector2(2.25f, 0.25f));
            Sound(new Vector2(-1.25f, 1.25f)); agent.Tick(0.05f);
            Assert.That(agent.NextPatrolIndex, Is.EqualTo(1)); Assert.That(agent.EpisodeAnchor, Is.EqualTo(initial));
            profile.EpisodeDuration = 0.1f; // 기록 생성 뒤 설정 변경으로 기존 마감을 줄여 쓰지 않는다.
            double deadline = agent.EpisodeDeadline; agent.Tick(21);
            Assert.That(agent.EpisodeDeadline == deadline || agent.EpisodeId == 0, Is.True);
            Until(() => agent.IsRecoveryProtected, 10);
            Assert.That(agent.NextPatrolIndex, Is.EqualTo(1));
            Until(() => agent.NextPatrolIndex >= 2, 10);
            Assert.That(agent.CurrentDuty, Is.EqualTo(EnemyDuty.Patrol));
        }
        [Test]
        public void DeletedOrVersionChangedPatrolPointStopsWithoutSkipping()
        {
            HideTarget(); Route(new Vector2(2.25f, 0.25f));
            agent.PatrolRouteVersion++; Advance(2);
            Assert.That(agent.NextPatrolIndex, Is.Zero); Assert.That(agent.PathFailed, Is.True);
            Vector3 position = agent.transform.position; Object.DestroyImmediate(agent.PatrolPoints[0].gameObject); Advance(2);
            Assert.That(agent.transform.position, Is.EqualTo(position)); Assert.That(agent.NextPatrolIndex, Is.Zero);
        }
        [Test]
        public void ExpiredClueImmediatelyInvalidatesHeldGoal()
        {
            HideTarget(); profile.SoundMemorySeconds = 0.1f; profile.MinimumGoalHold = 10;
            Sound(new Vector2(2, 1)); agent.Tick(0.05f); Assert.That(agent.CurrentGoal, Is.EqualTo(EnemyGoal.Search));
            agent.Tick(0.1f);
            Assert.That(agent.CurrentGoal, Is.EqualTo(EnemyGoal.ReturnToAnchor)); Assert.That(agent.Suspicion, Is.Zero);
        }
        [Test]
        public void AreaRejectsOutsideClueAndVisibleTargetWithoutBoundaryClamping()
        {
            HideTarget(); profile.PursuitRadius = 1;
            Sound(new Vector2(2, 0.25f)); Advance(0.5f);
            Assert.That(agent.EpisodeId, Is.Zero); Assert.That(agent.CurrentGoal, Is.EqualTo(EnemyGoal.Idle));
            target.gameObject.SetActive(true); Advance(1.5f); Assert.That(attacks, Is.Zero);
        }
        [Test]
        public void UnreachableResponseIsExcludedAndMapChangeCanMakeItEligibleAgain()
        {
            HideTarget(); profile.WallSoundMultiplier = 1;
            var wall = Wall(new Vector2(-1, 0), new Vector2(0.5f, 12));
            Sound(new Vector2(1, 0.25f)); Advance(0.5f);
            Assert.That(agent.PathRequestCount, Is.LessThanOrEqualTo(2));
            Until(() => agent.IsRecoveryProtected, 3);
            Object.DestroyImmediate(wall); world.Rebuild(); Advance(2.1f);
            Sound(new Vector2(1, 0.25f)); Advance(0.5f);
            Assert.That(agent.CurrentGoal, Is.EqualTo(EnemyGoal.Search));
        }
        [Test]
        public void SealedAnchorKeepsEpisodeAndUsesBoundedRetryUntilMapReopens()
        {
            HideTarget(); profile.EpisodeDuration = 2; profile.MoveSpeed = 2;
            Vector2 anchor = agent.transform.position; Sound(new Vector2(2.25f, 0.25f)); Advance(1.5f);
            Assert.That(Vector2.Distance(anchor, agent.transform.position), Is.GreaterThan(1));
            var seal = Wall(anchor, Vector2.one * 0.6f); Until(() => agent.PursuitPhase == PursuitPhase.ReturnBlocked, 6);
            int id = agent.EpisodeId, requests = agent.PathRequestCount;
            Advance(2); Assert.That(agent.EpisodeId, Is.EqualTo(id)); Assert.That(agent.PathRequestCount, Is.EqualTo(requests));
            Assert.That(agent.IsRecoveryProtected, Is.False);
            Object.DestroyImmediate(seal); world.Rebuild(); Until(() => agent.IsRecoveryProtected, 10);
            Assert.That(agent.EpisodeId, Is.Zero);
        }
        [Test]
        public void NewDynamicWallStopsSweptMovementWithoutRebuild()
        {
            HideTarget(); Sound(new Vector2(2, 0.25f)); Advance(0.4f);
            float wallX = agent.transform.position.x + 0.7f;
            var wall = Child("Late wall"); wall.layer = 8; wall.transform.position = new Vector2(wallX, 0);
            wall.AddComponent<BoxCollider2D>().size = new Vector2(0.4f, 12); Physics2D.SyncTransforms();
            Advance(2.5f);
            Assert.That(agent.transform.position.x, Is.LessThan(wallX - 0.2f));
        }
        [Test]
        public void OwnDeathCancelsOutputAndMovement()
        {
            Advance(1.2f); int before = attacks; Vector3 position = agent.transform.position;
            agent.IsAlive = false; Advance(1); Sound(Vector2.zero);
            Assert.That(attacks, Is.EqualTo(before)); Assert.That(agent.transform.position, Is.EqualTo(position));
            Assert.That(agent.HeardNoiseCount, Is.Zero);
        }
        [Test]
        public void InvalidWallMaskReportsErrorAndRecoversAfterRebuild()
        {
            world.ObstacleMask = 0;
            Assert.That(agent.ValidateConfiguration(out string error), Is.False);
            LogAssert.Expect(LogType.Warning, error); agent.Tick(0.1f);
            Assert.That(agent.ValidationError, Is.Not.Null);
            world.ObstacleMask = 1 << 8; world.Rebuild(); agent.Tick(0.1f);
            Assert.That(agent.ValidationError, Is.Null);
        }
        [Test]
        public void CurrentDirectEnemyRemainsActionableWhenHistoricalRecordWasProcessed()
        {
            Advance(1.2f); agent.Memory.MarkProcessed(target.ObservationId); int before = attacks;
            Advance(0.5f);
            Assert.That(agent.CurrentGoal, Is.EqualTo(EnemyGoal.Attack)); Assert.That(attacks, Is.GreaterThan(before));
        }
        [Test]
        public void LaterEpisodeGetsNewAnchorAndDeadlineAfterPatrolRecoveryProgress()
        {
            HideTarget(); profile.EpisodeDuration = 0.8f; profile.MoveSpeed = 2;
            Route(new Vector2(-0.75f, 0.25f), new Vector2(2.25f, 0.25f), new Vector2(3.75f, 0.25f));
            Sound(new Vector2(-1.75f, 1.25f)); agent.Tick(0.05f);
            int firstId = agent.EpisodeId; Vector2 firstAnchor = agent.EpisodeAnchor; double firstDeadline = agent.EpisodeDeadline;
            Until(() => agent.IsRecoveryProtected, 6);
            Until(() => !agent.IsRecoveryProtected, 10);
            Assert.That(agent.NextPatrolIndex, Is.GreaterThanOrEqualTo(1));
            Sound((Vector2)agent.transform.position + Vector2.up); agent.Tick(0.05f);
            Assert.That(agent.EpisodeId, Is.GreaterThan(firstId)); Assert.That(agent.EpisodeDeadline, Is.GreaterThan(firstDeadline));
            Assert.That(Vector2.Distance(firstAnchor, agent.EpisodeAnchor), Is.GreaterThan(1));
        }
        [Test]
        public void RecoveryDelayExpiresWithoutFinishingNextVisitOrPost()
        {
            HideTarget(); profile.EpisodeDuration = 0.5f;
            Route(new Vector2(3.25f, 0.25f)); Advance(0.4f);
            Sound((Vector2)agent.transform.position + Vector2.up); agent.Tick(0.05f);
            Until(() => agent.IsRecoveryProtected, 5);
            profile.MoveSpeed = 0.2f; Advance(2.5f);
            Assert.That(agent.IsRecoveryProtected, Is.False); Assert.That(agent.NextPatrolIndex, Is.Zero);
            Sound((Vector2)agent.transform.position + Vector2.up); agent.Tick(0.05f);
            Assert.That(agent.EpisodeId, Is.GreaterThan(0)); Assert.That(agent.NextPatrolIndex, Is.Zero);
        }
        [Test]
        public void HighSuspicionWithoutCurrentSightNeverAuthorizesAttack()
        {
            HideTarget(); Sound(agent.transform.position); Sound(agent.transform.position); Advance(0.2f);
            Assert.That(agent.Suspicion, Is.EqualTo(100)); Assert.That(agent.IsTargetIdentified, Is.False);
            Assert.That(attacks, Is.Zero); Assert.That(agent.CurrentGoal, Is.EqualTo(EnemyGoal.Search));
        }
        [Test]
        public void ClearedSearchDoesNotRepeatWithoutFreshInformation()
        {
            HideTarget(); profile.SearchRadius = 0; profile.SearchPointDuration = 0.1f;
            Sound(agent.transform.position); Until(() => agent.IsRecoveryProtected, 3);
            int requests = agent.PathRequestCount; Advance(3);
            Assert.That(agent.EpisodeId, Is.Zero); Assert.That(agent.CurrentGoal, Is.EqualTo(EnemyGoal.Idle));
            Assert.That(agent.PathRequestCount, Is.EqualTo(requests));
        }
        [Test]
        public void StartInsideWallDoesNotTeleportEvenIfGlobalQueriesSkipStartingColliders()
        {
            HideTarget(); bool previous = Physics2D.queriesStartInColliders;
            try
            {
                Wall(agent.transform.position, Vector2.one); Physics2D.queriesStartInColliders = false;
                Vector3 position = agent.transform.position; Sound((Vector2)position + Vector2.right); Advance(1);
                Assert.That(agent.transform.position, Is.EqualTo(position)); Assert.That(agent.EpisodeId, Is.Zero);
            }
            finally { Physics2D.queriesStartInColliders = previous; }
        }
        [Test]
        public void NoiseBusReplacementUnsubscribesOldAndLateBindingReceivesOnlyOnce()
        {
            HideTarget(); var replacement = Child("Replacement noise").AddComponent<NoiseBus>();
            agent.Noise = replacement; agent.Tick(0.05f);
            Sound(agent.transform.position); Assert.That(agent.HeardNoiseCount, Is.Zero);
            replacement.PublishAt(agent.transform.position, 100, agent.GameTime);
            Assert.That(agent.HeardNoiseCount, Is.EqualTo(1));
        }
        [Test]
        public void VisibleButUnreachableEnemyFailsPreparationInsteadOfFallingThroughToWatch()
        {
            profile.AttackRange = 0.5f;
            // 시야 선은 통과하지만 폭 0.2 틈을 반경 0.15의 몸은 통과할 수 없다.
            Wall(new Vector2(-1, 3.35f), new Vector2(0.5f, 6));
            Wall(new Vector2(-1, -2.85f), new Vector2(0.5f, 6));
            Until(() => agent.IsRecoveryProtected, 1.5f);
            Assert.That(agent.IsTargetVisible, Is.True); Assert.That(attacks, Is.Zero);
            Assert.That(agent.IsRecoveryProtected, Is.True); Assert.That(agent.PathRequestCount, Is.EqualTo(1));
        }
        [Test]
        public void BlockedReturnRetainsRetryHistoryAcrossTemporaryWorldDisable()
        {
            HideTarget(); profile.EpisodeDuration = 2;
            Vector2 anchor = agent.transform.position; Sound(new Vector2(2.25f, 0.25f)); Advance(1.5f);
            Wall(anchor, Vector2.one * 0.6f); Until(() => agent.PursuitPhase == PursuitPhase.ReturnBlocked, 6);
            int before = agent.PathRequestCount; int episode = agent.EpisodeId;
            world.enabled = false; agent.ValidateConfiguration(out string error);
            LogAssert.Expect(LogType.Warning, error); agent.Tick(0.05f);
            world.enabled = true; world.Rebuild(); Advance(1.1f);
            Assert.That(agent.PathRequestCount, Is.EqualTo(before + 1));
            Assert.That(agent.PursuitPhase, Is.EqualTo(PursuitPhase.ReturnBlocked)); Assert.That(agent.EpisodeId, Is.EqualTo(episode));
            Advance(2); Assert.That(agent.PathRequestCount, Is.EqualTo(before + 1));
        }
        [Test]
        public void ReentrantTargetReplacementCancelsRemainingAttackAndSpeech()
        {
            int lines = 0; profile.SpeechInterval = 0.1f; profile.SameSpeechInterval = 0.1f;
            agent.AttackRequested += _ => agent.SetTarget(null);
            agent.SpeechRequested += line => { if (line == "사격한다!") lines++; };
            Advance(1.2f);
            Assert.That(attacks, Is.EqualTo(1)); Assert.That(lines, Is.Zero); Assert.That(agent.Target, Is.Null);
        }
        [Test]
        public void WeakClueWaitsOnlyForMinimumTimeAfterReturn()
        {
            HideTarget(); profile.SearchRadius = 0; profile.SearchPointDuration = 0.1f;
            Sound(agent.transform.position); Until(() => agent.IsRecoveryProtected, 3);
            Sound((Vector2)agent.transform.position + Vector2.up);
            Advance(1.5f); Assert.That(agent.EpisodeId, Is.Zero); Assert.That(agent.IsRecoveryProtected, Is.True);
            Until(() => agent.EpisodeId > 0, 1);
            Assert.That(agent.IsRecoveryProtected, Is.False);
        }
        [Test]
        public void ExhaustedUnconfirmedClueSpeaksDoubtOnceAfterArrival()
        {
            HideTarget(); profile.SpeechInterval = profile.SameSpeechInterval = 0.1f;
            var lines = new List<string>(); agent.SpeechRequested += lines.Add;
            Sound(agent.transform.position, NoiseKind.Footstep);
            Until(() => lines.Contains("착각인가? 주변을 경계한다."), 3);
            Advance(3);
            Assert.That(lines.FindAll(line => line.StartsWith("착각인가?")).Count, Is.EqualTo(1));
            Assert.That(lines.Exists(line => line.StartsWith("놓쳤군.")), Is.False);
        }
        [Test]
        public void LostIdentifiedEnemyUsesLostContactLineInsteadOfDoubt()
        {
            profile.SpeechInterval = profile.SameSpeechInterval = 0.1f;
            profile.SearchRadius = 0; profile.SearchPointDuration = 0.1f; profile.MoveSpeed = 10;
            var lines = new List<string>(); agent.SpeechRequested += lines.Add;
            Advance(1.2f); Assert.That(agent.IsTargetIdentified, Is.True); HideTarget();
            Until(() => lines.Contains("놓쳤군. 경계를 유지한다."), 8);
            Assert.That(lines.Exists(line => line.StartsWith("착각인가?")), Is.False);
        }
        [TestCase(false)] [TestCase(true)]
        public void DeadlineReturnUsesTimeLimitAndCurrentVisibilityFacts(bool visible)
        {
            profile.SpeechInterval = profile.SameSpeechInterval = 0.1f;
            profile.EpisodeDuration = 0.4f; profile.DetectionGain = 10; profile.AttackInterval = 100;
            var lines = new List<string>(); agent.SpeechRequested += lines.Add;
            if (!visible) { HideTarget(); Sound((Vector2)agent.transform.position + Vector2.right); }
            Until(() => lines.Exists(line => line.StartsWith("대응 시간이 끝났다.")), 4);
            Assert.That(lines.Exists(line => line.StartsWith("착각인가?")), Is.False);
            Assert.That(lines.Exists(line => line.Contains("보이는 대상")), Is.EqualTo(visible));
        }
        [Test]
        public void FreshClueWaitingDuringReturnDoesNotGetDoubtLine()
        {
            HideTarget(); profile.SearchRadius = 0; profile.SearchPointDuration = 0.1f;
            profile.SpeechInterval = profile.SameSpeechInterval = 0.1f;
            var lines = new List<string>(); agent.SpeechRequested += lines.Add;
            Sound(new Vector2(0.75f, 0.25f)); Until(() => agent.IsReturning, 8);
            Sound(new Vector2(-4, 1)); Until(() => agent.IsRecoveryProtected, 8);
            agent.Tick(0.1f);
            Assert.That(lines.Exists(line => line.StartsWith("착각인가?")), Is.False);
        }
        [Test]
        public void BlockedReturnDefendsWithoutChangingEpisodeAndSharesAttackInterval()
        {
            HideTarget(); profile.EpisodeDuration = 2;
            Vector2 anchor = agent.transform.position; Sound(new Vector2(2.25f, 0.25f)); Advance(1.5f);
            Wall(anchor, Vector2.one * 0.6f); Until(() => agent.PursuitPhase == PursuitPhase.ReturnBlocked, 6);
            int episodeId = agent.EpisodeId; double deadline = agent.EpisodeDeadline; Vector3 position = agent.transform.position;
            target.transform.position = (Vector2)position + Vector2.right * 0.5f;
            target.gameObject.SetActive(true); profile.DetectionGain = 100;
            Until(() => attacks > 0, 0.5f);
            Assert.That(agent.EpisodeId, Is.EqualTo(episodeId)); Assert.That(agent.EpisodeDeadline, Is.EqualTo(deadline));
            Assert.That(agent.EpisodeAnchor, Is.EqualTo(anchor)); Assert.That(agent.CurrentGoal, Is.EqualTo(EnemyGoal.ReturnToAnchor));
            Assert.That(agent.transform.position, Is.EqualTo(position)); Assert.That(agent.IsRecoveryProtected, Is.False);
            int before = attacks; agent.Tick(0.05f); Assert.That(attacks, Is.EqualTo(before));
            // 명중 여부와 무관하게 몸/표적이 고정 추적 영역 안에 있어야 제자리 방어가 가능하다.
            target.transform.position = anchor + Vector2.right * (profile.PursuitRadius + 0.5f); Advance(0.5f);
            Assert.That(attacks, Is.EqualTo(before));
            target.IsAlive = false; Advance(0.5f); Assert.That(attacks, Is.EqualTo(before));
        }
        [Test]
        public void BlockedPatrolDefendsWithoutStartingResponseEpisodeOrSkippingVisit()
        {
            HideTarget(); Route(new Vector2(2.25f, 0.25f));
            Wall(new Vector2(-1, 0), new Vector2(0.5f, 12));
            Until(() => agent.IsPathBlocked, 5);
            Vector3 position = agent.transform.position;
            target.transform.position = (Vector2)position + Vector2.right * 0.5f;
            target.gameObject.SetActive(true); profile.DetectionGain = 100;
            Until(() => attacks > 0, 0.5f);
            Assert.That(agent.EpisodeId, Is.Zero); Assert.That(agent.CurrentGoal, Is.EqualTo(EnemyGoal.Patrol));
            Assert.That(agent.NextPatrolIndex, Is.Zero); Assert.That(agent.transform.position, Is.EqualTo(position));
        }
        [Test]
        public void LateReturnGenerationIsDiscardedWhenCurrentEnemyRequiresNewResponse()
        {
            HideTarget(); profile.SpeechInterval = profile.SameSpeechInterval = 0.1f;
            var dialogue = agent.gameObject.AddComponent<EnemyDialogue>();
            var provider = agent.gameObject.AddComponent<DialogueTestProvider>(); dialogue.Provider = provider; agent.Dialogue = dialogue;
            var late = new TaskCompletionSource<string>();
            provider.Responses.Enqueue(Task.FromResult("소리 확인")); provider.Responses.Enqueue(late.Task);
            var lines = new List<string>(); agent.SpeechRequested += lines.Add;
            try
            {
                agent.Tick(0.05f); Sound(agent.transform.position, NoiseKind.Footstep);
                Until(() => agent.IsRecoveryProtected && dialogue.IsPending, 3);
                target.gameObject.SetActive(true); profile.DetectionGain = 100; agent.Tick(0.05f);
                late.SetResult("착각인가? 주변을 경계한다."); dialogue.Tick(0, false);
                Assert.That(attacks, Is.GreaterThan(0)); Assert.That(lines.Exists(line => line.StartsWith("착각인가?")), Is.False);
            }
            finally { dialogue.Cancel(); late.TrySetCanceled(); }
        }
        [Test]
        public void SlowReturnGenerationSurvivesNormalDutyResumeAndFallsBackOnce()
        {
            HideTarget(); profile.SpeechInterval = profile.SameSpeechInterval = 0.1f;
            var dialogue = agent.gameObject.AddComponent<EnemyDialogue>();
            var provider = agent.gameObject.AddComponent<DialogueTestProvider>(); dialogue.Provider = provider; agent.Dialogue = dialogue;
            var late = new TaskCompletionSource<string>();
            provider.Responses.Enqueue(Task.FromResult("소리 확인")); provider.Responses.Enqueue(late.Task);
            var lines = new List<string>(); agent.SpeechRequested += lines.Add;
            try
            {
                agent.Tick(0.05f); Sound(agent.transform.position, NoiseKind.Footstep);
                Until(() => agent.IsRecoveryProtected && dialogue.IsPending, 3);
                agent.Tick(0.05f); Assert.That(agent.CurrentGoal, Is.EqualTo(EnemyGoal.Idle));
                Assert.That(dialogue.IsPending, Is.True);
                dialogue.Tick(3, false); late.SetResult("늦은 결과"); dialogue.Tick(0, false);
                Assert.That(lines.FindAll(line => line.StartsWith("착각인가?")).Count, Is.EqualTo(1));
                Assert.That(lines.Contains("늦은 결과"), Is.False);
            }
            finally { dialogue.Cancel(); late.TrySetCanceled(); }
        }
    }
}
