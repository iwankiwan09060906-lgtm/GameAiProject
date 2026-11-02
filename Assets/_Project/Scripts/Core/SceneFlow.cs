using UnityEngine.SceneManagement;

namespace GameAi
{
    // 씬 전환과 선택한 맵을 들고 있다
    public static class SceneFlow
    {
        public const string MainMenuScene = "MainMenu";
        public const string GameScene = "Game";

        public static string SelectedMapId { get; set; } = "map_01";

        public static void LoadGame(string mapId)
        {
            SelectedMapId = mapId;
            SceneManager.LoadScene(GameScene);
        }

        // 게임 씬을 다시 불러오면 탐색 기억·적·전투 흔적이 함께 초기화된다
        public static void Restart()
        {
            SceneManager.LoadScene(GameScene);
        }
    }
}
