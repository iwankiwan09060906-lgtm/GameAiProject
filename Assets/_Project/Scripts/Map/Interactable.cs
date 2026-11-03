using System.Collections.Generic;
using UnityEngine;

namespace GameAi
{
    // Space로 상호작용하는 대상(문, 이후 목표·폭탄)이 구현하는 약속
    public interface IInteractable
    {
        Vector2 Position { get; }

        // 대상이 서 있는 칸. 시야가 닿는지 검사할 때 쓴다
        Vector2Int Cell { get; }

        // 안내에 띄울 동작 이름(열기, 닫기 ...)
        string PromptAction { get; }
        bool CanInteract { get; }
        void Interact(int actorId);
    }

    // 거리 안의 가장 가까운 대상 하나를 고르고, Space를 누르면 상호작용시킨다
    public sealed class InteractionSystem
    {
        readonly GridMap grid;
        readonly float range;
        readonly List<IInteractable> items = new List<IInteractable>();

        // 지금 안내를 띄울 대상(없으면 null)
        public IInteractable Current { get; private set; }

        public InteractionSystem(GridMap grid, float range)
        {
            this.grid = grid;
            this.range = range;
        }

        public void Add(IInteractable item)
        {
            items.Add(item);
        }

        public void Tick(Vector2 actorPos, bool pressed, int actorId)
        {
            Current = FindNearest(actorPos);
            if (pressed && Current != null)
            {
                Current.Interact(actorId);

                // 상호작용 결과(열기 ↔ 닫기)가 바로 안내에 반영되도록 다시 고른다
                Current = FindNearest(actorPos);
            }
        }

        // 거리 안에서 가장 가까운, 상호작용 가능하고, 벽 너머가 아닌 대상
        IInteractable FindNearest(Vector2 actorPos)
        {
            IInteractable best = null;
            float bestDistance = range;
            foreach (var item in items)
            {
                float distance = Vector2.Distance(actorPos, item.Position);
                if (distance > bestDistance || !item.CanInteract) continue;

                // 문은 닫혀 있어도 그 칸 자체에는 닿아야 한다. 벽 너머 문은 고르지 않는다
                if (!LineOfSight.ReachesCell(grid, actorPos, item.Cell, RayMask.Sight)) continue;
                best = item;
                bestDistance = distance;
            }
            return best;
        }
    }
}
