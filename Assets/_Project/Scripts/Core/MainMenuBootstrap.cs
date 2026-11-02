using UnityEngine;

namespace GameAi
{
    // Phase 7에서 메뉴가 생기기 전까지 바로 게임으로 넘어간다
    public sealed class MainMenuBootstrap : MonoBehaviour
    {
        void Start()
        {
            SceneFlow.LoadGame(SceneFlow.SelectedMapId);
        }
    }
}
