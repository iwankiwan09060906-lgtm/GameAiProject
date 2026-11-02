using UnityEngine;

namespace GameAi
{
    // 플레이어를 따라가며 커서 쪽으로 앞당긴다
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

        public static Vector2 LookAhead(Vector2 player, Vector2 cursor, float ratio, float maxOffset)
        {
            return Vector2.ClampMagnitude((cursor - player) * ratio, maxOffset);
        }

        public void Tick(Vector2 player, Vector2 cursor)
        {
            Vector2 position = player + LookAhead(player, cursor, ratio, maxOffset);
            camera.position = new Vector3(position.x, position.y, camera.position.z);
        }
    }
}
