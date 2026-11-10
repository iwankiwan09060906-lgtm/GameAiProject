using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace GameAi
{
    // 조명 스위치(C): 플레이어가 Space로 맵 조명 하나를 끄고 켠다. 화면 조명과 논리 밝기에서 함께 빠진다
    public sealed class LightSwitch : IInteractable
    {
        readonly LightGrid lightGrid;
        readonly Light2D lamp;
        readonly int lightId;

        public bool IsOn { get; private set; } = true;
        public Vector2 Position { get; }
        public RectInt Area { get; }
        public string PromptAction { get { return IsOn ? "조명 끄기" : "조명 켜기"; } }
        public bool CanInteract { get { return true; } }

        public LightSwitch(GridMap grid, Vector2 position, int lightId, LightGrid lightGrid, Light2D lamp)
        {
            Position = position;
            Vector2Int cell = grid.WorldToCell(position);
            Area = new RectInt(cell.x, cell.y, 1, 1);
            this.lightId = lightId;
            this.lightGrid = lightGrid;
            this.lamp = lamp;
        }

        public void Interact(int actorId)
        {
            IsOn = !IsOn;
            if (lamp != null) lamp.enabled = IsOn;
            lightGrid.SetLight(lightId, IsOn);
            EventBus.Publish(new LightChangedEvent { lightId = lightId, isOn = IsOn, actorId = actorId, time = GameClock.Time });
        }
    }
}
