// 역할: 감각→개인 기억→강제 규칙→Utility 목표→행동 트리를 한 순서로 연결한다.
// EnemyState는 UI 요약일 뿐, 별도로 행동을 결정하는 FSM이 아니다.
// 이동/절차 코드는 같은 partial 클래스의 Movement / Actions 파일에서 이어진다.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace NullTrace.AI
{
    [DisallowMultipleComponent]
    public sealed partial class EnemyAgent : MonoBehaviour
    {
        public EnemyProfile Profile;
        public EnemyTarget Target;
        public NoiseBus Noise;
        public GridWorld2D World;
        public RadioChannel Radio;
        public EnemyDialogue Dialogue;
        public Transform[] PatrolPoints = Array.Empty<Transform>();
        public bool AutoStartPatrol = true;
        public bool IsAlive = true;
        public int PatrolRouteVersion = 1;
        [Tooltip("(0,0,0,0)이면 World.Bounds. 다른 값은 양수 크기의 월드 XY 사각형이어야 합니다.")]
        public Rect PatrolArea, PursuitArea, PostReturnArea;

        public EnemyState State { get; private set; }
        public EnemyGoal CurrentGoal { get; private set; } = EnemyGoal.Idle;
        public EnemyDuty CurrentDuty { get; private set; } = EnemyDuty.Guard;
        public PursuitPhase PursuitPhase => episode == null ? PursuitPhase.None : episode.Phase;
        public float Detection { get; private set; }
        public float Suspicion => memory.Suspicion;
        public Vector2 FacingDirection { get; private set; } = Vector2.right;
        public Vector2 LastKnownPosition { get; private set; }
        public bool IsTargetVisible { get; private set; }
        public bool IsTargetIdentified => IsTargetVisible && Detection >= 1f && IsUsable(Target);
        public bool HasKnownPosition { get; private set; }
        public bool IsReturning => episode != null && episode.Phase != PursuitPhase.Responding;
        public bool IsRecoveryProtected { get; private set; }
        public double GameTime { get; private set; }
        public int EpisodeId => episode?.Id ?? 0;
        public Vector2 EpisodeAnchor => episode?.Anchor ?? post;
        public double EpisodeDeadline => episode?.Deadline ?? 0;
        public double EpisodeStartedAt => episode?.StartedAt ?? 0;
        public int NextPatrolIndex { get; private set; }
        public int PatrolRunId { get; private set; }
        public string GoalReason { get; private set; }
        public string EpisodeEndReason { get; private set; }
        public EnemyMemory Memory => memory;
        public bool PathFailed { get; private set; }
        public string ValidationError { get; private set; }
        public string PathError { get; private set; }
        public int PathRequestCount { get; private set; }
        public int HeardNoiseCount { get; private set; }
        public int RadioReceivedCount { get; private set; }
        public event Action<EnemyState, EnemyState> StateChanged;
        public event Action<Vector2> AttackRequested;
        public event Action<string> SpeechRequested;
        public event Action PatrolCompleted;

        // Episode는 처음 출발할 때만 만든다. 목표 교체로 Anchor/Deadline/순찰 사본을 수정하지 않는다.
        private sealed class Episode
        {
            public int Id, PatrolIndex, RunId, RouteVersion;
            public EnemyDuty Duty;
            public Vector2 Anchor;
            public double StartedAt, Deadline;
            public MovementConstraint2D Area;
            public Transform[] Visits;
            public Vector2[] VisitPositions;
            public PursuitPhase Phase = PursuitPhase.Responding;
            // 소리만 조사한 경우와 실제로 식별한 적을 놓친 경우의 복귀 대사를 구분한다.
            public bool IdentifiedEnemy;
            public readonly HashSet<long> UsedClues = new HashSet<long>();
        }
        private readonly EnemyMemory memory = new EnemyMemory();
        private readonly SpeechGate speech = new SpeechGate();
        private readonly List<EnemyCandidate> candidates = new List<EnemyCandidate>();
        private readonly Dictionary<long, FailedClue> failedClues = new Dictionary<long, FailedClue>();
        private readonly struct FailedClue
        {
            public readonly int MapRevision;
            public readonly Vector2 Position;
            public FailedClue(int revision, Vector2 position) { MapRevision = revision; Position = position; }
        }
        private Episode episode;
        private EnemyCandidate selected;
        private NoiseBus boundNoise;
        private RadioChannel boundRadio;
        private EnemyDialogue boundDialogue;
        private EnemyTarget observedTarget;
        private Transform[] visits = Array.Empty<Transform>();
        private Vector2[] visitPositions = Array.Empty<Vector2>();
        private Vector2 post;
        private Vector2 selectedCluePosition;
        private int routeVersion, episodeSequence, actionId, prepareCompletedVersion = -1;
        private bool ticking, automaticTick, goalActive, forceDecision, patrolActive;
        private double nextDecision, goalStartedAt, nextAttack, recoveryStartedAt;
        // 복귀 결과는 다음 정상 업무에서 한 번 요청한다. 바로 끝난 복귀 행동에 붙이면 다음 Tick 취소로 사라진다.
        private string pendingReturnLine;
        private const string DoubtReturnLine = "착각인가? 주변을 경계한다.";
        private bool returnLineNeedsSight, returnLineNeedsNoSight;
        private string reportedError;
        private int speechActionId, speechEpisodeId;
        private EnemyTarget speechTarget;
        private bool speechNeedsCurrentSight, speechNeedsNoSight, speechNeedsNoClue;

        private void OnEnable() => ResetAgent();
        private void Update()
        {
            automaticTick = true;
            try { Tick(Time.deltaTime); }
            finally { automaticTick = false; }
        }
        private void OnDisable()
        {
            if (boundNoise) boundNoise.Emitted -= Hear;
            if (boundRadio) boundRadio.Unregister(this);
            if (boundDialogue) { boundDialogue.Cancel(); boundDialogue.LineReady -= EmitSpeech; }
            boundNoise = null; boundRadio = null; boundDialogue = null;
            pendingReturnLine = null;
            IsTargetVisible = false;
            CancelAction();
        }
        /// <summary>현재 위치를 새로운 초기 초소로 정한다. 객체를 이동시키거나 이벤트 구독자를 지우지 않는다.</summary>
        public void ResetAgent()
        {
            CancelAction();
            if (boundRadio) boundRadio.Unregister(this);
            boundRadio = null;
            post = transform.position;
            Vector2 direction = transform.right;
            FacingDirection = AiMath.IsFinite(direction) && direction.sqrMagnitude > 0 ? direction.normalized : Vector2.right;
            GameTime = Time.timeAsDouble;
            nextDecision = nextAttack = GameTime;
            dutyPauseUntil = GameTime;
            episode = null; failedClues.Clear(); memory.Clear(); speech.Reset();
            pendingReturnLine = null;
            Detection = 0; IsTargetVisible = HasKnownPosition = IsRecoveryProtected = false;
            LastKnownPosition = post;
            CurrentDuty = EnemyDuty.Guard; CurrentGoal = EnemyGoal.Idle;
            NextPatrolIndex = PatrolRunId = PathRequestCount = HeardNoiseCount = RadioReceivedCount = 0;
            patrolActive = false; visits = Array.Empty<Transform>(); visitPositions = Array.Empty<Vector2>();
            ValidationError = PathError = reportedError = EpisodeEndReason = null;
            prepareCompletedVersion = -1; observedTarget = Target; forceDecision = true;
            ResetMovement(true); BuildTree(); BindInputs(); SetState(EnemyState.Calm);
            if (AutoStartPatrol && PatrolPoints != null && PatrolPoints.Length > 0) StartPatrol();
        }
        /// <summary>유한한 한 회차를 시작한다. 진행 중 회차나 추적 기록은 새 요청으로 덮어쓰지 않는다.</summary>
        public bool StartPatrol()
        {
            if (patrolActive || episode != null || PatrolPoints == null || PatrolPoints.Length == 0) return false;
            foreach (var point in PatrolPoints)
                if (!point || !AiMath.IsFinite((Vector2)point.position)) { ReportPath("순찰점이 없거나 좌표가 잘못되었습니다."); return false; }
            visits = (Transform[])PatrolPoints.Clone();
            visitPositions = new Vector2[visits.Length];
            for (int i = 0; i < visits.Length; i++) visitPositions[i] = visits[i].position;
            routeVersion = PatrolRouteVersion; NextPatrolIndex = 0; PatrolRunId++;
            patrolActive = true; CurrentDuty = EnemyDuty.Patrol; forceDecision = true;
            return true;
        }
        public void SetTarget(EnemyTarget target)
        {
            CancelAction(IsMandatory(CurrentGoal));
            pendingReturnLine = null;
            if (observedTarget) memory.Remove(observedTarget.ObservationId);
            Target = observedTarget = target; Detection = 0; IsTargetVisible = false;
            RefreshKnownPosition();
            forceDecision = true;
        }
        private static bool IsUsable(EnemyTarget target) => target && target.isActiveAndEnabled && target.IsAlive;
        private void BindInputs()
        {
            if (boundNoise != Noise)
            {
                if (boundNoise) boundNoise.Emitted -= Hear;
                boundNoise = Noise;
                if (boundNoise && isActiveAndEnabled) boundNoise.Emitted += Hear;
            }
            if (boundRadio != Radio)
            {
                if (boundRadio) boundRadio.Unregister(this);
                boundRadio = Radio;
                if (boundRadio && isActiveAndEnabled) boundRadio.Register(this);
            }
            if (boundDialogue != Dialogue)
            {
                if (boundDialogue) { boundDialogue.Cancel(); boundDialogue.LineReady -= EmitSpeech; }
                boundDialogue = Dialogue;
                if (boundDialogue) boundDialogue.LineReady += EmitSpeech;
            }
        }
        private Rect Resolve(Rect configured) => configured == default ? World.Bounds : configured;
        private bool ValidArea(Rect area) => AiMath.IsFinite(area.position) && AiMath.IsFinite(area.size)
            && AiMath.IsFinite(area.xMax) && AiMath.IsFinite(area.yMax) && area.width > 0 && area.height > 0;
        public bool ValidateConfiguration(out string error)
        {
            error = null;
            if (!Profile) error = "EnemyAgent.Profile이 없습니다.";
            else if (!World) error = "EnemyAgent.World가 없습니다.";
            if (error != null || !World.ValidateConfiguration(out error)) return false;
            if (!World.IsReady) error = "GridWorld2D가 비활성이거나 Rebuild()가 필요합니다.";
            else if (!AiMath.IsFinite((Vector2)transform.position)) error = "적의 XY 좌표가 유한수가 아닙니다.";
            else if ((World.ObstacleMask.value & (1 << gameObject.layer)) != 0) error = "적 루트 레이어가 ObstacleMask에 포함되어 있습니다.";
            else if (IsUsable(Target) && (World.ObstacleMask.value & (1 << Target.gameObject.layer)) != 0)
                error = "대상 루트 레이어가 ObstacleMask에 포함되어 있습니다.";
            else if (!ValidArea(Resolve(PatrolArea)) || !ValidArea(Resolve(PursuitArea)) || !ValidArea(Resolve(PostReturnArea)))
                error = "이동 구역은 양수 크기의 유한한 사각형이어야 합니다. (0,0,0,0)만 World.Bounds를 뜻합니다.";
            return error == null;
        }
        /// <summary>
        /// 게임 시간은 전체 dt만큼, 감각/이동 적분은 최대 0.25초만 진행한다. 긴 프레임도 추적 마감을 연장하지 않는다.
        /// 자동 Update와 수동 Tick 중 하나만 사용한다. dt=0, NaN, 무한대는 모든 게임 타이머를 멈춘다.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!isActiveAndEnabled || ticking || !AiMath.IsFinite(deltaTime) || deltaTime <= 0) return;
            ticking = true;
            try { Step(deltaTime, Mathf.Min(deltaTime, 0.25f)); }
            finally { ticking = false; }
        }
        private void Step(float elapsed, float dt)
        {
            BindInputs();
            if (!IsAlive) { IsTargetVisible = false; pendingReturnLine = null; CancelAction(); return; }
            GameTime = automaticTick ? Time.timeAsDouble : GameTime + elapsed;
            if (!ValidateConfiguration(out string error))
            {
                ValidationError = error; IsTargetVisible = false; CancelAction(IsMandatory(CurrentGoal));
                if (reportedError != error) { Debug.LogWarning(error, this); reportedError = error; }
                return;
            }
            ValidationError = reportedError = null; Profile.ValidateValues();
            if (observedTarget != Target) SetTarget(Target);
            // 같은 주기의 순찰 도착은 감각보다 먼저 완료한다. 추적 사본에 이미 끝난 점이 남지 않는다.
            if (episode == null) CompleteDutyArrival();
            Sense(dt);
            if (episode != null && IsTargetIdentified) episode.IdentifiedEnemy = true;
            if (Radio) Radio.Deliver(GameTime);
            memory.Advance(GameTime, elapsed, Profile.SuspicionQuietSeconds, Profile.SuspicionDecayPerSecond);
            RefreshKnownPosition();
            UpdateRecovery();
            if (episode != null && episode.Phase == PursuitPhase.Responding
                && (GameTime >= episode.Deadline || !episode.Area.Contains(transform.position, World.AgentRadius)))
                BeginReturn(GameTime >= episode.Deadline ? "추적 시간 제한" : "허용 영역 위반");
            if (IsReturning)
                SelectForced(EnemyGoal.ReturnToAnchor, "이탈점 도착까지 복귀 유지");
            else if (movementBlocked && IsMandatory(CurrentGoal))
                SelectForced(CurrentGoal, "경로 복구 대기와 제자리 경계");
            else if (episode == null && IsRecoveryProtected && !IsTargetIdentified)
                SelectForced(DutyGoal(), "약한 단서를 다시 조사하기 전 잠시 대기");
            else
            {
                // 매 프레임 유효성은 확인하되, 유효한 후보끼리의 경쟁만 일반 판단 주기를 따른다.
                BuildCandidates();
                bool valid = goalActive && candidates.Exists(item => item.Same(selected));
                if (!valid || forceDecision || GameTime >= nextDecision)
                {
                    nextDecision = GameTime + Profile.DecisionInterval; forceDecision = false;
                    if (candidates.Count == 0)
                    {
                        if (episode != null) { BeginReturn("처리할 대응 후보 없음"); SelectForced(EnemyGoal.ReturnToAnchor, EpisodeEndReason); }
                        else SelectForced(DutyGoal(), "원래 업무");
                    }
                    else
                    {
                        var next = EnemyUtility.Choose(candidates, selected, goalActive, GameTime - goalStartedAt,
                            Profile.MinimumGoalHold, Profile.GoalSwitchAdvantage);
                        if (IsResponse(next.Goal) && episode == null && !StartEpisode()) SelectForced(DutyGoal(), "추적 기록 검증 실패");
                        else Select(next, "Utility 최고 유효 후보");
                    }
                }
            }
            if (!goalActive) return;
            int executingAction = actionId;
            var result = tree.Tick(dt);
            // 이벤트 소비자가 Reset/SetTarget을 호출해도 이전 행동의 완료 결과를 새 행동에 붙이지 않는다.
            if (executingAction != actionId) return;
            if (result != BehaviorStatus.Running)
            {
                if (result == BehaviorStatus.Failure && IsResponse(CurrentGoal)) ExcludeSelectedClue();
                goalActive = false; forceDecision = true;
            }
            TrySpeakReturnResult();
        }
        private void Sense(float dt)
        {
            IsTargetVisible = false;
            memory.SetCurrentSighting(0);
            if (IsUsable(Target))
            {
                Vector2 position = Target.transform.position;
                IsTargetVisible = AiMath.IsFinite(position) && AiMath.InView(transform.position, FacingDirection,
                    position, Profile.ViewDistance(Target.Light), Profile.ViewAngle) && World.HasLineOfSight(transform.position, position);
                if (IsTargetVisible)
                {
                    memory.SetCurrentSighting(Target.ObservationId);
                    Detection = Mathf.Clamp01(Detection + Profile.DetectionGain * dt);
                    var report = new ClueReport(Target.ObservationId, ClueKind.Sighting, NoiseKind.Footstep, position,
                        Target.ObservationStartedAt, GameTime, GameTime + Profile.SightMemorySeconds, Target.GetInstanceID(),
                        GetInstanceID(), ++Target.ObservationRevision, 1, 1, IsTargetIdentified);
                    memory.Receive(report, GameTime, true, dt);
                    Radio?.Send(this, report, GameTime);
                    return;
                }
            }
            Detection = Mathf.Max(0, Detection - Profile.DetectionDecay * dt);
        }
        private void Hear(NoiseEvent sound)
        {
            if (!isActiveAndEnabled || !IsAlive || !Profile || !World || !World.IsReady || !AiMath.IsFinite(sound.Position)) return;
            Profile.ValidateValues();
            float strength = AiMath.HeardStrength(Vector2.Distance(transform.position, sound.Position), sound.Radius,
                sound.Loudness, World.CountWalls(transform.position, sound.Position), Profile.WallSoundMultiplier);
            if (strength < Profile.HearingThreshold) return;
            var report = new ClueReport(sound.EventId, ClueKind.Sound, sound.Kind, sound.Position, sound.OccurredAt,
                sound.OccurredAt, sound.OccurredAt + Profile.SoundMemorySeconds, 0, GetInstanceID(), 0, strength, 1);
            // 소리 발행자의 Update가 먼저 실행되면 이 NPC의 마지막 Tick은 한 프레임 전이다.
            // 실제 수신 시각과 수동 시계 중 큰 값을 쓰되 원본 발생 시각/만료는 그대로 둔다.
            double receivedAt = System.Math.Max(GameTime, Time.timeAsDouble);
            if (memory.Receive(report, receivedAt, true))
            { HeardNoiseCount++; Radio?.Send(this, report, receivedAt); forceDecision = true; }
        }
        /// <summary>채널이 전달 완료한 보고만 받는다. 무전을 직접 시야·공격 허가로 바꾸거나 자동 재전송하지 않는다.</summary>
        public void ReceiveRadio(ClueReport report, double receivedAt)
        {
            if (!isActiveAndEnabled || !IsAlive) return;
            if (memory.Receive(report, receivedAt)) { RadioReceivedCount++; forceDecision = true; }
        }
        private void RefreshKnownPosition()
        {
            HasKnownPosition = false; ClueMemory latest = null;
            foreach (var clue in memory.Entries)
                if (clue.Report.Kind != ClueKind.Investigation && (latest == null || clue.Report.ObservedAt > latest.Report.ObservedAt
                    || (clue.Report.ObservedAt == latest.Report.ObservedAt && clue.Report.Id > latest.Report.Id))) latest = clue;
            if (latest != null) { HasKnownPosition = true; LastKnownPosition = latest.Report.Position; }
        }
        private bool IsResponse(EnemyGoal goal) => goal == EnemyGoal.Inspect || goal == EnemyGoal.Search
            || goal == EnemyGoal.PrepareCombat || goal == EnemyGoal.Attack;
        private bool IsMandatory(EnemyGoal goal) => goal == EnemyGoal.Patrol || goal == EnemyGoal.ReturnToPost || goal == EnemyGoal.ReturnToAnchor;
        private EnemyGoal DutyGoal() => CurrentDuty == EnemyDuty.Patrol ? EnemyGoal.Patrol
            : CurrentDuty == EnemyDuty.ReturnToPost ? EnemyGoal.ReturnToPost : EnemyGoal.Idle;
        private MovementConstraint2D ResponseArea() => episode != null ? episode.Area
            : new MovementConstraint2D(Resolve(PursuitArea), transform.position, Profile.PursuitRadius);
        private bool Eligible(ClueMemory clue)
        {
            if (clue.Processed || clue.Report.ExpiresAt <= GameTime || !ResponseArea().Contains(clue.Report.Position, World.AgentRadius)) return false;
            if (!failedClues.TryGetValue(clue.Report.Id, out var failure)) return true;
            return failure.MapRevision != World.Revision || (clue.Direct && clue.Report.Kind == ClueKind.Sighting
                && (clue.Report.Position - failure.Position).sqrMagnitude >= 0.25f);
        }
        private void BuildCandidates()
        {
            candidates.Clear();
            if (IsTargetIdentified)
            {
                if (memory.TryGet(Target.ObservationId, out var sight) && ResponseArea().Contains(sight.Report.Position, World.AgentRadius)
                    && (!failedClues.TryGetValue(sight.Report.Id, out var failed) || failed.MapRevision != World.Revision
                        || (sight.Report.Position - failed.Position).sqrMagnitude >= 0.25f))
                {
                    float distance = Mathf.Clamp01(Vector2.Distance(transform.position, sight.Report.Position) / Profile.PursuitRadius);
                    candidates.Add(new EnemyCandidate(EnemyGoal.PrepareCombat, sight.Report.Id, 90 - 10 * distance));
                    if (CanAttack()) candidates.Add(new EnemyCandidate(EnemyGoal.Attack, sight.Report.Id, 100));
                }
                return;
            }
            bool investigable = false;
            foreach (var clue in memory.Entries)
            {
                if (!Eligible(clue)) continue;
                investigable = true;
                float evidence = clue.Report.Strength * clue.Report.Confidence;
                float distance = Mathf.Clamp01(Vector2.Distance(transform.position, clue.Report.Position) / Profile.PursuitRadius);
                float inspectThreshold = goalActive && selected.Goal == EnemyGoal.Inspect && selected.ClueId == clue.Report.Id ? 5 : 10;
                float searchThreshold = goalActive && selected.Goal == EnemyGoal.Search && selected.ClueId == clue.Report.Id ? 25 : 30;
                if (!clue.Inspected && Suspicion >= inspectThreshold) candidates.Add(new EnemyCandidate(EnemyGoal.Inspect, clue.Report.Id, 20 + 20 * evidence));
                if (Suspicion >= searchThreshold) candidates.Add(new EnemyCandidate(EnemyGoal.Search, clue.Report.Id, 40 + 30 * evidence - 10 * distance));
            }
            float prepareThreshold = goalActive && CurrentGoal == EnemyGoal.PrepareCombat ? 65 : 70;
            if (!investigable && Suspicion >= prepareThreshold && prepareCompletedVersion != memory.InformationVersion)
                candidates.Add(new EnemyCandidate(EnemyGoal.PrepareCombat, 0, 35));
            if (episode == null) candidates.Add(new EnemyCandidate(DutyGoal(), 0, CurrentDuty == EnemyDuty.Guard ? 5 : 10));
        }
        private bool StartEpisode()
        {
            var area = ResponseArea();
            if (!area.Contains(transform.position, World.AgentRadius) || !World.CanOccupy(transform.position))
            { ReportPath("최초 이탈점에 몸 전체가 설 수 없어 대응을 시작할 수 없습니다."); return false; }
            episode = new Episode { Id = ++episodeSequence, Duty = CurrentDuty, Anchor = transform.position,
                StartedAt = GameTime, Deadline = GameTime + Profile.EpisodeDuration, Area = area, PatrolIndex = NextPatrolIndex,
                RunId = PatrolRunId, RouteVersion = routeVersion, Visits = visits, VisitPositions = visitPositions,
                IdentifiedEnemy = IsTargetIdentified };
            pendingReturnLine = null;
            failedClues.Clear(); EpisodeEndReason = null;
            return true;
        }
        private void BeginReturn(string reason)
        {
            if (episode == null || episode.Phase != PursuitPhase.Responding) return;
            episode.Phase = PursuitPhase.Returning; EpisodeEndReason = reason;
            CancelAction(); forceDecision = true;
        }
        private void FinishReturn()
        {
            // 호출자는 실제 마지막 구간과 작은 도착 오차를 이미 검사했다. 이 전환은 실패 때 실행하지 않는다.
            var completed = episode;
            CurrentDuty = completed.Duty; NextPatrolIndex = completed.PatrolIndex; PatrolRunId = completed.RunId;
            routeVersion = completed.RouteVersion; visits = completed.Visits; visitPositions = completed.VisitPositions;
            foreach (long id in completed.UsedClues) memory.MarkProcessed(id);
            QueueReturnResult(completed);
            episode = null; failedClues.Clear();
            IsRecoveryProtected = true; recoveryStartedAt = GameTime;
            if (Dialogue) Dialogue.Cancel();
            forceDecision = true;
            // 생성기가 없으면 도착 즉시 기본 문장을 쓸 수 있다. 비동기 생성은 다음 정상 업무에 붙인다.
            // 막 끝난 Return 행동에 요청하면 다음 Tick의 정상 Idle 전환만으로도 요청이 불필요하게 취소된다.
            if (!(Dialogue && Dialogue.isActiveAndEnabled && Dialogue.Provider)) TrySpeakReturnResult(true);
        }
        private void UpdateRecovery()
        {
            // 다음 순찰점/초소 도착은 요구하지 않는다. 이 시간은 약한 단서의 반복 조사만 늦춘다.
            if (IsRecoveryProtected && GameTime - recoveryStartedAt >= Profile.RecoveryDuration) IsRecoveryProtected = false;
        }
        private void QueueReturnResult(Episode completed)
        {
            returnLineNeedsSight = returnLineNeedsNoSight = false;
            if (IsTargetVisible)
            {
                returnLineNeedsSight = true;
                pendingReturnLine = EpisodeEndReason == "추적 시간 제한"
                    ? "대응 시간이 끝났다. 보이는 대상을 계속 경계한다."
                    : "대상이 아직 보인다. 복귀 위치에서 경계한다.";
            }
            else if (EpisodeEndReason == "추적 시간 제한")
                pendingReturnLine = "대응 시간이 끝났다. 복귀 위치에서 경계한다.";
            else if (completed.IdentifiedEnemy)
            {
                returnLineNeedsNoSight = true;
                pendingReturnLine = "놓쳤군. 경계를 유지한다.";
            }
            else if (EpisodeEndReason == "처리할 대응 후보 없음" && !HasUnprocessedClue())
            {
                returnLineNeedsNoSight = true;
                pendingReturnLine = DoubtReturnLine;
            }
            else pendingReturnLine = "복귀했다. 주변을 경계한다.";
        }
        private bool HasUnprocessedClue()
        {
            foreach (var clue in memory.Entries)
                if (!clue.Processed && clue.Report.Kind != ClueKind.Investigation) return true;
            return false;
        }
        private void TrySpeakReturnResult(bool arriving = false)
        {
            if (pendingReturnLine == null || episode != null || !goalActive || IsResponse(CurrentGoal)
                || (!arriving && CurrentGoal == EnemyGoal.ReturnToAnchor)) return;
            bool sight = HasCurrentSight();
            if ((returnLineNeedsSight && !sight) || (returnLineNeedsNoSight && sight))
            { pendingReturnLine = null; return; }
            if (pendingReturnLine == DoubtReturnLine && HasUnprocessedClue()) pendingReturnLine = "복귀했다. 주변을 경계한다.";
            // 빈도 제한에 걸리면 같은 상황인 동안만 기다린다. 허용된 요청은 한 번만 내보낸다.
            if (Speak(pendingReturnLine, returnLineNeedsSight, returnLineNeedsNoSight)) pendingReturnLine = null;
        }
        private void SelectForced(EnemyGoal goal, string reason) => Select(new EnemyCandidate(goal, 0, 0), reason);
        private void Select(EnemyCandidate next, string reason)
        {
            // 같은 목격 ID라도 직접 관측점이 충분히 이동했다면 수색 절차만 다시 계획한다. 추적 예산은 그대로다.
            bool movedObservation = next.Goal == EnemyGoal.Search && memory.TryGet(next.ClueId, out var updated)
                && updated.Direct && updated.Report.Kind == ClueKind.Sighting
                && (updated.Report.Position - selectedCluePosition).sqrMagnitude >= 0.25f;
            if (goalActive && selected.Same(next) && !movedObservation) return;
            // 같은 필수 업무의 일시 중단/재선택은 차단 이력을 보존한다. 새 목적이나 완료된 순찰점은 별도로 초기화한다.
            CancelAction(next.Goal == CurrentGoal && IsMandatory(next.Goal)); selected = next; CurrentGoal = next.Goal;
            goalActive = true; goalStartedAt = GameTime; GoalReason = reason;
            if (memory.TryGet(next.ClueId, out var selectedClue)) selectedCluePosition = selectedClue.Report.Position;
            if (episode != null && next.ClueId != 0) episode.UsedClues.Add(next.ClueId);
            int selectingAction = actionId;
            SetState(next.Goal == EnemyGoal.Search ? EnemyState.Search
                : next.Goal == EnemyGoal.Attack || next.Goal == EnemyGoal.PrepareCombat ? EnemyState.Combat
                : next.Goal == EnemyGoal.Inspect ? EnemyState.Alert : EnemyState.Calm);
            if (selectingAction != actionId) return;
            // 시각 단서를 확인하는데 소리를 들었다고 말하지 않도록 실제 단서 종류에 맞춘다.
            string inspectLine = memory.TryGet(next.ClueId, out var inspecting) && inspecting.Report.Kind == ClueKind.Sighting
                ? "움직임이 보였다. 확인하겠다." : "소리가 들렸다. 확인하겠다.";
            Speak(next.Goal == EnemyGoal.Search ? "단서 주변을 수색한다." : next.Goal == EnemyGoal.Inspect ? inspectLine
                : next.Goal == EnemyGoal.PrepareCombat ? "경계하고 지원을 요청한다." : null);
        }
        private void CancelAction(bool preserveFailure = false)
        {
            actionId++; goalActive = false; tree?.Reset(); waitElapsed = 0; searchPoints.Clear(); searchIndex = 0;
            if (Dialogue) Dialogue.Cancel();
            ResetMovement(false, preserveFailure);
            // nextAttack은 보존한다. 목표 전환으로 연사 간격을 우회할 수 없다.
        }
        private void ExcludeSelectedClue()
        {
            if (memory.TryGet(selected.ClueId, out var clue)) failedClues[selected.ClueId] = new FailedClue(World.Revision, clue.Report.Position);
            else if (selected.ClueId == 0) prepareCompletedVersion = memory.InformationVersion;
        }
        private void Face(Vector2 direction)
        { if (AiMath.IsFinite(direction) && direction.sqrMagnitude > 0.0001f) FacingDirection = direction.normalized; }
        private void SetState(EnemyState next)
        { if (State == next) return; var previous = State; State = next; StateChanged?.Invoke(previous, next); }
        private void EmitSpeech(string line)
        {
            // Dialogue.Update가 먼저 실행되는 프레임도 있다. 캐시된 IsTargetVisible만 믿지 않고 현재 입력을 재검사한다.
            if (!isActiveAndEnabled || !IsAlive || speechActionId != actionId || speechEpisodeId != EpisodeId) return;
            if (episode != null && episode.Phase == PursuitPhase.Responding
                && System.Math.Max(GameTime, Time.timeAsDouble) >= episode.Deadline) return;
            if (speechNeedsCurrentSight && (Target != speechTarget || !HasCurrentSight())) return;
            if (speechNeedsNoSight && HasCurrentSight()) return;
            if (speechNeedsNoClue && HasUnprocessedClue()) return;
            SpeechRequested?.Invoke(line);
        }
        private bool HasCurrentSight() => IsUsable(Target) && World && World.IsReady && Profile
            && AiMath.InView(transform.position, FacingDirection, Target.transform.position, Profile.ViewDistance(Target.Light), Profile.ViewAngle)
            && World.HasLineOfSight(transform.position, Target.transform.position);
        private bool Speak(string line, bool requireSight = false, bool requireNoSight = false)
        {
            if (!Profile || string.IsNullOrEmpty(line) || !speech.TrySpeak(line, GameTime, Profile.SpeechInterval, Profile.SameSpeechInterval)) return false;
            speechActionId = actionId; speechEpisodeId = EpisodeId; speechTarget = Target;
            speechNeedsCurrentSight = requireSight || (IsTargetIdentified && (CurrentGoal == EnemyGoal.Attack || CurrentGoal == EnemyGoal.PrepareCombat));
            speechNeedsNoSight = requireNoSight;
            speechNeedsNoClue = line == DoubtReturnLine;
            string facts = $"목표: {CurrentGoal}, 알고 있는 단서: {(HasKnownPosition ? LastKnownPosition.ToString() : "없음")}";
            if (Dialogue && Dialogue.isActiveAndEnabled) Dialogue.Request(new EnemyDialogueContext(EpisodeId, actionId, facts, line));
            else EmitSpeech(line);
            return true;
        }
    }
}
