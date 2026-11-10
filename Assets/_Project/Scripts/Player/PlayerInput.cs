using UnityEngine;

namespace GameAi
{
    // 이동 모드. 속도·소음·적의 감지 배율이 모드마다 다르다(7.2)
    public enum MoveMode { Walk, Run, Crouch }

    // 한 프레임의 입력 모음. 게임 로직은 키가 아니라 이 값만 본다.
    // 덕분에 키보드 대신 자동 재생(SmokeRunner)이나 입력 기록을 끼워 넣을 수 있다
    public struct PlayerInputState
    {
        // WASD 방향(정규화 전). 대각선이면 길이가 1보다 크다
        public Vector2 Move;

        // 마우스 커서의 월드 좌표
        public Vector2 AimWorld;
        public bool RunHeld;
        public bool CrouchHeld;

        // 사격 버튼: Held는 누르는 동안(연사), Pressed는 누른 프레임 한 번(단발)
        public bool FireHeld;
        public bool FirePressed;
        public bool ReloadPressed;
        public bool InteractPressed;
        // 폭탄 해체처럼 누르고 있는 동안
        public bool InteractHeld;
        // 유인탄(G)·야간 투시(N)
        public bool DecoyPressed;
        public bool NightVisionPressed;
        public bool MeleePressed;

        // 0이면 선택 없음, 1~3은 무기 번호
        public int WeaponSelect;
    }

    // 입력을 읽어 주는 쪽의 약속. 실제 키보드나 자동 재생이 이것을 구현한다
    public interface IInputSource
    {
        PlayerInputState Read(Camera camera);
    }

    // 레거시 UnityEngine.Input로 실제 키보드·마우스를 읽는다(CLAUDE.md: 입력은 레거시, 상호작용은 Space)
    public sealed class LegacyInputSource : IInputSource
    {
        public PlayerInputState Read(Camera camera)
        {
            var state = new PlayerInputState();

            var move = Vector2.zero;
            if (Input.GetKey(KeyCode.A)) move.x -= 1f;
            if (Input.GetKey(KeyCode.D)) move.x += 1f;
            if (Input.GetKey(KeyCode.S)) move.y -= 1f;
            if (Input.GetKey(KeyCode.W)) move.y += 1f;
            state.Move = move;

            // 화면 좌표의 마우스를 월드 좌표로 바꾼다
            state.AimWorld = camera.ScreenToWorldPoint(Input.mousePosition);
            state.RunHeld = Input.GetKey(KeyCode.LeftShift);
            state.CrouchHeld = Input.GetKey(KeyCode.LeftControl);
            state.FireHeld = Input.GetMouseButton(0);
            state.FirePressed = Input.GetMouseButtonDown(0);
            state.ReloadPressed = Input.GetKeyDown(KeyCode.R);
            state.InteractPressed = Input.GetKeyDown(KeyCode.Space);
            state.InteractHeld = Input.GetKey(KeyCode.Space);
            state.DecoyPressed = Input.GetKeyDown(KeyCode.G);
            state.NightVisionPressed = Input.GetKeyDown(KeyCode.N);
            state.MeleePressed = Input.GetKeyDown(KeyCode.F);

            if (Input.GetKeyDown(KeyCode.Alpha1)) state.WeaponSelect = 1;
            else if (Input.GetKeyDown(KeyCode.Alpha2)) state.WeaponSelect = 2;
            else if (Input.GetKeyDown(KeyCode.Alpha3)) state.WeaponSelect = 3;
            return state;
        }
    }
}
