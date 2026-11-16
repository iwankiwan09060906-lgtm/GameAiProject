
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GameAi;

[DisallowMultipleComponent]
public sealed class WeaponHUDPreview : MonoBehaviour
{
    [Header("MVP")]
    [SerializeField] private GameManager gameManager;

    [Header("Weapon Sprites")]
    [SerializeField] private Sprite pistolImage;
    [SerializeField] private Sprite silencedPistolImage;
    [SerializeField] private Sprite rifleImage;

    [Header("Utility Sprites")]
    [SerializeField] private Sprite smokeImage;
    [SerializeField] private Sprite nvgImage;

    private static readonly Color PanelColor = new Color32(23, 29, 36, 225);
    private static readonly Color AccentColor = new Color32(164, 214, 164, 255);
    private static readonly Color InactiveColor = new Color32(65, 77, 88, 255);
    private static readonly Color White = Color.white;

    private readonly Image[] ammoIcons = new Image[5];
    private readonly Image[] slotBackgrounds = new Image[3];
    private readonly TMP_Text[] slotTexts = new TMP_Text[3];

    private Image weaponImage;
    private Image smokeIcon;
    private Image nvgIcon;

    private TMP_Text weaponNameText;
    private TMP_Text ammoInfoText;
    private TMP_Text smokeCountText;
    private TMP_Text nvgBatteryText;

    private WeaponInventory inventory;
    private string currentWeaponId;

    private int smokeCount = 2;
    private float nvgBattery = 60f;
    private bool nvgEnabled = true;

    private bool connected;

    private void OnEnable()
    {
        EventBus.Subscribe<AmmoChangedEvent>(OnAmmoChanged);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<AmmoChangedEvent>(OnAmmoChanged);
    }

    private void Start()
    {
        CreateHUD();
        TryConnectToMVP();
    }

    private void Update()
    {
        // 게임 초기화가 늦어진 경우에만 연결을 재시도한다.
        if (!connected)
            TryConnectToMVP();
    }

    private void TryConnectToMVP()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        if (gameManager == null || gameManager.Weapons == null)
            return;

        inventory = gameManager.Weapons.Inventory;

        if (inventory == null || inventory.Current == null)
            return;

        connected = true;
        RefreshWeapon();
    }

    private void OnAmmoChanged(AmmoChangedEvent evt)
    {
        if (!connected)
        {
            TryConnectToMVP();
            return;
        }

        RefreshWeapon();
    }

    private void RefreshWeapon()
    {
        if (inventory == null || inventory.Current == null)
            return;

        WeaponSlot slot = inventory.Current;
        WeaponDef def = slot.Def;

        weaponNameText.text = def.display_name;
        ammoInfoText.text =
            "MAG  " + slot.Mag + " / " + def.mag_size +
            "       RES  " + slot.Reserve;

        int selectedSlot = inventory.CurrentIndex + 1;

        UpdateWeaponImage(def.id);
        UpdateAmmoIcons(slot.Mag, def.mag_size);
        UpdateSlots(selectedSlot);
    }

    private void UpdateWeaponImage(string weaponId)
    {
        if (weaponId == currentWeaponId)
            return;

        currentWeaponId = weaponId;

        Sprite sprite = null;

        switch (weaponId)
        {
            case "pistol":
                sprite = pistolImage;
                break;

            case "silenced_pistol":
                sprite = silencedPistolImage;
                break;

            case "rifle":
                sprite = rifleImage;
                break;
        }

        weaponImage.sprite = sprite;
        weaponImage.enabled = sprite != null;
        weaponImage.color = White;

        if (sprite == null)
            Debug.LogWarning("[WeaponHUD] Missing weapon sprite: " + weaponId);
    }

    private void UpdateAmmoIcons(int current, int maximum)
    {
        int filled = Mathf.CeilToInt(
            (float)current / Mathf.Max(1, maximum) * ammoIcons.Length
        );

        for (int i = 0; i < ammoIcons.Length; i++)
            ammoIcons[i].color = i < filled ? AccentColor : InactiveColor;
    }

    private void UpdateSlots(int selected)
    {
        for (int i = 0; i < slotBackgrounds.Length; i++)
        {
            bool active = selected == i + 1;

            slotBackgrounds[i].color =
                active ? AccentColor : InactiveColor;

            slotTexts[i].color =
                active ? PanelColor : White;
        }
    }

    // 외부 시스템에서 연막탄과 NVG 수치를 전달할 때 사용
    public void SetUtilityData(bool nvg, float battery, int smoke)
    {
        nvgEnabled = nvg;
        nvgBattery = Mathf.Max(0f, battery);
        smokeCount = Mathf.Max(0, smoke);

        if (smokeCountText == null)
            return;

        smokeCountText.text = "x" + smokeCount;
        nvgBatteryText.text = nvgEnabled
            ? Mathf.CeilToInt(nvgBattery) + "s"
            : "OFF";

        nvgIcon.color = nvgEnabled ? White : InactiveColor;
    }

    private void CreateHUD()
    {
        GameObject canvasObject = new GameObject("WeaponHUDCanvas");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panel = CreateUIObject(
            "WeaponPanel",
            canvasObject.transform,
            new Vector2(430, 245),
            new Vector2(1, 0),
            new Vector2(-30, 55)
        );

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.pivot = new Vector2(1, 0);

        Image background = panel.AddComponent<Image>();
        background.color = PanelColor;

        Outline outline = panel.AddComponent<Outline>();
        outline.effectColor = AccentColor;
        outline.effectDistance = new Vector2(1, -1);

        Transform root = panel.transform;

        weaponNameText = CreateText(
            "WeaponName", root, "", 20, AccentColor,
            new Vector2(16, -12), new Vector2(260, 28),
            TextAlignmentOptions.Left
        );

        // 왼쪽: 탄약 표시
        for (int i = 0; i < ammoIcons.Length; i++)
        {
            GameObject icon = CreateUIObject(
                "AmmoIcon_" + i, root,
                new Vector2(11, 23),
                new Vector2(0, 1),
                new Vector2(17 + i * 17, -55)
            );

            ammoIcons[i] = icon.AddComponent<Image>();
            ammoIcons[i].color = InactiveColor;
        }

        smokeIcon = CreateUtilityImage(
            "SmokeIcon", root, smokeImage,
            new Vector2(25, -105), new Vector2(35, 35)
        );

        smokeCountText = CreateText(
            "SmokeCount", root, "x" + smokeCount, 16, White,
            new Vector2(66, -107), new Vector2(55, 30),
            TextAlignmentOptions.Left
        );

        nvgIcon = CreateUtilityImage(
            "NVGIcon", root, nvgImage,
            new Vector2(25, -157), new Vector2(35, 35)
        );

        nvgBatteryText = CreateText(
            "NVGBattery", root, "60s", 16, White,
            new Vector2(66, -159), new Vector2(55, 30),
            TextAlignmentOptions.Left
        );

        // 오른쪽: 총기 이미지
        GameObject imageArea = CreateUIObject(
            "WeaponImageArea", root,
            new Vector2(275, 105),
            new Vector2(0, 1),
            new Vector2(140, -42)
        );

        Image imageBackground = imageArea.AddComponent<Image>();
        imageBackground.color = new Color32(35, 45, 54, 255);

        weaponImage = CreateImage(
            "WeaponSprite", imageArea.transform,
            null, Vector2.zero, new Vector2(275, 105)
        );

        weaponImage.preserveAspect = true;
        weaponImage.enabled = false;

        ammoInfoText = CreateText(
            "AmmoInfo", root, "MAG  0 / 0       RES  0",
            14, White,
            new Vector2(140, -155), new Vector2(275, 30),
            TextAlignmentOptions.Center
        );

        // 아래: 무기 슬롯
        for (int i = 0; i < 3; i++)
        {
            GameObject slot = CreateUIObject(
                "WeaponSlot_" + (i + 1), root,
                new Vector2(48, 27),
                new Vector2(0.5f, 0),
                new Vector2((i - 1) * 58, 12)
            );

            RectTransform slotRect = slot.GetComponent<RectTransform>();
            slotRect.pivot = new Vector2(0.5f, 0);

            slotBackgrounds[i] = slot.AddComponent<Image>();
            slotBackgrounds[i].color = InactiveColor;

            slotTexts[i] = CreateText(
                "SlotText", slot.transform,
                (i + 1).ToString(), 15, White,
                Vector2.zero, new Vector2(48, 27),
                TextAlignmentOptions.Center
            );

            RectTransform textRect = slotTexts[i].rectTransform;
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
        }

        SetUtilityData(nvgEnabled, nvgBattery, smokeCount);
    }

    private Image CreateUtilityImage(
        string objectName, Transform parent, Sprite sprite,
        Vector2 position, Vector2 size)
    {
        Image image = CreateImage(
            objectName, parent, sprite, position, size
        );

        image.color = sprite != null ? White : InactiveColor;
        return image;
    }

    private Image CreateImage(
        string objectName, Transform parent, Sprite sprite,
        Vector2 position, Vector2 size)
    {
        GameObject obj = CreateUIObject(
            objectName, parent, size,
            new Vector2(0, 1), position
        );

        Image image = obj.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.color = White;
        image.raycastTarget = false;

        return image;
    }

    private GameObject CreateUIObject(
        string objectName, Transform parent,
        Vector2 size, Vector2 anchor, Vector2 position)
    {
        GameObject obj = new GameObject(
            objectName, typeof(RectTransform)
        );

        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0, 1);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        return obj;
    }

    private TMP_Text CreateText(
        string objectName, Transform parent,
        string value, float fontSize, Color color,
        Vector2 position, Vector2 size,
        TextAlignmentOptions alignment)
    {
        GameObject obj = CreateUIObject(
            objectName, parent, size,
            new Vector2(0, 1), position
        );

        TMP_Text text = obj.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.enableAutoSizing = false;

        return text;
    }
}
