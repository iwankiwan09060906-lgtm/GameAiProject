using UnityEngine;

namespace GameAi
{
    // Phase 3까지는 움직이지 않는 허수아비다. 감지·FSM·이동은 Phase 5에서 붙인다
    public sealed class EnemyController : MonoBehaviour
    {
        public CombatTarget Target { get; private set; }
        public Health Health { get; private set; }
        public CharacterView View { get; private set; }
        public Vector2 Facing { get; private set; } = Vector2.right;
        public EnemyState State { get; private set; } = EnemyState.Patrol;
        public float Gauge { get; private set; }

        public void Init(CombatTarget target, Health health, CharacterView view, Vector2 facing)
        {
            Target = target;
            Health = health;
            View = view;
            Facing = facing.sqrMagnitude > 0f ? facing.normalized : Vector2.right;
            view.SetAim(MathUtil.AngleDeg(Facing));
        }

        public void Tick(float dt)
        {
            if (Health.IsDead) State = EnemyState.Dead;
            else View.Tick(dt, CharacterPose.Idle);
        }
    }
}
