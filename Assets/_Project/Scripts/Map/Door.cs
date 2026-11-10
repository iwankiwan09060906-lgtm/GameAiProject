using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace GameAi
{
    // 문 하나. 닫힘은 이동·시야·총알·빛을 막고, 열림은 모두 통과시킨다(FN-MAP-02).
    // 열림 여부의 진짜 값은 GridMap에 있고, 이 클래스는 그림과 그림자만 맞춘다.
    // 칸이 작은 맵(cell_m 0.5)에서는 이어진 D 칸 여러 개가 문 하나가 된다
    public sealed class Door : MonoBehaviour, IInteractable
    {
        // 열린 문 막대를 문 끝에서 안쪽으로 들이는 거리(m)
        const float BarInset = 0.06f;

        // shortcut: 두께·열리는 시간은 상수, 튜닝이 잦아지면 Config JSON으로 옮긴다
        const float Thickness = 0.1f;
        const float OpenSeconds = 0.5f;

        // 0 = 닫힘, 1 = 활짝 열림. 화면에 보이는 문의 진행 정도
        float progress;

        GridMap grid;

        // 열린 문 칸에 서 있는 대상을 찾는 데 쓴다
        IReadOnlyList<CombatTarget> occupants;
        SpriteRenderer view;
        ShadowCaster2D shadow;
        Sprite horizontalBar;
        Sprite verticalBar;

        // 가로로 놓인 문(복도를 가로막는 문)이면 가로 막대로 닫힌다
        bool isHorizontal;

        // 문 폭(m). 막대 그림을 이 길이로 늘린다
        float length;

        // 화면에 마지막으로 보여 준 상태. 시야 밖에서는 실제 상태가 바뀌어도 이 값을 유지한다(FN-VIS-03)
        bool shownOpen;

        public int Id { get; private set; }
        public RectInt Area { get; private set; }
        public Vector2 Position { get { return grid.AreaCenter(Area); } }
        public bool IsOpen { get { return grid.IsDoorOpen(Area.x, Area.y); } }
        public string PromptAction { get { return IsOpen ? "닫기" : "열기"; } }

        // 열린 문 칸에 살아 있는 대상이 서 있으면 닫지 못한다
        public bool CanInteract { get { return !IsOpen || !IsOccupied(); } }

        public void Init(int id, RectInt area, GridMap map, IReadOnlyList<CombatTarget> targets,
            SpriteRenderer renderer, ShadowCaster2D caster, Sprite horizontal, Sprite vertical)
        {
            Id = id;
            Area = area;
            grid = map;
            occupants = targets;
            view = renderer;
            shadow = caster;
            horizontalBar = horizontal;
            verticalBar = vertical;

            // 여러 칸이면 긴 쪽 방향, 한 칸이면 양옆이 벽인지로 정한다
            if (area.width != area.height) isHorizontal = area.width > area.height;
            else isHorizontal = IsWall(area.x - 1, area.y) || IsWall(area.xMax, area.y);
            length = Mathf.Max(area.width, area.height) * map.CellSize;

            shownOpen = IsOpen;
            progress = shownOpen ? 1f : 0f;
            ApplyView();
        }

        bool IsWall(int x, int y)
        {
            return grid.GetTile(x, y) == TileType.Wall;
        }

        // 문 칸과 겹치는 살아 있는 대상이 있는지. 시체는 막지 않는다.
        // foreach 대신 인덱스로 순회해 매 프레임 호출돼도 메모리를 할당하지 않는다
        bool IsOccupied()
        {
            Rect rect = grid.AreaRect(Area);
            for (int i = 0; i < occupants.Count; i++)
            {
                CombatTarget target = occupants[i];
                if (!target.Alive) continue;
                var closest = new Vector2(Mathf.Clamp(target.Position.x, rect.xMin, rect.xMax), Mathf.Clamp(target.Position.y, rect.yMin, rect.yMax));
                if ((closest - target.Position).sqrMagnitude < target.Radius * target.Radius)
                    return true;
            }
            return false;
        }

        // 열림 ↔ 닫힘 전환. 격자 값, 그림자, 그림을 바꾸고 DoorChangedEvent를 보낸다
        public void Interact(int actorId)
        {
            bool open = !IsOpen;
            for (int y = Area.yMin; y < Area.yMax; y++)
                for (int x = Area.xMin; x < Area.xMax; x++)
                    grid.SetDoorOpen(x, y, open);

            // 열린 문은 빛을 막지 않으므로 그림자 상자를 끈다
            shadow.enabled = !open;

            // 플레이어가 직접 연 문은 방금 본 것이니 바로 그림을 바꾼다
            RefreshView(actorId == EntityIds.Player);
            EventBus.Publish(new DoorChangedEvent { doorId = Id, isOpen = open, actorId = actorId, time = Time.time });
        }

        // 보이는 문만 목표 상태를 실제 상태로 맞춘다. 시야 밖 문은 마지막으로 본 모습을 유지한다
        public void RefreshView(bool visible)
        {
            if (visible) shownOpen = IsOpen;
        }

        // 열림 정도(progress)를 목표 쪽으로 서서히 옮기고 그림에 반영한다
        public void Tick(float dt)
        {
            float target = shownOpen ? 1f : 0f;
            if (Mathf.Approximately(progress, target)) return;
            progress = Mathf.MoveTowards(progress, target, dt / OpenSeconds);
            ApplyView();
        }

        // 닫힘 자세에서 열림 자세로 막대를 회전·이동시킨다(가로 문은 0°→90°, 세로 문은 90°→0°)
        void ApplyView()
        {
            view.sprite = horizontalBar;
            Vector2 size = view.sprite != null ? (Vector2)view.sprite.bounds.size : Vector2.one;
            view.transform.localScale = new Vector3(length / Mathf.Max(0.01f, size.x), Thickness / Mathf.Max(0.01f, size.y), 1f);

            float closedAngle = isHorizontal ? 0f : 90f;
            view.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(closedAngle, 90f - closedAngle, progress));

            // 열린 문은 한쪽 끝(경첩)에 붙여 그린다
            Vector2 openCenter = Position + (isHorizontal ? Vector2.left : Vector2.up) * (length / 2f - BarInset);
            view.transform.position = Vector2.Lerp(Position, openCenter, progress);
        }
    }
}
