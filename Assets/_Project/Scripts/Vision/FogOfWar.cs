using UnityEngine;

namespace GameAi
{
    // 칸의 탐색 상태. 한 번도 못 봄 → 지금 보임 → 봤었지만 지금은 안 보임(기억)
    public enum FogState : byte { Unexplored, Memory, Visible }

    // 탐색 기억(FN-VIS-02). 한 번 본 칸은 기억으로 남고, 지금 보이는 칸만 Visible이다.
    // 새로 만들면 모두 Unexplored라서 재시작·사망 때 새로 만드는 것으로 "지운다"를 처리한다
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

        // 이번 프레임의 시야로 상태를 갱신한다
        public void Update(PlayerVision vision)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    int i = grid.Index(x, y);
                    if (vision.IsVisible(x, y))
                        states[i] = FogState.Visible;

                    // 방금까지 보였는데 이제 안 보이면 기억으로 바꾼다. 미탐색은 그대로 둔다
                    else if (states[i] == FogState.Visible)
                        states[i] = FogState.Memory;
                }
            }
        }

        // 텍스처에 쓸 픽셀을 채운다. 한 번이라도 본 칸(지금 보임 포함)은 회색조 반투명, 미탐색은 검정.
        // 지금 보이는 곳의 구멍은 칸 단위가 아니라 시야 메시(VisionRenderer)가 부드럽게 뚫는다
        public void WritePixels(Color32[] pixels, Color32 unexplored, Color32 memory)
        {
            for (int i = 0; i < states.Length; i++)
                pixels[i] = states[i] == FogState.Unexplored ? unexplored : memory;
        }
    }
}
