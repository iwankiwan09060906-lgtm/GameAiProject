using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameAi
{
    // HUD(10장): 좌하단 통합 패널(체력·방탄, 밝기, 소음, 무기 칸) + 자세 실루엣, 좌상단 목표·타이머, 우상단 전역 경계
    public sealed class Hud : MonoBehaviour
    {
        const int Ticks = 10;
        const float NoiseWindow = 0.5f;
        const float NoiseScale = 25f;

        static readonly Color HpColor = new Color32(0xC8, 0x32, 0x3C, 0xFF);
        static readonly Color ArmorColor = new Color32(0x8F, 0xD3, 0xF0, 0xFF);
        static readonly Color LightColor = new Color32(0x37, 0xC8, 0x71, 0xFF);
        static readonly Color NoiseColor = new Color32(0xF2, 0xC2, 0x30, 0xFF);
        static readonly Color Off = new Color32(0x30, 0x30, 0x38, 0xFF);

        AssetCatalog catalog;
        int maxHp = 100, maxArmor = 50;
        int hp = 100, armor = 50;
        int mag, reserve, magSize = 1;
        string weaponId = "pistol";
        float noiseLevel;
        float noiseTime = float.NegativeInfinity;
        int alertLevel;

        Image hpBar, armorBar, weaponImage, stance;
        Image[] lightTicks, noiseTicks, magTicks;
        RectTransform lightRow;
        TextMeshProUGUI ammoText, weaponName, objectiveText, timerText, alertText, batteryText, decoyText;

        public Func<float> LightLevel { get; set; }
        public Func<string> Objective { get; set; } = () => "";
        public Func<float> Timer { get; set; } = () => 0f;
        public Func<bool> TimerIsCountdown { get; set; } = () => false;
        public Func<MoveMode> Mode { get; set; } = () => MoveMode.Walk;
        public Func<bool> Moving { get; set; } = () => false;
        public Func<float> Battery { get; set; }
        public Func<int> Decoys { get; set; }
        public Func<WeaponDef[]> WeaponDefs { get; set; }

        public void Init(UiRoot ui, AssetCatalog assetCatalog, int playerHp, int playerArmor)
        {
            catalog = assetCatalog;
            maxHp = hp = playerHp;
            maxArmor = armor = playerArmor;
            RectTransform root = (RectTransform)transform;
            Widgets.Stretch(root);

            Image panel = Widgets.Panel("Panel", root, catalog);
            Widgets.Place(panel.rectTransform, Vector2.zero, Vector2.zero, new Vector2(24f, 24f), new Vector2(540f, 236f));
            RectTransform p = panel.rectTransform;

            Image armorIcon = UiRoot.CreateImage("ArmorIcon", p, catalog.GetSprite("icon_armor"));
            Widgets.Place(armorIcon.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -16f), new Vector2(32f, 32f));
            hpBar = Bar(p, "Hp", new Vector2(60f, -16f), HpColor);
            armorBar = Bar(p, "Armor", new Vector2(60f, -34f), ArmorColor);

            lightRow = UiRoot.CreateRect("LightRow", p);
            Widgets.Place(lightRow, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -60f), new Vector2(500f, 32f));
            lightTicks = Meter(lightRow, "icon_light", LightColor);
            RectTransform noiseRow = UiRoot.CreateRect("NoiseRow", p);
            Widgets.Place(noiseRow, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -98f), new Vector2(500f, 32f));
            noiseTicks = Meter(noiseRow, "icon_noise", NoiseColor);

            weaponImage = UiRoot.CreateImage("Weapon", p, null);
            Widgets.Place(weaponImage.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(16f, 16f), new Vector2(128f, 64f));
            ammoText = Widgets.Label("Ammo", p, catalog, "", 36f);
            Widgets.Place(ammoText.rectTransform, Vector2.zero, Vector2.zero, new Vector2(160f, 40f), new Vector2(200f, 44f));
            RectTransform magRow = UiRoot.CreateRect("MagTicks", p);
            Widgets.Place(magRow, Vector2.zero, Vector2.zero, new Vector2(160f, 22f), new Vector2(200f, 12f));
            magTicks = new Image[30];
            for (int i = 0; i < magTicks.Length; i++)
            {
                magTicks[i] = Widgets.Fill("T" + i, magRow, Widgets.Text);
                Widgets.Place(magTicks[i].rectTransform, Vector2.zero, Vector2.zero, new Vector2(i * 6.5f, 0f), new Vector2(4f, 12f));
            }
            Image namePanel = Widgets.Panel("NamePanel", p, catalog, "panel_gold");
            Widgets.Place(namePanel.rectTransform, Vector2.zero, Vector2.zero, new Vector2(372f, 16f), new Vector2(152f, 44f));
            weaponName = Widgets.Label("Name", namePanel.transform, catalog, "", 24f, TextAlignmentOptions.Center);
            Widgets.Stretch(weaponName.rectTransform);
            batteryText = Widgets.Label("Battery", p, catalog, "", 24f, TextAlignmentOptions.Right);
            Widgets.Place(batteryText.rectTransform, Vector2.zero, Vector2.zero, new Vector2(372f, 64f), new Vector2(152f, 30f));
            decoyText = Widgets.Label("Decoys", p, catalog, "", 24f, TextAlignmentOptions.Right);
            Widgets.Place(decoyText.rectTransform, Vector2.zero, Vector2.zero, new Vector2(372f, 92f), new Vector2(152f, 30f));

            stance = UiRoot.CreateImage("Stance", root, catalog.GetSprite("stance_idle"));
            Widgets.Place(stance.rectTransform, Vector2.zero, Vector2.zero, new Vector2(580f, 24f), new Vector2(64f, 128f));

            objectiveText = Widgets.Label("Objective", root, catalog, "", 24f);
            Widgets.Place(objectiveText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -20f), new Vector2(900f, 32f));
            timerText = Widgets.Label("Timer", root, catalog, "", 24f);
            timerText.color = Widgets.Dim;
            Widgets.Place(timerText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -54f), new Vector2(400f, 32f));
            alertText = Widgets.Label("Alert", root, catalog, "", 24f, TextAlignmentOptions.Right);
            Widgets.Place(alertText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -20f), new Vector2(300f, 32f));
        }

        Image Bar(RectTransform parent, string name, Vector2 pos, Color color)
        {
            Image back = Widgets.Fill(name + "Back", parent, Off);
            Widgets.Place(back.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, new Vector2(400f, 14f));
            Image fill = Widgets.Fill(name, back.transform, color);
            Widgets.Place(fill.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(400f, 14f));
            return fill;
        }

        Image[] Meter(RectTransform row, string icon, Color color)
        {
            Image image = UiRoot.CreateImage("Icon", row, catalog.GetSprite(icon));
            Widgets.Place(image.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(32f, 32f));
            var ticks = new Image[Ticks];
            for (int i = 0; i < Ticks; i++)
            {
                ticks[i] = Widgets.Fill("T" + i, row, Off);
                Widgets.Place(ticks[i].rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(44f + i * 30f, 0f), new Vector2(22f, 16f));
                ticks[i].color = Off;
            }
            ticksColor[ticks] = color;
            return ticks;
        }

        readonly System.Collections.Generic.Dictionary<Image[], Color> ticksColor = new System.Collections.Generic.Dictionary<Image[], Color>();

        void SetMeter(Image[] ticks, float value)
        {
            int lit = Mathf.Clamp(Mathf.RoundToInt(value * Ticks), 0, Ticks);
            Color on = ticksColor[ticks];
            for (int i = 0; i < ticks.Length; i++) ticks[i].color = i < lit ? on : Off;
        }

        void OnEnable()
        {
            EventBus.Subscribe<HealthChangedEvent>(OnHealth);
            EventBus.Subscribe<AmmoChangedEvent>(OnAmmo);
            EventBus.Subscribe<NoiseEvent>(OnNoise);
            EventBus.Subscribe<GlobalAlertChangedEvent>(OnAlert);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<HealthChangedEvent>(OnHealth);
            EventBus.Unsubscribe<AmmoChangedEvent>(OnAmmo);
            EventBus.Unsubscribe<NoiseEvent>(OnNoise);
            EventBus.Unsubscribe<GlobalAlertChangedEvent>(OnAlert);
        }

        void OnHealth(HealthChangedEvent e)
        {
            if (e.entityId != EntityIds.Player) return;
            hp = e.hp;
            armor = e.armor;
        }

        // 시작할 때 현재 무기를 한 번 넣는다(첫 탄약 이벤트는 HUD보다 먼저 나간다)
        public void SetAmmo(string id, int magCount, int reserveCount)
        {
            OnAmmo(new AmmoChangedEvent { weaponId = id, mag = magCount, reserve = reserveCount });
        }

        void OnAmmo(AmmoChangedEvent e)
        {
            weaponId = e.weaponId;
            mag = e.mag;
            reserve = e.reserve;
            if (WeaponDefs != null)
                foreach (WeaponDef d in WeaponDefs())
                    if (d.id == e.weaponId) { magSize = d.mag_size; weaponName.text = d.display_name; }
            weaponImage.sprite = catalog.GetSprite("weapon_" + e.weaponId);
        }

        // 최근 0.5초 동안 플레이어가 낸 가장 큰 소음 반경 ÷ 25m
        void OnNoise(NoiseEvent e)
        {
            if (e.sourceId != EntityIds.Player) return;
            float level = e.radius / NoiseScale;
            if (GameClock.Time - noiseTime > NoiseWindow || level >= noiseLevel)
            {
                noiseLevel = level;
                noiseTime = GameClock.Time;
            }
        }

        void OnAlert(GlobalAlertChangedEvent e) { alertLevel = e.level; }

        public void Tick()
        {
            hpBar.rectTransform.sizeDelta = new Vector2(400f * Mathf.Clamp01((float)hp / maxHp), 14f);
            armorBar.rectTransform.sizeDelta = new Vector2(400f * Mathf.Clamp01((float)armor / Mathf.Max(1, maxArmor)), 14f);
            bool hasLight = LightLevel != null;
            lightRow.gameObject.SetActive(hasLight);
            if (hasLight) SetMeter(lightTicks, LightLevel());
            if (GameClock.Time - noiseTime > NoiseWindow) noiseLevel = 0f;
            SetMeter(noiseTicks, noiseLevel);
            ammoText.text = mag + " | " + reserve;
            for (int i = 0; i < magTicks.Length; i++)
            {
                magTicks[i].enabled = i < magSize;
                magTicks[i].color = i < mag ? Widgets.Text : Off;
            }
            float battery = Battery != null ? Battery() : -1f;
            batteryText.text = battery >= 0f ? "NVG " + Mathf.RoundToInt(battery * 100f) + "%" : "";
            decoyText.text = Decoys != null ? "연막탄 " + Decoys() : "";
            MoveMode mode = Mode();
            string pose = !Moving() ? (mode == MoveMode.Crouch ? "stance_crouch" : "stance_idle") : mode == MoveMode.Run ? "stance_run" : mode == MoveMode.Crouch ? "stance_crouch" : "stance_walk";
            stance.sprite = catalog.GetSprite(pose);
            objectiveText.text = Objective();
            float t = Timer();
            int total = Mathf.Max(0, Mathf.FloorToInt(t));
            timerText.text = (TimerIsCountdown() ? "남은 시간 " : "") + (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
            alertText.text = alertLevel == 2 ? "전투" : alertLevel == 1 ? "경계" : "평상";
            alertText.color = alertLevel == 2 ? Widgets.Accent : alertLevel == 1 ? (Color)new Color32(0xF2, 0xC2, 0x30, 0xFF) : Widgets.Dim;
        }
    }
}
