using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameAi
{
    // 적 AI 총괄. 월드 이벤트를 모아 적마다 인지(청각·시각·무전)를 판정하고 의심도에 더한 뒤,
    // 분산 감지·경로 예산·경보·전역 경계도를 처리한다(5.4의 3~5단계). 플레이어 위치는 여기서만 읽는다.
    public sealed class AiDirector : MonoBehaviour, IEnemyWorld
    {
        // 사물 단서(탄피·혈흔·시체·떨어진 무기). 미션 끝까지 남고 적마다 한 번만 본다
        struct ClueObject
        {
            public int EventId;
            public string Code;
            public Vector2 Position;
            public int ObjectId;
        }

        struct RadioDelivery
        {
            public float At;
            public int ReceiverId;
            public int EventId;
            public Vector2 Position;
            public float Value;
            public float Lifetime;
        }

        readonly List<EnemyController> enemies = new List<EnemyController>();
        readonly List<WorldEvent> pending = new List<WorldEvent>();
        readonly List<WorldEvent> processing = new List<WorldEvent>();
        readonly List<ClueObject> clueObjects = new List<ClueObject>();
        readonly List<RadioDelivery> radio = new List<RadioDelivery>();
        readonly Dictionary<int, int> alertsSent = new Dictionary<int, int>();
        readonly Dictionary<int, float> lastAlert = new Dictionary<int, float>();
        readonly Dictionary<int, (float time, int eventId)> doorChangeEvents = new Dictionary<int, (float, int)>();
        readonly Queue<EnemyController> pathQueue = new Queue<EnemyController>();
        readonly HashSet<int> queued = new HashSet<int>();

        GridMap grid;
        AStarPathfinder nav;
        AiConfig ai;
        EnemyMoveConfig move;
        EventTypeTable table;
        IReadOnlyList<CombatTarget> targets;
        IReadOnlyList<Door> doors;
        PlayerController player;
        Func<Vector2, float> lightAt = p => 1f;
        float difficulty = 1f;
        int frame;
        int globalLevel;
        bool gustActive;

        public GridMap Grid { get { return grid; } }
        public AStarPathfinder Nav { get { return nav; } }
        public AiConfig Ai { get { return ai; } }
        public EnemyMoveConfig Move { get { return move; } }
        public IReadOnlyList<CombatTarget> Targets { get { return targets; } }
        public float DifficultyMult { get { return difficulty; } }
        public int GlobalAlertLevel { get { return globalLevel; } }
        public bool GustActive { get { return gustActive; } }
        public IReadOnlyList<EnemyController> Enemies { get { return enemies; } }
        public int PathsThisFrame { get; private set; }
        // 플레이어가 사격해 순간 밝아진 시각(7.17)
        public float LastPlayerShotTime { get; private set; } = float.NegativeInfinity;

        public void Init(GridMap map, AStarPathfinder pathfinder, AiConfig aiConfig, EnemyMoveConfig moveConfig, EventTypeTable eventTable,
            IReadOnlyList<CombatTarget> allTargets, IReadOnlyList<Door> allDoors, PlayerController playerController, float difficultyMult)
        {
            grid = map;
            nav = pathfinder;
            ai = aiConfig;
            move = moveConfig;
            table = eventTable;
            targets = allTargets;
            doors = allDoors;
            player = playerController;
            difficulty = difficultyMult;
        }

        // 단계별 누적 시간(ms): 0 시작, 1 이벤트, 2 감지, 3 경보·경로, 4 적 판단·이동. 부하 측정용
        readonly System.Diagnostics.Stopwatch stage = new System.Diagnostics.Stopwatch();
        public readonly double[] StageMs = new double[5];
        double lastMark;

        void Mark(int index)
        {
            if (index == 0) { stage.Restart(); lastMark = 0; return; }
            double t = stage.Elapsed.TotalMilliseconds;
            StageMs[index] += t - lastMark;
            lastMark = t;
        }

        public void SetLightSource(Func<Vector2, float> source) { lightAt = source ?? (p => 1f); }

        public void Add(EnemyController enemy)
        {
            enemies.Add(enemy);
            // 소총병 무전 점검은 서로 엇갈리게
            enemy.NextRadioCheck = ai.clue.radio_check_s + enemies.Count * 3f;
        }

        void OnEnable()
        {
            EventBus.Subscribe<WorldEvent>(OnWorldEvent);
            EventBus.Subscribe<DeathEvent>(OnDeath);
            EventBus.Subscribe<DoorChangedEvent>(OnDoorChanged);
            EventBus.Subscribe<ShotFiredEvent>(OnShot);
            EventBus.Subscribe<DamageEvent>(OnDamage);
            EventBus.Subscribe<LightChangedEvent>(OnLightChanged);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<WorldEvent>(OnWorldEvent);
            EventBus.Unsubscribe<DeathEvent>(OnDeath);
            EventBus.Unsubscribe<DoorChangedEvent>(OnDoorChanged);
            EventBus.Unsubscribe<ShotFiredEvent>(OnShot);
            EventBus.Unsubscribe<DamageEvent>(OnDamage);
            EventBus.Unsubscribe<LightChangedEvent>(OnLightChanged);
        }

        // ---- 이벤트 수집(처리는 Tick의 정해진 순서에서) ----

        void OnWorldEvent(WorldEvent e)
        {
            // 적 한 명만 아는 이벤트(목격·무전)는 만든 자리에서 바로 처리했다
            if (e.code == "SIG_INTRUDER" || e.code == "SIG_GLIMPSE" || e.code == "CLU_RADIO_HELP" || e.code == "CLU_NO_ANSWER"
                || e.code == "CLU_DOOR_OPEN" || e.code == "CLU_DOOR_CLOSED") return;
            pending.Add(e);
        }

        void OnShot(ShotFiredEvent e)
        {
            if (e.shooterId != EntityIds.Player) return;
            LastPlayerShotTime = GameClock.Time;
            // 총구 화염(시야 각도 무관)과 떨어진 탄피. 탄피 위치는 연출 난수 없이 사수 옆으로 정한다
            WorldEvents.Raise("SIG_FLASH", e.muzzlePos, EntityIds.Player);
            Vector2 side = new Vector2(e.dir.y, -e.dir.x) * 0.6f;
            WorldEvents.Raise("CLU_CASING", e.muzzlePos + side, EntityIds.Player);
        }

        void OnDamage(DamageEvent e)
        {
            if (e.kind != DamageKind.Bullet) return;
            WorldEvents.Raise("CLU_BLOOD", e.hitPoint, e.targetId);
        }

        void OnDeath(DeathEvent e)
        {
            if (e.isPlayer) return;
            EnemyController dead = Find(e.entityId);
            WorldEvents.Raise("SIG_ALLY_DOWN", e.position, e.entityId, e.entityId);
            WorldEvents.Raise("CLU_CORPSE", e.position, e.entityId, e.entityId);
            Vector2 offset = MathUtil.FromAngleDeg(e.rotationDeg + 90f) * ai.clue.dropped_weapon_offset_m;
            WorldEvents.Raise("CLU_WEAPON", e.position + offset, e.entityId, e.entityId);
            EventBus.Publish(new DroppedWeaponEvent { position = e.position + offset, weaponSprite = dead != null && dead.Type.id == "rifleman" ? "held_rifle" : "held_pistol" });
        }

        void OnDoorChanged(DoorChangedEvent e)
        {
            if (e.actorId != EntityIds.Player) return;
            Door door = FindDoor(e.doorId);
            if (door != null) WorldEvents.Raise("SIG_DOOR_MOVE", door.Position, e.actorId, e.doorId);
        }

        void OnLightChanged(LightChangedEvent e)
        {
            if (!e.isOn) pendingLightOff.Add(e.lightId);
        }

        readonly List<int> pendingLightOff = new List<int>();
        public Func<int, (Vector2 pos, float radius)> LightInfo { get; set; }

        // ---- 매 프레임 ----

        public void Tick(float dt)
        {
            float now = GameClock.Time;
            frame++;
            PathsThisFrame = 0;
            TickWind(now);
            DeliverRadio(now);
            Mark(0);
            ProcessEvents(now);
            ProcessLightOff(now);
            Mark(1);
            Sense(dt, now);
            Mark(2);
            RadioChecks(now);
            ResendAlerts(now);
            PlanPaths(now);
            Mark(3);
            foreach (EnemyController enemy in enemies)
            {
                if (enemy.IsDead) continue;
                enemy.IsPlayerRunning = player.Mode == MoveMode.Run && player.IsMoving;
                enemy.Tick(dt, now);
            }
            UpdateGlobalLevel();
            Mark(4);
        }

        void TickWind(float now)
        {
            if (ai.wind.period_s <= 0f) return;
            bool gust = now % ai.wind.period_s < ai.wind.gust_s && now >= ai.wind.period_s;
            if (gust && !gustActive) WorldEvents.Raise("SND_WIND", player.Position, -1, -1, 0f);
            gustActive = gust;
        }

        void ProcessEvents(float now)
        {
            if (pending.Count == 0) return;
            processing.Clear();
            processing.AddRange(pending);
            pending.Clear();
            foreach (WorldEvent e in processing)
            {
                EventTypeDef def = table.Get(e.code);
                if (def == null) continue;
                if (def.IsSound)
                {
                    if (e.code != "SND_WIND") HearSound(e, def, now);
                    continue;
                }
                switch (e.code)
                {
                    case "CLU_CASING":
                    case "CLU_BLOOD":
                        // 1m 안에 같은 종류 단서가 이미 있으면 하나로 본다(교전 중 탄피·혈흔이 수백 개 쌓이는 것을 막는다)
                        if (HasNearbyClue(e.code, e.position, 1f)) break;
                        clueObjects.Add(new ClueObject { EventId = e.eventId, Code = e.code, Position = e.position, ObjectId = e.objectId });
                        break;
                    case "CLU_CORPSE":
                    case "CLU_WEAPON":
                        clueObjects.Add(new ClueObject { EventId = e.eventId, Code = e.code, Position = e.position, ObjectId = e.objectId });
                        break;
                    case "SIG_FLASH":
                        foreach (EnemyController enemy in enemies)
                        {
                            if (enemy.IsDead) continue;
                            if (Vector2.Distance(enemy.Position, e.position) > enemy.ViewRange * 2f) continue;
                            if (!LineOfSight.IsClear(grid, enemy.Position, e.position, RayMask.Sight)) continue;
                            Perceive(enemy, e.eventId, def, e.position, 1f, 1f, true, false, now);
                        }
                        break;
                    case "SIG_ALLY_DOWN":
                        foreach (EnemyController enemy in enemies)
                        {
                            if (enemy.IsDead || enemy.Id == e.objectId) continue;
                            if (!SightSensor.CanSeeObject(grid, enemy.Position, enemy.Facing, enemy.Fov, enemy.ViewRange, e.position, 1f, 0f)) continue;
                            enemy.KnownDead.Add(e.objectId);
                            Perceive(enemy, e.eventId, def, e.position, 1f, 1f, true, false, now);
                        }
                        break;
                    case "SIG_DOOR_MOVE":
                        foreach (EnemyController enemy in enemies)
                        {
                            if (enemy.IsDead || enemy.PlayerVisible) continue;
                            Door door = FindDoor(e.objectId);
                            if (door == null) continue;
                            if (Vector2.Distance(enemy.Position, door.Position) > enemy.ViewRange) continue;
                            if (!SightSensor.InCone(enemy.Position, enemy.Facing, enemy.Fov, door.Position)) continue;
                            if (!LineOfSight.ReachesArea(grid, enemy.Position, door.Area, RayMask.Sight)) continue;
                            Perceive(enemy, e.eventId, def, door.Position, 1f, 1f, true, false, now);
                        }
                        break;
                }
            }
        }

        void HearSound(WorldEvent e, EventTypeDef def, float now)
        {
            Vector2Int sourceCell = grid.WorldToCell(e.position);
            bool masked = gustActive && grid.GetFloor(sourceCell.x, sourceCell.y) == FloorType.Outdoor;
            foreach (EnemyController enemy in enemies)
            {
                if (enemy.IsDead || enemy.Id == e.sourceId) continue;
                float strength = SoundPropagation.Intensity(grid, e.position, e.radius, enemy.Position, ai.hearing.wall_mult);
                if (masked) strength *= ai.wind.mult;
                if (strength < ai.hearing.react_threshold) continue;
                Perceive(enemy, e.eventId, def, e.position, strength, 1f, true, e.flag, now);
            }
        }

        void ProcessLightOff(float now)
        {
            if (pendingLightOff.Count == 0 || LightInfo == null) return;
            EventTypeDef def = table.Get("SIG_LIGHT_OFF");
            foreach (int lightId in pendingLightOff)
            {
                (Vector2 pos, float radius) = LightInfo(lightId);
                int eventId = WorldEvents.Raise("SIG_LIGHT_OFF", pos, EntityIds.Player, lightId);
                foreach (EnemyController enemy in enemies)
                {
                    if (enemy.IsDead) continue;
                    bool sameRoom = Vector2.Distance(enemy.Position, pos) <= radius && LineOfSight.IsClear(grid, enemy.Position, pos, RayMask.Light);
                    bool seen = SightSensor.CanSeeObject(grid, enemy.Position, enemy.Facing, enemy.Fov, enemy.ViewRange, pos, 1f, 0f);
                    if (sameRoom || seen) Perceive(enemy, eventId, def, pos, 1f, 1f, true, false, now);
                }
            }
            pendingLightOff.Clear();
        }

        // 적 한 명의 인지 1건: 의심도에 더하고, 대사용 PerceptionEvent를 낸다
        public void Perceive(EnemyController enemy, int eventId, EventTypeDef def, Vector2 position, float strength, float confidence,
            bool direct, bool flag, float now, float overrideValue = -1f, float overrideLifetime = 0f)
        {
            float score = flag && def.score_silenced > 0f ? def.score_silenced : def.score;
            float value = overrideValue >= 0f ? overrideValue : score * strength * confidence;
            float lifetime = def.lifetime_s == 0f ? overrideLifetime : def.lifetime_s;
            bool isNew = enemy.Suspicion.Add(eventId, def.code, position, value, now, lifetime, direct, def.IsSight);
            EventBus.Publish(new PerceptionEvent
            {
                enemyId = enemy.Id,
                eventId = eventId,
                code = def.code,
                direct = direct,
                position = position,
                strength = strength,
                contribution = value,
                time = now,
                flag = flag,
                isNew = isNew,
            });
            if (isNew && value > 0f) enemy.FlagNewClue(enemy.Suspicion.Find(eventId), !direct);
        }

        // 분산 감지: 적마다 phase = id % 4, frame % 4 == phase인 적만 시각 판정(5.4)
        void Sense(float dt, float now)
        {
            int phases = Mathf.Max(1, ai.perception_phase_count);
            bool playerAlive = player.Target.Alive;
            Vector2 playerPos = player.Position;
            float light = PlayerLight(playerPos, now);
            foreach (EnemyController enemy in enemies)
            {
                if (enemy.IsDead) continue;
                if (enemy.Id % phases != frame % phases) continue;
                bool seen = playerAlive && SightSensor.CanSee(grid, enemy.Position, enemy.Facing, enemy.Fov, enemy.ViewRange, playerPos, light, ai.gauge.near_dist_m, ai.light);
                float distance = Vector2.Distance(enemy.Position, playerPos);
                float mult = AwarenessGauge.RiseMultiplier(distance, player.IsMoving ? player.Mode : MoveMode.Walk, light, ai.gauge, ai.light);
                if (player.Mode == MoveMode.Crouch) mult = AwarenessGauge.RiseMultiplier(distance, MoveMode.Crouch, light, ai.gauge, ai.light);
                enemy.SetSight(seen, playerPos, mult);
                if (seen) Glimpse(enemy, playerPos, dt * phases, now);
                SeeClueObjects(enemy, now);
                SeeDoors(enemy, now);
            }
        }

        public float PlayerLight(Vector2 playerPos, float now)
        {
            if (now - LastPlayerShotTime <= ai.light.shot_flash_s) return 1f;
            return lightAt(playerPos);
        }

        // 게이지 30~100 사이로 흐릿하게 보면 움직임 포착(초당 최대 20씩 쌓임)
        void Glimpse(EnemyController enemy, Vector2 playerPos, float senseDt, float now)
        {
            if (enemy.Gauge < ai.gauge.suspect || enemy.Gauge >= ai.gauge.detect) return;
            EventTypeDef def = table.Get("SIG_GLIMPSE");
            ClueRecord record = enemy.GlimpseEventId > 0 ? enemy.Suspicion.Find(enemy.GlimpseEventId) : null;
            if (record == null)
            {
                int id = WorldEvents.Raise("SIG_GLIMPSE", playerPos, enemy.Id, enemy.Id);
                enemy.GlimpseEventId = id;
                Perceive(enemy, id, def, playerPos, 1f, 1f, true, false, now, def.score * senseDt);
                return;
            }
            enemy.Suspicion.Accumulate(record.EventId, def.score * senseDt, 100f, now, playerPos);
        }

        bool HasNearbyClue(string code, Vector2 position, float radius)
        {
            for (int i = 0; i < clueObjects.Count; i++)
                if (clueObjects[i].Code == code && (clueObjects[i].Position - position).sqrMagnitude <= radius * radius) return true;
            return false;
        }

        public int ClueObjectCount { get { return clueObjects.Count; } }

        void SeeClueObjects(EnemyController enemy, float now)
        {
            // 추격·교전 중에는 바닥을 살피지 않는다(눈앞의 침입자가 우선)
            if (enemy.State == EnemyState.Chase || enemy.State == EnemyState.Engage) return;
            for (int i = 0; i < clueObjects.Count; i++)
            {
                ClueObject o = clueObjects[i];
                if (enemy.SeenObjects.Contains(o.EventId)) continue;
                if (o.Code != "CLU_CASING" && o.ObjectId == enemy.Id) continue;
                EventTypeDef def = table.Get(o.Code);
                float range = def.range_m > 0f ? def.range_m : enemy.ViewRange;
                if (!SightSensor.CanSeeObject(grid, enemy.Position, enemy.Facing, enemy.Fov, range, o.Position, lightAt(o.Position), def.min_light)) continue;
                enemy.SeenObjects.Add(o.EventId);
                if (o.Code == "CLU_CORPSE") enemy.KnownDead.Add(o.ObjectId);
                Perceive(enemy, o.EventId, def, o.Position, 1f, 1f, true, false, now);
            }
        }

        // 마지막 개폐자가 플레이어이고 평소와 다른 문을 보면 문 상태 단서(7.21)
        void SeeDoors(EnemyController enemy, float now)
        {
            if (doors == null) return;
            foreach (Door door in doors)
            {
                if (door.LastActorId != EntityIds.Player || door.IsOpen == door.UsualOpen) continue;
                string code = door.IsOpen ? "CLU_DOOR_OPEN" : "CLU_DOOR_CLOSED";
                EventTypeDef def = table.Get(code);
                if (Vector2.Distance(enemy.Position, door.Position) > def.range_m) continue;
                if (lightAt(door.Position) < def.min_light) continue;
                if (!SightSensor.InCone(enemy.Position, enemy.Facing, enemy.Fov, door.Position)) continue;
                if (!LineOfSight.ReachesArea(grid, enemy.Position, door.Area, RayMask.Sight)) continue;
                if (!doorChangeEvents.TryGetValue(door.Id, out var change) || change.time != door.LastChangedTime)
                {
                    change = (door.LastChangedTime, WorldEvents.Raise(code, door.Position, EntityIds.Player, door.Id));
                    doorChangeEvents[door.Id] = change;
                }
                if (!enemy.SeenObjects.Add(change.eventId)) continue;
                Perceive(enemy, change.eventId, def, door.Position, 1f, 1f, true, false, now);
            }
        }

        // ---- 발견·경보(7.8) ----

        public void OnDetected(EnemyController enemy)
        {
            float now = GameClock.Time;
            EventBus.Publish(new DetectionEvent { enemyId = enemy.Id, time = now });
            EventTypeDef def = table.Get("SIG_INTRUDER");
            int id = WorldEvents.Raise("SIG_INTRUDER", enemy.LastKnownPlayer, enemy.Id, enemy.Id);
            Perceive(enemy, id, def, enemy.LastKnownPlayer, 1f, 1f, true, false, now);
            alertsSent[enemy.Id] = 0;
            lastAlert[enemy.Id] = float.NegativeInfinity;
            SendAlert(enemy, now, id);
        }

        void ResendAlerts(float now)
        {
            foreach (EnemyController enemy in enemies)
            {
                if (enemy.IsDead || enemy.Gauge < ai.gauge.detect) continue;
                if (!alertsSent.TryGetValue(enemy.Id, out int sent) || sent >= ai.alert.max_per_detection) continue;
                if (now - lastAlert[enemy.Id] < ai.alert.cooldown_s) continue;
                SendAlert(enemy, now, -1);
            }
        }

        void SendAlert(EnemyController sender, float now, int sourceEventId)
        {
            if (alertsSent[sender.Id] >= ai.alert.max_per_detection || now - lastAlert[sender.Id] < ai.alert.cooldown_s) return;
            alertsSent[sender.Id]++;
            lastAlert[sender.Id] = now;
            bool shout = sender.Type.alert != "radio";
            EventBus.Publish(new AlertEvent { kind = shout ? AlertKind.Shout : AlertKind.Radio, position = sender.LastKnownPlayer, time = now, senderId = sender.Id });
            EventTypeDef intruder = table.Get("SIG_INTRUDER");
            int radioId = WorldEvents.Raise("CLU_RADIO_HELP", sender.LastKnownPlayer, sender.Id, sourceEventId);
            foreach (EnemyController receiver in enemies)
            {
                if (receiver == sender || receiver.IsDead) continue;
                if (shout && Vector2.Distance(receiver.Position, sender.Position) > ai.alert.shout_radius_m) continue;
                radio.Add(new RadioDelivery
                {
                    At = now + ai.alert.radio_delay_s,
                    ReceiverId = receiver.Id,
                    EventId = radioId,
                    Position = sender.LastKnownPlayer,
                    Value = intruder.score * ai.suspicion.radio_confidence,
                    Lifetime = intruder.lifetime_s,
                });
            }
        }

        void DeliverRadio(float now)
        {
            if (radio.Count == 0) return;
            EventTypeDef def = table.Get("CLU_RADIO_HELP");
            for (int i = radio.Count - 1; i >= 0; i--)
            {
                RadioDelivery d = radio[i];
                if (now < d.At) continue;
                radio.RemoveAt(i);
                EnemyController receiver = Find(d.ReceiverId);
                if (receiver == null || receiver.IsDead) continue;
                Perceive(receiver, d.EventId, def, d.Position, 1f, ai.suspicion.radio_confidence, false, false, now, d.Value, d.Lifetime);
            }
        }

        // 소총병이 45초마다 무전 점검: 모르는 죽은 동료가 있으면 무응답(S)
        void RadioChecks(float now)
        {
            EventTypeDef def = table.Get("CLU_NO_ANSWER");
            foreach (EnemyController rifleman in enemies)
            {
                if (rifleman.IsDead || rifleman.Type.alert != "radio" || now < rifleman.NextRadioCheck) continue;
                rifleman.NextRadioCheck = now + ai.clue.radio_check_s;
                foreach (EnemyController ally in enemies)
                {
                    if (ally == rifleman || !ally.IsDead || rifleman.KnownDead.Contains(ally.Id)) continue;
                    rifleman.KnownDead.Add(ally.Id);
                    int id = WorldEvents.Raise("CLU_NO_ANSWER", ally.SpawnPosition, rifleman.Id, ally.Id);
                    Perceive(rifleman, id, def, ally.SpawnPosition, 1f, 1f, false, false, now);
                    break;
                }
            }
        }

        // ---- 경로 예산(프레임당 최대 2건) ----

        public void RequestPath(EnemyController enemy) { }

        void PlanPaths(float now)
        {
            foreach (EnemyController enemy in enemies)
                if (!enemy.IsDead && enemy.NeedsPath(now) && queued.Add(enemy.Id)) pathQueue.Enqueue(enemy);
            int budget = Mathf.Max(1, ai.nav.max_paths_per_frame);
            while (budget > 0 && pathQueue.Count > 0)
            {
                EnemyController enemy = pathQueue.Dequeue();
                queued.Remove(enemy.Id);
                if (enemy.IsDead || !enemy.NeedsPath(now)) continue;
                enemy.PlanPath(now);
                PathsThisFrame++;
                budget--;
            }
        }

        public bool TryOpenDoorAt(Vector2Int cell, int actorId)
        {
            if (doors == null) return false;
            foreach (Door door in doors)
            {
                if (!door.Area.Contains(cell)) continue;
                if (door.IsOpen) return true;
                door.Interact(actorId);
                return true;
            }
            return false;
        }

        public void PublishState(EnemyController enemy, EnemyState from, EnemyState to, string reason)
        {
            EventBus.Publish(new EnemyStateChangedEvent { enemyId = enemy.Id, from = from, to = to, reason = reason, time = GameClock.Time });
        }

        void UpdateGlobalLevel()
        {
            int level = 0;
            foreach (EnemyController enemy in enemies)
            {
                if (enemy.IsDead) continue;
                if (enemy.State == EnemyState.Chase || enemy.State == EnemyState.Engage) { level = 2; break; }
                if (enemy.State == EnemyState.Investigate || enemy.State == EnemyState.Search) level = 1;
            }
            if (level == globalLevel) return;
            globalLevel = level;
            EventBus.Publish(new GlobalAlertChangedEvent { level = level });
        }

        EnemyController Find(int id)
        {
            foreach (EnemyController enemy in enemies)
                if (enemy.Id == id) return enemy;
            return null;
        }

        Door FindDoor(int id)
        {
            if (doors == null) return null;
            foreach (Door door in doors)
                if (door.Id == id) return door;
            return null;
        }

        public bool AllPatrolling()
        {
            foreach (EnemyController enemy in enemies)
                if (!enemy.IsDead && enemy.State != EnemyState.Patrol) return false;
            return true;
        }
    }
}
