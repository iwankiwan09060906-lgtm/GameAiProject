using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameAi
{
    // 대상 위에 [SPACE] 열기 안내를 띄운다. 키 칸은 회색 테두리 상자, 동작 이름은 흰 글자
    public sealed class InteractPrompt
    {
        const float FontSize = 24f;
        const float KeyPadding = 12f;
        const float Gap = 10f;
        const float BoxHeight = 34f;
        const float WorldOffset = 0.9f;
        const string KeyLabel = "SPACE";

        readonly RectTransform root;
        readonly RectTransform canvasRect;
        readonly RectTransform keyBox;
        readonly TextMeshProUGUI actionText;
        readonly Camera camera;

        public string CurrentAction { get; private set; }

        public InteractPrompt(UiRoot ui, AssetCatalog catalog, Camera camera)
        {
            this.camera = camera;
            canvasRect = ui.Rect;
            root = UiRoot.CreateRect("InteractPrompt", ui.Rect);

            Image box = UiRoot.CreateImage("KeyBox", root, catalog.GetSprite("panel_dark"));
            box.type = Image.Type.Sliced;
            keyBox = box.rectTransform;
            keyBox.pivot = new Vector2(1f, 0.5f);

            TextMeshProUGUI key = UiRoot.CreateText("KeyText", keyBox, catalog, FontSize);
            key.text = KeyLabel;
            key.alignment = TextAlignmentOptions.Center;
            key.rectTransform.anchorMin = Vector2.zero;
            key.rectTransform.anchorMax = Vector2.one;
            key.rectTransform.offsetMin = Vector2.zero;
            key.rectTransform.offsetMax = Vector2.zero;
            keyBox.sizeDelta = new Vector2(key.GetPreferredValues(KeyLabel).x + KeyPadding * 2f, BoxHeight);
            keyBox.anchoredPosition = new Vector2(-Gap / 2f, 0f);

            actionText = UiRoot.CreateText("Action", root, catalog, FontSize);
            actionText.alignment = TextAlignmentOptions.Left;
            actionText.rectTransform.pivot = new Vector2(0f, 0.5f);
            actionText.rectTransform.sizeDelta = new Vector2(120f, BoxHeight);
            actionText.rectTransform.anchoredPosition = new Vector2(Gap / 2f, 0f);

            root.gameObject.SetActive(false);
        }

        public void Tick(IInteractable target)
        {
            bool show = target != null;
            CurrentAction = show ? target.PromptAction : null;
            if (root.gameObject.activeSelf != show) root.gameObject.SetActive(show);
            if (!show) return;

            actionText.text = target.PromptAction;
            Vector2 world = target.Position + Vector2.up * WorldOffset;
            Vector2 screen = camera.WorldToScreenPoint(world);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local))
                root.anchoredPosition = local;
        }
    }
}
