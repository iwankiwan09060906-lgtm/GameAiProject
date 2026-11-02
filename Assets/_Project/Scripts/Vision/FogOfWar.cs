using UnityEngine;

namespace GameAi
{
    public enum FogState : byte { Unexplored, Memory, Visible }

    // 탐색 기억. 한 번 본 칸은 기억으로 남고, 지금 보이는 칸만 Visible이다
    public sealed class FogOfWar
    {
        readonly GridMap grid;
        readonly FogState[] states;

        public FogOfWar(GridMap grid)
        {
            this.grid = grid;
            states = new FogState[grid.Width * grid.Height];
        }

        public FogState GetState(int x, int y)
        {
            return states[grid.Index(x, y)];
        }

        public void Update(PlayerVision vision)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    int i = grid.Index(x, y);
                    if (vision.IsVisible(x, y))
                        states[i] = FogState.Visible;
                    else if (states[i] == FogState.Visible)
                        states[i] = FogState.Memory;
                }
            }
        }

        public void WritePixels(Color32[] pixels, Color32 unexplored, Color32 memory)
        {
            var clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < states.Length; i++)
            {
                switch (states[i])
                {
                    case FogState.Visible: pixels[i] = clear; break;
                    case FogState.Memory: pixels[i] = memory; break;
                    default: pixels[i] = unexplored; break;
                }
            }
        }
    }
}
