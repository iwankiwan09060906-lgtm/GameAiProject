using UnityEngine;
using UnityEngine.UI;

namespace GameAi
{
    // 하드웨어 커서 대신 꺾쇠 조준선을 그린다. 벌어짐은 연출만이고 명중과 무관하다
    public sealed class Crosshair : MonoBehaviour
    {
        RectTransform rect;
        RectTransform canvasRect;
        CrosshairConfig config;
        float shotLeft;

        public void Init(UiRoot ui, AssetCatalog catalog, CrosshairConfig crosshairConfig)
        {
            config = crosshairConfig;
            canvasRect = ui.Rect;
            Image image = UiRoot.CreateImage("CrosshairImage", transform, catalog.GetSprite("crosshair"));
            image.SetNativeSize();
            rect = image.rectTransform;
            rect.sizeDelta *= config.display_scale;
            Cursor.visible = false;
        }

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

        public void Tick(float dt, MoveMode mode, bool moving, Vector2? screenOverride)
        {
            shotLeft = Mathf.Max(0f, shotLeft - dt);
            float scale = 1f;
            if (moving && mode == MoveMode.Run) scale = config.run_spread;
            if (shotLeft > 0f) scale = Mathf.Max(scale, config.shot_spread);
            rect.localScale = new Vector3(scale, scale, 1f);

            Vector2 screen = screenOverride ?? (Vector2)Input.mousePosition;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local))
                rect.anchoredPosition = local;
        }
    }
}
