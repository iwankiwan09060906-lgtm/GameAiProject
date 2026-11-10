using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace GameAi
{
    // Qwen 요청 1건(bark_request)
    public sealed class BarkRequest
    {
        public int Id;
        public BarkTrigger Trigger;
        public EnemyController Enemy;
        public BarkContext Context;
        public string CacheKey;
        public float Score;
        public int EpisodeId;
        public int ActionId;
        public string Task;
        public float SentAt;
        public float QueuedAt;
        public bool FellBack;
        public bool Warmup;
        public UnityWebRequest Web;
        public string State = "queued";
    }

    // qwen_log 1건
    public struct QwenLogEntry
    {
        public int RequestId;
        public int EnemyId;
        public string Code;
        public float LatencyMs;
        public string Input;
        public string Raw;
        public string Line;
        public string Emotion;
        public string Result;
    }

    // 상황 대사(7.22): 같은 PC의 OpenAI 호환 로컬 서버(Ollama/llama-server)에 비동기 요청한다.
    // 동시 1건·대기열 4건, 실제 시간 2초 한도, 검증 실패·지연·서버 없음이면 기본 대사. 출력은 대사에만 쓴다
    public sealed class QwenBarkSource : IBarkLineSource
    {
        [Serializable] class ChatResponse { public Choice[] choices; }
        [Serializable] class Choice { public Message message; }
        [Serializable] class Message { public string content; }
        [Serializable] class BarkOutput { public string line; public string emotion; }

        readonly QwenConfig config;
        readonly BarkPicker picker;
        readonly BarkConfig barks;
        readonly EventTypeTable table;
        readonly Func<EnemyController, BarkTrigger, BarkContext> buildContext;
        readonly Func<Vector2> cameraCenter;
        readonly List<BarkRequest> queue = new List<BarkRequest>();
        readonly List<BarkRequest> inFlight = new List<BarkRequest>();
        readonly List<BarkRequest> held = new List<BarkRequest>();
        readonly Dictionary<string, List<(string line, string emotion)>> cache = new Dictionary<string, List<(string, string)>>();
        readonly Dictionary<int, int> eventOwner = new Dictionary<int, int>();
        int nextId;
        int consecutiveFailures;
        bool offlineWarned;
        bool paused;

        public bool Enabled { get; private set; }
        public bool Offline { get; private set; }
        public bool Stopped { get; private set; }
        public readonly List<QwenLogEntry> Log = new List<QwenLogEntry>();
        public int Requests { get; private set; }
        public int Successes { get; private set; }
        public int CacheHits { get; private set; }
        public readonly Dictionary<string, int> FailCounts = new Dictionary<string, int>();
        public int QueueCount { get { return queue.Count; } }
        public float AverageLatencyMs
        {
            get
            {
                float sum = 0f; int n = 0;
                foreach (QwenLogEntry e in Log) if (e.Result == "OK") { sum += e.LatencyMs; n++; }
                return n > 0 ? sum / n : 0f;
            }
        }
        public string Status { get { return !Enabled ? "꺼짐" : Offline ? "서버 없음" : Stopped ? "중지(연속 실패)" : "연결"; } }

        public QwenBarkSource(QwenConfig qwen, BarkPicker barkPicker, BarkConfig barkConfig, EventTypeTable eventTable,
            Func<EnemyController, BarkTrigger, BarkContext> contextBuilder, Func<Vector2> center, bool fixedSeed)
        {
            config = qwen;
            picker = barkPicker;
            barks = barkConfig;
            table = eventTable;
            buildContext = contextBuilder;
            cameraCenter = center;
            Enabled = config.enabled && !fixedSeed;
            if (config.enabled && !config.IsLocal)
            {
                Debug.LogWarning("[Qwen] endpoint가 로컬 주소가 아니라 끕니다: " + config.endpoint);
                Enabled = false;
            }
            if (Enabled && config.warmup_on_start) SendWarmup();
        }

        public void SetPaused(bool value) { paused = value; }

        // 메인 메뉴·게임 시작 때 모델을 미리 불러 둔다(첫 호출은 수십 초 걸림). 응답은 버린다
        void SendWarmup()
        {
            var request = new BarkRequest { Id = ++nextId, Warmup = true, SentAt = Time.realtimeSinceStartup };
            request.Web = Post("너는 게임 속 경비병이다.", "{\"사건\":\"준비\"}", 4);
            request.State = "sent";
            inFlight.Add(request);
        }

        public bool TryRequest(BarkTrigger trigger, EnemyController enemy)
        {
            if (!Enabled || Offline || Stopped) return false;
            BarkContext context = buildContext(enemy, trigger);
            string key = context.CacheKey(trigger.When);

            // 캐시에 통과 대사가 있으면 바로 쓴다
            if (cache.TryGetValue(key, out var lines))
            {
                List<string> recent = picker.Recent(enemy.Id);
                foreach (var (line, emotion) in lines)
                {
                    if (recent.Contains(line)) continue;
                    CacheHits++;
                    return picker.Say(enemy, trigger, line, emotion, "Cache");
                }
            }

            // 잡담은 대기열이 비었을 때만
            if (trigger.Chatter && (queue.Count > 0 || inFlight.Count > 0)) return true;

            // 같은 사건에 여러 적이 반응하면 화면 중앙에 가장 가까운 한 명만 Qwen
            if (trigger.EventId > 0 && eventOwner.TryGetValue(trigger.EventId, out int ownerId) && ownerId != enemy.Id)
            {
                BarkRequest owner = queue.Find(r => r.Enemy.Id == ownerId && r.Trigger.EventId == trigger.EventId);
                if (owner == null) return false;
                float mine = Vector2.Distance(enemy.Position, cameraCenter());
                float theirs = Vector2.Distance(owner.Enemy.Position, cameraCenter());
                if (mine >= theirs) return false;
                queue.Remove(owner);
                FallBack(owner, "CANCELLED");
            }
            if (trigger.EventId > 0) eventOwner[trigger.EventId] = enemy.Id;

            // 적 1명당 진행 중 요청 1건: 새 요청이 이전 요청을 무효화
            for (int i = queue.Count - 1; i >= 0; i--)
                if (queue[i].Enemy.Id == enemy.Id) { queue.RemoveAt(i); }
            foreach (BarkRequest old in inFlight)
                if (old.Enemy != null && old.Enemy.Id == enemy.Id) old.State = "cancelled";

            var request = new BarkRequest
            {
                Id = ++nextId,
                Trigger = trigger,
                Enemy = enemy,
                Context = context,
                CacheKey = key,
                Score = trigger.Score,
                EpisodeId = enemy.EpisodeId,
                ActionId = enemy.ActionId,
                Task = context.Task,
                QueuedAt = Time.realtimeSinceStartup,
            };
            queue.Add(request);
            // 넘치면 우선순위가 낮은 것부터 기본 대사
            while (queue.Count > config.queue_max)
            {
                BarkRequest lowest = queue[0];
                foreach (BarkRequest r in queue) if (r.Score < lowest.Score) lowest = r;
                queue.Remove(lowest);
                FallBack(lowest, "DROPPED");
            }
            Requests++;
            return true;
        }

        void FallBack(BarkRequest request, string reason)
        {
            if (request.FellBack || request.Enemy == null) return;
            request.FellBack = true;
            if (!request.Enemy.IsDead) picker.SayFallback(request.Trigger, request.Enemy);
        }

        public void Tick(float dt)
        {
            // 보낼 수 있으면 우선순위가 가장 높은 것부터
            while (Enabled && !Offline && !Stopped && queue.Count > 0 && ActiveCount() < Mathf.Max(1, config.max_concurrent))
            {
                BarkRequest best = queue[0];
                foreach (BarkRequest r in queue) if (r.Score > best.Score) best = r;
                queue.Remove(best);
                if (best.Enemy.IsDead) continue;
                best.SentAt = Time.realtimeSinceStartup;
                best.Web = Post(SystemPrompt(best.Context), best.Context.ToJson(), config.max_tokens);
                best.State = "sent";
                inFlight.Add(best);
            }

            float now = Time.realtimeSinceStartup;
            // 보내지도 못하고 2초가 지난 요청은 기본 대사
            for (int i = queue.Count - 1; i >= 0; i--)
            {
                if (now - queue[i].QueuedAt <= config.timeout_s) continue;
                BarkRequest late = queue[i];
                queue.RemoveAt(i);
                Count("TIMEOUT");
                FallBack(late, "TIMEOUT");
            }
            for (int i = inFlight.Count - 1; i >= 0; i--)
            {
                BarkRequest r = inFlight[i];
                float waited = now - r.SentAt;
                if (!r.Warmup && !r.FellBack && waited > config.timeout_s && r.State == "sent")
                {
                    // 2초 초과: 기본 대사. 늦은 응답은 검증을 통과하면 캐시에만 넣는다
                    AddLog(r, waited, "", "", "", "TIMEOUT");
                    Count("TIMEOUT");
                    FallBack(r, "TIMEOUT");
                }
                if (r.Web == null || !r.Web.isDone) continue;
                inFlight.RemoveAt(i);
                Finish(r, waited);
            }

            if (!paused)
            {
                foreach (BarkRequest r in held) Deliver(r);
                held.Clear();
            }
        }

        int ActiveCount()
        {
            int n = 0;
            // 예열 요청은 세지 않는다(모델을 불러오는 동안에도 요청은 보내고 2초 뒤 기본 대사로 넘어간다)
            foreach (BarkRequest r in inFlight) if (!r.Warmup) n++;
            return n;
        }

        void Finish(BarkRequest r, float waited)
        {
            UnityWebRequest web = r.Web;
            bool ok = web.result == UnityWebRequest.Result.Success;
            string body = ok ? web.downloadHandler.text : "";
            web.Dispose();
            r.Web = null;
            if (!ok)
            {
                if (!offlineWarned)
                {
                    offlineWarned = true;
                    Debug.LogWarning("[Qwen] 서버에 연결할 수 없어 기본 대사만 씁니다: " + config.endpoint);
                }
                Offline = true;
                if (!r.Warmup)
                {
                    AddLog(r, waited, "", "", "", "OFFLINE");
                    Count("OFFLINE");
                    FallBack(r, "OFFLINE");
                }
                return;
            }
            if (r.Warmup) return;
            r.Context.Recent.Clear();
            string content = ExtractContent(body);
            ParseOutput(content, r.Context.DefaultEmotion, out string line, out string emotion);
            List<string> global = new List<string>();
            int start = Mathf.Max(0, picker.History.Count - config.history_global);
            for (int i = start; i < picker.History.Count; i++) global.Add(picker.History[i].text);
            string result = BarkValidator.Validate(line, r.Context, barks.banned_words, picker.Recent(r.Enemy.Id), global);
            if (result == "OK" && r.State == "cancelled") result = "STALE";
            if (result == "OK" && (r.Enemy.IsDead || r.Enemy.EpisodeId != r.EpisodeId || r.Enemy.ActionId != r.ActionId)) result = "STALE";
            if (result == "OK")
            {
                consecutiveFailures = 0;
                AddToCache(r.CacheKey, line, emotion);
            }
            else if (result != "STALE")
            {
                consecutiveFailures++;
                if (consecutiveFailures >= config.fail_stop_count && !Stopped)
                {
                    Stopped = true;
                    Debug.LogWarning("[Qwen] 검증 실패가 연속 " + consecutiveFailures + "회라 이번 실행 동안 Qwen을 끕니다.");
                }
            }
            if (!r.FellBack || result != "OK") AddLog(r, waited, content, line, emotion, r.FellBack && result == "OK" ? "LATE_CACHED" : result);
            Count(result);
            if (r.FellBack) return;
            if (result != "OK")
            {
                FallBack(r, result);
                return;
            }
            Successes++;
            r.Trigger.DefaultEmotion = emotion;
            r.State = "done";
            pendingLine[r] = (line, emotion);
            if (paused) held.Add(r);
            else Deliver(r);
        }

        readonly Dictionary<BarkRequest, (string line, string emotion)> pendingLine = new Dictionary<BarkRequest, (string, string)>();

        void Deliver(BarkRequest r)
        {
            if (!pendingLine.TryGetValue(r, out var value)) return;
            pendingLine.Remove(r);
            // 보류 중 상황이 바뀌었으면 버린다
            if (r.Enemy.IsDead || r.Enemy.EpisodeId != r.EpisodeId || r.Enemy.ActionId != r.ActionId) return;
            picker.Say(r.Enemy, r.Trigger, value.line, value.emotion, "Qwen");
        }

        void AddToCache(string key, string line, string emotion)
        {
            if (!cache.TryGetValue(key, out var list))
            {
                list = new List<(string, string)>();
                cache[key] = list;
            }
            if (list.Exists(x => x.line == line)) return;
            list.Add((line, emotion));
            if (list.Count > 5) list.RemoveAt(0);
        }

        void Count(string result)
        {
            FailCounts.TryGetValue(result, out int n);
            FailCounts[result] = n + 1;
        }

        void AddLog(BarkRequest r, float waited, string raw, string line, string emotion, string result)
        {
            Log.Add(new QwenLogEntry
            {
                RequestId = r.Id,
                EnemyId = r.Enemy != null ? r.Enemy.Id : -1,
                Code = r.Trigger != null ? r.Trigger.When : "",
                LatencyMs = waited * 1000f,
                Input = r.Context != null ? r.Context.ToJson() : "",
                Raw = raw,
                Line = line,
                Emotion = emotion,
                Result = result,
            });
            if (Log.Count > config.log_max) Log.RemoveAt(0);
        }

        public QwenLogEntry? LastFor(int enemyId)
        {
            for (int i = Log.Count - 1; i >= 0; i--)
                if (Log[i].EnemyId == enemyId) return Log[i];
            return null;
        }

        string SystemPrompt(BarkContext c)
        {
            return "너는 2D 잠입 게임 속 적 " + c.Role + "이다. 성격: " + c.Persona + "(" + c.PersonaStyle + "). " +
                   "사용자가 주는 상황에 맞는 혼잣말 대사 한 줄을 한국어로, 공백과 문장부호를 포함해 20자 이내로 말한다. " +
                   "입력에 없는 정보(위치, 방향, 무기, 이름)는 말하지 않는다. 세부에 동료 이름이 있으면 활용한다. 최근 대사와 같은 말은 피한다. " +
                   "숫자, 영어, 'AI·모델·플레이어·게임' 같은 말은 쓰지 않는다. " +
                   "감정은 의심·놀람·긴장·분노·안도 중 하나. 반드시 {\"line\":\"대사\",\"emotion\":\"감정\"} JSON으로만 답한다.";
        }

        UnityWebRequest Post(string system, string user, int maxTokens)
        {
            var sb = new StringBuilder("{");
            sb.Append("\"model\":").Append(BarkContext.Quote(config.model)).Append(',');
            sb.Append("\"max_tokens\":").Append(maxTokens).Append(',');
            sb.Append("\"temperature\":").Append(config.temperature.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',');
            sb.Append("\"stream\":false,");
            sb.Append("\"messages\":[{\"role\":\"system\",\"content\":").Append(BarkContext.Quote(system)).Append("},");
            sb.Append("{\"role\":\"user\",\"content\":").Append(BarkContext.Quote(user)).Append("}]");
            if (config.use_json_schema)
            {
                sb.Append(",\"response_format\":{\"type\":\"json_schema\",\"json_schema\":{\"name\":\"bark\",\"schema\":{\"type\":\"object\",");
                sb.Append("\"properties\":{\"line\":{\"type\":\"string\"},\"emotion\":{\"type\":\"string\",\"enum\":[\"의심\",\"놀람\",\"긴장\",\"분노\",\"안도\"]}},");
                sb.Append("\"required\":[\"line\",\"emotion\"]}}}");
            }
            sb.Append('}');
            var web = new UnityWebRequest(config.endpoint, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(sb.ToString())),
                downloadHandler = new DownloadHandlerBuffer(),
                // 늦은 응답도 캐시에 넣기 위해 2초보다 길게 기다린다
                timeout = 60,
            };
            web.SetRequestHeader("Content-Type", "application/json");
            web.SendWebRequest();
            return web;
        }

        static string ExtractContent(string body)
        {
            try
            {
                ChatResponse response = JsonUtility.FromJson<ChatResponse>(body);
                if (response != null && response.choices != null && response.choices.Length > 0 && response.choices[0].message != null)
                    return response.choices[0].message.content ?? "";
            }
            catch (ArgumentException) { }
            return "";
        }

        // JSON 스키마 제약이 없을 때를 대비해, JSON이 아니면 글자 전체를 대사로 본다
        public static void ParseOutput(string content, string defaultEmotion, out string line, out string emotion)
        {
            line = "";
            emotion = defaultEmotion;
            string trimmed = BarkValidator.Clean(content);
            if (trimmed.StartsWith("{"))
            {
                try
                {
                    BarkOutput output = JsonUtility.FromJson<BarkOutput>(trimmed);
                    if (output != null)
                    {
                        line = BarkValidator.Clean(output.line);
                        emotion = BarkValidator.FixEmotion(output.emotion, defaultEmotion);
                        return;
                    }
                }
                catch (ArgumentException) { }
            }
            line = trimmed;
        }

        public void Dispose()
        {
            foreach (BarkRequest r in inFlight) r.Web?.Dispose();
            inFlight.Clear();
        }
    }
}
