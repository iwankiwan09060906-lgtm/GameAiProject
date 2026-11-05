// 역할: 선택된 목표를 실제 순서로 실행한다. Utility는 이 파일에 없으며 행동 성공/실패만 돌려준다.
// 예: 수색 = 계획 → [이동 → 조사 대기 → 다음 지점] 반복 → 단서 처리 완료.
using System.Collections.Generic;
using UnityEngine;

namespace NullTrace.AI
{
    public sealed partial class EnemyAgent
    {
        private BehaviorNode tree;
        private float waitElapsed;
        private readonly List<Vector2> searchPoints = new List<Vector2>();
        private int searchIndex;
        private double dutyPauseUntil;

        private BehaviorNode Branch(EnemyGoal goal, params BehaviorNode[] procedure)
            => new BehaviorSequence(new BehaviorAction(dt => CurrentGoal == goal ? BehaviorStatus.Success : BehaviorStatus.Failure),
                new BehaviorSequence(procedure));
        private BehaviorAction Action(System.Func<float, BehaviorStatus> step) => new BehaviorAction(step);
        private void BuildTree()
        {
            tree = new BehaviorSelector(
                Branch(EnemyGoal.ReturnToAnchor, Action(ReturnMove), Action(dt => { FinishReturn(); return BehaviorStatus.Success; })),
                Branch(EnemyGoal.Patrol, Action(CheckRoute), Action(WaitDuty), Action(PatrolMove), Action(dt =>
                    { CompleteDutyArrival(); return BehaviorStatus.Success; })),
                Branch(EnemyGoal.ReturnToPost, Action(CheckRoute), Action(WaitDuty), Action(PostMove), Action(dt =>
                    { CompleteDutyArrival(); return BehaviorStatus.Success; })),
                Branch(EnemyGoal.Inspect, Action(dt =>
                    { FaceClue(); return Wait(dt, Profile.InspectDuration); }), Action(dt =>
                    { memory.MarkInspected(selected.ClueId); return BehaviorStatus.Success; })),
                Branch(EnemyGoal.Search, Action(PlanSearch),
                    new BehaviorRepeat(new BehaviorSequence(Action(SearchMove), Action(dt =>
                        { RotateView(dt); return Wait(dt, Profile.SearchPointDuration); }), Action(dt =>
                        { searchIndex++; waitElapsed = 0; ResetMovement(false); return BehaviorStatus.Success; })),
                        () => searchIndex >= searchPoints.Count),
                    Action(CompleteSearch)),
                Branch(EnemyGoal.PrepareCombat, new BehaviorSelector(
                    new BehaviorSequence(Action(dt => IsTargetIdentified ? BehaviorStatus.Success : BehaviorStatus.Failure), Action(PrepareVisible)),
                    new BehaviorSequence(Action(dt => !IsTargetIdentified ? BehaviorStatus.Success : BehaviorStatus.Failure),
                        Action(dt => { RotateView(dt); return Wait(dt, Profile.PrepareDuration); }), Action(dt =>
                        { prepareCompletedVersion = memory.InformationVersion; return BehaviorStatus.Success; })))),
                Branch(EnemyGoal.Attack, Action(Attack)),
                Branch(EnemyGoal.Idle, Action(dt => BehaviorStatus.Running)));
        }
        private BehaviorStatus Wait(float dt, float duration)
        { waitElapsed += dt; return waitElapsed >= duration ? BehaviorStatus.Success : BehaviorStatus.Running; }
        private void RotateView(float dt)
        {
            float angle = dt * 90 * Mathf.Deg2Rad;
            FacingDirection = new Vector2(FacingDirection.x * Mathf.Cos(angle) - FacingDirection.y * Mathf.Sin(angle),
                FacingDirection.x * Mathf.Sin(angle) + FacingDirection.y * Mathf.Cos(angle));
        }
        private void FaceClue()
        { if (memory.TryGet(selected.ClueId, out var clue)) Face(clue.Report.Position - (Vector2)transform.position); }
        private BehaviorStatus PlanSearch(float dt)
        {
            if (!memory.TryGet(selected.ClueId, out var clue) || episode == null) return BehaviorStatus.Failure;
            searchPoints.Clear(); searchIndex = 0;
            // 실제 단서 위치를 조사하지 못했는데 주변 경계만 방문하고 완료라고 보고하지 않는다.
            if (!episode.Area.Contains(clue.Report.Position, World.AgentRadius) || !World.CanOccupy(clue.Report.Position))
                return BehaviorStatus.Failure;
            searchPoints.Add(clue.Report.Position);
            int desired = Suspicion >= 50 ? 5 : 3;
            Vector2[] offsets = { Vector2.right, Vector2.up, Vector2.left, Vector2.down };
            foreach (var offset in offsets)
            {
                Vector2 point = clue.Report.Position + offset * Profile.SearchRadius;
                if (searchPoints.Count >= desired) break;
                if (episode.Area.Contains(point, World.AgentRadius) && World.CanOccupy(point) && !searchPoints.Contains(point)) searchPoints.Add(point);
            }
            return BehaviorStatus.Success;
        }
        private BehaviorStatus SearchMove(float dt)
            => Move(searchPoints[searchIndex], Profile.MoveSpeed, dt, episode.Area, false);
        private BehaviorStatus CompleteSearch(float dt)
        {
            if (memory.TryGet(selected.ClueId, out var clue))
            {
                // 이번 계획 뒤 다른 위치를 새로 봤다면 옛 조사 결과로 새 관측을 완료 처리하지 않는다.
                if (clue.Report.Kind == ClueKind.Sighting && searchPoints.Count > 0
                    && (clue.Report.Position - searchPoints[0]).sqrMagnitude >= 0.25f) return BehaviorStatus.Success;
                memory.MarkProcessed(selected.ClueId);
                var report = new ClueReport(AiEventIds.Next(), ClueKind.Investigation, NoiseKind.Footstep,
                    clue.Report.Position, GameTime, GameTime, GameTime + Profile.SoundMemorySeconds,
                    GetInstanceID(), GetInstanceID(), 0, 0, 1, false, Profile.SearchRadius);
                memory.Receive(report, GameTime, true); Radio?.Send(this, report, GameTime);
            }
            Speak("해당 단서 주변 조사 완료.");
            return BehaviorStatus.Success;
        }
        private BehaviorStatus ReturnMove(float dt)
            => Move(episode.Anchor, Profile.MoveSpeed, dt, episode.Area, true);
        private BehaviorStatus PrepareVisible(float dt)
        {
            if (!IsTargetIdentified || !memory.TryGet(selected.ClueId, out var clue)) return BehaviorStatus.Failure;
            Face(clue.Report.Position - (Vector2)transform.position);
            // 사격이 가능해지면 다음 판단에서 Attack으로 바뀐다. 발사 간격은 여기서 건드리지 않는다.
            if (CanAttack()) { forceDecision = true; return BehaviorStatus.Running; }
            return Move(clue.Report.Position, Profile.CombatMoveSpeed, dt, episode.Area, false);
        }
        private bool CanAttack()
        {
            // 복귀 후 약한 단서 대기는 직접 식별한 적을 무시하게 만들지 않는다. 이동 중 복귀 금지는 유지한다.
            return Profile && World && !IsReturning && HasFiringSolution(ResponseArea());
        }
        private bool HasFiringSolution(MovementConstraint2D area)
        {
            if (!isActiveAndEnabled || !IsAlive || !IsTargetIdentified || !World || !World.IsReady || !Profile) return false;
            Vector2 position = Target.transform.position;
            return area.Contains(transform.position, World.AgentRadius)
                && area.Contains(position, World.AgentRadius)
                && Vector2.Distance(transform.position, position) <= Profile.AttackRange
                && HasCurrentSight();
        }
        private BehaviorStatus Attack(float dt)
        {
            // Utility 평가 뒤에도 피해/벽/대상 위치가 변할 수 있으므로 실제 요청 직전 다시 검사한다.
            if (!CanAttack()) return BehaviorStatus.Failure;
            FireAtTarget("사격한다!");
            return BehaviorStatus.Running;
        }
        /// <summary>
        /// Movement가 필수 경로의 3회 실패 후 정지한 동안 호출한다. 주어진 이동 영역이 사격 허용 영역이기도 하다.
        /// 추적 기록/목표/위치에는 손대지 않으며 다음 발사 가능 시각은 정상 Attack과 공유한다.
        /// 따라서 막힘 해제나 목표 전환으로 연사 간격이 초기화되지 않는다.
        /// </summary>
        private void GuardBlockedPath(MovementConstraint2D area)
        {
            if (!movementBlocked || !IsMandatory(CurrentGoal) || !HasFiringSolution(area)) return;
            FireAtTarget("길이 막혔다. 제자리에서 대응한다.");
        }
        private void FireAtTarget(string line)
        {
            Vector2 aim = Target.transform.position;
            Face(aim - (Vector2)transform.position);
            if (GameTime >= nextAttack)
            {
                nextAttack = GameTime + Profile.AttackInterval;
                int firingAction = actionId;
                AttackRequested?.Invoke(aim);
                if (firingAction == actionId) Speak(line, true);
            }
        }
        private bool RouteValid()
        {
            if (!patrolActive) return true;
            if (PatrolRouteVersion != routeVersion || PatrolPoints == null || PatrolPoints.Length != visits.Length) return false;
            for (int i = 0; i < visits.Length; i++)
                if (!visits[i] || PatrolPoints[i] != visits[i] || (Vector2)visits[i].position != visitPositions[i]) return false;
            return true;
        }
        private BehaviorStatus CheckRoute(float dt)
        {
            if (RouteValid()) return BehaviorStatus.Success;
            ReportPath("순찰 방문 목록·위치·버전이 바뀌었습니다. 회차를 건너뛰지 않고 정지합니다.");
            return BehaviorStatus.Running;
        }
        private BehaviorStatus WaitDuty(float dt) => GameTime >= dutyPauseUntil ? BehaviorStatus.Success : BehaviorStatus.Running;
        private BehaviorStatus PatrolMove(float dt)
        {
            // 진행 중에도 참조 삭제와 버전 변경을 확인한다. 첫 노드 통과 후 삭제되는 경우도 막는다.
            if (!RouteValid()) return CheckRoute(dt);
            if (NextPatrolIndex >= visitPositions.Length) return BehaviorStatus.Success;
            return Move(visitPositions[NextPatrolIndex], Profile.MoveSpeed, dt, new MovementConstraint2D(Resolve(PatrolArea)), true);
        }
        private BehaviorStatus PostMove(float dt)
        {
            if (!RouteValid()) return CheckRoute(dt);
            return Move(post, Profile.MoveSpeed, dt, new MovementConstraint2D(Resolve(PostReturnArea)), true);
        }
        private void CompleteDutyArrival()
        {
            if (!patrolActive || !RouteValid()) return;
            if (CurrentDuty == EnemyDuty.Patrol && NextPatrolIndex < visitPositions.Length
                && Arrived(visitPositions[NextPatrolIndex], new MovementConstraint2D(Resolve(PatrolArea))))
            {
                NextPatrolIndex++;
                dutyPauseUntil = GameTime + Profile.PatrolPause;
                if (NextPatrolIndex >= visitPositions.Length) CurrentDuty = EnemyDuty.ReturnToPost;
                CancelAction(); forceDecision = true;
            }
            // 마지막 점과 초소가 같은 경우에도 '초소 실제 도착' 조건을 따로 확인한다.
            if (CurrentDuty == EnemyDuty.ReturnToPost && Arrived(post, new MovementConstraint2D(Resolve(PostReturnArea))))
            {
                patrolActive = false; CurrentDuty = EnemyDuty.Guard;
                CancelAction(); forceDecision = true;
                PatrolCompleted?.Invoke();
            }
        }
    }
}
