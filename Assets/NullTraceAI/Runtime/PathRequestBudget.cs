// 역할: 같은 월드의 적들이 한 프레임에 모두 A*를 계산하지 않도록 순서를 나눠 준다.
// 연결: EnemyAgent.Move가 계산 직전에 신청하고, 행동 취소 시 자기 신청을 취소한다.
// 기다리는 것은 '길 없음'이 아니다. 이 대기 때문에 실패 횟수나 복귀 완료 여부를 바꾸지 않는다.
using System.Collections.Generic;
using UnityEngine;

namespace NullTrace.AI
{
    [DisallowMultipleComponent, RequireComponent(typeof(GridWorld2D))]
    public sealed class PathRequestBudget : MonoBehaviour
    {
        [Min(1), Tooltip("한 프레임에 이 월드에서 시작할 경로 계산 수입니다. 시간(ms) 보장은 아닙니다.")]
        public int RequestsPerFrame = 4;

        // 연결 목록과 표를 함께 쓰면 중복 신청을 찾거나 중간 신청을 취소할 때 전체 줄을 훑지 않는다.
        private readonly LinkedList<EnemyAgent> waiting = new LinkedList<EnemyAgent>();
        private readonly Dictionary<EnemyAgent, LinkedListNode<EnemyAgent>> tickets = new Dictionary<EnemyAgent, LinkedListNode<EnemyAgent>>();
        private long currentTick = long.MinValue;
        private GridWorld2D world;
        public int PendingCount => waiting.Count;
        public int RequestsThisTick { get; private set; }

        /// <summary>
        /// 먼저 신청한 적에게 먼저 계산 차례를 준다. 차례가 아니면 다음 프레임에 다시 묻는다.
        /// tick은 실제 실행에서는 Time.frameCount, 수동 테스트에서는 같은 60Hz 모의 시간표다.
        /// 서로 다른 시간을 넘겨 예산을 초기화하지 않도록 한 월드의 수동 Tick은 같은 간격으로 진행한다.
        /// </summary>
        public bool TryAcquire(EnemyAgent agent, long tick)
        {
            if (!world) world = GetComponent<GridWorld2D>();
            if (!isActiveAndEnabled || !agent || !agent.isActiveAndEnabled || !agent.IsAlive || agent.World != world) return false;
            if (tick != currentTick) { currentTick = tick; RequestsThisTick = 0; }
            if (!tickets.ContainsKey(agent)) tickets.Add(agent, waiting.AddLast(agent));

            // 풀로 돌아갔거나 사라진 적이 맨 앞에 남아 뒤의 적들을 계속 막지 않게 한다.
            while (waiting.First != null)
            {
                var first = waiting.First.Value;
                if (first && first.isActiveAndEnabled && first.IsAlive && first.World == world) break;
                tickets.Remove(first); waiting.RemoveFirst();
            }
            if (waiting.First == null || waiting.First.Value != agent || RequestsThisTick >= Mathf.Clamp(RequestsPerFrame, 1, 64)) return false;
            Cancel(agent);
            RequestsThisTick++;
            return true;
        }

        public void Cancel(EnemyAgent agent)
        {
            // 파괴된 Unity 오브젝트도 C# 참조는 남을 수 있어 ReferenceEquals로 실제 null만 제외한다.
            if (ReferenceEquals(agent, null) || !tickets.TryGetValue(agent, out var ticket)) return;
            waiting.Remove(ticket); tickets.Remove(agent);
        }

        private void OnDisable()
        { waiting.Clear(); tickets.Clear(); currentTick = long.MinValue; RequestsThisTick = 0; }
    }
}
