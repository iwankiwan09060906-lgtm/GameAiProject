using System.Collections.Generic;
using UnityEngine;

namespace GameAi
{
    public enum Team { Player, Enemy }

    public static class EntityIds
    {
        public const int Player = 0;
    }

    // 총알과 근접이 찾는 대상. 위치는 주인이 매 프레임 갱신한다
    public sealed class CombatTarget
    {
        public int Id;
        public Team Team;
        public Vector2 Position;
        public float Radius;
        public bool Alive = true;
    }

    public struct HitResult
    {
        public Vector2 End;
        public bool HitTarget;
        public int TargetId;
    }

    // 첫 번째로 닿는 대상이나 막는 칸에서 멈춘다. 관통은 없다
    public static class Hitscan
    {
        public static HitResult Cast(GridMap grid, Vector2 origin, Vector2 dir, float range,
            IReadOnlyList<CombatTarget> targets, Team shooterTeam)
        {
            RayHit wall = Raycast.Cast(grid, origin, dir, range, RayMask.Bullet);
            float best = wall.Hit ? wall.Distance : range;
            int hitId = -1;

            foreach (var target in targets)
            {
                if (!target.Alive || target.Team == shooterTeam) continue;
                if (TryHitCircle(origin, dir, target.Position, target.Radius, out float t) && t < best)
                {
                    best = t;
                    hitId = target.Id;
                }
            }

            return new HitResult { End = origin + dir * best, HitTarget = hitId >= 0, TargetId = hitId };
        }

        static bool TryHitCircle(Vector2 origin, Vector2 dir, Vector2 center, float radius, out float t)
        {
            Vector2 toCenter = center - origin;
            float along = Vector2.Dot(toCenter, dir);
            float closestSqr = toCenter.sqrMagnitude - along * along;
            float radiusSqr = radius * radius;
            t = 0f;
            if (closestSqr > radiusSqr) return false;

            float offset = Mathf.Sqrt(radiusSqr - closestSqr);
            float near = along - offset;
            float far = along + offset;
            if (far < 0f) return false;
            t = Mathf.Max(0f, near);
            return true;
        }
    }

    public static class MeleeRules
    {
        // 7.5 조건을 모두 만족해야 한다. enemyFacing은 정규화된 벡터
        public static bool CanTakedown(GridMap grid, Vector2 playerPos, Vector2 enemyPos, Vector2 enemyFacing,
            EnemyState state, float gauge, float detectGauge, MeleeConfig config)
        {
            if (state != EnemyState.Patrol && state != EnemyState.Investigate) return false;
            if (gauge >= detectGauge) return false;

            Vector2 toPlayer = playerPos - enemyPos;
            float distance = toPlayer.magnitude;
            if (distance > config.range_m || distance < 1e-5f) return false;

            float fromBack = Vector2.Angle(-enemyFacing, toPlayer);
            if (fromBack > config.back_arc_deg / 2f) return false;

            return LineOfSight.IsClear(grid, playerPos, enemyPos, RayMask.Bullet);
        }
    }
}
