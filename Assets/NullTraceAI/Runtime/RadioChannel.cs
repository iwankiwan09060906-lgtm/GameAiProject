// 역할: 구조화된 보고를 수신자별로 지연 전달한다. 분대 전체가 즉시 읽는 공용 기억이 아니다.
// 발신자는 직접 감지한 사실만 Send한다. ReceiveRadio는 다시 Send하지 않아 전달 순환이 생기지 않는다.
using System.Collections.Generic;
using UnityEngine;

namespace NullTrace.AI
{
    [DisallowMultipleComponent]
    public sealed class RadioChannel : MonoBehaviour
    {
        public float DeliveryDelay = 0.5f;
        public float SendInterval = 0.5f;
        [Tooltip("채널 전체의 수신 대기 사본 상한. 초과 신규 보고는 버리며 이미 접수한 전달 순서는 지킵니다.")]
        public int MaxPendingDeliveries = 256;
        public int PendingCount => pending.Count;
        public int DeliveryScanCount { get; private set; }
        public int CoalescedCount { get; private set; }
        public int DroppedCount { get; private set; }
        private readonly Dictionary<EnemyAgent, int> listeners = new Dictionary<EnemyAgent, int>();
        private readonly Dictionary<EnemyAgent, double> nextSend = new Dictionary<EnemyAgent, double>();
        private readonly List<Delivery> pending = new List<Delivery>();
        private int generation;
        private double nextDeliveryAt = double.PositiveInfinity;
        private readonly struct Delivery
        {
            public readonly EnemyAgent Receiver;
            public readonly int Generation;
            public readonly double Due;
            public readonly ClueReport Report;
            public Delivery(EnemyAgent receiver, int generation, double due, ClueReport report)
            { Receiver = receiver; Generation = generation; Due = due; Report = report; }
        }
        public void Register(EnemyAgent agent)
        { if (agent && !listeners.ContainsKey(agent)) listeners.Add(agent, ++generation); }
        public void Unregister(EnemyAgent agent)
        {
            if (ReferenceEquals(agent, null)) return;
            listeners.Remove(agent); nextSend.Remove(agent);
            pending.RemoveAll(item => item.Receiver == agent);
        }
        public bool Send(EnemyAgent sender, ClueReport report, double gameTime)
        {
            if (!isActiveAndEnabled || !sender || !listeners.ContainsKey(sender) || !report.IsValid(gameTime)
                || (nextSend.TryGetValue(sender, out double next) && gameTime < next)) return false;
            // 이미 도착할 시각인 보고는 먼저 전달한다. 다음 재전송이 먼저 와 기존 전달을 계속 미루지 않는다.
            Deliver(gameTime);
            float delay = AiMath.IsFinite(DeliveryDelay) ? Mathf.Max(0, DeliveryDelay) : 0.5f;
            float interval = AiMath.IsFinite(SendInterval) ? Mathf.Max(0.05f, SendInterval) : 0.5f;
            nextSend[sender] = gameTime + interval;
            foreach (var pair in listeners)
            {
                if (!pair.Key || !pair.Key.isActiveAndEnabled || pair.Key == sender) continue;
                // 같은 수신자의 같은 사건은 처음 접수한 관측/전달 시각 한 개만 보존한다.
                // 최신 관측으로 바꾸고 옛 도착 시각을 쓰면 새 정보가 0.5초보다 빨리 전달되는 문제가 생긴다.
                // 다음 송신 기회에는 최신 순번을 다시 보낼 수 있다. 전역 기억을 대신 읽는 것은 아니다.
                if (pending.Exists(item => item.Receiver == pair.Key && item.Generation == pair.Value && item.Report.Id == report.Id))
                { CoalescedCount++; continue; }
                if (pending.Count >= Mathf.Clamp(MaxPendingDeliveries, 1, 4096)) { DroppedCount++; continue; }
                double due = gameTime + delay;
                pending.Add(new Delivery(pair.Key, pair.Value, due, report));
                nextDeliveryAt = System.Math.Min(nextDeliveryAt, due);
            }
            return true;
        }
        private void Update() => Deliver(Time.timeAsDouble);
        /// <summary>수동 테스트에서는 모든 NPC와 같은 게임 시각을 준다. 일시정지 중에는 진행하지 않는다.</summary>
        public void Deliver(double gameTime)
        {
            if (!isActiveAndEnabled || double.IsNaN(gameTime) || double.IsInfinity(gameTime)) return;
            int limit = Mathf.Clamp(MaxPendingDeliveries, 1, 4096);
            if (pending.Count > limit)
            {
                DroppedCount += pending.Count - limit;
                pending.RemoveRange(limit, pending.Count - limit);
            }
            // 한 채널을 여러 NPC가 같은 프레임에 호출해도, 아직 도착할 것이 없으면 O(1)로 끝난다.
            // 새 0초 지연 보고가 추가된 경우만 같은 시각에 다시 실제 전달 검사가 필요하다.
            if (pending.Count == 0 || gameTime < nextDeliveryAt) return;
            DeliveryScanCount++;
            nextDeliveryAt = double.PositiveInfinity;
            // 등록·송신 순서를 유지한다. 같은 ID의 이전/최신 보고가 섞여도 수신자가 순번과 시각을 검사한다.
            for (int i = 0; i < pending.Count;)
            {
                var item = pending[i];
                if (gameTime < item.Due) { nextDeliveryAt = System.Math.Min(nextDeliveryAt, item.Due); i++; continue; }
                pending.RemoveAt(i);
                if (item.Receiver && item.Receiver.isActiveAndEnabled
                    && listeners.TryGetValue(item.Receiver, out int version) && version == item.Generation)
                    item.Receiver.ReceiveRadio(item.Report, gameTime);
            }
        }
        private void OnDisable() { pending.Clear(); nextSend.Clear(); nextDeliveryAt = double.PositiveInfinity; }
    }
}
