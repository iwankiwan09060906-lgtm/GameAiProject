using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameAi
{
    // uGUI 부품을 코드로 만든다(10장 색: 패널 #0E0E12 85%, 글자 #E8E4DA, 강조 #C8323C, 테두리 #C9A24A)
    public static class Widgets
    {
        public static readonly Color Text = new Color32(0xE8, 0xE4, 0xDA, 0xFF);
        public static readonly Color Accent = new Color32(0xC8, 0x32, 0x3C, 0xFF);
        public static readonly Color Gold = new Color32(0xC9, 0xA2, 0x4A, 0xFF);
        public static readonly Color PanelFill = new Color32(0x0E, 0x0E, 0x12, 0xD9);
        public static readonly Color Dim = new Color32(0x9A, 0x9A, 0xA0, 0xFF);

        // 버튼 클릭을 받으려면 씬에 EventSystem이 하나 있어야 한다
        public static void EnsureEventSystem(Transform parent)
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            go.transform.SetParent(parent, false);
        }

        public static RectTransform Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        public static RectTransform Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        public static Image Panel(string name, Transform parent, AssetCatalog catalog, string sprite = "panel_dark")
        {
            Sprite s = catalog.GetSprite(sprite);
            Image image = UiRoot.CreateImage(name, parent, s);
            if (s != null && s.border != Vector4.zero) image.type = Image.Type.Sliced;
            if (s == null) image.color = PanelFill;
            // 스프라이트 1px = UI 2px(정수 배율 ×2). 캔버스 기준 100px/유닛, 스프라이트 32px/유닛
            image.pixelsPerUnitMultiplier = 100f / 32f / 2f;
            return image;
        }

        public static Image Fill(string name, Transform parent, Color color)
        {
            Image image = UiRoot.CreateImage(name, parent, null);
            image.color = color;
            return image;
        }

        public static TextMeshProUGUI Label(string name, Transform parent, AssetCatalog catalog, string text, float size, TextAlignmentOptions align = TextAlignmentOptions.Left)
        {
            TextMeshProUGUI label = UiRoot.CreateText(name, parent, catalog, size);
            label.text = text;
            label.alignment = align;
            return label;
        }

        public static Button Button(string name, Transform parent, AssetCatalog catalog, string text, Action onClick, float width = 360f, float height = 60f)
        {
            Image bg = Panel(name, parent, catalog);
            bg.raycastTarget = true;
            bg.rectTransform.sizeDelta = new Vector2(width, height);
            var button = bg.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(1f, 0.75f, 0.75f);
            colors.pressedColor = new Color(0.8f, 0.4f, 0.4f);
            button.colors = colors;
            TextMeshProUGUI label = Label("Label", bg.transform, catalog, text, 24f, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);
            button.onClick.AddListener(() =>
            {
                EventBus.Publish(new UiClickEvent());
                onClick?.Invoke();
            });
            return button;
        }

        public static Slider Slider(string name, Transform parent, AssetCatalog catalog, string text, float value, Action<float> onChange)
        {
            RectTransform row = UiRoot.CreateRect(name, parent);
            row.sizeDelta = new Vector2(560f, 48f);
            TextMeshProUGUI label = Label("Label", row, catalog, text, 24f);
            Place(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(200f, 40f));
            RectTransform track = Fill("Track", row, new Color32(0x30, 0x30, 0x38, 0xFF)).rectTransform;
            Place(track, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(220f, 0f), new Vector2(320f, 12f));
            var slider = track.gameObject.AddComponent<Slider>();
            RectTransform fillArea = UiRoot.CreateRect("FillArea", track);
            Stretch(fillArea);
            Image fill = Fill("Fill", fillArea, Accent);
            Stretch(fill.rectTransform);
            RectTransform handleArea = UiRoot.CreateRect("HandleArea", track);
            Stretch(handleArea);
            Image handle = Fill("Handle", handleArea, Text);
            handle.raycastTarget = true;
            handle.rectTransform.sizeDelta = new Vector2(16f, 28f);
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = value;
            track.GetComponent<Image>().raycastTarget = true;
            slider.onValueChanged.AddListener(v => onChange?.Invoke(v));
            return slider;
        }

        public static VerticalLayoutGroup Column(RectTransform rect, float spacing)
        {
            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return layout;
        }

        // 화면 전체를 덮는 반투명 배경(오버레이 창 뒤)
        public static Image Dimmer(Transform parent, float alpha = 0.7f)
        {
            Image dim = Fill("Dimmer", parent, new Color(0f, 0f, 0f, alpha));
            dim.raycastTarget = true;
            Stretch(dim.rectTransform);
            return dim;
        }
    }
}
