using System.Collections.Generic;
using UnityEngine;

namespace GameAi
{
    // 적이 AI 총괄(AiDirector)에서 받는 공용 기능
    public interface IEnemyWorld
    {
        GridMap Grid { get; }
        AStarPathfinder Nav { get; }
        AiConfig Ai { get; }
        EnemyMoveConfig Move { get; }
        IReadOnlyList<CombatTarget> Targets { get; }
        float DifficultyMult { get; }
        int GlobalAlertLevel { get; }
        void RequestPath(EnemyController enemy);
        bool TryOpenDoorAt(Vector2Int cell, int actorId);
        void OnDetected(EnemyController enemy);
        void PublishState(EnemyController enemy, EnemyState from, EnemyState to, string reason);
    }

    // 적 1명. 감지 결과는 AiDirector가 넣어 주고, 여기서는 FSM 전환·이동·사격을 한다(자체 Update 없음)
    public sealed class EnemyController : MonoBehaviour
    {
        const float ArriveDistance = 0.15f;
        const float TurnDegPerSecond = 360f;

        IEnemyWorld world;
        readonly AwarenessGauge gauge = new AwarenessGauge();
        readonly List<Vector2Int> path = new List<Vector2Int>();
        int pathIndex;
        Vector2 destination;
        bool wantsPath;
        bool hasPath;
        float lastPlanTime = float.NegativeInfinity;
        float pathFailTime = float.NegativeInfinity;
        int pathFailures;
        float doorWait;
        float stuckTime;
        Vector2 lastPosition;

        // 순찰
        Vector2[] patrol = new Vector2[0];
        int patrolIndex;
        float waitLeft;

        // 조사·수색
        ClueRecord currentClue;
        float lookLeft;
        bool arrived;
        bool pendingNewClue;
        bool pendingRadio;
        readonly List<Vector2> searchPoints = new List<Vector2>();
        int searchIndex;
        float searchEnd;

        // 교전
        int mag;
        float reloadLeft;
        float nextShotTime;
        Vector2? coverSpot;
        bool wasDetected;

        // 감지 결과(AiDirector가 채움)
        bool playerSeen;
        float riseMult = 1f;

        public int Id { get { return Target.Id; } }
        public CombatTarget Target { get; private set; }
        public Health Health { get; private set; }
        public CharacterView View { get; private set; }
        public EnemyTypeDef Type { get; private set; }
        public Vector2 Position { get { return Target.Position; } }
        public Vector2 Facing { get; private set; } = Vector2.right;
        public EnemyState State { get; private set; } = EnemyState.Patrol;
        public float Gauge { get { return gauge.Value; } }
        public SuspicionMeter Suspicion { get; } = new SuspicionMeter();
        public Tension Tension { get { return SuspicionMeter.TensionOf(Suspicion.Value, world.Ai.suspicion.tension_bands); } }
        public Vector2 LastKnownPlayer { get; private set; }
        public bool PlayerVisible { get { return playerSeen; } }
        public bool IsMoving { get; private set; }
        public int EpisodeId { get; private set; }
        public int ActionId { get; private set; }
        public string Persona { get; set; } = "veteran";
        public string Callsign { get; set; } = "";
        public Vector2 SpawnPosition { get; private set; }
        public int GlimpseEventId { get; set; } = -1;
        public bool IsReturning { get; private set; }
        public IReadOnlyList<Vector2Int> CurrentPath { get { return path; } }
        public int PathIndex { get { return pathIndex; } }
        // 이 적이 이미 본 물체 단서(같은 물체는 1회)
        public readonly HashSet<int> SeenObjects = new HashSet<int>();
        // 이 적이 이미 아는 죽은 동료(무응답 점검)
        public readonly HashSet<int> KnownDead = new HashSet<int>();
        public float NextRadioCheck { get; set; }
        public float ViewRange { get { return Type.view_m * world.DifficultyMult; } }
        public float Fov { get { return Type.fov_deg; } }
        public bool IsDead { get { return State == EnemyState.Dead || !Target.Alive; } }

        public void Init(IEnemyWorld enemyWorld, CombatTarget target, Health health, CharacterView view, EnemyTypeDef type, Vector2 facing, Vector2[] patrolPoints)
        {
            world = enemyWorld;
            Target = target;
            Health = health;
            View = view;
            Type = type;
            Facing = facing.sqrMagnitude > 0f ? facing.normalized : Vector2.right;
            view.SetAim(MathUtil.AngleDeg(Facing));
            patrol = patrolPoints ?? new Vector2[0];
            SpawnPosition = target.Position;
            mag = type.weapon.mag;
            lastPosition = target.Position;
            waitLeft = Rng.Ai.Range(world.Ai.patrol.wait_min_s, world.Ai.patrol.wait_max_s);
        }

        // ---- AiDirector가 부르는 감지 입력 ----

        public void SetSight(bool seen, Vector2 playerPos, float mult)
        {
            playerSeen = seen;
            riseMult = mult;
            if (seen) LastKnownPlayer = playerPos;
        }

        // 의심도에 새 단서가 들어왔다(의심도 10 이상)
        public void FlagNewClue(ClueRecord record, bool radio)
        {
            pendingNewClue = true;
            pendingRadio |= radio;
            if (State == EnemyState.Investigate && record != null && currentClue != null && record != currentClue)
            {
                ClueRecord best = Suspicion.BestClue(Position);
                if (best != currentClue) StartInvestigate(best);
            }
        }

        // ---- 매 프레임 판단·행동 ----

        public void Tick(float dt, float now)
        {
            if (State == EnemyState.Dead) return;
            AiConfig ai = world.Ai;
            gauge.Tick(dt, playerSeen, riseMult, ai.gauge);
            bool detected = gauge.Value >= ai.gauge.detect;
            if (detected && !wasDetected) world.OnDetected(this);
            wasDetected = detected;
            Suspicion.SetDetected(detected, ai.suspicion.detected_floor);
            Suspicion.Tick(now, dt, ai.suspicion);

            var input = new BrainInput
            {
                Dead = Health.IsDead,
                Gauge = gauge.Value,
                DetectAt = ai.gauge.detect,
                SuspectAt = ai.gauge.suspect,
                PlayerVisible = playerSeen,
                InRange = Vector2.Distance(Position, LastKnownPlayer) <= Type.weapon.range_m,
                NewClue = pendingNewClue && Suspicion.Value >= ai.suspicion.new_clue_min && Suspicion.BestClue(Position) != null,
                ArrivedAtClue = arrived && lookLeft <= 0f,
                SearchDone = now >= searchEnd,
                RadioReceived = pendingRadio,
            };
            EnemyState next = EnemyBrain.Next(State, input, out string reason);
            if (next != State) ChangeState(next, reason, now);
            pendingNewClue = false;
            pendingRadio = false;
            if (State == EnemyState.Dead) return;

            switch (State)
            {
                case EnemyState.Patrol: TickPatrol(dt); break;
                case EnemyState.Investigate: TickInvestigate(dt); break;
                case EnemyState.Chase: TickChase(now); break;
                case EnemyState.Engage: TickEngage(dt, now); break;
                case EnemyState.Search: TickSearch(dt); break;
            }
            Move(dt, now);
        }

        void ChangeState(EnemyState next, string reason, float now)
        {
            EnemyState from = State;
            State = next;
            ActionId++;
            if (from == EnemyState.Patrol) EpisodeId++;
            arrived = false;
            lookLeft = 0f;
            IsReturning = false;
            switch (next)
            {
                case EnemyState.Investigate:
                    StartInvestigate(gauge.Value >= world.Ai.gauge.suspect && playerSeen ? null : Suspicion.BestClue(Position));
                    break;
                case EnemyState.Search:
                    StartSearch(now);
                    break;
                case EnemyState.Engage:
                    float delay = world.Ai.combat.first_shot_s / world.DifficultyMult;
                    nextShotTime = Mathf.Max(nextShotTime, now + delay);
                    coverSpot = null;
                    break;
                case EnemyState.Patrol:
                    // 수색이 끝나면 지금까지의 단서는 처리한 것으로 보고 가장 가까운 순찰 지점으로
                    foreach (ClueRecord r in Suspicion.Records) r.Handled = true;
                    patrolIndex = NearestPatrolIndex();
                    IsReturning = true;
                    SetDestination(patrol.Length > 0 ? patrol[patrolIndex] : SpawnPosition);
                    break;
                case EnemyState.Dead:
                    Stop();
                    break;
            }
            world.PublishState(this, from, next, reason);
        }

        int NearestPatrolIndex()
        {
            int best = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < patrol.Length; i++)
            {
                float d = Vector2.SqrMagnitude(patrol[i] - Position);
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        void TickPatrol(float dt)
        {
            if (patrol.Length == 0)
            {
                if (Vector2.Distance(Position, SpawnPosition) > ArriveDistance && !hasPath && !wantsPath) SetDestination(SpawnPosition);
                return;
            }
            if (hasPath || wantsPath) return;
            if (Vector2.Distance(Position, patrol[patrolIndex]) > 0.4f)
            {
                SetDestination(patrol[patrolIndex]);
                return;
            }
            IsReturning = false;
            waitLeft -= dt;
            if (waitLeft > 0f) return;
            waitLeft = Rng.Ai.Range(world.Ai.patrol.wait_min_s, world.Ai.patrol.wait_max_s);
            patrolIndex = (patrolIndex + 1) % patrol.Length;
            SetDestination(patrol[patrolIndex]);
        }

        void StartInvestigate(ClueRecord clue)
        {
            currentClue = clue;
            arrived = false;
            lookLeft = 0f;
            Vector2 target = clue != null ? clue.Position : LastKnownPlayer;
            SetDestination(target);
        }

        void TickInvestigate(float dt)
        {
            if (playerSeen && gauge.Value >= world.Ai.gauge.suspect)
            {
                // 흐릿하게 보이는 쪽으로 다가간다
                if (Vector2.Distance(destination, LastKnownPlayer) > 1f) SetDestination(LastKnownPlayer);
            }
            if (arrived)
            {
                lookLeft -= dt;
                LookAround(dt);
                if (lookLeft <= 0f && currentClue != null) Suspicion.MarkHandled(currentClue.EventId);
                return;
            }
            if (!hasPath && !wantsPath && Vector2.Distance(Position, destination) <= 0.6f)
            {
                arrived = true;
                lookLeft = world.Ai.search.look_s;
            }
            else if (!hasPath && !wantsPath && pathFailures > world.Ai.nav.max_retry)
            {
                // 닿을 수 없는 단서는 그 자리에서 둘러본 것으로 친다
                arrived = true;
                lookLeft = world.Ai.search.look_s;
            }
        }

        void StartSearch(float now)
        {
            float total = world.Ai.search.total_s * world.DifficultyMult;
            if (world.GlobalAlertLevel >= 1) total *= world.Ai.search.alert_mult;
            searchEnd = now + total;
            int count = Suspicion.Value >= world.Ai.suspicion.search_points_high_at ? world.Ai.suspicion.search_points_high : world.Ai.search.points;
            Vector2 center = currentClue != null && !playerSeen ? currentClue.Position : LastKnownPlayer;
            if (currentClue == null && LastKnownPlayer == Vector2.zero) center = Position;
            searchPoints.Clear();
            searchPoints.AddRange(SearchPointPicker.Pick(world.Nav, world.Grid, center, Position, world.Ai.search.radius_m, count, Rng.Ai));
            searchIndex = 0;
            lookLeft = 0f;
            if (searchPoints.Count > 0) SetDestination(searchPoints[0]);
        }

        void TickSearch(float dt)
        {
            if (searchIndex >= searchPoints.Count)
            {
                LookAround(dt);
                return;
            }
            if (hasPath || wantsPath) return;
            if (lookLeft > 0f)
            {
                lookLeft -= dt;
                LookAround(dt);
                if (lookLeft <= 0f)
                {
                    searchIndex++;
                    if (searchIndex < searchPoints.Count) SetDestination(searchPoints[searchIndex]);
                }
                return;
            }
            lookLeft = world.Ai.search.look_s;
        }

        void TickChase(float now)
        {
            int cells = world.Ai.nav.chase_move_cells;
            if (Vector2.Distance(destination, LastKnownPlayer) > cells * world.Grid.CellSize || (!hasPath && !wantsPath))
                SetDestination(LastKnownPlayer);
            if (playerSeen) FaceToward(LastKnownPlayer, 999f);
        }

        void TickEngage(float dt, float now)
        {
            AiConfig ai = world.Ai;
            bool reloading = reloadLeft > 0f;
            if (reloading)
            {
                reloadLeft -= dt;
                if (reloadLeft <= 0f) mag = Type.weapon.mag;
            }
            float distance = Vector2.Distance(Position, LastKnownPlayer);
            bool inRange = distance <= Type.weapon.range_m;
            bool lineBlocked = !LineOfSight.IsClear(world.Grid, Position, LastKnownPlayer, RayMask.Bullet);
            float hpRatio = (float)Health.Hp / Mathf.Max(1, Type.hp);
            bool coverAvailable = coverSpot.HasValue || FindCover(out _);
            CombatAction action = CombatTree.Decide(hpRatio, ai.combat.cover_hp_ratio, reloadLeft > 0f, mag <= 0, lineBlocked,
                inRange, playerSeen, coverAvailable, true);

            switch (action)
            {
                case CombatAction.TakeCover:
                    if (!coverSpot.HasValue && FindCover(out Vector2 spot)) coverSpot = spot;
                    if (coverSpot.HasValue && Vector2.Distance(destination, coverSpot.Value) > 0.3f) SetDestination(coverSpot.Value);
                    if (mag <= 0 && reloadLeft <= 0f) StartReload();
                    if (playerSeen) FaceToward(LastKnownPlayer, 999f);
                    break;
                case CombatAction.Reload:
                    if (reloadLeft <= 0f) StartReload();
                    Stop();
                    if (playerSeen) FaceToward(LastKnownPlayer, 999f);
                    break;
                case CombatAction.Approach:
                case CombatAction.MoveToLastSeen:
                    coverSpot = null;
                    if (Vector2.Distance(destination, LastKnownPlayer) > 1f || (!hasPath && !wantsPath)) SetDestination(LastKnownPlayer);
                    break;
                case CombatAction.Shoot:
                    Stop();
                    FaceToward(LastKnownPlayer, 999f);
                    if (now >= nextShotTime) Fire(now);
                    break;
            }
        }

        void StartReload()
        {
            reloadLeft = Type.weapon.reload_s;
            EventBus.Publish(new EnemyActionEvent { enemyId = Id, action = "reload", time = GameClock.Time });
        }

        // 6m 안 엄폐물 옆 칸 중 플레이어 쪽 사선이 막히는 가장 가까운 곳
        bool FindCover(out Vector2 spot)
        {
            spot = Position;
            GridMap grid = world.Grid;
            float radius = world.Ai.combat.cover_search_radius_m;
            int r = Mathf.CeilToInt(radius / grid.CellSize);
            Vector2Int c = grid.WorldToCell(Position);
            float best = float.MaxValue;
            bool found = false;
            for (int y = c.y - r; y <= c.y + r; y += 2)
            {
                for (int x = c.x - r; x <= c.x + r; x += 2)
                {
                    if (!world.Nav.IsWalkable(x, y)) continue;
                    Vector2 p = grid.CellCenter(x, y);
                    float d = Vector2.Distance(p, Position);
                    if (d > radius || d >= best) continue;
                    if (!NearCover(grid, x, y)) continue;
                    if (LineOfSight.IsClear(grid, p, LastKnownPlayer, RayMask.Bullet)) continue;
                    best = d;
                    spot = p;
                    found = true;
                }
            }
            return found;
        }

        static bool NearCover(GridMap grid, int x, int y)
        {
            for (int oy = -2; oy <= 2; oy++)
                for (int ox = -2; ox <= 2; ox++)
                {
                    TileType t = grid.GetTile(x + ox, y + oy);
                    if (t == TileType.CoverLow || t == TileType.CoverHigh) return true;
                }
            return false;
        }

        void Fire(float now)
        {
            if (mag <= 0) return;
            mag--;
            EnemyWeaponDef weapon = Type.weapon;
            nextShotTime = now + weapon.interval_s;
            CombatConfig combat = world.Ai.combat;
            float error = combat.aim_error_deg / world.DifficultyMult;
            if (world.Targets.Count > 0 && world.Targets[0].Team == Team.Player && IsPlayerRunning) error *= combat.run_aim_mult;
            Vector2 aim = MathUtil.Rotate((LastKnownPlayer - Position).normalized, Rng.Ai.Range(-error, error));
            HitResult hit = Hitscan.Cast(world.Grid, Position, aim, weapon.range_m, world.Targets, Team.Enemy);
            View.PlayShoot();
            Vector2 muzzle = View.MuzzlePosition(Position);
            if (!LineOfSight.IsClear(world.Grid, Position, muzzle, RayMask.Bullet)) muzzle = Position;
            EventBus.Publish(new ShotFiredEvent { shooterId = Id, muzzlePos = muzzle, dir = aim, weaponId = Type.id, endPos = hit.End });
            if (hit.HitTarget)
            {
                EventBus.Publish(new DamageEvent
                {
                    targetId = hit.TargetId,
                    amount = weapon.damage,
                    attackerId = Id,
                    hitPoint = hit.End,
                    kind = DamageKind.Bullet,
                    time = Time.time,
                });
            }
        }

        // 플레이어가 달리는 중인지(조준 오차 배율). AiDirector가 매 프레임 넣어 준다
        public bool IsPlayerRunning { get; set; }

        void LookAround(float dt)
        {
            // 게임 시각 기준이라 같은 입력이면 같은 방향을 본다
            Facing = MathUtil.Rotate(Facing, 90f * dt * (Mathf.Sin(GameClock.Time * 1.7f + Id) > 0f ? 1f : -1f));
            View.SetAim(MathUtil.AngleDeg(Facing));
        }

        void FaceToward(Vector2 point, float maxDegPerSecond)
        {
            Vector2 to = point - Position;
            if (to.sqrMagnitude < 1e-4f) return;
            float current = MathUtil.AngleDeg(Facing);
            float target = MathUtil.AngleDeg(to);
            float next = maxDegPerSecond >= 900f ? target : Mathf.MoveTowardsAngle(current, target, maxDegPerSecond * Time.deltaTime);
            Facing = MathUtil.FromAngleDeg(next);
            View.SetAim(next);
        }

        // ---- 이동 ----

        public void SetDestination(Vector2 point)
        {
            destination = point;
            wantsPath = true;
            hasPath = false;
            arrived = false;
        }

        void Stop()
        {
            wantsPath = false;
            hasPath = false;
            path.Clear();
            IsMoving = false;
        }

        // AiDirector가 프레임당 예산 안에서 부른다
        public bool NeedsPath(float now)
        {
            if (!wantsPath) return false;
            if (now - lastPlanTime < world.Ai.nav.recalc_min_interval_s) return false;
            if (pathFailures > 0 && now - pathFailTime < world.Ai.nav.retry_delay_s) return false;
            return true;
        }

        public void PlanPath(float now)
        {
            lastPlanTime = now;
            wantsPath = false;
            GridMap grid = world.Grid;
            bool ok = world.Nav.FindPath(grid.WorldToCell(Position), grid.WorldToCell(destination), path);
            if (!ok)
            {
                pathFailures++;
                pathFailTime = now;
                hasPath = false;
                if (pathFailures <= world.Ai.nav.max_retry) wantsPath = true;
                return;
            }
            pathFailures = 0;
            hasPath = true;
            pathIndex = path.Count > 1 ? 1 : 0;
        }

        void Move(float dt, float now)
        {
            IsMoving = false;
            if (!hasPath)
            {
                View.Tick(dt, CharacterPose.Idle);
                return;
            }
            GridMap grid = world.Grid;
            Vector2 target = pathIndex >= path.Count - 1 ? destination : grid.CellCenter(path[pathIndex]);
            if (pathIndex < path.Count)
            {
                Vector2Int cell = path[pathIndex];
                if (grid.GetTile(cell.x, cell.y) == TileType.Door && !grid.IsDoorOpen(cell.x, cell.y))
                {
                    // 문 앞에서 0.5초 멈춰 열고 지나간다
                    FaceToward(grid.CellCenter(cell), TurnDegPerSecond);
                    doorWait += dt;
                    if (doorWait >= world.Ai.nav.door_open_wait_s)
                    {
                        doorWait = 0f;
                        world.TryOpenDoorAt(cell, Id);
                    }
                    View.Tick(dt, CharacterPose.Idle);
                    return;
                }
            }
            doorWait = 0f;
            bool chase = State == EnemyState.Chase || State == EnemyState.Engage;
            float speed = chase ? world.Move.chase_speed : world.Move.patrol_speed;
            Vector2 to = target - Position;
            float distance = to.magnitude;
            if (distance <= ArriveDistance)
            {
                pathIndex++;
                if (pathIndex >= path.Count)
                {
                    hasPath = false;
                    path.Clear();
                }
                View.Tick(dt, CharacterPose.Idle);
                return;
            }
            Vector2 step = to / distance * Mathf.Min(distance, speed * dt);
            float radius = Target.Radius;
            Vector2 next = PlayerMotor.Move(grid, Position, step, radius);
            // 통로 충돌: 2초 이상 막히면 다른 개체와 겹침을 잠시 허용(7.16)
            if (stuckTime < world.Ai.passage.overlap_after_s)
                next = PlayerMotor.PushOutOfTargets(next, radius, world.Targets, Id);
            next = PlayerMotor.PushOutOfCells(grid, next, radius);
            float moved = Vector2.Distance(next, Position);
            stuckTime = moved < speed * dt * 0.2f ? stuckTime + dt : 0f;
            if (stuckTime > world.Ai.nav.blocked_s && stuckTime - dt <= world.Ai.nav.blocked_s) wantsPath = true;
            SetPosition(next);
            IsMoving = moved > 1e-4f;
            if (State != EnemyState.Engage || !playerSeen) FaceToward(Position + to, TurnDegPerSecond);
            View.Tick(dt, IsMoving ? CharacterPose.Walk : CharacterPose.Idle);
        }

        public void SetPosition(Vector2 p)
        {
            Target.Position = p;
            transform.position = p;
        }

        public Vector2 Destination { get { return destination; } }
        public bool HasPath { get { return hasPath; } }
    }
}
