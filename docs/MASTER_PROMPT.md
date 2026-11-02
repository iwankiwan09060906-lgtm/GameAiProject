# MASTER PROMPT — 2D 탑뷰 전술 잠입 슈팅 게임 (Unity 6)

> 이 문서 전체를 Claude Code에 입력하면, Claude Code는 이 문서만 보고 처음부터 끝까지 게임을 완성한다.
> 문서에 적힌 값은 모두 **확정된 기본값**이다. 적히지 않은 세부 사항은 "합리적인 가장 단순한 방식"으로 스스로 정하고 `docs/DECISIONS.md`에 한 줄씩 기록한다. 사용자에게 되묻지 않는다.
> **목표 화면**은 `docs/reference/`의 3장이다. 화면 요소(색·배치·연출)가 헷갈리면 그 이미지와 `docs/ASSET_DIRECTION.md`를 본다. 외부 에셋의 출처·라이선스·크레딧 문구는 `docs/ASSET_CREDITS.md`가 기준이다.

---

## 0. 작업 지시 (Claude Code에게)

너는 이 프로젝트의 단독 개발자다. 아래 규칙을 지켜 작업한다.

1. 이 문서의 **6장 개발 순서**를 Phase 순서대로 진행한다. Phase 하나가 끝날 때마다 "완료 조건"을 확인하고, 통과하면 git 커밋한다(커밋 메시지는 `phase-N: 요약`).
2. **외부 파일 없이도 게임이 실행되어야 한다.** 외부 에셋은 이미 `Art/`·`Audio/`·`Fonts/`에 들어 있으므로 이 파일을 쓰고, 파일이 빠진 항목은 코드로 만든 임시 에셋(11장)으로 돌아간다.
3. Unity 에디터를 직접 조작할 수 없다고 가정한다. 씬·프리팹·ScriptableObject를 손으로 쓰지 말고, **`Assets/_Project/Editor/ProjectBuilder.cs`** 에 메뉴 항목(`Tools/Build Project`)을 만들어 씬과 프리팹을 코드로 생성한다. URP 2D 렌더러 에셋·Pixel Perfect Camera·전역 2D 라이트 설정도 여기서 만든다. Unity가 설치되어 있으면 `-batchmode -executeMethod`로 실행해 확인한다.
4. 게임 로직은 가능한 한 **MonoBehaviour와 분리된 순수 C# 클래스**로 만들고(격자, A*, 감지 계산, 소리 감쇠, 밝기 격자, 방탄·체력, 승패 판정, 기록 비교), EditMode 테스트를 함께 작성한다.
5. 모든 수치는 코드에 박지 말고 `StreamingAssets/Config/*.json`에서 읽는다(7장 값이 기본값). 파일이 없거나 값이 빠지면 코드 안의 기본값으로 채우고 경고 로그를 남긴다.
6. 코드 주석과 로그는 한국어, 클래스·변수 이름은 영어로 쓴다.
7. 막히는 부분이 있으면 가장 단순한 대안으로 구현하고 `docs/DECISIONS.md`에 이유를 적은 뒤 계속 진행한다. 범위 밖 기능(2.2)은 만들지 않는다.
8. 마지막에는 `README.md`에 실행·조작·빌드 방법을 쓰고, `docs/VERIFY.md`의 검수 체크리스트를 모두 실행해 결과를 기록한다.

---

## 1. 프로젝트 개요

- **장르**: 2D 탑뷰 전술 잠입·슈팅. 플레이어는 혼자 침투해 목표를 수행하고 탈출한다.
- **핵심 재미**: 소리·시야·**빛과 어둠**·문·엄폐를 이용해 적에게 들키지 않고 처리하거나, 들키면 교전한다. 적은 보고 들은 정보에만 반응한다.
- **흐름**: 브리핑 → 침투 → 탐색 → 목표 수행 → 탈출 → 디브리핑 → 결과 화면.
- **플레이 시간**: 한 판 5~10분. 맵 4종.
- **조작 방식**: 키보드(이동·상호작용) + 마우스(조준·사격). 마우스 커서 자리에 꺾쇠 조준선이 그려진다.
- **화면**: 기준 1920×1080(16:9), 픽셀아트 정탑뷰(1m = 32px), 어두운 전역광 + 따뜻한 점광원 조명.

| 목표 화면 | 내용 |
| --- | --- |
| `docs/reference/ref_01_combat.png` | 실내 교전. 조명 대비, 총구 화염·혈흔·시체, 적 머리 위 대사, 꺾쇠 조준선, 좌하단 통합 HUD |
| `docs/reference/ref_02_dialogue.png` | 하단 대화창. 초상화·빨강 이름표·대사·번호 선택지 |
| `docs/reference/ref_03_nightvision.png` | 야간 투시 녹색 필터, 문 앞 `[SPACE] 열기` 안내, 자세 실루엣 |

- **AI**: 학습형 AI 없음. 규칙 기반 FSM + 행동 트리 + 직접 구현한 A*.

## 2. 범위

### 2.1 포함 (구현 대상)
플레이어 이동·조준·사격, 방탄, 무기 3종, 재장전, 유인탄, 근접 제압, 야간 투시, 시야 차폐와 탐색 기억, 2D 조명과 칸별 밝기, 문, 엄폐물, 창문, 전투 흔적(총구 화염·탄피·혈흔·시체), 적 AI(시각·청각·밝기·인지 게이지·FSM·교전·순찰·수색·A*·동료 경보·문 흔적·난이도·통로 충돌), 적 대사, 미션(소탕·자료 탈취·자료 탈취 후 탈출·폭탄 해체), 브리핑 대화, 승패·발각·기록, HUD, 조준선, 상호작용 안내, 메뉴, 설정, 일시정지, 결과 화면, 크레딧 화면, 야외 환경음, 저장·복구, 디버그 오버레이, 고정 시드 재현 모드.

### 2.2 제외 (만들지 않는다)
인질 구출, 섬광탄, 잠금 문·열쇠·문 파괴, 적의 측면 이동, 문 경로 우회 소리 전파, 총알 관통, 적끼리 피해, 피격 경직·밀림·무적 시간, 마우스 감도·키 변경 설정, 창 크기 자유 변경, 네트워크·서버·계정·클라우드, 학습형 AI, 100×100 대형 맵, **대화 분기, 시체 발견 반응, 방탄 획득·회복, 전원 차단(조명 끄기), 음성 대사**.

### 2.3 우선순위
- **M(필수)**: 기말 전에 반드시 완성. **S(권장)**: M 이후. **C(여유)**: 하지 않는다.
- 야간 투시와 폭탄 해체는 S. M이 모두 끝난 뒤 구현한다.

## 3. 기술 스택·환경

| 항목 | 값 |
| --- | --- |
| 엔진 | Unity 6 — **6000.3.24f1** (`ProjectSettings/ProjectVersion.txt` 기준) |
| 플랫폼 | Windows PC 스탠드얼론(64bit), 싱글 플레이 |
| 렌더 | **URP 2D 렌더러**(`com.unity.render-pipelines.universal` 17.3, 설치됨). `Light2D`(Global + Point), 벽 `ShadowCaster2D`. 카메라 Orthographic + **Pixel Perfect Camera**(Assets PPU 32, Reference Resolution 960×540) |
| 스프라이트 | PPU 32, Filter Point, Compression None. Aseprite 임포터 설치됨(`.aseprite` 직접 사용 가능) |
| 언어 | C# |
| 입력 | 레거시 `UnityEngine.Input` 사용. Active Input Handling을 **Both**(`activeInputHandler: 2`)로 설정 |
| 데이터 | JSON (`JsonUtility` + 래퍼 클래스. `Newtonsoft` 쓰지 않음) |
| 길찾기 | **A* 직접 구현** (NavMesh·외부 길찾기 에셋 금지) |
| 물리 | 총알·시야·소리·밝기는 `Physics2D`를 쓰지 않고 **자체 격자 레이캐스트(DDA)** 로 계산한다. 캐릭터 이동 충돌은 격자 기반 또는 `Rigidbody2D`+`CircleCollider2D` 중 단순한 쪽 |
| 테스트 | Unity Test Framework(EditMode) |
| 버전 관리 | git, 아래 4장 구조 |

---

## 4. 저장소 구조 (GitHub)

```
repo-root/
├─ README.md                      # 실행·조작·빌드 방법, 스크린샷
├─ CLAUDE.md                      # 이 문서 0장 요약 + 코딩 규칙 (Claude Code가 항상 읽음)
├─ .gitignore / .gitattributes
├─ docs/
│  ├─ MASTER_PROMPT.md            # 이 문서
│  ├─ 요구사항 정의서.md / 기능 정의서.md
│  ├─ ASSET_DIRECTION.md          # 목표 화면 분석, 아트·사운드 방향, 파일 이름 규칙
│  ├─ ASSET_CREDITS.md            # 사용한 외부 에셋 출처·라이선스
│  ├─ DECISIONS.md                # 구현 중 스스로 내린 결정 기록
│  ├─ VERIFY.md                   # 검수 체크리스트와 결과표
│  ├─ reference/                  # 목표 화면 3장 (ref_01_combat, ref_02_dialogue, ref_03_nightvision)
│  └─ evidence/                   # 로그, 스크린샷, 클리어 영상, 목표 화면 비교
├─ AssetSource/                   # 사용한 외부 에셋 원본·라이선스, build_assets.ps1 (Unity가 임포트하지 않음)
├─ Packages/manifest.json
├─ ProjectSettings/
└─ Assets/
   ├─ StreamingAssets/Config/     # game, weapons, enemies, ai, difficulty, missions, barks, dialogue .json, credits.txt
   │  └─ maps/                    # map_01.json ~ map_04.json
   ├─ Tests/EditMode/             # 13.1의 테스트
   └─ _Project/
      ├─ Scripts/
      │  ├─ Core/                 # GameManager, SceneFlow, EventBus, Events, Rng, Config 로더, AssetCatalog
      │  ├─ Grid/                 # GridMap, TileType, Raycast(DDA), LineOfSight, SoundPropagation, LightGrid
      │  ├─ Player/               # PlayerController, PlayerMotor, PlayerNoise, CameraFollow, NightVision
      │  ├─ Weapons/              # WeaponData, WeaponController, Hitscan, Decoy(유인탄), MeleeTakedown
      │  ├─ Combat/               # Health(체력·방탄), DamageRequest, CoverObject, CombatFx(화염·탄피·혈흔·시체)
      │  ├─ Vision/               # PlayerVision, FogOfWar(탐색 기억), VisionRenderer, MapLighting(Light2D 배치)
      │  ├─ Map/                  # MapData, MapLoader, Door, Interactable, MapRegistry
      │  ├─ Mission/              # MissionManager, Objective(Eliminate/Steal/Bomb), ResultEvaluator, Records
      │  ├─ AI/
      │  │  ├─ Perception/        # SightSensor, HearingSensor, AwarenessGauge, ClueMemory
      │  │  ├─ FSM/               # EnemyBrain, EnemyState(Patrol/Investigate/Chase/Engage/Search/Dead)
      │  │  ├─ Behavior/          # BehaviorTree 노드, CombatTree, BarkPicker(상황 → 대사)
      │  │  ├─ Navigation/        # AStarPathfinder, PathFollower, PatrolRoute, SearchPointPicker, PassageYield
      │  │  ├─ Alert/             # AlertSystem(외침·무전), GlobalAlertLevel
      │  │  └─ EnemyController.cs
      │  ├─ UI/                   # Hud, AwarenessIcon, BarkView, Crosshair, InteractPrompt, DialogueBox,
      │  │                        # MainMenu, MissionSelect, SettingsOverlay, PauseMenu, ResultScreen, CreditsScreen
      │  ├─ Audio/                # AudioManager, SfxId, NoiseEmitter, AmbienceZone(야외 환경음)
      │  ├─ Data/                 # SaveSystem, SettingsData, DifficultyData, DialogueData, BarkData
      │  ├─ Debug/                # DebugOverlay, StateTransitionLogger, PerfProbe
      │  └─ Util/                 # MathUtil, ProceduralSprites, ObjectPool
      ├─ Editor/                  # ProjectBuilder.cs, AssetCatalogBuilder.cs, MapValidatorMenu.cs
      ├─ Scenes/                  # MainMenu.unity, Game.unity  (ProjectBuilder가 생성)
      ├─ Prefabs/                 # Player, Enemy_Guard, Enemy_Rifleman, Door, Lamp, Corpse, FX, UI
      ├─ Art/                     # tiles/ props/ chars/ fx/ obj/ ui/ portraits/ (파일이 빠지면 코드 생성)
      ├─ Audio/                   # sfx/ bgm/ (bgm/에 amb_ 환경음 포함, 파일이 빠지면 코드 생성 톤)
      └─ Fonts/                   # Galmuri11, 디버그용 NotoSansKR. 없으면 Unity 기본 폰트
```

---

## 5. 아키텍처

### 5.1 원칙
- 시스템끼리는 **이벤트 버스**로만 대화한다. 이벤트 구조체는 `Core/Events.cs` 한 파일에 모은다.
- 모든 구독은 `OnEnable`에서 하고 `OnDisable`에서 반드시 해제한다.
- 감지와 길찾기는 **적마다 시간 간격을 엇갈려** 처리한다(5.4). 밝기 격자는 문이 바뀔 때만 부분 재계산한다.
- 난수는 `Rng` 한 곳에서 만든다. **AI용 난수원과 연출용 난수원(대사 선택·혈흔 모양·탄피 방향·총구 화염 그림·피 입자)을 분리**해, 연출이 AI 재현성에 영향을 주지 않게 한다. 고정 시드 모드면 AI 난수원 시드를 고정한다.

### 5.2 이벤트 목록 (Core/Events.cs)

| 이벤트 | 필드 | 보내는 쪽 → 받는 쪽 |
| --- | --- | --- |
| `NoiseEvent` | position, kind, radius, sourceId, time | 플레이어·무기·유인탄 → HearingSensor, HUD 소음 미터 |
| `DamageEvent` | targetId, amount, attackerId, hitPoint, kind(Bullet/Melee), time | Hitscan·MeleeTakedown → Health, CombatFx |
| `HealthChangedEvent` | entityId, hp, armor | Health → HUD |
| `DeathEvent` | entityId, isPlayer, position, corpseSprite, rotationDeg, time (1회만) | Health → Mission·AI·UI·CombatFx |
| `ShotFiredEvent` | shooterId, muzzlePos, dir, weaponId, endPos | WeaponController → CombatFx(화염·궤적·탄피), LightGrid(사격 밝기), Crosshair |
| `EnemyStateChangedEvent` | enemyId, from, to, reason, time | EnemyBrain → UI·Mission·Debug·BarkPicker |
| `DetectionEvent` | enemyId, time | AwarenessGauge → Mission·Alert |
| `AlertEvent` | kind(Shout/Radio), position, time, senderId | AlertSystem → 수신 적 |
| `BarkEvent` | enemyId, text | BarkPicker → BarkView, Audio |
| `DoorChangedEvent` | doorId, isOpen, actorId, time | Door → Vision·LightGrid·Nav·AI |
| `DialogueStateEvent` | dialogueId, isActive | DialogueBox → GameManager(정지)·Mission·Audio |
| `MissionStateEvent` | state, objectiveProgress, timeLeft, result | Mission → UI·Audio·GameManager |
| `AmmoChangedEvent` | weaponId, mag, reserve | Weapon → HUD |
| `PauseEvent` | isPaused | GameManager → Audio·UI·AI |

### 5.3 피해·사망·재시작 책임
1. `Hitscan`이 명중을 판정하고 `DamageEvent`를 보낸다.
2. `Health`가 방탄을 먼저 줄이고 남은 피해로 체력을 줄인다. `HealthChangedEvent`를 보내고, 체력 0 이하면 `isDead`를 켜고 **`DeathEvent`를 한 번만** 보낸다.
3. `MissionManager`가 `DeathEvent`를 받아 처치 수를 올리고 승패를 판정한다. `CombatFx`는 시체를 남긴다.
4. 재시작은 `GameManager`가 게임 씬을 다시 불러와 처리한다. 탐색 기억·적·소음·타이머·전투 흔적은 자동 초기화된다.

### 5.4 매 프레임 처리 순서와 분산
`GameManager.Update` 한 곳에서 아래 순서를 보장한다(각 시스템은 `Tick(dt)`를 노출). 대화·일시정지 중에는 1~8을 건너뛴다.

1. 입력 읽기 → 2. 플레이어 이동 → 3. 소음 발생·전달 → 4. 적 감지(분산, 밝기 반영) → 5. 적 상태 전환·이동 → 6. 공격·피해 → 7. 사망 확정 → 8. 미션 판정 → 9. 화면 갱신(시야·조명·전투 흔적·대사·UI)

분산 규칙: 적마다 `phase = id % 4`, 시각 감지는 `frame % 4 == phase`인 적만 실행. 길찾기는 한 프레임 **최대 2건**, 나머지는 큐. 이동·상태 전환은 매 프레임.

---

## 6. 개발 순서 (Phase)

각 Phase 끝에서 "완료 조건"을 확인한다. 못 맞추면 다음으로 넘어가지 말고 고친다.

### Phase 0 — 프로젝트 뼈대
- 4장 폴더 구조, `.gitignore`, `.gitattributes`, `CLAUDE.md`, `README.md` 초안, `docs/` 파일.
- `Core`: `EventBus`, `Events`, `Rng`(AI·연출 분리), `ConfigLoader`, `GameManager` 골격.
- `Util/ProceduralSprites`: 픽셀 스프라이트(단색 + 테두리·격자 무늬, 원+총 막대 캐릭터)를 `Texture2D`(Point 필터)로 만든다. 크기는 `ASSET_DIRECTION.md` 3.1의 파일별 크기를 따른다(외부 파일과 바로 바꿔 끼울 수 있게).
- `Core/AssetCatalog` + `Editor/AssetCatalogBuilder`(11.0): 파일이 없어도 빈 카탈로그가 생기고 코드 생성 폴백이 동작해야 한다.
- `Editor/ProjectBuilder`: URP 2D 렌더러 에셋 생성·지정, 빈 `MainMenu`·`Game` 씬, Build Settings 등록, `AssetCatalogBuilder` 호출.
- **완료 조건**: `Tools/Build Project` 실행 시 두 씬이 생기고 URP 2D 렌더러가 지정되며, 에러 없이 컴파일된다.

### Phase 1 — 격자·맵·카메라
- `Grid/GridMap`(1칸=1m, 벽·바닥·문·창문·엄폐물 속성, 바닥 종류), `TileType`, `Raycast`(DDA), `LineOfSight`.
- `Map/MapLoader`: 9.3 JSON을 읽어 `Tilemap`으로 그린다. `map_01.json`을 **먼저 손으로 작성**한다.
- 카메라: Pixel Perfect Camera(960×540, PPU 32) → orthographic size 8.4375(화면 높이 약 17m). 플레이어를 따라가며 커서 쪽으로 최대 3m 앞당김.
- **완료 조건**: 맵 1이 픽셀이 깨지지 않게 그려지고, `GridTests`가 통과한다.

### Phase 2 — 플레이어 이동·조준·시야·조명 표시
- `PlayerController/Motor`: WASD 8방향, 대각선 정규화, 벽 충돌, 커서 바라보기, 걷기·달리기·웅크리기.
- `UI/Crosshair`: 하드웨어 커서 숨김, 꺾쇠 조준선.
- `Vision/PlayerVision`(90°·12m, DDA 차폐), `FogOfWar`(1픽셀/타일 오버레이, 기억 회색조, 미탐색 암전).
- `Vision/MapLighting`: 맵 `ambient`로 Global Light2D(남색), `lights`마다 Point Light2D(따뜻한 노랑), 벽 타일에 ShadowCaster2D. 색·Falloff·그림자 강도는 `game.json`의 `lighting`(9.1). **화면 조명만** 이 Phase에서 한다(밝기 격자는 Phase 8).
- 필름 그레인: 코드 생성 노이즈 텍스처(256×256)를 화면 전체에 알파 0.04로 깔고 매 프레임 UV를 옮긴다(야간 투시와 같은 텍스처를 쓴다).
- 시야 밖 적·아이템은 렌더러를 끈다.
- **완료 조건**: ref_01처럼 천장등 아래만 밝고 나머지가 어둡다. 벽 뒤는 보이지 않고 지나간 곳은 회색조로 남는다.

### Phase 3 — 문·엄폐물·무기·전투
- `Door` + `Interactable`: **Space** 토글, 1.2m 안 가장 가까운 대상에 `UI/InteractPrompt`(`[SPACE] 열기`). 닫힘=이동·시야·총알·빛 차단. `DoorChangedEvent`.
- `CoverObject`: 높은(`C`)·낮은(`c`) 엄폐물, 창문(`W`). 파괴 불가. 풀숲(`f`)은 이동만 막는 장식물이다.
- `WeaponController`: 3종, 탄창·예비, 재장전, 0.3초 전환 지연, 재장전 중 전환 시 취소·잔탄 유지. `Hitscan` + 궤적선 0.1초.
- `Health`: 체력 + 방탄(7.2). 피격 적색 깜빡임 0.1초.
- `CombatFx`: 총구 화염(0.05초, 작은 Light2D, 연출 난수로 1장), 탄피, 피격 순간 피(`fx/hit_blood_*` 번호 순서로 0.3초 재생, 파일이 없으면 혈흔 그림을 작게 4~6개 흩뿌림), 바닥 혈흔, 시체. `ObjectPool` 최대 512.
- `MeleeTakedown`: 7.5 조건.
- **완료 조건**: 허수아비 적을 사격·제압할 수 있고, 벽·문·창문·엄폐물 뒤는 맞지 않으며, 혈흔·탄피·시체가 남는다.

### Phase 4 — 소리 시스템
- `NoiseEmitter`(발소리·사격·유인탄), `SoundPropagation`: 7.6 공식.
- F3에서 소리 반경 원 표시.
- **완료 조건**: `SoundTests` 통과.

### Phase 5 — 적 AI 기본 (M)
1. `AStarPathfinder` + `AStarTests`.
2. `PathFollower`, `PatrolRoute`.
3. `SightSensor`, `HearingSensor`, `AwarenessGauge`(밝기 배율 자리는 두되 L=1로 고정), `ClueMemory`.
4. `EnemyBrain` FSM 6상태(7.7), `StateTransitionLogger`.
5. `SearchPointPicker`.
6. 교전 기본 사격.
7. `AwarenessIcon`(? / !), `BarkPicker` + `BarkView`(7.18).
- **완료 조건**: 맵 1에서 적이 순찰하고, 플레이어를 보면 `?`→`!`과 대사가 뜨고 쫓아와 쏘고, 놓치면 수색 후 순찰로 돌아간다. 소리에도 반응한다.

### Phase 6 — 미션·결과 (M)
- `MissionManager`: 준비→(브리핑)→진행→종료→(디브리핑)→결과. `Eliminate`, `Steal`(Space 상호작용), 탈출 지점(맵 3).
- `ResultEvaluator`: 7.9 우선순위, 7.10 발각 집계, 처치 수·클리어 시간.
- **완료 조건**: 맵 1 소탕을 처음부터 끝까지 클리어·실패할 수 있다. `MissionResultTests` 통과.

### Phase 7 — UI·메뉴·대화·사운드·설정 (M)
- `Hud`: 10장 좌하단 통합 패널(체력·방탄, 소음 미터, 무기 칸, 자세 실루엣; 밝기 미터는 Phase 8에서 켬), 좌상단 목표·타이머, 우상단 경계.
- `DialogueBox`(7.19) + `dialogue.json` 맵 1 브리핑.
- `MainMenu`, `MissionSelect`(난이도), `SettingsOverlay`, `PauseMenu`, `ResultScreen`, `CreditsScreen`(11.5).
- `AudioManager`: `Audio/`의 외부 소리 재생(파일이 없으면 코드 생성 톤, 11.2), 일시정지 시 정지·이어 재생, 음량 3종. `AmbienceZone`: 플레이어 칸 바닥이 야외(`o`)면 `amb_outdoor_night`를 0.5초 페이드 인, 실내면 페이드 아웃(배경음 음량을 따름).
- **완료 조건**: 메인 메뉴 → 미션 선택 → 브리핑 → 플레이 → 결과 → 메인 메뉴를 오갈 수 있고, 메인 메뉴에서 크레딧 화면이 열린다. HUD 배치가 ref_01과 같다.

### Phase 8 — 밝기 격자·맵 2~4·나머지 M
- `Grid/LightGrid`(7.17): 칸별 밝기 계산, 문 변경 시 부분 재계산, 사격 밝기. `SightSensor`에 밝기 배율·시야 거리 반영. HUD 밝기 미터 켜기. 어두운 칸의 적 표시 규칙.
- `map_02.json`~`map_04.json`(9.3 규격), `MapValidator` + 테스트.
- 무기 3종, 최고 기록, 기록 비교(7.11), 맵 2~4 브리핑 대사.
- **완료 조건**: 맵 4종을 성공·실패로 끝낼 수 있다. `LightGridTests` 통과. 어두운 구역에서 확실히 덜 들킨다.

### Phase 9 — S 기능
하나 끝날 때마다 커밋한다.
1. 적 엄폐 이동
2. 저장·복구(7.13)
3. 동료 경보·전역 경계도(7.8)
4. 난이도 3단계(7.12)
5. 문 흔적 인지(+30)
6. 통로 충돌 처리
7. 유인탄
8. 야간 투시(7.20)
9. 폭탄 해체 미션(`missions.json` 설정, 기본값은 자료 탈취 후 탈출 유지)

### Phase 10 — 마감
- `DebugOverlay`(F3, 밝기 히트맵 포함), `PerfProbe`, 고정 시드 모드.
- 적 20명 부하 테스트(조명 켠 상태), 30분 안정성 확인.
- 목표 화면 3장과 같은 구도의 스크린샷을 `docs/evidence/`에 저장하고 나란히 비교.
- `docs/VERIFY.md`, `docs/ASSET_CREDITS.md`(넣은 에셋은 상태 `사용`, 안 쓴 후보는 `제외`), `README.md` 완성. `credits.txt`가 `ASSET_CREDITS.md` 5장과 같은지 확인.
- Windows 빌드(`BuildPipeline` → `Build/Game.exe`), 실행 확인.
- **완료 조건**: 14장 "완료 정의"를 모두 만족한다.

---

## 7. 게임 규칙과 수치 (기본값)

> 단위: 거리 m, 각도 도(°), 시간 초. 모든 값은 JSON으로 빼서 코드 수정 없이 바꿀 수 있어야 한다.

### 7.1 조작 (기본 키 배치, 변경 UI 없음)

| 동작 | 입력 |
| --- | --- |
| 이동 | W A S D |
| 조준 | 마우스 커서 방향 |
| 사격 | 마우스 좌클릭 (권총류 단발, 소총 누르고 있으면 연사) |
| 재장전 | R |
| 무기 전환 | 1 권총 / 2 소음권총 / 3 소총 |
| 달리기 | Left Shift (누르고 있는 동안) |
| 웅크리기 | Left Ctrl (누르고 있는 동안) |
| 상호작용(문·자료·폭탄) | **Space** (폭탄은 누르고 있기) |
| 근접 제압 | F |
| 유인탄 투척 | G (커서 위치로) |
| 야간 투시 | N |
| 대화 넘기기 / 선택지 | Space·좌클릭 / 1~3 |
| 일시정지 | Esc |
| 디버그 화면 | F3 |

대화 중에는 무기 전환 키(1~3)가 선택지 선택으로만 쓰인다.

### 7.2 플레이어

| 항목 | 값 |
| --- | --- |
| 최대 체력 | 100 |
| 방탄 | 50으로 시작(최대 50). 피해를 먼저 1:1로 받고, 0이 되면 체력이 줄어든다. 회복·획득 없음 |
| 걷기 속도 | 3.5 m/s |
| 달리기 | 걷기 ×1.5 (5.25 m/s) |
| 웅크리기 | 걷기 ×0.5 (1.75 m/s) |
| 크기 | 지름 0.8m 원 |
| 발소리 반경 / 간격 | 걷기 6m / 0.6초, 달리기 9m / 0.4초, 웅크리기 3m / 0.8초 (정지 시 소리 없음) |
| 시야 | 90°, 12m, 마우스 방향 기준 |
| 피격 반응 | 적색 깜빡임 0.1초 + 피격음(방탄이 받으면 `sfx_hit_armor`). 경직·밀림·둔화·무적 없음 |
| 사망 | 체력 0 이하 → `DeathEvent` 1회 → 미션 실패 |
| 시작 장비 | 3종 무기, 권총을 든 채 시작, 유인탄 2개, 야간 투시(배터리 100%) |
| 상호작용 거리 | 1.2m (가장 가까운 대상 1개) |

### 7.3 무기

| 무기 ID / 표시 이름 | 피해 | 사거리 | 발사 간격 | 소음 반경 | 탄창 / 예비 | 장전 | 비고 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `pistol` / P-9 | 20 | 15m | 0.3초 | 12m | 12 / 36 | 1.5초 | 기본, 단발 |
| `silenced_pistol` / P-9 SD | 17 | 12m | 0.3초 | 3m | 10 / 30 | 1.8초 | 단발 |
| `rifle` / AR-30 | 30 | 30m | 0.12초 | 25m | 30 / 90 | 2.5초 | 연사 |

- 전환 지연 0.3초. 재장전 중 전환하면 재장전이 취소되고 **잔탄 유지**.
- 재장전은 탄창 부족분만큼 예비에서 채운다.
- **히트스캔**: 발사 순간 조준 방향으로 사거리까지 DDA 레이. 처음 닿는 적·벽·닫힌 문·창문·엄폐물에서 멈춘다. **관통 없음**. 적이 쏜 탄은 다른 적을 무시한다. 궤적선 약 0.1초.
- 소총 피해 > 권총 피해 > 소음권총 피해 관계를 유지한다. 표시 이름은 가상 이름이다.

### 7.4 유인탄 (S)
2개. 던진 지점에서 소음 반경 10m가 3초간 지속(0.5초 간격으로 소음 이벤트 반복). 벽 감쇠 동일. 플레이어에게 영향 없음. 최대 10m, 비행 0.5초.

### 7.5 근접 제압
조건 **모두** 만족: 적과 1.5m 이내, 플레이어가 적의 뒤쪽 120° 부채꼴 안, 적 상태가 순찰·조사(미발각)이며 게이지 100 미만. 성공하면 소음 없이 즉시 제압(사망 처리, 처치 수 +1, 시체 남음).

### 7.6 소리 전파 (SoundPropagation) — 순수 함수

```
입력: 소음 위치 S, 반경 R, 청취자 위치 L
d = |S - L|
if d > R: 세기 = 0
else:
  base = 1 - d / R
  walls = S→L DDA 레이에서 지나친 "벽 + 닫힌 문 + 창문" 타일 개수
  세기 = base * 0.5^walls             # 열린 문은 세지 않는다
반응 조건: 세기 >= 0.2
```

### 7.7 적 종류와 수치

| 항목 | 경비병 (Guard) | 소총병 (Rifleman) |
| --- | --- | --- |
| 체력 | 50 | 60 |
| 시야 | 90°, 10m | 70°, 14m |
| 무기 | 권총형: 피해 12, 간격 0.5초, 사거리 12m, 탄창 12 | 소총형: 피해 8, 간격 0.25초, 사거리 20m, 탄창 30 |
| 장전 | 1.5초 | 2.5초 |
| 예비 탄약 | 무제한 | 무제한 |
| 순찰 / 추격 속도 | 2.0 / 3.5 m/s | 2.0 / 3.5 m/s |
| 외형 | 어두운 붉은 갈색 옷, 권총 | 짙은 녹갈색 옷, 소총 |
| 경보 방식 | 외침(반경 12m) | 무전(전체) |

**공통 교전 수치**: 반응 지연 0.5초, 첫 발 0.8초, 명중률 약 60%(조준 오차 ±5° 균등 난수, 플레이어가 달리면 ×1.5), 사거리 안이면 멈춰 사격, 밖이면 접근.

**인지 게이지** (적마다 0~100):

| 항목 | 값 |
| --- | --- |
| 의심 / 발견 기준 | 30 / 100 |
| 시야 안 상승 | 초당 +40 |
| 상승 배율 (곱함) | 3m 이내 ×2, 달리기 ×1.5, 웅크리기 ×0.5, **밝기 ×(0.3 + 0.7·L)** (3m 이내면 밝기 배율 생략) |
| 실제 시야 거리 | 기본 거리 × (0.5 + 0.5·L) (3m 이내는 항상 보임) |
| 놓친 뒤 | 1초 유지 후 초당 −15 |
| 새 소리 | 게이지를 최소 30까지 올리고 소리 위치를 단서로 저장 |
| 문 흔적 | +30 |

L은 플레이어가 서 있는 칸의 밝기(7.17). Phase 8 전에는 L = 1.

**단서 기억**: 소리 8초, 마지막 목격 15초. 우선순위 최근 목격 > 가까운 큰 소리 > 먼 소리.

**FSM 전환 표**:

| 현재 | 조건 | 다음 |
| --- | --- | --- |
| 순찰 | 게이지 ≥ 30 또는 새 소리 | 조사 |
| 조사 | 게이지 = 100, 사거리 안 | 교전 |
| 조사 | 게이지 = 100, 사거리 밖 | 추격 |
| 조사 | 단서 위치 도착, 플레이어 없음 | 수색 |
| 추격 | 사거리 안 & 시야 확보 | 교전 |
| 추격·교전 | 플레이어를 놓쳐 게이지 < 100 | 수색 |
| 수색 | 새 소리 | 조사 |
| 수색 | 플레이어를 다시 봄(게이지 100) | 추격 또는 교전 |
| 수색 | 수색 10초 종료 | 순찰 복귀 |
| 모든 상태 | 체력 ≤ 0 | 사망 |

**조건 충돌 시 우선순위**: 사망 > 눈앞의 플레이어 > 새 소리 > 수색 종료.

**상태 설명**
- 조사: 단서 위치로 걸어가 둘러본다(도착 후 1초 정지). 2.0 m/s.
- 수색: 마지막 인지 위치 반경 8m 안 도달 가능한 무작위 3곳을 가까운 순서로, **총 10초**. 끝나면 가장 가까운 순찰 지점으로.
- 순찰: 순찰 지점 순환, 지점마다 2~3초 대기.

**교전 행동 트리** (`Selector`):
1. 체력 ≤ 40% 또는 재장전 중 또는 사선 막힘 → 6m 안 가장 가까운 엄폐물로 *(S)*
2. 탄창 비었음 → 재장전
3. 사거리 밖 → 접근
4. 사거리 안 & 시야 확보 → 정지 후 사격
5. 그 외 → 마지막 목격 위치로 이동

### 7.8 동료 경보·전역 경계도 (S)
- 적이 **발견 상태에 처음 들어갈 때**(`DetectionEvent`) 경보. 경비병은 외침(12m, 벽 무시), 소총병은 무전(전체).
- 수신 적은 **조사 상태**로 그 위치를 조사, 재전파 없음. 발신은 5초에 1회, 사건당 최대 2회.
- **전역 경계도**: 평상(전원 순찰)·경계(조사·수색 존재)·전투(추격·교전 존재). 경계 이상이면 수색 시간 ×1.2. HUD 우상단.

### 7.9 승패 판정 (프레임당 1회)

| 동시에 일어난 사건 (같은 프레임) | 결과 |
| --- | --- |
| 목표 달성 + 제한 시간 종료 | 성공 |
| 탈출 + 사망 | 실패 |
| 목표 달성 + 사망 | 실패 |
| 서로 다른 프레임 | 먼저 일어난 사건 |

판정 후 입력을 막고 디브리핑 → 결과 화면으로 간다.

### 7.10 발각 횟수
게이지가 100에 처음 도달한 순간(`DetectionEvent`, 직접 목격만)을 1회. 같은 시간대 여러 적은 1회. **모든 살아 있는 적이 순찰로 돌아간 뒤** 다시 발각되면 +1. 경보로 안 것은 세지 않는다.

### 7.11 기록
- 비교 순서: **클리어 시간(짧을수록, 소수점 셋째 자리) > 발각 횟수 > 처치 수(적을수록)**. 같으면 기존 유지.
- 미션·난이도별로 저장. 실패는 기록하지 않는다. 클리어 시간에서 대화 시간은 뺀다.

### 7.12 난이도 (S, 보통 1단계만 M)

| 난이도 | 배율 m |
| --- | --- |
| 쉬움 | 0.8 |
| 보통 | 1.0 |
| 어려움 | 1.2 |

적용: **시야 거리·수색 시간 × m**, **반응 지연·조준 오차 × (1/m)**.

### 7.13 설정·저장
- 설정: 음량(전체·효과음·배경음) 즉시 적용, 전체화면/창모드·해상도(1280×720, 1920×1080)는 "적용" 때.
- 파일: 읽기용 `streamingAssetsPath/Config/*.json`, 사용자 저장 `persistentDataPath/save.json`.
- 저장 시점: 설정은 적용 때, 최고 기록은 미션 종료 때. `save.json.tmp`에 쓴 뒤 교체, 이전 파일은 `save.json.bak`.
- 실패: 안내 → 1회 재시도 → 기록 없이 진행. 손상: `.corrupt` 보존 → `.bak` 복원 → 없으면 동의 후 초기화.

### 7.14 성능 기준 (합격선)
- 적 20명 동시 활성, **2D 조명·그림자 켠 상태**에서 **60fps**.
- AI 처리 **프레임당 2ms 이하** (렌더링·물리 제외). 밝기 격자 재계산은 문 변경 때만이며 따로 측정해 기록한다.
- 최저 45fps(22ms)까지 허용, 0.5초 넘게 지속 금지, **33ms 초과 프레임 0회**.
- 측정: 적 20명 추격·교전 60초, 평균·최대·2ms 초과 비율. Development Build 측정 후 배포 빌드 재확인.
- 30분 연속 플레이에서 예외·메모리 증가 없음.

### 7.15 길찾기 (A*)
- 격자: 1칸 = 1m, 8방향. 비용 직선 1, 대각 1.414. 휴리스틱 **옥타일 거리**.
- 모서리 규칙: **대각 이동 시 인접한 두 칸 중 하나라도 막히면 통과 불가**.
- 문: 닫힌 문은 통과 가능(비용 +2), 문 앞 0.5초 멈춰 열고 지나간다. 창문·엄폐물·풀숲은 통과 불가.
- 재계산: 목표 변경, 문 변경, 0.5초 이상 막힘, 추격 목표 3칸 이상 이동. 최소 0.3초. 실패 시 1초 뒤 최대 3회, 계속 실패하면 가까운 도달 가능 지점 → 순찰 복귀.
- 노드 배열 재사용, 이진 힙 직접 구현.

### 7.16 통로 충돌 (S)
좁은 곳에서 마주치면 우선순위가 낮은 적(순찰·대기)이 가까운 빈 칸으로 비킨다. 2초 이상 막히면 임시 겹침 허용.

### 7.17 조명과 밝기 (LightGrid) — 순수 클래스

```
입력: 맵 칸, 바닥 종류별 전역광(ambient), 조명 목록 [{pos, radius, intensity}]
for 각 칸 c:
  L = ambient[floor(c)]                          # 실내 0.15, 야외 0.08
  for 각 조명 k (|k.pos - c| <= k.radius):
    if DDA(k.pos → c)가 벽·닫힌 문·높은 엄폐물에 막히지 않으면:
      L += k.intensity * (1 - |k.pos - c| / k.radius)
  light[c] = clamp(L, 0, 1)
문이 바뀌면: 그 문에서 반경 안에 있는 조명이 비추는 칸만 다시 계산
사격하면: 0.3초 동안 사수 칸 밝기 = 1 (플레이어만 감지에 영향)
```
- 화면 조명(`Light2D`)과 같은 조명 목록을 쓴다. 화면 밝기와 논리 밝기가 완전히 같을 필요는 없지만 "밝아 보이는 곳이 밝다"는 일치해야 한다.
- 플레이어 시야: 밝기 0.15 미만 칸의 적은 4m 밖이면 렌더러를 끈다(야간 투시 켜면 켬).
- HUD 밝기 미터 = 플레이어 칸 밝기.

### 7.18 적 대사 (Bark)
- `EnemyStateChangedEvent`·피격·재장전·경보 수신 때 `BarkPicker`가 `barks.json`에서 상황에 맞는 대사 하나를 연출용 난수로 고른다.
- 머리 위 0.9m에 흰 글자(그림자 1px), 2초 표시 후 0.3초 페이드. 적 1명당 3초에 1회. 플레이어 화면에 렌더되는 적만.
- 소음·경보·AI 상태에 아무 영향도 없다. 표시음 `sfx_bark` 1회.

### 7.19 대화 (브리핑·디브리핑)
- 미션 시작 전과 판정 후 `dialogue.json`의 `{map_id}_briefing` / `{map_id}_debrief_success` / `{map_id}_debrief_fail`를 재생. 없으면 건너뛴다.
- 화면 하단 전체 폭, 높이 약 30%. 왼쪽 초상화(32×40 픽셀을 4배) + 빨강 이름표, 오른쪽 대사(초당 40자 출력, Space/클릭으로 즉시 완성) + 번호 선택지. 선택지가 없으면 `1. [...]` 하나.
- 대화 중 `Time.timeScale`을 바꾸지 말고 `GameManager`가 1~8 단계 Tick을 건너뛴다(타이머·AI 정지). BGM은 계속.

### 7.20 야간 투시 (S)
- N 토글. 배터리 60초, 켜면 초당 1 소모, 끄면 초당 0.5 충전. 0이면 자동 꺼짐.
- 화면: URP 풀스크린 패스 또는 카메라 앞 쿼드로 녹색 단색 + 노이즈 + 비네팅. 켜져 있는 동안 화면용 Global Light2D 강도를 `lighting.nvg_global_intensity`(0.6)로 올린다. 어두운 칸의 적 표시 규칙(7.17)을 해제한다.
- 밝기 값·AI 감지에는 영향 없음. HUD 무기 칸 아래에 배터리 %.

---

## 8. AI는 어떻게 쓰이는가

### 8.1 구성 한눈에
```
[감각]  SightSensor ──┐  (LightGrid 밝기 반영)
        HearingSensor ─┼─> AwarenessGauge ─> (의심 30 / 발견 100)
        DoorClue ──────┘          │
                                  v
[판단]  EnemyBrain (FSM 6상태) ── 전환 표(7.7) ── EnemyStateChangedEvent ─> BarkPicker(연출)
                                  │
                  교전 상태일 때만 v
        CombatTree (행동 트리: 엄폐→재장전→접근→사격→추적)
                                  │
[행동]  PathFollower(A*) ─ 이동 / WeaponController ─ 사격 / AlertSystem ─ 경보
```
- **FSM**이 큰 상태를 정하고, **행동 트리**는 교전 안에서 "무엇을 할지"만 고른다.
- 적은 **자기가 보고 들은 것과 전달받은 정보**로만 움직인다. 플레이어 위치를 직접 읽는 코드는 `SightSensor` 한 곳에만 둔다.
- 대사는 FSM 결과를 **읽기만** 한다. 대사가 AI 판단에 되먹임되면 안 된다.
- 모든 상태 전환은 `reason` 문자열과 함께 로그로 남긴다(예: `"sight_gauge_100"`, `"heard_noise"`, `"search_timeout"`).

### 8.2 구현 규칙
- 학습형 AI·외부 AI 패키지·NavMesh 금지.
- AI 난수(순찰 대기, 수색 지점, 조준 오차)는 `Rng`의 AI 난수원에서만. 고정 시드 모드에서 같은 입력이면 같은 상태 전이.
- `PerfProbe`가 AI 처리 시간을 `Stopwatch`로 잰다.
- F3: 적 ID·상태·게이지, 시야 부채꼴, A* 경로선, 소리 반경 원, 칸별 밝기 히트맵.

### 8.3 Claude Code가 이 프로젝트에서 AI(자기 자신)를 쓰는 방식
- 이 문서가 단일 진실 공급원이다. 숫자·표를 따르고, 문서에 없으면 `docs/DECISIONS.md`에 적고 진행한다. 화면 요소는 `docs/reference/` 이미지를 본다.
- Phase마다 **작게 만들고 → 테스트하고 → 커밋**한다.
- 맵 JSON은 Claude Code가 직접 작성하고 `MapValidator`로 검증한다.
- 외부 에셋은 11.5 라이선스 기준을 따르고 `docs/ASSET_CREDITS.md`에 기록한다. Claude Code는 에셋을 내려받지 않는다. 목표 화면 게임의 그림·글꼴·대사는 쓰지 않는다.

---

## 9. 데이터 파일

### 9.1 `game.json`
```json
{
  "version": 2,
  "reference_resolution": [1920, 1080],
  "pixel_reference_resolution": [960, 540],
  "pixels_per_unit": 32,
  "camera_ortho_size": 8.4375,
  "camera_lookahead_ratio": 0.3,
  "camera_lookahead_max_m": 3.0,
  "interact_range_m": 1.2,
  "fixed_seed_enabled": false,
  "fixed_seed": 12345,
  "default_difficulty": "normal",
  "lighting": {
    "ambient_color": "#1A1830",
    "global_intensity_scale": 10.0,
    "lamp_color": "#FFC873",
    "lamp_falloff": 0.6,
    "shadow_strength": 0.9,
    "wall_rim_color": "#3A3A44",
    "muzzle_light_color": "#FFD38A",
    "muzzle_light_radius_m": 3.0,
    "muzzle_light_s": 0.05,
    "nvg_color": "#7CFF9A",
    "nvg_global_intensity": 0.6,
    "film_grain_alpha": 0.04
  },
  "audio": { "ambience_fade_s": 0.5 },
  "player": { "walk_speed": 3.5, "run_mult": 1.5, "crouch_mult": 0.5, "radius_m": 0.4, "fov_deg": 90, "view_m": 12, "vision_rays": 90 },
  "view": { "hand_x_m": 0.25, "hand_y_m": -0.25, "held_center_m": 0.16, "muzzle_m": 0.31, "walk_fps": 8, "shoot_frame_s": 0.1, "melee_frame_s": 0.2, "enemy_radius_m": 0.4 },
  "fog": { "unexplored_alpha": 1.0, "memory_alpha": 0.65, "memory_gray": 0.04 },
  "fx": { "tracer_s": 0.1, "tracer_color": "#FFE680", "muzzle_s": 0.05, "hit_flash_s": 0.1, "hit_blood_s": 0.3, "pool_max": 512,
          "casing_s": 0.15, "casing_dist_min_m": 0.4, "casing_dist_max_m": 0.8, "blood_scale_min": 0.5, "blood_scale_max": 0.9, "blood_scatter_m": 0.3 },
  "crosshair": { "shot_spread": 1.35, "shot_spread_s": 0.15, "run_spread": 1.2, "display_scale": 1 }
}
```
`global_intensity_scale`은 화면용 Global Light2D 세기 = `ambient.indoor` × 이 값이다. 판정용 밝기(7.17)는 `ambient` 값을 그대로 쓴다.

### 9.2 `weapons.json`
```json
{
  "version": 2,
  "weapons": [
    { "id": "pistol",          "display_name": "P-9",    "damage": 20, "range_m": 15, "fire_interval_s": 0.3,  "noise_radius_m": 12, "mag_size": 12, "reserve": 36, "reload_s": 1.5, "auto": false },
    { "id": "silenced_pistol", "display_name": "P-9 SD", "damage": 17, "range_m": 12, "fire_interval_s": 0.3,  "noise_radius_m": 3,  "mag_size": 10, "reserve": 30, "reload_s": 1.8, "auto": false },
    { "id": "rifle",           "display_name": "AR-30",  "damage": 30, "range_m": 30, "fire_interval_s": 0.12, "noise_radius_m": 25, "mag_size": 30, "reserve": 90, "reload_s": 2.5, "auto": true }
  ],
  "switch_delay_s": 0.3,
  "player": { "hp": 100, "armor": 50, "decoys": 2, "nvg_battery_s": 60, "nvg_recharge_per_s": 0.5 },
  "melee": { "range_m": 1.5, "back_arc_deg": 120 }
}
```

### 9.3 맵 JSON (ASCII 격자)
- `rows[0]`이 **맨 위(y 최대)** 줄이다. 로더가 y를 뒤집는다. 원점은 왼쪽 아래 칸의 왼쪽 아래 모서리, 1칸 = 1m, 칸 중심은 (x+0.5, y+0.5).
- `floor_rows`는 `rows`와 같은 크기이며 바닥 종류를 적는다(생략하면 모두 `w`). 벽 칸의 값은 무시한다.

| `rows` 문자 | 뜻 |
| --- | --- |
| `#` | 벽(이동·시야·총알·빛 차단, 소리 ×0.5) |
| `.` | 바닥 |
| `D` | 문(기본 닫힘) |
| `W` | 창문(이동·총알 차단, 시야·빛 투과, 소리 ×0.5) |
| `c` | 낮은 엄폐물(총알 차단, 시야·빛 투과, 이동 불가) |
| `C` | 높은 엄폐물(시야·총알·빛 차단, 이동 불가) |
| `f` | 풀숲·나무(이동만 불가, 시야·총알·빛·소리 투과. 야외 장식) |
| `P` | 플레이어 시작 |
| `T` | 목표(자료 / 폭탄 위치) |
| `X` | 탈출 지점 |
| 숫자 `1`~`9` | 적 스폰 (`enemies`의 `spawn` 번호) |

| `floor_rows` 문자 | 바닥 | 전역광 |
| --- | --- | --- |
| `w` | 나무 마루 | 실내 0.15 |
| `t` | 타일 | 실내 0.15 |
| `k` | 카펫 | 실내 0.15 |
| `o` | 야외(풀·자갈) | 야외 0.08 |

바닥 문자는 그림 파일 `tile_floor_wood` / `tile_floor_tile` / `tile_floor_carpet` / `tile_floor_outdoor`에 대응한다. 야외(`o`) 칸에 있으면 야외 환경음이 켜진다(6장 Phase 7 `AmbienceZone`).

```json
{
  "version": 2,
  "id": "map_01",
  "name": "맵 1 - 소탕",
  "mission": "eliminate",
  "target_time_s": 300,
  "ambient": { "indoor": 0.15, "outdoor": 0.08 },
  "rows": [
    "####################",
    "#P.....#...........#",
    "#......D.....1.....W",
    "#...c..#...........#",
    "#......#####D#######",
    "#######...#....#...#",
    "#..2...D..D..C.D.3.#",
    "####################"
  ],
  "floor_rows": [
    "oooooooooooooooooooo",
    "otttttokwwwwwwwwwwwo",
    "otttttowwwwwwwwwwwwo",
    "otttttowwwwwwwwwwwwo",
    "otttttoooooooooooooo",
    "ooooooowwwokkkkokkko",
    "okkkkkkwwwokkkkokkko",
    "oooooooooooooooooooo"
  ],
  "lights": [
    { "pos": [3.5, 5.5],  "radius_m": 5, "intensity": 0.9 },
    { "pos": [13.5, 5.5], "radius_m": 6, "intensity": 0.9 },
    { "pos": [12.0, 1.5], "radius_m": 4, "intensity": 0.7 }
  ],
  "enemies": [
    { "spawn": 1, "type": "guard",    "patrol": [[13.5,5.5],[17.5,5.5],[17.5,2.5]] },
    { "spawn": 2, "type": "rifleman", "patrol": [[3.5,1.5],[5.5,1.5]] },
    { "spawn": 3, "type": "guard",    "patrol": [[17.5,1.5],[14.5,1.5]] }
  ]
}
```
(위 예시는 **형식 설명용 작은 맵**이다. 실제 맵 1~4는 아래 규격으로 새로 설계한다.)

**맵 규격 (실제 제작용)**

| 맵 | 미션 | 크기(m) | 방 | 적 | 목표 시간 | 분위기·구성 |
| --- | --- | --- | --- | --- | --- | --- |
| map_01 | 소탕 | 40×30 | 4 | 4 | 5분 | 저택 실내(욕실·거실·침실) + 어두운 정원, 소총병 1 |
| map_02 | 자료 탈취 | 50×40 | 6 | 6 | 7분 | 창고(상자 엄폐물 많음) + 사무실, `T` 1, 소총병 2 |
| map_03 | 자료 탈취 후 탈출 | 60×40 | 7 | 8 | 8분 | 야간 사무동, 조명 적음, `T` 1 + `X` 1, 소총병 3 |
| map_04 | 최종 소탕 | 70×50 | 9 | 10 | 10분 | 저택 + 창고 복합, 소총병 4, 엄폐물 많음 |

맵 제작 조건(검증 대상):
- 모든 방은 문으로 연결되고 시작점에서 모든 목표·탈출·순찰 지점이 도달 가능.
- 적 스폰과 순찰 지점은 서로 벽으로 막히지 않는다. 순찰 지점 2~5개.
- 복도 폭 2칸 이상, 방마다 문 1~2개, 엄폐물(`c`·`C`) 1~3개.
- **방마다 조명 0~2개. 전체 칸 중 밝기 0.5 이상이 30~60%가 되게** 하고, 순찰 경로 중 어두운 구간이 반드시 있다.
- 시작점 시야(12m) 안에 적이 없다.
- 소탕 맵은 `T`/`X` 없어도 됨, 탈취 맵은 `T` 정확히 1, 탈출 맵은 `X` 정확히 1.

### 9.4 `enemies.json`
```json
{
  "version": 2,
  "types": [
    { "id": "guard",    "hp": 50, "fov_deg": 90, "view_m": 10, "weapon": { "damage": 12, "interval_s": 0.5,  "range_m": 12, "mag": 12, "reload_s": 1.5 }, "alert": "shout", "sprite": "guard",    "debug_color": "#E04545" },
    { "id": "rifleman", "hp": 60, "fov_deg": 70, "view_m": 14, "weapon": { "damage": 8,  "interval_s": 0.25, "range_m": 20, "mag": 30, "reload_s": 2.5 }, "alert": "radio", "sprite": "rifleman", "debug_color": "#A82020" }
  ],
  "move": { "patrol_speed": 2.0, "chase_speed": 3.5 }
}
```

### 9.5 `ai.json`
```json
{
  "version": 2,
  "gauge": { "suspect": 30, "detect": 100, "rise_per_s": 40, "hold_after_lost_s": 1.0, "fall_per_s": 15,
             "near_dist_m": 3, "near_mult": 2.0, "run_mult": 1.5, "crouch_mult": 0.5, "door_clue": 30 },
  "light": { "gauge_min_mult": 0.3, "view_min_mult": 0.5, "shot_flash_s": 0.3,
             "dark_threshold": 0.15, "dark_visible_m": 4 },
  "hearing": { "wall_mult": 0.5, "react_threshold": 0.2 },
  "memory": { "sound_clue_s": 8, "last_seen_s": 15 },
  "combat": { "reaction_delay_s": 0.5, "first_shot_s": 0.8, "accuracy": 0.6, "aim_error_deg": 5, "run_aim_mult": 1.5,
             "cover_search_radius_m": 6, "cover_hp_ratio": 0.4 },
  "patrol": { "wait_min_s": 2, "wait_max_s": 3 },
  "search": { "radius_m": 8, "points": 3, "total_s": 10 },
  "alert": { "shout_radius_m": 12, "cooldown_s": 5, "max_per_detection": 2 },
  "bark": { "show_s": 2.0, "fade_s": 0.3, "cooldown_s": 3.0, "offset_m": 0.9 },
  "nav": { "recalc_min_interval_s": 0.3, "blocked_s": 0.5, "chase_move_cells": 3, "retry_delay_s": 1.0, "max_retry": 3,
           "door_open_wait_s": 0.5, "max_paths_per_frame": 2 },
  "passage": { "overlap_after_s": 2.0 },
  "perception_phase_count": 4
}
```

### 9.6 `difficulty.json`
```json
{ "version": 1, "levels": [
  { "id": "easy",   "mult": 0.8 },
  { "id": "normal", "mult": 1.0 },
  { "id": "hard",   "mult": 1.2 }
] }
```

### 9.7 `missions.json`
```json
{
  "version": 1,
  "maps": [
    { "id": "map_01", "file": "maps/map_01.json", "mission": "eliminate" },
    { "id": "map_02", "file": "maps/map_02.json", "mission": "steal" },
    { "id": "map_03", "file": "maps/map_03.json", "mission": "steal_exit" },
    { "id": "map_04", "file": "maps/map_04.json", "mission": "eliminate" }
  ],
  "bomb": { "enabled": false, "time_limit_s": 180, "defuse_s": 5, "keep_progress": true }
}
```

### 9.8 `barks.json`
```json
{
  "version": 1,
  "barks": [
    { "when": "investigate_noise", "lines": ["뭐지, 방금 그 소리?", "누구 있어?"] },
    { "when": "investigate_sight", "lines": ["저기... 뭐가 움직였나?", "잘못 봤나?"] },
    { "when": "detect",            "lines": ["저기 있다!", "침입자다!", "거기 서!"] },
    { "when": "hit",               "lines": ["윽!", "맞았다!"] },
    { "when": "reload",            "lines": ["장전 중! 엄호해!"] },
    { "when": "lost",              "lines": ["어디 숨었어...", "놓쳤다, 찾아!"] },
    { "when": "search_end",        "lines": ["기분 탓인가.", "아무도 없군."] },
    { "when": "alert_received",    "lines": ["연락 받았다, 간다."] }
  ]
}
```

### 9.9 `dialogue.json`
```json
{
  "version": 1,
  "speakers": [
    { "id": "accomplice", "name": "조력자", "portrait": "accomplice" },
    { "id": "player",     "name": "나",     "portrait": "player" }
  ],
  "dialogues": [
    { "id": "map_01_briefing", "lines": [
      { "speaker": "accomplice", "text": "저택 안에 경비가 넷. 전부 정리하면 끝이야.", "choices": ["[...]"] },
      { "speaker": "accomplice", "text": "불 켜진 방에선 금방 들켜. 어두운 쪽으로 다녀.", "choices": ["알았어.", "[...]"] },
      { "speaker": "accomplice", "text": "할 수 있겠어?", "choices": ["[...]"] }
    ] }
  ]
}
```

### 9.10 `save.json` (사용자 저장)
```json
{
  "version": 1,
  "settings": { "master": 1.0, "sfx": 1.0, "bgm": 0.7, "fullscreen": true, "width": 1920, "height": 1080 },
  "records": [
    { "map": "map_01", "difficulty": "normal", "clear_time_s": 183.412, "detections": 1, "kills": 3 }
  ]
}
```
키는 소문자 snake_case, `version`은 정수. 구버전·누락 키는 기본값 + 경고 로그.

---

## 10. 화면 구성

| 화면 | 내용 |
| --- | --- |
| 메인 메뉴(`MainMenu` 씬) | 제목, 시작(→ 미션 선택), 설정(오버레이), 크레딧, 종료. 배경은 어두운 맵 한 장면 + 천장등 하나 |
| 크레딧 | `credits.txt` 전문을 스크롤 표시(11.5), 닫기 |
| 미션 선택 | 맵 4개 카드(이름·미션 종류·최고 기록), 난이도, 시작 |
| 게임(`Game` 씬) | 월드(조명·탐색 기억) + HUD |
| **HUD 좌하단 통합 패널** | ① 체력(빨강 `#C8323C`)·방탄(하늘색 `#8FD3F0`) 2줄 막대 + 방탄 아이콘 ② 밝기 미터(전구 아이콘 + 초록 눈금) ③ 소음 미터(스피커 아이콘 + 노랑 눈금) ④ 무기 칸: 무기 그림(빨강 윤곽), `탄창 \| 예비`, 탄창 눈금, 무기 이름(금색 테두리). 패널 오른쪽에 자세 실루엣. 야간 투시 켜면 무기 칸에 배터리 % |
| HUD 좌상단 / 우상단 | 목표 문구·타이머(작게) / 전역 경계 3단계(평상 회색·경계 노랑·전투 빨강) |
| 월드 위 UI | 조준선(흰 꺾쇠), 상호작용 안내(`[SPACE] 열기`), 적 머리 위 `?`(노랑)·`!`(빨강)·대사(흰 글자) |
| 대화창 | 하단 전체 폭, 위쪽 1px 빨강 선, 초상화 + 빨강 이름표, 대사, 번호 선택지(선택된 줄 빨강 막대) |
| 일시정지 | 이어하기·설정·메인 메뉴. 소리·AI·타이머 정지 |
| 설정(오버레이) | 음량 3개, 전체화면, 해상도, 적용·닫기 |
| 결과 | 성공/실패, 클리어 시간, 발각 횟수, 처치 수, 최고 기록 갱신 여부, 다시 하기·다음 미션·메인 메뉴 |
| 디버그(F3) | 적 ID·상태·게이지, 시야 부채꼴, A* 경로선, 소리 반경, 밝기 히트맵, 피아 색 원, FPS·AI ms |

UI는 **uGUI**(Canvas Screen Space Overlay, Canvas Scaler 1920×1080 Scale With Screen Size). 픽셀 UI 이미지는 정수 배율(×2)로 표시한다. 폰트는 11.3을 따른다. 한글이 깨지면 DECISIONS에 적고 영문 라벨로 대체.

---

## 11. 에셋

> 외부 에셋은 이미 `Art/`·`Audio/`·`Fonts/`에 들어 있다(2026-11-03). 게임은 이 파일을 쓰도록 만들되, 파일이 빠져도 실행되어야 하므로 아래 "코드 생성" 버전도 함께 만든다. 파일을 바꾸려면 **같은 이름의 파일로 덮어쓰고 `Tools/Build Project`를 다시 실행**한다. 방식은 `AssetCatalog`(아래 11.0)이며 `Resources`/`Addressables`는 쓰지 않는다. 실제 파일·크기·원본 전체 목록은 `docs/ASSET_DIRECTION.md` 3장, Unity 기능으로 만드는 연출 값은 같은 문서 2.4(= `game.json`의 `lighting`).
>
> - **변형 파일**: 이름에 `_*`가 붙은 항목(`cover_low_*`, `cover_high_*`, `foliage_*`, `blood_*`, `corpse_*`, `muzzle_flash_*`)은 같은 접두사 파일을 모두 모은다. 맵 소품은 칸 좌표 해시로 고르고(난수 안 씀), 혈흔·총구 화염은 연출 난수원으로 고른다. 시체는 `corpse_guard`·`corpse_rifleman`을 적 종류에 맞춰 쓴다. `hit_blood_*`는 변형이 아니라 번호 순서로 재생하는 프레임이다.
> - **캐릭터**: `chars/{player,guard,rifleman}_{idle,shoot}.png`는 1프레임, `_walk.png`는 가로로 이어 붙인 32×32 2프레임, `player_melee.png`는 1프레임이다. 걷기는 초당 8프레임, 사격 프레임은 0.1초 표시. 모두 오른쪽(+X)을 보고, 파일이 없으면 코드 생성 1프레임.
> - **손에 든 총**: 캐릭터 그림에는 총이 없다. 현재 무기의 `chars/held_{pistol,silenced_pistol,rifle}.png`를 아래쪽 손 위치에 겹쳐 그린다(`ASSET_DIRECTION.md` 3.1 "캐릭터 사용법"). 경비병은 권총, 소총병은 소총.
> - **벽 테두리**: `tile_wall.png`에는 1px 선이 없다. 바닥과 맞닿은 변에만 맵 렌더러가 `#3A3A44` 1px 선을 그린다.
> - **UI 패널**: `ui/panel_dark`(HUD), `panel_gold`(무기 이름 칸), `panel_dialogue`(대화창)는 테두리 2px 9-slice 16×16이다. 임포트 때 Sprite Border를 2로 지정한다.
> - 외부 파일이 있는 항목과 없는 항목이 섞여도 동작해야 한다.

### 11.0 에셋 연결 방식 (`AssetCatalog`)
Unity 빌드는 `Assets/` 폴더를 실행 중에 읽지 못하므로, 파일 이름으로 찾는 연결표를 둔다.
- `Core/AssetCatalog`: `ScriptableObject`. 이름(확장자 제외) → `Sprite[]`(시트·변형 포함) / `AudioClip` / `TMP_FontAsset` 목록을 직렬화해 가진다. `GameManager`와 `MainMenu` 씬의 부트스트랩이 참조하므로 빌드에 자동 포함된다.
- `Editor/AssetCatalogBuilder`: `Art/`(하위 폴더 포함)·`Audio/`·`Fonts/`를 훑어 `AssetCatalog.asset`을 다시 만든다. `Tools/Build Project`가 호출하고, 에셋이 바뀌면 `AssetPostprocessor`가 자동 갱신한다. 이때 스프라이트는 PPU 32·Point·압축 없음, 걷기 시트(`chars/*_walk`)는 32×32로 자르는 임포트 설정을 함께 적용한다. `Fonts/`의 `*-OFL.txt`는 라이선스 파일이므로 경고 없이 건너뛴다.
- 조회: `AssetCatalog.GetSprite(name)` / `GetClip(id)` / `GetFont()`. 없으면 `ProceduralSprites`·`AudioManager`의 코드 생성 에셋을 돌려준다. 조회 결과는 시작할 때 한 번 캐시한다.
- 이름 규칙·크기는 `ASSET_DIRECTION.md` 3장. 알 수 없는 이름의 파일은 무시하고 경고 로그.
- 테스트: 파일이 없을 때 폴백, 이름 충돌(같은 이름 두 개)은 먼저 찾은 것 사용 + 경고(EditMode `ConfigTests`에 포함).

### 11.1 그래픽 (픽셀아트 32px/m, 코드 생성 기본값)

| 에셋 | 코드 생성 기본 | 비고 |
| --- | --- | --- |
| 바닥 나무 / 타일 / 카펫 / 야외 | `#6B4A2B` / `#8A8A8A` / `#2F7C7A` / `#1E2A22` + 2px 격자 무늬 | 조명이 실제 밝기를 정함 |
| 벽 | `#0B0B0E` + 안쪽 1px `#3A3A44` | ShadowCaster2D |
| 창문 | 벽 + 가운데 흰 막대 `#E8E4DA` | |
| 문 닫힘 / 열림 | 금색 `#C9A24A` 얇은 막대 / 90° 회전 | |
| 낮은 / 높은 엄폐물 | `#7A5532` 줄무늬 / `#4A3220` | 소파·상자 / 옷장·선반 |
| 풀숲·나무 `f` | 진한 녹색 원 `#1F4A2A` | 야외 장식, 이동만 막음 |
| 천장등 | 노란 점 `#FFC873` + Point Light2D | 반경·세기는 맵 JSON |
| 플레이어 | 밝은 회색 원 `#D8D8D8` + 총 막대 | 지름 0.8m |
| 경비병 / 소총병 | `#7A3B2E` / `#4E5A3A` 원 + 총 막대 | 디버그에서만 빨강 원 덧그림 |
| 시체 | 같은 색 눕힌 타원 + 혈흔 | 미션 끝까지 남음 |
| 혈흔 / 탄피 / 총구 화염 | `#6E0F0F` 얼룩 / `#D9B24C` 2px / 노란 별 0.05초 + 빛 `#FFD38A` 3m | 풀 최대 512 |
| 총알 궤적 | `#FFE680` 선, 0.1초 | |
| 목표 / 폭탄 / 탈출 / 유인탄 | 노랑 서류 `#F2C230` / 빨간 상자 `#C8323C` / 초록 윤곽 `#37C871` / 주황 원 `#FF9A3C` | |
| 탐색 기억 오버레이 | 미탐색 알파 1.0, 기억 알파 0.65(회색조), 현재 시야 알파 0 | 조명 위에 덮음 |
| 조준선 | 흰 꺾쇠 4개 | |
| 인지 아이콘 | `?` 노랑 / `!` 빨강 | |
| HUD 아이콘 (방탄·밝기·소음) | 16×16 단색 기호 | |
| 자세 실루엣 (서기·걷기·달리기·웅크리기) | 32×64 흰 사람 모양 | |
| HUD 무기 그림 | 64×32 빨강 윤곽 총 | |
| 대화 초상화 | 32×40 사각 + 이름 첫 글자 | `accomplice`·`player` 임시본이 들어 있음 |
| 야간 투시 필터 | 녹색 `#7CFF9A` 곱하기 + 노이즈 + 비네팅 | |
| 필름 그레인 | 노이즈 텍스처 256×256, 알파 0.04 | 야간 투시와 같은 텍스처 |
| UI 패널 | `#0E0E12` 알파 0.85, 글자 `#E8E4DA`, 강조 `#C8323C`, 테두리 `#C9A24A` | |

### 11.2 사운드 (없으면 코드로 톤 생성: `AudioClip.Create` + 사인/노이즈)

아래 길이는 코드 생성 톤의 기준이다. 들어 있는 외부 파일의 실제 길이는 `ASSET_DIRECTION.md` 3.2.

| ID | 용도 | 길이 | 코드 생성 방법 |
| --- | --- | --- | --- |
| `sfx_shot_pistol` / `sfx_shot_silenced` / `sfx_shot_rifle` | 사격 | 0.08~0.12s | 노이즈 버스트(소음권총은 작게, 소총은 저음 추가) |
| `sfx_reload` / `sfx_empty` | 재장전 / 빈 탄창 | 0.4s / 0.05s | 클릭 두 번 / 한 번 |
| `sfx_casing` | 탄피 | 0.1s | 고음 금속 틱 |
| `sfx_footstep` | 발소리 | 0.08s | 저역 노이즈 톡 |
| `sfx_door` | 문 | 0.2s | 저음 슬라이드 |
| `sfx_hit_player` / `sfx_hit_armor` / `sfx_hit_enemy` | 피격 | 0.1~0.15s | 하강 톤 / 금속 톤 / 짧은 톤 |
| `sfx_takedown` | 근접 제압 | 0.2s | 둔탁한 소리 |
| `sfx_alert` | 발각 경고음 | 1.0s | 880Hz 두 번 |
| `sfx_bark` | 대사 표시음 | 0.15s | 낮은 웅얼(저역 노이즈 + 사인) |
| `sfx_decoy` | 유인탄 | 0.3s | 금속 틱 반복 |
| `sfx_pickup` | 목표 확보 | 0.3s | 상승 톤 |
| `sfx_nvg_on` / `sfx_nvg_off` | 야간 투시 | 0.2s | 상승 / 하강 전자음 |
| `sfx_dialogue_next` | 대화 넘김 | 0.05s | 클릭 |
| `sfx_success` / `sfx_fail` / `sfx_ui_click` | 결과·버튼 | 1.0 / 1.0 / 0.05s | 아르페지오 / 클릭 |
| `bgm_menu` / `bgm_game` | 배경음 | 루프 20s / 30s | 저음 패드 |
| `amb_outdoor_night` | 야외 환경음(배경음 위에 겹침) | 루프 11s | 고역 필터 노이즈 + 짧은 고음 펄스 반복(풀벌레) |

외부 효과음·BGM은 11.5 라이선스 기준에 맞는 것만, 파일 이름을 위 ID로 맞춰 효과음은 `Audio/sfx/`, 배경음·환경음(`bgm_*`, `amb_*`)은 `Audio/bgm/`에 넣고 출처를 `ASSET_CREDITS.md`에 적는다. 위 ID는 모두 파일이 들어 있고, 확장자는 `.ogg`·`.wav`·`.mp3`를 모두 찾는다. 각 ID의 원본은 `ASSET_DIRECTION.md` 3.2.

### 11.3 폰트
`Fonts/Galmuri11.ttf`로 TMP 폰트 에셋 `Galmuri11 SDF.asset`(Atlas Render Mode: Raster, Point, Sampling Point Size 12의 배수, 기본 24)을 만든다. 글자 크기도 12의 배수로 쓴다. Galmuri11의 도트 격자가 12px이라 다른 크기에서는 픽셀이 어긋난다. 없으면 Unity 기본 폰트로 표시하고 `DECISIONS.md`에 적는다.

HUD·대화창·메뉴·적 대사는 같은 폰트 하나로 통일한다. 디버그 오버레이만 `NotoSansKR-Regular.ttf`를 쓴다(없으면 위 폰트).

### 11.4 에셋 준비 담당 메모
김성범이 픽셀아트 제작·에셋 수집·출처 확인을 맡는다. 기본 방침은 **바닥·벽·가구는 무료 타일을 고쳐 쓰고, 캐릭터는 dyru 캐릭터를 재채색·보강, 초상화·HUD 패널은 직접 그린다**(Aseprite). 생성 도구는 수업 규정 확인 전에는 쓰지 않는다. 목표 화면은 스타일 참고용이며 그 게임의 그림·글꼴·대사를 가져오지 않는다. Claude Code는 **들어 있는 파일을 연결**하고 코드 생성 폴백을 유지한다. 초상화·패널·자세 실루엣 등은 임시본이 들어 있으므로(`ASSET_DIRECTION.md` 3.1 "코드로 그림"), 직접 그린 그림이 나오면 같은 이름으로 덮어쓴다.

### 11.5 라이선스와 크레딧
- **허용**: CC0 1.0(우선), CC BY 3.0/4.0(크레딧 표기 조건), 폰트는 SIL OFL 1.1.
- **금지**: CC-BY-SA, GPL 단독, 비상업(NC), 재배포 금지, 라이선스 표기 없음, 유료. 공개 GitHub 저장소에 원본 파일이 올라가기 때문이다. 여러 라이선스 중 고를 수 있으면 허용 라이선스로 쓴다(예: Dark Ambience Loop는 CC BY 3.0으로 사용).
- 에셋을 넣을 때 다운로드 페이지에서 라이선스를 다시 확인하고 `ASSET_CREDITS.md`의 상태를 `후보` → `사용`으로 바꾼다(날짜·확인한 사람). `사용(재확인 필요)` 항목은 사람이 페이지를 확인한 뒤 `사용`으로 바꾼다.
- **크레딧 화면**: `StreamingAssets/Config/credits.txt`를 그대로 보여 준다. 내용은 `ASSET_CREDITS.md` 5장 문구(CC-BY 지정 문구 + CC0 감사 표시 + 폰트)와 같게 유지한다. CC-BY 에셋은 이 문구가 빠지면 쓸 수 없다. 파일이 없으면 "외부 에셋 없음 — 모든 그래픽·사운드는 코드로 생성"을 표시하고 경고 로그.
- Claude Code는 실제로 `Assets/_Project/`에 들어 있는 에셋만 `credits.txt`에 남긴다. `credits.txt`는 `ASSET_CREDITS.md` 5장 문구로 이미 만들어 두었다(Dark Ambience Loop 문구는 넣은 뒤에만 더한다).

---

## 12. 일정 (참고용)

| 시점 | 목표 |
| --- | --- |
| 중간 범위 | Phase 0~6: 맵 1 소탕을 처음부터 끝까지 시연, ref_01과 비슷한 화면(조명·HUD·대사·전투 흔적) |
| 기말 범위 | Phase 7~9: 맵 4종, 대화, 밝기 감지, UI, 저장, S 기능 |
| 마감 | Phase 10: 성능·재현·검수 기록, 목표 화면 비교, 빌드 |

중간발표 시연 범위 = 맵 1, 권총, 순찰·조사·추격·교전 AI, 문(Space), 엄폐, 근접 제압, 인지 표시·대사, 화면 조명, 통합 HUD(밝기 미터 제외), 전투 흔적, 디버그 오버레이. 시간이 부족하면 S 기능을 Phase 9 뒤쪽부터 뺀다.

---

## 13. 테스트·검수·재현

### 13.1 EditMode 테스트 (필수)

| 테스트 | 확인 |
| --- | --- |
| GridTests | DDA가 벽·닫힌 문·높은 엄폐물에서 멈춤, 낮은 엄폐물·창문은 시야 통과·총알 차단, 풀숲은 시야·총알 통과, 모서리 |
| AStarTests | 최단 경로, 대각 모서리 금지, 문 비용, 창문·풀숲 통과 불가, 도달 불가, 재시도 |
| SoundTests | 거리별 세기, 벽 1·2개, 창문, 닫힌/열린 문, 임계값 0.2 |
| LightGridTests | 전역광만 있을 때 값, 조명 거리 감쇠, 벽·닫힌 문 뒤는 조명 제외, 문 열면 부분 재계산 결과가 전체 재계산과 같음, 사격 밝기 0.3초 |
| AwarenessTests | 게이지 상승·유지·하강, 배율(근접·달리기·웅크리기·밝기), 3m 이내 밝기 무시, 소리로 30 |
| HealthTests | 방탄 먼저 감소, 방탄 초과 피해가 체력으로 넘어감, 사망 1회 |
| FsmTests | 7.7 전환 표 모든 줄, 우선순위 |
| MissionResultTests | 7.9 네 가지 동시 사건, 한 번만 판정 |
| DetectionCountTests | 동시 발견 1회, 전원 순찰 복귀 후 +1, 경보 제외 |
| RecordsTests | 비교 순서, 동점 유지, 미션·난이도별, 대화 시간 제외 |
| SaveTests | 실패 재시도, `.corrupt`, `.bak`, 동의 후 초기화 |
| MapValidatorTests | 4개 맵 도달 가능성·목표 개수·문 연결·밝은 칸 비율 30~60% |
| ConfigTests | `barks.json`·`dialogue.json`·`game.json`의 `lighting` 누락 시 기본값, `credits.txt` 없을 때 기본 문구, 대사·총구 화염·혈흔 선택이 AI 난수원을 쓰지 않음 |

### 13.2 `docs/VERIFY.md` 체크리스트
- [ ] 이동 모드별 소음 반경이 6m·9m·3m이고 HUD 소음 미터·자세 실루엣이 따라 바뀐다
- [ ] 피해가 방탄 → 체력 순서로 줄고 HUD 막대 두 개가 맞다
- [ ] 벽·닫힌 문·창문·엄폐물 뒤 적은 맞지 않고 적끼리 피해가 없다
- [ ] 근접 제압은 뒤 120°·1.5m 안 미발각 적에게만 된다
- [ ] 혈흔·탄피·시체가 남고 512개 이상 쌓여도 메모리가 늘지 않는다
- [ ] 벽 뒤는 보이지 않고, 방문 지형이 회색조로 남으며 재시작하면 지워진다
- [ ] 밝은 칸에서 어두운 칸보다 빨리 발견되고, 사격하면 순간 밝아진다(F3 게이지·히트맵)
- [ ] 같은 상황의 적 상태 전환이 FSM 표와 같다
- [ ] 소리는 벽·닫힌 문·창문 1개마다 절반, 열린 문은 그대로
- [ ] A* 경로가 벽 모서리를 대각선으로 통과하지 않고 닫힌 문은 열고 지나간다
- [ ] 대사가 상태 전환 때 뜨고 적 1명당 3초에 1회를 넘지 않으며, 대사를 꺼도 상태 전이 로그가 같다
- [ ] 문 앞 1.2m에서 `[SPACE] 열기`가 뜨고 Space로 열린다
- [ ] 브리핑 중 타이머·AI가 멈추고, 클리어 시간에 대화 시간이 빠진다
- [ ] 같은 프레임 승패 4가지 결과가 표와 같다
- [ ] 여러 적이 동시에 발견해도 발각 1회, 경보로 안 경우 제외
- [ ] 저장 손상 시 `.bak` 복원 또는 동의 후 초기화
- [ ] 적 20명·조명 켠 상태에서 60fps·AI 2ms 이하, 33ms 초과 프레임 0
- [ ] 같은 맵·시드·입력으로 3회 실행한 상태 전이 로그가 같다
- [ ] 30분 연속 플레이 오류·메모리 증가 없음
- [ ] 맵 4종 모두 성공·실패로 끝낼 수 있다
- [ ] 목표 화면 3장과 같은 구도 스크린샷에서 HUD·대화창·조명 대비가 같다
- [ ] 야외(`o`) 칸에 들어가면 환경음이 페이드 인, 실내로 나오면 페이드 아웃된다
- [ ] `Art/`·`Audio/`에 같은 이름 파일을 넣고 `Tools/Build Project`를 실행하면 교체되고(Windows 빌드에서도), 빼면 코드 생성 에셋으로 돌아간다
- [ ] 크레딧 화면 문구가 `ASSET_CREDITS.md` 5장과 같고, 사용한 CC-BY 에셋의 지정 문구가 모두 있다

### 13.3 고정 시드 재현
`fixed_seed_enabled`가 true면 AI 난수원을 `fixed_seed`로 초기화한다. 입력을 파일(`docs/evidence/input_replay.json`)로 기록·재생해 같은 입력으로 3회 실행, `StateTransitionLogger` 출력을 비교한다. 대화는 리플레이 때 자동으로 넘긴다.

### 13.4 증거 (`docs/evidence/`)
결과표, 상태 전이 로그 3회분, 주요 스크린샷, **목표 화면 비교 이미지 3장**(`compare_01_combat.png` 등, 왼쪽 목표·오른쪽 우리 게임), 미션별 클리어 영상 1건(불가능하면 DECISIONS에 사유 + 로그·스크린샷).

---

## 14. 완료 정의 (Definition of Done)

1. `Tools/Build Project` 한 번으로 씬·프리팹·URP 2D 설정이 생성되고, 컴파일 오류와 경고성 예외가 없다.
2. Windows 빌드가 실행되어 메인 메뉴 → 미션 선택 → 브리핑 → 플레이 → 결과 → 메인 메뉴 흐름이 동작한다.
3. 맵 4종 모두 성공·실패로 끝낼 수 있다. M 기능은 전부 동작한다.
4. S 기능은 Phase 9 순서대로 가능한 만큼 구현되어 있고, 못 한 것은 `DECISIONS.md`에 사유가 있다.
5. EditMode 테스트가 모두 통과한다.
6. 7.14 성능 기준과 13.2 체크리스트 결과가 `VERIFY.md`에 기록되어 있다.
7. 에셋이 없어도 실행되며, 외부 에셋을 쓴 경우 `ASSET_CREDITS.md`에 출처가 있고(상태 `사용`), CC-BY 에셋의 지정 문구가 크레딧 화면에 나온다.
8. `README.md`에 실행·조작(7.1)·빌드·폴더 구조 설명이 있다.
9. git 이력에 Phase별 커밋이 남아 있다.
10. 목표 화면 비교 이미지 3장이 `docs/evidence/`에 있다.

## 15. 금지 사항 요약
인질·섬광·잠금 문·측면 이동·관통·피격 경직·대화 분기·시체 발견 반응 구현 금지 / NavMesh·외부 AI 패키지 금지 / `Physics2D`로 시야·소리·총알·밝기 판정 금지 / 수치 하드코딩 금지 / 구독 해제 누락 금지 / 11.5 허용 라이선스(CC0·CC BY·OFL) 밖의 에셋(CC-BY-SA·GPL·NC·재배포 금지·표기 없음·유료)과 목표 화면 게임의 그림 사용 금지 / CC-BY 에셋의 크레딧 누락 금지 / 대사·연출이 AI 판단이나 AI 난수에 영향 주는 것 금지 / 사용자에게 질문하며 멈추기 금지(문서 기준으로 결정하고 기록).

---

**지금 바로 Phase 0부터 시작하라.**
