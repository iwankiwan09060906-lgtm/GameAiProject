# NULL TRACE 적 AI 이동 및 대사 출력 알고리즘 분석

이 문서는 **NULL TRACE** 프로젝트의 실제 런타임 코드(`namespace GameAi`)를 기준으로, 적 AI가 **어떻게 인지·판단·이동(교전 포함)** 하는지, **어떻게 상황 대사를 생성·검증·출력**하는지, 그리고 **다른 알고리즘 대신 현재 구조를 선택한 설계 이유**를 정리한 기술 분석 문서입니다.

---

## 1. 전체 아키텍처 개요

적 AI는 크게 **판단·행동 계층(시뮬레이션)** 과 **대사·연출 계층(프레젠테이션)** 으로 완전히 분리되어 있습니다.

```text
[매 프레임 GameManager.Update]
  ├─ TickSimulation(dt)  ── (대화/일시정지 중에는 건너뜀)
  │    ├─ 1. 입력 읽기 & 2. 플레이어 이동·소음·상호작용
  │    ├─ 3~5. AiDirector.Tick(dt)
  │    │    ├─ 이벤트 수집·전달 (소리/단서/시야/무전/조명)
  │    │    ├─ 4프레임 분산 시각 감지 (SightSensor + AwarenessGauge + SuspicionMeter)
  │    │    ├─ 프레임당 최대 2건 A* 경로 계획 (PlanPaths)
  │    │    └─ 적별 EnemyController.Tick(dt, now)
  │    │         └─ FSM(EnemyBrain) ─▶ 교전 시 BT(CombatTree) ─▶ 이동(PlayerMotor)
  │    ├─ 6. 근접 제압 · 무기 사격
  │    ├─ 7. 사망 확정 (DeathEvent)
  │    └─ 8. 미션 판정
  │
  └─ TickPresentation(dt)
       ├─ 시야 · 안개 · 캐릭터 렌더링
       ├─ BarkPicker.Tick(dt)
       │    ├─ 이벤트 병합(0.5s) 및 우선순위 산출
       │    └─ 즉시 대사 / 캐시 / Qwen 비동기 요청(2.0s 타임아웃) / 기본 대사 폴백
       └─ BarkView.Tick(...) ── 화면에 보이는 적 머리 위 말풍선·감정 연출
```

### 핵심 설계 원칙
1. **단방향 정보 흐름(비간섭 원칙)**: 대사 생성(`BarkPicker`, `QwenBarkSource`)은 `AiDirector`와 `EnemyController`가 만들어 낸 **인지·상태 전이 결과를 읽기만** 하며, 생성된 대사나 감정이 AI의 의심도·상태·이동·공격에 **절대 되먹임되지 않습니다.**
2. **단일 실행 순서 보장**: 적을 포함한 모든 시스템은 자체 `Update()`를 가지지 않고 `GameManager`가 `Tick(dt)`를 정해진 순서로 호출합니다.
3. **난수원 분리(`Rng.Ai` vs `Rng.Fx`)**: 순찰 대기 시간·수색 지점·사격 오차 등 AI 판단용 난수는 `Rng.Ai`, 기본 대사 선택 등 연출용 난수는 `Rng.Fx`를 사용해 연출이 AI 재현성에 영향을 주지 않습니다.

---

## 2. 적 AI 이동 및 행동 알고리즘

적 AI의 움직임은 **감각 인지 → 상태 판단(FSM) → 교전 세부 선택(BT) → 경로 탐색(A*) → 물리적 격자 이동**의 5단계 파이프라인으로 동작합니다.

### 2.1 감각 인지 (Perception)

`AiDirector`가 월드에서 발생한 이벤트와 플레이어 위치를 일괄 검사해 각 적(`EnemyController`)에 전달합니다. 플레이어 좌표를 직접 읽는 곳은 `AiDirector` 한 곳뿐입니다.

#### ① 시각 감지 (`Sense`, `SightSensor`, `AwarenessGauge`)
- **4프레임 분산 처리**: `enemy.Id % phases != frame % phases` (`ai.json`의 `perception_phase_count = 4`) 조건으로 매 프레임 전체 적의 1/4만 시각 판정을 수행하여 CPU 부하를 분산합니다.
- **시야 조건 (`SightSensor.CanSee`)**:
  1. 거리 판정: 기본 시야 거리(`view_m × difficultyMult`, 경비병 14m·소총병 18m)에 플레이어 칸의 밝기(`LightGrid`) 배율 `0.5 + 0.5 * L`을 곱합니다. 단, `near_dist_m`(5m/3m) 이내 근접 시야 예외가 적용됩니다.
  2. 시야각(FOV): 적 정면 벡터(`Facing`) 기준 부채꼴 각도(경비병 110°, 소총병 90°) 안인지 검사합니다.
  3. 차폐 검사: `Physics2D` 대신 자체 **DDA 격자 레이캐스트(`LineOfSight.IsClear(..., RayMask.Sight)`)** 와 연막탄 연기 차폐(`sightBlocked`)를 통과해야 보인 것으로 판정합니다.
- **인지 게이지 (`AwarenessGauge`, 0~100)**:
  - 시야에 보이면 초당 `rise_per_s`(80) × 배율만큼 상승합니다.
  - 상승 배율(`RiseMultiplier`): 5m 이내 `×2.0`, 달리기 `×1.5`, 웅크리기 `×0.5`, 밝기 `×(0.5 + 0.5 * L)` (5m 이내면 밝기 배율 생략).
  - `30` 이상이면 의심(`Investigate`) 및 `SIG_GLIMPSE` 누적, `100`에 도달하면 발견(`DetectionEvent`, `SIG_INTRUDER`)이 확정됩니다.
  - 시야에서 사라지면 `hold_after_lost_s`(3.0초) 동안 게이지를 유지한 뒤 초당 `10`씩 감소합니다.
- **시야 상실 시 예측 이동 (`EnemyController.SetSight`)**:
  - 적이 플레이어를 놓치는 순간 마지막 관측 속도 `seenVelocity`를 이용해 `LastKnownPlayer + ClampMagnitude(seenVelocity * 1.5f, 5f)` 지점까지 총알 레이캐스트가 막히지 않는 범위 내에서 예측 위치를 갱신해 그쪽으로 추격합니다.

#### ② 청각 감지 (`HearSound`, `SoundPropagation`)
- 소음 이벤트(`SND_*`) 발생 시 반경 `radius` 내의 적들에게 `SoundPropagation.Intensity`로 세기를 계산합니다.
  $$\text{Intensity} = \left(1 - \frac{d}{R}\right) \times (\text{wall\_mult})^{\text{벽 수}}$$
  - `wall_mult`는 `0.5`이며, 0.5m 격자 맵 등에서 벽 타일이 여러 칸 두께로 이어져 있어도 `WallCounter`가 연속 구간을 **벽 1개**로 계산합니다.
  - 야외 돌풍(`gustActive`, 20초 주기 4초 지속) 중 야외 바닥(`FloorType.Outdoor`)에서 난 소리는 `wind.mult`(0.6)이 추가 곱해져 마스킹됩니다.
  - 최종 세기가 `react_threshold`(0.2) 이상이면 인지합니다.

#### ③ 사물 단서 · 무전 · 의심도 (`SuspicionMeter`)
- **사물 단서(`SeeClueObjects`, `SeeDoors`)**: 추격(`Chase`)·교전(`Engage`) 중이 아닐 때만 바닥 단서(시체, 탄피, 혈흔, 떨어진 무기)와 플레이어가 조작해 평소와 달라진 문(`CLU_DOOR_OPEN`/`CLOSED`)을 검사합니다. 같은 물체는 적당 1회(`SeenObjects`)만 반응합니다.
- **동료 경보(`SendAlert`, `DeliverRadio`, `RadioChecks`)**:
  - 처음 게이지 100에 도달하면 경비병은 외침(반경 12m), 소총병은 무전(전체)으로 `0.5초` 지연 후 `CLU_RADIO_HELP`를 전달합니다(신뢰도 `0.8`, 사건당 5초 간격 최대 2회).
  - 소총병은 45초마다(`radio_check_s`) 아직 사망 사실을 모르는 동료가 죽었는지 점검해 `CLU_NO_ANSWER`를 생성합니다.
- **의심도(`SuspicionMeter`, 0~100)와 긴장도**:
  - 적마다 최대 16개(`MaxRecords`)의 `ClueRecord`를 유지하며 남은 기여도의 합이 의심도입니다. 동일 `eventId`는 직접 인지와 무전 중 큰 값 하나만 유지합니다.
  - 새 단서가 3초(`quiet_s`)간 없으면 초당 `6`씩 비례 감쇠하며, 수명이 다한 단서는 즉시 제거됩니다.
  - 구간 `[10, 30, 50, 70, 90]`에 따라 긴장도 6단계(`평상 ~ 높은 위협`)로 나뉘며, 대사 우선순위·문맥·이동 속도(의심도 30 이상 조사 시 추격 속도)에 쓰입니다.

---

### 2.2 상태 판단 및 행동 선택 (하이브리드 FSM + Behavior Tree)

#### ① 상위 의사결정: 6상태 유한 상태 기계 (`EnemyState`, `EnemyBrain.Next`)
매 프레임 `EnemyController.Tick`은 현재 센서 입력을 `BrainInput` 구조체로 묶어 순수 정적 함수 `EnemyBrain.Next`에 넘깁니다.

```text
         ┌──────────────────────────────────────────────┐
         ▼                                              │ (수색 시간 10초 종료)
    [Patrol (순찰)] ──(게이지≥30 또는 새 단서≥10)──▶ [Investigate (조사)]
         │                                              │
         │ (한 프레임에 게이지≥100)                      ├──(단서 도착 & 안 보임)──▶ [Search (수색)]
         │                                              │                               ▲  │
         ▼                                              ▼                               │  │ (새 단서)
    [Chase (추격)] ◀───(게이지=100, 사거리 밖)──────────┴───(게이지=100, 사거리 안)──▶ [Engage (교전)]
         │                                                                              │
         └──────────────(시야 상실, 게이지<100)──────────────────────────────────────────┘
    (* 어느 상태에서든 체력 ≤ 0 이면 최우선으로 Dead 전이)
```

- **조건 충돌 우선순위**: `사망(Dead)` > `눈앞의 플레이어(게이지 100 → Engage/Chase)` > `새 단서(BestClue → Investigate)` > `수색 종료(SearchDone → Patrol)`.
- **상태별 이동 목표**:
  - **Patrol**: `patrol[]` 순찰 지점을 순환하며 각 지점에서 2~3초(`wait_min_s`~`wait_max_s`) 대기합니다. 복귀 시에는 가장 가까운 순찰 지점(`NearestPatrolIndex`)부터 재개합니다.
  - **Investigate**: `Suspicion.BestClue` 우선순위(`최근 목격 > 남은 기여 > 가까운 거리`)로 선정된 단서 위치로 이동 후 1초간 주위를 둘러봅니다(`LookAround`).
  - **Search**: `SearchPointPicker.Pick`을 호출해 중심점 반경 8m 내에서 **닫힌 문을 통과하지 않는 BFS**로 도달 가능한 칸 중 서로 1.5m 이상 떨어진 무작위 3곳(의심도 50 이상이면 5곳)을 뽑아 가까운 순으로 순회 수색합니다(총 10초 × 난이도 × 전역 경계 1.2). 추격 직후 수색이면 `LastKnownPlayer`를 첫 수색 지점으로 추가합니다.
  - **Chase**: `LastKnownPlayer`가 이전 목적지로부터 `chase_move_cells`(3칸) 이상 이동했거나 경로가 없으면 경로를 재요청하며 추적합니다.

#### ② 교전 내부 의사결정: 행동 트리 (`CombatTree.Decide`)
`Engage` 상태에서는 매 프레임 **Selector 방식의 행동 트리**(`CombatTree.Decide`)가 위에서 아래로 조건을 평가해 가장 먼저 참인 단일 행동(`CombatAction`)을 선택합니다.

| 우선순위 | 조건 | 선택 행동 | 실제 동작 (`TickEngage`) |
| :---: | --- | --- | --- |
| 1 | `allowCover && coverAvailable && (hpRatio <= 0.4 \|\| reloading \|\| (lineBlocked && visible))` | `TakeCover` | 0.5초 주기로 6m 반경 내(2칸 스텝 샘플링) 벽/엄폐물 옆 중 사선이 가려지는 가장 가까운 엄폐 지점(`FindCover`)으로 이동, 탄창이 비었으면 재장전 병행 |
| 2 | `magEmpty \|\| reloading` | `Reload` | 정지 후 재장전 (`EnemyActionEvent("reload")` 발행), 시야에 보이면 조준 회전 |
| 3 | `!inRange` | `Approach` | 엄폐 목표 해제 후 `LastKnownPlayer`로 접근 이동 |
| 4 | `visible` | `Shoot` | 정지 후 `LastKnownPlayer` 조준, `nextShotTime` 경과 시 `Hitscan` 발사 (조준 오차 `±5° / difficultyMult`, 플레이어 달리기 시 `×1.5`) |
| 5 | 그 외 (시야 가려짐 등) | `MoveToLastSeen` | 마지막 목격 위치로 이동 |

---

### 2.3 경로 탐색 및 격자 이동 알고리즘 (`AStarPathfinder`, `EnemyController.Move`)

#### ① 프레임 예산 기반 경로 스케줄링 (`AiDirector.PlanPaths`)
- 적이 목적지를 바꾸거나(`SetDestination`), `0.5s` 이상 막히면 `wantsPath = true`를 표시합니다.
- 재계산 최소 간격(`recalc_min_interval_s = 0.3s`), 경로 실패 시 `retry_delay_s = 1.0s` 간격 최대 `3회` 재시도 제한이 있습니다.
- `AiDirector.PlanPaths`는 큐(`pathQueue`)를 사용해 **프레임당 최대 `2건`(`max_paths_per_frame = 2`)** 만 `AStarPathfinder.FindPath`를 호출해 한 프레임에 다수 적의 길찾기가 몰려 프레임 드랍이 나는 것을 막습니다.

#### ② 커스텀 A* (`AStarPathfinder`)
- **8방향 탐색 & 옥타일(Octile) 휴리스틱**:
  - 직선 이동 비용 `1.0`, 대각선 이동 비용 `1.41421356`.
  - 휴리스틱 함수: `Heuristic(a, b) = (dx + dy) + (Diagonal - 2f) * min(dx, dy)`.
- **대각선 모서리 끼임 방지**: 대각선 이동 시 인접한 가로·세로 두 칸 중 하나라도 막혀 있으면 통과를 금지합니다.
- **문 비용 페널티**: 닫힌 문 타일(`TileType.Door && !IsDoorOpen`)은 통과 가능으로 취급하되 이동 비용 `+2.0`(`DoorCost`)을 부과해 열려 있는 우회로가 가깝다면 그쪽을 선호하게 합니다.
- **2단계 몸 반경 여유(`Clearance`) 탐색**:
  - 0.5m 격자처럼 칸 크기가 몸 반경보다 작으면 벽 인접 칸을 막은 `walkableWide` 지도로 먼저 경로를 찾고, 경로가 없을 때만 여유 없는 `walkableRaw`로 폴백 탐색합니다.
- **성능 최적화 (GC 할당 제로)**:
  - `stamp[]` 타임스탬프 배열(`searchId++`)로 매 탐색 시 `g[]`, `closed[]` 전체 배열을 `Array.Clear` 하지 않고 O(1)로 초기화합니다.
  - 직접 구현한 고정 배열 기반 `MinHeap`으로 힙 할당을 제거했습니다.

#### ③ 실제 이동 · 문 열기 · 좁은 통로 충돌 해소 (`EnemyController.Move`, `PlayerMotor.Move`)
- **닫힌 문 처리**: 다음 경로 셀이 닫힌 문이면 정지한 채 문을 바라보고 `0.5초`(`door_open_wait_s`) 대기 후 `world.TryOpenDoorAt`으로 문을 열고 통과합니다.
- **좁은 통로 충돌 회피**:
  - `stuckTime > 0.5초`이고 `Patrol` 상태이며 앞에 다른 적이 있으면(`AllyAhead`), 진행 방향의 수직 벡터 `(-step.y, step.x)`로 비켜섭니다.
  - `stuckTime >= 2.0초`(`overlap_after_s`) 이상 계속 막히면 `PushOutOfTargets`를 건너뛰어 임시 겹침을 허용함으로써 좁은 복도 데드락을 방지합니다.
- **격자 충돌 해소 (`PlayerMotor.Move`)**:
  - `Physics2D` 없이 X축과 Y축을 분리해 순차 이동하며 벽을 따라 미끄러집니다.
  - `PushOutOfCells`는 **면에 닿은 칸을 먼저 밀고 모서리에 닿은 칸을 나중에 미는 2패스 방식**으로 벽 타일 이음새에 걸려 캐릭터가 튕기는 현상을 방지합니다.

---

## 3. 적 AI 대사 출력 알고리즘

적 대사 시스템은 **이벤트 발생 → 계기 병합 및 점수화 → 즉시/캐시/Qwen 비동기 분기 → 엄격한 검증 및 타임아웃 폴백 → UI 말풍선 연출**의 파이프라인으로 동작합니다.

### 3.1 대사 계기 수집 · 병합 · 우선순위 점수 (`BarkPicker`)

#### ① 수집 대상 이벤트
`BarkPicker`는 5가지 `EventBus` 이벤트와 2가지 내부 타이머를 통해 `BarkTrigger`를 생성합니다.
1. `PerceptionEvent` (`e.isNew == true`일 때만): 22종 월드 이벤트(`SND_*`, `CLU_*`, `SIG_*`) 인지 시.
2. `EnemyStateChangedEvent`: 추격/교전에서 수색으로 넘어갈 때(`lost`), 수색에서 순찰로 복귀할 때(`search_end`).
3. `DamageEvent`: 생존 상태 피격 시(`hit`, `Instant = true`).
4. `EnemyActionEvent`: 재장전 시작 시(`reload`).
5. `WorldEvent("SND_WIND")`: 야외 돌풍 시 화면에 보이는 야외 적 1명이 잡담.
6. **혼잣말 타이머(`IDLE_CHATTER`)**: 순찰 중 화면에 보이는 적 대상으로 `60초`마다 저우선순위 잡담.
7. **동료 대구(`ScheduleReply`)**: 일반 대사 또는 소리 반응 대사 발화 1초 후, `10m` 이내 화면에 보이는 가장 가까운 동료가 `reply` 기본 대사로 응답.

#### ② 0.5초 병합 윈도우 및 점수 산정
동일한 적에게 짧은 순간 여러 사건이 동시에 겹치면(예: 문소리와 발소리) 대사가 난잡하게 덮이는 것을 막기 위해 `mergeWindow = 0.5초` 동안 대기하며 가장 높은 점수의 트리거 1건만 남깁니다(`Instant == true`인 긴급 트리거는 즉시 덮어쓰고 바로 실행).

$$\text{Score} = (\text{priority} \times 10) + (\text{Tension} \times 3) + (\text{화면 중앙 6m 이내} \; ? \; 5 : 0) - (\text{현 추적 내 동일 코드 발화 횟수} \times 5)$$

- 같은 사건을 반복해서 인지할수록 점수가 5점씩 깎여 다른 이벤트에 밀리게 됩니다.

---

### 3.2 대사 생성 분기: 즉시 대사 · 캐시 · Qwen 비동기 큐 (`BarkPicker.Resolve`, `QwenBarkSource`)

`Tick`에서 준비된 트리거는 **생존 확인**, **화면에 보이는 적 여부(`isVisible`)**, **적당 3초 쿨다운(`cooldown_s = 3.0`)** 을 통과한 뒤 다음 순서로 처리됩니다.

```text
[BarkTrigger 준비]
   ├─ 1) 긴급(Instant) 또는 Qwen 꺼짐/오프라인/연속실패중지/고정시드
   │       └─▶ BarkFallback.Pick (기본 대사 즉시 출력)
   │
   └─ 2) QwenBarkSource.TryRequest
           ├─ [캐시 히트] CacheKey 일치 & 최근 5회 대사 미중복 ─▶ 즉시 출력 ("Cache")
           ├─ [중복 사건 제어] 동일 EventId 반응 적이 여럿이면 카메라 중앙에 가장 가까운 1명만 Qwen 허용
           ├─ [적 1명당 1건] 기존 대기/진행 중 요청은 cancelled 처리
           └─ [우선순위 대기열 적재] 최대 4건(queue_max=4), 초과 시 낮은 점수부터 기본 대사 폴백("DROPPED")
                   │
                   ▼ (동시 최대 1건 max_concurrent=1 비동기 HTTP POST)
           [로컬 Ollama / llama-server (127.0.0.1)]
                   ├─ 2.0초 초과 ─▶ 기본 대사 즉시 출력 ("TIMEOUT")
                   │                 (단, 늦게 온 응답도 검증 통과 시 캐시에 저장 "LATE_CACHED")
                   └─ 2.0초 이내 응답 ─▶ BarkValidator.Validate 검증
                                           ├─ 통과 ─▶ 캐시 저장 & BarkPicker.Say ("Qwen")
                                           └─ 실패 ─▶ 기본 대사 폴백 (연속 5회 실패 시 세션 중 Qwen 중지)
```

#### ① 정보 격리 컨텍스트 (`BarkContext`)
Qwen에 넘기는 입력 JSON에는 숫자, 맵 좌표, 플레이어 실제 위치·체력·무기, 의심도 수치, 다른 적의 내부 상태가 **전혀 포함되지 않으며**, 오직 그 적이 알고 있는 **말로 된 구간값**만 들어갑니다.
- 예: `방향`("앞"/"뒤"/"왼쪽"/"오른쪽", 무전이면 생략), `거리`("가까움"<4m / "중간"≤10m / "멀음"), `업무`, `긴장도`, `몸 상태`, `주변 동료`, `장소`, `밝기`, `바람`, `전체 경계`, `세부`(동료 이름·평소 문 상태·바닥), `최근 대사`(최대 5개).
- 이 구간값 조합 중 `code|Route|Direction|Distance|Repeat|Task|Tension|Persona`를 `CacheKey`로 사용해 동일 맥락에서는 LLM 재호출 없이 캐시(키당 최대 5개)를 즉시 재활용합니다.

#### ② 로컬 LLM 호출 및 예열 (`QwenBarkSource`)
- `qwen.json`의 `endpoint`(`http://127.0.0.1:11434/v1/chat/completions`, 모델 `qwen2.5:7b`)로 비동기 `UnityWebRequest` POST를 보냅니다(`IsLocal` 검사로 외부 주소 차단).
- `use_json_schema = true` 설정으로 `{"line": string, "emotion": enum(5종)}` 형태를 강제하고 `max_tokens = 32`로 응답 길이를 제한합니다.
- Ollama 모델 첫 로딩에 수십 초가 걸리는 문제를 완화하기 위해 시작 시 `SendWarmup()`으로 더미 요청을 미리 보내 모델을 메모리에 올립니다(이 예열 요청은 `ActiveCount()`에 세지 않아 일반 요청을 막지 않습니다).

#### ③ 8중 응답 검증 파이프라인 (`BarkValidator.Validate` & 상태 일치 검사)
소형 로컬 모델의 환각·장황함·규칙 위반을 걸러내기 위해 다음 검사를 통과한 대사만 화면에 띄웁니다.

| 검사 단계 | 검증 기준 | 실패 코드 |
| --- | --- | --- |
| 1. 전처리 | `<think>...</think>` 추론 태그 및 양끝 따옴표 제거 (`Clean`) | — |
| 2. 길이·형식 | 빈 문자열 불가, 줄바꿈(`\n`, `\r`) 불가, `StringInfo` 기준 **20자 이내** | `EMPTY`, `MULTI_LINE`, `TOO_LONG` |
| 3. 한국어 | 숫자·영문 ASCII 문자 금지, 한글 비율 60% 이상 (단 `동료 이름`은 예외) | `NOT_KOREAN` |
| 4. 정보 누출 | 입력 방향(`context.Direction`)과 다른 방향 단어, 무기명(`권총`, `소총` 등), 계급 호칭 임의 언급 금지 | `LEAK` |
| 5. 금지어 | `barks.json`의 `banned_words` 포함 여부 (`AI`, `모델`, `플레이어`, `게임`, 욕설) | `BANNED` |
| 6. 반복 금지 | 공백·문장부호 제거 정규화(`Normalize`) 후 해당 적 최근 5개·전체 최근 10개와 일치 금지 | `REPEAT` |
| 7. 감정 보정 | `의심`, `놀람`, `긴장`, `분노`, `안도` 5종 외 값이면 이벤트 기본 감정으로 대체 | — |
| 8. 시의성(Stale) | 응답 도착 시 적이 죽었거나, 취소(`cancelled`)되었거나, `EpisodeId`·`ActionId`가 달라졌으면 폐기 | `STALE` |

#### ④ 기본 대사 폴백 (`BarkFallback.Pick`) & 말풍선 연출 (`BarkView`)
- **기본 대사(`BarkFallback.Pick`)**: 1차로 `when` + (`tension`·`persona`) 정확 일치 후보를 찾고, 없으면 `any` 후보 → 그래도 없으면 `Alias()`(`SIG_INTRUDER`→`detect`, `SND_*`→`investigate_noise` 등)로 대체해 최근 대사를 피한 뒤 `Rng.Fx`로 선택합니다.
- **감정별 말풍선 연출 (`BarkView`)**:
  - 적 머리 위 `offset_m = 0.9m` 위치에 `show_s = 2.0초` 표시 후 `fade_s = 0.3초` 페이드아웃.
  - 감정별 시각 차별화: **의심**(흰색 `#E8E4DA` + 말끝 `…`), **놀람**(노랑 `#F2C230` + 0.2초 가로 흔들림), **긴장**(흰색 + 자간 `-6` 축소), **분노**(빨강 `#C8323C`), **안도**(회색 `#9A9AA0`).

---

## 4. 다른 알고리즘 대신 현재 알고리즘을 선택한 이유

| 영역 | 현재 선택 | 비교 대상(대안) | 현재 알고리즘을 선택한 핵심 이유 |
| --- | --- | --- | --- |
| **상위 의사결정** | **6상태 FSM + 교전 내 얕은 Selector BT** | GOAP, 순수 Utility AI, 강화학습(ML-Agents) | **1) 결정론적 재현성과 디버깅**: 고정 시드 모드에서 3회 연속 동일한 상태 전이 로그를 보장해야 하며, `StateTransitionLogger`가 상태 전환 이유(`reason`)를 명확히 추적할 수 있어야 합니다.<br>**2) 명시적 우선순위**: 잠입 게임 특성상 `사망 > 눈앞 발견 > 새 단서 > 수색 종료` 규칙이 플레이어에게 직관적으로 읽혀야 하며, 복잡한 플래너나 가중치 튜닝 지옥 없이 교전 내 행동(엄폐→재장전→접근→사격)만 BT로 분리하는 것이 가장 견고합니다. |
| **길찾기·공간 판정** | **자체 격자 A* + DDA 레이캐스트 + 2패스 원-칸 밀어내기** | Unity NavMesh + Physics2D | **1) 타일맵 JSON 기반 동적 환경 제어**: 1m/0.5m 격자 JSON을 런타임에 로드하고, 문 개폐에 따른 시야·총알·빛 차단(반엄폐/완전 차폐), 닫힌 문 비용 `+2` 부여, 대각 모서리 관통 금지를 엔진 물리 의존 없이 순수 C#으로 100% 제어할 수 있습니다.<br>**2) 성능 및 EditMode 테스트**: `MonoBehaviour`와 분리되어 Unity 씬 없이 250여 개 EditMode 테스트가 가능하며, `stamp[]`·`MinHeap`·프레임당 경로 예산(2건)으로 적 20명 교전 시에도 AI 처리 시간을 2ms 이하로 유지합니다. |
| **통로 충돌 회피** | **순찰 시 수직 양보 + 2초 정체 시 임시 겹침 허용** | RVO / ORCA (군중 회피) | 실내 맵의 1~2칸 좁은 복도와 문 앞에서는 속도 기반 회피(RVO)가 상호 진동(oscillation)이나 교착을 일으키기 쉽습니다. 순찰 적만 옆으로 비켜주고 2초 이상 막히면 임시 겹침을 허용하는 규칙이 훨씬 단순하고 데드락이 없습니다. |
| **대사 생성 모델** | **로컬 LLM(Qwen2.5 7B) 비동기 + 2초 타임아웃 + 정적 폴백** | ① 정적 대사 테이블만 사용<br>② 클라우드 LLM API<br>③ LLM이 AI 행동까지 결정 | **① 대비**: 사건·거리·방향·반복·성격·환경(밝기/바람)에 따라 매번 달라지는 생동감 있는 반응을 제공합니다.<br>**② 대비**: 싱글 플레이 로컬 PC 환경에서 API 키·네트워크 지연·비용 없이 동작하며, 서버가 꺼져도 폴백으로 100% 플레이 가능합니다.<br>**③ 대비**: 7B 소형 모델의 추론 지연(최대 수 초)과 환각이 게임 프레임(60fps)과 잠입 판정 공정성을 해치지 않도록 대사 연출에만 격리하고 `BarkValidator`로 정보 누출과 메타 발언을 차단했습니다. |

---

## 5. 핵심 코드 구조 요약

| 파일 경로 | 핵심 클래스 / 메서드 | 담당 역할 |
| --- | --- | --- |
| [GameManager.cs](file:///c:/Users/user/Desktop/20/3-1/GameAi/GameAiProject/Assets/_Project/Scripts/Core/GameManager.cs) | `TickSimulation`, `TickPresentation`, `BuildBarkContext` | 매 프레임 시스템 실행 순서 보장, `BarkContext` 조립 |
| [AiDirector.cs](file:///c:/Users/user/Desktop/20/3-1/GameAi/GameAiProject/Assets/_Project/Scripts/AI/AiDirector.cs) | `Tick`, `Sense`, `HearSound`, `PlanPaths`, `SendAlert` | 이벤트 수집, 4프레임 분산 시각 감지, 청각·무전, 경로 예산 관리 |
| [EnemyController.cs](file:///c:/Users/user/Desktop/20/3-1/GameAi/GameAiProject/Assets/_Project/Scripts/AI/EnemyController.cs) | `Tick`, `TickEngage`, `FindCover`, `Move` | 적 개체 제어, FSM/BT 실행, 엄폐 탐색, 사격, 문 열기, 통로 양보 |
| [EnemyBrain.cs](file:///c:/Users/user/Desktop/20/3-1/GameAi/GameAiProject/Assets/_Project/Scripts/AI/FSM/EnemyBrain.cs) | `EnemyBrain.Next`, `CombatTree.Decide` | 6상태 FSM 전이 표 및 교전 Selector 행동 트리 판정(순수 C#) |
| [AwarenessGauge.cs](file:///c:/Users/user/Desktop/20/3-1/GameAi/GameAiProject/Assets/_Project/Scripts/AI/Perception/AwarenessGauge.cs) | `AwarenessGauge.Tick`, `SightSensor.CanSee` | 밝기·자세·거리 반영 시각 인지 게이지 및 부채꼴+DDA 가시성 판정 |
| [SuspicionMeter.cs](file:///c:/Users/user/Desktop/20/3-1/GameAi/GameAiProject/Assets/_Project/Scripts/AI/Perception/SuspicionMeter.cs) | `SuspicionMeter`, `BestClue`, `TensionOf` | 최대 16개 단서 기여 관리, 중복 제거, 감쇠, 긴장도 6구간 계산 |
| [AStarPathfinder.cs](file:///c:/Users/user/Desktop/20/3-1/GameAi/GameAiProject/Assets/_Project/Scripts/AI/Navigation/AStarPathfinder.cs) | `FindPath`, `Search`, `MinHeap` | 8방향 옥타일 A*, 대각 모서리 제한, 문 비용(+2), 여유 반경 2단계 탐색 |
| [SearchPointPicker.cs](file:///c:/Users/user/Desktop/20/3-1/GameAi/GameAiProject/Assets/_Project/Scripts/AI/Navigation/SearchPointPicker.cs) | `SearchPointPicker.Pick` | 닫힌 문 제외 BFS 영역 내 상호 거리 1.5m 이상 무작위 수색 지점 추출 |
| [PlayerMotor.cs](file:///c:/Users/user/Desktop/20/3-1/GameAi/GameAiProject/Assets/_Project/Scripts/Player/PlayerMotor.cs) | `Move`, `PushOutOfCells`, `PushOutOfTargets` | X/Y 축 분리 이동, 면 우선/모서리 후처리 격자 충돌 밀어내기 |
| [SoundPropagation.cs](file:///c:/Users/user/Desktop/20/3-1/GameAi/GameAiProject/Assets/_Project/Scripts/Grid/SoundPropagation.cs) | `Intensity`, `CountWalls` | 거리 선형 감쇠 및 연속 벽 덩어리당 `0.5` 감쇠 계산 |
| [BarkPicker.cs](file:///c:/Users/user/Desktop/20/3-1/GameAi/GameAiProject/Assets/_Project/Scripts/AI/Bark/BarkPicker.cs) | `Offer`, `Score`, `Resolve`, `Say` | 0.5초 병합, 우선순위 점수화, 3초 쿨다운, 잡담/대구 스케줄링 |
| [QwenBarkSource.cs](file:///c:/Users/user/Desktop/20/3-1/GameAi/GameAiProject/Assets/_Project/Scripts/AI/Bark/QwenBarkSource.cs) | `TryRequest`, `Tick`, `Finish`, `Post` | 캐시 조회, 우선순위 큐(최대 4), 비동기 HTTP 요청, 2초 타임아웃/폴백 |
| [BarkContext.cs](file:///c:/Users/user/Desktop/20/3-1/GameAi/GameAiProject/Assets/_Project/Scripts/AI/Bark/BarkContext.cs) | `BarkContext.ToJson`, `BarkValidator.Validate` | 구간값 JSON 직렬화 및 20자·한글·누출·금지어·반복 응답 검증 |
| [BarkConfig.cs](file:///c:/Users/user/Desktop/20/3-1/GameAi/GameAiProject/Assets/_Project/Scripts/Data/BarkConfig.cs) | `BarkFallback.Pick`, `Alias` | 이벤트/긴장도/성격별 기본 대사 폴백 선택 |
| [BarkView.cs](file:///c:/Users/user/Desktop/20/3-1/GameAi/GameAiProject/Assets/_Project/Scripts/UI/BarkView.cs) | `Say`, `Tick`, `EmotionColor` | 머리 위 말풍선, ?/! 아이콘, 감정 5종 색상·흔들림·자간 연출 |

