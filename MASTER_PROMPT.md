# MASTER PROMPT — 2D 탑뷰 전술 잠입 슈팅 게임 (Unity 6)

> 이 문서 전체를 Claude Code에 입력하면, Claude Code는 이 문서만 보고 처음부터 끝까지 게임을 완성한다.
> 문서에 적힌 값은 모두 **확정된 기본값**이다. 적히지 않은 세부 사항은 "합리적인 가장 단순한 방식"으로 스스로 정하고 `docs/DECISIONS.md`에 한 줄씩 기록한다. 사용자에게 되묻지 않는다.

---

## 0. 작업 지시 (Claude Code에게)

너는 이 프로젝트의 단독 개발자다. 아래 규칙을 지켜 작업한다.

1. 이 문서의 **6장 개발 순서**를 Phase 순서대로 진행한다. Phase 하나가 끝날 때마다 해당 Phase의 "완료 조건"을 확인하고, 통과하면 git 커밋한다(커밋 메시지는 `phase-N: 요약`).
2. **외부 파일 없이도 게임이 실행되어야 한다.** 이미지·소리 에셋이 없으면 코드로 만든 임시 에셋(11장)을 쓴다. 에셋 파일을 나중에 넣으면 교체되도록 만든다.
3. Unity 에디터를 직접 조작할 수 없다고 가정하고 만든다. 씬·프리팹·ScriptableObject를 손으로 쓰지 말고, **`Assets/_Project/Editor/ProjectBuilder.cs`** 에 메뉴 항목(`Tools/Build Project`)을 만들어 씬과 프리팹을 코드로 생성한다. Unity가 설치되어 있으면 `-batchmode -executeMethod`로 실행해 확인한다.
4. 게임 로직은 가능한 한 **MonoBehaviour와 분리된 순수 C# 클래스**로 만들고(격자, A*, 감지 계산, 소리 감쇠, 승패 판정, 기록 비교), EditMode 테스트를 함께 작성한다.
5. 모든 수치는 코드에 박지 말고 `StreamingAssets/Config/*.json`에서 읽는다(7장 값이 기본값). 파일이 없거나 값이 빠지면 코드 안의 기본값으로 채우고 경고 로그를 남긴다.
6. 코드 주석과 로그는 한국어, 클래스·변수 이름은 영어로 쓴다.
7. 막히는 부분이 있으면 가장 단순한 대안으로 구현하고 `docs/DECISIONS.md`에 이유를 적은 뒤 계속 진행한다. 범위 밖 기능(2.2)은 만들지 않는다.
8. 마지막에는 `README.md`에 실행·조작·빌드 방법을 쓰고, `docs/VERIFY.md`의 검수 체크리스트를 모두 실행해 결과를 기록한다.

---

## 1. 프로젝트 개요

- **장르**: 2D 탑뷰 전술 잠입·슈팅. 플레이어는 혼자 침투해 목표를 수행하고 탈출한다.
- **핵심 재미**: 소리·시야·문·엄폐를 이용해 적에게 들키지 않고 처리하거나, 들키면 교전한다. 적은 보고 들은 정보에만 반응한다.
- **흐름**: 침투 → 탐색 → 목표 수행 → 탈출 → 결과 화면.
- **플레이 시간**: 한 판 5~10분. 맵 4종.
- **조작 방식**: 키보드(이동) + 마우스(조준·사격). 마우스 커서가 조준선이다.
- **화면**: 기준 1920×1080(16:9), 단순 도형 스타일 탑뷰, 어두운 배경.
- **AI**: 학습형 AI 없음. 규칙 기반 FSM + 행동 트리 + 직접 구현한 A*.

## 2. 범위

### 2.1 포함 (구현 대상)
플레이어 이동·조준·사격, 무기 3종, 재장전, 유인탄, 근접 제압, 시야 차폐와 탐색 기억, 문, 엄폐물, 적 AI(시각·청각·인지 게이지·FSM·교전·순찰·수색·A*·동료 경보·문 흔적·난이도·통로 충돌), 미션(소탕·자료 탈취·자료 탈취 후 탈출·폭탄 해체), 승패·발각·기록, HUD, 메뉴, 설정, 일시정지, 결과 화면, 저장·복구, 디버그 오버레이, 고정 시드 재현 모드.

### 2.2 제외 (만들지 않는다)
인질 구출, 섬광탄, 잠금 문·열쇠·문 파괴, 적의 측면 이동(상대 옆으로 돌아가기), 문 경로 우회 소리 전파, 총알 관통, 적끼리 피해, 피격 경직·밀림·무적 시간, 마우스 감도·키 변경 설정, 창 크기 자유 변경, 네트워크·서버·계정·클라우드, 학습형 AI, 100×100 대형 맵.

### 2.3 우선순위
- **M(필수)**: 기말 전에 반드시 완성. **S(권장)**: M 이후. **C(여유)**: 하지 않는다.
- 폭탄 해체는 S. M이 모두 끝난 뒤 구현한다.

## 3. 기술 스택·환경

| 항목 | 값 |
| --- | --- |
| 엔진 | Unity 6 — 6000.3.24f1 (프로젝트 버전이 다르면 설치된 6000.x로 두고 DECISIONS에 기록) |
| 플랫폼 | Windows PC 스탠드얼론(64bit), 싱글 플레이 |
| 렌더 | 2D (Universal Render Pipeline 2D 템플릿 또는 Built-in 2D), 카메라 Orthographic |
| 언어 | C# |
| 입력 | 레거시 `UnityEngine.Input` 사용. `ProjectSettings`의 Active Input Handling을 **Both**(`activeInputHandler: 2`)로 설정 |
| 데이터 | JSON (`JsonUtility` 또는 Unity 내장 직렬화로 충분하면 그것을 쓰고, 배열·딕셔너리가 필요하면 `Newtonsoft`를 쓰지 말고 래퍼 클래스를 만든다) |
| 길찾기 | **A* 직접 구현** (Unity NavMesh·외부 길찾기 에셋 금지) |
| 물리 | 총알·시야·소리는 `Physics2D`를 쓰지 않고 **자체 격자 레이캐스트(DDA)** 로 계산한다. 캐릭터 이동 충돌은 격자 기반 충돌 또는 `Rigidbody2D`+`BoxCollider2D` 중 단순한 쪽을 쓴다 |
| 테스트 | Unity Test Framework(EditMode) |
| 버전 관리 | git, 아래 4장 구조 |

---

## 4. 저장소 구조 (GitHub)

```
repo-root/
├─ README.md                      # 실행·조작·빌드 방법, 스크린샷
├─ CLAUDE.md                      # 이 문서 0장 요약 + 코딩 규칙 (Claude Code가 항상 읽음)
├─ .gitignore                     # Unity 표준 (Library/ Temp/ Logs/ obj/ Build/ *.csproj *.sln 등)
├─ .gitattributes                 # *.png *.wav *.ogg binary
├─ docs/
│  ├─ MASTER_PROMPT.md            # 이 문서
│  ├─ DECISIONS.md                # 구현 중 스스로 내린 결정 기록
│  ├─ VERIFY.md                   # 검수 체크리스트와 결과표
│  ├─ ASSET_CREDITS.md            # 사용한 외부 에셋 출처·라이선스
│  └─ evidence/                   # 로그, 스크린샷, 클리어 영상
├─ Packages/manifest.json
├─ ProjectSettings/
└─ Assets/
   └─ _Project/
      ├─ Scripts/
      │  ├─ Core/                 # GameManager, SceneFlow, EventBus, Events, Rng, Config 로더
      │  ├─ Grid/                 # GridMap, TileType, Raycast(DDA), LineOfSight, SoundPropagation
      │  ├─ Player/               # PlayerController, PlayerMotor, PlayerNoise, PlayerHealth
      │  ├─ Weapons/              # WeaponData, WeaponController, Hitscan, Decoy(유인탄), MeleeTakedown
      │  ├─ Combat/               # Health, DamageRequest, CoverObject
      │  ├─ Vision/               # PlayerVision, FogOfWar(탐색 기억), VisionRenderer
      │  ├─ Map/                  # MapData, MapLoader, Door, MapRegistry
      │  ├─ Mission/              # MissionManager, Objective(Eliminate/Steal/Bomb), ResultEvaluator, Records
      │  ├─ AI/
      │  │  ├─ Perception/        # SightSensor, HearingSensor, AwarenessGauge, ClueMemory
      │  │  ├─ FSM/               # EnemyBrain, EnemyState(Patrol/Investigate/Chase/Engage/Search/Dead)
      │  │  ├─ Behavior/          # BehaviorTree 노드(Selector/Sequence/Condition/Action), CombatTree
      │  │  ├─ Navigation/        # AStarPathfinder, PathFollower, PatrolRoute, SearchPointPicker, PassageYield
      │  │  ├─ Alert/             # AlertSystem(외침·무전), GlobalAlertLevel
      │  │  └─ EnemyController.cs # 위 요소를 묶는 컴포넌트
      │  ├─ UI/                   # HUD, AwarenessIcon, MainMenu, MissionSelect, SettingsOverlay, PauseMenu, ResultScreen
      │  ├─ Audio/                # AudioManager, SfxId, NoiseEmitter
      │  ├─ Data/                 # SaveSystem, SettingsData, DifficultyData
      │  ├─ Debug/                # DebugOverlay, StateTransitionLogger, PerfProbe
      │  └─ Util/                 # MathUtil, ProceduralSprites, ObjectPool
      ├─ Editor/                  # ProjectBuilder.cs (씬·프리팹 생성), MapValidatorMenu.cs
      ├─ Scenes/                  # MainMenu.unity, Game.unity  (ProjectBuilder가 생성)
      ├─ Prefabs/                 # Player, Enemy_Guard, Enemy_Rifleman, Door, Bullet Tracer, DecoyGrenade, UI 프리팹
      ├─ Art/                     # (비워둠) 외부 스프라이트를 넣는 자리. 없으면 코드 생성 스프라이트 사용
      ├─ Audio/                   # (비워둠) sfx/ bgm/ 폴더. 없으면 코드 생성 톤 사용
      ├─ Fonts/                   # (비워둠) 무료 한글 폰트. 없으면 Unity 기본 폰트
      └─ StreamingAssets/Config/  # game.json, weapons.json, enemies.json, ai.json, difficulty.json,
                                  # missions.json, maps/map_01.json ~ map_04.json
Assets/Tests/EditMode/            # GridTests, AStarTests, SoundTests, AwarenessTests, MissionResultTests, RecordsTests, SaveTests
```

`StreamingAssets`는 `Assets/StreamingAssets/Config/`에 두어야 Unity가 인식한다. 위 트리에서는 보기 좋게 묶었지만, 실제 경로는 **`Assets/StreamingAssets/Config/`** 이다.

---

## 5. 아키텍처

### 5.1 원칙
- 시스템끼리는 **이벤트 버스**로만 대화한다. 이벤트 구조체는 `Core/Events.cs` 한 파일에 모은다(공통 이벤트 정의 파일).
- 모든 구독은 `OnEnable`에서 하고 `OnDisable`에서 반드시 해제한다.
- 매 프레임 일을 전부 하지 않는다. 감지와 길찾기는 **적마다 시간 간격을 엇갈려** 처리한다(5.4).
- 난수는 `Rng` 한 곳에서만 만든다. 고정 시드 모드면 시드를 고정한다.

### 5.2 이벤트 목록 (Core/Events.cs)

| 이벤트 | 필드 | 보내는 쪽 → 받는 쪽 |
| --- | --- | --- |
| `NoiseEvent` | position, kind, radius, sourceId, time | 플레이어·무기·유인탄 → 적 HearingSensor |
| `DamageEvent` | targetId, amount, attackerId, time | Hitscan → Health |
| `DeathEvent` | entityId, isPlayer, time (1회만) | Health → Mission·AI·UI |
| `EnemyStateChangedEvent` | enemyId, from, to, reason, time | EnemyBrain → UI·Mission·Debug |
| `DetectionEvent` | enemyId, time (게이지가 100에 처음 닿을 때) | AwarenessGauge → Mission(발각 집계)·Alert |
| `AlertEvent` | kind(Shout/Radio), position, time, senderId | AlertSystem → 수신 적 |
| `DoorChangedEvent` | doorId, isOpen, actorId, time | Door → Vision·Nav·AI·Sound |
| `MissionStateEvent` | state, objectiveProgress, timeLeft, result | Mission → UI·Audio·GameManager |
| `AmmoChangedEvent` | weaponId, mag, reserve | Weapon → HUD |
| `PauseEvent` | isPaused | GameManager → Audio·UI·AI |

### 5.3 피해·사망·재시작 책임
1. `Hitscan`이 명중을 판정하고 `DamageEvent`를 보낸다.
2. `Health` 컴포넌트가 체력을 줄이고, 0 이하가 되면 `isDead`를 켜고 **`DeathEvent`를 한 번만** 보낸다.
3. `MissionManager`가 `DeathEvent`를 받아 처치 수를 올리고 승패를 판정한다.
4. 재시작은 `GameManager`가 게임 씬을 다시 불러와 처리한다. 탐색 기억·적·소음·타이머는 새 씬이 새로 만들므로 자동 초기화된다.

### 5.4 매 프레임 처리 순서와 분산
`GameManager.Update` 한 곳에서 아래 순서를 보장한다(각 시스템은 자체 Update를 쓰지 말고 `Tick(dt)`를 노출한다).

1. 입력 읽기 → 2. 플레이어 이동 → 3. 소음 발생·전달 → 4. 적 감지(분산) → 5. 적 상태 전환·이동 → 6. 공격·피해 → 7. 사망 확정 → 8. 미션 판정 → 9. 화면 갱신(시야·UI)

분산 규칙: 적 N명 각각에게 `phase = id % 4`를 부여하고, 시각 감지는 프레임마다 `frame % 4 == phase`인 적만 실행한다(실제 감지 간격 약 0.067초 @60fps). 길찾기는 한 프레임에 **최대 2건**만 처리하고 나머지는 큐에 쌓는다. 이동·상태 전환은 매 프레임 한다.

---

## 6. 개발 순서 (Phase)

각 Phase 끝에서 "완료 조건"을 확인한다. 못 맞추면 다음으로 넘어가지 말고 고친다.

### Phase 0 — 프로젝트 뼈대
- Unity 프로젝트 생성(2D), 4장 폴더 구조, `.gitignore`, `.gitattributes`, `CLAUDE.md`, `README.md` 초안, `docs/` 파일 생성.
- `Core`: `EventBus`, `Events`, `Rng`, `ConfigLoader`(JSON 읽기·기본값 채우기·경고 로그), `GameManager` 골격.
- `Util/ProceduralSprites`: 흰 사각형·원·부채꼴 스프라이트를 `Texture2D`로 만든다.
- `Editor/ProjectBuilder`: 빈 `MainMenu`, `Game` 씬 생성과 Build Settings 등록.
- **완료 조건**: `Tools/Build Project` 실행 시 두 씬이 생기고, 에러 없이 컴파일된다. git 첫 커밋.

### Phase 1 — 격자·맵·카메라
- `Grid/GridMap`(타일 배열, 1칸=1m, 벽·바닥·문·엄폐물 속성), `TileType`, `Raycast`(DDA, 막는 타일에서 정지), `LineOfSight`.
- `Map/MapLoader`: 9장 JSON(ASCII 맵)을 읽어 타일을 `SpriteRenderer` 사각형으로 그린다(타일 수가 많으므로 한 번에 하나의 메시·텍스처로 합치거나 오브젝트 풀을 쓴다). `map_01.json`을 **먼저 손으로 작성**한다.
- 카메라는 플레이어를 따라간다. 화면 높이 약 18m가 보이도록 orthographic size = 9.
- **완료 조건**: 맵 1이 화면에 그려지고, `GridTests`(벽에 막히는 레이, 모서리 처리)가 통과한다.

### Phase 2 — 플레이어 이동·조준·시야
- `PlayerController/Motor`: WASD 8방향 이동, 대각선 속도 정규화, 벽 충돌, 마우스 방향 바라보기, 걷기·달리기·웅크리기.
- `Vision/PlayerVision`: 90°·12m 부채꼴 시야, DDA 레이로 벽·닫힌 문·높은 엄폐물 차폐. `FogOfWar`: 보인 타일 기억(30~40% 회색조, 윤곽 유지), 미탐색 완전 암전. 한 장의 `Texture2D`(타일당 1픽셀)를 맵 크기 쿼드에 입힌 오버레이로 그린다.
- 시야 밖의 적·아이템은 렌더러를 끈다.
- **완료 조건**: 이동·회전이 부드럽고, 벽 뒤가 보이지 않으며, 지나간 곳이 회색조로 남는다.

### Phase 3 — 문·엄폐물·무기·전투
- `Door`: E 키 토글, 닫힘=이동·시야·총알 차단, 열림=통과, `DoorChangedEvent`. 시야 즉시 갱신.
- `CoverObject`: 높은 엄폐물(시야·총알 차단), 낮은 엄폐물(총알 차단·시야 투과). 파괴 불가.
- `WeaponController`: 3종, 탄창·예비 탄약, 재장전, 0.3초 전환 지연, 재장전 중 전환 시 취소·잔탄 유지. `Hitscan` + 궤적선 0.1초. `Health`, 피격 시 적색 깜빡임 0.1초.
- `MeleeTakedown`: 7장 조건.
- **완료 조건**: 허수아비 적(AI 없는 `Health` 오브젝트)을 사격·제압할 수 있고, 벽·문·엄폐물 뒤는 맞지 않는다.

### Phase 4 — 소리 시스템
- `NoiseEmitter`(발소리·사격·유인탄), `SoundPropagation`: 7.5의 공식.
- 디버그용으로 소리 반경 원을 그릴 수 있게 한다(F3 때만).
- **완료 조건**: `SoundTests` 통과(거리·벽 개수·열린 문별 세기).

### Phase 5 — 적 AI 기본 (M)
순서대로 구현하고 각각 EditMode 테스트 또는 디버그 오버레이로 확인한다.
1. `AStarPathfinder`(8방향, 옥타일, 모서리 규칙, 문 통과) + `AStarTests`.
2. `PathFollower`(재계산 조건), `PatrolRoute`(지점 대기 2~3초).
3. `SightSensor`, `HearingSensor`, `AwarenessGauge`, `ClueMemory`.
4. `EnemyBrain` FSM 6상태와 전환 표(7.7). `StateTransitionLogger`.
5. `SearchPointPicker`(반경 8m 무작위 3곳, 총 10초).
6. 교전 기본 사격(반응 지연 0.5초, 첫 발 0.8초, 명중률 60%, ±5°, 탄창·재장전).
7. 적 머리 위 `AwarenessIcon`(? / !).
- **완료 조건**: 맵 1에서 적이 순찰하고, 플레이어를 보면 `?`→`!`이 뜨고 쫓아와 쏘고, 놓치면 수색 후 순찰로 돌아간다. 소리에도 반응한다.

### Phase 6 — 미션·결과 (M)
- `MissionManager`: 준비→진행→종료→결과. `Eliminate`(모든 적 처치), `Steal`(목표 지점 E 상호작용), 탈출 지점 도달(맵 3).
- `ResultEvaluator`: 같은 프레임 우선순위(7.9). 발각 집계(7.10). 처치 수·클리어 시간.
- **완료 조건**: 맵 1 소탕을 처음부터 끝까지 클리어·실패할 수 있다. `MissionResultTests` 통과.

### Phase 7 — UI·메뉴·사운드·설정 (M)
- `HUD`(좌하 체력, 우하 무기·탄약, 좌상 목표·타이머, 우상 전역 경계 3단계), `MainMenu`, `MissionSelect`(난이도 포함), `SettingsOverlay`(음량 3종·전체화면·해상도), `PauseMenu`, `ResultScreen`.
- `AudioManager`: 코드 생성 톤(11.2)으로 효과음·BGM 재생. 일시정지 시 모두 정지 후 이어서 재생. 음량 3종.
- 해상도: 1920×1080 기준, 16:9가 아니면 검은 여백. 전체화면/창모드, 프리셋 1280×720·1920×1080.
- **완료 조건**: 메인 메뉴 → 미션 선택 → 플레이 → 결과 → 메인 메뉴를 마우스만으로 오갈 수 있다.

### Phase 8 — 맵 2~4와 나머지 M
- `map_02.json`(자료 탈취), `map_03.json`(자료 탈취 후 탈출), `map_04.json`(최종 소탕). 9.3 규격. `MapValidator`(모든 목표·탈출·순찰 지점이 시작점에서 도달 가능, 문 위치 유효)를 만들고 테스트로 돌린다.
- 무기 3종 모두 사용 가능, 최고 기록(메모리 안에서 먼저), 기록 비교(7.11).
- **완료 조건**: 맵 4종을 모두 성공·실패로 끝낼 수 있다. 한 판 5~10분 안팎.

### Phase 9 — S 기능
아래 순서로 구현하고, 하나 끝날 때마다 커밋한다.
1. 적 엄폐 이동(체력 40% 이하·재장전 중·사선 차단 시 6m 안 엄폐물) — 교전 행동 트리에 포함
2. 저장·복구(`SaveSystem`, 7.13)
3. 동료 경보·전역 경계도(7.8)
4. 난이도 3단계(7.12)
5. 문 흔적 인지(+30)
6. 통로 충돌 처리(비켜주기 → 2초 후 겹침)
7. 유인탄
8. 폭탄 해체 미션(맵 3을 폭탄 해체로 바꿀 수 있는 `missions.json` 설정 추가, 기본값은 자료 탈취 후 탈출 유지)

### Phase 10 — 마감
- `DebugOverlay`(F3), `PerfProbe`(AI 처리 시간 평균·최대·2ms 초과 비율, FPS), 고정 시드 모드.
- 적 20명 부하 테스트 씬(또는 디버그 옵션)으로 성능 확인(7.14). 30분 안정성 확인 스크립트(자동 플레이 루프).
- `docs/VERIFY.md` 체크리스트 실행·기록, `docs/ASSET_CREDITS.md`, `README.md` 완성.
- Windows 빌드(`BuildPipeline`로 `Build/Game.exe`), 실행 확인.
- **완료 조건**: 8장 "완료 정의"를 모두 만족한다.

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
| 문·목표 상호작용 | E |
| 유인탄 투척 | G (커서 위치로 던짐) |
| 근접 제압 | F |
| 일시정지 | Esc |
| 디버그 화면 | F3 |

### 7.2 플레이어

| 항목 | 값 |
| --- | --- |
| 최대 체력 | 100 |
| 걷기 속도 | 3.5 m/s |
| 달리기 | 걷기 ×1.5 (5.25 m/s) |
| 웅크리기 | 걷기 ×0.5 (1.75 m/s) |
| 크기 | 지름 0.8m 원 |
| 발소리 반경 / 간격 | 걷기 6m / 0.6초, 달리기 9m / 0.4초, 웅크리기 3m / 0.8초 (정지 시 소리 없음) |
| 시야 | 90°, 12m, 마우스 방향 기준 |
| 피격 반응 | 적색 깜빡임 0.1초 + 피격음. 경직·밀림·둔화·무적 없음 |
| 사망 | 체력 0 이하 → `DeathEvent` 1회 → 미션 실패 |
| 시작 장비 | 3종 무기 모두 휴대, 권총을 든 채 시작, 유인탄 2개 |

### 7.3 무기

| 무기 | 피해 | 사거리 | 발사 간격 | 소음 반경 | 탄창 / 예비 | 장전 | 비고 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 권총 | 20 | 15m | 0.3초 | 12m | 12 / 36 | 1.5초 | 기본, 단발 |
| 소음권총 | 17 | 12m | 0.3초 | 3m | 10 / 30 | 1.8초 | 단발 |
| 소총 | 30 | 30m | 0.12초 | 25m | 30 / 90 | 2.5초 | 연사 |

- 무기 전환 지연 0.3초. 재장전 중 전환하면 재장전이 취소되고 **기존 잔탄 유지**.
- 재장전은 탄창 부족분만큼 예비 탄약에서 채운다.
- **히트스캔**: 발사 순간 조준 방향으로 사거리까지 DDA 레이. 처음 닿는 적·벽·닫힌 문·엄폐물(높음·낮음 모두)에서 멈춘다. **관통 없음**. 적이 쏜 탄은 다른 적을 무시한다. 궤적선은 약 0.1초 표시한다.
- 소총 피해 > 권총 피해 > 소음권총 피해 관계를 유지한다.

### 7.4 유인탄 (S)
2개. 던진 지점에서 소음 반경 10m가 3초간 지속(0.5초 간격으로 소음 이벤트 반복). 벽 감쇠 규칙 동일. 플레이어에게 영향 없음. 던지는 거리는 최대 10m, 날아가는 시간 0.5초.

### 7.5 근접 제압
조건 **모두** 만족: 적과 1.5m 이내, 플레이어가 적의 뒤쪽 120° 부채꼴 안, 적 상태가 순찰·조사(미발각)이며 인지 게이지 100 미만. 성공하면 소음 없이 즉시 제압(그 적은 사망 처리, 처치 수 +1). 정면이거나 추격·교전 중이면 동작하지 않는다.

### 7.6 소리 전파 (SoundPropagation) — 순수 함수로 구현

```
입력: 소음 위치 S, 반경 R, 청취자 위치 L
d = |S - L|
if d > R: 세기 = 0
else:
  base = 1 - d / R                    # 선형 감쇠
  walls = S→L DDA 레이에서 지나친 "벽 타일 + 닫힌 문 타일"의 개수
  세기 = base * 0.5^walls             # 열린 문은 세지 않는다
반응 조건: 세기 >= 0.2
```
- 벽 개수는 타일 단위로 센다(두꺼운 벽 2칸이면 2개).
- 문을 통한 우회 전파는 구현하지 않는다.

### 7.7 적 종류와 수치

| 항목 | 경비병 (Guard) | 소총병 (Rifleman) |
| --- | --- | --- |
| 체력 | 50 | 60 |
| 시야 | 90°, 10m | 70°, 14m |
| 무기 | 권총형: 피해 12, 간격 0.5초, 사거리 12m, 탄창 12 | 소총형: 피해 8, 간격 0.25초, 사거리 20m, 탄창 30 |
| 장전 | 1.5초 | 2.5초 |
| 예비 탄약 | 무제한 | 무제한 |
| 순찰 속도 | 2.0 m/s | 2.0 m/s |
| 추격·교전 이동 속도 | 3.5 m/s | 3.5 m/s |
| 크기 | 지름 0.8m, 빨강 | 지름 0.8m, 진한 빨강 |
| 경보 방식 | 외침(반경 12m) | 무전(전체) |

**공통 교전 수치**: 반응 지연 0.5초(발견 후), 첫 발은 발견 후 0.8초, 명중률 약 60%(발사 시 조준 오차 ±5° 균등 난수, 플레이어가 달리는 중이면 오차 ×1.5), 사거리 안이면 멈춰서 사격, 밖이면 접근.

**인지 게이지** (적마다 하나, 0~100):

| 항목 | 값 |
| --- | --- |
| 의심 기준 | 30 |
| 발견 기준 | 100 |
| 시야 안 상승 | 초당 +40 |
| 상승 배율 | 3m 이내 ×2, 플레이어 달리기 ×1.5, 웅크리기 ×0.5 (곱한다) |
| 놓친 뒤 | 1초 유지 후 초당 −15 |
| 새 소리 | 게이지를 최소 30(의심)까지 올리고 소리 위치를 단서로 저장 |
| 문 흔적 | +30 |

**단서 기억**: 소리 단서 8초, 마지막 목격 위치 15초. 선택 우선순위는 최근 목격 > 가까운 큰 소리 > 먼 소리, 같은 종류는 더 최근 것.

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

**조건 충돌 시 우선순위**: 사망 > 눈앞의 플레이어(시야 안 발견) > 새 소리 > 수색 종료. (미션 시간 종료는 미션 매니저 소관이며 이 순위와 무관하다.)

**상태 설명**
- 조사: 단서 위치로 걸어가 둘러본다(도착 후 1초 정지). 이동 속도 2.0 m/s.
- 수색: 마지막 인지 위치 반경 8m 안에서 도달 가능한 무작위 타일 3곳을 가까운 순서로 방문, **총 10초**. 끝나면 가장 가까운 순찰 지점으로 복귀.
- 순찰: 순찰 지점을 순서대로 이동, 지점마다 2~3초(무작위) 대기, 마지막 지점 다음은 처음 지점(순환).

**교전 행동 트리** (교전 상태 안에서만 사용, `Selector` 우선순위):
1. 체력 ≤ 40% 또는 재장전 중 또는 사선이 막힘 → 반경 6m 안 가장 가까운 엄폐물로 이동(없으면 다음 노드로) *(S)*
2. 탄창이 비었음 → 재장전
3. 사거리 밖 → 플레이어 쪽으로 접근
4. 사거리 안 & 시야 확보 → 정지 후 사격
5. 그 외 → 마지막 목격 위치로 이동

### 7.8 동료 경보·전역 경계도 (S)
- 적이 **발견 상태에 처음 들어갈 때**(`DetectionEvent`) 경보를 보낸다. 경비병은 **외침**(반경 12m 안 적, 벽 무시), 소총병은 **무전**(모든 적).
- 경보에는 마지막 목격 위치와 시각이 담긴다. 수신한 적은 **의심(조사) 상태**로 그 위치를 조사하며, 다시 퍼뜨리지 않는다.
- 발신 제한: 한 적은 5초에 1회, 같은 발견 사건당 최대 2회.
- **전역 경계도**: 모든 살아 있는 적의 상태 중 최댓값을 3단계로 표시 — 평상(전원 순찰), 경계(조사·수색 존재), 전투(추격·교전 존재). 경계 이상이면 수색 시간 ×1.2. HUD 우상단에 표시한다.

### 7.9 승패 판정 (미션 매니저, 프레임당 1회)

| 동시에 일어난 사건 (같은 프레임) | 결과 |
| --- | --- |
| 목표 달성 + 제한 시간 종료 | 성공 |
| 탈출 + 사망 | 실패 |
| 목표 달성 + 사망 | 실패 |
| 서로 다른 프레임 | 먼저 일어난 사건 |

판정이 나면 이후 입력을 막고 결과 화면으로 간다.

### 7.10 발각 횟수
적의 인지 게이지가 100에 처음 도달한 순간(`DetectionEvent`, **직접 목격**만)을 1회로 센다. 여러 적이 같은 시간대에 봐도 1회. **모든 살아 있는 적이 순찰 상태로 돌아간 뒤** 다시 발각되면 +1. 외침·무전으로 알게 된 것은 세지 않는다.

### 7.11 기록
- 비교 순서: **클리어 시간(짧을수록, 소수점 셋째 자리) > 발각 횟수(적을수록) > 처치 수(적을수록)**. 모두 같으면 기존 기록 유지.
- 기록은 **미션(맵)·난이도별로 따로** 저장한다. 실패는 기록하지 않는다.

### 7.12 난이도 (S, 기본은 보통 1단계만 M)
난이도는 미션 선택 화면에서 고르고 플레이 중에는 바꿀 수 없다.

| 난이도 | 배율 m |
| --- | --- |
| 쉬움 | 0.8 |
| 보통 | 1.0 |
| 어려움 | 1.2 |

적용: **시야 거리·수색 시간 × m**, **반응 지연·조준 오차 × (1/m)** (어려울수록 시야가 길고 반응이 빠르고 정확하다).

### 7.13 설정·저장
- 설정 화면: 음량(전체·효과음·배경음), 전체화면/창모드, 해상도(1280×720, 1920×1080). 음량은 즉시 적용, 전체화면·해상도는 "적용" 버튼 때 적용. 마우스 감도·키 변경·난이도는 넣지 않는다.
- 파일: 읽기용 설정 `Application.streamingAssetsPath/Config/*.json`, 사용자 저장 `Application.persistentDataPath/save.json`.
- 저장 시점: 설정은 적용 때, 최고 기록은 미션 종료 때. 임시 파일(`save.json.tmp`)에 쓴 뒤 교체하고, 교체 전 이전 파일을 `save.json.bak`으로 둔다.
- 저장 실패: 안내 문구 → 1회 재시도 → 또 실패하면 기록 없이 게임 진행.
- 손상(JSON 파싱 실패): 손상 파일을 `save.json.corrupt`로 보존 → `.bak`이 정상이면 복원 → 없으면 "초기화할까요?" 동의 후 초기화.
- 게임 수치 JSON은 재실행 후 반영, 난이도는 다음 미션부터 반영.

### 7.14 성능 기준 (합격선)
- 적 20명이 동시에 활성인 상태에서 **60fps**.
- AI 처리 **프레임당 2ms 이하** (렌더링·물리 제외, 감지·상태·경로·이동·공격 판단 전체의 평균).
- 순간 지연: 최저 45fps(프레임 22ms)까지 허용하되 0.5초 넘게 이어지면 안 되고, **33ms 초과 프레임 0회**.
- 측정: 적 20명이 추격·교전하는 구간 60초, 평균·최대·2ms 초과 프레임 비율 기록. Development Build에서 측정한 뒤 배포 빌드에서 재확인.
- 30분 연속 플레이에서 예외·메모리 증가 없음.

### 7.15 길찾기 (A*)
- 격자: 맵 타일과 동일(1칸 = 1m). 8방향 이동.
- 비용: 직선 1, 대각 1.414. 휴리스틱은 **옥타일 거리**: `D*(dx+dy) + (D2 - 2D)*min(dx,dy)`, D=1, D2=1.414.
- 모서리 규칙: **대각 이동 시 인접한 두 칸 중 하나라도 벽이면 통과 불가**.
- 문: 닫힌 문은 통과 가능한 칸으로 취급하되 문 앞에서 0.5초 멈춰 열고 지나간다(경로 비용 +2).
- 재계산 조건: 목표 변경, 문 상태 변경, 0.5초 이상 막힘, 추격 목표가 3칸 이상 이동. 최소 간격 0.3초. 실패하면 1초 뒤 최대 3회 재시도, 계속 실패하면 가장 가까운 도달 가능 지점으로 이동한 뒤 순찰 복귀.
- 노드 배열은 재사용(할당 최소화), 우선순위 큐는 직접 구현한 이진 힙.

### 7.16 통로 충돌 (S)
적끼리 좁은 곳에서 마주치면 우선순위가 낮은 적(순찰·대기 중)이 가까운 빈 칸으로 비킨다. 2초 이상 서로 막히면 임시로 겹침을 허용한다.

---

## 8. AI는 어떻게 쓰이는가

### 8.1 구성 한눈에
```
[감각]  SightSensor ──┐
        HearingSensor ─┼─> AwarenessGauge ─> (의심 30 / 발견 100)
        DoorClue ──────┘          │
                                  v
[판단]  EnemyBrain (FSM 6상태) ── 전환 표(7.7) ── EnemyStateChangedEvent
                                  │
                  교전 상태일 때만 v
        CombatTree (행동 트리: 엄폐→재장전→접근→사격→추적)
                                  │
[행동]  PathFollower(A*) ─ 이동 / WeaponController ─ 사격 / AlertSystem ─ 경보
```
- **FSM**이 큰 상태를 정하고, **행동 트리**는 교전 안에서 "무엇을 할지"만 고른다. 둘을 섞지 않는다.
- 적은 **자기가 보고 들은 것과 전달받은 정보**로만 움직인다. 플레이어 위치를 직접 읽는 코드는 `SightSensor` 한 곳에만 둔다(시야 안일 때만).
- 모든 상태 전환은 `reason` 문자열과 함께 `EnemyStateChangedEvent`로 알리고 로그로 남긴다(예: `"sight_gauge_100"`, `"heard_noise"`, `"search_timeout"`).

### 8.2 구현 규칙
- 학습형 AI·외부 AI 패키지·NavMesh 사용 금지. 모든 로직은 직접 작성한 C#.
- 난수(순찰 대기 시간, 수색 지점, 조준 오차)는 `Rng`에서만 뽑는다. 고정 시드 모드에서 같은 입력이면 같은 상태 전이가 나와야 한다.
- AI 처리 시간은 `PerfProbe`가 `Stopwatch`로 재서 평균·최대·2ms 초과 비율을 기록한다.
- 디버그 오버레이(F3)에서 적 머리 위 ID·상태·게이지 수치, 시야 부채꼴, A* 경로선, 소리 반경 원을 볼 수 있다.

### 8.3 Claude Code가 이 프로젝트에서 AI(자기 자신)를 쓰는 방식
- 이 문서가 단일 진실 공급원이다. 구현 중 판단이 갈리면 이 문서의 숫자·표를 따르고, 문서에 없으면 `docs/DECISIONS.md`에 적고 진행한다.
- Phase마다 **작게 만들고 → 테스트하고 → 커밋**한다. 한 번에 여러 Phase를 만들지 않는다.
- 맵 JSON은 Claude Code가 직접 작성한다. 작성 후 `MapValidator`로 반드시 검증한다.
- 외부 에셋을 쓰려면 라이선스가 명확한 것만 쓰고 `docs/ASSET_CREDITS.md`에 기록한다. 확실하지 않으면 코드 생성 에셋을 쓴다.

---

## 9. 데이터 파일

### 9.1 `game.json`
```json
{
  "version": 1,
  "reference_resolution": [1920, 1080],
  "camera_ortho_size": 9.0,
  "fixed_seed_enabled": false,
  "fixed_seed": 12345,
  "default_difficulty": "normal"
}
```

### 9.2 `weapons.json`
```json
{
  "version": 1,
  "weapons": [
    { "id": "pistol",        "damage": 20, "range_m": 15, "fire_interval_s": 0.3,  "noise_radius_m": 12, "mag_size": 12, "reserve": 36, "reload_s": 1.5, "auto": false },
    { "id": "silenced_pistol","damage": 17, "range_m": 12, "fire_interval_s": 0.3,  "noise_radius_m": 3,  "mag_size": 10, "reserve": 30, "reload_s": 1.8, "auto": false },
    { "id": "rifle",         "damage": 30, "range_m": 30, "fire_interval_s": 0.12, "noise_radius_m": 25, "mag_size": 30, "reserve": 90, "reload_s": 2.5, "auto": true }
  ],
  "switch_delay_s": 0.3
}
```

### 9.3 맵 JSON (ASCII 격자)
- `rows[0]`이 **맨 위(y 최대)** 줄이다. 로더가 y를 뒤집는다. 좌표 원점은 왼쪽 아래 칸의 왼쪽 아래 모서리, 1칸 = 1m, 칸 중심은 (x+0.5, y+0.5).
- 범례:

| 문자 | 뜻 |
| --- | --- |
| `#` | 벽(이동·시야·총알 차단) |
| `.` | 바닥 |
| `D` | 문(기본 닫힘) |
| `c` | 낮은 엄폐물(총알 차단, 시야 투과, 이동 불가) |
| `C` | 높은 엄폐물(시야·총알 차단, 이동 불가) |
| `P` | 플레이어 시작 |
| `T` | 목표(자료 / 폭탄 위치) |
| `X` | 탈출 지점 |
| 숫자 `1`~`9` | 적 스폰 (숫자는 `enemies`의 `spawn` 번호와 연결) |

```json
{
  "version": 1,
  "id": "map_01",
  "name": "맵 1 - 소탕",
  "mission": "eliminate",
  "target_time_s": 300,
  "rows": [
    "####################",
    "#P.....#...........#",
    "#......D.....1.....#",
    "#...c..#...........#",
    "#......#####D#######",
    "#######...#....#...#",
    "#..2...D..D..C.D.3.#",
    "####################"
  ],
  "enemies": [
    { "spawn": 1, "type": "guard",     "patrol": [[13.5,5.5],[17.5,5.5],[17.5,2.5]] },
    { "spawn": 2, "type": "rifleman",  "patrol": [[3.5,1.5],[3.5,0.5]] },
    { "spawn": 3, "type": "guard",     "patrol": [[17.5,1.5],[14.5,1.5]] }
  ]
}
```
(위 예시는 **형식 설명용 작은 맵**이다. 실제 맵 1~4는 아래 규격에 맞게 새로 설계한다.)

**맵 규격 (실제 제작용)**

| 맵 | 미션 | 크기(가로×세로 m) | 방 | 적 | 목표 시간 | 구성 |
| --- | --- | --- | --- | --- | --- | --- |
| map_01 | 소탕 | 40×30 | 4 | 4 | 5분 | 경비병 위주, 소총병 1 |
| map_02 | 자료 탈취 | 50×40 | 6 | 6 | 7분 | 목표 `T` 1개, 소총병 2 |
| map_03 | 자료 탈취 후 탈출 | 60×40 | 7 | 8 | 8분 | `T` 1개 + 탈출 `X` 1개, 소총병 3 |
| map_04 | 최종 소탕 | 70×50 | 9 | 10 | 10분 | 소총병 4, 엄폐물 많음 |

맵 제작 조건(검증 대상):
- 모든 방은 문(`D`)으로 연결되고 시작점에서 모든 목표·탈출·순찰 지점이 도달 가능해야 한다.
- 적 스폰과 순찰 지점은 서로 벽으로 막히지 않아야 한다. 순찰 지점은 한 적당 2~5개.
- 복도 폭 2칸 이상(통로 충돌이 계속 발생하지 않도록), 방마다 문 1~2개.
- 낮은 엄폐물 `c`와 높은 엄폐물 `C`를 방마다 1~3개 배치. 시작점 시야 안(12m)에 적이 없게 한다.
- 소탕 맵은 `T`/`X`가 없어도 되고, 탈취 맵은 `T`가 정확히 1개, 탈출 맵은 `X`가 정확히 1개.

### 9.4 `enemies.json`
```json
{
  "version": 1,
  "types": [
    { "id": "guard",    "hp": 50, "fov_deg": 90, "view_m": 10, "weapon": { "damage": 12, "interval_s": 0.5,  "range_m": 12, "mag": 12, "reload_s": 1.5 }, "alert": "shout", "color": "#E04545" },
    { "id": "rifleman", "hp": 60, "fov_deg": 70, "view_m": 14, "weapon": { "damage": 8,  "interval_s": 0.25, "range_m": 20, "mag": 30, "reload_s": 2.5 }, "alert": "radio", "color": "#A82020" }
  ],
  "move": { "patrol_speed": 2.0, "chase_speed": 3.5 }
}
```

### 9.5 `ai.json`
```json
{
  "version": 1,
  "gauge": { "suspect": 30, "detect": 100, "rise_per_s": 40, "hold_after_lost_s": 1.0, "fall_per_s": 15,
             "near_dist_m": 3, "near_mult": 2.0, "run_mult": 1.5, "crouch_mult": 0.5, "door_clue": 30 },
  "hearing": { "wall_mult": 0.5, "react_threshold": 0.2 },
  "memory": { "sound_clue_s": 8, "last_seen_s": 15 },
  "combat": { "reaction_delay_s": 0.5, "first_shot_s": 0.8, "accuracy": 0.6, "aim_error_deg": 5, "run_aim_mult": 1.5,
             "cover_search_radius_m": 6, "cover_hp_ratio": 0.4 },
  "patrol": { "wait_min_s": 2, "wait_max_s": 3 },
  "search": { "radius_m": 8, "points": 3, "total_s": 10 },
  "alert": { "shout_radius_m": 12, "cooldown_s": 5, "max_per_detection": 2 },
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

### 9.8 `save.json` (사용자 저장)
```json
{
  "version": 1,
  "settings": { "master": 1.0, "sfx": 1.0, "bgm": 0.7, "fullscreen": true, "width": 1920, "height": 1080 },
  "records": [
    { "map": "map_01", "difficulty": "normal", "clear_time_s": 183.412, "detections": 1, "kills": 3 }
  ]
}
```
키 이름은 소문자 snake_case, `version`은 정수. 구버전·누락 키는 기본값으로 채우고 경고 로그를 남긴다.

---

## 10. 화면 구성

| 화면 | 내용 |
| --- | --- |
| 메인 메뉴(`MainMenu` 씬) | 제목, 시작(→ 미션 선택), 설정(오버레이), 종료 |
| 미션 선택 | 맵 4개 카드(이름·미션 종류·최고 기록), 난이도 선택(쉬움·보통·어려움), 시작 버튼 |
| 게임(`Game` 씬) | 월드 + HUD |
| HUD | 좌하단 체력(막대+수치), 우하단 무기 이름·탄창/예비, 좌상단 목표 문구·타이머(경과 시간, 폭탄 시 남은 시간), 우상단 전역 경계 3단계(평상·경계·전투 색 구분) |
| 적 머리 위 | 의심 `?`(노랑), 발견 `!`(빨강). 발견 시 경고음 1회(약 1초), `!`는 발견 상태 동안 유지 |
| 일시정지 | 이어하기·설정·메인 메뉴. 모든 소리·AI·타이머 정지 |
| 설정(오버레이) | 음량 3개 슬라이더, 전체화면 토글, 해상도 드롭다운, 적용·닫기 |
| 결과 | 성공/실패, 클리어 시간, 발각 횟수, 처치 수, 최고 기록 갱신 여부, 다시 하기·다음 미션·메인 메뉴 |
| 디버그(F3) | 적 ID·상태·게이지, 시야 부채꼴, A* 경로선, 소리 반경 원, FPS·AI ms |

UI는 uGUI(Canvas, Screen Space Overlay, Canvas Scaler 1920×1080 기준 Scale With Screen Size) 또는 UI Toolkit 중 **uGUI**를 쓴다. 폰트는 `Fonts/`에 한글 폰트가 있으면 쓰고, 없으면 Unity 기본(Legacy Runtime) 폰트로 영문 라벨을 쓰되 한글 라벨은 `TextMeshPro` 기본 폰트가 지원하는 범위에서 표시한다. 한글이 깨지면 DECISIONS에 적고 영문 라벨로 대체한다.

---

## 11. 에셋

> 에셋이 하나도 없어도 게임이 실행되어야 한다. 아래 "기본(코드 생성)"을 먼저 만들고, 외부 에셋은 **같은 이름의 파일을 넣으면 자동으로 교체**되도록 `Resources`/`Addressables` 없이 단순한 경로 탐색(`Art/`, `Audio/`)으로 처리한다.

### 11.1 그래픽 (단순 도형 2D 탑뷰, 어두운 배경)

| 에셋 | 종류 | 기본(코드 생성) | 비고 |
| --- | --- | --- | --- |
| 바닥 타일 | 정사각 | 어두운 회색 `#2A2D33` | 1m |
| 벽 타일 | 정사각 | 밝은 회색 `#7A808C` + 어두운 테두리 | |
| 문(닫힘/열림) | 직사각 | 갈색 `#8B5A2B`, 열림은 얇은 막대 | 개폐 상태 구분 |
| 낮은 엄폐물 | 정사각 | 청회색 `#5B6C80`, 낮은 높이 느낌의 줄무늬 | 시야 투과 |
| 높은 엄폐물 | 정사각 | 진한 청회색 `#3E4A5A` | 시야 차단 |
| 플레이어 | 원+방향 표시 | 파랑 `#3C8DFF`, 앞쪽에 작은 삼각형 | 지름 0.8 |
| 경비병 / 소총병 | 원+방향 표시 | 빨강 `#E04545` / 진한 빨강 `#A82020` | 지름 0.8 |
| 목표(자료/폭탄) | 마름모 | 노랑 `#F2C230` | |
| 탈출 지점 | 사각 윤곽 | 초록 `#37C871` 반투명 | |
| 시야 부채꼴 | 메시 | 흰색 알파 0.08 (플레이어), 디버그에서 적 부채꼴 빨강 알파 0.1 | |
| 탐색 기억 오버레이 | 1픽셀/타일 텍스처 | 미탐색 알파 1.0(검정), 기억 알파 0.65, 현재 시야 알파 0 | 포인트 필터 |
| 총알 궤적 | 선(LineRenderer) | 노랑 `#FFE680`, 0.1초 | |
| 유인탄 | 작은 원 | 주황 `#FF9A3C` | |
| 소리 파장(디버그) | 원 윤곽 | 하양 알파 0.3 | F3에서만 |
| 인지 아이콘 | `?` `!` 텍스트 스프라이트 | 노랑 / 빨강 | 적 머리 위 |
| 피격 효과 | 색 곱하기 | 적색 0.1초 | |
| UI | uGUI 기본 | 어두운 패널 + 밝은 글자 | |

### 11.2 사운드 (없으면 코드로 톤 생성: `AudioClip.Create` + 사인/노이즈 파형)

| ID | 용도 | 길이 | 코드 생성 방법 |
| --- | --- | --- | --- |
| `sfx_shot_pistol` | 권총 사격 | 0.12s | 짧은 노이즈 버스트 + 빠른 감쇠 |
| `sfx_shot_silenced` | 소음권총 | 0.08s | 낮은 볼륨 노이즈 |
| `sfx_shot_rifle` | 소총 | 0.1s | 노이즈 + 저음 |
| `sfx_reload` | 재장전 | 0.4s | 두 번의 짧은 클릭 |
| `sfx_empty` | 빈 탄창 | 0.05s | 클릭 |
| `sfx_footstep` | 발소리 | 0.08s | 저역 노이즈 톡 |
| `sfx_door` | 문 열기/닫기 | 0.2s | 저음 슬라이드 |
| `sfx_hit_player` | 플레이어 피격 | 0.15s | 하강 톤 |
| `sfx_hit_enemy` | 적 피격 | 0.1s | 짧은 톤 |
| `sfx_takedown` | 근접 제압 | 0.2s | 둔탁한 소리 |
| `sfx_alert` | 발각 경고음 | 1.0s | 880Hz 두 번 삑 |
| `sfx_decoy` | 유인탄 | 0.3s | 금속 틱 반복 |
| `sfx_pickup` | 목표 확보 | 0.3s | 상승 톤 |
| `sfx_success` / `sfx_fail` | 결과 | 1.0s | 상승 / 하강 아르페지오 |
| `sfx_ui_click` | 버튼 | 0.05s | 클릭 |
| `bgm_menu` | 메뉴 배경 | 루프 20s | 느린 저음 패드(사인 코드) |
| `bgm_game` | 게임 배경 | 루프 30s | 낮은 앰비언트 패드 |

외부 효과음·BGM을 쓸 때는 CC0 또는 사용이 허용된 무료 에셋만 쓰고, 파일 이름을 위 ID로 맞춰 `Audio/sfx/`, `Audio/bgm/`에 넣는다. 출처는 `docs/ASSET_CREDITS.md`에 적는다.

### 11.3 폰트
무료 사용 가능한 한글 폰트(예: Noto Sans KR 등 라이선스가 명확한 것)가 있으면 `Fonts/`에 넣고 TMP 폰트 에셋으로 만든다. 없으면 기본 폰트를 쓴다.

### 11.4 에셋 준비 담당 메모
김성범이 에셋 수집·출처 확인을 맡는다. 생성 도구(이미지·음원 생성)는 수업 규정 확인 전에는 쓰지 않는다. Claude Code는 **코드 생성 에셋만으로 완성**하고, 외부 에셋은 교체 지점만 만들어 둔다.

---

## 12. 일정 (참고용, 크게 신경 쓰지 않는다)

| 시점 | 목표 |
| --- | --- |
| 중간 범위 | Phase 0~6: 맵 1 소탕을 처음부터 끝까지 시연 가능 |
| 기말 범위 | Phase 7~9: 맵 4종, UI, 저장, S 기능 |
| 마감 | Phase 10: 성능·재현·검수 기록, 빌드 |

중간발표 시연 범위 = 맵 1, 권총, 순찰·조사·추격·교전 AI, 문, 엄폐, 근접 제압, 인지 표시, 디버그 오버레이. 시간이 부족하면 S 기능을 Phase 9 뒤쪽부터 뺀다.

---

## 13. 테스트·검수·재현

### 13.1 EditMode 테스트 (필수)

| 테스트 | 확인 |
| --- | --- |
| GridTests | DDA가 벽·닫힌 문·높은 엄폐물에서 멈춘다, 낮은 엄폐물은 시야는 통과하고 총알은 막는다, 모서리 케이스 |
| AStarTests | 최단 경로 길이, 대각 모서리 통과 금지, 문 비용, 도달 불가 처리, 재시도 규칙 |
| SoundTests | 거리별 세기, 벽 1·2개, 닫힌/열린 문, 임계값 0.2 경계 |
| AwarenessTests | 게이지 상승·유지·하강, 배율(근접·달리기·웅크리기), 소리로 30까지 상승 |
| FsmTests | 7.7 전환 표의 모든 줄, 조건 충돌 우선순위 |
| MissionResultTests | 7.9 네 가지 동시 사건, 한 번만 판정 |
| DetectionCountTests | 동시 발견 1회, 전원 순찰 복귀 후 재발각 +1, 경보로 안 경우 제외 |
| RecordsTests | 비교 순서, 동점 시 기존 유지, 미션·난이도별 분리 |
| SaveTests | 실패 재시도, `.corrupt` 보존, `.bak` 복원, 동의 후 초기화 |
| MapValidatorTests | 4개 맵 모두 도달 가능성·목표 개수·문 연결 |

### 13.2 `docs/VERIFY.md` 체크리스트 (실행 후 결과 기록)
- [ ] 이동 모드별 소음 반경이 걷기 6m·달리기 9m·웅크리기 3m
- [ ] 벽·닫힌 문·엄폐물 뒤 적은 맞지 않고 적끼리 피해가 없다
- [ ] 근접 제압은 뒤 120°·1.5m 안 미발각 적에게만 된다
- [ ] 벽 뒤는 보이지 않고, 방문 지형이 회색조로 남으며 재시작하면 지워진다
- [ ] 같은 상황의 적 상태 전환이 FSM 표와 같다(상태 전이 로그)
- [ ] 소리는 벽·닫힌 문 1개마다 절반, 열린 문은 그대로
- [ ] A* 경로가 벽 모서리를 대각선으로 통과하지 않고 닫힌 문은 열고 지나간다
- [ ] 같은 프레임 승패 4가지 결과가 표와 같다
- [ ] 여러 적이 동시에 발견해도 발각 1회, 경보로 안 경우 제외
- [ ] 저장 손상 시 `.bak` 복원 또는 동의 후 초기화
- [ ] 적 20명 60fps·AI 2ms 이하, 33ms 초과 프레임 0
- [ ] 같은 맵·시드·입력으로 3회 실행한 상태 전이 로그가 같다
- [ ] 30분 연속 플레이 오류·메모리 증가 없음
- [ ] 맵 4종 모두 성공·실패로 끝낼 수 있다

### 13.3 고정 시드 재현
`game.json`의 `fixed_seed_enabled`가 true면 `Rng`가 `fixed_seed`로 초기화된다. 입력을 파일(`docs/evidence/input_replay.json`: 프레임·키·마우스 위치)로 기록·재생하는 간단한 리플레이 기능을 만들어, 같은 입력으로 3회 실행해 `StateTransitionLogger` 출력을 비교한다.

### 13.4 증거 (`docs/evidence/`)
결과표, 상태 전이 로그 3회분, 주요 스크린샷, 미션별 클리어 영상 1건(영상은 녹화가 불가능하면 DECISIONS에 사유를 적고 로그·스크린샷으로 대체).

---

## 14. 완료 정의 (Definition of Done)

아래를 모두 만족하면 끝이다.

1. `Tools/Build Project` 한 번으로 씬·프리팹이 생성되고, 컴파일 오류와 경고성 예외가 없다.
2. Windows 빌드가 실행되어 메인 메뉴 → 미션 선택 → 플레이 → 결과 → 메인 메뉴 흐름이 동작한다.
3. 맵 4종 모두 성공·실패로 끝낼 수 있다. M 기능은 전부 동작한다.
4. S 기능은 Phase 9 순서대로 가능한 만큼 구현되어 있고, 못 한 것은 `DECISIONS.md`에 사유가 있다.
5. EditMode 테스트가 모두 통과한다.
6. 7.14 성능 기준과 13.2 체크리스트 결과가 `VERIFY.md`에 기록되어 있다.
7. 에셋이 없어도 실행되며, 외부 에셋을 쓴 경우 `ASSET_CREDITS.md`에 출처가 있다.
8. `README.md`에 실행·조작(7.1)·빌드·폴더 구조 설명이 있다.
9. git 이력에 Phase별 커밋이 남아 있다.

## 15. 금지 사항 요약
인질·섬광·잠금 문·측면 이동·관통·피격 경직 구현 금지 / NavMesh·외부 AI 패키지 사용 금지 / `Physics2D`로 시야·소리·총알 판정 금지 / 수치를 코드에 하드코딩 금지 / 구독 해제 누락 금지 / 라이선스 불명 에셋 사용 금지 / 사용자에게 질문하며 멈추기 금지(문서 기준으로 결정하고 기록).

---

**지금 바로 Phase 0부터 시작하라.**
