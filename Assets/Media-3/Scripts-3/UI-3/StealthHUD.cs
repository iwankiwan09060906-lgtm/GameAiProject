
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class StealthHUD : MonoBehaviour
{
    [Header("폰트")]
    [SerializeField] private TMP_FontAsset hudFont;

    [Header("아이콘 이미지")]
    [SerializeField] private Sprite suspicionImage;
    [SerializeField] private Sprite soundImage;

    [Header("색상")]
    [SerializeField]
    private Color panelColor =
        new Color32(23, 29, 36, 225);

    [SerializeField]
    private Color accentColor =
        new Color32(164, 214, 164, 255);

    [SerializeField]
    private Color suspicionColor =
        new Color32(213, 168, 76, 255);

    [SerializeField]
    private Color inactiveColor =
        new Color32(65, 77, 88, 255);

    private Image suspicionFill;
    private Image soundFill;

    private TMP_Text suspicionValue;
    private TMP_Text soundValue;

    private void Start()
    {
        CreateHUD();

        // 임시 확인용 수치
        SetSuspicion(35);
        SetSound(60);
    }

    // 의심도 수치 변경
    public void SetSuspicion(float value)
    {
        value = Mathf.Clamp(value, 0f, 100f);

        if (suspicionFill == null)
            return;

        suspicionFill.fillAmount = value / 100f;
        suspicionValue.text = Mathf.RoundToInt(value) + "%";
    }

    // 소리 수치 변경
    public void SetSound(float value)
    {
        value = Mathf.Clamp(value, 0f, 100f);

        if (soundFill == null)
            return;

        soundFill.fillAmount = value / 100f;
        soundValue.text = Mathf.RoundToInt(value) + "%";
    }

    private void CreateHUD()
    {
        // Canvas 생성
        GameObject canvasObject = new GameObject("StealthHUDCanvas");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // 오른쪽 위 패널
        GameObject panel = CreateObject(
            "StealthPanel",
            canvasObject.transform,
            new Vector2(190, 225),
            new Vector2(1, 1),
            new Vector2(-30, -30),
            new Vector2(1, 1)
        );

        Image background = panel.AddComponent<Image>();
        background.color = panelColor;

        Outline outline = panel.AddComponent<Outline>();
        outline.effectColor = accentColor;
        outline.effectDistance = new Vector2(1, -1);

        // 왼쪽: 의심도
        CreateGauge(
            panel.transform,
            "Suspicion",
            "의심도",
            -45,
            suspicionColor,
            suspicionImage,
            out suspicionFill,
            out suspicionValue
        );

        // 오른쪽: 소리
        CreateGauge(
            panel.transform,
            "Sound",
            "소리",
            45,
            accentColor,
            soundImage,
            out soundFill,
            out soundValue
        );
    }

    private void CreateGauge(
        Transform parent,
        string objectName,
        string title,
        float centerX,
        Color fillColor,
        Sprite iconSprite,
        out Image fill,
        out TMP_Text valueText)
    {
        // 제목
        CreateText(
            objectName + "Title",
            parent,
            title,
            16,
            Color.white,
            new Vector2(centerX, -12),
            new Vector2(70, 24)
        );

        // 막대 배경
        GameObject bar = CreateObject(
            objectName + "Bar",
            parent,
            new Vector2(13, 95),
            new Vector2(0.5f, 1),
            new Vector2(centerX, -42),
            new Vector2(0.5f, 1)
        );

        Image barBackground = bar.AddComponent<Image>();
        barBackground.color = inactiveColor;

        // 막대 채움 영역
        GameObject fillObject = CreateObject(
            objectName + "Fill",
            bar.transform,
            Vector2.zero,
            Vector2.zero,
            Vector2.zero,
            new Vector2(0.5f, 0.5f)
        );

        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        fill = fillObject.AddComponent<Image>();
        fill.color = fillColor;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Vertical;
        fill.fillOrigin = (int)Image.OriginVertical.Bottom;
        fill.fillAmount = 0f;

        // 막대 바로 아래 이미지
        GameObject icon = CreateObject(
            objectName + "Icon",
            parent,
            new Vector2(42, 42),
            new Vector2(0.5f, 1),
            new Vector2(centerX, -148),
            new Vector2(0.5f, 1)
        );

        Image iconImage = icon.AddComponent<Image>();
        iconImage.sprite = iconSprite;
        iconImage.preserveAspect = true;
        iconImage.color = iconSprite != null ? Color.white : fillColor;
        iconImage.raycastTarget = false;

        // 수치
        valueText = CreateText(
            objectName + "Value",
            parent,
            "0%",
            13,
            Color.white,
            new Vector2(centerX, -195),
            new Vector2(70, 20)
        );
    }

    private GameObject CreateObject(
        string objectName,
        Transform parent,
        Vector2 size,
        Vector2 anchor,
        Vector2 position,
        Vector2 pivot)
    {
        GameObject obj = new GameObject(
            objectName,
            typeof(RectTransform)
        );

        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.GetComponent<RectTransform>();

        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        return obj;
    }

    private TMP_Text CreateText(
        string objectName,
        Transform parent,
        string value,
        float fontSize,
        Color color,
        Vector2 position,
        Vector2 size)
    {
        GameObject obj = CreateObject(
            objectName,
            parent,
            size,
            new Vector2(0.5f, 1),
            position,
            new Vector2(0.5f, 1)
        );

        TMP_Text text = obj.AddComponent<TextMeshProUGUI>();

        text.text = value;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.enableAutoSizing = false;

        // 지정한 폰트 적용
        if (hudFont != null)
            text.font = hudFont;
        else
            text.font = TMP_Settings.defaultFontAsset;

        return text;
    }
}
