using UnityEngine;
using UnityEngine.UI;

namespace GameAi
{
    // 하드웨어 커서 대신 꺾쇠 조준선을 그린다(FN-UI-08).
    // 사격 직후와 달리는 동안 바깥으로 벌어지는 것은 연출뿐이고 명중률과 무관하다
    public sealed class Crosshair : MonoBehaviour
    {
        RectTransform rect;
        RectTransform canvasRect;
        CrosshairConfig config;

        // 사격 직후 벌어짐이 남은 시간
        float shotLeft;

        public void Init(UiRoot ui, AssetCatalog catalog, CrosshairConfig crosshairConfig)
        {
            config = crosshairConfig;
            canvasRect = ui.Rect;
            Image image = UiRoot.CreateImage("CrosshairImage", transform, catalog.GetSprite("crosshair"));
            image.SetNativeSize();
            rect = image.rectTransform;
            rect.sizeDelta *= config.display_scale;

            // 진짜 마우스 커서는 숨긴다
            Cursor.visible = false;
        }

        // 구독은 OnEnable, 해제는 OnDisable(5.1). 꺼질 때 커서를 되살린다
        void OnEnable()
        {
            EventBus.Subscribe<ShotFiredEvent>(OnShot);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<ShotFiredEvent>(OnShot);
            Cursor.visible = true;
        }

        void OnShot(ShotFiredEvent evt)
        {
            if (evt.shooterId == EntityIds.Player) shotLeft = config.shot_spread_s;
        }

        // screenOverride가 있으면 마우스 대신 그 화면 좌표를 따라간다(자동 재생용)
        public void Tick(float dt, MoveMode mode, bool moving, Vector2? screenOverride)
        {
            shotLeft = Mathf.Max(0f, shotLeft - dt);

            // 달리는 중이거나 쏜 직후에는 조준선을 살짝 키워 벌어진 것처럼 보이게 한다
            float scale = 1f;
            if (moving && mode == MoveMode.Run) scale = config.run_spread;
            if (shotLeft > 0f) scale = Mathf.Max(scale, config.shot_spread);
            rect.localScale = new Vector3(scale, scale, 1f);

            // 화면 좌표를 Canvas 안의 좌표로 바꿔 조준선을 옮긴다
            Vector2 screen = screenOverride ?? (Vector2)Input.mousePosition;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local))
                rect.anchoredPosition = local;
        }
    }
}
