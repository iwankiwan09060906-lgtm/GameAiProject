using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace GameAi
{
    // 조명 스위치(C): 플레이어가 Space로 같은 스위치에 묶인 맵 조명들을 끄고 켠다. 화면 조명과 논리 밝기에서 함께 빠진다
    public sealed class LightSwitch : IInteractable
    {
        readonly LightGrid lightGrid;
        readonly List<int> lightIds = new List<int>();
        readonly List<Light2D> lamps = new List<Light2D>();

        public bool IsOn { get; private set; } = true;
        public Vector2 Position { get; }
        public RectInt Area { get; }
        public string PromptAction { get { return IsOn ? "조명 끄기" : "조명 켜기"; } }
        public bool CanInteract { get { return true; } }
        public IReadOnlyList<int> LightIds { get { return lightIds; } }

        public LightSwitch(GridMap grid, Vector2 position, LightGrid lightGrid)
        {
            Position = position;
            Vector2Int cell = grid.WorldToCell(position);
            Area = new RectInt(cell.x, cell.y, 1, 1);
            this.lightGrid = lightGrid;
        }

        public void Add(int lightId, Light2D lamp)
        {
            lightIds.Add(lightId);
            lamps.Add(lamp);
        }

        public void Interact(int actorId)
        {
            IsOn = !IsOn;
            for (int i = 0; i < lightIds.Count; i++)
            {
                if (lamps[i] != null) lamps[i].enabled = IsOn;
                lightGrid.SetLight(lightIds[i], IsOn);
                EventBus.Publish(new LightChangedEvent { lightId = lightIds[i], isOn = IsOn, actorId = actorId, time = GameClock.Time });
            }
        }
    }
}
