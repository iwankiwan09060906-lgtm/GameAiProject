using System.Collections.Generic; // List, IReadOnlyList 등 제네릭 컬렉션을 사용하기 위한 네임스페이스입니다.
using UnityEngine; // MonoBehaviour, GameObject, Vector2 등 유니티 엔진의 핵심 기능을 사용하기 위한 네임스페이스입니다.

namespace GameAi // 게임의 AI 및 핵심 로직들을 묶어둔 네임스페이스입니다.
{
    // 게임의 중심. 설정을 읽고 세계를 만든 뒤, 매 프레임 정해진 순서로 각 시스템의 Tick을 부른다(5.4).
    // 각 시스템은 자기 Update를 쓰지 않고 Tick(dt)만 노출한다. 순서가 곧 규칙이라 한곳에서 정한다.
    public sealed class GameManager : MonoBehaviour // 더 이상 상속될 수 없는(sealed) 게임 관리자 클래스입니다. 유니티 게임 오브젝트에 붙어 동작합니다.
    {
        // 한 프레임의 시간 상한(초). 길게 멈췄다 돌아와도 캐릭터가 벽을 뚫고 튀지 않게 한다
        const float MaxDeltaTime = 0.05f; // 게임이 멈췄다 풀릴 때(랙 발생 시) 델타 타임이 너무 커져서 물리 연산 오류가 생기는 것을 막기 위한 최대 시간값(초)입니다.

        // ProjectBuilder가 씬에 연결해 주는 값
        [SerializeField] AssetCatalog catalog; // 유니티 에디터의 인스펙터에서 할당하는 에셋 목록(스프라이트, 프리팹 등)입니다.
        [SerializeField] Camera worldCamera; // 게임 세계를 비추는 메인 카메라의 참조입니다.

        // 총알·근접이 찾는 대상 목록(플레이어 + 적). 위치는 각자가 매 프레임 갱신한다
        readonly List<CombatTarget> targets = new List<CombatTarget>(); // 공격 판정의 대상이 되는 모든 엔티티(플레이어와 적)의 리스트입니다.
        readonly List<EnemyController> enemies = new List<EnemyController>(); // 맵에 존재하는 모든 적들의 컨트롤러 리스트입니다.
        readonly List<Door> doors = new List<Door>(); // 맵에 존재하는 모든 문(Door) 객체들의 리스트입니다.
        readonly PerfProbe perf = new PerfProbe(); // 게임의 성능(프레임 타임 등)을 측정하기 위한 프로파일링 도구입니다.

        // 설정 파일(StreamingAssets/Config)에서 읽은 값
        GameConfig config; // 게임의 전반적인 설정(시스템, UI 등)을 담고 있는 객체입니다.
        WeaponConfig weaponConfig; // 무기와 관련된 수치 설정들을 담고 있는 객체입니다.
        EnemyConfig enemyConfig; // 적의 스탯, 종류 등 설정 데이터를 담고 있는 객체입니다.
        AiConfig aiConfig; // AI의 감지 게이지, 행동 패턴 수치 등을 담고 있는 객체입니다.

        // 맵. mapData는 JSON 그대로, map은 격자로 바꾼 것
        ParsedMap map; // JSON 데이터를 바탕으로 게임 내 논리적 격자(Grid)로 변환된 맵 객체입니다.
        MapData mapData; // JSON 파일에서 막 읽어 들인 원본 맵 데이터 구조체입니다.

        // 세계가 다 만들어졌는지. 실패하면 Update가 아무것도 하지 않는다
        bool ready; // 게임 초기화(맵 로드 등)가 완전히 끝났는지 나타내는 플래그입니다.
        bool paused; // 게임이 일시정지 상태인지 나타내는 플래그입니다.
        bool inDialogue; // 대화 이벤트가 진행 중인지 나타내는 플래그입니다.

        IInputSource inputSource = new LegacyInputSource(); // 플레이어 입력을 받는 인터페이스입니다. 기본값으로 기존 입력 방식을 씁니다.
        PlayerInputState input; // 현재 프레임의 입력 상태(이동 방향, 조준, 버튼 눌림 등)를 저장하는 구조체입니다.
        WeaponController weapons; // 무기 발사, 재장전 등 전투 로직을 총괄하는 컨트롤러입니다.
        MeleeTakedown melee; // 근접 공격 및 암살(테이크다운) 로직을 처리하는 클래스입니다.
        InteractionSystem interaction; // 문 열기, 아이템 줍기 등 상호작용 가능한 객체들을 관리하는 시스템입니다.
        PlayerVision vision; // 플레이어의 시야(Field of View)와 Raycast를 계산하는 클래스입니다.
        FogOfWar fog; // 시야 밖의 영역을 어둡게 가리는 전장의 안개 논리 객체입니다.
        VisionRenderer fogRenderer; // 전장의 안개를 화면에 실제로 그려주는 렌더러 객체입니다.
        CombatFx fx; // 전투 시 발생하는 특수효과(파티클, 혈흔 등)를 관리하는 객체입니다.
        CameraFollow cameraFollow; // 카메라가 플레이어나 특정 대상을 부드럽게 따라가도록 제어하는 객체입니다.
        Crosshair crosshair; // 마우스 커서 위치에 그려지는 조준선 UI 객체입니다.
        InteractPrompt prompt; // 상호작용이 가능할 때 화면에 띄워주는 안내 문구(예: "Space로 열기") UI입니다.
        FilmGrain filmGrain; // 화면에 영화 같은 노이즈 효과를 주는 후처리 객체입니다.
        PerfOverlay perfOverlay; // 화면 구석에 성능 지표(FPS 등)를 띄워주는 UI 객체입니다.
        PlayerNoise playerNoise;
        CanSystem cans;
        EventTypeTable eventTable;
        AStarPathfinder nav;
        AiDirector director;
        BarkPicker barks;
        BarkView barkView;
        BarkConfig barkConfig;
        float difficultyMult = 1f;

        // 값이 있으면 자동 재생(SmokeRunner)이 조준선을 직접 움직인다
        Vector2? crosshairOverride; // null이 아니면 플레이어 마우스 대신 시스템(또는 리플레이)이 조준선 위치를 강제합니다.

        // 외부에서 읽기 전용으로 접근할 수 있도록 열어둔 프로퍼티들입니다.
        public PlayerController Player { get; private set; } // 생성된 플레이어 컨트롤러를 반환합니다. 할당은 내부에서만 가능합니다.
        public IReadOnlyList<EnemyController> Enemies { get { return enemies; } } // 적 리스트를 읽기 전용으로 반환합니다.
        public IReadOnlyList<Door> Doors { get { return doors; } } // 문 리스트를 읽기 전용으로 반환합니다.
        public GridMap Grid { get { return map.Grid; } } // 맵의 타일 격자망 데이터를 반환합니다.
        public WeaponController Weapons { get { return weapons; } } // 무기 컨트롤러를 반환합니다.
        public CombatFx Fx { get { return fx; } } // 특수효과 관리자를 반환합니다.
        public FogOfWar Fog { get { return fog; } } // 전장의 안개 관리자를 반환합니다.
        public PlayerVision Vision { get { return vision; } } // 플레이어 시야 관리자를 반환합니다.
        public InteractPrompt Prompt { get { return prompt; } } // 상호작용 UI를 반환합니다.
        public Camera WorldCamera { get { return worldCamera; } } // 월드 렌더링 카메라를 반환합니다.
        public PerfProbe Perf { get { return perf; } } // 성능 측정기를 반환합니다.
        public bool IsReady { get { return ready; } } // 게임 준비 완료 여부를 반환합니다.

        // 자동 재생과 입력 재생(13.3)이 입력원을 바꿔 끼운다
        public void SetInputSource(IInputSource source, bool overrideCrosshair) // 플레이어의 실제 키보드/마우스 입력 대신 가짜(리플레이용) 입력을 주입할 때 사용합니다.
        {
            inputSource = source; // 새로운 입력 소스를 덮어씌웁니다.
            crosshairOverride = overrideCrosshair ? Vector2.zero : (Vector2?)null; // 조준선 오버라이드 여부에 따라 조준선 강제 제어 변수를 설정합니다.
        }

        void Awake() // 유니티 스크립트 생명주기: 게임오브젝트가 활성화될 때 가장 먼저 딱 한 번 불립니다.
        {
            // 카탈로그가 없어도(Tools/Build Project 전) 코드 생성 에셋으로 돌아가 실행된다
            if (catalog == null) // 인스펙터에서 에셋 카탈로그가 연결되지 않았다면
            {
                Debug.LogWarning("[GameManager] 에셋 카탈로그가 없어 코드 생성 에셋을 사용합니다. Tools/Build Project를 실행하세요."); // 경고 로그를 출력합니다.
                catalog = ScriptableObject.CreateInstance<AssetCatalog>(); // 비어있는 가짜 카탈로그 객체를 생성하여 에러를 방지합니다.
            }
            if (worldCamera == null) worldCamera = Camera.main; // 카메라가 연결되지 않았다면 씬의 메인 카메라를 찾아 자동으로 연결합니다.

            config = ConfigLoader.Load<GameConfig>("game.json"); // StreamingAssets 등에서 game.json 설정 파일을 읽어 객체로 만듭니다.
            weaponConfig = ConfigLoader.Load<WeaponConfig>("weapons.json"); // 무기 설정 파일을 읽습니다.
            enemyConfig = ConfigLoader.Load<EnemyConfig>("enemies.json"); // 적 설정 파일을 읽습니다.
            aiConfig = ConfigLoader.Load<AiConfig>("ai.json"); // AI 설정 파일을 읽습니다.
            Rng.Init(config.fixed_seed_enabled, config.fixed_seed); // 설정에 따라 랜덤(난수) 생성기의 시드를 고정하여 리플레이가 똑같이 재생되게 하거나 랜덤하게 만듭니다.
            GameClock.Reset();
            eventTable = new EventTypeTable(ConfigLoader.Load<EventTypeConfig>("events.json"));
            WorldEvents.Reset(eventTable);
            barkConfig = ConfigLoader.Load<BarkConfig>("barks.json");
            difficultyMult = ConfigLoader.Load<DifficultyConfig>("difficulty.json").MultFor(SceneFlow.SelectedDifficulty);
            ApplyFramePacing(); // 프레임 속도 제한 및 수직동기를 적용하는 함수를 호출합니다.

            if (!LoadMap(SceneFlow.SelectedMapId)) return; // 선택된 맵 데이터를 불러옵니다. 실패하면 이 밑의 초기화는 진행하지 않습니다(return).
            BuildWorld(); // 성공적으로 맵을 불러왔다면, 실제 게임 오브젝트들과 시스템들을 생성하고 조립합니다.
            ready = true; // 모든 준비가 끝났으므로 ready 플래그를 켭니다. 이제 Update가 작동합니다.
        }

        // 수직동기를 켜서 모니터 주사율에 프레임을 맞춘다. 제한 없이 그리면 노트북이 뜨거워져
        // 클럭이 오르내리고 프레임 간격이 들쭉날쭉해져 끊겨 보인다.
        // 에디터는 수직동기를 Game 뷰 설정이 정하므로, 꺼져 있어도 주사율로 상한을 둔다.
        void ApplyFramePacing() // 프레임 속도를 관리하는 함수입니다.
        {
            QualitySettings.vSyncCount = config.vsync_count; // 설정값에 따라 유니티 엔진의 수직동기화(VSync)를 켭니다(1) 거나 끕니다(0).
#if UNITY_EDITOR // 유니티 에디터 환경에서만 실행되는 조건부 컴파일입니다. 빌드된 게임에선 빠집니다.
            if (config.vsync_count > 0) // 에디터 환경이면서 수직동기가 켜져 있어야 한다면
                Application.targetFrameRate = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value); // 에디터상에서는 주사율에 맞춰 강제로 최대 프레임을 제한합니다.
#endif
        }

        // 구독은 OnEnable, 해제는 OnDisable(5.1)
        void OnEnable() // 스크립트가 켜질 때 호출됩니다. 이벤트 리스너를 등록합니다.
        {
            EventBus.Subscribe<DialogueStateEvent>(OnDialogueState); // 대화창 상태 변경 이벤트를 구독하여 OnDialogueState 함수가 반응하게 합니다.
            EventBus.Subscribe<PauseEvent>(OnPause); // 일시정지 이벤트를 구독하여 OnPause 함수가 반응하게 합니다.
        }

        void OnDisable() // 스크립트가 꺼지거나 파괴될 때 호출됩니다.
        {
            EventBus.Unsubscribe<DialogueStateEvent>(OnDialogueState); // 켜질 때 등록했던 대화 이벤트를 해제합니다(메모리 누수 방지).
            EventBus.Unsubscribe<PauseEvent>(OnPause); // 일시정지 이벤트 구독을 해제합니다.
        }

        // 대화 중에는 Time.timeScale을 건드리지 않고 시뮬레이션 단계만 건너뛴다(7.19)
        void OnDialogueState(DialogueStateEvent evt) // 대화 이벤트 발생 시 불리는 콜백 함수입니다.
        {
            inDialogue = evt.isActive; // 대화창이 열렸는지(true), 닫혔는지(false) 상태를 변수에 저장합니다.
        }

        void OnPause(PauseEvent evt) // 일시정지 이벤트 발생 시 불리는 콜백 함수입니다.
        {
            paused = evt.isPaused; // 게임이 멈췄는지 상태를 변수에 저장합니다.
        }

        // missions.json에서 맵 파일을 찾아 읽고 격자로 바꾼다. 오류가 있으면 false
        bool LoadMap(string mapId) // 선택된 맵 ID를 바탕으로 맵을 메모리에 로드하는 함수입니다.
        {
            var missions = ConfigLoader.Load<MissionConfig>("missions.json"); // 미션 목록이 담긴 JSON을 읽어옵니다.
            string file = MapRegistry.FindFile(missions, mapId); // 미션 목록 안에서 mapId와 일치하는 실제 맵 파일의 경로를 찾습니다.
            string json = file != null ? ConfigLoader.ReadText(file) : null; // 파일 경로가 존재하면 해당 맵의 JSON 텍스트를 읽어옵니다.
            if (json == null) // 파일을 읽지 못했다면
            {
                Debug.LogError("[GameManager] 맵 파일을 찾을 수 없습니다: " + mapId); // 에러 로그를 남깁니다.
                return false; // 로드 실패를 알리고 돌아갑니다.
            }

            mapData = MapJson.Parse(json, file); // 맵 JSON 문자열을 분석(Parse)하여 구조체(MapData)로 바꿉니다.
            var errors = new List<string>(); // 맵 변환 중 생길 에러들을 담을 빈 리스트를 만듭니다.
            map = MapParser.Parse(mapData, errors); // 맵 데이터를 분석해 논리적인 타일 기반 격자 맵(ParsedMap)으로 컴파일합니다.
            foreach (string error in errors) // 위 과정에서 담긴 에러가 있다면 순회하면서
                Debug.LogError("[Map] " + error); // 콘솔에 전부 출력합니다.
            return map != null && errors.Count == 0; // 맵 객체가 정상 생성되었고 에러가 하나도 없을 때만 true(성공)를 반환합니다.
        }

        // 맵 데이터로 화면에 보이는 모든 것을 코드로 조립한다(프리팹 없음)
        void BuildWorld() // 로드된 맵 데이터를 바탕으로 실제 게임 내 오브젝트와 시스템을 생성하는 함수입니다.
        {
            var world = new GameObject("World").transform; // "World"라는 빈 부모 게임오브젝트를 만듭니다.
            world.SetParent(transform, false); // GameManager가 붙은 오브젝트의 자식으로 "World"를 집어넣어 계층 구조를 정리합니다.

            new MapLoader(catalog).Build(map, mapData, config, world); // 벽, 바닥 등 맵의 정적 환경 오브젝트들을 생성해 World 아래 배치합니다.
            var lighting = new MapLighting(catalog, config.lighting, world); // 맵의 조명 시스템 객체를 만듭니다.
            lighting.Build(map, mapData); // 조명과 그림자 데이터를 계산하고 생성합니다.

            var fxObject = new GameObject("CombatFx"); // 전투 이펙트를 관리할 빈 게임오브젝트를 만듭니다.
            fxObject.transform.SetParent(world, false); // 이것도 World 자식으로 넣습니다.
            fx = fxObject.AddComponent<CombatFx>(); // CombatFx 컴포넌트를 붙입니다.
            fx.Init(catalog, config); // 이펙트 관련 에셋과 설정을 초기화합니다.

            SpawnPlayer(world); // 플레이어 캐릭터를 맵에 생성합니다.
            nav = new AStarPathfinder(map.Grid, config.view.enemy_radius_m);
            director = gameObject.AddComponent<AiDirector>();
            director.Init(map.Grid, nav, aiConfig, enemyConfig.move, eventTable, targets, doors, Player, difficultyMult);
            SpawnEnemies(world); // 적 캐릭터들을 맵에 생성합니다.
            SpawnDoors(world, lighting); // 문 객체들을 맵에 생성하고 조명 시스템과 연결합니다.

            // 문을 상호작용 대상으로 등록한다(Space로 열고 닫기)
            interaction = new InteractionSystem(map.Grid, config.interact_range_m); // 상호작용 시스템을 초기화합니다 (상호작용 거리 설정 적용).
            foreach (Door door in doors) // 방금 생성한 모든 문들을 돌면서
                interaction.Add(door); // 상호작용 시스템에 등록하여 플레이어가 접근 시 인식할 수 있게 합니다.

            melee = new MeleeTakedown(map.Grid, weaponConfig.melee, aiConfig.gauge.detect, enemies); // 근접 공격 시스템을 초기화합니다.
            weapons = new WeaponController(map.Grid, targets, Player.View, catalog, weaponConfig, EntityIds.Player); // 무기 시스템을 초기화합니다 (아군/적 피격 판정 대상 공유).

            vision = new PlayerVision(map.Grid, config.player.fov_deg, config.player.view_m, config.player.vision_rays); // 플레이어 시야(FOV 각도, 거리, 레이캐스트 수) 시스템 초기화.
            fog = new FogOfWar(map.Grid); // 전장의 안개 데이터를 담을 시스템 초기화.
            fogRenderer = new VisionRenderer(map.Grid, fog, config.fog, config.player, catalog, world); // 전장의 안개와 부드러운 시야 경계를 화면에 그릴 렌더러 초기화.

            cameraFollow = new CameraFollow(worldCamera.transform, config); // 메인 카메라가 따라다니게 할 컨트롤러 초기화.

            // 화면 위에 덮는 UI. 아래쪽에 만든 것일수록 위에 그려진다
            var ui = new UiRoot(config, transform); // UI들을 담을 루트(Canvas)를 생성합니다.
            filmGrain = new FilmGrain(ui, config); // 필름 노이즈(자글자글한 효과) UI를 올립니다.
            prompt = new InteractPrompt(ui, catalog, worldCamera); // "상호작용 하려면 누르세요" 텍스트 팝업을 올립니다.
            var crosshairObject = UiRoot.CreateRect("Crosshair", ui.Rect); // 조준선을 담을 UI Rect를 만듭니다.
            crosshair = crosshairObject.gameObject.AddComponent<Crosshair>(); // 조준선 스크립트를 붙입니다.
            crosshair.Init(ui, catalog, config.crosshair); // 조준선 에셋과 설정을 초기화합니다.
            perfOverlay = new PerfOverlay(ui, catalog, perf); // 성능(FPS 등) 측정치를 띄우는 UI를 제일 위에 올립니다.
            playerNoise = new PlayerNoise(map.Grid, config.player, aiConfig.hearing.floor_mult);
            cans = new CanSystem(map.Grid, map.Cans, aiConfig.clue.can_cooldown_s);
            SpawnCans(world);
            ApplyUsualOpenDoors();
            barkView = new BarkView(ui, catalog, worldCamera, aiConfig.bark);
            barks = gameObject.AddComponent<BarkPicker>();
            barks.Init(director, barkConfig, eventTable, aiConfig.bark, barkView, IsEnemyOnScreen, () => (Vector2)worldCamera.transform.position, 0.5f, 6f);
        }

        // 플레이어: 맵의 P 칸에서 시작한다. id 0은 항상 플레이어
        void SpawnPlayer(Transform world) // 플레이어 객체를 생성하는 함수입니다.
        {
            var target = new CombatTarget // 피격 판정에 사용될 CombatTarget 구조체를 만듭니다.
            {
                Id = EntityIds.Player, // 고유 ID를 부여합니다 (통상적으로 0번).
                Team = Team.Player, // 팀을 플레이어 측으로 설정합니다.
                Position = map.PlayerStart, // 맵 데이터에 기록된 플레이어 시작 위치를 할당합니다.
                Radius = config.player.radius_m, // 피격 반경(콜라이더 크기 느낌)을 설정합니다.
            };
            targets.Add(target); // 전체 피격 대상 리스트에 플레이어를 추가합니다 (적들이 사격할 때 검색함).

            var go = new GameObject("Player"); // "Player"라는 빈 게임오브젝트를 생성합니다.
            go.transform.SetParent(world, false); // World 아래에 둡니다.
            var view = go.AddComponent<CharacterView>(); // 캐릭터의 스프라이트/애니메이션을 담당할 뷰 컴포넌트를 붙입니다.
            view.Init("player", catalog, config.view); // 플레이어용 스프라이트와 설정으로 뷰를 초기화합니다.
            var health = go.AddComponent<Health>(); // 체력 관리 컴포넌트를 붙입니다.
            health.Init(target, weaponConfig.player.hp, weaponConfig.player.armor, view, config.fx.hit_flash_s, true, "corpse_player"); // 체력, 방어력, 피격 플래시 효과 시간, 사망 시 생성될 시체 스프라이트 등을 세팅합니다.
            Player = go.AddComponent<PlayerController>(); // 최종적으로 플레이어를 조작하는 컨트롤러 스크립트를 붙입니다.
            Player.Init(map.Grid, config.player, targets, target, health, view); // 플레이어 이동, 타겟 데이터, 체력, 뷰를 하나로 묶어 초기화합니다.
        }

        // 맵 JSON의 enemies를 읽어 스폰 번호에 해당하는 칸에 적을 세운다
        void SpawnEnemies(Transform world) // 맵 데이터를 기반으로 다수의 적을 생성하는 함수입니다.
        {
            int nextId = EntityIds.Player + 1; // 플레이어 다음 ID부터 적들에게 순차적으로 부여하기 위한 변수입니다.
            foreach (MapEnemy data in mapData.enemies) // JSON에 정의된 적 스폰 정보 목록을 순회합니다.
            {
                if (!map.TryGetSpawn(data.spawn, out Vector2 position)) // 맵 격자상에서 해당 스폰 번호의 실제 좌표(position)를 찾아오는데 실패하면
                {
                    Debug.LogError("[Map] 적 스폰 번호를 맵에서 찾을 수 없습니다: " + data.spawn); // 에러를 뿜고
                    continue; // 이 적은 건너뛰고 다음 적으로 넘어갑니다.
                }
                EnemyTypeDef type = enemyConfig.Find(data.type); // 적 종류(예: 소총병, 경비병 등)에 해당하는 스탯/설정을 가져옵니다.
                if (type == null) // 해당 적 타입이 설정 파일에 정의되어 있지 않다면
                {
                    Debug.LogError("[Map] 알 수 없는 적 종류입니다: " + data.type); // 에러를 뿜고
                    continue; // 건너뜁니다.
                }
                enemies.Add(SpawnEnemy(world, nextId++, type, position, FirstFacing(data, position), data));
            }
        }

        // 순찰할 첫 지점을 바라보고 서 있는다. 순찰 지점이 없으면 오른쪽
        static Vector2 FirstFacing(MapEnemy data, Vector2 position) // 적이 처음 스폰될 때 어느 쪽을 바라볼지 결정하는 함수입니다.
        {
            foreach (MapPoint point in data.patrol) // 적에게 할당된 순찰 지점 목록을 순회합니다.
            {
                Vector2 delta = point.Position - position; // 스폰 위치에서 순찰 지점까지의 방향 벡터(거리차)를 구합니다.
                if (delta.sqrMagnitude > 0.01f) return delta.normalized; // 그 거리가 유의미하게 멀다면, 그 방향을 정규화(길이 1의 방향 벡터)하여 반환합니다.
            }
            return Vector2.right; // 순찰 지점이 자기 자리이거나 없다면 기본적으로 오른쪽을 바라보게 합니다.
        }

        EnemyController SpawnEnemy(Transform world, int id, EnemyTypeDef type, Vector2 position, Vector2 facing, MapEnemy data)
        {
            var target = new CombatTarget { Id = id, Team = Team.Enemy, Position = position, Radius = config.view.enemy_radius_m }; // 피격 판정용 적 객체 데이터를 만듭니다.
            targets.Add(target); // 전체 사격 대상 목록에 추가합니다 (플레이어가 쏠 때 판정 대상).

            var go = new GameObject("Enemy_" + id + "_" + type.id); // "Enemy_1_rifleman" 형식으로 이름 지어 오브젝트 생성.
            go.transform.SetParent(world, false); // World 아래로 넣습니다.
            go.transform.position = position; // 실제 게임 화면상의 위치를 지정합니다.
            var view = go.AddComponent<CharacterView>(); // 적 스프라이트를 그릴 뷰 추가.
            view.Init(type.sprite, catalog, config.view); // 적 타입에 맞는 스프라이트로 초기화.

            // 경비병은 권총, 소총병은 소총을 든다
            view.SetHeld(catalog.GetSprite(type.id == "rifleman" ? "held_rifle" : "held_pistol")); // 들고 있는 무기 그래픽을 타입에 맞춰 세팅합니다.
            var health = go.AddComponent<Health>(); // 적 체력 관리 컴포넌트 추가.
            health.Init(target, type.hp, 0, view, config.fx.hit_flash_s, false, "corpse_" + type.sprite); // 적 체력 스탯 및 사망 시 시체 스프라이트 세팅 (방어력은 0).
            var enemy = go.AddComponent<EnemyController>(); // 적 AI 컨트롤러 컴포넌트 추가.
            var patrol = new Vector2[data.patrol.Length];
            for (int i = 0; i < patrol.Length; i++) patrol[i] = data.patrol[i].Position;
            enemy.Init(director, target, health, view, type, facing, patrol);
            enemy.Persona = string.IsNullOrEmpty(data.persona) ? PersonaFor(enemies.Count) : data.persona;
            enemy.Callsign = data.callsign ?? "";
            director.Add(enemy);
            return enemy; // 다 만들어진 적 컨트롤러를 반환합니다.
        }

        // 맵의 이어진 D 칸마다 문 하나. 문 그림 하나와 그림자 상자 하나를 달아 준다
        // 깡통 그림(이동·시야·총알을 막지 않음)
        void SpawnCans(Transform world)
        {
            Sprite sprite = catalog.GetSprite("obj_can");
            for (int i = 0; i < cans.Count; i++)
            {
                var renderer = new GameObject("Can_" + i, typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                renderer.transform.SetParent(world, false);
                renderer.transform.position = map.Grid.CellCenter(cans.CanCell(i));
                renderer.sprite = sprite;
                renderer.sharedMaterial = catalog.SpriteLit;
                renderer.sortingOrder = SortOrder.FloorProp;
            }
        }

        // 맵 open_doors에 적힌 문을 평소 열린 문으로 둔다
        void ApplyUsualOpenDoors()
        {
            if (mapData.open_doors == null) return;
            foreach (MapPoint point in mapData.open_doors)
            {
                Vector2Int cell = map.Grid.WorldToCell(point.Position);
                Door door = doors.Find(d => d.Area.Contains(cell));
                if (door != null) door.SetUsualOpen();
                else Debug.LogWarning("[Map] open_doors 위치에 문이 없습니다: " + point.Position);
            }
        }

        static readonly string[] Personas = { "veteran", "rookie", "grumpy", "cautious" };

        static string PersonaFor(int index) { return Personas[index % Personas.Length]; }

        // 플레이어 화면에 그려지는 적인지(시야 안 + 카메라 안)
        bool IsEnemyOnScreen(int enemyId)
        {
            foreach (EnemyController enemy in enemies)
            {
                if (enemy.Id != enemyId) continue;
                if (!enemy.Target.Alive || !vision.IsVisible(enemy.Position)) return false;
                Vector3 v = worldCamera.WorldToViewportPoint(enemy.Position);
                return v.x >= 0f && v.x <= 1f && v.y >= 0f && v.y <= 1f;
            }
            return false;
        }

        // 머리 위 아이콘: 0 없음, 1 ?(조사·수색·의심), 2 !(추격·교전)
        int AwarenessIcon(EnemyController enemy)
        {
            if (enemy.State == EnemyState.Chase || enemy.State == EnemyState.Engage) return 2;
            if (enemy.State == EnemyState.Investigate || enemy.State == EnemyState.Search || enemy.Gauge >= aiConfig.gauge.suspect) return 1;
            return 0;
        }

        void SpawnDoors(Transform world, MapLighting lighting) // 맵상의 문을 생성하는 함수입니다.
        {
            Sprite horizontal = catalog.GetSprite("door_closed"); // 가로로 닫힌 문 스프라이트.
            Sprite vertical = catalog.GetSprite("door_open"); // 세로로 열린 문 스프라이트.
            int id = 0; // 문 고유 ID.
            foreach (RectInt cell in map.Doors) // 맵 격자 데이터 중 문 하나가 차지하는 칸 묶음(이어진 D 칸)을 순회합니다.
            {
                var go = new GameObject("Door_" + id); // "Door_0" 식으로 문 오브젝트 생성.
                go.transform.SetParent(world, false); // World 자식으로.
                var renderer = new GameObject("Bar", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>(); // 실제 문 그래픽을 그릴 자식 오브젝트와 SpriteRenderer 생성.
                renderer.transform.SetParent(go.transform, false); // 부모인 문 오브젝트 밑으로 넣음.
                renderer.sharedMaterial = catalog.SpriteLit; // 조명을 받을 수 있는 재질(Material)을 할당.
                renderer.sortingOrder = SortOrder.Door; // 다른 타일/오브젝트와 겹칠 때 그리는 순서(Order in Layer) 지정.

                // 닫힌 문만 빛을 막으므로, 그림자 상자는 문을 열면 끈다(Door.Interact)
                var caster = lighting.CreateShadowBox(cell, go.transform); // 문이 차지하는 칸 영역만큼 빛을 가로막는 그림자 캐스터 생성.
                var door = go.AddComponent<Door>(); // 문 작동(열림/닫힘) 로직 컴포넌트 추가.
                door.Init(id++, cell, map.Grid, targets, renderer, caster, horizontal, vertical); // 타일 좌표, 렌더러, 그림자, 열림/닫힘 이미지 정보를 넣어 문 초기화.
                doors.Add(door); // 전체 문 리스트에 추가.
            }
        }

        void Update() // 매 프레임(화면 갱신마다) 호출되는 메인 루프입니다.
        {
            if (!ready) return; // 로드가 끝나지 않았다면 아무것도 안 하고 넘깁니다.

            // 직전 프레임 전체 시간(렌더링과 수직동기 대기 포함)을 기록하고, 이번 프레임의 게임 코드 시간을 잰다
            perf.Record(Time.unscaledDeltaTime); // 프로파일러에 렉을 포함한 쌩 프레임 시간을 기록.
            perf.BeginSection(); // 코드 연산 시간 측정 시작.

            float dt = Mathf.Min(Time.deltaTime, MaxDeltaTime); // 델타 타임(프레임 간 간격)이 비정상적으로 커지는 걸 방지하기 위해 MaxDeltaTime(0.05)으로 상한선을 둡니다.

            // 대화·일시정지 중에는 1~8단계(시뮬레이션)를 건너뛴다. 화면 갱신은 계속한다
            if (!paused && !inDialogue) // 일시정지도 아니고 대화창도 닫혀 있을 때만
                TickSimulation(dt); // 캐릭터 이동, AI 등 실질적인 게임 상태(로직)를 변경(진행)시킵니다.

            TickPresentation(dt); // 카메라 이동, UI, 애니메이션 갱신 등 눈에 보이는 '표현' 부분을 처리합니다 (일시정지 상태여도 시점 이동이나 이펙트 재생을 위해 돌아갑니다).

            perf.EndSection(); // 연산 시간 측정 종료.
            perfOverlay.Tick(Time.unscaledDeltaTime); // 성능 수치 UI 업데이트.
        }

        // 5.4의 1~8단계. 순서를 바꾸면 규칙이 바뀐다
        void TickSimulation(float dt) // 게임 로직의 핵심 파이프라인. 여기서 실행 순서가 곧 이 게임의 물리/판정 규칙입니다.
        {
            bool playerAlive = Player.Target.Alive; // 플레이어가 살아있는지 확인합니다.
            GameClock.Advance(dt);

            // 1. 입력 읽기
            input = inputSource.Read(worldCamera); // 키보드/마우스 상태를 읽어와 구조체에 갱신합니다 (입력을 가장 먼저 처리해야 즉각 반응).

            // 2. 플레이어 이동과 상호작용(죽었으면 입력을 막는다)
            if (playerAlive) // 살아있을 때만
            {
                Player.TickMove(dt, input); // 키 입력에 따라 플레이어의 위치를 움직입니다.
                playerNoise.Tick(dt, Player.IsMoving, Player.Mode, Player.Position);
                cans.Tick(Player.Position, Player.Mode, GameClock.Time);
                interaction.Tick(Player.Position, input.InteractPressed, EntityIds.Player); // 플레이어 위치 주변의 문이나 아이템과 상호작용 버튼(Space) 처리를 합니다.
            }

            // 3. 소음은 Phase 4, 4~5. 감지·상태 전환은 Phase 5. 지금은 허수아비라 표시만 갱신한다
            director.Tick(dt);

            // 6. 공격·피해. 근접을 먼저 처리해 같은 프레임의 사격보다 우선한다
            if (playerAlive) // 공격 처리
            {
                melee.Tick(input.MeleePressed, Player.Position, Player.View); // 근접 공격(테이크다운)을 먼저 처리합니다 (근접과 총격이 동시에 일어나면 근접이 이깁니다).
                weapons.Tick(dt, input, Player.Position, Player.AimDir); // 그 다음 총기 발사(사격 레이캐스트 판정, 탄약 소모 등)를 처리합니다.
            }

            // 7. 사망 확정. 피해 이벤트로 체력이 0이 된 대상에게 DeathEvent를 한 번만 보낸다
            Player.Health.Tick(); // 이번 프레임의 사격/공격 결과로 플레이어 체력이 바닥났는지 체크하고 사망 처리를 합니다.
            foreach (EnemyController enemy in enemies) // 적들의 체력도 마찬가지로 체크합니다.
                enemy.Health.Tick(); // 죽은 적은 타겟에서 제외되거나 시체로 변하는 처리를 합니다.

            // 8. 미션 판정은 Phase 6
            // (주석만 존재하고 현재 코드에는 탈출/클리어 조건 판정 코드가 생략되어 있음)
        }

        // 5.4의 9단계. 시야·조명·전투 흔적·UI를 갱신한다
        void TickPresentation(float dt) // 계산이 다 끝난 게임 데이터를 바탕으로 화면에 그릴 준비를 하는 시각적 연산 파이프라인입니다.
        {
            Vector2 position = Player.Position; // 시점을 맞추기 위해 플레이어 최종 위치를 가져옵니다.
            cameraFollow.Tick(position, input.AimWorld); // 메인 카메라가 플레이어 위치와 마우스 조준점 사이를 부드럽게 추적하도록 위치를 이동합니다.

            // 시야를 다시 계산하고 탐색 기억과 안개 그림에 반영한다
            vision.Compute(position, Player.AimDir); // 벽에 막히는 레이캐스트 연산을 통해 플레이어의 시야(밝은 영역)를 도출합니다.
            fog.Update(vision); // 도출된 시야를 전장의 안개 데이터에 덮어써서 밝힌 곳과 다시 어두워진 곳을 업데이트합니다.
            fogRenderer.Refresh(position, Player.AimDir); // 탐색 기억 텍스처를 갱신하고, 벽에 닿은 레이 끝점으로 만든 시야 메시로 보이는 곳을 부드럽게 뚫습니다.

            // 시야 밖 문은 마지막으로 본 모습을 유지하고, 시야 밖 적은 그리지 않는다
            foreach (Door door in doors) // 문 객체를 순회하며
            {
                door.RefreshView(vision.IsVisible(door.Area)); // 문 칸 중 하나라도 시야에 들어온 문만 목표 상태를 갱신합니다(시야 밖이면 마지막 모습 유지).
                door.Tick(dt); // 열림/닫힘 애니메이션을 진행합니다.
            }
            foreach (EnemyController enemy in enemies) // 적 객체를 순회하며
                if (enemy.Target.Alive) enemy.View.SetVisible(vision.IsVisible(enemy.Target.Position)); // 살아있는 적이 플레이어 시야 안에 있을 때만 화면에 보이도록(SpriteRenderer On) 만듭니다.
            barks.Tick(dt);
            foreach (EnemyController enemy in enemies)
                barkView.Tick(dt, enemy.Id, enemy.Position, IsEnemyOnScreen(enemy.Id), AwarenessIcon(enemy), enemy.Target.Alive);

            Player.TickView(dt); // 플레이어의 스프라이트 애니메이션(걷기, 조준 방향 회전 등)을 갱신합니다.
            fx.Tick(dt); // 피 흘림, 총구 섬광 등 특수효과 파티클의 타이머를 줄이고 갱신합니다.
            filmGrain.Tick(); // 필름 노이즈 이펙트를 갱신합니다(화면이 일렁거리게).
            prompt.Tick(paused || inDialogue ? null : interaction.Current); // 게임이 멈춰있지 않다면, 상호작용 가능한 객체 텍스트(예: "문 열기")를 띄웁니다.

            // 자동 재생 중에는 입력의 월드 좌표를 화면 좌표로 바꿔 조준선을 움직인다
            Vector2? screen = crosshairOverride.HasValue ? (Vector2)worldCamera.WorldToScreenPoint(input.AimWorld) : (Vector2?)null; // 리플레이 모드면 강제 조준점의 월드 좌표를 화면 픽셀 좌표로 변환합니다.
            crosshair.Tick(dt, Player.Mode, Player.IsMoving, screen); // 최종적으로 조준선 UI를 마우스(또는 리플레이 화면) 위치에 그려줍니다.
        }
    }
}