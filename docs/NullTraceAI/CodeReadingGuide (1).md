# AI 팀 피드백 구현 메모 · 2026-11-06

AI 담당 소스/테스트 작성 및 정적 컴파일 완료. 통합 실행을 위해 소스 고정. 이전 156개 테스트/11-05 ZIP은 이전 버전 검증이며 새 사양 결과로 사용하지 않는다. Unity 실행/이동 코드/PlayMode/통합/배포는 총괄 소유다.

## 변경 계약

- RecoveryDuration(기본2초)은 복귀 뒤 약한 단서의 재조사 대기만 뜻한다. 다음 순찰점/초소 도착 AND 조건을 삭제한다. IsRecoveryProtected는 이 타이머 표시이며, 현재 직접 식별된 적은 이를 우회한다. 공통 복귀 중에는 계속 공격 금지다.
- EnemyAgent.Actions.cs에 `private void GuardBlockedPath(MovementConstraint2D area)`를 제공한다. 총괄은 Movement에서 movementBlocked일 때 호출한다. 메서드는 현재 직접 식별/생존/시야/사거리/전달받은 area를 검사하고 기존 nextAttack을 공유한다. 이동·추적 생성·episode 변경·복귀 완료를 하지 않는다. Agent는 막힌 필수 목표를 보존해 이 경계 행동을 수행할 수 있게 한다.
- 기억 최대8개, 제거 이력 최대32개. 현재 직접 목격은 보호하고 처리된/오래된 기록부터 제거한다. 같은 관측의 간접 중복은 무시하며, 실제 직접 확인은 간접 기여를 한 번 대체한다. 신뢰도 비례 병합은 제거한다. 과거 사건 ID의 무제한 영구 저장은 하지 않는다.
- RadioChannel.MaxPendingDeliveries 기본256(1..4096), PendingCount/DeliveryScanCount/CoalescedCount/DroppedCount 진단. 같은 수신자/사건은 전달 전 한 사본만 유지한다. 먼저 대기한 관측과 전달 시각을 보존해 최신 재전송이 전달을 끝없이 미루거나 새 관측의 지연을 단축하지 않게 한다. 상한 초과 신규 패킷은 버린다. 다음 전달 예정 시각 전에는 큐를 재스캔하지 않는다.
- EnemyDialogue.MaxConcurrentGenerations는 전역 static 설정(기본1, 1..32), ConcurrentGenerationCount는 실제 미완료 요청 수다. 슬롯이 없으면 우선순위 대기열(최대4)에 넣고, 대기열이 넘치면 점수가 낮은 요청부터 생성기를 호출하지 않고 기본 대사를 사용한다. 우선순위 식과 합치기 규칙은 [DB설계.md](../DB설계.md) 6장이다. 취소/timeout은 UI 요청만 폐기하며 Task가 실제 완료되어야 전역 슬롯을 반환한다. 영원히 끝나지 않는 제공자는 슬롯을 계속 차지하므로 제공자가 실제 작업 수명을 Task로 정확히 표현해야 한다.
- 복귀 결과 대사는 사건 소진/실제 적 상실/현재 적 목격/시간 제한을 구분한다. 생성기가 없으면 도착 즉시 기본 문장을 시도하고 비동기 생성은 정상 업무 재개 후 한 번 요청하며, 행동 변경/시야 변화/새 추적에서는 늦은 결과를 폐기한다. SpeechGate의 전체·동일 문장 간격은 유지한다.

## 통합 시 주의

기존 PlayMode에서 보호 중 직접 적에게 무조건 발사0/다음 순찰점 또는 초소까지 대기를 기대한 부분은 새 사양으로 바꿔야 한다. 실제로 막힌 복귀 중 방어만 예외이며 이동 중 공통 복귀에서는 여전히 발사0이다. 가짜 대사 제공자의 미완료 Task는 테스트 정리에서 실제 완료/취소해야 전역 동시성 슬롯이 다음 검사로 남지 않는다. 전역 카운터를 임의 초기화해 누수를 숨기지 않는다.


## 고친 파일과 검증

수정한 Runtime은 EnemyAgent.cs, EnemyAgent.Actions.cs, EnemyMemory.cs, EnemyProfile.cs, RadioChannel.cs, EnemyDialogue.cs 6개다. SpeechGate.cs는 기존 전체/동일 문장 제한을 그대로 재사용했으며 수정하지 않았다. 수정한 검사는 AiAgentTests.cs, AiMemoryTests.cs, AiRadioTests.cs, DialogueTests.cs 4개다. AiLogicTests.cs는 기존 5개 검사를 유지했다.

Unity 6000.3.24f1 설치의 Roslyn, 실제 UnityEngine/.NET Standard 2.1/NUnit/TestRunner 참조로 현재 전체 Runtime과 AI 소유 검사 assembly를 정적 컴파일했다. 모두 exit0/컴파일 진단0. AI/Dialogue 검사 사례는 88개(Agent42·Memory19·Radio8·Dialogue14·기존 Logic5)다. 이전 AI/Dialogue64개에서24개를 추가하고 보호/신뢰도 병합 관련 기존 검사를 새 사양으로 갱신했다. 이 숫자는 작성/컴파일된 사례 수이며 Unity 실행 통과 수가 아니다.

| 변경 | 주요 검사 |
|---|---|
| 약한 단서 대기와 직접 식별 우회 | WeakClueWaitsOnlyForMinimumTimeAfterReturn, ReturnLatchHoldsUntilArrivalThenDirectSightBypassesWeakClueDelay, RecoveryDelayExpiresWithoutFinishingNextVisitOrPost |
| 복귀 대사의 근거 | ExhaustedUnconfirmedClueSpeaksDoubtOnceAfterArrival, LostIdentifiedEnemyUsesLostContactLineInsteadOfDoubt, DeadlineReturnUsesTimeLimitAndCurrentVisibilityFacts(false/true), FreshClueWaitingDuringReturnDoesNotGetDoubtLine |
| 비동기 복귀 대사 수명 | LateReturnGenerationIsDiscardedWhenCurrentEnemyRequiresNewResponse, SlowReturnGenerationSurvivesNormalDutyResumeAndFallsBackOnce |
| 막힌 필수 목표 방어 | BlockedReturnDefendsWithoutChangingEpisodeAndSharesAttackInterval, BlockedPatrolDefendsWithoutStartingResponseEpisodeOrSkippingVisit |
| 기억8·제거이력32·직접 우선 | FloodIsBoundedAndCurrentDirectSightSurvivesWeakSounds, ProcessedRecordIsEvictedBeforeOlderUnprocessedClue, EvictedDuplicateCannotRestoreScoreOrReopenInvestigation, ForgottenVeryOldPacketCannotEvictRecentUnprocessedClue, SameObservationDirectConfirmationReplacesIndirectWithoutAddingOrReopening |
| 무전 대기열/중복/스캔 | WholeChannelQueueHasHardLimitAndDropsExcessNewRecipients, InFlightSameEventKeepsOneSnapshotAndOriginalDelay, RepeatedNpcDeliverCallsDoNotRescanUnchangedQueue, LoweringLimitTrimsTailAndZeroDelayStillDeliversNewInformation |
| 전체 생성 자원 | GlobalLimitFallsBackWithoutCallingThirdProvider, CancelAndTimeoutKeepSlotUntilUnderlyingTaskActuallyFinishes, NullThrowAndAlreadyCompletedTasksReturnTheirGlobalSlots |

총괄이 추가한 Movement의 IsPathBlocked/GuardBlockedPath 호출과 동작 계약을 소스에서 확인했다. 해당 파일·Navigation·GridWorld·문 코드·PlayMode·Demo·기존 안내 문서·Results·ZIP은 이 채팅에서 수정하지 않았다. Unity 실행/종료·UI 조작도 하지 않았다.

## 남는 제한

- 최근 제거 사건32개 이후의 영구적인 중복 차단은 제공하지 않는다. 오래된 패킷의 만료와 최신 단서 우선 비교는 계속 적용한다.
- 큐가 가득 차면 초과 신규 수신 사본을 버린다. DroppedCount로 확인할 수 있으며 송신 성공은 모든 수신자 전달 보장이 아니다.
- 같은 사건이 아직 전달 중이면 최초 대기 관측을 보존한다. 더 최신 관측은 전달 후 다음 송신 기회에 보낸다.
- 실제로 끝나지 않는 제공자 작업은 전역 슬롯을 계속 차지한다. 이때 신규 요청은 기본 대사를 사용한다. 제공자가 조기에 완료된 Task를 반환하고 별도 추론을 계속하는 행위까지 이 컴포넌트가 감지할 수는 없다.
- 실제 Qwen 추론과 게임 전체 프레임 성능은 이 정적 검사로 검증하지 않았다. Qwen 응답은 20자 이내 한 줄 + 감정 5종이며 검증 실패가 연속 5회면 그 실행 동안 생성기를 끈다([DB설계.md](../DB설계.md) 5·6장).

## 총괄 최종 확인

AI/Dialogue 88개를 포함한 EditMode 180개와 PlayMode 28개가 실제 Unity에서 통과했다. [최종 검증](../Results/2026-11-06/Verification.md)이 새 사양의 실행 근거다.
