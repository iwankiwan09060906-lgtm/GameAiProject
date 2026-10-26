# CLAUDE.md

2D 탑뷰 전술 잠입 슈팅 게임 (Unity 6000.3.10f1, URP 2D, Windows). 단일 기준 문서는 `docs/MASTER_PROMPT.md`이다.

## 문서 우선순위
1. `docs/MASTER_PROMPT.md` — 규칙·수치·데이터 형식·개발 순서(Phase)
2. `docs/요구사항 정의서.md` — 팀 결정 #1~#30 (바뀌면 여기부터 고친다)
3. `docs/기능 정의서.md` — 기능별 동작 규칙
4. `docs/ASSET_DIRECTION.md` + `docs/reference/*.png` — 화면 모습(색·배치·연출)의 기준

문서에 없는 것은 가장 단순한 방식으로 정하고 `docs/DECISIONS.md`에 한 줄로 적는다. 사용자에게 되묻지 않는다.

## 작업 규칙
- Phase 순서대로, Phase마다 완료 조건 확인 후 `phase-N: 요약`으로 커밋한다.
- 외부 에셋 없이도 실행되어야 한다(코드 생성 스프라이트·톤). 같은 이름 파일을 넣으면 교체된다.
- 씬·프리팹은 손으로 만들지 않고 `Assets/_Project/Editor/ProjectBuilder.cs`(`Tools/Build Project`)로 생성한다.
- 범위 밖 기능(MASTER_PROMPT 2.2, 15장)은 만들지 않는다.

## 코딩 규칙
- 주석·로그는 한국어, 클래스·변수 이름은 영어.
- 게임 로직(격자, A*, 감지, 소리, 밝기, 체력·방탄, 승패, 기록)은 MonoBehaviour와 분리된 순수 C# + EditMode 테스트.
- 수치는 `Assets/StreamingAssets/Config/*.json`에서 읽는다. 누락 시 기본값 + 경고 로그. 하드코딩 금지.
- 시스템 간 통신은 `Core/Events.cs`의 이벤트 버스만. 구독은 `OnEnable`, 해제는 `OnDisable`.
- 각 시스템은 자체 `Update` 대신 `Tick(dt)`를 노출하고 `GameManager`가 순서대로 부른다.
- 난수는 `Rng`에서만. AI 난수원과 연출 난수원을 분리한다.
- 시야·소리·총알·밝기 판정은 `Physics2D` 대신 자체 격자 DDA. NavMesh·외부 AI 패키지 금지.
- 입력은 레거시 `UnityEngine.Input`. 상호작용 키는 Space.
