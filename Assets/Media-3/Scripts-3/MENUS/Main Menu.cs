
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 메인 메뉴 UI 전체를 생성하고 관리하는 스크립트
///
/// [담당 기능]
/// 1. 상단 중앙 게임 타이틀 표시
/// 2. 왼쪽 전투 / 재정비 / 퇴근 메뉴 생성
/// 3. 전투 버튼 클릭 시 오른쪽에 맵 4개 표시
/// 4. 마우스 오버 시 버튼 및 맵 카드 색상과 크기 변경
/// 5. 버튼과 맵 카드에 테두리 강조 효과 적용
/// 6. 버튼 및 맵 선택 효과음 재생
/// 7. 맵 선택 시 Inspector에 입력한 씬으로 이동
///
/// [구현 방식]
/// Canvas와 모든 UI 오브젝트를 코드로 생성한다.
///
/// [주의]
/// 기존 팀장 MVP의 전투 및 게임 로직은 수정하지 않는다.
/// 이 스크립트는 메인 메뉴 UI만 담당한다.
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    // =====================================================
    // 1. Inspector 설정
    // =====================================================

    [Header("폰트 설정")]

    [Tooltip("메인 메뉴 전체에 사용할 TextMeshPro 폰트")]
    [SerializeField] private TMP_FontAsset hudFont;


    [Header("게임 타이틀")]

    [Tooltip("화면 상단 중앙에 표시할 게임 이름")]
    [SerializeField] private string gameTitle = "GAME TITLE";


    [Header("맵 이미지")]

    [Tooltip("맵 1~4의 미리보기 이미지. 맵 제작 후 연결")]
    [SerializeField] private Sprite[] mapImages = new Sprite[4];


    [Header("맵 이름")]

    [Tooltip("맵 카드에 표시할 이름. Inspector에서 변경 가능")]
    [SerializeField]
    private string[] mapNames =
    {
        "맵 01 - 적 소탕",
        "맵 02 - 자료 탈취",
        "맵 03 - 탈취 후 탈출",
        "맵 04 - 최종 소탕"
    };


    [Header("맵 씬 이름")]

    [Tooltip("맵 카드 클릭 시 이동할 실제 Unity 씬 이름")]
    [SerializeField] private string[] mapSceneNames = new string[4];


    [Header("효과음")]

    [Tooltip("마우스를 버튼이나 맵 카드에 올렸을 때 재생")]
    [SerializeField] private AudioClip hoverSound;

    [Tooltip("전투, 재정비, 퇴근 버튼 클릭 시 재생")]
    [SerializeField] private AudioClip clickSound;

    [Tooltip("맵 카드를 선택했을 때 재생")]
    [SerializeField] private AudioClip mapSelectSound;

    [Tooltip("이전 메뉴로 돌아갈 때 사용할 효과음. 추후 기능 연결")]
    [SerializeField] private AudioClip backSound;


    [Header("효과음 크기")]

    [Range(0f, 1f)]
    [SerializeField] private float soundVolume = 0.7f;


    // =====================================================
    // 2. HUD와 통일한 색상 설정
    // =====================================================

    // 전체 메뉴 및 패널 배경
    // 기존 HealthHUD / MissionHUD와 동일한 색상
    private readonly Color panelColor =
        new Color32(23, 29, 36, 245);       // #171D24

    // 타이틀과 선택된 UI의 강조 테두리
    private readonly Color outlineColor =
        new Color32(164, 214, 164, 255);    // #A4D6A4

    // 기본 버튼 및 카드 배경
    private readonly Color inactiveColor =
        new Color32(65, 77, 88, 255);       // #414D58

    // 마우스 오버 시 사용하는 밝은 청회색
    // 연녹색 배경을 사용하지 않아 글자가 묻히는 문제 방지
    private readonly Color hoverColor =
        new Color32(38, 52, 62, 255);       // #26343E

    // 일반 메뉴 및 맵 이름 글자
    private readonly Color normalTextColor =
        new Color32(235, 239, 235, 255);


    // =====================================================
    // 3. 내부 UI 및 오디오 참조
    // =====================================================

    // 메인 메뉴 UI가 표시될 Canvas
    private Canvas mainCanvas;

    // 효과음 재생용 AudioSource
    private AudioSource audioSource;

    // 전투 버튼 클릭 시 표시할 맵 선택 영역
    private GameObject mapSection;

    // 각 UI의 확대 애니메이션을 개별 관리
    // 한 버튼의 애니메이션이 다른 버튼에 영향을 주지 않게 한다.
    private readonly Dictionary<Transform, Coroutine> scaleCoroutines =
        new Dictionary<Transform, Coroutine>();


    // =====================================================
    // 4. 초기 실행
    // =====================================================

    private void Start()
    {
        // 효과음 재생 환경 생성
        CreateAudioSource();

        // Canvas 생성 및 해상도 설정
        CreateCanvas();

        // UI 입력을 처리할 EventSystem 확인
        CreateEventSystem();

        // 메인 메뉴 구성
        BuildMainMenu();
    }


    // =====================================================
    // 5. 효과음 생성 및 재생
    // =====================================================

    /// <summary>
    /// 메인 메뉴 오브젝트에 AudioSource를 추가한다.
    ///
    /// Inspector에 연결한 효과음을 재생하는 데 사용한다.
    /// 효과음 파일을 아직 연결하지 않아도 오류가 발생하지 않는다.
    /// </summary>
    private void CreateAudioSource()
    {
        audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.volume = soundVolume;
    }


    /// <summary>
    /// 전달받은 효과음을 재생한다.
    ///
    /// 효과음 파일이 비어 있으면 아무 작업도 하지 않는다.
    /// </summary>
    private void PlaySound(AudioClip clip)
    {
        if (clip == null || audioSource == null)
            return;

        audioSource.PlayOneShot(clip, soundVolume);
    }


    // =====================================================
    // 6. Canvas 생성
    // =====================================================

    /// <summary>
    /// 메인 메뉴가 표시될 Canvas를 생성한다.
    ///
    /// Screen Space - Overlay 방식으로 설정하여
    /// 카메라 위치와 관계없이 화면 위에 UI를 표시한다.
    ///
    /// CanvasScaler를 사용하여 기준 해상도에 맞춰
    /// 다른 화면 해상도에서도 UI 크기가 조정되도록 한다.
    /// </summary>
    private void CreateCanvas()
    {
        GameObject canvasObject = new GameObject(
            "MainMenuCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster)
        );

        // MainMenuManager 아래에 Canvas 배치
        canvasObject.transform.SetParent(transform, false);

        mainCanvas = canvasObject.GetComponent<Canvas>();

        // 화면 위에 UI를 직접 표시
        mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // CanvasScaler 컴포넌트 가져오기
        CanvasScaler scaler =
            canvasObject.GetComponent<CanvasScaler>();

        // 화면 해상도에 따라 UI 크기 자동 조정
        scaler.uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;

        // 기준 해상도: 1920 x 1080
        scaler.referenceResolution =
            new Vector2(1920, 1080);

        // 가로와 세로 비율을 균형 있게 반영
        scaler.matchWidthOrHeight = 0.5f;
    }


    // =====================================================
    // 7. EventSystem 확인
    // =====================================================

    /// <summary>
    /// UI의 마우스 클릭 및 오버 입력을 처리할 EventSystem을 확인한다.
    ///
    /// 기존 씬에 EventSystem이 있으면 새로 만들지 않는다.
    /// 없으면 기본 StandaloneInputModule 방식으로 생성한다.
    /// </summary>
    private void CreateEventSystem()
    {
        // 현재 씬에 EventSystem이 있는지 확인
        if (FindFirstObjectByType<EventSystem>() != null)
            return;

        // EventSystem이 없으면 새로 생성
        new GameObject(
            "MainMenuEventSystem",
            typeof(EventSystem),
            typeof(StandaloneInputModule)
        );
    }


    // =====================================================
    // 8. 메인 메뉴 전체 구성
    // =====================================================

    /// <summary>
    /// 메인 메뉴의 전체 화면 구성을 담당한다.
    ///
    /// [상단 중앙]
    /// 게임 타이틀
    ///
    /// [왼쪽]
    /// 전투 / 재정비 / 퇴근
    ///
    /// [오른쪽]
    /// 전투 선택 후 맵 4개 표시
    ///
    /// UI 위치는 기준 해상도 1920 x 1080의
    /// 정규화 앵커를 이용하여 설정한다.
    /// </summary>
    private void BuildMainMenu()
    {
        // 전체 배경 패널 생성
        RectTransform root = CreatePanel(
            "MainBackground",
            mainCanvas.transform,
            panelColor
        );

        // Canvas 전체를 배경으로 채움
        Stretch(root);


        // -------------------------------------------------
        // 상단 중앙 게임 타이틀
        // -------------------------------------------------

        CreateText(
            "GameTitle",
            gameTitle,
            root,
            48,
            outlineColor,
            TextAlignmentOptions.Center,
            new Vector2(0.2f, 0.83f),
            new Vector2(0.8f, 0.96f)
        );


        // -------------------------------------------------
        // 타이틀 아래 구분선
        // -------------------------------------------------

        CreatePanel(
            "TitleLine",
            root,
            outlineColor,
            new Vector2(0.2f, 0.815f),
            new Vector2(0.8f, 0.819f)
        );


        // -------------------------------------------------
        // 왼쪽 메뉴 버튼
        //
        // 기존보다 가로 폭을 줄이고 글자를 키웠다.
        // -------------------------------------------------

        // 전투 버튼
        CreateMenuButton(
            "CombatButton",
            "전투",
            root,
            new Vector2(0.07f, 0.57f),
            new Vector2(0.28f, 0.69f),
            OnCombatClicked
        );


        // 재정비 버튼
        CreateMenuButton(
            "RefitButton",
            "재정비",
            root,
            new Vector2(0.07f, 0.40f),
            new Vector2(0.28f, 0.52f),
            OnRefitClicked
        );


        // 퇴근 버튼
        CreateMenuButton(
            "QuitButton",
            "퇴근",
            root,
            new Vector2(0.07f, 0.23f),
            new Vector2(0.28f, 0.35f),
            OnQuitClicked
        );


        // -------------------------------------------------
        // 오른쪽 맵 선택 영역
        // 처음에는 숨겨진 상태로 생성
        // -------------------------------------------------

        CreateMapSection(root);
    }



    // =====================================================
    // 9. 오른쪽 맵 선택 영역
    // =====================================================

    /// <summary>
    /// 오른쪽에 표시할 작전 선택 영역을 생성한다.
    ///
    /// 별도 팝업이나 씬이 아니라 현재 메인 메뉴 화면의
    /// 오른쪽 영역을 활성화하는 방식이다.
    ///
    /// 전투 버튼을 누르기 전에는 비활성화되어 있다.
    /// 
    /// [위치 수정]
    /// 기존 맵 선택 영역이 화면 오른쪽으로 치우쳐 있어
    /// 왼쪽 메뉴 버튼과 자연스럽게 이어지도록
    /// 전체 맵 영역을 왼쪽으로 이동했다.
    /// </summary>
    private void CreateMapSection(RectTransform root)
    {
        GameObject sectionObject = new GameObject(
            "MapSelectionSection",
            typeof(RectTransform)
        );

        sectionObject.transform.SetParent(root, false);

        RectTransform section =
            sectionObject.GetComponent<RectTransform>();

        Stretch(section);

        // 맵 선택 영역 참조 저장
        mapSection = sectionObject;

        // 최초 실행 시에는 숨김
        mapSection.SetActive(false);


        // -------------------------------------------------
        // 작전 선택 제목
        //
        // 기존 X좌표 0.40 ~ 0.92
        // 수정 X좌표 0.34 ~ 0.86
        //
        // 맵 카드 전체 위치와 제목을 함께 이동한다.
        // -------------------------------------------------

        CreateText(
    "OperationTitle",
    "작전 선택",
    section,
    29,
    outlineColor,
    TextAlignmentOptions.Left,
    new Vector2(0.34f, 0.70f), // 기존 0.75f → 0.70f
    new Vector2(0.86f, 0.76f)  // 기존 0.81f → 0.76f
);

        // -------------------------------------------------
        // 맵 카드 4개 배치
        //
        // 전체적으로 왼쪽으로 이동
        //
        // 1행: 맵 01 / 맵 02
        // 2행: 맵 03 / 맵 04
        //
        // 카드 크기와 세로 위치는 기존 그대로 유지한다.
        // -------------------------------------------------

        // 맵 01 - 왼쪽 위
        CreateMapCard(
            section, 0,
            new Vector2(0.34f, 0.43f),
            new Vector2(0.58f, 0.70f)
        );

        // 맵 02 - 오른쪽 위
        CreateMapCard(
            section, 1,
            new Vector2(0.62f, 0.43f),
            new Vector2(0.86f, 0.70f)
        );

        // 맵 03 - 왼쪽 아래
        CreateMapCard(
            section, 2,
            new Vector2(0.34f, 0.12f),
            new Vector2(0.58f, 0.39f)
        );

        // 맵 04 - 오른쪽 아래
        CreateMapCard(
            section, 3,
            new Vector2(0.62f, 0.12f),
            new Vector2(0.86f, 0.39f)
        );
    }



    // =====================================================
    // 10. 개별 맵 카드 생성
    // =====================================================

    /// <summary>
    /// 맵 하나를 표시할 카드 UI를 생성한다.
    ///
    /// Inspector의 Map Images 배열에 이미지가 연결되어 있으면
    /// 해당 이미지를 카드 배경에 적용한다.
    ///
    /// 이미지가 없으면 기본 청회색 배경을 사용한다.
    ///
    /// MAP 번호는 카드 상단에 배치하여
    /// 마우스 오버 시에도 항상 알아보기 쉽게 표시한다.
    /// </summary>
    private void CreateMapCard(
        RectTransform parent,
        int index,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        GameObject cardObject = new GameObject(
            "MapCard_" + (index + 1),
            typeof(RectTransform),
            typeof(Image),
            typeof(Button)
        );

        cardObject.transform.SetParent(parent, false);

        RectTransform rect =
            cardObject.GetComponent<RectTransform>();

        SetRect(rect, anchorMin, anchorMax);


        // -------------------------------------------------
        // 카드 배경 및 이미지
        // -------------------------------------------------

        Image background =
            cardObject.GetComponent<Image>();

        background.color = inactiveColor;

        // 해당 맵 이미지가 연결되어 있으면 적용
        if (mapImages != null &&
            index < mapImages.Length &&
            mapImages[index] != null)
        {
            background.sprite = mapImages[index];
            background.type = Image.Type.Simple;
            background.preserveAspect = true;
        }


        // -------------------------------------------------
        // 카드 테두리
        // -------------------------------------------------

        Outline cardOutline =
            AddOutline(cardObject, inactiveColor);


        // -------------------------------------------------
        // 카드 버튼 클릭 이벤트
        // -------------------------------------------------

        Button button =
            cardObject.GetComponent<Button>();

        // 기본 Unity 색상 전환 대신 직접 효과 제어
        button.transition = Selectable.Transition.None;

        // 클릭한 맵 번호 전달
        button.onClick.AddListener(
            () => OnMapClicked(index)
        );


        // 마우스 오버 및 아웃 효과 연결
        AddHoverEffect(
            cardObject,
            background,
            cardOutline
        );


        // -------------------------------------------------
        // 맵 번호 표시
        // 카드의 위쪽에 배치
        // -------------------------------------------------

        CreateText(
            "MapNumber",
            "MAP 0" + (index + 1),
            cardObject.transform,
            19,
            outlineColor,
            TextAlignmentOptions.Left,
            new Vector2(0.06f, 0.80f),
            new Vector2(0.94f, 0.97f)
        );


        // -------------------------------------------------
        // 맵 이름 표시
        // -------------------------------------------------

        CreateText(
            "MapName",
            GetMapName(index),
            cardObject.transform,
            19,
            normalTextColor,
            TextAlignmentOptions.Center,
            new Vector2(0.04f, 0.04f),
            new Vector2(0.96f, 0.24f)
        );
    }


    // =====================================================
    // 11. 왼쪽 메뉴 버튼 생성
    // =====================================================

    /// <summary>
    /// 전투, 재정비, 퇴근 버튼을 공통 방식으로 생성한다.
    ///
    /// 버튼별 이름, 글자, 위치, 클릭 함수를 전달받는다.
    ///
    /// 기존보다 글자 크기를 키우고 버튼 가로 폭을 줄였다.
    /// </summary>
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


        // 버튼 기본 배경
        Image image =
            buttonObject.GetComponent<Image>();

        image.color = inactiveColor;


        // -------------------------------------------------
        // 버튼 테두리
        // -------------------------------------------------

        Outline buttonOutline =
            AddOutline(buttonObject, inactiveColor);


        // -------------------------------------------------
        // 버튼 클릭 함수 연결
        // -------------------------------------------------

        Button button =
            buttonObject.GetComponent<Button>();

        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(action);


        // 마우스 오버 효과 연결
        AddHoverEffect(
            buttonObject,
            image,
            buttonOutline
        );


        // -------------------------------------------------
        // 버튼 중앙 글자
        // 기존 28에서 32로 글자 크기 확대
        // -------------------------------------------------

        CreateText(
            "Label",
            label,
            buttonObject.transform,
            32,
            normalTextColor,
            TextAlignmentOptions.Center,
            Vector2.zero,
            Vector2.one
        );
    }


    // =====================================================
    // 12. UI 테두리 생성
    // =====================================================

    /// <summary>
    /// 버튼이나 맵 카드에 Outline 컴포넌트를 추가한다.
    ///
    /// 기본 상태에서는 어두운 테두리를 사용하고
    /// 마우스 오버 시 연녹색으로 변경한다.
    /// </summary>
    private Outline AddOutline(
        GameObject target,
        Color initialColor)
    {
        Outline outline =
            target.AddComponent<Outline>();

        outline.effectColor = initialColor;

        // 테두리 두께
        outline.effectDistance =
            new Vector2(2f, 2f);

        // 테두리 색상이 이미지 투명도에 영향을 받지 않도록 설정
        outline.useGraphicAlpha = false;

        return outline;
    }


    // =====================================================
    // 13. 마우스 오버 및 마우스 아웃 효과
    // =====================================================

    /// <summary>
    /// 버튼이나 맵 카드에 마우스가 들어오고 나갈 때
    /// 배경색, 테두리, 크기를 변경한다.
    ///
    /// PointerEnter : 마우스가 UI 위에 올라왔을 때
    /// PointerExit  : 마우스가 UI 밖으로 나갔을 때
    ///
    /// 마우스 오버 배경은 연녹색이 아니라
    /// 어두운 청회색 #26343E를 사용한다.
    ///
    /// 따라서 흰색 글자와 연녹색 글자가
    /// 배경색에 묻히는 문제를 방지한다.
    /// </summary>
    private void AddHoverEffect(
        GameObject target,
        Image targetImage,
        Outline targetOutline)
    {
        EventTrigger trigger =
            target.AddComponent<EventTrigger>();


        // -------------------------------------------------
        // 마우스가 UI 위에 올라왔을 때
        // -------------------------------------------------

        EventTrigger.Entry enter =
            new EventTrigger.Entry();

        enter.eventID =
            EventTriggerType.PointerEnter;

        enter.callback.AddListener((data) =>
        {
            // 배경을 밝은 청회색으로 변경
            targetImage.color = hoverColor;

            // 테두리를 연녹색으로 강조
            targetOutline.effectColor = outlineColor;

            // 해당 UI만 확대
            PlayScaleEffect(
                target.transform,
                1.045f
            );

            // 마우스 오버 효과음
            PlaySound(hoverSound);
        });

        trigger.triggers.Add(enter);


        // -------------------------------------------------
        // 마우스가 UI 밖으로 나갔을 때
        // -------------------------------------------------

        EventTrigger.Entry exit =
            new EventTrigger.Entry();

        exit.eventID =
            EventTriggerType.PointerExit;

        exit.callback.AddListener((data) =>
        {
            // 기본 배경색으로 복귀
            targetImage.color = inactiveColor;

            // 기본 테두리로 복귀
            targetOutline.effectColor = inactiveColor;

            // 원래 크기로 복귀
            PlayScaleEffect(
                target.transform,
                1f
            );
        });

        trigger.triggers.Add(exit);
    }


    // =====================================================
    // 14. 개별 확대 애니메이션 관리
    // =====================================================

    /// <summary>
    /// 특정 UI의 기존 확대 애니메이션만 중단하고
    /// 새로운 애니메이션을 실행한다.
    ///
    /// 이전 코드의 StopAllCoroutines()는
    /// 한 버튼의 애니메이션이 다른 버튼의 애니메이션까지
    /// 중단할 수 있었기 때문에 개별 관리 방식으로 변경했다.
    /// </summary>
    private void PlayScaleEffect(
        Transform target,
        float targetScale)
    {
        if (target == null)
            return;

        // 해당 UI에 실행 중인 애니메이션이 있다면 중단
        if (scaleCoroutines.TryGetValue(
            target,
            out Coroutine runningCoroutine))
        {
            if (runningCoroutine != null)
                StopCoroutine(runningCoroutine);

            scaleCoroutines.Remove(target);
        }

        // 새로운 애니메이션 실행
        Coroutine newCoroutine =
            StartCoroutine(
                ScaleEffect(target, targetScale)
            );

        // 해당 UI의 애니메이션 참조 저장
        scaleCoroutines[target] = newCoroutine;
    }


    // =====================================================
    // 15. 확대 및 축소 애니메이션
    // =====================================================

    /// <summary>
    /// 버튼이나 맵 카드의 크기를 부드럽게 변경한다.
    ///
    /// targetScale = 1.045 : 약 4.5% 확대
    /// targetScale = 1.0   : 원래 크기로 복귀
    ///
    /// Time.unscaledDeltaTime을 사용하므로
    /// 게임 시간이 정지되어 있어도 애니메이션이 진행된다.
    /// </summary>
    private IEnumerator ScaleEffect(
        Transform target,
        float targetScale)
    {
        Vector3 startScale =
            target.localScale;

        Vector3 endScale =
            Vector3.one * targetScale;

        float elapsed = 0f;

        // 애니메이션 진행 시간
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

        // 마지막 프레임에서 목표 크기 적용
        target.localScale = endScale;

        // 애니메이션이 끝났으므로 참조 제거
        scaleCoroutines.Remove(target);
    }


    // =====================================================
    // 16. 전투 버튼
    // =====================================================

    /// <summary>
    /// 전투 버튼을 클릭했을 때 실행된다.
    ///
    /// 현재 화면의 오른쪽에 맵 선택 영역을 표시한다.
    /// 실제 전투 씬으로 이동하는 것은 맵 카드를 클릭했을 때다.
    /// </summary>
    private void OnCombatClicked()
    {
        PlaySound(clickSound);

        if (mapSection != null)
            mapSection.SetActive(true);

        Debug.Log("전투 선택 - 작전 지역 4개 표시");
    }


    // =====================================================
    // 17. 재정비 버튼
    // =====================================================

    /// <summary>
    /// 재정비 버튼을 클릭했을 때 실행된다.
    ///
    /// 추후 무기 변경 및 장비 준비 화면을 연결할 예정이다.
    /// 현재는 임시 기능으로 로그만 출력한다.
    ///
    /// 팀장 MVP의 무기 및 인벤토리 로직은 수정하지 않는다.
    /// </summary>
    private void OnRefitClicked()
    {
        PlaySound(clickSound);

        Debug.Log(
            "재정비 선택 - 무기 변경 기능은 추후 구현"
        );
    }


    // =====================================================
    // 18. 퇴근 버튼
    // =====================================================

    /// <summary>
    /// 퇴근 버튼을 클릭하면 게임을 종료한다.
    ///
    /// Unity 에디터에서는 Application.Quit()이
    /// 에디터 자체를 종료하지 않으므로 로그만 출력한다.
    ///
    /// 빌드된 게임에서는 Application.Quit()으로 종료한다.
    /// </summary>
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
    // 19. 맵 선택 및 씬 이동
    // =====================================================

    /// <summary>
    /// 맵 카드 중 하나를 클릭했을 때 실행된다.
    ///
    /// Inspector의 Map Scene Names 배열에서
    /// 선택한 맵에 해당하는 씬 이름을 가져온다.
    ///
    /// 씬 이름이 비어 있으면 씬을 이동하지 않고
    /// Console에 안내 메시지를 출력한다.
    ///
    /// 실제 씬 이름은 MVP 구조를 확인한 뒤 입력해야 한다.
    /// </summary>
    private void OnMapClicked(int index)
    {
        PlaySound(mapSelectSound);

        // 유효한 맵 번호인지 확인
        if (index < 0 || index >= 4)
        {
            Debug.LogWarning(
                "잘못된 맵 번호가 전달되었습니다."
            );

            return;
        }

        // 씬 이름이 입력되어 있는지 확인
        if (mapSceneNames == null ||
            index >= mapSceneNames.Length ||
            string.IsNullOrWhiteSpace(mapSceneNames[index]))
        {
            Debug.Log(
                GetMapName(index) +
                " 선택됨 - Inspector에 씬 이름을 입력해야 합니다."
            );

            return;
        }

        string sceneName =
            mapSceneNames[index].Trim();

        // Build Settings 또는 Build Profiles에 등록된
        // 실제 씬 이름으로 이동
        SceneManager.LoadScene(sceneName);
    }


    // =====================================================
    // 20. 맵 이름 확인
    // =====================================================

    /// <summary>
    /// Inspector에서 맵 이름을 수정할 수 있도록 분리했다.
    ///
    /// 이름이 비어 있거나 배열에 해당 항목이 없으면
    /// 기본 작전 지역 이름을 사용한다.
    /// </summary>
    private string GetMapName(int index)
    {
        if (mapNames != null &&
            index >= 0 &&
            index < mapNames.Length &&
            !string.IsNullOrWhiteSpace(mapNames[index]))
        {
            return mapNames[index];
        }

        return "작전 지역 " + (index + 1);
    }


    // =====================================================
    // 21. UI 패널 생성 공통 함수
    // =====================================================

    /// <summary>
    /// 배경이나 구분선처럼 Image가 필요한 UI 오브젝트를 생성한다.
    ///
    /// parent : 생성한 오브젝트가 들어갈 부모
    /// color  : 오브젝트 배경색
    /// anchorMin / anchorMax : 화면에서 차지할 위치와 크기
    ///
    /// 위치값을 생략하면 부모 영역 전체를 사용한다.
    /// </summary>
    private RectTransform CreatePanel(
        string objectName,
        Transform parent,
        Color color,
        Vector2? anchorMin = null,
        Vector2? anchorMax = null)
    {
        GameObject obj = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(Image)
        );

        obj.transform.SetParent(parent, false);

        RectTransform rect =
            obj.GetComponent<RectTransform>();

        rect.anchorMin =
            anchorMin ?? Vector2.zero;

        rect.anchorMax =
            anchorMax ?? Vector2.one;

        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        obj.GetComponent<Image>().color = color;

        return rect;
    }


    // =====================================================
    // 22. TMP 텍스트 생성 공통 함수
    // =====================================================

    /// <summary>
    /// 메인 메뉴에서 사용할 TextMeshPro 텍스트를 생성한다.
    ///
    /// 게임 타이틀, 메뉴 이름, 맵 번호, 맵 이름 등
    /// 모든 글자가 이 함수를 통해 생성된다.
    ///
    /// Inspector에 Hud Font를 지정하면
    /// 생성되는 모든 텍스트에 같은 폰트를 적용한다.
    ///
    /// Raycast Target을 꺼서 글자가 버튼 입력을 가로채지 않게 한다.
    /// </summary>
    private void CreateText(
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

        SetRect(
            rect,
            anchorMin,
            anchorMax
        );

        TextMeshProUGUI text =
            obj.GetComponent<TextMeshProUGUI>();

        // 화면에 표시할 글자
        text.text = content;

        // 기본 글자 크기
        text.fontSize = fontSize;

        // 글자 색상
        text.color = color;

        // 글자 정렬
        text.alignment = alignment;

        // 영역에 맞게 글자 크기를 자동 조절
        text.enableAutoSizing = true;
        text.fontSizeMin = 12;
        text.fontSizeMax = fontSize;

        // Inspector에서 지정한 폰트 적용
        if (hudFont != null)
            text.font = hudFont;

        // 글자가 마우스 입력을 가로채지 않도록 설정
        text.raycastTarget = false;
    }


    // =====================================================
    // 23. RectTransform 위치 설정
    // =====================================================

    /// <summary>
    /// UI 오브젝트의 위치와 크기를 설정한다.
    ///
    /// anchorMin : 영역의 왼쪽 아래 기준점
    /// anchorMax : 영역의 오른쪽 위 기준점
    ///
    /// 0~1 사이의 값을 사용하므로
    /// 부모 영역을 기준으로 비율에 맞춰 배치된다.
    /// </summary>
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
    // 24. 부모 영역 전체 채우기
    // =====================================================

    /// <summary>
    /// 자식 UI 오브젝트가 부모 영역 전체를 채우도록 설정한다.
    ///
    /// 메인 배경과 맵 선택 영역처럼
    /// 부모 Canvas 전체를 기준으로 사용하는 오브젝트에 적용한다.
    /// </summary>
    private void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;

        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
