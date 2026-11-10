using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameAi
{
    // 설정 오버레이(7.13, 10장): 음량 3개는 바로 적용, 전체화면·해상도는 "적용" 때. 적용하면 저장
    public sealed class SettingsOverlay
    {
        readonly GameObject root;
        bool pendingFullscreen;
        int pendingWidth, pendingHeight;
        TextMeshProUGUI fullscreenLabel, resolutionLabel;

        public bool IsOpen { get { return root.activeSelf; } }
        public Action OnVolumeChanged { get; set; }

        public SettingsOverlay(RectTransform parent, AssetCatalog catalog)
        {
            root = UiRoot.CreateRect("Settings", parent).gameObject;
            RectTransform rect = (RectTransform)root.transform;
            Widgets.Stretch(rect);
            Widgets.Dimmer(rect);
            Image panel = Widgets.Panel("Panel", rect, catalog);
            Widgets.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(680f, 560f));
            RectTransform column = UiRoot.CreateRect("Column", panel.transform);
            Widgets.Stretch(column);
            column.offsetMax = new Vector2(0f, -32f);
            Widgets.Column(column, 18f);
            Widgets.Label("Title", column, catalog, "설정", 36f, TextAlignmentOptions.Center).rectTransform.sizeDelta = new Vector2(600f, 48f);
            SettingsData s = GameSession.Settings;
            Widgets.Slider("Master", column, catalog, "전체 음량", s.master, v => { s.master = v; OnVolumeChanged?.Invoke(); });
            Widgets.Slider("Sfx", column, catalog, "효과음", s.sfx, v => { s.sfx = v; OnVolumeChanged?.Invoke(); });
            Widgets.Slider("Bgm", column, catalog, "배경음", s.bgm, v => { s.bgm = v; OnVolumeChanged?.Invoke(); });
            fullscreenLabel = Widgets.Button("Fullscreen", column, catalog, "", ToggleFullscreen, 560f, 52f).GetComponentInChildren<TextMeshProUGUI>();
            resolutionLabel = Widgets.Button("Resolution", column, catalog, "", ToggleResolution, 560f, 52f).GetComponentInChildren<TextMeshProUGUI>();
            Widgets.Button("Apply", column, catalog, "적용", Apply, 560f, 52f);
            Widgets.Button("Close", column, catalog, "닫기", Close, 560f, 52f);
            root.SetActive(false);
        }

        public void Open()
        {
            SettingsData s = GameSession.Settings;
            pendingFullscreen = s.fullscreen;
            pendingWidth = s.width;
            pendingHeight = s.height;
            Refresh();
            root.SetActive(true);
            root.transform.SetAsLastSibling();
        }

        public void Close()
        {
            GameSession.Save();
            root.SetActive(false);
        }

        void ToggleFullscreen()
        {
            pendingFullscreen = !pendingFullscreen;
            Refresh();
        }

        void ToggleResolution()
        {
            if (pendingWidth == 1920) { pendingWidth = 1280; pendingHeight = 720; }
            else { pendingWidth = 1920; pendingHeight = 1080; }
            Refresh();
        }

        void Apply()
        {
            SettingsData s = GameSession.Settings;
            s.fullscreen = pendingFullscreen;
            s.width = pendingWidth;
            s.height = pendingHeight;
            GameSession.ApplyScreen();
            GameSession.Save();
        }

        void Refresh()
        {
            fullscreenLabel.text = pendingFullscreen ? "화면: 전체화면" : "화면: 창모드";
            resolutionLabel.text = "해상도: " + pendingWidth + "×" + pendingHeight;
        }
    }

    // 크레딧 화면: credits.txt 전문을 스크롤 표시(11.5)
    public sealed class CreditsScreen
    {
        public const string Fallback = "외부 에셋 없음 — 모든 그래픽·사운드는 코드로 생성";
        readonly GameObject root;
        readonly RectTransform content;

        public bool IsOpen { get { return root.activeSelf; } }

        public static string ReadCredits()
        {
            string text = ConfigLoader.ReadText("credits.txt");
            if (string.IsNullOrWhiteSpace(text))
            {
                Debug.LogWarning("[Credits] credits.txt가 없어 기본 문구를 표시합니다.");
                return Fallback;
            }
            return text;
        }

        public CreditsScreen(RectTransform parent, AssetCatalog catalog)
        {
            root = UiRoot.CreateRect("Credits", parent).gameObject;
            RectTransform rect = (RectTransform)root.transform;
            Widgets.Stretch(rect);
            Widgets.Dimmer(rect, 0.9f);
            Image panel = Widgets.Panel("Panel", rect, catalog);
            Widgets.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(1400f, 800f));
            var scroll = panel.gameObject.AddComponent<ScrollRect>();
            RectTransform viewport = UiRoot.CreateRect("Viewport", panel.transform);
            Widgets.Stretch(viewport);
            viewport.offsetMin = new Vector2(32f, 32f);
            viewport.offsetMax = new Vector2(-32f, -32f);
            viewport.gameObject.AddComponent<RectMask2D>();
            content = UiRoot.CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            TextMeshProUGUI text = Widgets.Label("Text", content, catalog, ReadCredits(), 24f, TextAlignmentOptions.TopLeft);
            text.textWrappingMode = TextWrappingModes.Normal;
            Widgets.Stretch(text.rectTransform);
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.scrollSensitivity = 40f;
            panel.raycastTarget = true;
            Button close = Widgets.Button("Close", rect, catalog, "닫기", () => root.SetActive(false));
            Widgets.Place((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(360f, 60f));
            root.SetActive(false);
        }

        public void Open()
        {
            root.SetActive(true);
            root.transform.SetAsLastSibling();
        }

        public void Close() { root.SetActive(false); }
    }

    // 일시정지(Esc): 이어하기·설정·메인 메뉴 + 들은 대사 기록(C). 소리·AI·타이머 정지
    public sealed class PauseMenu
    {
        readonly GameObject root;
        readonly SettingsOverlay settings;
        readonly TextMeshProUGUI history;
        readonly Func<IReadOnlyList<(int, string, string, string)>> heardLines;

        public bool IsOpen { get { return root.activeSelf; } }

        public PauseMenu(RectTransform parent, AssetCatalog catalog, SettingsOverlay settingsOverlay, Func<IReadOnlyList<(int, string, string, string)>> lines)
        {
            settings = settingsOverlay;
            heardLines = lines;
            root = UiRoot.CreateRect("Pause", parent).gameObject;
            RectTransform rect = (RectTransform)root.transform;
            Widgets.Stretch(rect);
            Widgets.Dimmer(rect);
            RectTransform column = UiRoot.CreateRect("Column", rect);
            Widgets.Place(column, new Vector2(0.3f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400f, 420f));
            Widgets.Column(column, 20f);
            Widgets.Label("Title", column, catalog, "일시정지", 48f, TextAlignmentOptions.Center).rectTransform.sizeDelta = new Vector2(400f, 64f);
            Widgets.Button("Resume", column, catalog, "이어하기", () => SetOpen(false));
            Widgets.Button("Settings", column, catalog, "설정", () => settings.Open());
            Widgets.Button("Menu", column, catalog, "메인 메뉴", () => { SetOpen(false); SceneFlow.LoadMainMenu(); });
            Image panel = Widgets.Panel("Heard", rect, catalog);
            Widgets.Place(panel.rectTransform, new Vector2(0.68f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 760f));
            history = Widgets.Label("Lines", panel.transform, catalog, "", 24f, TextAlignmentOptions.TopLeft);
            history.textWrappingMode = TextWrappingModes.Normal;
            Widgets.Stretch(history.rectTransform);
            history.rectTransform.offsetMin = new Vector2(24f, 24f);
            history.rectTransform.offsetMax = new Vector2(-24f, -24f);
            root.SetActive(false);
        }

        public void SetOpen(bool open)
        {
            if (open == IsOpen) return;
            root.SetActive(open);
            if (open)
            {
                root.transform.SetAsLastSibling();
                var sb = new System.Text.StringBuilder("들은 대사\n");
                foreach (var line in heardLines()) sb.Append("· ").Append(line.Item2).Append('\n');
                history.text = sb.ToString();
            }
            EventBus.Publish(new PauseEvent { isPaused = open });
        }
    }

    // 결과 화면: 성공/실패, 클리어 시간, 발각, 처치, 최고 기록 갱신, 다시 하기·다음 미션·메인 메뉴
    public sealed class ResultScreen
    {
        readonly GameObject root;
        readonly TextMeshProUGUI title, body;
        readonly Button nextButton;

        public bool IsOpen { get { return root.activeSelf; } }

        public ResultScreen(RectTransform parent, AssetCatalog catalog, Action retry, Action next, Action menu)
        {
            root = UiRoot.CreateRect("Result", parent).gameObject;
            RectTransform rect = (RectTransform)root.transform;
            Widgets.Stretch(rect);
            Widgets.Dimmer(rect, 0.8f);
            Image panel = Widgets.Panel("Panel", rect, catalog);
            Widgets.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(760f, 520f));
            title = Widgets.Label("Title", panel.transform, catalog, "", 48f, TextAlignmentOptions.Center);
            Widgets.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -32f), new Vector2(700f, 64f));
            body = Widgets.Label("Body", panel.transform, catalog, "", 36f, TextAlignmentOptions.TopLeft);
            Widgets.Place(body.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(620f, 360f));
            RectTransform row = UiRoot.CreateRect("Buttons", rect);
            Widgets.Place(row, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -280f), new Vector2(1180f, 60f));
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 30f;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = h.childControlHeight = false;
            Widgets.Button("Retry", row, catalog, "다시 하기", retry);
            nextButton = Widgets.Button("Next", row, catalog, "다음 미션", next);
            Widgets.Button("Menu", row, catalog, "메인 메뉴", menu);
            root.SetActive(false);
        }

        public void Show(MissionManager mission, bool newRecord, bool hasNext, bool saveFailed)
        {
            bool success = mission.Outcome == MissionResult.Success;
            title.text = success ? "미션 성공" : "미션 실패";
            title.color = success ? Widgets.Text : Widgets.Accent;
            body.text = "클리어 시간  " + (success ? mission.ClearTime.ToString("F3") + "초" : "-") +
                        "\n발각 횟수  " + mission.Detections +
                        "\n처치 수  " + mission.Kills +
                        (success ? "\n최고 기록  " + (newRecord ? "갱신!" : "유지") : "") +
                        (saveFailed ? "\n(저장 실패: 기록 없이 진행)" : "");
            nextButton.gameObject.SetActive(success && hasNext);
            root.SetActive(true);
            root.transform.SetAsLastSibling();
        }
    }
}
