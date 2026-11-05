// 역할: 팀 문과 AI 격자를 잇는 최소 연결부. 문 상태가 바뀐 뒤 지도 버전과 알림을 함께 갱신한다.
// 계획에서는 열 수 있는 문을 후보로 쓰고, 실제 접근 때만 Collider2D를 끈다. 잠금은 개방보다 우선한다.
using System;
using UnityEngine;

namespace NullTrace.AI
{
    [DisallowMultipleComponent]
    public sealed class AiDoor2D : MonoBehaviour
    {
        [Tooltip("이 문이 영향을 주는 AI 격자. 문 Collider의 레이어가 World.ObstacleMask에 있어야 합니다.")]
        public GridWorld2D World;
        [Tooltip("문 한 개의 독립된 비 Trigger Collider2D. 같은 오브젝트가 아니어도 직접 연결할 수 있습니다.")]
        public Collider2D DoorCollider;
        [SerializeField, Tooltip("초기 잠금. 실행 중 변경은 TrySetLocked를 사용하세요.")]
        private bool locked;
        [SerializeField, Tooltip("잠기지 않은 문을 NPC가 열 수 있는가. 실행 중 변경은 SetNpcCanOpen을 사용하세요.")]
        private bool npcCanOpen = true;

        // 실제 충돌체 상태가 기준이다. 팀 코드가 직접 enabled를 바꾸지 말고 아래 API를 사용해야 지도와 일치한다.
        public bool IsOpen => DoorCollider && !DoorCollider.enabled;
        public bool IsLocked => locked;
        public bool NpcCanOpen => npcCanOpen;
        public bool CanNpcOpen => !locked && npcCanOpen && ValidateConfiguration(out _);
        // 상태·물리·Revision을 모두 반영한 뒤 호출한다. 팀의 소리·표시를 연결하는 알림이며 개방 승인 요청은 아니다.
        public event Action<AiDoor2D> StateChanged;

        private void Reset() => DoorCollider = GetComponent<Collider2D>();
        private void OnEnable()
        {
            if (!ValidateConfiguration(out _)) return;
            // 초기 Collider가 꺼진 채 잠금이 지정됐다면 안전하게 닫기부터 시도한다.
            // NPC가 끼어 있으면 잠금을 적용하지 않는다. '잠겼지만 통과되는 문'으로 표시하지 않기 위해서다.
            if (locked && IsOpen) { locked = false; TrySetLocked(true); }
            World.Rebuild();
        }
        private void OnDisable()
        {
            // 연결부를 끄면 닫힌 문은 다시 일반 장애물이다. 이미 열린 문을 강제로 닫지는 않는다.
            if (World && World.isActiveAndEnabled) World.Rebuild();
        }

        public bool ValidateConfiguration(out string error)
        {
            error = null;
            if (!isActiveAndEnabled) error = "AiDoor2D가 비활성 상태입니다.";
            else if (!World || !DoorCollider) error = "문에 World와 DoorCollider를 연결하세요.";
            else if (!DoorCollider.gameObject.activeInHierarchy || DoorCollider.isTrigger)
                error = "문 Collider는 활성 오브젝트의 비 Trigger 충돌체여야 합니다.";
            else if (DoorCollider is CompositeCollider2D || DoorCollider.compositeOperation != Collider2D.CompositeOperation.None)
                error = "문은 벽과 합쳐지지 않은 독립 Collider2D여야 합니다.";
            else if (DoorCollider.attachedRigidbody && !DoorCollider.attachedRigidbody.simulated)
                error = "문 Rigidbody2D의 Simulated를 켜세요.";
            else if ((World.ObstacleMask.value & (1 << DoorCollider.gameObject.layer)) == 0)
                error = "문 레이어가 World.ObstacleMask에 포함되어 있지 않습니다.";
            return error == null;
        }

        /// <summary>
        /// 팀 문 조작용 API. true면 요청 상태다. 잠긴 문 열기 또는 NPC가 낀 문 닫기는 false로 거부한다.
        /// NPC는 이 함수를 먼 거리에서 직접 호출하지 말고 World.TryOpenDoorAhead를 사용한다.
        /// </summary>
        public bool TrySetOpen(bool open)
        {
            if (!ValidateConfiguration(out _) || (open && locked)) return false;
            if (IsOpen == open) return true;
            if (!open && !CanCloseWithoutNpc()) return false;
            DoorCollider.enabled = !open;
            Changed();
            return true;
        }

        /// <summary>
        /// 잠글 때는 닫힘까지 함께 적용한다. NPC 때문에 닫을 수 없으면 잠금과 물리 상태를 모두 보존한다.
        /// 잠금 해제는 문을 자동으로 열지 않으며, 다음 계획에서 개방 가능한 후보로만 돌아온다.
        /// </summary>
        public bool TrySetLocked(bool value)
        {
            if (!ValidateConfiguration(out _)) return false;
            if (value && IsOpen && !CanCloseWithoutNpc()) return false;
            if (locked == value && (!value || !IsOpen)) return true;
            if (value) DoorCollider.enabled = true;
            locked = value;
            Changed();
            return true;
        }

        public void SetNpcCanOpen(bool allowed)
        {
            if (npcCanOpen == allowed) return;
            npcCanOpen = allowed;
            Changed();
        }

        private void Changed()
        {
            // Rebuild 내부에서 SyncTransforms까지 처리한 뒤 알리므로 수신자는 새 지도/충돌 상태를 읽는다.
            if (World) World.Rebuild();
            StateChanged?.Invoke(this);
        }

        private bool CanCloseWithoutNpc()
        {
            bool wasEnabled = DoorCollider.enabled;
            try
            {
                // 꺼진 Collider의 ClosestPoint는 입력 좌표 그대로를 반환한다. 검사 동안만 켜서 실제 문 윤곽을 쓴다.
                // 같은 메인 스레드 호출 안에서 조회 후 원복하며 물리 시뮬레이션이나 NPC 이동을 실행하지 않는다.
                DoorCollider.enabled = true;
                Physics2D.SyncTransforms();
                // 닫기 요청 때만 조회한다. 움직임/경로 간선마다 NPC 목록을 검색하지 않는다.
                foreach (var agent in FindObjectsByType<EnemyAgent>(FindObjectsSortMode.None))
                {
                    Vector2 center = agent.transform.position;
                    float radius = agent.World ? agent.World.AgentRadius : World.AgentRadius;
                    if (!AiMath.IsFinite(center) || !AiMath.IsFinite(radius) || radius <= 0f) return false;
                    // 다른 월드의 NPC와 비활성 행동 컴포넌트도 활성 오브젝트라면 몸이 남아 있다. 겹치면 닫지 않는다.
                    // 반지름에 작은 여유를 더해 문에 정확히 닿은 몸을 끼워 넣지 않는다.
                    if ((DoorCollider.ClosestPoint(center) - center).sqrMagnitude <= (radius + 0.002f) * (radius + 0.002f))
                        return false;
                }
                return true;
            }
            finally
            {
                DoorCollider.enabled = wasEnabled;
                Physics2D.SyncTransforms();
            }
        }
    }
}
