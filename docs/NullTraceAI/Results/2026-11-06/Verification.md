# 2026-11-06 적 NPC AI 검증 결과

**최종 EditMode 180/180 + PlayMode 28/28 = 208/208 통과. 실패 0, 스킵 0.** Unity 6000.3.24f1과 기존 URP 2D를 유지했다. 최종 소스는 두 전체 실행 전에 고정되었으며 파일별 SHA-256을 [기계 판독 검증 기록](verification-summary.json)에 남겼다. XML의 UTC 11월 5일 16시대는 한국 시간 11월 6일 01시대다.

## 실행 증거

- [EditMode 원본 XML](EditMode.xml), [PlayMode 원본 XML](PlayMode.xml): 두 CLI 종료 코드 0, 모든 사례 Passed.
- [저장 씬 실행](sandbox-execution.txt), [실제 이동](movement-evidence.txt), [상태 흐름](fsm-flow.txt): 시간·프레임·위치·공격 이벤트의 실제 변화를 확인했다.
- [순찰 화면](sandbox-patrol.png), [교전 화면](sandbox-combat.png): 각각 1280×720. 한글 대사/상태·E 버튼·패널 줄바꿈을 열어 확인했다. OS 화면이나 원격 포인터를 캡처하지 않았다.
- SandboxBuilder.Build로 시연 씬/프리팹을 재생성했고 저장된 AiDoor2D와 E 버튼 연결도 통과했다.

테스트 assertion/예외와 C# 컴파일 오류는 최종 실행에 없다. 로그의 라이선스 클라이언트 갱신·그래픽 진단 메시지는 테스트 실패와 구분했다. 종료 코드와 XML에서 실행 완료를 확인했으며 경고가 전혀 없는 환경이라고 주장하지 않는다.

## 사양과 주요 검사 대응

| 사양 | 실제 통과한 검사 |
|---|---|
| 약한 단서 대기 단순화 | `WeakClueProtectionExpiresByTimeWithoutNextPatrolArrival` |
| 직접 식별 적의 대기 우회 | `DirectIdentifiedEnemyCanBeAttackedBeforeWeakClueProtectionExpires` |
| 막힌 복귀의 방어·같은 앵커 복원 | `PermanentlyBlockedReturnDefendsInPlaceThenResumesOriginalAnchor` |
| 막힌 순찰의 방어·같은 회차 재개 | `LockedOnlyPassageDefendsWithoutNewEpisodeAndUnlockResumesPatrol` |
| 일반문 근접 개방·실제 통과 | `NpcPlansThroughOrdinaryDoorButOpensOnlyWhenNearAndThenPasses` |
| 잠금문 유지·우회 | `LockedDoorIsNotOpenedAndNpcUsesAvailableDetour` |
| 높은 프레임률의 짧은 이동 | `TinyMovementStepStillOpensNearbyDoorWithoutRemoteOpening` |
| 지도 연속 변경에도 계산 기회 보존 | `MapRebuildEveryFrameDoesNotStarveWaitingAgents` |
| 무전 폭주·중복 합치기·자연 배출 | `RadioBurstCoalescesDuplicatesBoundsQueueAndEventuallyDrains` |
| 실제 미완료 생성의 전역 상한 | `GenerationLimitUsesFallbackAndCancelledWorkKeepsSlotUntilActuallyFinished` |
| 미확인 조사 결과 대사 | `ExhaustedUnconfirmedClueSpeaksDoubtOnceAfterArrival` |
| 확인했던 적을 놓친 대사 | `LostIdentifiedEnemyUsesLostContactLineInsteadOfDoubt` |
| 늦은 복귀 대사 취소 | `LateReturnGenerationIsDiscardedWhenCurrentEnemyRequiresNewResponse` |
| 20명 발견·재탐색 부하 | `TwentyNpcDiscoveryReplanningAndRadioLoad` |
| 40명 발견·재탐색 부하 | `FortyNpcDiscoveryReplanningAndRadioLoad` |

이 표는 대표 검사다. 전체 목록은 XML에 있다. 기존 자체 A*·구역/몸체 제한·유한 순찰·추적 마감/앵커 고정·단서 만료/중복·시야·공격 취소 검사도 함께 통과했다. 기억은 AiMemoryTests, 문 닫기/잠금 끼임 거부는 AiDoorTests, 무전과 대사 경계는 AiRadioTests/DialogueTests/DialogueLifecycleTests에서 확인한다. 수업 알고리즘을 NavMesh나 외부 AI 패키지로 교체하지 않았다.

## 이번 실행에서 발견해 수정한 문제

| 첫 실패 | 원인 | 수정과 재확인 |
|---|---|---|
| EditMode 4개 | 문 fixture가 OnEnable 자동 실행을 전제해 지도에 문을 등록하지 않았음. 대기열 정리 fixture의 SendMessage는 EditMode에서 Unity assertion을 발생시킴. | 연결 뒤 Rebuild를 명시하고 비공개 정리 함수는 검사에서 직접 reflection 호출. 기대 행동을 낮추지 않고 전체 재실행. |
| PlayMode 부하 2개 | 코루틴에서 만든 측정 컴포넌트가 첫 Update 이전 LateUpdate에서 미초기화 배열에 접근함. | Stopwatch가 시작된 온전한 프레임만 측정. 두 600프레임 표본과 예외 없음 확인. |
| PlayMode 문 통과 2개 | 매우 짧은 프레임에서 개방 감지 거리가 한 걸음으로 줄어 물리 접촉 여유 앞에서 멈춤. 별도 재현에서 x=-0.46, Patrol, Collider 차단 확인. | 실제 이동량과 독립된 가까운 0.5단위 전방 조회. 작은 0.0001 이동 회귀를 추가하고 원격/역방향·잠금 거부도 유지. |

첫 실행은 EditMode 175/179, PlayMode 24/28이었다. 최종 소스의 결과와 섞지 않는다. [첫 EditMode](EditMode-first.xml), [첫 PlayMode](PlayMode-first.xml), [문 원인 재현](Door-diagnostic.xml)을 보존했다. 최종 결과는 위 180+28 전체 실행이다. 개발 검토 중에는 지도 Revision 변경 때 FIFO 표를 취소하면 계산 기회가 계속 밀릴 수 있음을 찾아, 대기 순서를 보존하고 매 프레임 지도 변경 회귀로 확인했다.

## 20/40명 측정

| NPC | 중앙값 p50 | p95 | 최대 | 계산/프레임 최대 | 측정 중 계산 합계 | 경로 대기 최대 |
|---|---|---|---|---|---|---|
| 20명 | 0.1910ms | 1.4869ms | 2.9129ms | 4회 | 200회 | 16명 |
| 40명 | 0.3704ms | 1.7482ms | 8.9469ms | 4회 | 400회 | 36명 |

Ryzen 9 9950X3D·RTX 5070 Ti·Windows 11·Unity 6000.3.24f1에서 측정했다. 40×32 격자, 실제 공통 대상과 단순 도형, 자동 Update만 사용했다. 준비 120프레임 뒤 600프레임을 측정하며 60프레임마다 지도 변경과 서로 다른 총성 16건을 넣었다.

측정값은 가장 이른 Update부터 첫 LateUpdate까지의 실제 경과 시간이다. 감각·판단·경로·충돌·무전·지도 재구축은 포함하고 렌더링·후속 LateUpdate는 제외한다. 게임 시간 간격 1/60초 설정은 60fps 달성을 뜻하지 않는다. 두 표본 모두 개인 기억 최대 8개, 프레임당 경로 요청 최대 4회, 모든 NPC의 발견·재계산 및 입력 중단 뒤 대기열 해소를 통과했다. 공격 이벤트는 두 표본 모두 0이므로 **사격 부하 검증이 아닌 발견·재탐색 부하 표본**이다. 20/40명은 확정 최대 NPC 수가 아니며 최종 게임 FPS도 보장하지 않는다.

원본: [20명 기록](crowd-load-20.txt)·[600프레임 CSV](crowd-load-20.csv), [40명 기록](crowd-load-40.txt)·[600프레임 CSV](crowd-load-40.csv). 기존 [대상 없는 20명 순찰 표본](performance-20-agents.txt)은 다른 조건이므로 위 수치와 합산하지 않는다.

## 인계와 한계

[인수인계 및 사양 확인](../../Docs/적_NPC_AI_인수인계_및_사양확인_2026-11-06.md)에 전체 흐름·팀 피드백 반영·초기 수치·문/전투/외형 연결·남은 작업을 정리했다. 10월 31일/11월 5일 ZIP, 원 설계, 20쪽 PDF는 SHA-256이 이전 값과 일치한다. 새 사양은 이번 문서를 우선한다.

최종 팀 맵/모델/Animator/플레이어/피해·실제 Qwen·실행 파일은 이번 범위에 없다. 문은 Collider를 즉시 개폐하며 끼임 검사는 EnemyAgent 몸체에 한정한다. 플레이어/물건 끼임과 문 애니메이션 시점은 팀 연결 시 확인한다. 잠긴 유일 통로가 영구 폐쇄되면 AI는 제자리 경계를 유지하며 임의 개방·순간이동하지 않는다. 셀 중심 A*는 좁은 경계를 보수적으로 거부할 수 있다.
