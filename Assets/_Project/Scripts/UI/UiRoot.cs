using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameAi
{
    // 게임 화면의 Canvas. 씬에 저장하지 않고 코드로 만든다
    public sealed class UiRoot
    {
        public Canvas Canvas { get; }
        public RectTransform Rect { get; }

        public UiRoot(GameConfig config, Transform parent)
        {
            var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(parent, false);
            Canvas = go.GetComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(config.reference_resolution[0], config.reference_resolution[1]);
            scaler.matchWidthOrHeight = 0.5f;

            Rect = go.GetComponent<RectTransform>();
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        public static Image CreateImage(string name, Transform parent, Sprite sprite)
        {
            RectTransform rect = CreateRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

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
