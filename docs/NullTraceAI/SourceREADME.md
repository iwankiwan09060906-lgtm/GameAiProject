# Null Trace 적 AI 샌드박스

Unity **6000.3.24f1 / URP 2D / XY 평면**. 외부 게임 에셋 없이 감각·개인 기억·강제 규칙·Utility 목표 선택·행동 트리 실행·자체 격자 A*·대사/공격 요청을 확인하는 통합 프로젝트입니다. 기존 4상태는 화면용 요약입니다. 수업 요구 알고리즘을 NavMesh나 외부 AI 패키지로 대체하지 않았습니다.

2026-11-06 팀 피드백 반영판을 완성했습니다. EditMode 180개 + PlayMode 28개, **전체 208개 통과·실패/스킵 0**입니다. 팀 전달 기준은 [인수인계 및 사양 확인](Docs/적_NPC_AI_인수인계_및_사양확인_2026-11-06.md), 실행 결과는 [검증 보고서](Results/2026-11-06/Verification.md), 쉬운 설명은 [코드 읽기 안내](Docs/CodeReadingGuide.md)입니다. 11월 5일의 156개 통과 기록과 ZIP은 이전 버전으로 보존합니다.

코드를 처음 읽는 팀원은 [쉬운 코드 읽기 안내](Docs/CodeReadingGuide.md)의 순서대로 시작하세요. 실제 C# 파일에는 역할, 계산 예시, 상태 전환 이유와 팀 연결 방법을 한국어 주석으로 설명했습니다.

`Assets/NullTraceAI/Scenes/AI Sandbox.unity`를 열고 Play 합니다. A(원)는 왼쪽 방의 유한 순찰을 수행하고 B(사각형)는 오른쪽 초소를 지킵니다. 두 NPC는 0.5초 지연 무전을 주고받으며 기억은 각자 보관합니다. 청록 원은 **시연 전용 임시 PlayerTarget**입니다. 팀 플레이어·전투·미션 시스템의 구현이 아닙니다. 벽은 회청색, 문은 주황색입니다.

| 입력 | 동작 |
|---|---|
| WASD / 방향키 | 임시 타겟 이동, 이동 중 작은 발소리 |
| Space 또는 Emit noise 버튼 | 현재 위치에 큰 충돌 소음 |
| 1 / 2 / 3 | 어둠 / 약한 빛 / 밝음 |
| H | 타겟 숨김 / 노출 |
| E | `AiDoor2D`로 문 개폐. 잠금·NPC 끼임은 거부하고 성공 시 격자 자동 갱신 |
| P | A의 다음 순찰 회차 시작 요청. 진행 중 회차·추적·보호는 덮어쓰지 않음 |
| R | 시작 위치·조도·문·AI·요청 횟수 초기화 |

오른쪽 패널은 두 NPC의 목표·업무, **Detection(시각 식별량 0~1)**과 **Suspicion(개인 의심도 0~100)**, 추적 ID/단계/남은 시간/이탈점, 다음 방문 인덱스(0부터), 약한 단서 대기, 기억·무전 수신 횟수, 실제 대사를 표시합니다. 초록=평시, 노랑=확인, 보라=수색, 빨강=교전, 파랑=복귀/보호, 회색=경로 차단 경계입니다. 적의 짧은 선은 방향, 긴 보라 선은 기억한 위치, 빨간 순간 선은 공격 요청입니다. 공격 요청은 실제 데미지를 주지 않습니다.

문을 열고 왼쪽 방으로 이동하면 감지와 교전을 시험할 수 있습니다. 벽 뒤 소음은 발생점 단서만 제공합니다. 충돌음 한 건은 최대 25점이므로 우선 제자리 확인을 하며, 충분한 추가 증거가 쌓이면 수색합니다. 추적이 끝나면 이탈점에 돌아올 때까지 복귀를 유지하고 약한 단서에만 기본 2초 재조사 대기를 적용합니다. 다음 지점 도착은 요구하지 않으며 현재 직접 식별한 적은 대기 중에도 대응합니다. 샘플 감지 거리 3/6/10과 모든 프로필 수치는 **튜닝 시작값**입니다.

## 팀 프로젝트에 옮기기

1. 최소 통합은 `Assets/NullTraceAI/Runtime` 폴더와 대응 `.meta`를 같은 경로로 복사합니다. Runtime에는 추가 패키지 의존성이 없습니다. Unity 버전을 임의로 올리지 마세요.
2. `EnemyTarget`, `NoiseBus`, `GridWorld2D`, `EnemyAgent`와 `EnemyProfile`을 연결합니다. 세부 단계와 구독 코드는 [Docs/Integration.md](Docs/Integration.md)에 있습니다.
3. 시연도 원하면 `Assets/NullTraceAI` 전체와 `.meta`를 복사합니다. Input System, uGUI/TextMeshPro, Test Framework 및 URP 2D가 필요합니다. 테스트를 가져오지 않으려면 `Tests`를 제외해도 Runtime은 동작합니다.
4. 씬의 8번 `AIObstacle` 레이어를 팀 프로젝트의 빈 장애물 레이어로 맞추고 `GridWorld2D.ObstacleMask`를 바꾸세요. 적·플레이어는 장애물 레이어에 넣지 않습니다.
5. `EnemySandbox.prefab`의 Profile과 기본 대사 컴포넌트는 연결되어 있지만 World/Noise/Target/Radio/PatrolPoints는 씬에서 연결해야 합니다. 무전과 모델 제공자는 선택 사항입니다. **Visual 자식만** 최종 모델/스프라이트로 바꿉니다.

기본 도형은 이 프로젝트에서 생성했습니다. Demo의 TextMesh Pro 리소스는 설치된 Unity uGUI 패키지의 공식 Essentials를 비대화식으로 가져왔으며 해당 라이선스 파일을 포함합니다. 한글 디버그 대사는 설치된 맑은 고딕/Apple SD Gothic Neo/Noto Sans CJK KR 중 사용 가능한 OS 글꼴을 런타임에 사용합니다. OS 글꼴 파일은 배포하지 않습니다. 해당 글꼴이 없는 기기에서는 팀이 허가된 한글 TMP 폰트를 연결해야 합니다.

## 검증과 재현

새 실행 증거는 `Results/2026-11-06`에 저장합니다. `Results/Verification.md`는 최신 보고서 안내이며 최상위 기존 XML/PNG는 2026-10-31 구현의 보존 기록입니다. 새 EditMode/PlayMode 원본 XML, 이동·목표·저장 씬 실행 증거, 내부 카메라 캡처와 20/40명 동시 발견·재탐색 표본을 별도로 확인합니다. 실제 시간과 위치가 변했는지 확인하며, Play 진입만으로 실행 성공을 판단하지 않습니다.

Unity Test Runner에서 `NullTrace.AI.EditModeTests`, `NullTrace.AI.PlayModeTests`를 실행할 수 있습니다. 배치 테스트는 `-batchmode -runTests -testPlatform EditMode` 또는 `PlayMode`에 `-projectPath`, `-testResults`, `-logFile`을 지정합니다. `-runTests`에는 `-quit`을 함께 넣지 않습니다. 캡처 테스트에는 GPU가 필요하므로 `-nographics`를 넣지 않습니다.

생성 씬을 다시 만들 때만 `Null Trace > Rebuild generated sandbox`를 사용합니다. 이 명령은 생성 씬/프리팹과 Build Settings의 시연 씬 목록을 다시 저장하므로 편집한 씬은 먼저 다른 이름으로 보관하세요. 정상 사용과 팀 Runtime 통합에는 재생성이 필요 없습니다.

최종 게임 에셋, Animator, 실제 전투·플레이어·미션·저장 시스템 및 실행 파일 빌드는 이 전달물의 범위 밖입니다. 20/40명 표본은 측정 조건을 포함하며 최종 게임 FPS나 안전한 NPC 상한을 보장하지 않습니다.
