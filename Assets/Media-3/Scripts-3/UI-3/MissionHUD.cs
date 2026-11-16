
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameAi
{
    public class MissionHUD : MonoBehaviour
    {
        // =========================================================
        // 1. 공통 폰트 설정
        // =========================================================

        // Inspector에서 TextMeshPro용 폰트 에셋을 연결한다.
        //
        // Unity 프로젝트에 설치한 TTF/OTF 파일 자체가 아니라
        // Font Asset Creator로 생성한 TMP Font Asset을 넣어야 한다.
        //
        // 이 폰트는 아래 CreateText() 함수에서 적용된다.
        // 따라서 미션 이름, 목표, 게이지 제목, 시간 등
        // MissionHUD에서 생성하는 모든 텍스트에 동일하게 적용된다.
        //
        // Inspector에서 아무것도 지정하지 않으면
        // TextMeshPro의 기본 폰트를 그대로 사용한다.
        [Header("Mission HUD Font")]
        [SerializeField]
        [Tooltip("미션 HUD 전체에 적용할 TextMeshPro 한글 폰트")]
        private TMP_FontAsset hudFont;


        // =========================================================
        // 2. HUD 디자인 색상 설정
        // =========================================================

        // 패널 내부 배경색
        // HealthHUD에서 사용하는 색상과 동일하게 적용한다.
        // RGB(23, 29, 36)은 어두운 청회색이며,
        // Alpha 225를 적용해 약간의 투명도를 유지한다.
        private readonly Color panelColor =
            new Color32(23, 29, 36, 225);

        // 패널 테두리 색상
        // 체력 HUD와 동일한 밝은 연녹색을 사용한다.
        // 패널 배경과 대비시켜 전술 장비 패널의 외곽을 강조한다.
        private readonly Color outlineColor =
            new Color32(164, 214, 164, 255);

        // 제목 및 강조 글씨 색상
        // 미션 이름과 게이지 미리보기 제목에 사용한다.
        // 패널 테두리와 같은 연녹색으로 디자인을 통일한다.
        private readonly Color accentColor =
            new Color32(164, 214, 164, 255);

        // 진행 막대의 비활성 영역 색상
        // 게이지가 아직 채워지지 않은 부분을 표시한다.
        // HealthHUD의 비활성 게이지 색상과 동일하다.
        private readonly Color inactiveColor =
            new Color32(65, 77, 88, 255);

        // 진행 막대의 채워지는 영역 색상
        // 실제 게임에서는 미션 진행률을 나타낸다.
        // 미리보기에서는 게이지 디자인 확인용으로 사용한다.
        private readonly Color progressColor =
            new Color32(164, 214, 164, 255);

        // 일반 글씨 색상
        // 목표 및 시간 등 기본 정보를 흰색으로 표시한다.
        private readonly Color textColor =
            Color.white;


        // =========================================================
        // 3. 게임 데이터 및 UI 참조 변수
        // =========================================================

        // GameManager 참조
        // 실제 게임 씬에서 게임 전체를 관리하는 GameManager를 가져온다.
        // GameManager를 통해 MissionManager의 미션 정보를 읽는다.
        // 별도 HUD 미리보기 씬에는 GameManager가 없을 수 있으므로
        // 이 변수가 null인 경우 미리보기 모드로 동작한다.
        private GameManager gameManager;

        // 미션 이름 표시용 텍스트
        // 실제 게임에서는 선택된 맵과 미션 종류에 맞는 이름을 표시한다.
        // 미리보기에서는 데이터 연결 안내 문구를 표시한다.
        private TextMeshProUGUI missionNameText;

        // 현재 목표 표시용 텍스트
        // 실제 게임에서는 MissionManager의 진행 상태를 바탕으로
        // 현재 수행해야 할 목표를 한국어로 표시한다.
        private TextMeshProUGUI objectiveText;

        // 게이지 미리보기 제목 표시용 텍스트
        // 미리보기 씬에서 진행 막대가 어떤 용도인지 알려준다.
        // 실제 게임에서는 별도의 미리보기 제목을 표시하지 않는다.
        private TextMeshProUGUI progressPreviewText;

        // 미션 시간 표시용 텍스트
        // 실제 게임에서는 제한 시간 또는 경과 시간을 표시한다.
        // 미리보기에서는 데이터가 없다는 안내 문구를 표시한다.
        private TextMeshProUGUI timeText;

        // 진행률을 표시하는 막대
        // 실제 게임에서는 MissionManager.Progress 값을 반영한다.
        // 미리보기에서는 디자인 확인용 임시 길이를 적용한다.
        private Image progressFill;

        // HUD 전체 패널
        // 배경 이미지, 테두리, 텍스트, 게이지를 포함하는 부모 오브젝트다.
        private GameObject missionPanel;


        // =========================================================
        // 4. Unity 초기화 및 실행 모드 확인
        // =========================================================

        private void Start()
        {
            // -----------------------------------------------------
            // 4-1. GameManager 검색
            // -----------------------------------------------------

            // 현재 씬에 실제 게임을 관리하는 GameManager가 있는지 검색한다.
            // GameManager는 게임 씬에서 생성되는 게임 관리 객체다.
            //
            // 별도로 제작한 HUD 미리보기 씬에는 GameManager가 없으므로
            // 검색 결과가 null로 나오는 것이 정상이다.
            gameManager = FindObjectOfType<GameManager>();


            // -----------------------------------------------------
            // 4-2. HUD 화면 생성
            // -----------------------------------------------------

            // 실제 게임 씬인지 미리보기 씬인지와 관계없이
            // 동일한 디자인의 HUD 패널과 UI 요소를 생성한다.
            // 이 과정에서 Canvas, 패널, 텍스트, 진행 막대가 만들어진다.
            CreateHUD();


            // -----------------------------------------------------
            // 4-3. 실행 모드 확인
            // -----------------------------------------------------

            if (gameManager == null)
            {
                // GameManager가 없으면 미리보기 모드로 실행한다.
                // 미리보기 씬에서는 실제 게임 데이터가 없으므로
                // UpdatePreview()에서 안내 문구와 게이지를 표시한다.
                Debug.Log(
                    "[MissionHUD] HUD 미리보기 모드로 실행합니다."
                );
            }
            else
            {
                // GameManager가 존재하면 실제 게임 모드로 실행한다.
                // 이후 Update()에서 MissionManager의 데이터를 읽는다.
                Debug.Log(
                    "[MissionHUD] 실제 게임 데이터 연결을 확인했습니다."
                );
            }
        }


        // =========================================================
        // 5. HUD 화면 구성
        // =========================================================

        private void CreateHUD()
        {
            // -----------------------------------------------------
            // 5-1. Canvas 생성
            // -----------------------------------------------------

            // HUD의 모든 UI 요소를 담을 Canvas를 생성한다.
            // Screen Space - Overlay 방식으로 설정하여
            // 게임 화면 위에 HUD가 항상 표시되도록 한다.
            GameObject canvasObject = new GameObject(
                "MissionHUD_Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster)
            );

            // 생성한 Canvas의 컴포넌트를 가져온다.
            Canvas canvas =
                canvasObject.GetComponent<Canvas>();

            // 카메라와 별도로 화면 위에 직접 UI를 표시한다.
            canvas.renderMode =
                RenderMode.ScreenSpaceOverlay;

            // 다른 Canvas와 겹칠 경우 표시 순서를 결정한다.
            // 숫자가 클수록 앞쪽에 표시된다.
            canvas.sortingOrder = 20;


            // -----------------------------------------------------
            // 5-2. Canvas 해상도 및 크기 설정
            // -----------------------------------------------------

            // 해상도가 달라져도 UI 크기가 비정상적으로 변하지 않도록
            // CanvasScaler를 사용해 화면 크기에 맞춰 조정한다.
            CanvasScaler scaler =
                canvasObject.GetComponent<CanvasScaler>();

            scaler.uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;

            // UI 배치의 기준 해상도
            scaler.referenceResolution =
                new Vector2(1920, 1080);

            // 가로와 세로 중간값을 기준으로 크기를 조절한다.
            scaler.matchWidthOrHeight = 0.5f;


            // -----------------------------------------------------
            // 5-3. 미션 패널 생성
            // -----------------------------------------------------

            // 미션 HUD의 모든 UI 요소를 담을 부모 패널을 생성한다.
            // 이 패널 안에 미션 이름, 목표, 진행 막대, 시간이 배치된다.
            missionPanel = CreateUIObject(
                "MissionPanel",
                canvasObject.transform
            );

            RectTransform panelRect =
                missionPanel.GetComponent<RectTransform>();

            // 화면 오른쪽 위를 기준으로 패널을 배치한다.
            // anchorMin과 anchorMax를 같은 위치로 설정해
            // 화면 오른쪽 위를 고정 기준점으로 사용한다.
            panelRect.anchorMin = new Vector2(1, 1);
            panelRect.anchorMax = new Vector2(1, 1);

            // 패널의 오른쪽 위 모서리를 기준으로 위치를 계산한다.
            panelRect.pivot = new Vector2(1, 1);

            // 화면 오른쪽과 위쪽에서 각각 30만큼 떨어뜨린다.
            panelRect.anchoredPosition =
                new Vector2(-30, -30);

            // 패널의 가로와 세로 크기를 설정한다.
            panelRect.sizeDelta =
                new Vector2(430, 190);


            // -----------------------------------------------------
            // 5-4. 패널 배경색 적용
            // -----------------------------------------------------

            // 패널에 Image 컴포넌트를 추가한다.
            // Image는 패널의 실제 배경을 그리는 역할을 한다.
            Image panelImage =
                missionPanel.AddComponent<Image>();

            // HealthHUD와 동일한 어두운 청회색을 적용한다.
            panelImage.color = panelColor;


            // -----------------------------------------------------
            // 5-5. 패널 외곽선 적용
            // -----------------------------------------------------

            // Outline 컴포넌트를 추가해 패널 외곽에 테두리를 만든다.
            // 체력 HUD와 동일한 연녹색으로 두 HUD의 디자인을 통일한다.
            Outline outline =
                missionPanel.AddComponent<Outline>();

            // 테두리 색상과 두께를 설정한다.
            outline.effectColor = outlineColor;
            outline.effectDistance =
                new Vector2(1, -1);


            // -----------------------------------------------------
            // 5-6. 미션 이름 UI 생성
            // -----------------------------------------------------

            // 패널 상단에 미션 이름을 표시할 텍스트를 생성한다.
            // 실제 게임에서는 맵 ID와 미션 종류에 따라 이름이 바뀐다.
            // 미리보기에서는 데이터가 없다는 안내를 표시한다.
            missionNameText = CreateText(
                "MissionName",
                missionPanel.transform,
                new Vector2(20, -18),
                new Vector2(390, 35),
                25,
                accentColor
            );


            // -----------------------------------------------------
            // 5-7. 현재 목표 UI 생성
            // -----------------------------------------------------

            // 미션 이름 아래에 현재 수행할 목표를 표시한다.
            // 실제 게임에서는 MissionManager의 미션 상태를 바탕으로
            // 현재 단계에 맞는 한국어 목표 문구를 표시한다.
            objectiveText = CreateText(
                "Objective",
                missionPanel.transform,
                new Vector2(20, -62),
                new Vector2(390, 35),
                19,
                textColor
            );


            // -----------------------------------------------------
            // 5-8. 게이지 미리보기 제목 생성
            // -----------------------------------------------------

            // 진행 막대 바로 위에 게이지의 용도를 알려주는
            // 짧은 제목을 표시할 텍스트를 생성한다.
            // 미리보기에서는 '게이지 미리보기'라고 표시한다.
            // 실제 게임에서는 이 제목을 숨긴다.
            progressPreviewText = CreateText(
                "ProgressPreviewTitle",
                missionPanel.transform,
                new Vector2(20, -99),
                new Vector2(390, 18),
                14,
                accentColor
            );


            // -----------------------------------------------------
            // 5-9. 진행 막대 배경 생성
            // -----------------------------------------------------

            // 진행 막대가 놓일 전체 배경을 생성한다.
            // 이 배경의 너비는 진행률이 100%일 때의 기준이 된다.
            GameObject progressBack = CreateUIObject(
                "ProgressBack",
                missionPanel.transform
            );

            RectTransform backRect =
                progressBack.GetComponent<RectTransform>();

            // 패널의 왼쪽 위를 기준으로 위치를 설정한다.
            backRect.anchorMin = new Vector2(0, 1);
            backRect.anchorMax = new Vector2(0, 1);
            backRect.pivot = new Vector2(0, 1);

            // 패널 왼쪽에서 20, 위쪽에서 122만큼 떨어진 곳에 배치한다.
            backRect.anchoredPosition =
                new Vector2(20, -122);

            // 진행 막대 배경의 전체 크기다.
            backRect.sizeDelta =
                new Vector2(390, 14);

            // Image 컴포넌트를 추가해 막대 배경을 표시한다.
            Image backImage =
                progressBack.AddComponent<Image>();

            // 아직 채워지지 않은 영역을 어두운 회청색으로 표시한다.
            backImage.color = inactiveColor;


            // -----------------------------------------------------
            // 5-10. 진행 막대 채움 영역 생성
            // -----------------------------------------------------

            // 진행 막대 배경 안에 실제로 채워지는 영역을 생성한다.
            // 이 오브젝트의 너비가 변하면서 진행률이 시각적으로 표현된다.
            GameObject fillObject = CreateUIObject(
                "ProgressFill",
                progressBack.transform
            );

            RectTransform fillRect =
                fillObject.GetComponent<RectTransform>();

            // 부모인 진행 막대 배경의 왼쪽을 기준으로 채워진다.
            // 높이는 부모 배경의 높이를 그대로 사용한다.
            fillRect.anchorMin = new Vector2(0, 0);
            fillRect.anchorMax = new Vector2(0, 1);

            // 왼쪽 끝을 기준으로 너비가 증가하도록 설정한다.
            fillRect.pivot = new Vector2(0, 0.5f);

            // 채움 영역의 시작 위치를 배경의 왼쪽 끝에 맞춘다.
            fillRect.anchoredPosition = Vector2.zero;

            // 처음 생성될 때는 너비를 0으로 설정한다.
            // 이후 Update() 또는 UpdatePreview()에서 너비를 갱신한다.
            fillRect.sizeDelta = new Vector2(0, 0);

            // 채움 영역에 Image 컴포넌트를 추가한다.
            progressFill =
                fillObject.AddComponent<Image>();

            // 실제 진행률을 나타내는 연녹색을 적용한다.
            progressFill.color = progressColor;


            // -----------------------------------------------------
            // 5-11. 시간 표시 UI 생성
            // -----------------------------------------------------

            // 패널 아래쪽에 미션 시간을 표시할 텍스트를 생성한다.
            // 실제 게임에서는 제한 시간 또는 경과 시간을 표시한다.
            // 미리보기에서는 데이터가 없다는 안내 문구가 표시된다.
            timeText = CreateText(
                "MissionTime",
                missionPanel.transform,
                new Vector2(20, -150),
                new Vector2(390, 30),
                18,
                textColor
            );
        }


        // =========================================================
        // 6. 공통 UI 오브젝트 생성 함수
        // =========================================================

        // UI 요소를 생성할 때 공통으로 사용하는 함수다.
        //
        // Unity UI는 일반 GameObject가 아니라 RectTransform을
        // 기준으로 위치와 크기를 관리한다.
        //
        // objectName : Hierarchy에 표시될 오브젝트 이름
        // parent     : 새 UI 오브젝트가 들어갈 부모 Transform
        //
        // 생성한 오브젝트를 부모 아래에 배치한 뒤 반환한다.
        private GameObject CreateUIObject(
            string objectName,
            Transform parent
        )
        {
            // RectTransform을 가진 UI 전용 오브젝트를 생성한다.
            GameObject obj = new GameObject(
                objectName,
                typeof(RectTransform)
            );

            // 생성한 UI 오브젝트를 지정된 부모 아래에 넣는다.
            // false를 전달해 부모의 로컬 UI 배치 기준을 유지한다.
            obj.transform.SetParent(parent, false);

            return obj;
        }


        // =========================================================
        // 7. 공통 텍스트 UI 생성 함수
        // =========================================================

        // 미션 이름, 목표, 게이지 제목, 시간 등을 생성하는 함수다.
        //
        // objectName : Hierarchy에 표시될 텍스트 오브젝트 이름
        // parent     : 텍스트가 들어갈 부모 오브젝트
        // position   : 부모 패널 기준 텍스트 위치
        // size       : 텍스트 영역의 가로와 세로 크기
        // fontSize   : 글씨 크기
        // color      : 글씨 색상
        //
        // 이 함수를 공통으로 사용하면 텍스트마다 반복해서
        // RectTransform과 TextMeshPro 설정을 작성할 필요가 없다.
        //
        // [폰트 적용 과정]
        // 1. Inspector에서 지정한 hudFont를 확인한다.
        // 2. 폰트가 지정되어 있으면 생성한 TMP 텍스트에 적용한다.
        // 3. 폰트가 null이면 TextMeshPro 기본 폰트를 유지한다.
        //
        // 모든 HUD 텍스트가 이 함수를 거쳐 생성되므로
        // 폰트를 한 번만 지정해도 전체 텍스트에 적용된다.
        private TextMeshProUGUI CreateText(
            string objectName,
            Transform parent,
            Vector2 position,
            Vector2 size,
            float fontSize,
            Color color
        )
        {
            // 텍스트를 담을 UI 오브젝트를 생성한다.
            GameObject obj = CreateUIObject(
                objectName,
                parent
            );

            // 위치와 크기를 관리할 RectTransform을 가져온다.
            RectTransform rect =
                obj.GetComponent<RectTransform>();

            // 부모의 왼쪽 위를 기준으로 텍스트를 배치한다.
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);

            // 전달받은 위치와 크기를 적용한다.
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            // TextMeshProUGUI를 추가해 실제 글씨를 표시한다.
            TextMeshProUGUI tmpText =
                obj.AddComponent<TextMeshProUGUI>();


            // -----------------------------------------------------
            // Inspector에서 지정한 공통 폰트 적용
            // -----------------------------------------------------

            // hudFont는 스크립트 상단의 SerializeField 변수다.
            // Inspector에서 TMP Font Asset을 넣으면 이곳으로 전달된다.
            //
            // 폰트가 지정되어 있다면 현재 생성한 텍스트에 적용한다.
            // 이 함수는 미션 이름, 목표, 게이지 제목, 시간 생성 시
            // 각각 실행되므로 모든 텍스트에 같은 폰트가 적용된다.
            //
            // 폰트를 지정하지 않은 경우에는 기본 폰트를 유지한다.
            if (hudFont != null)
            {
                tmpText.font = hudFont;
            }


            // -----------------------------------------------------
            // 텍스트 기본 설정
            // -----------------------------------------------------

            // 전달받은 글씨 크기와 색상을 설정한다.
            tmpText.fontSize = fontSize;
            tmpText.color = color;

            // 왼쪽 정렬로 표시한다.
            tmpText.alignment =
                TextAlignmentOptions.Left;

            // 한 줄 안에서 표시되도록 줄바꿈을 비활성화한다.
            tmpText.enableWordWrapping = false;

            return tmpText;
        }


        // =========================================================
        // 8. 실제 미션 데이터 및 HUD 갱신
        // =========================================================

        private void Update()
        {
            // -----------------------------------------------------
            // 8-1. 미리보기 모드 확인
            // -----------------------------------------------------

            // Start()에서 GameManager를 찾지 못했다면
            // 현재 씬은 별도로 제작한 HUD 미리보기 씬이다.
            //
            // 이 경우 실제 미션 데이터를 조회하지 않고
            // UpdatePreview()를 실행해 안내 문구와 게이지를 표시한다.
            if (gameManager == null)
            {
                UpdatePreview();
                return;
            }


            // -----------------------------------------------------
            // 8-2. 실제 미션 데이터 확인
            // -----------------------------------------------------

            // GameManager가 존재하더라도 MissionManager가 준비되지 않았다면
            // 아직 미션 데이터를 화면에 반영할 수 없다.
            // 데이터가 준비될 때까지 현재 화면 갱신을 중단한다.
            if (gameManager.Mission == null)
                return;

            // GameManager가 보유한 MissionManager 참조를 가져온다.
            // 이후 미션 이름, 목표, 진행률, 시간을 이 객체에서 읽는다.
            MissionManager mission = gameManager.Mission;


            // -----------------------------------------------------
            // 8-3. 미션 준비 상태 확인
            // -----------------------------------------------------

            // MissionManager의 현재 상태를 확인한다.
            // Prep 상태는 미션이 본격적으로 진행되기 전 준비 단계다.
            //
            // 준비 단계에서는 미션 HUD를 숨기고,
            // 준비 단계가 끝나면 HUD를 다시 표시한다.
            missionPanel.SetActive(
                mission.State != MissionManager.Prep
            );

            // 패널이 숨겨진 상태라면 아래 UI 갱신 작업을 진행하지 않는다.
            if (!missionPanel.activeSelf)
                return;


            // -----------------------------------------------------
            // 8-4. 미션 이름 표시
            // -----------------------------------------------------

            // SceneFlow에서 선택된 맵 ID를 가져온다.
            // 맵 ID와 MissionManager의 미션 종류를 기준으로
            // 화면에 표시할 한국어 미션 이름을 결정한다.
            //
            // 예:
            // map_01 → 지역 소탕
            // map_02 → 기밀 자료 확보
            // map_03 → 기밀 자료 탈취 및 탈출
            // map_04 → 최종 지역 소탕
            missionNameText.text = GetMissionName(
                SceneFlow.SelectedMapId,
                mission.MissionType
            );


            // -----------------------------------------------------
            // 8-5. 현재 목표 표시
            // -----------------------------------------------------

            // MissionManager의 미션 종류와 현재 상태를 확인한다.
            // 자료 확보 여부나 폭탄 해체 여부 등 실제 진행 상태에 따라
            // 현재 수행할 목표 문구를 결정한다.
            objectiveText.text =
                GetObjectiveText(mission);


            // -----------------------------------------------------
            // 8-6. 진행률 게이지 갱신
            // -----------------------------------------------------

            // MissionManager.Progress에서 실제 진행률을 가져온다.
            // Clamp01을 사용해 값이 0보다 작거나 1보다 커지지 않도록 한다.
            //
            // 0   : 진행되지 않음
            // 0.5 : 절반 진행
            // 1   : 완료
            //
            // 진행률 숫자는 표시하지 않고 막대의 길이로만 표현한다.
            float progress =
                Mathf.Clamp01(mission.Progress);

            // 진행 막대의 크기를 변경하기 위해 RectTransform을 가져온다.
            RectTransform fillRect =
                progressFill.GetComponent<RectTransform>();

            // 전체 너비 390에 실제 진행률을 곱해 채움 영역의 길이를 계산한다.
            // 예를 들어 진행률이 0.5라면 너비는 195가 된다.
            fillRect.sizeDelta =
                new Vector2(390f * progress, 0);


            // -----------------------------------------------------
            // 8-7. 미션 시간 갱신
            // -----------------------------------------------------

            // TimeLimit이 0보다 크면 제한 시간이 있는 미션이다.
            // 제한 시간이 있는 경우 남은 시간(TimeLeft)을 표시한다.
            //
            // 제한 시간이 없는 일반 미션에서는
            // 미션 시작 후 경과한 시간(Elapsed)을 표시한다.
            bool isCountdown =
                mission.TimeLimit > 0f;

            // 제한 시간 여부에 따라 표시할 시간 데이터를 선택한다.
            float currentTime = isCountdown
                ? mission.TimeLeft
                : mission.Elapsed;

            // 초 단위 시간을 정수로 변환하고 음수는 0으로 제한한다.
            int totalSeconds =
                Mathf.Max(0, Mathf.FloorToInt(currentTime));

            // 초 단위 데이터를 분:초 형식으로 변환한다.
            // 예: 154초 → 02:34
            string formattedTime =
                (totalSeconds / 60).ToString("00") + ":" +
                (totalSeconds % 60).ToString("00");

            // 제한 시간이 있는 미션은 '남은 시간',
            // 제한 시간이 없는 미션은 '경과 시간'이라는 제목을 붙인다.
            timeText.text =
                (isCountdown ? "남은 시간 " : "경과 시간 ") +
                formattedTime;


            // -----------------------------------------------------
            // 8-8. 실제 게임에서는 미리보기 제목 숨기기
            // -----------------------------------------------------

            // 실제 게임에서는 진행률 막대가 실제 미션 데이터를 표시하므로
            // 디자인 확인용 '게이지 미리보기' 문구를 숨긴다.
            progressPreviewText.gameObject.SetActive(false);
        }


        // =========================================================
        // 9. 미리보기 전용 HUD 표시
        // =========================================================

        private void UpdatePreview()
        {
            // -----------------------------------------------------
            // 9-1. 미션 패널 표시
            // -----------------------------------------------------

            // 별도 HUD 미리보기 씬에는 GameManager가 없다.
            // 따라서 미션 상태를 확인할 수 없으므로
            // 패널을 항상 표시해 디자인을 확인할 수 있도록 한다.
            missionPanel.SetActive(true);


            // -----------------------------------------------------
            // 9-2. 미션 이름 표시
            // -----------------------------------------------------

            // 실제 미션 이름은 MissionManager에서 가져와야 하지만
            // 미리보기 씬에는 해당 데이터가 존재하지 않는다.
            // 임의의 미션 이름을 표시하지 않고 데이터 안내 문구를 표시한다.
            missionNameText.text =
                "데이터를 불러올 수 없습니다.";


            // -----------------------------------------------------
            // 9-3. 현재 목표 표시
            // -----------------------------------------------------

            // 실제 목표 정보 역시 미션 데이터에 포함되어 있으므로
            // 현재 미리보기 씬에서는 가져올 수 없다.
            // 실제 게임 데이터가 연결되지 않았음을 문구로 안내한다.
            objectiveText.text =
                "데이터를 불러올 수 없습니다.";


            // -----------------------------------------------------
            // 9-4. 게이지 미리보기 제목 표시
            // -----------------------------------------------------

            // 실제 진행률이 아니라 HUD 디자인을 확인하기 위한
            // 별도 게이지라는 것을 알 수 있도록 제목을 표시한다.
            progressPreviewText.gameObject.SetActive(true);

            progressPreviewText.text =
                "게이지 미리보기";


            // -----------------------------------------------------
            // 9-5. 진행률 게이지 미리보기
            // -----------------------------------------------------

            // 실제 미션 진행률을 가져오는 것이 아니다.
            // 게이지의 배경색, 채움 색상, 전체 크기와 비율을
            // 확인할 수 있도록 임시로 65%를 설정한다.
            //
            // 실제 게임 씬에서는 MissionManager.Progress 값이
            // 이 자리에 적용된다.
            float previewProgress = 0.65f;

            // 진행 막대 채움 영역의 RectTransform을 가져온다.
            RectTransform fillRect =
                progressFill.GetComponent<RectTransform>();

            // 전체 너비 390에 미리보기 진행률 0.65를 곱한다.
            // 따라서 막대가 전체 길이의 65%만큼 채워진다.
            fillRect.sizeDelta =
                new Vector2(390f * previewProgress, 0);


            // -----------------------------------------------------
            // 9-6. 시간 표시
            // -----------------------------------------------------

            // 실제 게임에서는 MissionManager의 TimeLeft 또는
            // Elapsed 값을 사용해 미션 시간을 표시한다.
            //
            // 미리보기 씬에는 실제 시간 데이터가 없으므로
            // 임의의 시간을 표시하지 않고 안내 문구를 표시한다.
            timeText.text =
                "데이터를 불러올 수 없습니다.";
        }


        // =========================================================
        // 10. 맵 ID와 미션 종류에 따른 미션 이름 결정
        // =========================================================

        // 이 함수는 실제 게임에서 표시할 미션 이름을 결정한다.
        //
        // mapId:
        // SceneFlow.SelectedMapId에서 전달받은 선택 맵 ID
        //
        // missionType:
        // MissionManager에서 전달받은 미션 종류
        //
        // 같은 미션 종류라도 맵에 따라 이름이 달라질 수 있으므로
        // 먼저 맵 ID를 확인하고, 해당하지 않는 경우 미션 종류를 확인한다.
        private string GetMissionName(
            string mapId,
            string missionType
        )
        {
            // -----------------------------------------------------
            // 10-1. 맵 ID에 따른 미션 이름 결정
            // -----------------------------------------------------

            switch (mapId)
            {
                case "map_01":
                    return "지역 소탕";

                case "map_02":
                    return "기밀 자료 확보";

                case "map_03":
                    return "기밀 자료 탈취 및 탈출";

                case "map_04":
                    return "최종 지역 소탕";
            }


            // -----------------------------------------------------
            // 10-2. 등록되지 않은 맵의 기본 이름 결정
            // -----------------------------------------------------

            // 새로운 맵이 추가되었거나 맵 ID를 찾지 못한 경우
            // MissionManager의 미션 종류를 기준으로 기본 이름을 표시한다.
            switch (missionType)
            {
                case "steal":
                    return "기밀 자료 확보";

                case "steal_exit":
                    return "기밀 자료 탈취 및 탈출";

                case "bomb":
                    return "폭탄 해체";

                default:
                    return "지역 소탕";
            }
        }


        // =========================================================
        // 11. 현재 미션 상태에 따른 목표 문구 결정
        // =========================================================

        // 이 함수는 MissionManager의 실제 진행 상태를 읽어서
        // 화면에 표시할 한국어 목표 문구를 반환한다.
        //
        // mission:
        // GameManager가 보유한 MissionManager 객체
        //
        // 미션 종류에 따라 서로 다른 조건을 확인한다.
        // 예를 들어 자료 탈취 미션은 자료 확보 여부를 확인하고,
        // 폭탄 해체 미션은 Objective의 완료 여부를 확인한다.
        private string GetObjectiveText(
            MissionManager mission
        )
        {
            switch (mission.MissionType)
            {
                // -------------------------------------------------
                // 11-1. 적 소탕 미션
                // -------------------------------------------------

                case "eliminate":

                    // 최종 소탕 맵은 일반 소탕과 구분된 목표 문구를 사용한다.
                    if (SceneFlow.SelectedMapId == "map_04")
                        return "남아 있는 적을 모두 제거하라";

                    // 일반 소탕 미션의 목표 문구
                    return "지역 내 적을 모두 제거하라";


                // -------------------------------------------------
                // 11-2. 자료 탈취 미션
                // -------------------------------------------------

                case "steal":

                    // Stolen은 자료 확보 여부를 나타내는 상태다.
                    // 자료를 확보하면 완료 문구를 표시하고,
                    // 아직 확보하지 못했다면 확보 목표를 표시한다.
                    return mission.Stolen
                        ? "기밀 자료 확보 완료"
                        : "기밀 자료를 확보하라";


                // -------------------------------------------------
                // 11-3. 자료 탈취 후 탈출 미션
                // -------------------------------------------------

                case "steal_exit":

                    // 자료 확보 전에는 자료 확보와 탈출을 목표로 안내한다.
                    // 자료를 확보한 뒤에는 다음 단계인 탈출 지점 이동을 안내한다.
                    return mission.Stolen
                        ? "탈출 지점으로 이동하라"
                        : "자료를 확보하고 탈출 지점으로 이동하라";


                // -------------------------------------------------
                // 11-4. 폭탄 해체 미션
                // -------------------------------------------------

                case "bomb":

                    // Objective.Done을 확인해 폭탄 해체 완료 여부를 판단한다.
                    // 완료된 경우 완료 문구를 표시하고,
                    // 아직 진행 중이라면 해체 목표를 표시한다.
                    return mission.Objective != null &&
                           mission.Objective.Done
                        ? "폭탄 해체 완료"
                        : "폭탄을 해체하라";


                // -------------------------------------------------
                // 11-5. 그 외 미션
                // -------------------------------------------------

                default:

                    // 별도로 지정하지 않은 미션은
                    // MissionManager가 제공하는 기본 목표 문구를 사용한다.
                    return mission.ObjectiveText;
            }
        }
    }
}
