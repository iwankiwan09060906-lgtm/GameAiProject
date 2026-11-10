using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameAi
{
    // 하단 대화창(7.19): 초상화(32×40 ×4) + 빨강 이름표, 대사(초당 40자, Space/클릭으로 즉시 완성) + 번호 선택지.
    // 선택지는 다음 줄로 넘어가기만 한다(분기 없음). 대화 중에는 GameManager가 1~8단계를 건너뛴다
    public sealed class DialogueBox : MonoBehaviour
    {
        const float CharsPerSecond = 40f;
        const int MaxChoices = 3;

        DialogueConfig config;
        AssetCatalog catalog;
        DialogueDef current;
        int lineIndex;
        float shown;
        int selected;
        bool autoAdvance;

        Image portrait;
        TextMeshProUGUI nameText, lineText;
        readonly List<TextMeshProUGUI> choiceTexts = new List<TextMeshProUGUI>();
        readonly List<Image> choiceBars = new List<Image>();

        public bool IsActive { get { return current != null; } }
        public string CurrentId { get { return current != null ? current.id : null; } }

        public void Init(UiRoot ui, AssetCatalog assetCatalog, DialogueConfig dialogueConfig)
        {
            catalog = assetCatalog;
            config = dialogueConfig;
            RectTransform root = (RectTransform)transform;
            root.anchorMin = new Vector2(0f, 0f);
            root.anchorMax = new Vector2(1f, 0.3f);
            root.offsetMin = root.offsetMax = Vector2.zero;
            Image panel = Widgets.Panel("Panel", root, catalog, "panel_dialogue");
            Widgets.Stretch(panel.rectTransform);
            Image line = Widgets.Fill("TopLine", root, Widgets.Accent);
            line.rectTransform.anchorMin = new Vector2(0f, 1f);
            line.rectTransform.anchorMax = new Vector2(1f, 1f);
            line.rectTransform.pivot = new Vector2(0.5f, 1f);
            line.rectTransform.sizeDelta = new Vector2(0f, 2f);

            portrait = UiRoot.CreateImage("Portrait", root, null);
            Widgets.Place(portrait.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(60f, 20f), new Vector2(128f, 160f));
            Image namePlate = Widgets.Fill("NamePlate", root, Widgets.Accent);
            Widgets.Place(namePlate.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(44f, -90f), new Vector2(160f, 36f));
            nameText = Widgets.Label("Name", namePlate.transform, catalog, "", 24f, TextAlignmentOptions.Center);
            Widgets.Stretch(nameText.rectTransform);

            lineText = Widgets.Label("Line", root, catalog, "", 36f, TextAlignmentOptions.TopLeft);
            lineText.textWrappingMode = TextWrappingModes.Normal;
            lineText.rectTransform.anchorMin = new Vector2(0f, 0f);
            lineText.rectTransform.anchorMax = new Vector2(1f, 1f);
            lineText.rectTransform.offsetMin = new Vector2(260f, 130f);
            lineText.rectTransform.offsetMax = new Vector2(-60f, -40f);
            for (int i = 0; i < MaxChoices; i++)
            {
                Image bar = Widgets.Fill("ChoiceBar" + i, root, Widgets.Accent);
                Widgets.Place(bar.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(250f, 96f - i * 40f), new Vector2(6f, 32f));
                TextMeshProUGUI choice = Widgets.Label("Choice" + i, root, catalog, "", 24f);
                Widgets.Place(choice.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(266f, 96f - i * 40f), new Vector2(900f, 32f));
                choiceBars.Add(bar);
                choiceTexts.Add(choice);
            }
            gameObject.SetActive(false);
        }

        // 대화가 없으면 false(건너뛴다)
        public bool Play(string id, bool auto = false)
        {
            DialogueDef def = config.Find(id);
            if (def == null || def.lines == null || def.lines.Length == 0) return false;
            current = def;
            lineIndex = 0;
            autoAdvance = auto;
            gameObject.SetActive(true);
            ShowLine();
            EventBus.Publish(new DialogueStateEvent { dialogueId = id, isActive = true });
            return true;
        }

        void ShowLine()
        {
            DialogueLine line = current.lines[lineIndex];
            DialogueSpeaker speaker = config.Speaker(line.speaker);
            nameText.text = speaker.name;
            portrait.sprite = catalog.GetSprite(speaker.portrait);
            portrait.enabled = portrait.sprite != null;
            shown = 0f;
            selected = 0;
            lineText.text = line.text;
            lineText.maxVisibleCharacters = 0;
        }

        string[] Choices()
        {
            string[] choices = current.lines[lineIndex].choices;
            return choices != null && choices.Length > 0 ? choices : new[] { "[...]" };
        }

        // 일시정지와 관계없이 매 프레임(실제 시간)
        public void Tick(float unscaledDt, bool advancePressed, int numberPressed)
        {
            if (current == null) return;
            DialogueLine line = current.lines[lineIndex];
            shown += unscaledDt * CharsPerSecond;
            bool complete = shown >= line.text.Length;
            lineText.maxVisibleCharacters = Mathf.Min(line.text.Length, Mathf.FloorToInt(shown));
            string[] choices = Choices();
            for (int i = 0; i < MaxChoices; i++)
            {
                bool on = complete && i < choices.Length;
                choiceTexts[i].gameObject.SetActive(on);
                choiceBars[i].gameObject.SetActive(on && i == selected);
                if (on)
                {
                    choiceTexts[i].text = (i + 1) + ". " + choices[i];
                    choiceTexts[i].color = i == selected ? Widgets.Text : Widgets.Dim;
                }
            }
            if (autoAdvance) advancePressed = complete || advancePressed;
            if (numberPressed >= 1 && numberPressed <= choices.Length && complete)
            {
                selected = numberPressed - 1;
                Next();
                return;
            }
            if (!advancePressed) return;
            if (!complete)
            {
                shown = line.text.Length;
                return;
            }
            Next();
        }

        void Next()
        {
            EventBus.Publish(new UiClickEvent());
            lineIndex++;
            if (lineIndex < current.lines.Length)
            {
                ShowLine();
                return;
            }
            string id = current.id;
            current = null;
            gameObject.SetActive(false);
            EventBus.Publish(new DialogueStateEvent { dialogueId = id, isActive = false });
        }
    }
}
