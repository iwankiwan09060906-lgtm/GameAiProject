using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameAi
{
    // 상호작용 대상 위에 [SPACE] 열기 안내를 띄운다(FN-UI-09).
    // 키 칸은 회색 테두리 상자, 동작 이름은 흰 글자
    public sealed class InteractPrompt
    {
        // 글자 크기는 Galmuri11의 도트 격자(12px)에 맞춰 12의 배수로 한다
        const float FontSize = 24f;
        const float KeyPadding = 12f;
        const float Gap = 10f;
        const float BoxHeight = 34f;

        // 대상 위로 얼마나 띄울지(m)
        const float WorldOffset = 0.9f;
        const string KeyLabel = "SPACE";

        readonly RectTransform root;
        readonly RectTransform canvasRect;
        readonly RectTransform keyBox;
        readonly TextMeshProUGUI actionText;
        readonly Camera camera;

        // 지금 띄운 동작 이름(없으면 null). 테스트가 확인하는 값이기도 하다
        public string CurrentAction { get; private set; }

        public InteractPrompt(UiRoot ui, AssetCatalog catalog, Camera camera)
        {
            this.camera = camera;
            canvasRect = ui.Rect;
            root = UiRoot.CreateRect("InteractPrompt", ui.Rect);

            // 키 칸: 어두운 패널 그림(9-slice) 위에 SPACE 글자
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

            // 상자 폭은 글자 폭에 좌우 여백을 더해 정한다
            keyBox.sizeDelta = new Vector2(key.GetPreferredValues(KeyLabel).x + KeyPadding * 2f, BoxHeight);
            keyBox.anchoredPosition = new Vector2(-Gap / 2f, 0f);

            // 동작 이름은 키 칸 오른쪽에 붙인다
            actionText = UiRoot.CreateText("Action", root, catalog, FontSize);
            actionText.alignment = TextAlignmentOptions.Left;
            actionText.rectTransform.pivot = new Vector2(0f, 0.5f);
            actionText.rectTransform.sizeDelta = new Vector2(120f, BoxHeight);
            actionText.rectTransform.anchoredPosition = new Vector2(Gap / 2f, 0f);

            root.gameObject.SetActive(false);
        }

        // target이 null이면 숨긴다. 있으면 그 위에 띄운다
        public void Tick(IInteractable target)
        {
            bool show = target != null;
            CurrentAction = show ? target.PromptAction : null;
            if (root.gameObject.activeSelf != show) root.gameObject.SetActive(show);
            if (!show) return;

            // 글자가 바뀔 때만 설정한다. 같은 글자를 매 프레임 다시 넣으면 글자 메시를 계속 새로 만든다
            if (actionText.text != target.PromptAction) actionText.text = target.PromptAction;

            // 대상의 월드 좌표를 화면 → Canvas 좌표로 바꿔 위치를 잡는다
            Vector2 world = target.Position + Vector2.up * WorldOffset;
            Vector2 screen = camera.WorldToScreenPoint(world);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local))
                root.anchoredPosition = local;
        }
    }
}
