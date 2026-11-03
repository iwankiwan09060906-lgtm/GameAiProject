using UnityEngine;

namespace GameAi
{
    // MainMenu 씬에 놓인 임시 부트스트랩. 메뉴 화면은 Phase 7에서 만들고,
    // 그 전까지는 실행하자마자 게임 씬으로 넘어간다.
    public sealed class MainMenuBootstrap : MonoBehaviour
    {
        void Start()
        {
            SceneFlow.LoadGame(SceneFlow.SelectedMapId);
        }
    }
}
