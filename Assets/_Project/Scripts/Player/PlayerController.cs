using System.Collections.Generic;
using UnityEngine;

namespace GameAi
{
    // 이동·조준. 무기·근접·상호작용은 GameManager가 순서대로 따로 부른다
    public sealed class PlayerController : MonoBehaviour
    {
        GridMap grid;
        PlayerConfig config;
        IReadOnlyList<CombatTarget> targets;

        public CombatTarget Target { get; private set; }
        public Health Health { get; private set; }
        public CharacterView View { get; private set; }
        public MoveMode Mode { get; private set; }
        public bool IsMoving { get; private set; }
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

        public void TickMove(float dt, in PlayerInputState input)
        {
            Mode = PlayerMotor.ModeFor(input.RunHeld, input.CrouchHeld);
            Vector2 velocity = PlayerMotor.Velocity(input.Move, PlayerMotor.SpeedFor(Mode, config));
            IsMoving = velocity != Vector2.zero;

            Vector2 next = PlayerMotor.Move(grid, Target.Position, velocity * dt, config.radius_m);
            next = PlayerMotor.PushOutOfTargets(next, config.radius_m, targets, Target.Id);
            next = PlayerMotor.PushOutOfCells(grid, next, config.radius_m);
            SetPosition(next);

            Vector2 toAim = input.AimWorld - next;
            if (toAim.sqrMagnitude > 1e-4f)
            {
                AimDir = toAim.normalized;
                View.SetAim(MathUtil.AngleDeg(AimDir));
            }
        }

        public void SetPosition(Vector2 position)
        {
            Target.Position = position;
            transform.position = position;
        }

        public void TickView(float dt)
        {
            View.Tick(dt, IsMoving ? CharacterPose.Walk : CharacterPose.Idle);
        }
    }
}
