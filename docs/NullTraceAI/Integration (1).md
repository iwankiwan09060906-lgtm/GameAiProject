# 적 AI 연결 안내

2026-11-06 팀 피드백 반영판. namespace는 `NullTrace.AI`, runtime assembly는 기존 `NullTrace.AI.Runtime`이다. Unity 6000.3.24f1 / XY 2D / 자체 격자 A*를 유지한다. 이 문서의 새 계약이 옛 FSM·무한 순찰 설명을 대신한다.

## 현재 검증 상태

새 사양의 실행 검증을 완료했다. EditMode 180/180, PlayMode 28/28, 실패·스킵 0이다. 최신 결과는 [검증 보고서](../Results/Verification.md), 팀 연결은 [인수인계 및 사양 확인](적_NPC_AI_인수인계_및_사양확인_2026-11-06.md)을 참조한다. 11월 5일의 138+18 통과 기록은 이전 버전 결과로 보존한다.

## 파일 읽는 순서

1. `EnemyTypes.cs`, `EnemyProfile.cs`: 목표·원래 업무·추적 단계와 튜닝값.
2. `EnemyMemory.cs`: 최대 8개 단서, 중복 방지, 단서별 의심도 기여와 만료.
3. `EnemyAgent.cs`: Tick 순서, 현재 시야, Utility 후보, 추적 기록과 복원.
4. `EnemyBehaviorTree.cs`, `EnemyAgent.Actions.cs`: 선택된 목표의 계층형 실행 절차.
5. `EnemyAgent.Movement.cs`: 목적별 이동 영역, A*와 실제 몸체 이동 검사, 실패 복구.
6. `RadioChannel.cs`, `NoiseBus.cs`, `EnemyDialogue.cs`: 사건 발행·지연 전달·선택적 문장 생성.

## 연결 API

`EnemyAgent`의 기존 `Profile`, `Target`, `Noise`, `World`, `PatrolPoints`와 `State`, `Detection`, `FacingDirection`, `LastKnownPosition`, `IsTargetVisible`를 유지한다. `StateChanged(previous,next)`, `AttackRequested(aimPoint)`, `SpeechRequested(line)`도 그대로 사용한다. 실제 탄환·피해·외형·말풍선은 이 이벤트의 소비자가 담당한다.

추가 입력은 다음과 같다.

| 입력 | 의미 |
|---|---|
| `RadioChannel Radio` | 선택적 분대 통신. 수신 완료된 구조화 보고만 개인 기억에 들어온다. |
| `EnemyDialogue Dialogue` | 선택적 문장 생성. 없으면 기존 기본 대사를 바로 출력한다. |
| `bool IsAlive = true` | 이 NPC의 생존 입력. false면 실행·이동·공격을 중지한다. |
| `bool AutoStartPatrol = true` | Reset 시 유효한 방문 목록이 있으면 한 회차 시작. |
| `int PatrolRouteVersion = 1` | 진행 중 경로 변경을 탐지하는 버전. |
| `Rect PatrolArea`, `PursuitArea`, `PostReturnArea` | 월드 XY 사각형. 정확한 `(0,0,0,0)`만 `World.Bounds`를 뜻한다. 다른 비정상/음수/빈 사각형은 설정 오류다. |

읽기 결과는 `CurrentGoal`, `CurrentDuty`, `PursuitPhase`, `Suspicion`, `IsTargetIdentified`, `IsRecoveryProtected`, `GameTime`, `EpisodeId`, `EpisodeAnchor`, `EpisodeStartedAt`, `EpisodeDeadline`, `EpisodeEndReason`, `NextPatrolIndex`, `PatrolRunId`, `GoalReason`, `Memory`, `RadioReceivedCount`다. 기존 `HasKnownPosition`, `IsReturning`, `PathFailed`, `PathError`, `ValidationError`, `PathRequestCount`, `HeardNoiseCount`도 유지한다.

- `State`는 표시 요약이다. Inspect→Alert, Search→Search, PrepareCombat/Attack→Combat, 업무/복귀→Calm이다. 독립 FSM 판단기는 없다.
- `Detection`은 직접 식별 누적 0..1, `Suspicion`은 여러 정보의 남은 기여 합계 0..100이다. 높은 의심도가 현재 직접 식별을 대신하지 않는다.
- `EnemyGoal.Blocked` enum 값은 호환용 선언이며 현재 필수 이동은 원래 목표를 유지하고 `PathFailed`로 막힘을 표시한다. 이탈점 복귀 막힘은 `PursuitPhase.ReturnBlocked`로 구분한다.
- `ValidateConfiguration(out error)`는 연결·마스크·좌표·사각형을 검사한다. 설정 오류는 경고 후 정지하고 수정 시 재개한다.

```csharp
world.ObstacleMask = LayerMask.GetMask("Walls");
world.Rebuild();
enemy.Profile = profile;
enemy.World = world;
enemy.Noise = noiseBus;
enemy.Radio = radioChannel;       // 선택
enemy.Dialogue = dialogue;        // 선택
enemy.SetTarget(playerTarget);
enemy.ResetAgent();
enemy.AttackRequested += OnAttack;
enemy.SpeechRequested += OnSpeech;
noiseBus.Publish(footstepPosition, 6f, 1f, NoiseKind.Footstep);
```

이벤트 소비자는 자신의 OnDisable에서 구독을 해제한다. 벽은 별도 레이어의 비 Trigger Collider2D이고 적/플레이어 레이어는 마스크에서 제외한다. 마스크 0은 오류다. AgentRadius가 실제 로직 몸체 폭을 표현한다. 동일 루트를 동적 Rigidbody2D나 다른 이동 스크립트와 동시에 움직이지 않는다.

## 시간과 초기화

`Update`는 Unity의 게임 시각을 사용하고 `Tick(float dt)` 수동 호출은 `GameTime`을 누적한다. 수동 Tick과 자동 Update를 동시에 사용하지 않는다. dt ≤ 0/NaN/Infinity는 무시한다. 감각·이동 적분은 한 번에 최대 0.25초지만 마감·기억 시각은 전체 유효 dt를 반영한다. 긴 프레임이 추적 예산을 연장하지 않는다. 일시정지는 게임 시간과 모든 판단을 멈추고 대사 생성의 실제 시간 제한만 별도로 흐른다.

`ResetAgent()`는 현재 위치를 초소로, 로직 루트의 right를 최초 방향으로 삼고 기억·경로·진단·대사 제한·추적을 초기화한다. 위치를 옮기지 않고 이벤트 소비자를 지우지 않는다. Disable→Enable도 새 시작이다. 런타임 AddComponent 뒤 필드를 연결하고 Rebuild/Reset을 호출하면 첫 Tick 전 소음 구독도 준비된다. 버스 필드 교체는 다음 Tick에서 이전 구독을 해제한다.

`SetTarget`은 이전 대상의 목격 기억과 실행·대사 요청을 즉시 취소한다. 다른 소리 기억은 유지한다. 직접 필드 교체는 다음 Tick에서 처리한다. 숨거나 죽은 대상의 현재 좌표는 추격에 사용하지 않고 과거에 본 기록은 원래 만료까지 보존한다.

## 판단과 실행

한 Tick의 순서는 업무 도착 기록 → 직접 감각/수신 무전 → 기억 만료/감쇠 → 강제 복귀/보호 → 일반 Utility → 선택 목표의 행동 트리다. 순찰점 도착과 단서가 같은 Tick에 있으면 완료 순번을 먼저 저장한다.

| 목표 | 기본 조건 / 점수 | 절차 |
|---|---|---|
| Idle / Patrol | 원래 업무, 5 / 10 | 초소 대기 / 다음 미완료 방문점으로 이동 |
| Inspect | 의심도 10 진입·5 유지, 미확인 단서, `20+20E` | 단서 방향을 약 1초 확인하고 확인 완료 표시 |
| Search | 30 진입·25 유지, 조사 가능한 미처리 단서, `40+30E-10D` | 계획→이동→조사→다음 지점 반복→처리 완료 |
| PrepareCombat | 직접 식별한 적이 보이면 `80+10E-10D`; 조사할 단서가 없고 70 진입·65 유지면 35 | 허용 사격 위치 접근 / 2초 감시 후 완료 |
| Attack | 현재 직접 식별·생존·시야·사거리·영역, 100 | 행동 내부에서 발사 간격 대기, 발사 직전 조건 재검사 |

E는 강도×신뢰도, D는 거리/추적 반경을 0..1로 제한한 값이다. 직접 식별한 적이 현재 보이면 일반 후보를 PrepareCombat/Attack으로 제한한다. 발사 간격 중에도 Attack 후보는 유효하다. 공격 한 발은 목표 전체의 성공이 아니다.

유효 후보는 기본 0.2초마다 비교하고 현재 목표는 최소 0.5초 유지한다. 새 점수가 10 이상 높아야 바꾼다. 동점이면 현재 목표, 새 선택은 enum 순서와 작은 사건 ID를 우선한다. 생존·시야·만료·영역 위반·강제 복귀는 유지 시간을 기다리지 않는다. Search는 단서 위치부터 최대 3개, 의심도 50 이상이면 최대 5개 허용 지점을 조사한다. 지점별 최대 2초이며 전체 대응 마감이 우선이다.

행동 트리는 Selector/Sequence/Repeat/Action으로 실제 절차를 표현한다. 목표 변경은 트리 진행·이동·대기·대사를 취소한다. 추적 전체 마감과 다음 발사 가능 시각은 그대로 둔다. 완료된 확인/수색은 같은 기록으로 반복하지 않는다. 현재 직접 보이는 적은 처리 완료 기록과 무관하게 대응할 수 있다.

## 추적·순찰·복귀

첫 확인·수색·교전 진입 전에 이탈점·원래 업무·회차 ID/버전/방문 사본/다음 미완료 순번·20초 마감·고정 허용 영역을 저장한다. 제자리 대응도 같은 절차다. 이탈점에 몸 전체가 설 수 없으면 진입을 거부하고 보고한다. 새 단서와 목표 변경은 기록을 갱신하지 않는다.

마감, 영역 위반 또는 유효 대응 소진은 복귀를 시작한다. 복귀는 이탈점에 실제 도착할 때까지 유지하고 새 적·소리로 취소하지 않는다. 감각과 기억 처리는 계속한다. 도착한 Tick에서 원래 업무 복원·이번 추적 사용 단서만 처리 완료·기록 종료·재이탈 보호 시작을 함께 처리한다. 복귀 중 새로 도착한 미사용 단서는 남는다.

`StartPatrol()`은 한 회차를 시작하고 bool을 반환한다. 진행 중 회차/추적/보호를 덮어쓰지 않는다. 방문 목록은 시작 시 고정하며 재방문은 목록에 명시한다. 전부 마친 뒤 초소에 도착해야 `PatrolCompleted`를 한 번 내고 Guard가 된다. 자동으로 다음 회차를 반복하지 않는다. 순찰점 삭제·교체·위치/버전 변경은 정지·보고하며 조용히 건너뛰지 않는다.

복귀 후 RecoveryDuration(기본 2초)은 약한 단서의 재조사 대기다. 다음 순찰점/초소 도착 조건은 없으며, 현재 직접 식별한 적은 대기 중에도 대응할 수 있다. 이미 처리한 단서는 재사용하지 않는다. 일반 복귀 중에는 공격하지 않지만 필수 경로가 3회 실패해 정지한 상태에서는 영역·시야·사거리를 만족한 직접 식별 대상에게 제자리 방어만 허용한다. 이 방어는 기존 추적/복귀 기록과 목적지를 변경하지 않는다.

## 영역과 실패

순찰은 PatrolArea, 마지막 초소 이동은 PostReturnArea, 추적과 이탈점 복귀는 추적 시작 당시 PursuitArea와 이탈점 중심 고정 반경의 교집합이다. 목표를 경계점으로 자동 보정하지 않는다. 몸체 반경을 포함해 A* 후보 구간·실제 출발/끝점·매 이동·최종 도착을 검사한다. 작은 도착 오차와 마지막 통과 가능한 구간을 모두 요구한다. 문/장애물 변경 뒤 `World.Rebuild()`를 호출한다.

응답 후보의 경로 실패는 해당 추적에서 그 단서를 제외한다. 지도 Revision 변경 또는 0.5 월드 단위 이상 달라진 새 직접 목격만 재검토한다. 필수 업무/복귀는 최초 실패 포함 최대 3회, 최소 1초 간격으로 시도한다. 이후 제자리 경계·보고·허용된 방어를 하며 5초 경과 또는 지도 변경 때 재검사한다. 최소 1초 제한은 지도 변경도 지킨다. 실제 이동에 2초 진전이 없으면 실패 처리한다. 닫힌 복귀점은 도착 처리하지 않고 기록을 계속 보존한다.

## 단서·무전·대사

`ClueReport`는 ID·종류·좌표·원본 발생/직접 관측/만료 시각·출처/전달자·강도/신뢰도·순번·직접 식별 여부를 담는다. 개인 `ClueMemory`에는 수신 시각·남은 기여·직접 여부·확인/처리 상태가 있다. 같은 표적의 연속 시야는 같은 `ObservationId`의 갱신이다. 소리는 Publish 한 번마다 새 ID다.

같은/낮은 순번은 위치·시각·점수를 추가하지 않는다. 같은 관측의 간접 재보고는 신뢰도가 높아도 무시한다. 직접 확인은 간접 기여를 한 번 대체한다. 만료된 과거 무전은 버린다. 직접 시각은 같은 관측의 간접 기여를 대체한다. 발소리 15, 문 25, 총성 60(소음권총 30), 장전 20, 비명 50, 깡통·유인탄 30 × 강도 × 신뢰도(직접 1.0, 무전 0.8)가 소리 기여다. 소리 외 단서·시야 이벤트도 같은 식이며 22종 점수·수명은 [DB설계.md](../DB설계.md) 2장이다. 직접 시각은 식별 전 최대 초당20, 식별 후 최소90을 유지한다. 시야가 끊겨도 해당 기여를 즉시 지우지 않는다. 새 유효 정보가 3초 없으면 전체 남은 기여를 비중대로 초당6점 줄인다. 각 단서 만료는 해당 기여만 제거한다.

`ClueKind`는 소리·목격 외에 시체·문 열림·문 닫힘·탄피·혈흔·떨어진 무기·동료 무응답·번쩍임·움직임 포착·불 꺼짐·동료 쓰러짐·문 움직임을 받는다. 같은 물체는 NPC마다 한 번만 기억한다.

조사 완료도 별도 `ClueKind.Investigation` 기록으로 조사 위치·시각·반경을 저장/보고한다. 다른 사건이나 더 최신 목격을 지우지 않고 의심도를 늘리지 않는다.

`RadioChannel` 기본 전달 지연/송신 간격은 각각0.5초다. 개별 수신 큐를 거쳐 `ReceiveRadio`가 호출되어야 개인 기억에 들어온다. 수신 보고는 자동 재전송하지 않는다. Reset/구독 해제는 지난 대기 패킷을 폐기한다. 수동 검증은 `Deliver(gameTime)`를 사용한다. `NoiseBus.PublishAt(position,radius,occurredAt,loudness,kind)`는 수동 게임 시각용이며 일반 게임에서는 `Publish`를 사용한다. 발행자 Update가 먼저여도 현재 Unity 수신 시각으로 받아들이며 원본 발생 시각은 바꾸지 않는다.

`EnemyDialogue.Provider`에는 `IEnemyDialogueProvider.GenerateAsync(EnemyDialogueContext,CancellationToken)`을 구현한 컴포넌트를 선택적으로 연결한다. Agent는 알려진 사실/기본 문장만 전달한다. `SpeechGate`가 요청 빈도를 제한하고 생성기는 2초 실제시간 제한·실패 기본값·대체/취소/늦은 결과 폐기를 담당한다. 일시정지 중 준비된 문장은 보류한다. Agent는 표시 직전 생존·행동/추적 번호·마감과 필요한 현재 시야를 다시 검사한다. 모델 출력은 AI 판단에 쓰지 않는다. 생성기는 `{"line","emotion"}`(20자 이내 한 줄, 감정 5종)을 돌려주고, 길이·한국어·정보 누출·금지어·반복·상황 일치 검사에 실패하면 기본 문장을 쓴다. 실제 Qwen 제공자는 게임 쪽 `QwenClient`가 같은 PC의 `llama-server`로 연결한다(MASTER 7.22, [DB설계.md](../DB설계.md) 4\~6장).

## 튜닝과 검사

새 필드는 EpisodeDuration20, PursuitRadius8, DecisionInterval0.2, MinimumGoalHold0.5, GoalSwitchAdvantage10, RecoveryDuration2, InspectDuration1, PrepareDuration2, SearchPointDuration2, SightMemorySeconds8, SoundMemorySeconds15, SuspicionQuietSeconds3, SuspicionDecayPerSecond6이다. 단위와 제한은 Profile 주석에 있다. 예전 AlertThreshold/AlertDuration/LostSightDelay/SearchDuration/SearchPause는 에셋/호출 호환용으로 남겼으며 현재 판단에 사용하지 않는다.

- `AiLogicTests`: 감각 수식, 비정상 설정, 발행 입력, 대사 빈도.
- `AiMemoryTests`: 중복·신뢰도·최신 관측·기여 대체/감쇠/만료·조사 결과·Utility/BT.
- `AiAgentTests`: 직접 식별과 공격, 기억 공정성, 고정 마감, 복귀 유지, 유한 순찰, 업무 재개 보호, 영역, 삭제/실패/복구, 사망/교체/시간/물리 입력.
- `AiRadioTests`: 지연·수신자 지식·중복·재구독·만료·송신 간격.
- `DialogueTests`는 총괄, 경로 테스트는 경로 담당, PlayMode/시연/실행 결과는 통합 담당 소유다.

AI 담당은 Navigation/GridWorld, Dialogue, asmdef, Demo/Editor/Scenes, 프로젝트/패키지 설정과 Results를 수정하지 않았다. Unity 실행·종료와 UI 조작도 하지 않았다.


## 보완설계 23개 인수 장면 대응표

아래는 **작성된 검사와 소스의 대응표**다. Unity 실행 결과는 통합 보고서에서 별도로 확인한다. ‘통합 보강’은 기존 단위 검사가 해당 숫자·장면의 모든 조합을 직접 실행하지 않는다는 뜻이다.

| 번호 / 장면 | AI 측 검사 또는 소스 근거 | 통합 확인 사항 |
|---|---|---|
| 1 초소에서 첫 대응 | GradualIdentificationCreatesEpisodeBeforeStationaryCombat, ReturnLatchIgnoresNewClueAndVisibleEnemyUntilActualArrivalThenProtects | 초소/원래 Guard 복원 |
| 2 순찰 중간 이탈 후 다음 점 재개 | WaypointArrivalWinsSameTickAsClueAndRestoresNextUnfinishedVisit | 실제 3→4번 경로 표시 |
| 3 다음 추적은 새 기록 | LaterEpisodeGetsNewAnchorAndDeadlineAfterPatrolRecoveryProgress | 새 번호/이탈점/마감 |
| 4 수색→교전·새 단서 | NewCluesAndCombatDoNotResetEpisodeBudget | 고정 영역도 유지 |
| 5 시간 초과 복귀 유지 | ReturnLatchIgnoresNewClueAndVisibleEnemyUntilActualArrivalThenProtects, InvalidAndZeroTimeFreezeTimersButLongFrameDoesNotExtendDeadline | 반경 진입이 복귀 해제로 바뀌지 않음 |
| 6 의심도 35→8 | BuildCandidates의 Search 유지 하한25, UtilityUsesHoldAdvantageAndDeterministicTieButNeverKeepsInvalidGoal | PlayMode FallingSuspicionFromThirtyFiveToEightStopsSearch 통과 |
| 7 의심도5에서 실제 식별 | DirectSightReplacesIndirectContributionAndSurvivesLostSight, GradualIdentificationCreatesEpisodeBeforeStationaryCombat | 5점 시작 + 사거리 밖 조합 보강 |
| 8 고의심·벽 뒤 적 | LightControlsRangeAndOcclusionPreservesHistoricalContributionWithoutAttacking, HighSuspicionWithoutCurrentSightNeverAuthorizesAttack | 시야 없는 공격0 |
| 9 발사 간격 중 목표 유지 | AttackCooldownStaysInsideGoalAndSurvivesGoalChanges | 공격/준비 왕복 없음 |
| 10 내부 목적지·외부 우회 | Move의 A* canTraverse·출발/마지막/실제 구간에 동일 Segment 검사, 경로 담당 영역 테스트 | NPC 수준 우회 장면 결합 확인 |
| 11 문 폐쇄로 복귀 막힘 | SealedAnchorKeepsEpisodeAndUsesBoundedRetryUntilMapReopens | 허위 성공/순찰 완료 없음 |
| 12 문 재개방 | 같은 테스트, BlockedReturnRetainsRetryHistoryAcrossTemporaryWorldDisable | 원래 업무 복원 |
| 13 복귀 중 새 단서/시야 | ReturnLatchIgnoresNewClueAndVisibleEnemyUntilActualArrivalThenProtects | 새 단서는 보존, 공격0 |
| 14 보호 후 같은 적 | CurrentDirectEnemyRemainsActionableWhenHistoricalRecordWasProcessed + 보호 장면 검사 | 보호 종료와 동일 표적의 결합 보강 |
| 15 마지막 지점/삭제 | RecoveryAfterLastVisitMustReachPostEvenWhenTwoSecondsPassed, DeletedOrVersionChangedPatrolPointStopsWithoutSkipping | 초소까지 회차 유지 |
| 16 초소 이동 중 추적 | StartEpisode/FinishReturn이 ReturnToPost 업무와 마지막 순번을 사본으로 복원 | PlayMode IncidentDuringPostReturnRestoresPostDutyAndCompletesOnlyAtPost 통과 |
| 17 같은 총성의 직접/무전 | DuplicateDoesNotAddSuspicionOrRefreshQuietTime, ConfidenceUpgradePreservesDecayedFractionAndProcessedStatus, Radio 중복 검사 | 직접 듣기와 무전을 함께 배치 |
| 18 오래된 무전 | OldHighConfidenceCannotOverwriteNewerObservation, ExpiredPacketNeverRestartsOriginalLifetime | 위치/수명 유지 |
| 19 새 소리와 옛 목격 수명 | ClueReport의 사건별 ExpiresAt, ContributionsDecayProportionallyAndExpiryRemovesOnlyItsEvent | 소리+목격 혼합 만료 장면 보강 |
| 20 수신 전 B의 무지 | ReceiverKnowsNothingUntilDeliveryAndRetransmissionCannotAddScore | 전달 전 개인 기억0 |
| 21 취소 뒤 결과 도착 | ReentrantTargetReplacementCancelsRemainingAttackAndSpeech, BT Reset 검사, 총괄 DialogueTests | A*는 동기 실행이며 늦은 이동 결과 없음 |
| 22 Qwen 없음/지연/지난 응답 | 총괄 DialogueTests + EmitSpeech의 현재 입력 재검사 | 실제 모델 설치 없이 Fake provider 검증 |
| 23 최종 에셋 없음 | 감각/판단은 EnemyTarget·Collider 입력만 사용 | Demo 대체 도형과 UI/PlayMode 담당 |

## 제한된 처리량

기억은 최대 8개, 제거 사건 이력은 최대 32개다. 현재 직접 목격을 우선하고 처리된/오래된 단서부터 버린다. 무전 대기 상한 기본 256개, 전체 실제 미완료 대사 생성 상한 기본 1개·대기열 4개다. 취소 신호만으로 생성 슬롯을 반환하지 않는다. 경로 계산은 월드별 PathRequestBudget에서 기본 4회/프레임으로 순서를 나눈다. 예산 대기는 실패로 세지 않는다. 자세한 상한 초과 처리와 팀 연결은 인수인계 문서를 참조한다.
