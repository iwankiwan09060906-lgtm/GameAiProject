using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace GameAi
{
    // 문 하나. 닫힘은 이동·시야·총알·빛을 막고, 열림은 모두 통과시킨다(FN-MAP-02).
    // 열림 여부의 진짜 값은 GridMap에 있고, 이 클래스는 그림과 그림자만 맞춘다.
    public sealed class Door : MonoBehaviour, IInteractable
    {
        // 열린 문 막대를 칸 가장자리로 옮기는 거리(m)
        const float BarOffset = 0.44f;

        GridMap grid;

        // 열린 문 칸에 서 있는 대상을 찾는 데 쓴다
        IReadOnlyList<CombatTarget> occupants;
        SpriteRenderer view;
        ShadowCaster2D shadow;
        Sprite horizontalBar;
        Sprite verticalBar;

        // 양옆이 벽이면(복도를 가로막는 문) 가로 막대로 닫힌다
        bool isHorizontal;

        // 화면에 마지막으로 보여 준 상태. 시야 밖에서는 실제 상태가 바뀌어도 이 값을 유지한다(FN-VIS-03)
        bool shownOpen;

        public int Id { get; private set; }
        public Vector2Int Cell { get; private set; }
        public Vector2 Position { get { return GridMap.CellCenter(Cell); } }
        public bool IsOpen { get { return grid.IsDoorOpen(Cell.x, Cell.y); } }
        public string PromptAction { get { return IsOpen ? "닫기" : "열기"; } }

        // 열린 문 칸에 살아 있는 대상이 서 있으면 닫지 못한다
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

            // 일부러 "열림"으로 시작해 첫 RefreshView가 닫힌 실제 상태로 바꾸게 한다
            shownOpen = true;
            RefreshView(true);
        }

        bool IsWall(int x, int y)
        {
            return grid.GetTile(x, y) == TileType.Wall;
        }

        // 문 칸과 겹치는 살아 있는 대상이 있는지. 시체는 막지 않는다.
        // foreach 대신 인덱스로 순회해 매 프레임 호출돼도 메모리를 할당하지 않는다
        bool IsOccupied()
        {
            for (int i = 0; i < occupants.Count; i++)
            {
                CombatTarget target = occupants[i];
                if (!target.Alive) continue;
                Vector2 closest = MathUtil.ClosestPointOnCell(target.Position, Cell.x, Cell.y);
                if ((closest - target.Position).sqrMagnitude < target.Radius * target.Radius)
                    return true;
            }
            return false;
        }

        // 열림 ↔ 닫힘 전환. 격자 값, 그림자, 그림을 바꾸고 DoorChangedEvent를 보낸다
        public void Interact(int actorId)
        {
            bool open = !IsOpen;
            grid.SetDoorOpen(Cell.x, Cell.y, open);

            // 열린 문은 빛을 막지 않으므로 그림자 상자를 끈다
            shadow.enabled = !open;

            // 플레이어가 직접 연 문은 방금 본 것이니 바로 그림을 바꾼다
            RefreshView(actorId == EntityIds.Player);
            EventBus.Publish(new DoorChangedEvent { doorId = Id, isOpen = open, actorId = actorId, time = Time.time });
        }

        // 보이는 문만 실제 상태로 그림을 맞춘다. 시야 밖 문은 마지막으로 본 모습을 유지한다
        public void RefreshView(bool visible)
        {
            if (!visible || shownOpen == IsOpen) return;
            shownOpen = IsOpen;

            // 가로 문이 닫히면 가로 막대, 열리면 세로 막대로 돌아간 모습(세로 문은 반대)
            bool bar = isHorizontal != shownOpen;
            view.sprite = bar ? horizontalBar : verticalBar;

            // 열린 문은 칸 한쪽 가장자리에 붙여 그린다
            Vector2 center = Position;
            if (shownOpen)
                center += isHorizontal ? Vector2.left * BarOffset : Vector2.up * BarOffset;
            view.transform.position = center;
        }
    }
}
