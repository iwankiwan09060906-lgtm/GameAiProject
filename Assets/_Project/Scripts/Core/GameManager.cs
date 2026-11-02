using System.Collections.Generic;
using UnityEngine;

namespace GameAi
{
    // 설정을 읽고 세계를 만든 뒤, 매 프레임 정해진 순서로 각 시스템의 Tick을 부른다(5.4)
    public sealed class GameManager : MonoBehaviour
    {
        const float MaxDeltaTime = 0.05f;

        [SerializeField] AssetCatalog catalog;
        [SerializeField] Camera worldCamera;

        readonly List<CombatTarget> targets = new List<CombatTarget>();
        readonly List<EnemyController> enemies = new List<EnemyController>();
        readonly List<Door> doors = new List<Door>();

        GameConfig config;
        WeaponConfig weaponConfig;
        EnemyConfig enemyConfig;
        AiConfig aiConfig;
        ParsedMap map;
        MapData mapData;
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
        public bool IsReady { get { return ready; } }

        // 스모크 테스트와 입력 재생이 입력원을 바꿔 끼운다
        public void SetInputSource(IInputSource source, bool overrideCrosshair)
        {
            inputSource = source;
            crosshairOverride = overrideCrosshair ? Vector2.zero : (Vector2?)null;
        }

        void Awake()
        {
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

            if (!LoadMap(SceneFlow.SelectedMapId)) return;
            BuildWorld();
            ready = true;
        }

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

        void OnDialogueState(DialogueStateEvent evt)
        {
            inDialogue = evt.isActive;
        }

        void OnPause(PauseEvent evt)
        {
            paused = evt.isPaused;
        }

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

            interaction = new InteractionSystem(map.Grid, config.interact_range_m);
            foreach (Door door in doors)
                interaction.Add(door);

            melee = new MeleeTakedown(map.Grid, weaponConfig.melee, aiConfig.gauge.detect, enemies);
            weapons = new WeaponController(map.Grid, targets, Player.View, catalog, weaponConfig, EntityIds.Player);

            vision = new PlayerVision(map.Grid, config.player.fov_deg, config.player.view_m, config.player.vision_rays);
            fog = new FogOfWar(map.Grid);
            fogRenderer = new VisionRenderer(map.Grid, fog, config.fog, catalog, world);

            cameraFollow = new CameraFollow(worldCamera.transform, config);

            var ui = new UiRoot(config, transform);
            filmGrain = new FilmGrain(ui, config);
            prompt = new InteractPrompt(ui, catalog, worldCamera);
            var crosshairObject = UiRoot.CreateRect("Crosshair", ui.Rect);
            crosshair = crosshairObject.gameObject.AddComponent<Crosshair>();
            crosshair.Init(ui, catalog, config.crosshair);
        }

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

        // 순찰할 첫 지점을 바라보고 서 있는다
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
            view.SetHeld(catalog.GetSprite(type.id == "rifleman" ? "held_rifle" : "held_pistol"));
            var health = go.AddComponent<Health>();
            health.Init(target, type.hp, 0, view, config.fx.hit_flash_s, false, "corpse_" + type.sprite);
            var enemy = go.AddComponent<EnemyController>();
            enemy.Init(target, health, view, facing);
            return enemy;
        }

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

                var caster = lighting.CreateShadowBox(new RectInt(cell.x, cell.y, 1, 1), go.transform);
                var door = go.AddComponent<Door>();
                door.Init(id++, cell, map.Grid, targets, renderer, caster, horizontal, vertical);
                doors.Add(door);
            }
        }

        void Update()
        {
            if (!ready) return;
            float dt = Mathf.Min(Time.deltaTime, MaxDeltaTime);

            if (!paused && !inDialogue)
                TickSimulation(dt);

            TickPresentation(dt);
        }

        void TickSimulation(float dt)
        {
            bool playerAlive = Player.Target.Alive;

            // 1. 입력 읽기
            input = inputSource.Read(worldCamera);

            // 2. 플레이어 이동
            if (playerAlive)
            {
                Player.TickMove(dt, input);
                interaction.Tick(Player.Position, input.InteractPressed, EntityIds.Player);
            }

            // 3. 소음은 Phase 4, 4~5. 감지·상태 전환은 Phase 5
            foreach (EnemyController enemy in enemies)
                enemy.Tick(dt);

            // 6. 공격·피해
            if (playerAlive)
            {
                melee.Tick(input.MeleePressed, Player.Position, Player.View);
                weapons.Tick(dt, input, Player.Position, Player.AimDir);
            }

            // 7. 사망 확정
            Player.Health.Tick();
            foreach (EnemyController enemy in enemies)
                enemy.Health.Tick();

            // 8. 미션 판정은 Phase 6
        }

        void TickPresentation(float dt)
        {
            Vector2 position = Player.Position;
            cameraFollow.Tick(position, input.AimWorld);

            vision.Compute(position, Player.AimDir);
            fog.Update(vision);
            fogRenderer.Refresh();

            foreach (Door door in doors)
                door.RefreshView(vision.IsVisible(door.Cell.x, door.Cell.y));
            foreach (EnemyController enemy in enemies)
                if (enemy.Target.Alive) enemy.View.SetVisible(vision.IsVisible(enemy.Target.Position));

            Player.TickView(dt);
            fx.Tick(dt);
            filmGrain.Tick();
            prompt.Tick(paused || inDialogue ? null : interaction.Current);

            Vector2? screen = crosshairOverride.HasValue ? (Vector2)worldCamera.WorldToScreenPoint(input.AimWorld) : (Vector2?)null;
            crosshair.Tick(dt, Player.Mode, Player.IsMoving, screen);
        }
    }
}
