using System.Collections.Generic;
using UnityEngine;

namespace GameAi
{
    // 편. 같은 편은 서로 맞히지 않는다(적끼리 피해 없음)
    public enum Team { Player, Enemy }

    // 개체 ID 약속. 플레이어는 항상 0, 적은 1부터
    public static class EntityIds
    {
        public const int Player = 0;
    }

    // 총알과 근접이 찾는 대상. 위치는 주인(PlayerController, EnemyController)이 매 프레임 갱신한다.
    // MonoBehaviour가 아니라서 Physics2D 없이 순수하게 계산하고 테스트할 수 있다
    public sealed class CombatTarget
    {
        public int Id;
        public Team Team;
        public Vector2 Position;

        // 몸 반지름(m). 총알이 맞는 원의 크기
        public float Radius;

        // 죽으면 false. 시체는 총알도 이동도 막지 않는다
        public bool Alive = true;
    }

    // 사격 한 번의 결과
    public struct HitResult
    {
        // 총알이 멈춘 지점. 궤적선의 끝이다
        public Vector2 End;
        public bool HitTarget;
        public int TargetId;
    }

    // 히트스캔: 발사 순간 조준 방향으로 사거리까지 선을 쏜다(FN-COM-01).
    // 처음 닿는 대상이나 막는 칸에서 멈추며 관통하지 않는다
    public static class Hitscan
    {
        public static HitResult Cast(GridMap grid, Vector2 origin, Vector2 dir, float range,
            IReadOnlyList<CombatTarget> targets, Team shooterTeam)
        {
            // 먼저 벽·닫힌 문·창문·엄폐물에서 멈추는 거리를 구한다. 총알은 낮은 엄폐물과 창문도 막는다
            RayHit wall = Raycast.Cast(grid, origin, dir, range, RayMask.Bullet);
            float best = wall.Hit ? wall.Distance : range;
            int hitId = -1;

            // 그 거리보다 가까운 대상 중 가장 가까운 하나를 찾는다.
            // 인덱스로 순회해 foreach의 메모리 할당을 피한다
            for (int i = 0; i < targets.Count; i++)
            {
                CombatTarget target = targets[i];
                if (!target.Alive || target.Team == shooterTeam) continue;
                if (TryHitCircle(origin, dir, target.Position, target.Radius, out float t) && t < best)
                {
                    best = t;
                    hitId = target.Id;
                }
            }

            return new HitResult { End = origin + dir * best, HitTarget = hitId >= 0, TargetId = hitId };
        }

        // 반직선과 원의 교차. 맞으면 시작점에서 처음 닿는 거리를 t로 돌려준다
        static bool TryHitCircle(Vector2 origin, Vector2 dir, Vector2 center, float radius, out float t)
        {
            Vector2 toCenter = center - origin;

            // along: 원 중심을 반직선에 내린 수선의 발까지의 거리, closestSqr: 중심과 반직선 사이 거리의 제곱
            float along = Vector2.Dot(toCenter, dir);
            float closestSqr = toCenter.sqrMagnitude - along * along;
            float radiusSqr = radius * radius;
            t = 0f;
            if (closestSqr > radiusSqr) return false;

            // 원 안쪽으로 파고드는 구간의 시작(near)과 끝(far)
            float offset = Mathf.Sqrt(radiusSqr - closestSqr);
            float near = along - offset;
            float far = along + offset;

            // 원이 전부 등 뒤에 있으면 맞지 않는다. 시작점이 원 안이면 거리 0으로 맞은 것
            if (far < 0f) return false;
            t = Mathf.Max(0f, near);
            return true;
        }
    }

    // 근접 제압 조건 판정(7.5, FN-COM-03). 순수 함수
    public static class MeleeRules
    {
        // 조건을 모두 만족해야 한다. enemyFacing은 정규화된 벡터
        public static bool CanTakedown(GridMap grid, Vector2 playerPos, Vector2 enemyPos, Vector2 enemyFacing,
            EnemyState state, float gauge, float detectGauge, MeleeConfig config)
        {
            // 순찰·조사(미발각)이고 게이지가 발견 기준(100) 미만일 때만
            if (state != EnemyState.Patrol && state != EnemyState.Investigate) return false;
            if (gauge >= detectGauge) return false;

            Vector2 toPlayer = playerPos - enemyPos;
            float distance = toPlayer.magnitude;
            if (distance > config.range_m || distance < 1e-5f) return false;

            // 적이 바라보는 방향의 반대(등 뒤)와 플레이어 방향이 이루는 각이 부채꼴 절반 이내인지
            float fromBack = Vector2.Angle(-enemyFacing, toPlayer);
            if (fromBack > config.back_arc_deg / 2f) return false;

            // 벽 너머로는 제압할 수 없다
            return LineOfSight.IsClear(grid, playerPos, enemyPos, RayMask.Bullet);
        }
    }
}
