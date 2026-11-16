
using UnityEngine;

// PauseUI 테스트 전용 입력 스크립트
public class PauseUITestHotkey : MonoBehaviour
{
    private PauseUI pauseUI;
    private bool isOpen = false;

    private void Awake()
    {
        // 같은 오브젝트에 붙어 있는 PauseUI 자동 검색
        pauseUI = GetComponent<PauseUI>();

        if (pauseUI == null)
        {
            Debug.LogError(
                "[PauseUITestHotkey] 같은 오브젝트에서 PauseUI를 찾을 수 없습니다."
            );
        }
    }

    private void Update()
    {
        // PauseUI가 없으면 입력 처리 중단
        if (pauseUI == null)
            return;

        // ESC 키 입력
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            isOpen = !isOpen;
            pauseUI.SetOpen(isOpen);
        }
    }
}
