// 역할: NPC 한 명에게 실제로 도착한 단서와 단서별 의심도 기여를 보관한다.
// 같은 사건의 보고를 더한다고 점수가 늘지 않는다. 보고의 시각은 수신 시각으로 바꾸지 않는다.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace NullTrace.AI
{
    /// <summary>무전으로 복사 가능한 사실. Transform이나 다른 NPC의 기억을 참조하지 않는다.</summary>
    public readonly struct ClueReport
    {
        public readonly long Id;
        public readonly ClueKind Kind;
        public readonly NoiseKind Sound;
        public readonly Vector2 Position;
        public readonly double OccurredAt, ObservedAt, ExpiresAt;
        public readonly int SourceId, ReporterId, Revision;
        public readonly float Strength, Confidence;
        public readonly float InvestigationRadius;
        public readonly bool Identified;

        public ClueReport(long id, ClueKind kind, NoiseKind sound, Vector2 position, double occurredAt,
            double observedAt, double expiresAt, int sourceId, int reporterId, int revision,
            float strength, float confidence, bool identified = false, float investigationRadius = 0)
        {
            Id = id; Kind = kind; Sound = sound; Position = position; OccurredAt = occurredAt;
            ObservedAt = observedAt; ExpiresAt = expiresAt; SourceId = sourceId; ReporterId = reporterId;
            Revision = revision; Strength = strength; Confidence = confidence; Identified = identified;
            InvestigationRadius = investigationRadius;
        }

        public bool IsValid(double now) => Id > 0 && Revision >= 0 && AiMath.IsFinite(Position)
            && AiMath.IsFinite(Strength) && Strength >= 0 && Strength <= 1
            && AiMath.IsFinite(Confidence) && Confidence >= 0 && Confidence <= 1
            && AiMath.IsFinite(InvestigationRadius) && InvestigationRadius >= 0
            && Finite(OccurredAt) && Finite(ObservedAt) && Finite(ExpiresAt) && Finite(now)
            && OccurredAt >= 0 && ObservedAt >= OccurredAt && ObservedAt <= now + 0.001
            && ExpiresAt > ObservedAt && ExpiresAt > now
            && Enum.IsDefined(typeof(ClueKind), Kind) && Enum.IsDefined(typeof(NoiseKind), Sound);

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        public float BasePoints => Kind == ClueKind.Investigation ? 0 : Kind == ClueKind.Sighting ? (Identified ? 90 : 20)
            : Sound == NoiseKind.Gunshot ? 50 : Sound == NoiseKind.Footstep ? 15 : 25;
    }

    /// <summary>보고 사본 + 이 수신자만의 처리 기록. 외부는 읽을 수 있고 변경은 EnemyMemory만 한다.</summary>
    public sealed class ClueMemory
    {
        public ClueReport Report { get; internal set; }
        public double ReceivedAt { get; internal set; }
        public float Contribution { get; internal set; }
        public bool Direct { get; internal set; }
        public bool Inspected { get; internal set; }
        public bool Processed { get; internal set; }
    }

    public sealed class EnemyMemory
    {
        public const int Capacity = 8;
        public const int RetiredCapacity = 32;
        private readonly Dictionary<long, ClueMemory> clues = new Dictionary<long, ClueMemory>();
        private readonly List<long> expired = new List<long>();
        // 버린 단서도 ID/관측 순번을 잠시 기억해야 늦은 무전이 새 사건처럼 되살아나지 않는다.
        // shortcut: 최근 제거 사건 32개만 중복을 기억한다. 장시간 영구 재전송 방지가 필요하면 발신 프로토콜을 확장한다.
        private readonly List<ClueReport> retired = new List<ClueReport>(RetiredCapacity);
        private long currentSighting;
        public IEnumerable<ClueMemory> Entries => clues.Values;
        public int Count => clues.Count;
        public int RetiredCount => retired.Count;
        // 새 유효 관측에만 증가한다. 같은 관측의 재전송/직접 확인 교체는 조사 완료를 다시 열지 않는다.
        public int InformationVersion { get; private set; }
        public double LastInformationAt { get; private set; }
        public float Suspicion
        {
            get { float sum = 0; foreach (var clue in clues.Values) sum += clue.Contribution; return Mathf.Clamp(sum, 0, 100); }
        }
        public bool TryGet(long id, out ClueMemory clue) => clues.TryGetValue(id, out clue);
        public void Clear() { clues.Clear(); retired.Clear(); currentSighting = 0; InformationVersion = 0; LastInformationAt = 0; }
        /// <summary>감각 단계가 현재 직접 보이는 대상 ID를 준다. 시야가 없으면 0으로 보호를 해제한다.</summary>
        public void SetCurrentSighting(long id) => currentSighting = id;
        public void MarkInspected(long id) { if (clues.TryGetValue(id, out var clue)) clue.Inspected = true; }
        public void MarkProcessed(long id) { if (clues.TryGetValue(id, out var clue)) clue.Processed = true; }
        public void Remove(long id)
        {
            if (!clues.TryGetValue(id, out var clue)) return;
            Retire(clue.Report); clues.Remove(id);
            if (currentSighting == id) currentSighting = 0;
        }

        /// <summary>
        /// true는 새 정보 또는 간접→직접 교체를 받았다는 뜻이다. 같은 관측의 간접 재전송은 신뢰도가 높아도 무시한다.
        /// 직접 확인은 해당 기여를 직접 값으로 한 번 교체하며 더하지 않는다. 순번/원본 시각/만료는 계속 검사한다.
        /// directVisualSeconds는 직접 시야일 때만 준다. 간접 시각 보고에 시각 누적을 적용하지 않는다.
        /// </summary>
        public bool Receive(ClueReport report, double now, bool direct = false, float directVisualSeconds = 0)
        {
            if (!report.IsValid(now) || !AiMath.IsFinite(directVisualSeconds) || directVisualSeconds < 0) return false;
            Prune(now);
            bool exists = clues.TryGetValue(report.Id, out var clue);
            bool visual = direct && report.Kind == ClueKind.Sighting;
            bool sameObservation = false;
            if (exists)
            {
                var previous = clue.Report;
                if (!SameEvent(report, previous) || report.Revision < previous.Revision || report.ObservedAt < previous.ObservedAt) return false;
                if (report.Revision == previous.Revision)
                {
                    // 같은 순번은 위치/시각을 바꿀 권한이 없다. direct로 교체해도 동일 관측이어야 한다.
                    if (report.ObservedAt != previous.ObservedAt || report.Position != previous.Position
                        || report.ExpiresAt != previous.ExpiresAt || !direct || clue.Direct) return false;
                    sameObservation = true;
                }
                // 음향은 한 번의 충격이다. 순번을 올려도 발생점·수명이 새 사건처럼 바뀌지 않는다.
                else if (report.Kind != ClueKind.Sighting
                    || (report.ObservedAt == previous.ObservedAt && (!direct || clue.Direct))) return false;
                sameObservation |= report.ObservedAt == previous.ObservedAt;
                // 현재 직접 보는 위치를 다른 NPC의 간접 보고로 교체하지 않는다.
                if (!direct && clue.Direct && report.Id == currentSighting) return false;
            }
            else
            {
                int oldIndex = retired.FindIndex(old => old.Id == report.Id);
                if (oldIndex >= 0)
                {
                    var old = retired[oldIndex];
                    if (!SameEvent(report, old) || report.Kind != ClueKind.Sighting
                        || report.Revision <= old.Revision || report.ObservedAt <= old.ObservedAt) return false;
                }
                if (!MakeRoom(report, visual)) { Retire(report); return false; }
                if (oldIndex >= 0) retired.RemoveAll(old => old.Id == report.Id);
                clue = new ClueMemory();
                clues.Add(report.Id, clue);
            }
            float points = report.BasePoints * report.Strength * report.Confidence;
            if (visual)
            {
                // 간접 보고를 직접 확인하면 기존 간접 점수를 이중 가산하지 않는다.
                float prior = clue.Direct ? clue.Contribution : 0;
                points = report.Identified ? Mathf.Max(90, prior) : Mathf.Min(90, prior + 20 * directVisualSeconds);
            }
            // 의미 있게 새 위치를 봤을 때만 완료된 조사를 다시 열 수 있다. 제자리 연속 시야는 직접 대응으로 처리한다.
            if (exists && (report.Position - clue.Report.Position).sqrMagnitude >= 0.25f)
                clue.Inspected = clue.Processed = false;
            clue.Report = report; clue.ReceivedAt = now; clue.Direct = direct;
            clue.Contribution = points;
            if (visual) currentSighting = report.Id;
            // 조사 완료 보고는 그 영역/시각의 결과일 뿐, 다른 사건을 지우거나 새 위협 점수를 만들지 않는다.
            if (report.Kind == ClueKind.Investigation) { clue.Inspected = clue.Processed = true; return true; }
            if (!sameObservation) { InformationVersion++; LastInformationAt = now; }
            return true;
        }

        private static bool SameEvent(ClueReport a, ClueReport b) => a.Kind == b.Kind && a.Sound == b.Sound
            && a.SourceId == b.SourceId && a.OccurredAt == b.OccurredAt;
        private void Retire(ClueReport report)
        {
            int index = retired.FindIndex(old => old.Id == report.Id);
            if (index >= 0)
            {
                if (retired[index].Revision >= report.Revision) return;
                retired.RemoveAt(index);
            }
            if (retired.Count == RetiredCapacity) retired.RemoveAt(0);
            retired.Add(report);
        }
        private bool MakeRoom(ClueReport incoming, bool directVisual)
        {
            if (clues.Count < Capacity) return true;
            ClueMemory victim = null;
            foreach (var candidate in clues.Values)
            {
                if (candidate.Report.Id == currentSighting) continue;
                if (victim == null || (candidate.Processed && !victim.Processed)
                    || (candidate.Processed == victim.Processed && (candidate.Report.ObservedAt < victim.Report.ObservedAt
                        || (candidate.Report.ObservedAt == victim.Report.ObservedAt && candidate.Report.Id < victim.Report.Id)))) victim = candidate;
            }
            if (victim == null) return false;
            // 조사 완료 보고나 오래된 지연 보고가 아직 유효한 최신 단서를 밀어내지는 않는다.
            if (!directVisual && !victim.Processed && (incoming.Kind == ClueKind.Investigation
                || incoming.ObservedAt < victim.Report.ObservedAt
                || (incoming.ObservedAt == victim.Report.ObservedAt && incoming.Id < victim.Report.Id))) return false;
            Remove(victim.Report.Id);
            return true;
        }
        private void Prune(double now)
        {
            expired.Clear();
            foreach (var pair in clues) if (pair.Value.Report.ExpiresAt <= now) expired.Add(pair.Key);
            foreach (long id in expired) Remove(id);
        }

        /// <summary>게임 시간으로 만료/감쇠한다. 초당 6점은 단서마다가 아니라 전체에 한 번 적용한다.</summary>
        public void Advance(double now, float elapsed, float quietSeconds = 3, float decayPerSecond = 6)
        {
            if (double.IsNaN(now) || double.IsInfinity(now) || !AiMath.IsFinite(elapsed) || elapsed < 0
                || !AiMath.IsFinite(quietSeconds) || quietSeconds < 0 || !AiMath.IsFinite(decayPerSecond) || decayPerSecond < 0) return;
            Prune(now);
            double duration = Math.Min(elapsed, Math.Max(0, now - LastInformationAt - quietSeconds));
            float total = 0; foreach (var clue in clues.Values) total += clue.Contribution;
            if (total <= 0 || duration <= 0) return;
            float fraction = Mathf.Max(0, 1 - (float)(duration * decayPerSecond) / total);
            foreach (var clue in clues.Values) clue.Contribution *= fraction;
        }
    }
}
