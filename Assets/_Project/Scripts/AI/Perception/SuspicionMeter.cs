using System.Collections.Generic;
using UnityEngine;

namespace GameAi
{
    public enum Tension { Calm, Caution, Searching, ActiveSearch, CombatReady, HighThreat }

    // 인지 1건이 남긴 기여(단서 기억). 7.21
    public sealed class ClueRecord
    {
        public int EventId;
        public string Code;
        public Vector2 Position;
        public float Value;
        public float Remaining;
        public float ExpiresAt;
        public bool Direct;
        public bool Sight;
        public float Time;
        public bool Handled;
    }

    // 의심도(0~100) = 남은 기여 합. 같은 사건은 한 번만, 조용하면 감쇠, 수명이 끝나면 그 기여만 뺀다
    public sealed class SuspicionMeter
    {
        public const int MaxRecords = 16;

        readonly List<ClueRecord> records = new List<ClueRecord>();
        float lastAddTime = float.NegativeInfinity;
        bool detected;
        float detectedFloor = 90f;

        public IReadOnlyList<ClueRecord> Records { get { return records; } }

        public float Value
        {
            get
            {
                float sum = 0f;
                foreach (ClueRecord r in records) sum += r.Remaining;
                if (detected) sum = Mathf.Max(sum, detectedFloor);
                return Mathf.Min(100f, sum);
            }
        }

        public void SetDetected(bool value, float floor)
        {
            detected = value;
            detectedFloor = floor;
        }

        // 새 기여면 true(같은 사건을 이미 알면 큰 쪽 하나만 남기고 false)
        public bool Add(int eventId, string code, Vector2 position, float value, float now, float lifetime, bool direct, bool sight)
        {
            if (value < 0f) value = 0f;
            ClueRecord existing = Find(eventId);
            if (existing != null)
            {
                if (value <= existing.Value) return false;
                float ratio = existing.Value > 0f ? existing.Remaining / existing.Value : 1f;
                existing.Value = value;
                existing.Remaining = value * ratio;
                existing.Direct |= direct;
                lastAddTime = now;
                return false;
            }
            if (records.Count >= MaxRecords) RemoveWeakest();
            records.Add(new ClueRecord
            {
                EventId = eventId,
                Code = code,
                Position = position,
                Value = value,
                Remaining = value,
                ExpiresAt = lifetime < 0f ? float.PositiveInfinity : now + lifetime,
                Direct = direct,
                Sight = sight,
                Time = now,
            });
            lastAddTime = now;
            return true;
        }

        // 같은 사건의 기여를 키운다(움직임 포착처럼 보는 동안 쌓이는 것). 최대값 cap
        public void Accumulate(int eventId, float amount, float cap, float now, Vector2 position)
        {
            ClueRecord r = Find(eventId);
            if (r == null) return;
            float add = Mathf.Min(amount, Mathf.Max(0f, cap - r.Value));
            r.Value += add;
            r.Remaining += add;
            r.Position = position;
            r.Time = now;
            if (add > 0f) lastAddTime = now;
        }

        public ClueRecord Find(int eventId)
        {
            foreach (ClueRecord r in records)
                if (r.EventId == eventId) return r;
            return null;
        }

        void RemoveWeakest()
        {
            int weakest = 0;
            for (int i = 1; i < records.Count; i++)
            {
                // 처리된 것부터, 그다음 남은 기여가 작은 것
                bool a = records[i].Handled, b = records[weakest].Handled;
                if (a != b ? a : records[i].Remaining < records[weakest].Remaining) weakest = i;
            }
            records.RemoveAt(weakest);
        }

        public void Tick(float now, float dt, SuspicionConfig config)
        {
            records.RemoveAll(r => now >= r.ExpiresAt);
            if (now - lastAddTime < config.quiet_s) return;
            float total = 0f;
            foreach (ClueRecord r in records) total += r.Remaining;
            if (total <= 0f) return;
            float decay = Mathf.Min(total, config.decay_per_s * dt);
            foreach (ClueRecord r in records)
                r.Remaining = Mathf.Max(0f, r.Remaining - decay * (r.Remaining / total));
        }

        // 조사할 단서: 최근 목격 > 기여가 큰 단서 > 가까운 단서. 처리 안 된 것만
        public ClueRecord BestClue(Vector2 from)
        {
            ClueRecord best = null;
            foreach (ClueRecord r in records)
            {
                if (r.Handled) continue;
                if (best == null || Better(r, best, from)) best = r;
            }
            return best;
        }

        static bool Better(ClueRecord a, ClueRecord b, Vector2 from)
        {
            if (a.Sight != b.Sight) return a.Sight;
            if (a.Sight) return a.Time > b.Time;
            if (!Mathf.Approximately(a.Remaining, b.Remaining)) return a.Remaining > b.Remaining;
            return Vector2.SqrMagnitude(a.Position - from) < Vector2.SqrMagnitude(b.Position - from);
        }

        public void MarkHandled(int eventId)
        {
            ClueRecord r = Find(eventId);
            if (r != null) r.Handled = true;
        }

        public void Clear()
        {
            records.Clear();
            detected = false;
        }

        public static Tension TensionOf(float value, float[] bands)
        {
            int level = 0;
            for (int i = 0; i < bands.Length && i < 5; i++)
                if (value >= bands[i]) level = i + 1;
            return (Tension)level;
        }

        public static string TensionKo(Tension t)
        {
            switch (t)
            {
                case Tension.Caution: return "주의";
                case Tension.Searching: return "수색";
                case Tension.ActiveSearch: return "적극 수색";
                case Tension.CombatReady: return "교전 준비";
                case Tension.HighThreat: return "높은 위협";
                default: return "평상";
            }
        }
    }
}
