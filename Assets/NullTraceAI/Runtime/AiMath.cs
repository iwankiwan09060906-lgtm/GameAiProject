// 역할: 시야 원뿔과 소리 감쇠를 숫자로 계산한다. 씬 오브젝트나 물리 충돌을 직접 조회하지 않는다.
// 연결: EnemyAgent가 위치/튜닝값/벽 개수를 넣고, 반환값에 따라 감지 여부를 결정한다.
// 읽는 순서: IsFinite → InView → HeardStrength. 실제 벽 검사는 GridWorld2D를 함께 읽는다.
using UnityEngine;

namespace NullTrace.AI
{
    /// <summary>같은 입력에는 같은 결과를 내는 감각 계산 모음. AiLogicTests에서 경계 조건을 검증한다.</summary>
    public static class AiMath
    {
        // 일반적인 유한한 수만 true. 잘못된 계산 결과를 좌표나 타이머로 사용하지 않기 위한 검사다.
        public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        // 좌표는 x, y 중 하나만 잘못되어도 전체를 사용하지 않는다.
        public static bool IsFinite(Vector2 value) => IsFinite(value.x) && IsFinite(value.y);

        /// <summary>
        /// observer에서 facing 방향으로 볼 때 target이 range 거리와 angle 각도 안에 있는지 반환한다.
        /// 위치/거리는 월드 단위, angle은 전체 시야각(도)이다. angle=90이면 정면 기준 좌우 45도다.
        /// true여도 벽에 가렸을 수 있으므로 EnemyAgent가 물리 시야 검사를 이어서 해야 한다.
        /// </summary>
        public static bool InView(Vector2 observer, Vector2 facing, Vector2 target,
            float range, float angle)
        {
            if (!IsFinite(observer) || !IsFinite(target) || !IsFinite(facing)
                || !IsFinite(range) || !IsFinite(angle) || range <= 0f || angle <= 0f)
                return false;
            // 대상 좌표에서 관찰자 좌표를 빼면 대상까지의 방향과 거리를 가진 벡터가 된다.
            Vector2 offset = target - observer;
            // 길이의 제곱끼리 비교하면 제곱근 계산 없이 최대 거리를 확인할 수 있다.
            if (offset.sqrMagnitude > range * range) return false;
            // 같은 위치에서는 방향 자체를 정할 수 없다. 거리 조건은 이미 만족했으므로 원뿔 검사는 통과한다.
            if (offset.sqrMagnitude < 0.000001f) return true;
            // normalized는 벡터 길이를 1로 맞추고 방향만 남긴다. 두 단위 벡터의 Dot(내적)은
            // 방향이 같을수록 1, 직각이면 0이다. 따라서 두 방향의 내적이 '반 시야각의 cos' 이상이면 보인다.
            // Cos는 라디안 단위를 받아 Deg2Rad로 변환한다. 작은 오차 허용값은 시야 경계의 반올림 오차용이다.
            return facing.sqrMagnitude > 0.000001f && Vector2.Dot(facing.normalized,
                offset.normalized) >= Mathf.Cos(Mathf.Min(angle, 360f) * 0.5f * Mathf.Deg2Rad) - 0.00001f;
        }

        /// <summary>
        /// 적과 발생점 사이 distance, 소리 반경 radius, 기본 음량 loudness, 벽 개수 walls,
        /// 벽 하나당 남는 비율 wallMultiplier로 도착 세기(0..1)를 계산한다. 잘못된 수는 0으로 처리한다.
        /// 이 함수는 들린다/안 들린다를 결정하지 않는다. 적이 반환값과 HearingThreshold를 비교한다.
        /// </summary>
        public static float HeardStrength(float distance, float radius, float loudness,
            int walls, float wallMultiplier)
        {
            if (!IsFinite(distance) || !IsFinite(radius) || !IsFinite(loudness)
                || !IsFinite(wallMultiplier) || radius <= 0f || distance < 0f)
                return 0f;
            // 거리 비율이 0이면 원래 음량, 반경의 절반이면 절반, 반경 밖이면 0이다.
            // 벽마다 남는 비율을 곱한다. 예: 거리 2/반경 10/음량 1/벽 2개/배율 0.5 → 1×0.8×0.5²=0.2.
            // Clamp01은 값을 0..1에 가두어 음량이 음수나 1 초과가 되지 않게 한다.
            // walls는 실제 물리 Collider 개수이므로 하나로 합친 CompositeCollider의 벽 구간 수와 다를 수 있다.
            return Mathf.Clamp01(loudness) * Mathf.Clamp01(1f - distance / radius)
                * Mathf.Pow(Mathf.Clamp01(wallMultiplier), Mathf.Max(0, walls));
        }
    }
}
