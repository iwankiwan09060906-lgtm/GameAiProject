// 역할: 소리를 한 사건으로 발행한다. 거리/벽 감쇠는 듣는 EnemyAgent마다 따로 계산한다.
// 같은 Publish에서 나온 EventId는 모든 청취자와 뒤이은 무전에서 동일하다.
using System;
using UnityEngine;

namespace NullTrace.AI
{
    [DisallowMultipleComponent]
    public sealed class NoiseBus : MonoBehaviour
    {
        public event Action<NoiseEvent> Emitted;

        /// <summary>게임 코드의 기본 입력. 시각은 일시정지와 함께 멈추는 Unity 게임 시간을 사용한다.</summary>
        public void Publish(Vector2 position, float radius, float loudness = 1f, NoiseKind kind = NoiseKind.Footstep)
            => PublishAt(position, radius, Time.timeAsDouble, loudness, kind);

        /// <summary>
        /// 수동 Tick 테스트용 입력. occurredAt에는 agent.GameTime과 같은 기준의 '발생 당시' 초를 준다.
        /// 수신 시각으로 원본 발생 시각을 바꾸지 않는다. 잘못된 수는 물리 검사 전에 무시한다.
        /// </summary>
        public void PublishAt(Vector2 position, float radius, double occurredAt, float loudness = 1f,
            NoiseKind kind = NoiseKind.Footstep)
        {
            if (!isActiveAndEnabled || !AiMath.IsFinite(position) || !AiMath.IsFinite(radius)
                || !AiMath.IsFinite(loudness) || radius <= 0f || loudness <= 0f
                || double.IsNaN(occurredAt) || double.IsInfinity(occurredAt) || occurredAt < 0)
                return;
            Emitted?.Invoke(new NoiseEvent(position, Mathf.Min(radius, 10000f), Mathf.Clamp01(loudness),
                kind, AiEventIds.Next(), occurredAt));
        }
    }
}
