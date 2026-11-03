using TMPro;
using UnityEngine;

namespace GameAi
{
    // F3으로 켜고 끄는 성능 표시. 우상단에 FPS와 프레임 시간, 게임 코드 시간을 보여 준다.
    // "프레임"은 길고 "게임 코드"가 짧으면 끊김의 원인은 게임 밖이다.
    public sealed class PerfOverlay
    {
        // 글자를 갱신하는 간격(초). 매 프레임 바꾸면 글자 메시를 계속 다시 만든다
        const float RefreshSeconds = 0.5f;
        const float FontSize = 24f;
        const float Margin = 16f;

        readonly TextMeshProUGUI text;
        readonly PerfProbe probe;
        float refreshLeft;

        public bool Visible { get; private set; }

        public PerfOverlay(UiRoot ui, AssetCatalog catalog, PerfProbe perfProbe)
        {
            probe = perfProbe;
            text = UiRoot.CreateText("PerfOverlay", ui.Rect, catalog, FontSize);
            text.alignment = TextAlignmentOptions.TopRight;
            text.color = new Color32(0x7C, 0xFF, 0x9A, 0xFF);

            // 우상단 모서리에 붙인다
            RectTransform rect = text.rectTransform;
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.sizeDelta = new Vector2(900f, 80f);
            rect.anchoredPosition = new Vector2(-Margin, -Margin);
            text.gameObject.SetActive(false);
        }

        public void Tick(float unscaledDelta)
        {
            if (Input.GetKeyDown(KeyCode.F3))
            {
                Visible = !Visible;
                text.gameObject.SetActive(Visible);
                refreshLeft = 0f;
            }
            if (!Visible) return;

            refreshLeft -= unscaledDelta;
            if (refreshLeft > 0f) return;
            refreshLeft = RefreshSeconds;

            float average = probe.AverageFrameMs;
            text.text = "FPS " + (average > 0f ? (1000f / average).ToString("F0") : "-")
                + "  프레임 " + average.ToString("F1") + "ms (최대 " + probe.MaxFrameMs.ToString("F1") + ")\n"
                + "게임 코드 " + probe.AverageSectionMs.ToString("F2") + "ms (최대 " + probe.MaxSectionMs.ToString("F2") + ")";
        }
    }
}
