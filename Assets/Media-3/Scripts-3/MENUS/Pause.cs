
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;

/// <summary>
/// 일시 정지 UI 전체를 코드로 생성하고 관리하는 스크립트
///
/// [주요 기능]
/// 1. 작전 재개 / 작전 재출격 / 작전 중지
/// 2. 무기 1~3번 정보 표시
/// 3. 전체 음량 / 배경 음악 / 효과음 설정
/// 4. 조작 키 안내
/// 5. 퇴근 버튼
/// 6. 버튼 마우스 오버 효과 및 효과음
///
/// [주의]
/// 이 스크립트는 UI 화면만 담당한다.
/// 실제 게임 일시 정지, 무기 데이터, 씬 이동은
/// 추후 기존 게임 시스템과 연결한다.
/// </summary>
public class PauseUI : MonoBehaviour
{
    // =====================================================
    // 1. Inspector 설정
    // =====================================================

    [Header("폰트 설정")]
    [Tooltip("한글을 지원하는 TextMeshPro 폰트")]
    [SerializeField] private TMP_FontAsset hudFont;


    [Header("무기 정보")]

    [Tooltip("무기 1~3번 이미지")]
    [SerializeField] private Sprite[] weaponImages = new Sprite[3];

    [Tooltip("무기 1~3번 이름")]
    [SerializeField]
    private string[] weaponNames =
    {
        "무기 정보 없음",
        "무기 정보 없음",
        "무기 정보 없음"
    };

    [Tooltip("무기별 탄창 및 예비 탄약")]
    [SerializeField]
    private string[] weaponAmmoInfo =
    {
        "탄창: --\n예비 탄약: --",
        "탄창: --\n예비 탄약: --",
        "탄창: --\n예비 탄약: --"
    };


    [Header("조작 키 목록")]

    [SerializeField]
    private string[] controlActions =
    {
        "이동",
        "조준",
        "사격",
        "재장전",
        "상호작용",
        "일시 정지"
    };

    [SerializeField]
    private string[] controlKeys =
    {
        "W / A / S / D",
        "마우스",
        "마우스 좌클릭",
        "R",
        "E",
        "ESC"
    };


    [Header("효과음")]

    [SerializeField] private AudioClip hoverSound;
    [SerializeField] private AudioClip clickSound;

    [Range(0f, 1f)]
    [SerializeField] private float soundVolume = 0.7f;


    [Header("기능 연결")]

    [SerializeField] private UnityEvent onResumeRequested = new UnityEvent();
    [SerializeField] private UnityEvent onRedeployRequested = new UnityEvent();
    [SerializeField] private UnityEvent onStopOperationRequested = new UnityEvent();

    [SerializeField] private UnityEvent<float> onMasterVolumeChanged = new UnityEvent<float>();
    [SerializeField] private UnityEvent<float> onBgmVolumeChanged = new UnityEvent<float>();
    [SerializeField] private UnityEvent<float> onSfxVolumeChanged = new UnityEvent<float>();


    // =====================================================
    // 2. 색상 설정
    // =====================================================

    // 전체 패널 배경
    private readonly Color panelColor =
        new Color32(23, 29, 36, 245);       // #171D24

    // 초록색 강조 및 테두리
    private readonly Color outlineColor =
        new Color32(164, 214, 164, 255);    // #A4D6A4

    // 기본 버튼 및 슬롯 배경
    private readonly Color inactiveColor =
        new Color32(65, 77, 88, 255);       // #414D58

    // 마우스 오버 배경
    private readonly Color hoverColor =
        new Color32(38, 52, 62, 255);       // #26343E

    // 기본 글자색
    private readonly Color normalTextColor =
        new Color32(235, 239, 235, 255);


    // =====================================================
    // 3. 내부 UI 참조
    // =====================================================

    private Canvas pauseCanvas;
    private GameObject pauseRoot;
    private AudioSource audioSource;

    // 버튼 확대 애니메이션 관리
    private readonly Dictionary<Transform, Coroutine> scaleCoroutines =
        new Dictionary<Transform, Coroutine>();


    // =====================================================
    // 4. 초기 실행
    // =====================================================

    private void Start()
    {
        CreateAudioSource();
        CreateCanvas();
        CreateEventSystem();

        BuildPauseUI();

        // 게임 시작 시 일시 정지 UI는 숨긴다.
        SetOpen(false);
    }


    // =====================================================
    // 5. UI 열기 / 닫기
    // =====================================================

    /// <summary>
    /// 일시 정지 UI를 열거나 닫는다.
    /// 테스트용 입력 스크립트 또는 GameManager에서 호출한다.
    ///
    /// 실제 게임 일시 정지 상태는 변경하지 않는다.
    /// </summary>
    public void SetOpen(bool open)
    {
        if (pauseRoot == null)
            return;

        pauseRoot.SetActive(open);
    }


    // =====================================================
    // 6. 효과음 관리
    // =====================================================

    private void CreateAudioSource()
    {
        audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.volume = soundVolume;
    }


    private void PlaySound(AudioClip clip)
    {
        if (clip == null || audioSource == null)
            return;

        audioSource.PlayOneShot(clip, soundVolume);
    }


    // =====================================================
    // 7. Canvas 생성
    // =====================================================

    private void CreateCanvas()
    {
        GameObject canvasObject = new GameObject(
            "PauseCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster)
        );

        canvasObject.transform.SetParent(transform, false);

        pauseCanvas = canvasObject.GetComponent<Canvas>();

        pauseCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        pauseCanvas.overrideSorting = true;
        pauseCanvas.sortingOrder = 100;

        CanvasScaler scaler =
            canvasObject.GetComponent<CanvasScaler>();

        scaler.uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;

        scaler.referenceResolution =
            new Vector2(1920, 1080);

        scaler.matchWidthOrHeight = 0.5f;
    }


    // =====================================================
    // 8. EventSystem 확인
    // =====================================================

    private void CreateEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
            return;

        new GameObject(
            "PauseEventSystem",
            typeof(EventSystem),
            typeof(StandaloneInputModule)
        );
    }


    // =====================================================
    // 9. 일시 정지 화면 전체 구성
    // =====================================================

    private void BuildPauseUI()
    {
        pauseRoot = new GameObject(
            "PauseRoot",
            typeof(RectTransform)
        );

        pauseRoot.transform.SetParent(
            pauseCanvas.transform,
            false
        );

        RectTransform root =
            pauseRoot.GetComponent<RectTransform>();

        Stretch(root);


        // -------------------------------------------------
        // 게임 화면 어둡게 처리
        // -------------------------------------------------

        Image dimmer = CreateImage(
            "BackgroundDimmer",
            root,
            new Color(0f, 0f, 0f, 0.78f),
            Vector2.zero,
            Vector2.one
        );

        dimmer.raycastTarget = true;


        // -------------------------------------------------
        // 상단 제목
        // -------------------------------------------------

        CreateText(
            "PauseTitle",
            "작전 일시 정지",
            root,
            48,
            outlineColor,
            TextAlignmentOptions.Center,
            new Vector2(0.25f, 0.85f),
            new Vector2(0.75f, 0.96f)
        );

        // 기존 영문 부제 대신 짧은 한글 문구 사용
        CreateText(
            "PauseSubtitle",
            "작전 대기 중",
            root,
            19,
            normalTextColor,
            TextAlignmentOptions.Center,
            new Vector2(0.25f, 0.81f),
            new Vector2(0.75f, 0.86f)
        );


        // -------------------------------------------------
        // 퇴근 버튼 - 오른쪽 위로 이동
        // -------------------------------------------------

        CreateMenuButton(
            "QuitButton",
            "퇴근",
            root,
            new Vector2(0.82f, 0.87f),
            new Vector2(0.94f, 0.95f),
            OnQuitClicked
        );


        // -------------------------------------------------
        // 제목 아래 구분선
        // -------------------------------------------------

        CreateImage(
            "TitleLine",
            root,
            outlineColor,
            new Vector2(0.07f, 0.795f),
            new Vector2(0.93f, 0.800f)
        );


        // -------------------------------------------------
        // 왼쪽 메뉴 버튼
        // 기존 버튼 위치 유지, 글씨 크기 확대
        // -------------------------------------------------

        CreateMenuButton(
            "ResumeButton",
            "작전 재개",
            root,
            new Vector2(0.07f, 0.571f),
            new Vector2(0.28f, 0.67f),
            OnResumeClicked
        );

        CreateMenuButton(
            "RedeployButton",
            "작전 재출격",
            root,
            new Vector2(0.07f, 0.42f),
            new Vector2(0.28f, 0.52f),
            OnRedeployClicked
        );

        CreateMenuButton(
            "StopOperationButton",
            "작전 중지",
            root,
            new Vector2(0.07f, 0.27f),
            new Vector2(0.28f, 0.37f),
            OnStopOperationClicked
        );


        // -------------------------------------------------
        // 중앙 장비 확인 영역
        // 기존보다 살짝 왼쪽으로 이동
        // -------------------------------------------------

        CreateEquipmentSection(root);


        // -------------------------------------------------
        // 오른쪽 설정 영역
        // -------------------------------------------------

        CreateSettingsSection(root);
    }


    // =====================================================
    // 10. 장비 확인 영역
    // =====================================================

    private void CreateEquipmentSection(RectTransform root)
    {
        // 중앙 패널을 살짝 왼쪽으로 이동하고 넓이를 조정
        RectTransform section = CreateSection(
    "EquipmentSection",
    root,
    new Vector2(0.33f, 0.08f),
    new Vector2(0.62f, 0.76f)
);


        // 장비 확인 제목
        CreateText(
            "EquipmentTitle",
            "장비 확인",
            section,
            32,
            outlineColor,
            TextAlignmentOptions.Center,
            new Vector2(0.05f, 0.87f),
            new Vector2(0.95f, 0.98f)
        );


        // 무기 슬롯 3개를 세로로 배치
        // 세 번째 슬롯도 패널 안에 들어오도록 위치 수정
        float[] topPositions =
        {
            0.83f,
            0.55f,
            0.27f
        };

        float[] bottomPositions =
        {
            0.59f,
            0.31f,
            0.03f
        };

        for (int i = 0; i < 3; i++)
        {
            CreateWeaponSlot(
                section,
                i,
                new Vector2(0.05f, bottomPositions[i]),
                new Vector2(0.95f, topPositions[i])
            );
        }
    }


    // =====================================================
    // 11. 개별 무기 슬롯
    // =====================================================

    private void CreateWeaponSlot(
        RectTransform parent,
        int index,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        GameObject slotObject = new GameObject(
            "WeaponSlot_" + (index + 1),
            typeof(RectTransform),
            typeof(Image)
        );

        slotObject.transform.SetParent(parent, false);

        RectTransform slotRect =
            slotObject.GetComponent<RectTransform>();

        SetRect(slotRect, anchorMin, anchorMax);

        Image background =
            slotObject.GetComponent<Image>();

        background.color = inactiveColor;

        // 초록색 슬롯 테두리
        AddOutline(slotObject, outlineColor);


        // -------------------------------------------------
        // 무기 이미지
        // -------------------------------------------------

        Image weaponImage = CreateImage(
            "WeaponImage",
            slotObject.transform,
            hoverColor,
            new Vector2(0.035f, 0.12f),
            new Vector2(0.34f, 0.88f)
        );

        weaponImage.preserveAspect = true;

        if (weaponImages != null &&
            index < weaponImages.Length &&
            weaponImages[index] != null)
        {
            weaponImage.sprite = weaponImages[index];
            weaponImage.color = Color.white;
        }
        else
        {
            CreateText(
                "ImagePlaceholder",
                "이미지 없음",
                weaponImage.transform,
                15,
                normalTextColor,
                TextAlignmentOptions.Center,
                Vector2.zero,
                Vector2.one
            );
        }


        // -------------------------------------------------
        // 무기 번호
        // -------------------------------------------------

        CreateText(
            "WeaponNumber",
            "무기 " + (index + 1),
            slotObject.transform,
            19,
            outlineColor,
            TextAlignmentOptions.Left,
            new Vector2(0.39f, 0.70f),
            new Vector2(0.95f, 0.92f)
        );


        // -------------------------------------------------
        // 무기 이름
        // -------------------------------------------------

        CreateText(
            "WeaponName",
            GetArrayText(weaponNames, index, "무기 정보 없음"),
            slotObject.transform,
            23,
            normalTextColor,
            TextAlignmentOptions.Left,
            new Vector2(0.39f, 0.39f),
            new Vector2(0.95f, 0.71f)
        );


        // -------------------------------------------------
        // 탄창 및 예비 탄약
        // 두 줄로 표시해 좁은 공간에서 넘치지 않도록 함
        // -------------------------------------------------

        CreateText(
            "WeaponAmmo",
            GetArrayText(
                weaponAmmoInfo,
                index,
                "탄창: --\n예비 탄약: --"
            ),
            slotObject.transform,
            16,
            outlineColor,
            TextAlignmentOptions.Left,
            new Vector2(0.39f, 0.08f),
            new Vector2(0.95f, 0.40f)
        );
    }



    // =====================================================
    // 12. 설정 영역
    // =====================================================

    private void CreateSettingsSection(RectTransform root)
    {
        RectTransform section = CreateSection(
            "SettingsSection",
            root,
            new Vector2(0.635f, 0.08f),
            new Vector2(0.925f, 0.76f)
        );


        // -------------------------------------------------
        // 설정 제목
        // -------------------------------------------------

        CreateText(
            "SettingsTitle",
            "설정",
            section,
            32,
            outlineColor,
            TextAlignmentOptions.Center,
            new Vector2(0.05f, 0.87f),
            new Vector2(0.95f, 0.98f)
        );


        // -------------------------------------------------
        // 음량 설정 제목
        // -------------------------------------------------

        CreateText(
    "VolumeTitle",
    "음량 설정",
    section,
    21,
    outlineColor,
    TextAlignmentOptions.Left,
    new Vector2(0.08f, 0.82f),
    new Vector2(0.92f, 0.91f)
);


        // -------------------------------------------------
        // 음량 슬라이더 3개
        // 글자, 수치, 슬라이더를 함께 위로 이동
        // -------------------------------------------------

        // 전체 음량
        CreateVolumeRow(
            section,
            "전체 음량",
            0.73f, 0.81f,
            0.69f, 0.73f,
            onMasterVolumeChanged
        );

        // 배경 음악
        CreateVolumeRow(
            section,
            "배경 음악",
            0.61f, 0.69f,
            0.57f, 0.61f,
            onBgmVolumeChanged
        );

        // 효과음
        CreateVolumeRow(
            section,
            "효과음",
            0.49f, 0.57f,
            0.45f, 0.49f,
            onSfxVolumeChanged
        );

        // -------------------------------------------------
        // 조작 키 제목
        // -------------------------------------------------

        CreateText(
            "ControlTitle",
            "조작 키",
            section,
            21,
            outlineColor,
            TextAlignmentOptions.Left,
            new Vector2(0.08f, 0.32f),
            new Vector2(0.92f, 0.37f)
        );


        // -------------------------------------------------
        // 조작 키 목록
        // 두 개의 세로 열로 표시
        // -------------------------------------------------

        CreateControlList(section);
    }


    // =====================================================
    // 13. 음량 슬라이더 생성
    // =====================================================

    /// <summary>
    /// 음량 이름, 수치, 슬라이더를 생성한다.
    ///
    /// 실제 게임 음량은 UnityEvent를 통해
    /// 추후 AudioManager와 연결한다.
    /// </summary>
    private void CreateVolumeRow(
        RectTransform parent,
        string label,
        float labelBottom,
        float labelTop,
        float sliderBottom,
        float sliderTop,
        UnityEvent<float> volumeEvent)
    {
        // 음량 이름
        CreateText(
            label + "_Label",
            label,
            parent,
            17,
            normalTextColor,
            TextAlignmentOptions.Left,
            new Vector2(0.08f, labelBottom),
            new Vector2(0.65f, labelTop)
        );


        // 현재 음량 수치
        TextMeshProUGUI valueText = CreateText(
            label + "_Value",
            "70%",
            parent,
            16,
            outlineColor,
            TextAlignmentOptions.Right,
            new Vector2(0.70f, labelBottom),
            new Vector2(0.92f, labelTop)
        );


        // 슬라이더 오브젝트
        GameObject sliderObject = new GameObject(
            label + "_Slider",
            typeof(RectTransform),
            typeof(Slider)
        );

        sliderObject.transform.SetParent(parent, false);

        RectTransform sliderRect =
            sliderObject.GetComponent<RectTransform>();

        // 전달받은 위치에 슬라이더 배치
        SetRect(
            sliderRect,
            new Vector2(0.08f, sliderBottom),
            new Vector2(0.92f, sliderTop)
        );


        // -------------------------------------------------
        // 슬라이더 배경
        // -------------------------------------------------

        CreateImage(
            "Background",
            sliderObject.transform,
            inactiveColor,
            Vector2.zero,
            Vector2.one
        );


        // -------------------------------------------------
        // 채움 영역
        // -------------------------------------------------

        RectTransform fillArea = CreateRect(
            "FillArea",
            sliderObject.transform,
            Vector2.zero,
            Vector2.one
        );

        fillArea.offsetMin = new Vector2(5f, 2f);
        fillArea.offsetMax = new Vector2(-5f, -2f);

        Image fillImage = CreateImage(
            "Fill",
            fillArea,
            outlineColor,
            Vector2.zero,
            Vector2.one
        );


        // -------------------------------------------------
        // 슬라이더 손잡이 영역
        // -------------------------------------------------

        RectTransform handleArea = CreateRect(
            "HandleSlideArea",
            sliderObject.transform,
            Vector2.zero,
            Vector2.one
        );

        handleArea.offsetMin = new Vector2(5f, 0f);
        handleArea.offsetMax = new Vector2(-5f, 0f);

        Image handleImage = CreateImage(
            "Handle",
            handleArea,
            normalTextColor,
            Vector2.zero,
            Vector2.one
        );

        RectTransform handleRect =
            handleImage.GetComponent<RectTransform>();

        // 손잡이 크기
        handleRect.sizeDelta = new Vector2(14f, 0f);


        // -------------------------------------------------
        // Slider 컴포넌트 연결
        // -------------------------------------------------

        Slider slider = sliderObject.GetComponent<Slider>();

        slider.fillRect = fillImage.rectTransform;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;

        slider.minValue = 0f;
        slider.maxValue = 100f;
        slider.value = 70f;

        slider.direction = Slider.Direction.LeftToRight;


        // 음량 변경 시 수치와 UnityEvent 갱신
        slider.onValueChanged.AddListener(value =>
        {
            valueText.text = Mathf.RoundToInt(value) + "%";

            if (volumeEvent != null)
                volumeEvent.Invoke(value / 100f);
        });
    }


    // =====================================================
    // 14. 조작 키 목록
    // =====================================================

    /// <summary>
    /// 조작 키를 두 개의 세로 열로 나누어 표시한다.
    ///
    /// 각 항목은 기능 이름과 키를 두 줄로 표시한다.
    ///
    /// 왼쪽 열:
    /// 이동 / 조준 / 사격
    ///
    /// 오른쪽 열:
    /// 재장전 / 상호작용 / 일시 정지
    /// </summary>
    private void CreateControlList(RectTransform parent)
    {
        int count = Mathf.Min(
            controlActions != null ? controlActions.Length : 0,
            controlKeys != null ? controlKeys.Length : 0
        );

        int rowsPerColumn = 3;

        for (int i = 0; i < count; i++)
        {
            // 왼쪽 3개, 오른쪽 3개로 나눈다.
            int column = i / rowsPerColumn;
            int row = i % rowsPerColumn;

            float columnMin = column == 0 ? 0.08f : 0.53f;
            float columnMax = column == 0 ? 0.48f : 0.92f;

            // 각 항목의 세로 위치
            float top = 0.29f - (row * 0.085f);
            float bottom = top - 0.065f;

            // 기능 이름
            CreateText(
                "ControlAction_" + i,
                controlActions[i],
                parent,
                17,
                normalTextColor,
                TextAlignmentOptions.Left,
                new Vector2(columnMin, bottom + 0.032f),
                new Vector2(columnMax, top)
            );

            // 해당 기능의 키
            CreateText(
                "ControlKey_" + i,
                controlKeys[i],
                parent,
                16,
                outlineColor,
                TextAlignmentOptions.Left,
                new Vector2(columnMin, bottom),
                new Vector2(columnMax, bottom + 0.034f)
            );
        }
    }

    // =====================================================
    // 15. 공통 섹션 패널 생성
    // =====================================================

    private RectTransform CreateSection(
        string objectName,
        RectTransform parent,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        GameObject obj = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(Image)
        );

        obj.transform.SetParent(parent, false);

        RectTransform rect =
            obj.GetComponent<RectTransform>();

        SetRect(rect, anchorMin, anchorMax);

        Image image = obj.GetComponent<Image>();
        image.color = panelColor;

        AddOutline(obj, outlineColor);

        return rect;
    }


    // =====================================================
    // 16. 메뉴 버튼 생성
    // =====================================================

    private void CreateMenuButton(
        string objectName,
        string label,
        RectTransform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(Image),
            typeof(Button)
        );

        buttonObject.transform.SetParent(parent, false);

        RectTransform rect =
            buttonObject.GetComponent<RectTransform>();

        SetRect(rect, anchorMin, anchorMax);

        Image image =
            buttonObject.GetComponent<Image>();

        image.color = inactiveColor;

        Outline outline =
            AddOutline(buttonObject, inactiveColor);

        Button button =
            buttonObject.GetComponent<Button>();

        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(action);

        AddHoverEffect(
            buttonObject,
            image,
            outline
        );

        // 버튼 글씨 크기 확대
        CreateText(
            "Label",
            label,
            buttonObject.transform,
            29,
            normalTextColor,
            TextAlignmentOptions.Center,
            Vector2.zero,
            Vector2.one
        );
    }


    // =====================================================
    // 17. 테두리 생성
    // =====================================================

    private Outline AddOutline(
        GameObject target,
        Color initialColor)
    {
        Outline outline =
            target.AddComponent<Outline>();

        outline.effectColor = initialColor;
        outline.effectDistance = new Vector2(3f, 3f);
        outline.useGraphicAlpha = false;

        return outline;
    }


    // =====================================================
    // 18. 마우스 오버 효과
    // =====================================================

    private void AddHoverEffect(
        GameObject target,
        Image targetImage,
        Outline targetOutline)
    {
        EventTrigger trigger =
            target.AddComponent<EventTrigger>();


        // 마우스 진입
        EventTrigger.Entry enter =
            new EventTrigger.Entry();

        enter.eventID = EventTriggerType.PointerEnter;

        enter.callback.AddListener(data =>
        {
            targetImage.color = hoverColor;
            targetOutline.effectColor = outlineColor;

            PlayScaleEffect(target.transform, 1.045f);
            PlaySound(hoverSound);
        });

        trigger.triggers.Add(enter);


        // 마우스 이탈
        EventTrigger.Entry exit =
            new EventTrigger.Entry();

        exit.eventID = EventTriggerType.PointerExit;

        exit.callback.AddListener(data =>
        {
            targetImage.color = inactiveColor;
            targetOutline.effectColor = inactiveColor;

            PlayScaleEffect(target.transform, 1f);
        });

        trigger.triggers.Add(exit);
    }


    // =====================================================
    // 19. 버튼 확대 애니메이션
    // =====================================================

    private void PlayScaleEffect(
        Transform target,
        float targetScale)
    {
        if (target == null)
            return;

        if (scaleCoroutines.TryGetValue(
            target,
            out Coroutine runningCoroutine))
        {
            if (runningCoroutine != null)
                StopCoroutine(runningCoroutine);

            scaleCoroutines.Remove(target);
        }

        Coroutine newCoroutine =
            StartCoroutine(
                ScaleEffect(target, targetScale)
            );

        scaleCoroutines[target] = newCoroutine;
    }


    private IEnumerator ScaleEffect(
        Transform target,
        float targetScale)
    {
        Vector3 startScale = target.localScale;
        Vector3 endScale = Vector3.one * targetScale;

        float elapsed = 0f;
        float duration = 0.12f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            target.localScale = Vector3.Lerp(
                startScale,
                endScale,
                elapsed / duration
            );

            yield return null;
        }

        target.localScale = endScale;

        scaleCoroutines.Remove(target);
    }


    // =====================================================
    // 20. 작전 재개
    // =====================================================

    private void OnResumeClicked()
    {
        PlaySound(clickSound);

        if (onResumeRequested != null)
            onResumeRequested.Invoke();
    }


    // =====================================================
    // 21. 작전 재출격
    // =====================================================

    private void OnRedeployClicked()
    {
        PlaySound(clickSound);

        if (onRedeployRequested != null)
            onRedeployRequested.Invoke();
    }


    // =====================================================
    // 22. 작전 중지
    // =====================================================

    private void OnStopOperationClicked()
    {
        PlaySound(clickSound);

        if (onStopOperationRequested != null)
            onStopOperationRequested.Invoke();
    }


    // =====================================================
    // 23. 퇴근
    // =====================================================

    private void OnQuitClicked()
    {
        PlaySound(clickSound);

#if UNITY_EDITOR
        Debug.Log(
            "게임 종료 요청 - Unity 에디터에서는 종료되지 않음"
        );
#else
        Application.Quit();
#endif
    }


    // =====================================================
    // 24. 이미지 생성
    // =====================================================

    private Image CreateImage(
        string objectName,
        Transform parent,
        Color color,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        GameObject obj = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(Image)
        );

        obj.transform.SetParent(parent, false);

        RectTransform rect =
            obj.GetComponent<RectTransform>();

        SetRect(rect, anchorMin, anchorMax);

        Image image = obj.GetComponent<Image>();
        image.color = color;

        return image;
    }


    // =====================================================
    // 25. TMP 텍스트 생성
    // =====================================================

    private TextMeshProUGUI CreateText(
        string objectName,
        string content,
        Transform parent,
        int fontSize,
        Color color,
        TextAlignmentOptions alignment,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        GameObject obj = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(TextMeshProUGUI)
        );

        obj.transform.SetParent(parent, false);

        RectTransform rect =
            obj.GetComponent<RectTransform>();

        SetRect(rect, anchorMin, anchorMax);

        TextMeshProUGUI text =
            obj.GetComponent<TextMeshProUGUI>();

        text.text = content;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;

        // 공간이 부족할 때 자동으로 글자 크기를 조절
        text.enableAutoSizing = true;
        text.fontSizeMin = 12;
        text.fontSizeMax = fontSize;

        // Inspector에서 지정한 한글 폰트 적용
        if (hudFont != null)
            text.font = hudFont;

        // 텍스트가 마우스 클릭을 가로채지 않도록 설정
        text.raycastTarget = false;

        // 영역을 벗어난 텍스트는 잘라서 표시
        text.overflowMode = TextOverflowModes.Truncate;

        return text;
    }


    // =====================================================
    // 26. RectTransform 생성
    // =====================================================

    private RectTransform CreateRect(
        string objectName,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        GameObject obj = new GameObject(
            objectName,
            typeof(RectTransform)
        );

        obj.transform.SetParent(parent, false);

        RectTransform rect =
            obj.GetComponent<RectTransform>();

        SetRect(rect, anchorMin, anchorMax);

        return rect;
    }


    // =====================================================
    // 27. 배열 데이터 확인
    // =====================================================

    private string GetArrayText(
        string[] source,
        int index,
        string fallback)
    {
        if (source != null &&
            index >= 0 &&
            index < source.Length &&
            !string.IsNullOrWhiteSpace(source[index]))
        {
            return source[index];
        }

        return fallback;
    }


    // =====================================================
    // 28. RectTransform 위치 설정
    // =====================================================

    private void SetRect(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;

        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }


    // =====================================================
    // 29. 부모 영역 전체 채우기
    // =====================================================

    private void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;

        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
