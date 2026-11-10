# Null Trace 적 NPC AI 연결 규약

갱신: 2026-11-06. 기본 구조는 이전 보완 설계를 유지하고, 변경 사양은 `적_NPC_AI_인수인계_및_사양확인_2026-11-06.md`를 우선한다. Unity 6000.3.24f1 / URP 2D / XY 평면을 유지한다. 최종 에셋·플레이어 모델은 나중에 연결한다.

## 판단과 실행

1. 행동 불가 → 복귀/막힌 필수 업무 유지 → 약한 단서 재조사 대기 → 일반 Utility 순으로 우선한다. 현재 직접 식별한 적은 재조사 대기를 우회한다.
2. Utility가 목표를 고르고 계층형 행동 트리가 절차를 실행한다. `EnemyState`는 기존 UI 요약이다.
3. 첫 대응에서 이탈점·마감·영역·원래 업무·순찰 진행을 고정한다. 새 단서와 목표 전환은 이를 초기화하지 않는다.
4. 복귀는 실제 도착까지 유지한다. 일반 복귀 이동 중 공격하지 않으며 3회 경로 실패로 정지한 때만 허용 영역 안의 현재 직접 식별 대상에게 제자리 방어한다. 도착 후 기본 2초는 약한 단서만 보류하며 순찰점/초소 도착을 요구하지 않는다.
5. 순찰은 허가받은 한 회차와 초소 복귀까지다. 지점 삭제·이동·버전 변경은 오류로 드러내며 건너뛰지 않는다.
6. 자체 A*에 목적별 사각형·고정 원·몸체 반경을 적용한다. 실제 Collider2D 충돌도 검사하며 목적지 투영·순간이동하지 않는다.
7. 기억은 수신자별로 관리한다. 사건 ID·관측 시각을 보존하고 중복 점수를 더하지 않는다. 무전은 직접 시야·공격 허가가 아니다.
8. 실제 피해·탄환·미션·영구 저장은 팀 시스템이 담당한다. Qwen은 선택적 문장 생성 연결까지 제공하며 출력(대사·감정)은 AI 판단에 쓰지 않는다.

## 공개 연결

namespace `NullTrace.AI`, assembly `NullTrace.AI.Runtime`.

| 구성 | 사용하는 기능 |
|---|---|
| `EnemyTarget` | `IsAlive`, `Light`; 감지 위치는 해당 Transform |
| `EnemyProfile` | 감각·목표·추적·수색·이동·공격·대사 수치. 단위·사용 여부는 소스 주석 참조 |
| `EnemyAgent` 입력 | `Profile`, `Target`, `Noise`, `World`, `Radio`, `Dialogue`, `PatrolPoints`, `IsAlive` |
| 순찰·구역 | `AutoStartPatrol`, `PatrolRouteVersion`, `PatrolArea`, `PursuitArea`, `PostReturnArea` |
| 시작·초기화 | `StartPatrol()` 성공 여부; `ResetAgent()` 현재 위치를 새 초소로 초기화; `SetTarget()` 대상 교체 |
| 판단 | `CurrentGoal`, `CurrentDuty`, `PursuitPhase`, `GoalReason`, `EpisodeEndReason` |
| 감각·기억 | `Detection` 시각 식별 0..1, `Suspicion` 개인 의심 0..100; `IsTargetVisible`, `IsTargetIdentified`, `HasKnownPosition`, `LastKnownPosition`, `Memory` |
| 진행·오류 | `EpisodeId`, `EpisodeAnchor`, `EpisodeDeadline`, `NextPatrolIndex`, `PatrolRunId`, `IsRecoveryProtected`, `GameTime`, `PathFailed`, `PathError`, `ValidationError` |
| 횟수 | `PathRequestCount`, `HeardNoiseCount`, `RadioReceivedCount` |
| 이벤트 | `StateChanged(이전, 다음)`, `AttackRequested(조준 XY)`, `SpeechRequested(문장)`, `PatrolCompleted()` |

Rect `(0,0,0,0)`만 미지정으로 `World.Bounds`를 쓴다. 다른 Rect는 유한한 양수 크기여야 한다. 순찰 회차 동안 방문점 위치·참조·순서·버전을 유지한다. 완료 후 `StartPatrol()`로 새 회차를 허가한다.

기존 `EnemyState { Calm, Alert, Search, Combat }`, `LightLevel { Dark, Dim, Bright }`를 유지한다. `NoiseKind`는 `{ Footstep, Gunshot, Impact, Door }`에 `Reload`, `Scream`, `Can`, `Decoy`를 더한다(점수는 [DB설계.md](../DB설계.md) 2장). 새 세부 이름은 `EnemyTypes.cs`가 기준이다.

## 사건·무전·대사

- `NoiseBus.Publish(Vector2 position, float radius, float loudness = 1f, NoiseKind kind = Footstep)`: 정상 플레이 소리 입력. 발생 시각과 ID를 한 번 부여한다.
- `PublishAt(Vector2 position, float radius, double occurredAt, float loudness = 1f, NoiseKind kind = Footstep)`: 수동 시계 검증용. 원래 발생 시각을 전달한다.
- `ClueReport`: ID·종류·위치·발생/관측/만료 시각·출처·보고자·순번·세기·신뢰도·식별 여부의 사본. Transform 공유 없음.
- `RadioChannel`: 기본 지연 0.5초. 직접 관측을 보내고 수신 보고를 자동 재방송하지 않는다. 수동 `Deliver(double gameTime)`에는 NPC와 같은 시계를 쓴다.
- `EnemyDialogue.Request(EnemyDialogueContext)`, `Cancel()`, `LineReady`: 선택적 생성 연결. `IEnemyDialogueProvider.GenerateAsync(context, cancellationToken)`은 `Task<string>`을 신속히 반환한다. 주 스레드 동기 추론 금지.
- NPC당 현재 대사 요청 하나, 전체 동시 1건·대기열 4건, 실제 시간 제한 기본 2초. 응답은 `{"line","emotion"}`(20자 이내 한 줄, 감정 5종)이며 실패·빈 응답·검증 실패 시 기본 문장, 취소·지난 상황 응답 폐기. 일시정지 중 출력 보류.

## 경로

- 기존 `GridMap(width, height)` 및 `AStarPathfinder.TryFindPath(map, start, goal, path, allowDiagonal)` 유지.
- 여섯 번째 인수 `Func<Vector2Int, Vector2Int, bool> canTraverse` 오버로드로 탐색 중 구간 제약 적용. 시작·도착도 `(cell, cell)`로 검사한다.
- 성공 경로는 시작·끝 포함, 실패는 false와 빈 목록. 막힌 시작/끝·구역 밖·대각 벽 모서리 관통 금지.
- `MovementConstraint2D(Rect)` 또는 `(Rect, anchor, radius)`의 `Contains(center, bodyRadius)`, `AllowsSegment(start, end, bodyRadius)` 사용. 지원 영역은 볼록한 사각형·원 교집합이다.
- `GridWorld2D`: `Origin`, `Width`, `Height`, `CellSize`, `AgentRadius`, `ObstacleMask`, `Bounds`, `Rebuild()`, `CanTraverse(start, end)`. 문·장애물 변경 뒤 Rebuild로 격자와 Revision 갱신.
- 실제 좌표↔셀 중심 연결·경로 간선·최종 도착·매 이동에 같은 영역/몸체 제약 적용.

## 담당과 검증

`(기말) 적 AI`는 AI Runtime·AI EditMode·AI.md, `(기말) 경로 탐색`은 Navigation·GridWorld2D·경로 테스트·Navigation.md, `(기말) 통합 검증`은 Demo·Editor·씬·프리팹·PlayMode·실행 증거·배포를 담당한다. 총괄은 규약·읽기 안내·대사 연결·상태·독립 검토를 담당한다. 이번 수정의 Unity 실행은 총괄 하나로 제한한다.

새 동작은 [11월6일 검증 결과 및 검사 대응](../Results/2026-11-06/Verification.md)과 해당 문서의 Unity XML로 확인한다. 이전 23개 설계 항목의 유지 규칙에 이번 피드백 변경을 적용했다. 원본 독립 프로젝트의 최종 결과는 EditMode 180개/PlayMode 28개 통과이며, 이 저장소의 Unity 버전에서 재실행한 결과는 아니다.

10월 31일 ZIP은 복원 기준으로 보존하고 새 전달본은 2026-11-06 이름으로 만든다. 11월 5일 ZIP도 보존한다. Week07Lab·Notion·원본 설계·수업 Unity 버전은 변경하지 않는다. 최종 모델/Qwen 실행과 20명 조건의 성능 목표는 해당 환경에서 별도 검증하며 증거 없이 달성으로 보고하지 않는다.


2026-11-05 완료 기록: 통합 채팅의 서비스 자원 오류 이후 총괄이 Unity 실행 소유권을 인수했다. 최종 EditMode 138/138, PlayMode 18/18 통과. 실제 결과는 Results/2026-11-05/Verification.md에 기록했다.

## 11월 6일 처리량·문 계약

개인 단서 8개/제거 이력 32개, 무전 대기 기본 256개, 전체 대사 실제 미완료 작업 기본 1개·대기열 4개, 월드별 경로 계산 기본 4회/프레임. 상한 초과의 정보 손실/기본 대사 사용/경로 대기는 사양에 명시하며 무제한 작업을 쌓지 않는다. AiDoor2D의 상태 API로 문·격자를 함께 갱신한다. 열 수 있는 문은 계획에 포함하지만 실제 몸체는 가까이에서 문이 열린 뒤에만 통과한다. 진단에는 IsPathBlocked, PathRequestBudget.PendingCount/RequestsThisTick을 추가한다.
