using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace GameAi
{
    // 메인 메뉴 씬(10장): 제목, 시작(→ 미션 선택), 설정, 크레딧, 종료. 배경은 어두운 바닥 + 천장등 하나
    public sealed class MainMenuBootstrap : MonoBehaviour
    {
        [SerializeField] AssetCatalog catalog;

        GameConfig config;
        UiRoot ui;
        AudioManager audioManager;
        SettingsOverlay settings;
        CreditsScreen credits;
        GameObject mainPanel, selectPanel, consentPanel;
        readonly List<(string id, TextMeshProUGUI record)> cards = new List<(string, TextMeshProUGUI)>();
        readonly List<(string id, Image bg)> difficultyButtons = new List<(string, Image)>();

        public bool IsReady { get; private set; }

        void Start()
        {
            if (catalog == null) catalog = ScriptableObject.CreateInstance<AssetCatalog>();
            config = ConfigLoader.Load<GameConfig>("game.json");
            FontPrewarm.Run(catalog.Font);
            if (string.IsNullOrEmpty(SceneFlow.SelectedDifficulty)) SceneFlow.SelectedDifficulty = config.default_difficulty;
            GameSession.ApplyScreenOnce();
            // 게임 씬에서 숨긴 커서를 메뉴에서는 다시 보인다
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            BuildBackground();
            ui = new UiRoot(config, transform);
            Widgets.EnsureEventSystem(transform);
            audioManager = AudioManager.Create(transform, catalog, config.audio.ambience_fade_s);
            audioManager.PlayBgm("bgm_menu");
            BuildMain();
            BuildSelect();
            settings = new SettingsOverlay(ui.Rect, catalog) { OnVolumeChanged = audioManager.ApplyVolumes };
            credits = new CreditsScreen(ui.Rect, catalog);
            if (GameSession.LoadStatus == SaveLoadStatus.CorruptNeedsConsent) BuildConsent();
            // -smoke로 실행하면 메뉴를 건너뛰고 바로 게임으로
            string[] args = System.Environment.GetCommandLineArgs();
            int scenario = System.Array.IndexOf(args, "-scenario");
            bool menuSmoke = scenario >= 0 && scenario + 1 < args.Length && args[scenario + 1] == "menu";
            if (System.Array.IndexOf(args, "-smoke") >= 0 && !menuSmoke) SceneFlow.LoadGame(SceneFlow.SelectedMapId);
            IsReady = true;
        }

        public void OpenCredits() { credits.Open(); }
        public void CloseCredits() { credits.Close(); }
        public void OpenSettings() { settings.Open(); }
        public void CloseSettings() { settings.Close(); }

        void BuildBackground()
        {
            var root = new GameObject("Background").transform;
            root.SetParent(transform, false);
            Sprite floor = catalog.GetSprite("tile_floor_wood");
            Sprite wall = catalog.GetSprite("tile_wall");
            for (int y = -9; y <= 9; y++)
                for (int x = -16; x <= 16; x++)
                {
                    var r = new GameObject("T", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                    r.transform.SetParent(root, false);
                    r.transform.position = new Vector3(x, y, 0f);
                    bool edge = Mathf.Abs(y) == 6 || (Mathf.Abs(x) == 11 && Mathf.Abs(y) < 6);
                    r.sprite = edge ? wall : floor;
                    r.sharedMaterial = catalog.SpriteLit;
                }
            var lamp = new GameObject("Lamp", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            lamp.transform.SetParent(root, false);
            lamp.transform.position = new Vector3(-4f, 1f, 0f);
            lamp.sprite = catalog.GetSprite("lamp");
            lamp.sharedMaterial = catalog.SpriteUnlit;
            var global = new GameObject("GlobalLight").AddComponent<Light2D>();
            global.transform.SetParent(root, false);
            global.lightType = Light2D.LightType.Global;
            global.color = MathUtil.ParseColor(config.lighting.ambient_color, Color.black);
            // 게임 화면보다 조금 밝게(제목이 보이도록), 천장등 하나만 따뜻하게
            global.intensity = 0.4f;
            var point = new GameObject("PointLight").AddComponent<Light2D>();
            point.transform.SetParent(root, false);
            point.transform.position = new Vector3(-4f, 1f, 0f);
            point.lightType = Light2D.LightType.Point;
            point.color = MathUtil.ParseColor(config.lighting.lamp_color, Color.white);
            point.pointLightOuterRadius = 6f;
            point.pointLightInnerRadius = 0.5f;
            point.intensity = 0.9f;
            point.falloffIntensity = config.lighting.lamp_falloff;
        }

        void BuildMain()
        {
            RectTransform panel = UiRoot.CreateRect("Main", ui.Rect);
            Widgets.Stretch(panel);
            mainPanel = panel.gameObject;
            TextMeshProUGUI title = Widgets.Label("Title", panel, catalog, "NULL TRACE", 96f, TextAlignmentOptions.Left);
            Widgets.Place(title.rectTransform, new Vector2(0.55f, 0.72f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(800f, 120f));
            TextMeshProUGUI sub = Widgets.Label("Sub", panel, catalog, "2D 탑뷰 전술 잠입", 36f, TextAlignmentOptions.Left);
            sub.color = Widgets.Accent;
            Widgets.Place(sub.rectTransform, new Vector2(0.55f, 0.64f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(800f, 48f));
            RectTransform column = UiRoot.CreateRect("Buttons", panel);
            Widgets.Place(column, new Vector2(0.55f, 0.45f), new Vector2(0f, 1f), Vector2.zero, new Vector2(360f, 400f));
            Widgets.Column(column, 16f).childAlignment = TextAnchor.UpperLeft;
            Widgets.Button("Start", column, catalog, "시작", () => ShowSelect(true));
            Widgets.Button("Settings", column, catalog, "설정", () => settings.Open());
            Widgets.Button("Credits", column, catalog, "크레딧", () => credits.Open());
            Widgets.Button("Quit", column, catalog, "종료", Application.Quit);
        }

        void BuildSelect()
        {
            RectTransform panel = UiRoot.CreateRect("MissionSelect", ui.Rect);
            Widgets.Stretch(panel);
            selectPanel = panel.gameObject;
            Widgets.Dimmer(panel, 0.6f);
            TextMeshProUGUI title = Widgets.Label("Title", panel, catalog, "미션 선택", 48f, TextAlignmentOptions.Center);
            Widgets.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(800f, 64f));
            var missions = ConfigLoader.Load<MissionConfig>("missions.json");
            RectTransform row = UiRoot.CreateRect("Cards", panel);
            Widgets.Place(row, new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1760f, 420f));
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 24f;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = h.childControlHeight = false;
            foreach (MissionMapEntry entry in missions.maps)
            {
                string json = ConfigLoader.ReadText(entry.file);
                if (json == null) continue;
                MapData data = MapJson.Parse(json, entry.file);
                string id = entry.id;
                Button card = Widgets.Button("Card_" + id, row, catalog, "", () => SceneFlow.LoadGame(id), 410f, 400f);
                TextMeshProUGUI label = card.GetComponentInChildren<TextMeshProUGUI>();
                label.alignment = TextAlignmentOptions.Top;
                label.rectTransform.offsetMax = new Vector2(-16f, -24f);
                label.text = data.name + "\n\n<size=24>" + MissionLabel(entry.mission, missions.bomb.enabled) + "</size>";
                TextMeshProUGUI record = Widgets.Label("Record", card.transform, catalog, "", 24f, TextAlignmentOptions.Bottom);
                Widgets.Stretch(record.rectTransform);
                record.rectTransform.offsetMin = new Vector2(16f, 24f);
                cards.Add((id, record));
            }
            RectTransform diff = UiRoot.CreateRect("Difficulty", panel);
            Widgets.Place(diff, new Vector2(0.5f, 0.2f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200f, 60f));
            var dh = diff.gameObject.AddComponent<HorizontalLayoutGroup>();
            dh.spacing = 24f;
            dh.childAlignment = TextAnchor.MiddleCenter;
            dh.childControlWidth = dh.childControlHeight = false;
            foreach (var (id, label) in new[] { ("easy", "쉬움"), ("normal", "보통"), ("hard", "어려움") })
            {
                string level = id;
                Button b = Widgets.Button("D_" + id, diff, catalog, label, () => { SceneFlow.SelectedDifficulty = level; RefreshSelect(); }, 240f, 56f);
                difficultyButtons.Add((id, b.GetComponent<Image>()));
            }
            Button back = Widgets.Button("Back", panel, catalog, "뒤로", () => ShowSelect(false));
            Widgets.Place((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(360f, 60f));
            selectPanel.SetActive(false);
        }

        public static string MissionLabel(string mission, bool bombEnabled)
        {
            switch (mission)
            {
                case "steal": return "자료 탈취";
                case "steal_exit": return bombEnabled ? "폭탄 해체" : "자료 탈취 후 탈출";
                case "bomb": return "폭탄 해체";
                default: return "소탕";
            }
        }

        public void ShowSelect(bool show)
        {
            selectPanel.SetActive(show);
            mainPanel.SetActive(!show);
            if (show) RefreshSelect();
        }

        void RefreshSelect()
        {
            foreach (var (id, record) in cards)
            {
                MissionRecord r = GameSession.Data.Find(id, SceneFlow.SelectedDifficulty);
                record.text = r == null ? "최고 기록 없음" : "최고 " + r.clear_time_s.ToString("F3") + "초\n발각 " + r.detections + " / 처치 " + r.kills;
            }
            foreach (var (id, bg) in difficultyButtons)
                bg.color = id == SceneFlow.SelectedDifficulty ? new Color(1f, 0.6f, 0.6f) : Color.white;
        }

        // 저장이 손상되고 백업도 없을 때: 동의 후 초기화(7.13)
        void BuildConsent()
        {
            RectTransform panel = UiRoot.CreateRect("Consent", ui.Rect);
            Widgets.Stretch(panel);
            consentPanel = panel.gameObject;
            Widgets.Dimmer(panel, 0.85f);
            TextMeshProUGUI text = Widgets.Label("Text", panel, catalog, "저장 파일이 손상되었고 백업도 없습니다.\n손상된 파일은 save.json.corrupt로 보존했습니다.\n저장을 초기화할까요?", 36f, TextAlignmentOptions.Center);
            Widgets.Place(text.rectTransform, new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200f, 200f));
            Button yes = Widgets.Button("Yes", panel, catalog, "초기화", () => { GameSession.ResetCorrupt(); consentPanel.SetActive(false); });
            Widgets.Place((RectTransform)yes.transform, new Vector2(0.5f, 0.4f), new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(300f, 60f));
            Button no = Widgets.Button("No", panel, catalog, "나중에", () => consentPanel.SetActive(false));
            Widgets.Place((RectTransform)no.transform, new Vector2(0.5f, 0.4f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(300f, 60f));
        }

        void Update()
        {
            audioManager.Tick();
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (settings.IsOpen) settings.Close();
                else if (credits.IsOpen) credits.Close();
                else if (selectPanel.activeSelf) ShowSelect(false);
            }
        }
    }
}
