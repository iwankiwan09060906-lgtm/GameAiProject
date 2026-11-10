using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameAi
{
    // 대사 계기 1건(인지·상태 전환·피격·재장전)
    public sealed class BarkTrigger
    {
        public int EnemyId;
        public string When;
        public int EventId = -1;
        public bool Instant;
        public bool Chatter;
        public bool Direct = true;
        public Vector2 Position;
        public float Time;
        public float Score;
        public string DefaultEmotion = "의심";
        public bool Flag;
    }

    // 대사를 고르고 표시한다(7.18). 상황 대사(Qwen, 7.22)는 lineSource가 맡고, 없거나 실패하면 기본 대사.
    // 대사는 AI 상태를 읽기만 하고 아무것도 바꾸지 않는다.
    public sealed class BarkPicker : MonoBehaviour
    {
        public const int HistoryPerEnemy = 5;
        public const int HistoryGlobal = 20;

        readonly Dictionary<int, BarkTrigger> pending = new Dictionary<int, BarkTrigger>();
        readonly Dictionary<int, float> lastSpoke = new Dictionary<int, float>();
        readonly Dictionary<int, List<string>> recentByEnemy = new Dictionary<int, List<string>>();
        readonly Dictionary<(int, int, string), int> repeats = new Dictionary<(int, int, string), int>();
        readonly List<int> ready = new List<int>();

        AiDirector director;
        BarkConfig config;
        EventTypeTable table;
        BarkTimingConfig timing;
        BarkView view;
        Func<int, bool> isVisible;
        Func<Vector2> cameraCenter;
        float mergeWindow = 0.5f;
        float screenCenterM = 6f;

        // 전체 최근 대사(들은 대사 기록·반복 방지)
        public readonly List<(int enemyId, string text, string emotion, string source)> History = new List<(int, string, string, string)>();
        public int Spoken { get; private set; }
        public IBarkLineSource LineSource { get; set; }
        // 잡담·대구(C)
        public Func<Vector2, bool> IsOutdoor { get; set; } = p => false;
        public float ChatterInterval { get; set; } = 60f;
        public float ReplyDelay { get; set; } = 1f;
        public float ReplyRadius { get; set; } = 10f;
        readonly Dictionary<int, float> nextChatter = new Dictionary<int, float>();
        readonly List<(float at, int enemyId)> replies = new List<(float, int)>();

        public void Init(AiDirector aiDirector, BarkConfig barkConfig, EventTypeTable eventTable, BarkTimingConfig barkTiming, BarkView barkView,
            Func<int, bool> visible, Func<Vector2> center, float merge, float centerM)
        {
            director = aiDirector;
            config = barkConfig;
            table = eventTable;
            timing = barkTiming;
            view = barkView;
            isVisible = visible;
            cameraCenter = center;
            mergeWindow = merge;
            screenCenterM = centerM;
        }

        void OnEnable()
        {
            EventBus.Subscribe<PerceptionEvent>(OnPerception);
            EventBus.Subscribe<EnemyStateChangedEvent>(OnStateChanged);
            EventBus.Subscribe<DamageEvent>(OnDamage);
            EventBus.Subscribe<EnemyActionEvent>(OnAction);
            EventBus.Subscribe<WorldEvent>(OnWorldEvent);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<PerceptionEvent>(OnPerception);
            EventBus.Unsubscribe<EnemyStateChangedEvent>(OnStateChanged);
            EventBus.Unsubscribe<DamageEvent>(OnDamage);
            EventBus.Unsubscribe<EnemyActionEvent>(OnAction);
            EventBus.Unsubscribe<WorldEvent>(OnWorldEvent);
        }

        EnemyController Enemy(int id)
        {
            foreach (EnemyController e in director.Enemies)
                if (e.Id == id) return e;
            return null;
        }

        void OnPerception(PerceptionEvent e)
        {
            if (!e.isNew) return;
            EventTypeDef def = table.Get(e.code);
            if (def == null) return;
            string mode = e.flag && !string.IsNullOrEmpty(def.bark_silenced) ? def.bark_silenced : def.bark;
            Offer(new BarkTrigger
            {
                EnemyId = e.enemyId,
                When = e.code,
                EventId = e.eventId,
                Instant = mode == "instant",
                Chatter = mode == "chatter",
                Direct = e.direct,
                Position = e.position,
                Time = e.time,
                DefaultEmotion = def.default_emotion,
                Flag = e.flag,
            }, def.priority);
        }

        void OnStateChanged(EnemyStateChangedEvent e)
        {
            string when = null;
            if (e.to == EnemyState.Search && (e.from == EnemyState.Chase || e.from == EnemyState.Engage)) when = "lost";
            else if (e.to == EnemyState.Patrol && e.from == EnemyState.Search) when = "search_end";
            if (when == null) return;
            Offer(new BarkTrigger { EnemyId = e.enemyId, When = when, Time = e.time, DefaultEmotion = when == "lost" ? "긴장" : "안도" }, 2);
        }

        void OnDamage(DamageEvent e)
        {
            EnemyController enemy = Enemy(e.targetId);
            if (enemy == null || enemy.Health.IsDead) return;
            Offer(new BarkTrigger { EnemyId = e.targetId, When = "hit", Instant = true, Time = GameClock.Time, DefaultEmotion = "분노" }, 4);
        }

        void OnAction(EnemyActionEvent e)
        {
            if (e.action == "reload")
                Offer(new BarkTrigger { EnemyId = e.enemyId, When = "reload", Time = e.time, DefaultEmotion = "긴장" }, 2);
        }

        // 돌풍이 불면 야외에 있는 적 한 명이 잡담(점수 0, 단서 아님)
        void OnWorldEvent(WorldEvent e)
        {
            if (e.code != "SND_WIND") return;
            foreach (EnemyController enemy in director.Enemies)
            {
                if (enemy.IsDead || !isVisible(enemy.Id) || !IsOutdoor(enemy.Position)) continue;
                Offer(new BarkTrigger { EnemyId = enemy.Id, When = "SND_WIND", Chatter = true, Time = e.time, Position = enemy.Position, DefaultEmotion = "안도" }, 1);
                break;
            }
        }

        // 우선순위 = priority×10 + 긴장도×3 + 화면 중앙 6m 안 5 − 같은 추적에서 같은 코드 발화 수×5
        public static float Score(int priority, Tension tension, bool nearCenter, int repeatsInEpisode)
        {
            return priority * 10f + (int)tension * 3f + (nearCenter ? 5f : 0f) - repeatsInEpisode * 5f;
        }

        void Offer(BarkTrigger trigger, int priority)
        {
            EnemyController enemy = Enemy(trigger.EnemyId);
            if (enemy == null || enemy.IsDead) return;
            bool nearCenter = Vector2.Distance(enemy.Position, cameraCenter()) <= screenCenterM;
            repeats.TryGetValue((enemy.Id, enemy.EpisodeId, trigger.When), out int count);
            trigger.Score = Score(priority, enemy.Tension, nearCenter, count);
            // 0.5초 안에 여러 계기가 오면 점수가 가장 높은 하나로 합친다
            if (pending.TryGetValue(trigger.EnemyId, out BarkTrigger existing))
            {
                if (trigger.Score <= existing.Score && !trigger.Instant) return;
                trigger.Time = Mathf.Min(trigger.Time, existing.Time);
            }
            pending[trigger.EnemyId] = trigger;
        }

        // 5.4의 9단계(화면 갱신)에서 부른다
        public void Tick(float dt)
        {
            float now = GameClock.Time;
            ready.Clear();
            foreach (var pair in pending)
                if (pair.Value.Instant || now - pair.Value.Time >= mergeWindow) ready.Add(pair.Key);
            foreach (int id in ready)
            {
                BarkTrigger trigger = pending[id];
                pending.Remove(id);
                Resolve(trigger, now);
            }
            TickChatter(now);
            LineSource?.Tick(dt);
        }

        // 초소 대기·순찰 중 60초마다 낮은 우선순위로 혼잣말(C). 잠입 전 적 위치를 귀로 찾는 힌트
        void TickChatter(float now)
        {
            foreach (EnemyController enemy in director.Enemies)
            {
                if (enemy.IsDead) continue;
                if (!nextChatter.TryGetValue(enemy.Id, out float at))
                {
                    nextChatter[enemy.Id] = now + ChatterInterval * 0.5f + enemy.Id * 7f;
                    continue;
                }
                if (now < at) continue;
                nextChatter[enemy.Id] = now + ChatterInterval;
                if (enemy.State != EnemyState.Patrol || !isVisible(enemy.Id)) continue;
                Offer(new BarkTrigger { EnemyId = enemy.Id, When = "IDLE_CHATTER", Chatter = true, Time = now, Position = enemy.Position, DefaultEmotion = "안도" }, 1);
            }
            // 동료 대구: 앞 대사 1초 뒤 가까운 동료가 짧게 답한다(분기 없음)
            for (int i = replies.Count - 1; i >= 0; i--)
            {
                if (now < replies[i].at) continue;
                EnemyController enemy = Enemy(replies[i].enemyId);
                replies.RemoveAt(i);
                if (enemy == null || enemy.IsDead) continue;
                string line = BarkFallback.Pick(config, "reply", SuspicionMeter.TensionKo(enemy.Tension), enemy.Persona, Recent(enemy.Id), Rng.Fx, out string emotion);
                if (line != null) Say(enemy, new BarkTrigger { EnemyId = enemy.Id, When = "reply", Time = now }, line, string.IsNullOrEmpty(emotion) ? "의심" : emotion, "Reply");
            }
        }

        void ScheduleReply(EnemyController speaker, float now)
        {
            EnemyController best = null;
            float bestD = ReplyRadius;
            foreach (EnemyController other in director.Enemies)
            {
                if (other == speaker || other.IsDead || !isVisible(other.Id)) continue;
                float d = Vector2.Distance(other.Position, speaker.Position);
                if (d <= bestD) { bestD = d; best = other; }
            }
            if (best != null) replies.Add((now + ReplyDelay, best.Id));
        }

        void Resolve(BarkTrigger trigger, float now)
        {
            EnemyController enemy = Enemy(trigger.EnemyId);
            if (enemy == null || enemy.IsDead) return;
            if (!isVisible(enemy.Id)) return;
            if (lastSpoke.TryGetValue(enemy.Id, out float last) && now - last < timing.cooldown_s) return;
            if (!trigger.Instant && LineSource != null && LineSource.TryRequest(trigger, enemy)) return;
            SayFallback(trigger, enemy);
        }

        // 기본 대사로 바로 말한다(즉시 이벤트, Qwen 꺼짐·실패·지연)
        public void SayFallback(BarkTrigger trigger, EnemyController enemy)
        {
            string line = BarkFallback.Pick(config, trigger.When, SuspicionMeter.TensionKo(enemy.Tension), enemy.Persona, Recent(enemy.Id), Rng.Fx, out string emotion);
            if (line == null) return;
            Say(enemy, trigger, line, string.IsNullOrEmpty(emotion) ? trigger.DefaultEmotion : emotion, "Fallback");
        }

        // Qwen 결과도 이 함수로 표시한다. 3초 간격·생존·화면 표시를 다시 확인한다
        public bool Say(EnemyController enemy, BarkTrigger trigger, string line, string emotion, string source)
        {
            float now = GameClock.Time;
            if (enemy.IsDead || !isVisible(enemy.Id)) return false;
            if (lastSpoke.TryGetValue(enemy.Id, out float last) && now - last < timing.cooldown_s) return false;
            lastSpoke[enemy.Id] = now;
            var key = (enemy.Id, enemy.EpisodeId, trigger.When);
            repeats.TryGetValue(key, out int count);
            repeats[key] = count + 1;
            List<string> recent = Recent(enemy.Id);
            recent.Add(line);
            if (recent.Count > HistoryPerEnemy) recent.RemoveAt(0);
            History.Add((enemy.Id, line, emotion, source));
            if (History.Count > HistoryGlobal) History.RemoveAt(0);
            Spoken++;
            view.Say(enemy.Id, line, emotion);
            EventBus.Publish(new BarkEvent { enemyId = enemy.Id, text = line, emotion = emotion, source = source });
            if (source != "Reply" && !trigger.Instant && (trigger.Chatter || trigger.When.StartsWith("SND_", StringComparison.Ordinal))) ScheduleReply(enemy, now);
            return true;
        }

        public List<string> Recent(int enemyId)
        {
            if (!recentByEnemy.TryGetValue(enemyId, out List<string> list))
            {
                list = new List<string>();
                recentByEnemy[enemyId] = list;
            }
            return list;
        }

        public int RepeatCount(EnemyController enemy, string when)
        {
            repeats.TryGetValue((enemy.Id, enemy.EpisodeId, when), out int count);
            return count;
        }

        public float LastSpoke(int enemyId)
        {
            return lastSpoke.TryGetValue(enemyId, out float t) ? t : float.NegativeInfinity;
        }
    }

    // 상황 대사(Qwen) 연결 자리. 요청을 받았으면 true(결과는 나중에 BarkPicker.Say로 표시)
    public interface IBarkLineSource
    {
        bool TryRequest(BarkTrigger trigger, EnemyController enemy);
        void Tick(float dt);
    }
}
