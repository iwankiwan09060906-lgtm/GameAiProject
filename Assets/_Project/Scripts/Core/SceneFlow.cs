using UnityEngine.SceneManagement;

namespace GameAi
{
    // 씬 전환과, 메뉴에서 고른 맵을 게임 씬에 넘겨 주는 역할
    public static class SceneFlow
    {
        public const string MainMenuScene = "MainMenu";
        public const string GameScene = "Game";

        // 게임 씬이 시작할 때 읽는 맵 ID. 씬이 바뀌어도 값이 유지되도록 정적으로 둔다
        public static string SelectedMapId { get; set; } = "map_01";
        // 미션 선택에서 고른 난이도(easy / normal / hard)
        public static string SelectedDifficulty { get; set; } = "normal";
        // 재현 테스트용: 고정 시드 강제(Qwen도 꺼진다)와 고정 시간 간격(0이면 실제 프레임 시간)
        public static bool ForceFixedSeed { get; set; }
        public static float FixedDeltaTime { get; set; }

        public static void LoadGame(string mapId)
        {
            SelectedMapId = mapId;
            SceneManager.LoadScene(GameScene);
        }

        // 게임 씬을 다시 불러오면 탐색 기억·적·타이머·전투 흔적이 함께 초기화된다(5.3)
        public static void LoadMainMenu()
        {
            SceneManager.LoadScene(MainMenuScene);
        }

        public static void Restart()
        {
            SceneManager.LoadScene(GameScene);
        }
    }
}
