
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HealthHUD : MonoBehaviour
{
    // =========================================================
    // 1. HP 및 Armor 데이터 설정
    // =========================================================

    [Header("Health Settings")]

    // 현재 HP
    // 실제 게임에서는 Health.cs의 Hp 값을 전달받는다.
    // 현재는 HUD 디자인 확인을 위한 임시값이다.
    [SerializeField] private float health = 70f;

    // HP 최대값
    // 게임 시작 시 HP 70이 풀 체력이다.
    [SerializeField] private float maxHealth = 70f;


    [Header("Armor Settings")]

    // 현재 Armor
    // 실제 게임에서는 Health.cs의 Armor 값을 전달받는다.
    // 현재는 HUD 디자인 확인을 위한 임시값이다.
    [SerializeField] private float armor = 20f;

    // Armor 최대값
    // 게임 시작 시 Armor 20이 풀 방어구다.
    [SerializeField] private float maxArmor = 20f;

    // =========================================================
    // 공통 폰트 설정
    // =========================================================

    // Unity에서 생성한 TextMeshPro용 한글 폰트 에셋을 연결한다.
    // Inspector에서 TMP Font Asset을 지정하면 모든 HUD 글씨에 적용된다.
    [SerializeField]
    private TMP_FontAsset hudFont;

// =========================================================
// 2. HUD 디자인 색상 설정
// =========================================================

// 패널 내부 배경색
// 무기 HUD와 동일한 어두운 청회색을 사용하여 두 HUD의 내부 배경을 통일한다.
private readonly Color panelColor =
new Color32(23, 29, 36, 225);

// 패널 테두리 색상
// 무기 HUD와 동일한 밝은 연녹색을 사용하여 패널 외곽선을 강조한다.
private readonly Color outlineColor =
    new Color32(164, 214, 164, 255);

// 강조 색상
// HP와 ARMOR 제목 글씨에 사용하며, 패널 테두리와 동일한 연녹색을 적용한다.
private readonly Color accentColor =
    new Color32(164, 214, 164, 255);

// 비활성 게이지 색상
// 체력이나 방어력이 채워지지 않은 게이지 영역을 표시한다.
private readonly Color inactiveColor =
    new Color32(65, 77, 88, 255);

// HP 게이지 색상
// 현재 체력 수치를 붉은색 막대로 표시한다.
private readonly Color healthColor =
    new Color32(198, 76, 82, 255);

// Armor 게이지 색상
// 현재 방어력 수치를 파란색 막대로 표시한다.
private readonly Color armorColor =
    new Color32(74, 144, 217, 255);

    // =========================================================
    // 3. UI 오브젝트 참조 변수
    // =========================================================

    // HP 숫자를 표시하는 텍스트
    private TMP_Text healthText;

    // Armor 숫자를 표시하는 텍스트
    private TMP_Text armorText;

    // HP 게이지의 채워지는 부분
    private RectTransform healthFill;

    // Armor 게이지의 채워지는 부분
    private RectTransform armorFill;


    // =========================================================
    // 4. 최초 실행
    // =========================================================

    private void Start()
    {
        // Canvas, 패널, 텍스트, 게이지를 생성한다.
        CreateHUD();

        // 초기 HP와 Armor를 화면에 표시한다.
        Refresh();
    }


    // =========================================================
    // 5. HP 데이터 전달 함수
    // =========================================================

    // [데이터 출처]
    // 기존 게임 스크립트 Health.cs의 Hp 값
    //
    // [데이터 전달]
    // 전달받은 현재 HP와 최대 HP를 저장한다.
    //
    // [HUD 처리]
    // Refresh()를 호출하여 숫자와 게이지를 갱신한다.
    public void SetHealth(float current, float maximum)
    {
        maxHealth = Mathf.Max(0f, maximum);
        health = Mathf.Clamp(current, 0f, maxHealth);

        Refresh();
    }


    // =========================================================
    // 6. Armor 데이터 전달 함수
    // =========================================================

    // [데이터 출처]
    // 기존 게임 스크립트 Health.cs의 Armor 값
    //
    // [데이터 전달]
    // 전달받은 현재 Armor와 최대 Armor를 저장한다.
    //
    // [HUD 처리]
    // Refresh()를 호출하여 숫자와 게이지를 갱신한다.
    public void SetArmor(float current, float maximum)
    {
        maxArmor = Mathf.Max(0f, maximum);
        armor = Mathf.Clamp(current, 0f, maxArmor);

        Refresh();
    }


    // =========================================================
    // 7. HP 및 Armor 화면 갱신
    // =========================================================

    // 저장된 HP와 Armor를 실제 화면에 표시한다.
    //
    // 숫자: 현재값 / 최대값
    // 게이지: 현재값이 최대값에서 차지하는 비율
    private void Refresh()
    {
        if (healthText == null || armorText == null)
            return;

        // HP 숫자 표시
        healthText.text =
            $"{Mathf.CeilToInt(health)} / {Mathf.CeilToInt(maxHealth)}";

        // Armor 숫자 표시
        armorText.text =
            $"{Mathf.CeilToInt(armor)} / {Mathf.CeilToInt(maxArmor)}";

        // HP 게이지 갱신
        SetBar(healthFill, health, maxHealth);

        // Armor 게이지 갱신
        SetBar(armorFill, armor, maxArmor);
    }


    // =========================================================
    // 8. 게이지 길이 계산
    // =========================================================

    // 현재값을 최대값으로 나누어 게이지 비율을 계산한다.
    //
    // HP 70 / 70 = 100%
    // HP 35 / 70 =  50%
    // HP  0 / 70 =   0%
    //
    // 계산된 비율을 Fill 오브젝트의 가로 길이에 적용한다.
    private void SetBar(
        RectTransform fill,
        float current,
        float maximum)
    {
        if (fill == null)
            return;

        float ratio = maximum > 0f
            ? current / maximum
            : 0f;

        ratio = Mathf.Clamp01(ratio);

        // 왼쪽은 고정하고 오른쪽 길이를 비율에 맞게 조절한다.
        fill.anchorMin = new Vector2(0f, 0f);
        fill.anchorMax = new Vector2(ratio, 1f);

        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;
    }


    // =========================================================
    // 9. HUD 전체 UI 생성
    // =========================================================

    private void CreateHUD()
    {
        // -----------------------------------------------------
        // 9-1. Canvas 생성
        // -----------------------------------------------------

        // HUD 전용 Canvas를 생성한다.
        GameObject canvasObject = new GameObject("HealthHUDCanvas");

        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();

        // 화면 위에 HUD를 고정한다.
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // 다른 UI보다 앞쪽에 표시한다.
        canvas.sortingOrder = 100;

        // 해상도에 따라 UI 크기를 조절한다.
        CanvasScaler scaler =
            canvasObject.AddComponent<CanvasScaler>();

        scaler.uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;

        scaler.referenceResolution =
            new Vector2(1920, 1080);

        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        // Canvas를 화면 전체 크기로 설정한다.
        RectTransform canvasRect =
            canvasObject.GetComponent<RectTransform>();

        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.offsetMin = Vector2.zero;
        canvasRect.offsetMax = Vector2.zero;


        // -----------------------------------------------------
        // 9-2. HP / Armor 패널 생성
        // -----------------------------------------------------

        // 패널 크기와 위치
        // 기존에 네가 조정한 크기와 위치를 그대로 유지한다.
        // 왼쪽 아래를 기준으로 HP와 Armor 정보를 표시할 패널을 생성한다.
        GameObject panel = CreateObject(
            "HealthPanel",
            canvasObject.transform,
            new Vector2(320, 125),
            new Vector2(0, 0),
            new Vector2(30, 55)
        );

        // 패널 기준점을 왼쪽 아래로 설정한다.
        // 이후 패널 내부 UI 요소의 위치를 배치할 때 기준으로 사용한다.
        RectTransform panelRect =
            panel.GetComponent<RectTransform>();

        panelRect.pivot = new Vector2(0, 0);

        // 패널 내부 배경색 적용
        // 무기 HUD와 동일한 어두운 청회색을 사용하여 두 HUD의 디자인을 통일한다.
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = panelColor;

        // 패널 외곽선 적용
        // 무기 HUD와 동일한 밝은 연녹색 테두리를 추가한다.
        // 내부 배경과 외곽선의 색상 대비로 전술 장비 패널 느낌을 표현한다.
        Outline outline = panel.AddComponent<Outline>();
        outline.effectColor = outlineColor;
        outline.effectDistance = new Vector2(1, -1);

        // -----------------------------------------------------
        // 9-3. HP 제목 및 숫자 생성
        // -----------------------------------------------------

        // HP 제목
        CreateText(
            "HP Label",
            panel.transform,
            "HP",
            18,
            accentColor,
            new Vector2(20, -18),
            new Vector2(55, 25)
        );

        // 현재 HP / 최대 HP
        healthText = CreateText(
            "HP Value",
            panel.transform,
            "70 / 70",
            18,
            Color.white,
            new Vector2(215, -18),
            new Vector2(85, 25),
            TextAlignmentOptions.Right
        );


        // -----------------------------------------------------
        // 9-4. HP 게이지 생성
        // -----------------------------------------------------

        // HP 게이지는 붉은색으로 표시한다.
        healthFill = CreateBar(
            "HP Bar",
            panel.transform,
            new Vector2(280, 12),
            new Vector2(20, -48),
            healthColor
        );


        // -----------------------------------------------------
        // 9-5. Armor 제목 및 숫자 생성
        // -----------------------------------------------------

        // Armor 제목
        CreateText(
            "Armor Label",
            panel.transform,
            "ARMOR",
            18,
            accentColor,
            new Vector2(20, -70),
            new Vector2(85, 25)
        );

        // 현재 Armor / 최대 Armor
        armorText = CreateText(
            "Armor Value",
            panel.transform,
            "20 / 20",
            18,
            Color.white,
            new Vector2(215, -70),
            new Vector2(85, 25),
            TextAlignmentOptions.Right
        );


        // -----------------------------------------------------
        // 9-6. Armor 게이지 생성
        // -----------------------------------------------------

        // Armor 게이지는 파란색으로 표시한다.
        armorFill = CreateBar(
            "Armor Bar",
            panel.transform,
            new Vector2(280, 12),
            new Vector2(20, -100),
            armorColor
        );
    }


    // =========================================================
    // 10. 게이지 배경과 Fill 생성
    // =========================================================

    // Background: 게이지가 줄어들었을 때 남는 뒷부분
    // Fill: 현재 HP 또는 Armor 비율만큼 채워지는 부분
    //
    // Fill의 RectTransform을 반환하여
    // SetBar()에서 게이지 길이를 조절할 수 있도록 한다.
    private RectTransform CreateBar(
        string objectName,
        Transform parent,
        Vector2 size,
        Vector2 position,
        Color fillColor)
    {
        // 게이지 전체 배경 생성
        GameObject background = CreateObject(
            objectName + " Background",
            parent,
            size,
            new Vector2(0, 1),
            position
        );

        Image backgroundImage =
            background.AddComponent<Image>();

        backgroundImage.color = inactiveColor;

        // 실제 게이지가 채워지는 Fill 생성
        GameObject fill = CreateObject(
            objectName + " Fill",
            background.transform,
            size,
            Vector2.zero,
            Vector2.zero
        );

        RectTransform fillRect =
            fill.GetComponent<RectTransform>();

        // Fill이 배경 전체를 기준으로 크기를 조절하도록 설정한다.
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        // HP 또는 Armor에 맞는 색상을 적용한다.
        Image fillImage = fill.AddComponent<Image>();
        fillImage.color = fillColor;

        return fillRect;
    }


    // =========================================================
    // 11. 텍스트 UI 생성
    // =========================================================

    // HP, ARMOR 제목과 숫자를 표시하는 TextMeshPro를 생성한다.
    private TMP_Text CreateText(
        string objectName,
        Transform parent,
        string text,
        int fontSize,
        Color color,
        Vector2 position,
        Vector2 size,
        TextAlignmentOptions alignment =
            TextAlignmentOptions.Left)
    {
        GameObject textObject = CreateObject(
            objectName,
            parent,
            size,
            new Vector2(0, 1),
            position
        );

        TMP_Text tmpText =
    textObject.AddComponent<TextMeshProUGUI>();

        // Inspector에 지정한 한글 폰트를 텍스트에 적용한다.
        // 폰트가 지정되지 않았다면 TextMeshPro 기본 폰트를 사용한다.
        if (hudFont != null)
        {
            tmpText.font = hudFont;
        }

        tmpText.text = text;
        tmpText.fontSize = fontSize;
        tmpText.color = color;
        tmpText.alignment = alignment;

        return tmpText;
    }


    // =========================================================
    // 12. 기본 UI 오브젝트 생성
    // =========================================================

    // 패널, 텍스트, 게이지 등을 생성할 때 사용하는 공통 함수다.
    //
    // parent: 생성할 오브젝트의 부모
    // size: 오브젝트의 가로와 세로 크기
    // anchor: 부모를 기준으로 하는 위치 기준점
    // position: 기준점에서 떨어진 거리
    private GameObject CreateObject(
        string objectName,
        Transform parent,
        Vector2 size,
        Vector2 anchor,
        Vector2 position)
    {
        GameObject obj = new GameObject(objectName);

        // 부모 오브젝트 아래에 배치한다.
        obj.transform.SetParent(parent, false);

        RectTransform rect =
            obj.AddComponent<RectTransform>();

        // 위치 기준점을 설정한다.
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;

        // 기본 피벗은 왼쪽 위로 설정한다.
        rect.pivot = new Vector2(0, 1);

        // 크기와 위치를 적용한다.
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        return obj;
    }
}
