// 역할: Utility가 고른 목표의 작은 절차를 실행한다. 이 트리에서는 목표 점수를 계산하지 않는다.
// Sequence는 순서, Selector는 가능한 가지, Repeat는 수색 지점 반복을 표현한다.
using System;

namespace NullTrace.AI
{
    public enum BehaviorStatus { Running, Success, Failure }
    public abstract class BehaviorNode
    {
        public abstract BehaviorStatus Tick(float dt);
        public virtual void Reset() { }
    }
    public sealed class BehaviorAction : BehaviorNode
    {
        private readonly Func<float, BehaviorStatus> action;
        public BehaviorAction(Func<float, BehaviorStatus> action) { this.action = action; }
        public override BehaviorStatus Tick(float dt) => action(dt);
    }
    public sealed class BehaviorSequence : BehaviorNode
    {
        private readonly BehaviorNode[] children;
        private int index;
        public BehaviorSequence(params BehaviorNode[] children) { this.children = children; }
        public override BehaviorStatus Tick(float dt)
        {
            while (index < children.Length)
            {
                var result = children[index].Tick(dt);
                if (result != BehaviorStatus.Success) return result;
                index++;
                // 한 프레임의 시간을 연속 대기 노드에 중복 사용하지 않는다.
                dt = 0;
            }
            return BehaviorStatus.Success;
        }
        public override void Reset() { index = 0; foreach (var child in children) child.Reset(); }
    }
    public sealed class BehaviorSelector : BehaviorNode
    {
        private readonly BehaviorNode[] children;
        private int index;
        public BehaviorSelector(params BehaviorNode[] children) { this.children = children; }
        public override BehaviorStatus Tick(float dt)
        {
            while (index < children.Length)
            {
                var result = children[index].Tick(dt);
                if (result != BehaviorStatus.Failure) return result;
                index++;
            }
            return BehaviorStatus.Failure;
        }
        public override void Reset() { index = 0; foreach (var child in children) child.Reset(); }
    }
    public sealed class BehaviorRepeat : BehaviorNode
    {
        private readonly BehaviorNode child;
        private readonly Func<bool> finished;
        public BehaviorRepeat(BehaviorNode child, Func<bool> finished) { this.child = child; this.finished = finished; }
        public override BehaviorStatus Tick(float dt)
        {
            if (finished()) return BehaviorStatus.Success;
            var result = child.Tick(dt);
            if (result == BehaviorStatus.Failure) return result;
            if (result == BehaviorStatus.Success) child.Reset();
            return finished() ? BehaviorStatus.Success : BehaviorStatus.Running;
        }
        public override void Reset() => child.Reset();
    }

    /// <summary>목표 후보의 점수와 사건 ID. 작은 값의 enum, 작은 사건 ID가 동점 우선순위다.</summary>
    public readonly struct EnemyCandidate
    {
        public readonly EnemyGoal Goal;
        public readonly long ClueId;
        public readonly float Score;
        public EnemyCandidate(EnemyGoal goal, long clueId, float score) { Goal = goal; ClueId = clueId; Score = score; }
        public bool Same(EnemyCandidate other) => Goal == other.Goal && ClueId == other.ClueId;
    }
    public static class EnemyUtility
    {
        /// <summary>유효한 현재 후보에만 최소 유지시간/점수 차이를 적용한다. 무효 목표는 즉시 바꾼다.</summary>
        public static EnemyCandidate Choose(System.Collections.Generic.IReadOnlyList<EnemyCandidate> candidates,
            EnemyCandidate current, bool hasCurrent, double heldSeconds, float minimumHold = 0.5f, float advantage = 10)
        {
            if (candidates.Count == 0) throw new ArgumentException("유효 후보가 필요합니다.", nameof(candidates));
            var best = candidates[0]; var liveCurrent = current; bool validCurrent = false;
            foreach (var candidate in candidates)
            {
                if (hasCurrent && candidate.Same(current)) { validCurrent = true; liveCurrent = candidate; }
                if (candidate.Score > best.Score || (candidate.Score == best.Score
                    && ((int)candidate.Goal < (int)best.Goal || (candidate.Goal == best.Goal && candidate.ClueId < best.ClueId)))) best = candidate;
            }
            if (validCurrent && (heldSeconds < minimumHold || best.Score < liveCurrent.Score + advantage || best.Score == liveCurrent.Score))
                return liveCurrent;
            return best;
        }
    }
}
