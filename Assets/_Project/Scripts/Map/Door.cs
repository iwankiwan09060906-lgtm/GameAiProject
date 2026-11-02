using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace GameAi
{
    // 닫힘은 이동·시야·총알·빛을 막고, 열림은 모두 통과시킨다
    public sealed class Door : MonoBehaviour, IInteractable
    {
        const float BarOffset = 0.44f;

        GridMap grid;
        IReadOnlyList<CombatTarget> occupants;
        SpriteRenderer view;
        ShadowCaster2D shadow;
        Sprite horizontalBar;
        Sprite verticalBar;
        bool isHorizontal;
        bool shownOpen;

        public int Id { get; private set; }
        public Vector2Int Cell { get; private set; }
        public Vector2 Position { get { return GridMap.CellCenter(Cell); } }
        public bool IsOpen { get { return grid.IsDoorOpen(Cell.x, Cell.y); } }
        public string PromptAction { get { return IsOpen ? "닫기" : "열기"; } }

        // 열린 문 칸에 누가 서 있으면 닫지 못한다
        public bool CanInteract { get { return !IsOpen || !IsOccupied(); } }

        public void Init(int id, Vector2Int cell, GridMap map, IReadOnlyList<CombatTarget> targets,
            SpriteRenderer renderer, ShadowCaster2D caster, Sprite horizontal, Sprite vertical)
        {
            Id = id;
            Cell = cell;
            grid = map;
            occupants = targets;
            view = renderer;
            shadow = caster;
            horizontalBar = horizontal;
            verticalBar = vertical;
            isHorizontal = IsWall(cell.x - 1, cell.y) || IsWall(cell.x + 1, cell.y);
            shownOpen = true;
            RefreshView(true);
        }

        bool IsWall(int x, int y)
        {
            return grid.GetTile(x, y) == TileType.Wall;
        }

        bool IsOccupied()
        {
            foreach (var target in occupants)
            {
                if (!target.Alive) continue;
                Vector2 closest = MathUtil.ClosestPointOnCell(target.Position, Cell.x, Cell.y);
                if ((closest - target.Position).sqrMagnitude < target.Radius * target.Radius)
                    return true;
            }
            return false;
        }

        public void Interact(int actorId)
        {
            bool open = !IsOpen;
            grid.SetDoorOpen(Cell.x, Cell.y, open);
            shadow.enabled = !open;
            RefreshView(actorId == EntityIds.Player);
            EventBus.Publish(new DoorChangedEvent { doorId = Id, isOpen = open, actorId = actorId, time = Time.time });
        }

        // 시야 밖 문은 마지막으로 본 모습을 유지한다(FN-VIS-03)
        public void RefreshView(bool visible)
        {
            if (!visible || shownOpen == IsOpen) return;
            shownOpen = IsOpen;
            bool bar = isHorizontal != shownOpen;
            view.sprite = bar ? horizontalBar : verticalBar;
            Vector2 center = Position;
            if (shownOpen)
                center += isHorizontal ? Vector2.left * BarOffset : Vector2.up * BarOffset;
            view.transform.position = center;
        }
    }
}
