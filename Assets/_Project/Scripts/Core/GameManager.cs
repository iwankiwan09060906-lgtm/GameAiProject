using System.Collections.Generic;
using UnityEngine;

namespace GameAi
{
    // 게임의 중심. 설정을 읽고 세계를 만든 뒤, 매 프레임 정해진 순서로 각 시스템의 Tick을 부른다(5.4).
    // 각 시스템은 자기 Update를 쓰지 않고 Tick(dt)만 노출한다. 순서가 곧 규칙이라 한곳에서 정한다.
    public sealed class GameManager : MonoBehaviour
    {
        // 한 프레임의 시간 상한(초). 길게 멈췄다 돌아와도 캐릭터가 벽을 뚫고 튀지 않게 한다
        const float MaxDeltaTime = 0.05f;

        // ProjectBuilder가 씬에 연결해 주는 값
        [SerializeField] AssetCatalog catalog;
        [SerializeField] Camera worldCamera;

        // 총알·근접이 찾는 대상 목록(플레이어 + 적). 위치는 각자가 매 프레임 갱신한다
        readonly List<CombatTarget> targets = new List<CombatTarget>();
        readonly List<EnemyController> enemies = new List<EnemyController>();
        readonly List<Door> doors = new List<Door>();
        readonly PerfProbe perf = new PerfProbe();

        // 설정 파일(StreamingAssets/Config)에서 읽은 값
        GameConfig config;
        WeaponConfig weaponConfig;
        EnemyConfig enemyConfig;
        AiConfig aiConfig;

        // 맵. mapData는 JSON 그대로, map은 격자로 바꾼 것
        ParsedMap map;
        MapData mapData;

        // 세계가 다 만들어졌는지. 실패하면 Update가 아무것도 하지 않는다
        bool ready;
        bool paused;
        bool inDialogue;

        IInputSource inputSource = new LegacyInputSource();
        PlayerInputState input;
        WeaponController weapons;
        MeleeTakedown melee;
        InteractionSystem interaction;
        PlayerVision vision;
        FogOfWar fog;
        VisionRenderer fogRenderer;
        CombatFx fx;
        CameraFollow cameraFollow;
        Crosshair crosshair;
        InteractPrompt prompt;
        FilmGrain filmGrain;
        PerfOverlay perfOverlay;

        // 값이 있으면 자동 재생(SmokeRunner)이 조준선을 직접 움직인다
        Vector2? crosshairOverride;

        public PlayerController Player { get; private set; }
        public IReadOnlyList<EnemyController> Enemies { get { return enemies; } }
        public IReadOnlyList<Door> Doors { get { return doors; } }
        public GridMap Grid { get { return map.Grid; } }
        public WeaponController Weapons { get { return weapons; } }
        public CombatFx Fx { get { return fx; } }
        public FogOfWar Fog { get { return fog; } }
        public PlayerVision Vision { get { return vision; } }
        public InteractPrompt Prompt { get { return prompt; } }
        public Camera WorldCamera { get { return worldCamera; } }
        public PerfProbe Perf { get { return perf; } }
        public bool IsReady { get { return ready; } }

        // 자동 재생과 입력 재생(13.3)이 입력원을 바꿔 끼운다
        public void SetInputSource(IInputSource source, bool overrideCrosshair)
        {
            inputSource = source;
            crosshairOverride = overrideCrosshair ? Vector2.zero : (Vector2?)null;
        }

        void Awake()
        {
            // 카탈로그가 없어도(Tools/Build Project 전) 코드 생성 에셋으로 돌아가 실행된다
            if (catalog == null)
            {
                Debug.LogWarning("[GameManager] 에셋 카탈로그가 없어 코드 생성 에셋을 사용합니다. Tools/Build Project를 실행하세요.");
                catalog = ScriptableObject.CreateInstance<AssetCatalog>();
            }
            if (worldCamera == null) worldCamera = Camera.main;

            config = ConfigLoader.Load<GameConfig>("game.json");
            weaponConfig = ConfigLoader.Load<WeaponConfig>("weapons.json");
            enemyConfig = ConfigLoader.Load<EnemyConfig>("enemies.json");
            aiConfig = ConfigLoader.Load<AiConfig>("ai.json");
            Rng.Init(config.fixed_seed_enabled, config.fixed_seed);
            ApplyFramePacing();

            if (!LoadMap(SceneFlow.SelectedMapId)) return;
            BuildWorld();
            ready = true;
        }

        // 수직동기를 켜서 모니터 주사율에 프레임을 맞춘다. 제한 없이 그리면 노트북이 뜨거워져
        // 클럭이 오르내리고 프레임 간격이 들쭉날쭉해져 끊겨 보인다.
        // 에디터는 수직동기를 Game 뷰 설정이 정하므로, 꺼져 있어도 주사율로 상한을 둔다.
        void ApplyFramePacing()
        {
            QualitySettings.vSyncCount = config.vsync_count;
#if UNITY_EDITOR
            if (config.vsync_count > 0)
                Application.targetFrameRate = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value);
#endif
        }

        // 구독은 OnEnable, 해제는 OnDisable(5.1)
        void OnEnable()
        {
            EventBus.Subscribe<DialogueStateEvent>(OnDialogueState);
            EventBus.Subscribe<PauseEvent>(OnPause);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<DialogueStateEvent>(OnDialogueState);
            EventBus.Unsubscribe<PauseEvent>(OnPause);
        }

        // 대화 중에는 Time.timeScale을 건드리지 않고 시뮬레이션 단계만 건너뛴다(7.19)
        void OnDialogueState(DialogueStateEvent evt)
        {
            inDialogue = evt.isActive;
        }

        void OnPause(PauseEvent evt)
        {
            paused = evt.isPaused;
        }

        // missions.json에서 맵 파일을 찾아 읽고 격자로 바꾼다. 오류가 있으면 false
        bool LoadMap(string mapId)
        {
            var missions = ConfigLoader.Load<MissionConfig>("missions.json");
            string file = MapRegistry.FindFile(missions, mapId);
            string json = file != null ? ConfigLoader.ReadText(file) : null;
            if (json == null)
            {
                Debug.LogError("[GameManager] 맵 파일을 찾을 수 없습니다: " + mapId);
                return false;
            }

            mapData = MapJson.Parse(json, file);
            var errors = new List<string>();
            map = MapParser.Parse(mapData, errors);
            foreach (string error in errors)
                Debug.LogError("[Map] " + error);
            return map != null && errors.Count == 0;
        }

        // 맵 데이터로 화면에 보이는 모든 것을 코드로 조립한다(프리팹 없음)
        void BuildWorld()
        {
            var world = new GameObject("World").transform;
            world.SetParent(transform, false);

            new MapLoader(catalog).Build(map, mapData, config, world);
            var lighting = new MapLighting(catalog, config.lighting, world);
            lighting.Build(map, mapData);

            var fxObject = new GameObject("CombatFx");
            fxObject.transform.SetParent(world, false);
            fx = fxObject.AddComponent<CombatFx>();
            fx.Init(catalog, config);

            SpawnPlayer(world);
            SpawnEnemies(world);
            SpawnDoors(world, lighting);

            // 문을 상호작용 대상으로 등록한다(Space로 열고 닫기)
            interaction = new InteractionSystem(map.Grid, config.interact_range_m);
            foreach (Door door in doors)
                interaction.Add(door);

            melee = new MeleeTakedown(map.Grid, weaponConfig.melee, aiConfig.gauge.detect, enemies);
            weapons = new WeaponController(map.Grid, targets, Player.View, catalog, weaponConfig, EntityIds.Player);

            vision = new PlayerVision(map.Grid, config.player.fov_deg, config.player.view_m, config.player.vision_rays);
            fog = new FogOfWar(map.Grid);
            fogRenderer = new VisionRenderer(map.Grid, fog, config.fog, catalog, world);

            cameraFollow = new CameraFollow(worldCamera.transform, config);

            // 화면 위에 덮는 UI. 아래쪽에 만든 것일수록 위에 그려진다
            var ui = new UiRoot(config, transform);
            filmGrain = new FilmGrain(ui, config);
            prompt = new InteractPrompt(ui, catalog, worldCamera);
            var crosshairObject = UiRoot.CreateRect("Crosshair", ui.Rect);
            crosshair = crosshairObject.gameObject.AddComponent<Crosshair>();
            crosshair.Init(ui, catalog, config.crosshair);
            perfOverlay = new PerfOverlay(ui, catalog, perf);
        }

        // 플레이어: 맵의 P 칸에서 시작한다. id 0은 항상 플레이어
        void SpawnPlayer(Transform world)
        {
            var target = new CombatTarget
            {
                Id = EntityIds.Player,
                Team = Team.Player,
                Position = map.PlayerStart,
                Radius = config.player.radius_m,
            };
            targets.Add(target);

            var go = new GameObject("Player");
            go.transform.SetParent(world, false);
            var view = go.AddComponent<CharacterView>();
            view.Init("player", catalog, config.view);
            var health = go.AddComponent<Health>();
            health.Init(target, weaponConfig.player.hp, weaponConfig.player.armor, view, config.fx.hit_flash_s, true, "corpse_player");
            Player = go.AddComponent<PlayerController>();
            Player.Init(map.Grid, config.player, targets, target, health, view);
        }

        // 맵 JSON의 enemies를 읽어 스폰 번호에 해당하는 칸에 적을 세운다
        void SpawnEnemies(Transform world)
        {
            int nextId = EntityIds.Player + 1;
            foreach (MapEnemy data in mapData.enemies)
            {
                if (!map.TryGetSpawn(data.spawn, out Vector2 position))
                {
                    Debug.LogError("[Map] 적 스폰 번호를 맵에서 찾을 수 없습니다: " + data.spawn);
                    continue;
                }
                EnemyTypeDef type = enemyConfig.Find(data.type);
                if (type == null)
                {
                    Debug.LogError("[Map] 알 수 없는 적 종류입니다: " + data.type);
                    continue;
                }
                enemies.Add(SpawnEnemy(world, nextId++, type, position, FirstFacing(data, position)));
            }
        }

        // 순찰할 첫 지점을 바라보고 서 있는다. 순찰 지점이 없으면 오른쪽
        static Vector2 FirstFacing(MapEnemy data, Vector2 position)
        {
            foreach (MapPoint point in data.patrol)
            {
                Vector2 delta = point.Position - position;
                if (delta.sqrMagnitude > 0.01f) return delta.normalized;
            }
            return Vector2.right;
        }

        EnemyController SpawnEnemy(Transform world, int id, EnemyTypeDef type, Vector2 position, Vector2 facing)
        {
            var target = new CombatTarget { Id = id, Team = Team.Enemy, Position = position, Radius = config.view.enemy_radius_m };
            targets.Add(target);

            var go = new GameObject("Enemy_" + id + "_" + type.id);
            go.transform.SetParent(world, false);
            go.transform.position = position;
            var view = go.AddComponent<CharacterView>();
            view.Init(type.sprite, catalog, config.view);

            // 경비병은 권총, 소총병은 소총을 든다
            view.SetHeld(catalog.GetSprite(type.id == "rifleman" ? "held_rifle" : "held_pistol"));
            var health = go.AddComponent<Health>();
            health.Init(target, type.hp, 0, view, config.fx.hit_flash_s, false, "corpse_" + type.sprite);
            var enemy = go.AddComponent<EnemyController>();
            enemy.Init(target, health, view, facing);
            return enemy;
        }

        // 맵의 D 칸마다 문 하나. 문 그림 하나와 그림자 상자 하나를 달아 준다
        void SpawnDoors(Transform world, MapLighting lighting)
        {
            Sprite horizontal = catalog.GetSprite("door_closed");
            Sprite vertical = catalog.GetSprite("door_open");
            int id = 0;
            foreach (Vector2Int cell in map.Doors)
            {
                var go = new GameObject("Door_" + id);
                go.transform.SetParent(world, false);
                var renderer = new GameObject("Bar", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                renderer.transform.SetParent(go.transform, false);
                renderer.sharedMaterial = catalog.SpriteLit;
                renderer.sortingOrder = SortOrder.Door;

                // 닫힌 문만 빛을 막으므로, 그림자 상자는 문을 열면 끈다(Door.Interact)
                var caster = lighting.CreateShadowBox(new RectInt(cell.x, cell.y, 1, 1), go.transform);
                var door = go.AddComponent<Door>();
                door.Init(id++, cell, map.Grid, targets, renderer, caster, horizontal, vertical);
                doors.Add(door);
            }
        }

        void Update()
        {
            if (!ready) return;

            // 직전 프레임 전체 시간(렌더링과 수직동기 대기 포함)을 기록하고, 이번 프레임의 게임 코드 시간을 잰다
            perf.Record(Time.unscaledDeltaTime);
            perf.BeginSection();

            float dt = Mathf.Min(Time.deltaTime, MaxDeltaTime);

            // 대화·일시정지 중에는 1~8단계(시뮬레이션)를 건너뛴다. 화면 갱신은 계속한다
            if (!paused && !inDialogue)
                TickSimulation(dt);

            TickPresentation(dt);

            perf.EndSection();
            perfOverlay.Tick(Time.unscaledDeltaTime);
        }

        // 5.4의 1~8단계. 순서를 바꾸면 규칙이 바뀐다
        void TickSimulation(float dt)
        {
            bool playerAlive = Player.Target.Alive;

            // 1. 입력 읽기
            input = inputSource.Read(worldCamera);

            // 2. 플레이어 이동과 상호작용(죽었으면 입력을 막는다)
            if (playerAlive)
            {
                Player.TickMove(dt, input);
                interaction.Tick(Player.Position, input.InteractPressed, EntityIds.Player);
            }

            // 3. 소음은 Phase 4, 4~5. 감지·상태 전환은 Phase 5. 지금은 허수아비라 표시만 갱신한다
            foreach (EnemyController enemy in enemies)
                enemy.Tick(dt);

            // 6. 공격·피해. 근접을 먼저 처리해 같은 프레임의 사격보다 우선한다
            if (playerAlive)
            {
                melee.Tick(input.MeleePressed, Player.Position, Player.View);
                weapons.Tick(dt, input, Player.Position, Player.AimDir);
            }

            // 7. 사망 확정. 피해 이벤트로 체력이 0이 된 대상에게 DeathEvent를 한 번만 보낸다
            Player.Health.Tick();
            foreach (EnemyController enemy in enemies)
                enemy.Health.Tick();

            // 8. 미션 판정은 Phase 6
        }

        // 5.4의 9단계. 시야·조명·전투 흔적·UI를 갱신한다
        void TickPresentation(float dt)
        {
            Vector2 position = Player.Position;
            cameraFollow.Tick(position, input.AimWorld);

            // 시야를 다시 계산하고 탐색 기억과 안개 그림에 반영한다
            vision.Compute(position, Player.AimDir);
            fog.Update(vision);
            fogRenderer.Refresh();

            // 시야 밖 문은 마지막으로 본 모습을 유지하고, 시야 밖 적은 그리지 않는다
            foreach (Door door in doors)
                door.RefreshView(vision.IsVisible(door.Cell.x, door.Cell.y));
            foreach (EnemyController enemy in enemies)
                if (enemy.Target.Alive) enemy.View.SetVisible(vision.IsVisible(enemy.Target.Position));

            Player.TickView(dt);
            fx.Tick(dt);
            filmGrain.Tick();
            prompt.Tick(paused || inDialogue ? null : interaction.Current);

            // 자동 재생 중에는 입력의 월드 좌표를 화면 좌표로 바꿔 조준선을 움직인다
            Vector2? screen = crosshairOverride.HasValue ? (Vector2)worldCamera.WorldToScreenPoint(input.AimWorld) : (Vector2?)null;
            crosshair.Tick(dt, Player.Mode, Player.IsMoving, screen);
        }
    }
}
