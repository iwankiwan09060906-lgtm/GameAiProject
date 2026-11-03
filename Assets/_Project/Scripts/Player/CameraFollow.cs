using UnityEngine;

namespace GameAi
{
    // 카메라가 플레이어를 따라가며 커서 쪽으로 앞당겨 앞을 더 보여 준다(FN-PLY-01)
    public sealed class CameraFollow
    {
        readonly Transform camera;
        readonly float ratio;
        readonly float maxOffset;

        public CameraFollow(Transform camera, GameConfig config)
        {
            this.camera = camera;
            ratio = config.camera_lookahead_ratio;
            maxOffset = config.camera_lookahead_max_m;
        }

        // 플레이어에서 커서까지 거리의 ratio만큼, 최대 maxOffset까지 앞당긴 오프셋.
        // 순수 함수라 테스트하기 쉽다
        public static Vector2 LookAhead(Vector2 player, Vector2 cursor, float ratio, float maxOffset)
        {
            return Vector2.ClampMagnitude((cursor - player) * ratio, maxOffset);
        }

        public void Tick(Vector2 player, Vector2 cursor)
        {
            Vector2 position = player + LookAhead(player, cursor, ratio, maxOffset);

            // z는 그대로 둔다(2D 카메라는 뒤에서 바라본다)
            camera.position = new Vector3(position.x, position.y, camera.position.z);
        }
    }
}
