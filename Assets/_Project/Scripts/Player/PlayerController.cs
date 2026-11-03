using System.Collections.Generic;
using UnityEngine;

namespace GameAi
{
    // 플레이어의 이동과 조준. 무기·근접·상호작용은 GameManager가 순서대로 따로 부른다.
    // 위치의 진짜 값은 CombatTarget.Position이고, transform은 그것을 따라간다
    public sealed class PlayerController : MonoBehaviour
    {
        GridMap grid;
        PlayerConfig config;

        // 겹치지 않으려고 확인할 다른 대상들
        IReadOnlyList<CombatTarget> targets;

        public CombatTarget Target { get; private set; }
        public Health Health { get; private set; }
        public CharacterView View { get; private set; }
        public MoveMode Mode { get; private set; }
        public bool IsMoving { get; private set; }

        // 몸이 바라보는 방향(커서 쪽). 사격과 시야의 기준이다
        public Vector2 AimDir { get; private set; } = Vector2.right;
        public Vector2 Position { get { return Target.Position; } }

        public void Init(GridMap map, PlayerConfig playerConfig, IReadOnlyList<CombatTarget> allTargets,
            CombatTarget target, Health health, CharacterView view)
        {
            grid = map;
            config = playerConfig;
            targets = allTargets;
            Target = target;
            Health = health;
            View = view;
            transform.position = target.Position;
        }

        // 2단계: 입력으로 이동하고 커서 쪽을 바라본다
        public void TickMove(float dt, in PlayerInputState input)
        {
            Mode = PlayerMotor.ModeFor(input.RunHeld, input.CrouchHeld);
            Vector2 velocity = PlayerMotor.Velocity(input.Move, PlayerMotor.SpeedFor(Mode, config));
            IsMoving = velocity != Vector2.zero;

            // 벽 → 다른 대상 → 벽 순으로 밀어낸다. 마지막 벽 처리는 대상이 밀어낸 결과가 벽 안으로 들어가는 것을 막는다
            Vector2 next = PlayerMotor.Move(grid, Target.Position, velocity * dt, config.radius_m);
            next = PlayerMotor.PushOutOfTargets(next, config.radius_m, targets, Target.Id);
            next = PlayerMotor.PushOutOfCells(grid, next, config.radius_m);
            SetPosition(next);

            // 커서가 몸 위에 거의 겹치면 방향이 불안정해지므로 바꾸지 않는다
            Vector2 toAim = input.AimWorld - next;
            if (toAim.sqrMagnitude > 1e-4f)
            {
                AimDir = toAim.normalized;
                View.SetAim(MathUtil.AngleDeg(AimDir));
            }
        }

        // 위치를 바꾼다(이동, 자동 재생의 순간이동 등)
        public void SetPosition(Vector2 position)
        {
            Target.Position = position;
            transform.position = position;
        }

        // 9단계: 걷는 중이면 걷기 그림, 아니면 서 있는 그림
        public void TickView(float dt)
        {
            View.Tick(dt, IsMoving ? CharacterPose.Walk : CharacterPose.Idle);
        }
    }
}
