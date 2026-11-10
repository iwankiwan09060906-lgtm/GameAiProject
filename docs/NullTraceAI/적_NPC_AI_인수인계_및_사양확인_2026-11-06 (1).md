# 문과 경로 연동 — 2026-11-06

NPC가 열 수 있는 닫힌 문은 경로에 포함할 수 있다. 실제로는 문 가까이 간 뒤 문을 열고, 기존 몸체 충돌 검사에 통과한 만큼만 움직인다. 잠긴 문과 NPC 개방 권한이 없는 문은 기존 벽과 같은 장애물이다. 우회로는 기존 자체 A*로 찾으며 허용 영역을 넓히지 않는다.

이 문서는 경로 담당의 구현 및 연결 계약이다. Unity 실행·전체 통합·새 배포 검증은 총괄 담당이다. 이전 2026-11-05 검증 결과와 ZIP은 변경하지 않았다.

## 담당 파일

- `Assets/NullTraceAI/Runtime/GridWorld2D.cs`: 문이 포함된 계획 지도, 계획용 구간/끝점 검사, 가까운 문 개방.
- `Assets/NullTraceAI/Runtime/AiDoor2D.cs`와 `.meta`: 문 상태, 잠금/권한 API, 닫기 안전 검사, 상태 변경 알림.
- `Assets/NullTraceAI/Tests/EditMode/AiDoorTests.cs`와 `.meta`: 문 계약 14개 테스트(총괄의 짧은 걸음 회귀 포함).

자체 A* 알고리즘과 MovementConstraint2D, 기존 CanTraverse/TryMove의 물리 안전 로직은 유지한다. 스케줄러와 EnemyAgent 계열 파일은 이 작업에서 수정하지 않는다.

## Inspector 연결

1. `AiDoor2D`를 문 제어 오브젝트에 붙이고 `World`와 `Door Collider`를 연결한다. Collider는 다른 오브젝트에 있어도 된다.
2. Collider는 World.ObstacleMask에 포함된 레이어의 독립된 비 Trigger Collider2D여야 한다. 벽과 합쳐진 CompositeCollider2D는 지원하지 않는다. Rigidbody2D가 있다면 Simulated를 켠다.
3. `Locked`와 `Npc Can Open`으로 초기 정책을 지정한다. 기본은 잠금 해제·NPC 개방 허용이다. 처음 닫힌 문은 Collider를 켜 둔다.
4. 실행 중 잠금·개방 권한·열림은 아래 API로 변경한다. Collider.enabled나 Inspector의 초기 정책을 직접 바꾸면 지도 버전 갱신이 보장되지 않는다.

런타임 생성 시 비활성 오브젝트에 참조를 연결한 뒤 활성화하면 OnEnable이 지도를 갱신한다. 이미 활성화된 컴포넌트의 World/Collider 연결이나 문 위치·크기를 바꿨다면 해당 World.Rebuild()를 호출한다. World 자체를 바꾸는 경우 이전 World도 재구축한다.

초기 Collider가 꺼져 있는데 Locked가 켜져 있으면 안전하게 닫기부터 시도한다. NPC가 끼어 닫을 수 없으면 열린 상태를 유지하며 잠금도 적용하지 않는다. 잘못된 연결은 `ValidateConfiguration(out error)`로 한국어 사유를 확인할 수 있다.

## 총괄 Movement 연결 계약

| API | 의미와 호출 위치 |
|---|---|
| `World.Map` | 이제 **계획 지도**다. 열린 문뿐 아니라 NPC가 열 수 있는 닫힌 문 자리도 통행 후보다. 실제 충돌 통과를 의미하지 않는다. |
| `World.CanPlanOccupy(position)` | 계획용 끝점 검사. 닫힌 문 안에 있는 셀 중심·목적지 후보를 허용할 수 있다. 실제 출발점/도착 성공에는 CanOccupy를 쓴다. |
| `World.CanPlanTraverse(start, end)` | A* 간선 및 실제 위치↔셀 중심 양끝 연결 검사. 문을 열거나 움직이지 않는다. 호출자가 기존 `area.AllowsSegment(..., World.AgentRadius)`도 함께 적용한다. |
| `World.TryOpenDoorAhead(start, desired)` | 허용 영역을 확인한 실제 한 걸음 직전 호출한다. 가까운 첫 장애물 한 개만 검사하며 최대 0.5 월드 단위의 쓸기 거리(+기존 Skin, 몸체 반경)를 사용한다. 새 문을 열었을 때만 true다. |
| `World.CanTraverse` / `World.TryMove` | 실제 충돌 검사다. 닫힌 문을 무시하지 않는다. 문 개방 요청의 성공 여부와 관계없이 실제 이동에는 TryMove를 계속 사용한다. |

계획 직전에는 실제 출발점에 `CanOccupy(position)`, 계획 목적지에 `CanPlanOccupy(destination)`를 적용한다. 목적지 자체가 문 자리에 놓이는 것을 게임 규칙상 금지하려면 목적지의 실제 CanOccupy 검사를 유지할 수 있으나, 일반적인 문 통과 셀은 계획 검사를 써야 한다. 실제 도착 판정은 기존 작은 오차 및 CanTraverse 최종 구간 확인을 유지한다.

개방이 성공하면 World.Revision이 증가하므로 캐시 경로는 다음 이동 판단에서 무효화된다. NPC가 아주 가까이 간 뒤 작은 한 걸음을 요청해도 문을 발견할 수 있다. 먼 목적지를 전달해 여러 문을 미리 여는 방식은 아니다. 앞에 일반 벽이나 다른 장애물이 있으면 그 뒤 문은 열지 않는다.

## 팀 문 코드에서 사용하는 API

| AiDoor2D API | 계약 |
|---|---|
| `bool TrySetOpen(bool open)` | 수동 개방/닫기. 잠긴 문 열기는 false. NPC 몸이 낀 문 닫기도 false. 이미 요청 상태이면 true이며 재구축하지 않는다. 수동 조작은 NpcCanOpen과 별개다. |
| `bool TrySetLocked(bool locked)` | 잠금은 닫힘까지 함께 적용한다. 닫을 수 없으면 잠금·열림·Revision을 모두 보존하고 false다. 해제는 문을 자동 개방하지 않는다. |
| `void SetNpcCanOpen(bool allowed)` | NPC 개방 권한을 바꾸고 지도를 동기화한다. 이미 열린 문을 강제로 닫지는 않는다. |
| `IsOpen`, `IsLocked`, `NpcCanOpen`, `CanNpcOpen` | 현재 상태/정책 조회. IsOpen은 실제 DoorCollider.enabled를 기준으로 한다. |
| `event Action<AiDoor2D> StateChanged` | 상태·물리 동기화·지도 재구축 후 알림. 실패하거나 동일 상태인 요청에는 새 알림이 없다. |

변경은 `Rebuild()`를 통해 Physics2D.SyncTransforms와 Revision 갱신을 먼저 끝내고 알린다. 현재 버전의 열림은 즉시 Collider를 끄는 방식이다. StateChanged는 소리·표시를 연결하기 위한 **완료 알림**이며, 애니메이션이 끝날 때까지 기다리는 승인 요청이 아니다. 이후 실제 문 애니메이션을 넣을 때는 물리 개방 완료 시점을 이 API와 함께 맞춰야 한다.

## 닫기 안전과 범위

닫기/잠금 요청 시 활성 GameObject의 EnemyAgent들을 확인한다. 판단 컴포넌트가 꺼져 있거나 다른 World를 사용하는 NPC도 검사한다. 각 NPC의 World.AgentRadius와 문 Collider의 실제 윤곽에 가장 가까운 점을 비교하므로 NPC에 별도 Collider가 없어도 몸체 폭을 지킨다. NPC의 World가 없으면 이 문의 World.AgentRadius를 사용한다. 비정상 좌표/반경은 안전하게 닫기를 거부한다.

꺼진 Collider의 ClosestPoint는 입력 점을 그대로 반환하므로, 조회 동안만 Collider를 켠 뒤 finally에서 원복한다. 이 동기 호출 안에서는 물리 시뮬레이션이나 NPC 이동을 실행하지 않는다. 겹침이 없다고 확인한 뒤 실제 닫힘을 적용한다. 닫기가 거부되면 원래 열린 상태와 잠금·Revision·알림 수가 유지된다.

팀 플레이어·별도 캐릭터·물건·애완 캐릭터 등의 실제 몸체는 아직 이 안전 검사에 연결하지 않았다. EnemyAgent가 없는 물체까지 보호한다고 가정하면 안 된다. 비활성 GameObject는 검사 대상에서 제외된다. 같은 Collider를 여러 AiDoor2D가 소유하면 계획에서는 안전하게 일반 장애물로 취급한다.

## 비용과 한계

- 문 목록 검색과 전체 격자 재구축은 문 상태 변경/Rebuild 때 수행한다. 경로 간선 검사에서는 Collider→문 사전 조회를 재사용한다.
- NPC 목록 검색은 열린 문을 닫거나 잠글 때만 수행한다. 매 프레임 수행하지 않는다.
- CanPlanTraverse는 기존 물리 조회와 같은 원형 몸체 검사를 사용한다. 문을 추가했다고 물리 검사를 제거하지 않는다.
- 이 변경에는 NPC 경로 요청 예산·대기 경계 행동·증분 격자 갱신을 추가하지 않았다. 문이 매우 많은 큰 맵의 전체 재구축 비용은 실제 팀 맵에서 측정해야 한다.
- 잠긴 문은 통과하지 않는다. 우회로도 없을 때의 경계/제자리 방어 정책은 총괄 소유 Movement에서 연결한다.

## 검증 상태

최초 담당 작성은 13개, 총괄 추가 후 최종 14개 EditMode 테스트 통과: 닫힌 개방 가능 문 계획과 실제 충돌 분리, 잠금/권한·Revision·알림, 원격/역방향 개방 방지, 한 번에 문 하나, 앞 벽과 겹친 별도 벽, 컴포넌트 해제, NPC 몸체 닫기/잠금 거부, 반복 요청, 몸체 폭/구역, 잠긴 문 우회, 다른 World 연결, 비정상 좌표.

경로 담당의 최초 인계 시점에는 Unity Editor를 실행하지 않았다. 정적 검사에서는 Unity 6000.3.24f1 DLL·NUnit 참조로 담당 코드/테스트와 Navigation/AiMath를 Roslyn 컴파일했다. EnemyAgent는 병행 수정 중이므로 이 검사에서 현재 사용하는 `World` 필드만 가진 메모리 내 API 대역으로 대체했다. 실행 검증이나 전체 프로젝트 컴파일 통과를 뜻하지 않는다. 이후 총괄이 실제 EnemyAgent를 포함한 전체 EditMode 180개/PlayMode 28개 통과를 확인했다. 문 개방 조회는 걸음 크기와 독립된 전방 0.5단위로 보완했다. [최종 검증](../Results/2026-11-06/Verification.md)이 완료 근거다.

## 확인한 Unity 6000.3 문서

- [Collider2D.ClosestPoint](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/collider2d/closestpoint): 비활성 Collider의 반환 규칙.
- [Physics2D.CircleCast](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/physics2d/circlecast): List 재사용 및 가까운 충돌 순서.
- [Object.FindObjectsByType](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/object/findobjectsbytype): 비활성 GameObject 제외와 정렬 생략.
- [Collider2D.compositeOperation](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/collider2d/compositeoperation-1): 결합 Collider의 형상 소유권.

적용한 공식 스킬 참조: create-2d-physics/SKILL.md 및 references/legacy-2d-physics.md. 기존 Physics2D/Collider2D 방식과 수업 Unity 버전을 유지한다.
