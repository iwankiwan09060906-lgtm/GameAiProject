namespace GameAi
{
    // 게임 시각(초). 대화·일시정지 중에는 GameManager가 진행하지 않는다. AI 시간 판정은 모두 이 값을 쓴다
    public static class GameClock
    {
        public static float Time { get; private set; }

        public static void Reset() { Time = 0f; }

        public static void Advance(float dt) { Time += dt; }
    }
}
