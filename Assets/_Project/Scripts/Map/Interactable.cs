using System.Collections.Generic;
using UnityEngine;

namespace GameAi
{
    // Space로 상호작용하는 대상(문, 이후 목표·폭탄)
    public interface IInteractable
    {
        Vector2 Position { get; }
        Vector2Int Cell { get; }
        string PromptAction { get; }
        bool CanInteract { get; }
        void Interact(int actorId);
    }

    // 거리 안의 가장 가까운 대상 하나를 고른다
    public sealed class InteractionSystem
    {
        readonly GridMap grid;
        readonly float range;
        readonly List<IInteractable> items = new List<IInteractable>();

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
                Current = FindNearest(actorPos);
            }
        }

        IInteractable FindNearest(Vector2 actorPos)
        {
            IInteractable best = null;
            float bestDistance = range;
            foreach (var item in items)
            {
                float distance = Vector2.Distance(actorPos, item.Position);
                if (distance > bestDistance || !item.CanInteract) continue;
                if (!LineOfSight.ReachesCell(grid, actorPos, item.Cell, RayMask.Sight)) continue;
                best = item;
                bestDistance = distance;
            }
            return best;
        }
    }
}
