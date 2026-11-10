using UnityEngine;

namespace GameAi
{
    // 인지 게이지(0~100, 시각 식별). 7.7
    public sealed class AwarenessGauge
    {
        float lostTimer;

        public float Value { get; private set; }

        public void Set(float value) { Value = value; }

        public void Tick(float dt, bool seen, float riseMult, GaugeConfig config)
        {
            if (seen)
            {
                lostTimer = 0f;
                Value = Mathf.Min(config.detect, Value + config.rise_per_s * riseMult * dt);
                return;
            }
            lostTimer += dt;
            if (lostTimer > config.hold_after_lost_s)
                Value = Mathf.Max(0f, Value - config.fall_per_s * dt);
        }

        // 상승 배율: 3m 이내 ×2, 달리기 ×1.5, 웅크리기 ×0.5, 밝기 ×(0.3+0.7L). 3m 이내는 밝기 생략
        public static float RiseMultiplier(float distance, MoveMode mode, float light, GaugeConfig gauge, AiLightConfig lightConfig)
        {
            float mult = 1f;
            bool near = distance <= gauge.near_dist_m;
            if (near) mult *= gauge.near_mult;
            if (mode == MoveMode.Run) mult *= gauge.run_mult;
            else if (mode == MoveMode.Crouch) mult *= gauge.crouch_mult;
            if (!near) mult *= lightConfig.gauge_min_mult + (1f - lightConfig.gauge_min_mult) * Mathf.Clamp01(light);
            return mult;
        }

        // 실제 시야 거리 = 기본 × (0.5 + 0.5L)
        public static float EffectiveView(float viewM, float light, AiLightConfig lightConfig)
        {
            return viewM * (lightConfig.view_min_mult + (1f - lightConfig.view_min_mult) * Mathf.Clamp01(light));
        }
    }

    // 시각 판정(순수). 플레이어 위치를 직접 읽는 곳은 이 판정을 부르는 AiDirector 한 곳뿐이다
    public static class SightSensor
    {
        public static bool InCone(Vector2 eye, Vector2 facing, float fovDeg, Vector2 target)
        {
            Vector2 to = target - eye;
            if (to.sqrMagnitude < 1e-6f) return true;
            return Vector2.Angle(facing, to) <= fovDeg * 0.5f;
        }

        // 부채꼴 안 + 실제 시야 거리 안(3m 이내는 항상) + 벽·닫힌 문·높은 엄폐물에 가리지 않음
        public static bool CanSee(GridMap grid, Vector2 eye, Vector2 facing, float fovDeg, float viewM, Vector2 target, float light, float nearDist, AiLightConfig lightConfig)
        {
            float distance = Vector2.Distance(eye, target);
            float range = distance <= nearDist ? float.MaxValue : AwarenessGauge.EffectiveView(viewM, light, lightConfig);
            if (distance > range) return false;
            if (!InCone(eye, facing, fovDeg, target)) return false;
            return LineOfSight.IsClear(grid, eye, target, RayMask.Sight);
        }

        // 물체(단서) 보기: 부채꼴 + 거리 + 차폐 + 밝기 조건
        public static bool CanSeeObject(GridMap grid, Vector2 eye, Vector2 facing, float fovDeg, float range, Vector2 target, float light, float minLight)
        {
            if (Vector2.Distance(eye, target) > range) return false;
            if (light < minLight) return false;
            if (!InCone(eye, facing, fovDeg, target)) return false;
            return LineOfSight.IsClear(grid, eye, target, RayMask.Sight);
        }
    }
}
