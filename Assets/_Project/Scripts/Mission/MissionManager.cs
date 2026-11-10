using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameAi
{
    // 목표 지점(T): Space로 자료 확보(탈취) 또는 누르고 있어 폭탄 해체
    public sealed class ObjectiveItem : IInteractable
    {
        readonly GridMap grid;
        readonly bool bomb;

        public bool Done { get; private set; }
        public Vector2 Position { get; }
        public RectInt Area { get; }
        public string PromptAction { get { return bomb ? "해체" : "자료 확보"; } }
        public bool CanInteract { get { return !Done; } }
        public bool IsBomb { get { return bomb; } }
        public SpriteRenderer Renderer { get; set; }

        public ObjectiveItem(GridMap grid, Vector2 position, bool bomb)
        {
            this.grid = grid;
            this.bomb = bomb;
            Position = position;
            Vector2Int cell = grid.WorldToCell(position);
            Area = new RectInt(cell.x, cell.y, 1, 1);
        }

        // 탈취는 누르는 순간 완료. 폭탄은 MissionManager가 누르고 있는 시간을 잰다
        public void Interact(int actorId)
        {
            if (bomb || Done) return;
            Complete();
        }

        public void Complete()
        {
            Done = true;
            if (Renderer != null) Renderer.enabled = false;
        }
    }

    // 미션 진행(준비 → 브리핑 → 진행 → 종료 → 디브리핑 → 결과). 프레임당 한 번만 승패를 판정한다(7.9)
    public sealed class MissionManager : MonoBehaviour
    {
        public const string Prep = "prep";
        public const string Briefing = "briefing";
        public const string Running = "running";
        public const string Ended = "ended";
        public const string Debrief = "debrief";
        public const string Result = "result";

        readonly DetectionCounter detections = new DetectionCounter();
        string mapId;
        string mission;
        int totalEnemies;
        float timeLimit;
        float defuseSeconds;
        bool keepProgress;
        float startTime;
        float dialogueSeconds;
        bool deathThisFrame;
        bool resolved;
        Func<bool> allPatrolling = () => true;
        Func<string, bool> playDialogue = id => false;
        List<Vector2> exits = new List<Vector2>();
        PlayerController player;

        public ObjectiveItem Objective { get; private set; }
        public string State { get; private set; } = Prep;
        public string MissionType { get { return mission; } }
        public int Kills { get; private set; }
        public int Detections { get { return detections.Count; } }
        public int TotalEnemies { get { return totalEnemies; } }
        public bool Stolen { get; private set; }
        public float DefuseProgress { get; private set; }
        public MissionResult Outcome { get; private set; }
        public float ClearTime { get; private set; }
        public float Elapsed { get { return State == Running ? GameClock.Time - startTime : ClearTime; } }
        public float TimeLeft { get { return timeLimit > 0f ? Mathf.Max(0f, timeLimit - Elapsed) : 0f; } }
        public float TimeLimit { get { return timeLimit; } }
        public float TargetTime { get; private set; }
        public bool InputBlocked { get { return State != Running; } }

        public void Init(string id, string missionType, float targetTime, int enemyCount, PlayerController playerController,
            ObjectiveItem objective, List<Vector2> exitPoints, BombConfig bomb, Func<bool> patrolCheck, Func<string, bool> dialogue)
        {
            mapId = id;
            mission = missionType;
            TargetTime = targetTime;
            totalEnemies = enemyCount;
            player = playerController;
            Objective = objective;
            exits = exitPoints ?? exits;
            timeLimit = mission == "bomb" ? bomb.time_limit_s : 0f;
            defuseSeconds = bomb.defuse_s;
            keepProgress = bomb.keep_progress;
            allPatrolling = patrolCheck ?? allPatrolling;
            playDialogue = dialogue ?? playDialogue;
        }

        void OnEnable()
        {
            EventBus.Subscribe<DeathEvent>(OnDeath);
            EventBus.Subscribe<DetectionEvent>(OnDetection);
            EventBus.Subscribe<DialogueStateEvent>(OnDialogue);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<DeathEvent>(OnDeath);
            EventBus.Unsubscribe<DetectionEvent>(OnDetection);
            EventBus.Unsubscribe<DialogueStateEvent>(OnDialogue);
        }

        // 세계를 다 만든 뒤 한 번 부른다
        public void Begin()
        {
            SetState(Briefing);
            if (!playDialogue(mapId + "_briefing")) StartRunning();
        }

        void StartRunning()
        {
            startTime = GameClock.Time;
            SetState(Running);
        }

        void OnDialogue(DialogueStateEvent e)
        {
            if (e.isActive) return;
            if (State == Briefing) StartRunning();
            else if (State == Debrief) SetState(Result);
        }

        void OnDeath(DeathEvent e)
        {
            if (State != Running) return;
            if (e.isPlayer) deathThisFrame = true;
            else Kills++;
        }

        void OnDetection(DetectionEvent e)
        {
            if (State == Running) detections.OnDetection();
        }

        // 5.4의 8단계. interactHeld는 폭탄 해체용
        public void Tick(float dt, bool interactHeld, bool nearObjective)
        {
            if (State != Running) return;
            detections.Tick(allPatrolling());
            if (Objective != null && Objective.IsBomb && !Objective.Done)
            {
                if (interactHeld && nearObjective) DefuseProgress += dt / Mathf.Max(0.01f, defuseSeconds);
                else if (!keepProgress) DefuseProgress = 0f;
                if (DefuseProgress >= 1f) Objective.Complete();
            }
            if (Objective != null && Objective.Done && !Stolen)
            {
                Stolen = true;
                if (!Objective.IsBomb) EventBus.Publish(new MissionStateEvent { state = "objective", objectiveProgress = 1f, timeLeft = TimeLeft, result = "" });
            }

            bool objectiveDone;
            switch (mission)
            {
                case "steal": objectiveDone = Stolen; break;
                case "steal_exit": objectiveDone = Stolen && AtExit(); break;
                case "bomb": objectiveDone = Objective != null && Objective.Done; break;
                default: objectiveDone = totalEnemies > 0 && Kills >= totalEnemies; break;
            }
            bool timeUp = timeLimit > 0f && Elapsed >= timeLimit;
            MissionResult result = ResultEvaluator.Resolve(objectiveDone, timeUp, deathThisFrame || !player.Target.Alive);
            deathThisFrame = false;
            if (result != MissionResult.None) End(result);
        }

        bool AtExit()
        {
            foreach (Vector2 exit in exits)
                if (Vector2.Distance(player.Position, exit) <= 1f) return true;
            return false;
        }

        void End(MissionResult result)
        {
            if (resolved) return;
            resolved = true;
            Outcome = result;
            ClearTime = RecordRules.Round3(GameClock.Time - startTime);
            SetState(Ended);
            SetState(Debrief);
            string id = mapId + (result == MissionResult.Success ? "_debrief_success" : "_debrief_fail");
            if (!playDialogue(id)) SetState(Result);
        }

        public MissionRecord MakeRecord(string difficulty)
        {
            return new MissionRecord { map = mapId, difficulty = difficulty, clear_time_s = ClearTime, detections = Detections, kills = Kills };
        }

        public float Progress
        {
            get
            {
                switch (mission)
                {
                    case "steal": return Stolen ? 1f : 0f;
                    case "steal_exit": return Stolen ? 0.5f : 0f;
                    case "bomb": return DefuseProgress;
                    default: return totalEnemies > 0 ? (float)Kills / totalEnemies : 0f;
                }
            }
        }

        void SetState(string state)
        {
            State = state;
            EventBus.Publish(new MissionStateEvent
            {
                state = state,
                objectiveProgress = Progress,
                timeLeft = TimeLeft,
                result = Outcome == MissionResult.Success ? "success" : Outcome == MissionResult.Fail ? "fail" : "",
            });
        }

        // HUD 좌상단 목표 문구
        public string ObjectiveText
        {
            get
            {
                switch (mission)
                {
                    case "steal": return Stolen ? "자료 확보 완료" : "자료를 확보하라";
                    case "steal_exit": return Stolen ? "탈출 지점으로 이동하라" : "자료를 확보하라";
                    case "bomb": return Objective != null && Objective.Done ? "해체 완료" : "폭탄을 해체하라 (" + Mathf.RoundToInt(DefuseProgress * 100f) + "%)";
                    default: return "적을 모두 처치하라 (" + Kills + "/" + totalEnemies + ")";
                }
            }
        }
    }
}
