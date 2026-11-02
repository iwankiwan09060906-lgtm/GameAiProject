using UnityEngine;

namespace GameAi
{
    public enum MoveMode { Walk, Run, Crouch }

    // 한 프레임의 입력. 게임 로직은 키가 아니라 이 값만 본다
    public struct PlayerInputState
    {
        public Vector2 Move;
        public Vector2 AimWorld;
        public bool RunHeld;
        public bool CrouchHeld;
        public bool FireHeld;
        public bool FirePressed;
        public bool ReloadPressed;
        public bool InteractPressed;
        public bool MeleePressed;
        public int WeaponSelect; // 0이면 없음, 1~3은 무기 번호
    }

    public interface IInputSource
    {
        PlayerInputState Read(Camera camera);
    }

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

            state.AimWorld = camera.ScreenToWorldPoint(Input.mousePosition);
            state.RunHeld = Input.GetKey(KeyCode.LeftShift);
            state.CrouchHeld = Input.GetKey(KeyCode.LeftControl);
            state.FireHeld = Input.GetMouseButton(0);
            state.FirePressed = Input.GetMouseButtonDown(0);
            state.ReloadPressed = Input.GetKeyDown(KeyCode.R);
            state.InteractPressed = Input.GetKeyDown(KeyCode.Space);
            state.MeleePressed = Input.GetKeyDown(KeyCode.F);

            if (Input.GetKeyDown(KeyCode.Alpha1)) state.WeaponSelect = 1;
            else if (Input.GetKeyDown(KeyCode.Alpha2)) state.WeaponSelect = 2;
            else if (Input.GetKeyDown(KeyCode.Alpha3)) state.WeaponSelect = 3;
            return state;
        }
    }
}
