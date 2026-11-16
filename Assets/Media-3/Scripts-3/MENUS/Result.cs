
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;

/// <summary>
/// 작전 결과 UI 전체를 코드로 생성하고 관리하는 스크립트.
///
/// [화면 구성]
/// 1. 좌측 : 작전 결과 및 기본 정보
/// 2. 중앙 : 주요 임무 결과 및 작전 통계
/// 3. 우측 : 작전 소요 시간, 최고 기록, 메뉴 버튼
/// 4. 우측 최상단 : 퇴근 버튼
///
/// [주요 기능]
/// - 작전 완수 / 작전 실패 표시
/// - 임무별 메인 목표 표시
/// - 처치 수 / 발각 횟수 / 자료 확보 표시
/// - 체력 및 방어구 표시
/// - 작전 소요 시간 및 최고 기록 표시
/// - 작전 재출격 / 철수
/// - 버튼 마우스 오버 효과 및 효과음
///
/// [주의]
/// 현재는 독립 테스트용 UI다.
/// 실제 게임 데이터 및 씬 이동은 추후 기존 시스템과 연결한다.
/// </summary>
public class ResultUI : MonoBehaviour
{
    // =====================================================
    // 1. Inspector 설정
    // =====================================================

    [Header("폰트 설정")]
    [Tooltip("한글을 지원하는 TextMeshPro 폰트")]
    [SerializeField] private TMP_FontAsset hudFont;


    [Header("작전 결과 정보")]
    [SerializeField] private string mapName = "작전 지역";

    // MissionManager에서 전달받는 실제 미션 종류
    // clear / steal / steal_exit / bomb
    [SerializeField] private string missionType = "clear";

    [SerializeField] private string difficulty = "보통";

    [SerializeField] private bool missionSuccess = true;


    [Header("작전 통계")]
    [SerializeField] private int kills = 8;
    [SerializeField] private int totalEnemies = 12;

    [SerializeField] private int detections = 2;
    [SerializeField] private bool stolen = true;

    [SerializeField] private int remainingHp = 75;
    [SerializeField] private int remainingArmor = 40;


    [Header("작전 기록")]
    [SerializeField] private string operationTime = "05:32";
    [SerializeField] private string bestRecord = "04:58";

    [SerializeField] private bool isNewRecord = false;


    [Header("효과음")]
    [SerializeField] private AudioClip hoverSound;
    [SerializeField] private AudioClip clickSound;

    [Range(0f, 1f)]
    [SerializeField] private float soundVolume = 0.7f;


    [Header("기능 연결")]
    [SerializeField] private UnityEvent onRedeployRequested = new UnityEvent();
    [SerializeField] private UnityEvent onWithdrawRequested = new UnityEvent();


    // =====================================================
    // 2. 색상 설정
    // =====================================================

    private readonly Color panelColor =
        new Color32(23, 29, 36, 245);

    // 성공 시 사용하는 기존 밝은 연두색
    private readonly Color outlineColor =
        new Color32(164, 214, 164, 255);

    private readonly Color inactiveColor =
        new Color32(65, 77, 88, 255);

    private readonly Color hoverColor =
        new Color32(38, 52, 62, 255);

    private readonly Color normalTextColor =
        new Color32(235, 239, 235, 255);

    // 실패 시 사용하는 기존 빨간색
    private readonly Color failureColor =
        new Color32(198, 76, 82, 255);


    // =====================================================
    // 3. 내부 UI 참조
    // =====================================================

    private Canvas resultCanvas;
    private GameObject resultRoot;

    private AudioSource audioSource;

    private TextMeshProUGUI resultTitleText;
    private TextMeshProUGUI objectiveResultText;

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

        BuildResultUI();

        // 테스트를 위해 결과 화면을 바로 표시한다.
        // 실제 게임 연결 후에는 결과 발생 시 SetOpen(true)를 호출한다.
        SetOpen(true);
    }


    // =====================================================
    // 5. UI 열기 / 닫기
    // =====================================================

    public void SetOpen(bool open)
    {
        if (resultRoot == null)
            return;

        resultRoot.SetActive(open);
    }


    // =====================================================
    // 6. 결과 데이터 설정
    // =====================================================

    public void SetResultData(
        string newMapName,
        string newMissionType,
        string newDifficulty,
        bool success,
        int newKills,
        int newTotalEnemies,
        int newDetections,
        bool newStolen,
        int newHp,
        int newArmor,
        string newOperationTime,
        string newBestRecord,
        bool newRecord)
    {
        mapName = newMapName;
        missionType = newMissionType;
        difficulty = newDifficulty;

        missionSuccess = success;

        kills = newKills;
        totalEnemies = newTotalEnemies;
        detections = newDetections;
        stolen = newStolen;

        remainingHp = newHp;
        remainingArmor = newArmor;

        operationTime = newOperationTime;
        bestRecord = newBestRecord;

        isNewRecord = newRecord;

        RefreshResultUI();
    }


    public void RefreshResultUI()
    {
        if (resultRoot == null)
            return;

        UpdateResultTitle();
        UpdateOperationInfo();
        UpdateStatistics();
        UpdateRecordInfo();

        // 전체 UI의 성공 / 실패 색상 적용
        UpdateResultTheme();

        // 주요 임무 결과는 실제 달성 여부에 맞춰 별도 색상 적용
        UpdateObjectiveResult();
    }


    // =====================================================
    // 7. AudioSource 생성
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
    // 8. Canvas 생성
    // =====================================================

    private void CreateCanvas()
    {
        GameObject canvasObject = new GameObject(
            "ResultCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster)
        );

        canvasObject.transform.SetParent(transform, false);

        resultCanvas = canvasObject.GetComponent<Canvas>();

        resultCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        resultCanvas.overrideSorting = true;
        resultCanvas.sortingOrder = 100;

        CanvasScaler scaler =
            canvasObject.GetComponent<CanvasScaler>();

        scaler.uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;

        scaler.referenceResolution =
            new Vector2(1920, 1080);

        scaler.matchWidthOrHeight = 0.5f;
    }


    // =====================================================
    // 9. EventSystem 확인
    // =====================================================

    private void CreateEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
            return;

        new GameObject(
            "ResultEventSystem",
            typeof(EventSystem),
            typeof(StandaloneInputModule)
        );
    }


    // =====================================================
    // 10. 결과 화면 전체 구성
    // =====================================================

    private void BuildResultUI()
    {
        resultRoot = new GameObject(
            "ResultRoot",
            typeof(RectTransform)
        );

        resultRoot.transform.SetParent(
            resultCanvas.transform,
            false
        );

        RectTransform root =
            resultRoot.GetComponent<RectTransform>();

        Stretch(root);


        // 게임 화면 어둡게 처리
        Image dimmer = CreateImage(
            "BackgroundDimmer",
            root,
            new Color(0f, 0f, 0f, 0.78f),
            Vector2.zero,
            Vector2.one
        );

        dimmer.raycastTarget = true;


        // 상단 제목
        CreateText(
            "ResultSubtitle",
            "작전 종료 보고",
            root,
            48,
            outlineColor,
            TextAlignmentOptions.Center,
            new Vector2(0.25f, 0.88f),
            new Vector2(0.75f, 0.95f)
        );


        // 퇴근 버튼
        CreateMenuButton(
            "QuitButton",
            "퇴근",
            root,
            new Vector2(0.84f, 0.89f),
            new Vector2(0.95f, 0.96f),
            OnQuitClicked
        );


        // 상단 구분선
        CreateImage(
            "TitleLine",
            root,
            outlineColor,
            new Vector2(0.05f, 0.82f),
            new Vector2(0.95f, 0.825f)
        );


        // 좌측 작전 결과 영역
        CreateOperationResultSection(root);


        // 중앙 임무 결과 영역
        CreateMissionSection(root);


        // 우측 기록 및 메뉴 영역
        CreateRecordAndMenuSection(root);


        RefreshResultUI();
    }


    // =====================================================
    // 11. 좌측 작전 결과 영역
    // =====================================================

    private void CreateOperationResultSection(RectTransform root)
    {
        RectTransform section = CreateSection(
            "OperationResultSection",
            root,
            new Vector2(0.045f, 0.09f),
            new Vector2(0.30f, 0.77f)
        );


        CreateText(
            "OperationSectionTitle",
            "작전 결과",
            section,
            36,
            outlineColor,
            TextAlignmentOptions.Center,
            new Vector2(0.05f, 0.87f),
            new Vector2(0.95f, 0.98f)
        );


        resultTitleText = CreateText(
            "OperationResultTitle",
            "작전 완수",
            section,
            44,
            outlineColor,
            TextAlignmentOptions.Center,
            new Vector2(0.05f, 0.68f),
            new Vector2(0.95f, 0.84f)
        );


        CreateText(
            "OperationInfoTitle",
            "작전 정보",
            section,
            29,
            outlineColor,
            TextAlignmentOptions.Left,
            new Vector2(0.09f, 0.53f),
            new Vector2(0.91f, 0.61f)
        );


        CreateInfoRow(section, "지역", mapName, 0.43f);
        CreateInfoRow(section, "임무", missionType, 0.32f);
        CreateInfoRow(section, "난이도", difficulty, 0.21f);
    }


    // =====================================================
    // 12. 중앙 임무 결과 영역
    // =====================================================

    private void CreateMissionSection(RectTransform root)
    {
        RectTransform section = CreateSection(
            "MissionSection",
            root,
            new Vector2(0.32f, 0.09f),
            new Vector2(0.68f, 0.77f)
        );


        CreateText(
            "MissionSectionTitle",
            "임무 보고",
            section,
            40,
            outlineColor,
            TextAlignmentOptions.Center,
            new Vector2(0.05f, 0.87f),
            new Vector2(0.95f, 0.98f)
        );


        CreateText(
            "MainObjectiveTitle",
            "주요 임무",
            section,
            30,
            outlineColor,
            TextAlignmentOptions.Left,
            new Vector2(0.08f, 0.72f),
            new Vector2(0.92f, 0.81f)
        );


        CreateImage(
            "MainObjectiveBackground",
            section,
            inactiveColor,
            new Vector2(0.06f, 0.53f),
            new Vector2(0.94f, 0.70f)
        );


        CreateText(
            "MainObjectiveName",
            "적 소탕",
            section,
            27,
            normalTextColor,
            TextAlignmentOptions.Center,
            new Vector2(0.09f, 0.60f),
            new Vector2(0.91f, 0.68f)
        );


        objectiveResultText = CreateText(
            "MainObjectiveResult",
            "임무 달성",
            section,
            25,
            outlineColor,
            TextAlignmentOptions.Center,
            new Vector2(0.09f, 0.54f),
            new Vector2(0.91f, 0.60f)
        );


        CreateText(
            "StatisticsTitle",
            "작전 통계",
            section,
            29,
            outlineColor,
            TextAlignmentOptions.Left,
            new Vector2(0.08f, 0.42f),
            new Vector2(0.92f, 0.49f)
        );


        CreateStatRow(
            section,
            "처치한 적",
            kills + " / " + totalEnemies,
            0.33f,
            "KillsValue"
        );


        CreateStatRow(
            section,
            "발각 횟수",
            detections + "회",
            0.24f,
            "DetectionValue"
        );


        CreateStatRow(
            section,
            "자료 확보",
            stolen ? "확보 완료" : "미확보",
            0.15f,
            "StolenValue"
        );


        CreateStatRow(
            section,
            "잔여 체력 / 방어구",
            remainingHp + " / " + remainingArmor,
            0.06f,
            "HealthValue"
        );
    }


    // =====================================================
    // 13. 우측 기록 및 메뉴 영역
    // =====================================================

    private void CreateRecordAndMenuSection(RectTransform root)
    {
        RectTransform section = CreateSection(
            "RecordAndMenuSection",
            root,
            new Vector2(0.70f, 0.09f),
            new Vector2(0.955f, 0.77f)
        );


        CreateText(
            "OperationTimeTitle",
            "작전 소요 시간",
            section,
            27,
            outlineColor,
            TextAlignmentOptions.Center,
            new Vector2(0.05f, 0.86f),
            new Vector2(0.95f, 0.96f)
        );


        CreateText(
            "OperationTimeValue",
            operationTime,
            section,
            42,
            normalTextColor,
            TextAlignmentOptions.Center,
            new Vector2(0.05f, 0.76f),
            new Vector2(0.95f, 0.86f)
        );


        CreateText(
            "BestRecordTitle",
            "최고 기록",
            section,
            27,
            outlineColor,
            TextAlignmentOptions.Center,
            new Vector2(0.05f, 0.64f),
            new Vector2(0.95f, 0.73f)
        );


        CreateText(
            "BestRecordValue",
            bestRecord,
            section,
            37,
            normalTextColor,
            TextAlignmentOptions.Center,
            new Vector2(0.05f, 0.54f),
            new Vector2(0.95f, 0.64f)
        );


        CreateText(
            "NewRecordText",
            "신기록 달성!",
            section,
            22,
            outlineColor,
            TextAlignmentOptions.Center,
            new Vector2(0.05f, 0.47f),
            new Vector2(0.95f, 0.54f)
        );


        // 작전 재출격 버튼
        CreateMenuButton(
            "RedeployButton",
            "작전 재출격",
            section,
            new Vector2(0.08f, 0.25f),
            new Vector2(0.92f, 0.39f),
            OnRedeployClicked
        );


        // 철수 버튼
        CreateMenuButton(
            "WithdrawButton",
            "철수",
            section,
            new Vector2(0.08f, 0.07f),
            new Vector2(0.92f, 0.21f),
            OnWithdrawClicked
        );
    }


    // =====================================================
    // 14. 작전 정보 행 생성
    // =====================================================

    private void CreateInfoRow(
        RectTransform parent,
        string label,
        string value,
        float y)
    {
        CreateText(
            label + "_Label",
            label,
            parent,
            28,
            outlineColor,
            TextAlignmentOptions.Left,
            new Vector2(0.09f, y),
            new Vector2(0.39f, y + 0.07f)
        );


        CreateText(
            label + "_Value",
            value,
            parent,
            28,
            normalTextColor,
            TextAlignmentOptions.Right,
            new Vector2(0.40f, y),
            new Vector2(0.91f, y + 0.07f)
        );
    }


    // =====================================================
    // 15. 작전 통계 행 생성
    // =====================================================

    private void CreateStatRow(
        RectTransform parent,
        string label,
        string value,
        float y,
        string objectName)
    {
        CreateText(
            objectName + "_Label",
            label,
            parent,
            23,
            normalTextColor,
            TextAlignmentOptions.Left,
            new Vector2(0.09f, y),
            new Vector2(0.55f, y + 0.07f)
        );


        CreateText(
            objectName,
            value,
            parent,
            23,
            outlineColor,
            TextAlignmentOptions.Right,
            new Vector2(0.55f, y),
            new Vector2(0.91f, y + 0.07f)
        );
    }


    // =====================================================
    // 16. 결과 제목 갱신
    // =====================================================

    private void UpdateResultTitle()
    {
        if (resultTitleText == null)
            return;

        if (missionSuccess)
        {
            resultTitleText.text = "작전 완수";
            resultTitleText.color = outlineColor;
        }
        else
        {
            resultTitleText.text = "작전 실패";
            resultTitleText.color = failureColor;
        }
    }


    // =====================================================
    // 17. 작전 기본 정보 갱신
    // =====================================================

    private void UpdateOperationInfo()
    {
        SetText("지역_Value", mapName);
        SetText("임무_Value", GetMissionDisplayName());
        SetText("난이도_Value", difficulty);
    }


    // =====================================================
    // 18. 메인 임무 결과 갱신
    // =====================================================

    // 18. 주요 임무 결과 갱신
    private void UpdateObjectiveResult()
    {
        if (objectiveResultText == null)
            return;

        string objectiveName = "";
        string resultText = "";
        bool completed = false;

        switch (missionType)
        {
            case "steal":

                objectiveName = "자료 확보";
                completed = stolen;
                resultText = completed ? "자료 확보 완료" : "자료 확보 실패";

                break;

            case "steal_exit":

                objectiveName = "자료 확보 후 탈출";
                completed = stolen && missionSuccess;

                if (completed)
                    resultText = "자료 확보 및 탈출 완료";
                else if (stolen)
                    resultText = "자료 확보 완료 · 탈출 실패";
                else
                    resultText = "자료 미확보 · 작전 실패";

                break;

            case "bomb":

                objectiveName = "폭탄 해체";
                completed = missionSuccess;
                resultText = completed ? "폭탄 해체 완료" : "폭탄 해체 실패";

                break;

            default:

                objectiveName = "적 소탕";
                completed = missionSuccess;
                resultText = completed ? "적 소탕 완료" : "적 소탕 실패";

                break;
        }

        // 주요 임무 이름은 항상 흰색
        SetText("MainObjectiveName", objectiveName);
        SetTextColor("MainObjectiveName", normalTextColor);

        // 실제 임무 결과 문구 갱신
        objectiveResultText.text = resultText;

        // 임무 달성 여부에 따라 결과 문구 색상 변경
        objectiveResultText.color =
            completed ? outlineColor : failureColor;
    }

    private string GetMissionDisplayName()
    {
        switch (missionType)
        {
            case "steal":
                return "자료 확보";

            case "steal_exit":
                return "자료 확보 후 탈출";

            case "bomb":
                return "폭탄 해체";

            default:
                return "적 소탕";
        }
    }

    // =====================================================
    // 19. 작전 통계 갱신
    // =====================================================

    private void UpdateStatistics()
    {
        SetText("KillsValue", kills + " / " + totalEnemies);
        SetText("DetectionValue", detections + "회");
        SetText("StolenValue", stolen ? "확보 완료" : "미확보");
        SetText("HealthValue", remainingHp + " / " + remainingArmor);
    }


    // =====================================================
    // 20. 기록 정보 갱신
    // =====================================================

    private void UpdateRecordInfo()
    {
        SetText("OperationTimeValue", operationTime);
        SetText("BestRecordValue", bestRecord);

        SetText(
            "NewRecordText",
            isNewRecord ? "신기록 달성!" : ""
        );
    }


    // =====================================================
    // 21. 텍스트 갱신 도우미
    // =====================================================

    /// <summary>
    /// ResultRoot 아래의 모든 하위 오브젝트에서
    /// 이름이 일치하는 TMP 텍스트를 찾아 내용을 변경한다.
    /// </summary>
    private void SetText(
        string objectName,
        string content)
    {
        if (resultRoot == null)
            return;

        TextMeshProUGUI[] allTexts =
            resultRoot.GetComponentsInChildren<TextMeshProUGUI>(true);

        foreach (TextMeshProUGUI text in allTexts)
        {
            if (text.gameObject.name == objectName)
            {
                text.text = content;
                return;
            }
        }
    }


    /// <summary>
    /// 이름이 일치하는 TMP 텍스트의 색상을 변경한다.
    /// </summary>
    private void SetTextColor(
        string objectName,
        Color color)
    {
        if (resultRoot == null)
            return;

        TextMeshProUGUI[] allTexts =
            resultRoot.GetComponentsInChildren<TextMeshProUGUI>(true);

        foreach (TextMeshProUGUI text in allTexts)
        {
            if (text.gameObject.name == objectName)
            {
                text.color = color;
                return;
            }
        }
    }


    /// <summary>
    /// 현재 작전 성공 / 실패에 맞는 강조 색상을 반환한다.
    /// </summary>
    private Color GetResultColor()
    {
        return missionSuccess ? outlineColor : failureColor;
    }


    /// <summary>
    /// 성공 / 실패에 따라 전체 결과 UI의 강조 색상을 변경한다.
    /// </summary>

    private void UpdateResultTheme()
    {
        Color resultColor = GetResultColor();

        // 성공 / 실패에 따라 전체 제목 색상 변경
        TextMeshProUGUI[] allTexts =
            resultRoot.GetComponentsInChildren<TextMeshProUGUI>(true);

        foreach (TextMeshProUGUI text in allTexts)
        {
            if (text == objectiveResultText)
                continue;

            if (text.color == normalTextColor)
                continue;

            text.color = resultColor;
        }

        // 왼쪽 작전 정보 항목은 항상 흰색
        SetTextColor("지역_Label", normalTextColor);
        SetTextColor("임무_Label", normalTextColor);
        SetTextColor("난이도_Label", normalTextColor);

        SetTextColor("지역_Value", resultColor);
        SetTextColor("임무_Value", resultColor);
        SetTextColor("난이도_Value", resultColor);

        // 패널 테두리 색상 변경
        Outline[] allOutlines =
            resultRoot.GetComponentsInChildren<Outline>(true);

        foreach (Outline outline in allOutlines)
        {
            if (outline.gameObject.name == "QuitButton" ||
                outline.gameObject.name == "RedeployButton" ||
                outline.gameObject.name == "WithdrawButton")
                continue;

            outline.effectColor = resultColor;
        }

        // 상단 구분선 색상 변경
        Transform line = resultRoot.transform.Find("TitleLine");

        if (line != null)
        {
            Image lineImage = line.GetComponent<Image>();

            if (lineImage != null)
                lineImage.color = resultColor;
        }
    }



    // =====================================================
    // 22. 메뉴 버튼 생성
    // =====================================================

    private Button CreateMenuButton(
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


        CreateText(
            "Label",
            label,
            buttonObject.transform,
            27,
            normalTextColor,
            TextAlignmentOptions.Center,
            Vector2.zero,
            Vector2.one
        );

        return button;
    }


    // =====================================================
    // 23. 테두리 생성
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
    // 24. 마우스 오버 효과
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
            if (!target.GetComponent<Button>().interactable)
                return;

            targetImage.color = hoverColor;

            // 성공 / 실패에 맞는 테두리 색상
            targetOutline.effectColor = GetResultColor();

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
    // 25. 버튼 확대 애니메이션
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
    // 26. 작전 재출격
    // =====================================================

    private void OnRedeployClicked()
    {
        PlaySound(clickSound);

        if (onRedeployRequested != null)
            onRedeployRequested.Invoke();
    }


    // =====================================================
    // 27. 철수
    // =====================================================

    private void OnWithdrawClicked()
    {
        PlaySound(clickSound);

        if (onWithdrawRequested != null)
            onWithdrawRequested.Invoke();
    }


    // =====================================================
    // 28. 퇴근
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
    // 29. 이미지 생성
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
    // 30. 공통 섹션 패널 생성
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
    // 31. TMP 텍스트 생성
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

        // 지정한 글자 크기를 그대로 사용한다.
        text.enableAutoSizing = false;

        // Inspector에서 지정한 한글 폰트 적용
        if (hudFont != null)
            text.font = hudFont;

        // 마우스 클릭을 가로채지 않도록 설정
        text.raycastTarget = false;

        // 영역을 벗어난 텍스트는 잘라서 표시
        text.overflowMode = TextOverflowModes.Truncate;

        return text;
    }


    // =====================================================
    // 32. RectTransform 위치 설정
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
    // 33. 부모 영역 전체 채우기
    // =====================================================

    private void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;

        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
