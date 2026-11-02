using System.Collections.Generic;
using UnityEngine;

namespace GameAi
{
    // 원과 격자 칸의 충돌 이동. Physics2D를 쓰지 않는다
    public static class PlayerMotor
    {
        const float MaxStep = 0.25f;

        public static float SpeedFor(MoveMode mode, PlayerConfig config)
        {
            switch (mode)
            {
                case MoveMode.Run: return config.walk_speed * config.run_mult;
                case MoveMode.Crouch: return config.walk_speed * config.crouch_mult;
                default: return config.walk_speed;
            }
        }

        // 웅크리기가 달리기보다 우선이다
        public static MoveMode ModeFor(bool run, bool crouch)
        {
            if (crouch) return MoveMode.Crouch;
            return run ? MoveMode.Run : MoveMode.Walk;
        }

        // 대각선 이동도 같은 속도가 되게 정규화한다
        public static Vector2 Velocity(Vector2 input, float speed)
        {
            return input.sqrMagnitude < 1e-6f ? Vector2.zero : input.normalized * speed;
        }

        public static Vector2 Move(GridMap grid, Vector2 position, Vector2 delta, float radius)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / MaxStep));
            Vector2 step = delta / steps;
            for (int i = 0; i < steps; i++)
            {
                position.x += step.x;
                position = PushOutOfCells(grid, position, radius);
                position.y += step.y;
                position = PushOutOfCells(grid, position, radius);
            }
            return position;
        }

        public static Vector2 PushOutOfCells(GridMap grid, Vector2 position, float radius)
        {
            // 면에 닿은 칸을 먼저 밀어낸다. 그래야 벽 칸 이음새의 모서리에 걸려 옆으로 튀지 않는다
            position = PushPass(grid, position, radius, false);
            return PushPass(grid, position, radius, true);
        }

        static Vector2 PushPass(GridMap grid, Vector2 position, float radius, bool corners)
        {
            int minX = Mathf.FloorToInt(position.x - radius);
            int maxX = Mathf.FloorToInt(position.x + radius);
            int minY = Mathf.FloorToInt(position.y - radius);
            int maxY = Mathf.FloorToInt(position.y + radius);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (!grid.BlocksMove(x, y)) continue;
                    Vector2 closest = MathUtil.ClosestPointOnCell(position, x, y);
                    Vector2 away = position - closest;
                    bool isCorner = away.x != 0f && away.y != 0f;
                    if (isCorner != corners) continue;

                    float distance = away.magnitude;
                    if (distance >= radius) continue;
                    if (distance > 1e-5f)
                        position = closest + away / distance * radius;
                    else
                        position = PushOutFromInside(position, x, y, radius);
                }
            }
            return position;
        }

        // 중심이 칸 안으로 들어갔을 때는 가장 가까운 변 쪽으로 뺀다
        static Vector2 PushOutFromInside(Vector2 p, int x, int y, float radius)
        {
            float left = p.x - x;
            float right = x + 1 - p.x;
            float down = p.y - y;
            float up = y + 1 - p.y;
            float min = Mathf.Min(left, right, down, up);
            if (min == left) p.x = x - radius;
            else if (min == right) p.x = x + 1 + radius;
            else if (min == down) p.y = y - radius;
            else p.y = y + 1 + radius;
            return p;
        }

        // 살아 있는 다른 대상과 겹치지 않게 민다. 시체는 막지 않는다
        public static Vector2 PushOutOfTargets(Vector2 position, float radius, IReadOnlyList<CombatTarget> targets, int selfId)
        {
            foreach (var target in targets)
            {
                if (target.Id == selfId || !target.Alive) continue;
                Vector2 away = position - target.Position;
                float minDistance = radius + target.Radius;
                float distance = away.magnitude;
                if (distance >= minDistance || distance < 1e-5f) continue;
                position = target.Position + away / distance * minDistance;
            }
            return position;
        }
    }
}
