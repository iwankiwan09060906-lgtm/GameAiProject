using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameAi
{
    // 게임 화면의 Canvas. 씬에 저장하지 않고 코드로 만든다(uGUI, 10장).
    // 화면 크기가 달라도 기준 해상도(1920x1080) 비율로 UI가 같이 커지거나 작아진다
    public sealed class UiRoot
    {
        public Canvas Canvas { get; }
        public RectTransform Rect { get; }

        public UiRoot(GameConfig config, Transform parent)
        {
            // GraphicRaycaster가 있어야 버튼이 클릭을 받는다
            var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            Canvas = go.GetComponent<Canvas>();

            // 화면 맨 위에 덮는다
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // 기준 해상도에 맞춰 비율로 늘리고 줄인다. 가로·세로 중간을 기준으로 삼는다
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(config.reference_resolution[0], config.reference_resolution[1]);
            scaler.matchWidthOrHeight = 0.5f;

            Rect = go.GetComponent<RectTransform>();
        }

        // 빈 UI 오브젝트 하나
        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        // 그림 하나. 마우스 클릭을 가로채지 않도록 raycastTarget을 끈다
        public static Image CreateImage(string name, Transform parent, Sprite sprite)
        {
            RectTransform rect = CreateRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        // 글자 하나. 폰트는 카탈로그의 Galmuri11(없으면 TMP 기본 폰트), 색은 UI 기본 글자색
        public static TextMeshProUGUI CreateText(string name, Transform parent, AssetCatalog catalog, float size)
        {
            RectTransform rect = CreateRect(name, parent);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset font = catalog.Font;
            if (font != null) text.font = font;
            text.fontSize = size;
            text.color = new Color32(0xE8, 0xE4, 0xDA, 0xFF);
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }
    }
}
