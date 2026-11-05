// 역할: 외부에 공개하는 상태 이름과 소음 사건 값을 정의한다.
// EnemyState는 기존 UI 호환용 요약이다. 실제 판단은 EnemyGoal, 업무 복원은 EnemyDuty가 맡는다.
using System.Threading;
using UnityEngine;

namespace NullTrace.AI
{
    public enum EnemyState { Calm, Alert, Search, Combat }
    public enum LightLevel { Dark, Dim, Bright }
    public enum NoiseKind { Footstep, Gunshot, Impact, Door }
    public enum EnemyGoal { Idle, Patrol, ReturnToPost, Inspect, Search, PrepareCombat, Attack, ReturnToAnchor, Blocked }
    public enum EnemyDuty { Guard, Patrol, ReturnToPost }
    public enum PursuitPhase { None, Responding, Returning, ReturnBlocked }
    public enum ClueKind { Sound, Sighting, Investigation }

    // 사건 번호는 생성할 때 한 번만 정한다. 같은 사건을 무전으로 전달할 때 새 번호를 만들지 않는다.
    public static class AiEventIds
    {
        private static long next;
        public static long Next() => Interlocked.Increment(ref next);
    }

    /// <summary>발생 순간의 XY 좌표와 게임 시각을 복사한 소리. 플레이어 Transform을 보관하지 않는다.</summary>
    public readonly struct NoiseEvent
    {
        public readonly Vector2 Position;
        public readonly float Radius;
        public readonly float Loudness;
        public readonly NoiseKind Kind;
        public readonly long EventId;
        public readonly double OccurredAt;

        // 기존 생성자도 유지한다. 수동 시간 검증은 명시적 시각 생성자 또는 NoiseBus.PublishAt을 쓴다.
        public NoiseEvent(Vector2 position, float radius, float loudness, NoiseKind kind)
            : this(position, radius, loudness, kind, AiEventIds.Next(), Time.timeAsDouble) { }

        public NoiseEvent(Vector2 position, float radius, float loudness, NoiseKind kind, long eventId, double occurredAt)
        {
            Position = position;
            Radius = radius;
            Loudness = loudness;
            Kind = kind;
            EventId = eventId;
            OccurredAt = occurredAt;
        }
    }
}
