using UnityEngine;

namespace GameAi
{
    // 적 한 명. Phase 3까지는 제자리에 서 있는 허수아비다.
    // 시야·소리 감지, 상태 전환, 이동, 사격은 Phase 5에서 이 클래스에 붙인다
    public sealed class EnemyController : MonoBehaviour
    {
        public CombatTarget Target { get; private set; }
        public Health Health { get; private set; }
        public CharacterView View { get; private set; }

        // 바라보는 방향. 근접 제압이 "뒤쪽"을 판정하는 기준이다
        public Vector2 Facing { get; private set; } = Vector2.right;
        public EnemyState State { get; private set; } = EnemyState.Patrol;

        // 인지 게이지(0~100). 허수아비는 아무것도 못 보므로 항상 0
        public float Gauge { get; private set; }

        public void Init(CombatTarget target, Health health, CharacterView view, Vector2 facing)
        {
            Target = target;
            Health = health;
            View = view;
            Facing = facing.sqrMagnitude > 0f ? facing.normalized : Vector2.right;
            view.SetAim(MathUtil.AngleDeg(Facing));
        }

        // 3~5단계 자리. 지금은 표시만 갱신한다
        public void Tick(float dt)
        {
            if (Health.IsDead) State = EnemyState.Dead;
            else View.Tick(dt, CharacterPose.Idle);
        }
    }
}
