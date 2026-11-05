// 역할: 자체 A*의 후보 구간과 실제 한 걸음에 같은 이동 영역·몸체 폭을 적용한다.
// 추적 후보는 실패 시 제외한다. 필수 업무 이동은 1초 이상 간격으로 3회, 이후 5초 간격으로 복구를 기다린다.
using System.Collections.Generic;
using UnityEngine;

namespace NullTrace.AI
{
    public sealed partial class EnemyAgent
    {
        private readonly List<Vector2Int> path = new List<Vector2Int>();
        private GridWorld2D pathWorld;
        private PathRequestBudget pathBudget;
        public bool IsPathBlocked => movementBlocked;
        private int pathIndex, pathRevision = -1, failedAttempts;
        private bool hasPathGoal, movementBlocked;
        private Vector2Int pathGoal;
        private double nextPath, lastAttempt = double.NegativeInfinity, lastProgressAt;
        private Vector2 progressPosition;

        private void ResetMovement(bool resetClock, bool preserveFailure = false)
        {
            if (pathBudget) pathBudget.Cancel(this);
            path.Clear(); pathIndex = 0; hasPathGoal = false;
            if (!preserveFailure)
            {
                failedAttempts = 0; movementBlocked = false;
                PathFailed = false; PathError = null; pathRevision = -1; pathWorld = null;
            }
            lastProgressAt = GameTime; progressPosition = transform.position;
            if (resetClock) { nextPath = GameTime; lastAttempt = double.NegativeInfinity; }
        }
        private bool Arrived(Vector2 destination, MovementConstraint2D area)
        {
            if (!World || !Profile) return false;
            Vector2 position = transform.position;
            float tolerance = Mathf.Min(Profile.ArrivalDistance, World.CellSize * 0.2f, 0.15f);
            return Vector2.Distance(position, destination) <= tolerance
                && area.AllowsSegment(position, destination, World.AgentRadius) && World.CanTraverse(position, destination);
        }
        private void ReportPath(string reason)
        {
            bool changed = !PathFailed || PathError != reason;
            PathFailed = true; PathError = reason;
            // 상세 원인은 PathError로 개발자에게 남기고, 플레이어에게는 NPC가 할 법한 말만 보여 준다.
            if (changed) Speak("길이 막혔다. 주변을 경계한다.");
        }
        private BehaviorStatus FailMove(string reason, bool mandatory)
        {
            path.Clear(); hasPathGoal = false;
            failedAttempts++; lastAttempt = GameTime;
            bool newlyBlocked = mandatory && failedAttempts >= 3 && !movementBlocked;
            movementBlocked |= mandatory && failedAttempts >= 3;
            nextPath = GameTime + (movementBlocked ? 5 : Mathf.Max(1, Profile.PathRetryDelay));
            ReportPath(reason);
            if (newlyBlocked)
            {
                if (IsReturning) episode.Phase = PursuitPhase.ReturnBlocked;
                Speak("길이 막혔다. 여기서 경계하며 지원을 기다린다.");
            }
            return mandatory ? BehaviorStatus.Running : BehaviorStatus.Failure;
        }
        private bool Segment(Vector2 from, Vector2 to, MovementConstraint2D area)
            => area.AllowsSegment(from, to, World.AgentRadius) && World.CanPlanTraverse(from, to);
        private BehaviorStatus Move(Vector2 destination, float speed, float dt, MovementConstraint2D area, bool mandatory)
        {
            Vector2 position = transform.position;
            // 맵/월드 교체는 경로만 폐기한다. 실패 재요청의 최소 1초 제한은 우회하지 않는다.
            bool mapChanged = pathWorld != World || pathRevision != World.Revision;
            if (mapChanged)
            {
                // 같은 월드의 지도만 바뀌었다면 기다린 순서를 보존한다. 문이 자주 바뀔 때
                // 매번 표를 버리고 줄 끝으로 가면 뒤의 NPC는 영원히 계산하지 못할 수 있다.
                if (pathWorld != World && pathBudget) pathBudget.Cancel(this);
                path.Clear(); hasPathGoal = false; pathWorld = World; pathRevision = World.Revision;
                pathBudget = World.GetComponent<PathRequestBudget>();
                if (!pathBudget) pathBudget = World.gameObject.AddComponent<PathRequestBudget>();
                if (movementBlocked) nextPath = System.Math.Max(lastAttempt + 1, GameTime);
            }
            if (Arrived(destination, area))
            {
                PathFailed = false; PathError = null; movementBlocked = false; failedAttempts = 0;
                return BehaviorStatus.Success;
            }
            bool goalInGrid = World.TryGetCell(destination, out Vector2Int goalCell);
            if (hasPathGoal && goalCell != pathGoal) { path.Clear(); hasPathGoal = false; }
            if (!hasPathGoal)
            {
                // 복귀 기록은 그대로 둔 채 눈앞의 적에게만 제자리 방어한다. 공격 때문에 새 추적을 열지 않는다.
                if (movementBlocked)
                {
                    int guardingAction = actionId;
                    GuardBlockedPath(area);
                    if (guardingAction != actionId || !IsAlive || !isActiveAndEnabled) return BehaviorStatus.Running;
                }
                if (GameTime < nextPath) return BehaviorStatus.Running;
                long budgetTick = automaticTick ? Time.frameCount : (long)System.Math.Floor(GameTime * 60 + 0.000001);
                if (!pathBudget || !pathBudget.TryAcquire(this, budgetTick)) return BehaviorStatus.Running;
                PathRequestCount++; lastAttempt = GameTime;
                nextPath = GameTime + Profile.RepathInterval;
                if (!goalInGrid || !World.TryGetCell(position, out Vector2Int start)
                    || !area.Contains(position, World.AgentRadius) || !area.Contains(destination, World.AgentRadius)
                    || !World.CanOccupy(position) || !World.CanOccupy(destination))
                    return FailMove("출발/목적지가 허용 구역 밖이거나 벽과 겹칩니다.", mandatory);
                // 셀 중심 사이도 실제 원형 몸체가 지나갈 수 있어야 한다. 끝에서만 거부하는 우회 검사가 아니다.
                if (!AStarPathfinder.TryFindPath(World.Map, start, goalCell, path, true,
                    (a, b) => Segment(World.CellCenter(a), World.CellCenter(b), area))
                    || path.Count == 0 || !Segment(position, World.CellCenter(path[0]), area)
                    || !Segment(World.CellCenter(path[path.Count - 1]), destination, area))
                    return FailMove("허용 영역 안의 완전한 경로를 찾지 못했습니다.", mandatory);
                pathGoal = goalCell; hasPathGoal = true; pathIndex = 0;
                PathFailed = false; PathError = null; failedAttempts = 0; movementBlocked = false;
                if (IsReturning) episode.Phase = PursuitPhase.Returning;
                lastProgressAt = GameTime; progressPosition = position;
            }
            Vector2 waypoint = pathIndex < path.Count ? World.CellCenter(path[pathIndex]) : destination;
            while (pathIndex < path.Count && Vector2.Distance(position, waypoint) < 0.001f)
            {
                pathIndex++;
                waypoint = pathIndex < path.Count ? World.CellCenter(path[pathIndex]) : destination;
            }
            Vector2 desired = Vector2.MoveTowards(position, waypoint, speed * dt);
            if (!area.AllowsSegment(position, desired, World.AgentRadius)) return FailMove("다음 이동이 허용 영역을 벗어납니다.", mandatory);
            // 경로 계산은 열 수 있는 문을 포함할 수 있지만, 몸은 실제로 문을 연 뒤에만 통과한다.
            // 현재 이동 방향의 가까운 문만 연다. 한 걸음이 아주 짧아도 개방 감지가 사라지지 않는다.
            int openingAction = actionId;
            World.TryOpenDoorAhead(position, desired);
            if (openingAction != actionId || !IsAlive || !isActiveAndEnabled || !World || !World.IsReady) return BehaviorStatus.Running;
            Face(desired - position);
            bool complete = World.TryMove(position, desired, out Vector2 reached);
            if (!area.AllowsSegment(position, reached, World.AgentRadius)) return FailMove("실제 이동 결과가 허용 영역 밖입니다.", mandatory);
            transform.position = new Vector3(reached.x, reached.y, transform.position.z);
            if ((reached - progressPosition).sqrMagnitude >= 0.0001f)
            { progressPosition = reached; lastProgressAt = GameTime; }
            // 동적 벽에 막히면 정지한 채 최대 2초 진전을 기다린다. 막힌 부분을 강제로 통과하지 않는다.
            if (GameTime - lastProgressAt >= 2)
                return FailMove("이동 중 2초 동안 진전이 없습니다. 문 변경 후 Rebuild가 필요할 수 있습니다.", mandatory);
            if (!complete) PathError = "실제 Collider2D에 막혀 진전을 기다리는 중입니다.";
            return BehaviorStatus.Running;
        }
    }
}
