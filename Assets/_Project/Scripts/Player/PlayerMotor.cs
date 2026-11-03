using System.Collections.Generic;
using UnityEngine;

namespace GameAi
{
    // 원과 격자 칸의 충돌 이동. Physics2D를 쓰지 않는다(CLAUDE.md).
    // 핵심 아이디어: 먼저 이동한 뒤, 막힌 칸과 겹치면 겹친 만큼 바깥으로 밀어낸다
    public static class PlayerMotor
    {
        // 한 번에 이 거리(m)보다 많이 움직이지 않게 잘게 나눈다. 빠르게 움직여도 벽을 뚫지 않는다
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

        // 입력 방향을 속도 벡터로. 정규화해서 대각선도 같은 속도가 된다
        public static Vector2 Velocity(Vector2 input, float speed)
        {
            return input.sqrMagnitude < 1e-6f ? Vector2.zero : input.normalized * speed;
        }

        // position에서 delta만큼 이동한 최종 위치. 벽에 닿으면 벽을 따라 미끄러진다
        public static Vector2 Move(GridMap grid, Vector2 position, Vector2 delta, float radius)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / MaxStep));
            Vector2 step = delta / steps;
            for (int i = 0; i < steps; i++)
            {
                // 축을 따로 움직이고 매번 밀어내야 벽을 따라 미끄러진다(한 축만 막혀도 다른 축은 간다)
                position.x += step.x;
                position = PushOutOfCells(grid, position, radius);
                position.y += step.y;
                position = PushOutOfCells(grid, position, radius);
            }
            return position;
        }

        // 막힌 칸과 겹친 원을 바깥으로 민다. 계산은 "1칸 = 1" 단위로 바꿔서 하고 m로 되돌린다
        public static Vector2 PushOutOfCells(GridMap grid, Vector2 position, float radius)
        {
            float cellSize = grid.CellSize;
            position /= cellSize;
            radius /= cellSize;

            // 면에 닿은 칸을 먼저 밀어낸다. 모서리를 먼저 처리하면 벽 칸 이음새의 모서리에 걸려 옆으로 튄다
            position = PushPass(grid, position, radius, false);
            return PushPass(grid, position, radius, true) * cellSize;
        }

        // 원 주변 칸을 훑으며 겹친 막힌 칸을 민다(칸 단위 좌표). corners가 true면 모서리에서 겹친 칸만, false면 면에서 겹친 칸만 처리한다
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

                    // 원 중심에서 칸까지 가장 가까운 점. x, y 둘 다 어긋나 있으면 칸의 모서리에 닿은 것이다
                    Vector2 closest = MathUtil.ClosestPointOnCell(position, x, y);
                    Vector2 away = position - closest;
                    bool isCorner = away.x != 0f && away.y != 0f;
                    if (isCorner != corners) continue;

                    float distance = away.magnitude;
                    if (distance >= radius) continue;

                    // 가장 가까운 점에서 반지름만큼 떨어진 곳으로 옮긴다
                    if (distance > 1e-5f)
                        position = closest + away / distance * radius;
                    else
                        position = PushOutFromInside(position, x, y, radius);
                }
            }
            return position;
        }

        // 원 중심이 막힌 칸 안으로 들어갔을 때는 가장 가까운 변 쪽으로 뺀다
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

        // 살아 있는 다른 대상과 겹치지 않게 민다. 시체는 막지 않는다.
        // foreach 대신 인덱스로 순회해 매 프레임 불려도 메모리를 할당하지 않는다
        public static Vector2 PushOutOfTargets(Vector2 position, float radius, IReadOnlyList<CombatTarget> targets, int selfId)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                CombatTarget target = targets[i];
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
