// 역할: EnemyAgent의 판단 결과를 도형 색과 선으로 보여 주는 시연 전용 보기입니다.
// 연결: SandboxBuilder가 Agent, Visual, Facing/Clue/Shot 선을 연결합니다. SandboxController는 같은 결과를 글로 표시합니다.
// 읽는 순서: OnEnable/OnDisable(공격 알림 연결) → OnAttack(잠깐 보여 줄 좌표 저장) → LateUpdate(화면 갱신).
// 이 파일을 제거하거나 Visual을 바꿔도 Runtime의 감각·목표 선택·길찾기는 그대로 동작해야 합니다.
using UnityEngine;

namespace NullTrace.AI.Demo
{
    // AI를 조종하지 않고 읽기용 상태와 공격 요청을 표시합니다. 선은 실제 탄환이나 피해 판정이 아닙니다.
    public sealed class EnemyDebugView : MonoBehaviour
    {
        public EnemyAgent Agent;
        // 외형과 선은 선택 사항입니다. 모델을 나중에 바꿔 참조가 사라져도 아래 null 검사로 표시만 생략합니다.
        public SpriteRenderer Visual;
        public LineRenderer Facing;
        public LineRenderer Clue;
        public LineRenderer Shot;
        // 공격 선을 끌 게임 시각(초)과 마지막 요청의 XY 조준점입니다. 대상의 현재 위치를 계속 추적하지 않습니다.
        private float shotUntil;
        private Vector2 shotPosition;

        // 켜질 때 알림 함수를 등록합니다. Agent는 활성화 전에 연결하는 것이 기본 사용법입니다.
        private void OnEnable() { if (Agent) Agent.AttackRequested += OnAttack; }
        // 꺼질 때 같은 함수를 해제해야 다시 켜도 중복 구독이 남지 않습니다. 종료 시 Agent가 먼저 삭제될 수도 있습니다.
        private void OnDisable() { if (Agent) Agent.AttackRequested -= OnAttack; }
        // 요청 순간 받은 위치를 0.15초 동안 선으로 표시합니다. 이 함수는 총알 생성이나 체력 감소를 하지 않습니다.
        private void OnAttack(Vector2 position) { shotPosition = position; shotUntil = Time.time + 0.15f; }

        // 모든 Update가 끝난 뒤 Unity가 호출하므로 이번 프레임의 AI 이동·판단을 바탕으로 선을 그릴 수 있습니다.
        // 입력은 Agent의 읽기용 상태이며, 결과는 SpriteRenderer/LineRenderer의 색·좌표·표시 여부뿐입니다.
        private void LateUpdate()
        {
            if (!Agent) return;
            // 빨강=교전, 보라=수색, 노랑=경계, 초록=평시입니다. 색은 상태를 알아보기 위한 약속입니다.
            Color color = Agent.IsPathBlocked || Agent.CurrentGoal == EnemyGoal.Blocked ? Color.gray :
                Agent.IsReturning || (Agent.IsRecoveryProtected && !Agent.IsTargetIdentified) ? new Color(0.3f, 0.65f, 1f) :
                Agent.State == EnemyState.Combat ? new Color(1f, 0.25f, 0.3f) :
                Agent.State == EnemyState.Search ? new Color(0.8f, 0.5f, 1f) :
                Agent.State == EnemyState.Alert ? new Color(1f, 0.8f, 0.25f) : new Color(0.45f, 1f, 0.55f);
            if (Visual) Visual.color = color;
            Vector3 origin = Agent.transform.position;
            // 방향은 '적 위치 + 단위 방향 × 1.2 월드 단위'로 표시합니다. 이 선 길이는 실제 감지 거리가 아닙니다.
            if (Facing)
            {
                Facing.SetPosition(0, origin);
                Facing.SetPosition(1, origin + (Vector3)(Agent.FacingDirection * 1.2f));
                Facing.startColor = Facing.endColor = color;
            }
            // 기억한 마지막 단서를 보여 줍니다. 벽 뒤 플레이어의 현재 좌표를 화면에서 대신 알려 주지 않습니다.
            if (Clue)
            {
                Clue.enabled = Agent.State != EnemyState.Calm;
                Clue.SetPosition(0, origin);
                Clue.SetPosition(1, Agent.LastKnownPosition);
            }
            // 요청이 온 직후만 켜집니다. 요청이 없으면 Combat 상태여도 선은 꺼져 있을 수 있습니다.
            if (Shot)
            {
                Shot.enabled = Time.time < shotUntil;
                Shot.SetPosition(0, origin);
                Shot.SetPosition(1, shotPosition);
            }
        }
    }
}
