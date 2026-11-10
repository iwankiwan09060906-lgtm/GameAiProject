using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameAi
{
    // 적 머리 위 표시: ?·! 아이콘과 대사 말풍선(7.18, 7.22 감정별 색)
    public sealed class BarkView
    {
        const float FontSize = 24f;
        const float IconSize = 32f;

        sealed class Slot
        {
            public RectTransform Root;
            public Image Icon;
            public TextMeshProUGUI Text;
            public float ShowLeft;
            public float Shake;
        }

        readonly Dictionary<int, Slot> slots = new Dictionary<int, Slot>();
        readonly RectTransform parent;
        readonly RectTransform canvasRect;
        readonly AssetCatalog catalog;
        readonly Camera camera;
        readonly BarkTimingConfig timing;
        readonly Sprite suspect;
        readonly Sprite detect;

        public BarkView(UiRoot ui, AssetCatalog catalog, Camera camera, BarkTimingConfig timing)
        {
            parent = UiRoot.CreateRect("Barks", ui.Rect);
            parent.anchorMin = Vector2.zero;
            parent.anchorMax = Vector2.one;
            parent.offsetMin = parent.offsetMax = Vector2.zero;
            canvasRect = ui.Rect;
            this.catalog = catalog;
            this.camera = camera;
            this.timing = timing;
            suspect = catalog.GetSprite("icon_suspect");
            detect = catalog.GetSprite("icon_detect");
        }

        public static Color EmotionColor(string emotion)
        {
            switch (emotion)
            {
                case "놀람": return new Color32(0xF2, 0xC2, 0x30, 0xFF);
                case "분노": return new Color32(0xC8, 0x32, 0x3C, 0xFF);
                case "안도": return new Color32(0x9A, 0x9A, 0xA0, 0xFF);
                default: return new Color32(0xE8, 0xE4, 0xDA, 0xFF);
            }
        }

        Slot Get(int id)
        {
            if (slots.TryGetValue(id, out Slot slot)) return slot;
            slot = new Slot { Root = UiRoot.CreateRect("Enemy_" + id, parent) };
            slot.Icon = UiRoot.CreateImage("Icon", slot.Root, suspect);
            slot.Icon.rectTransform.sizeDelta = new Vector2(IconSize, IconSize);
            slot.Icon.rectTransform.anchoredPosition = new Vector2(0f, 8f);
            slot.Text = UiRoot.CreateText("Line", slot.Root, catalog, FontSize);
            slot.Text.alignment = TextAlignmentOptions.Bottom;
            slot.Text.rectTransform.sizeDelta = new Vector2(600f, 40f);
            slot.Text.rectTransform.anchoredPosition = new Vector2(0f, 48f);
            slot.Text.fontStyle = FontStyles.Normal;
            var shadow = slot.Text.gameObject.AddComponent<Shadow>();
            shadow.effectDistance = new Vector2(1f, -1f);
            shadow.effectColor = Color.black;
            slot.Text.enabled = false;
            slots[id] = slot;
            return slot;
        }

        public void Say(int enemyId, string text, string emotion)
        {
            Slot slot = Get(enemyId);
            slot.Text.text = emotion == "의심" && !text.EndsWith("…") && !text.EndsWith("...") && !text.EndsWith("?") && !text.EndsWith("!") ? text + "…" : text;
            slot.Text.color = EmotionColor(emotion);
            // 긴장은 글자 간격을 좁힌다
            slot.Text.characterSpacing = emotion == "긴장" ? -6f : 0f;
            slot.ShowLeft = timing.show_s + timing.fade_s;
            slot.Shake = emotion == "놀람" ? 0.2f : 0f;
            slot.Text.enabled = true;
        }

        public bool IsShowing(int enemyId)
        {
            return slots.TryGetValue(enemyId, out Slot slot) && slot.ShowLeft > 0f;
        }

        // 화면에 보이는 적만 표시. icon: 0 없음, 1 ?, 2 !
        public void Tick(float dt, int enemyId, Vector2 worldPos, bool visible, int icon, bool alive)
        {
            if (!alive)
            {
                if (slots.TryGetValue(enemyId, out Slot dead)) dead.Root.gameObject.SetActive(false);
                return;
            }
            Slot slot = Get(enemyId);
            if (slot.ShowLeft > 0f) slot.ShowLeft -= dt;
            bool talking = slot.ShowLeft > 0f;
            bool show = visible && (icon > 0 || talking);
            slot.Root.gameObject.SetActive(show);
            if (!show) return;
            Vector3 screen = camera.WorldToScreenPoint(worldPos + Vector2.up * timing.offset_m);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local);
            if (slot.Shake > 0f)
            {
                slot.Shake -= dt;
                local.x += Mathf.Sin(Time.unscaledTime * 90f) * 4f;
            }
            slot.Root.anchoredPosition = local;
            slot.Icon.enabled = icon > 0;
            if (icon > 0) slot.Icon.sprite = icon == 2 ? detect : suspect;
            slot.Text.enabled = talking;
            if (talking)
            {
                Color c = slot.Text.color;
                c.a = Mathf.Clamp01(slot.ShowLeft / Mathf.Max(0.01f, timing.fade_s));
                slot.Text.color = c;
            }
        }
    }
}
