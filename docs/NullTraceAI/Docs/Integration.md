# 팀 연결 안내

## 컴포넌트와 좌표

namespace는 `NullTrace.AI`, Runtime assembly는 `NullTrace.AI.Runtime`입니다. 다른 asmdef의 팀 코드에서는 이 assembly를 참조합니다. Runtime은 UnityEngine에만 의존합니다. Demo/UI/Test/Editor 코드는 별도 assembly입니다.

월드는 XY 평면이며 Z는 렌더 정렬에만 씁니다. `GridWorld2D`의 Origin은 격자 왼쪽 아래이고 CellSize는 월드 단위입니다. Collider2D 벽을 전용 레이어에 놓습니다. 트리거는 장애물 샘플에서 제외합니다. Rigidbody2D/3D NavMesh는 AI의 필수 구성 요소가 아닙니다.

1. `GridWorld2D`: Origin/Width/Height/CellSize/AgentRadius/ObstacleMask를 설정하고 `Rebuild()`합니다. `0 < AgentRadius < CellSize / 2`여야 합니다. 샘플은 36×24칸, 한 칸 0.5, 반경 0.15입니다.
2. `NoiseBus`: 씬에 하나를 만들고 각 적의 Noise에 같은 버스를 연결합니다. 반경과 크기 등 비정상 입력은 런타임이 걸러냅니다.
3. `EnemyProfile`: Create > Null Trace > Enemy Profile로 생성합니다. 감지/소리 감쇠/이동/수색/공격/대사 간격을 조절합니다. ScriptableObject를 여러 적이 공유할 수 있습니다.
4. `EnemyAgent`: Profile/World/Noise/Target을 연결하고 순찰점 Transform 배열을 지정합니다. 순찰점은 격자 안의 이동 가능한 위치여야 합니다.
5. `EnemyTarget`: 실제 플레이어의 로직 루트에 붙입니다. `IsAlive`, `Light`만 팀 상태에서 갱신하고, 위치는 이 루트의 transform을 사용합니다. 시연의 SandboxController는 가져올 필요가 없습니다.
6. `RadioChannel`(선택): 분대마다 만들고 Agent.Radio를 연결합니다. 기본 송신 간격과 전달 지연은 각각 게임 시간 0.5초입니다. 전달 전에는 수신자 기억에 나타나지 않습니다.
7. `EnemyDialogue`(선택): Agent.Dialogue에 연결합니다. 제공자 없이 기본 문장으로 동작합니다. 모델을 붙일 때만 `IEnemyDialogueProvider` 구현 컴포넌트를 Dialogue.Provider에 넣습니다.

적을 코드로 생성할 때는 루트를 비활성화한 상태에서 연결한 다음 활성화합니다. 연결을 끝낸 후 `ResetAgent()`로 시작 위치와 상태를 초기화할 수 있습니다. 적·타겟의 레이어를 ObstacleMask에 포함하면 설정 오류입니다.

```csharp
var go = Instantiate(enemyPrefab);
go.SetActive(false);
var enemy = go.GetComponent<EnemyAgent>();
enemy.Profile = profile;
enemy.World = world;
enemy.Noise = noiseBus;
enemy.PatrolPoints = patrolPoints;
enemy.SetTarget(player.GetComponent<EnemyTarget>());
go.SetActive(true);
```

프리팹 생성 시 `Instantiate` 직후 첫 OnEnable이 실행될 수 있으므로, 완전히 동적인 초기화를 원하면 원본 프리팹을 비활성 상태로 저장하거나 비활성 부모 아래에서 생성합니다. 필수 참조 없는 채로 Update가 실행되면 ValidationError/경고를 먼저 고쳐야 합니다.

## 유한 순찰·추적·구역

`PatrolPoints`는 이번 회차에 방문할 **순서 있는 목록**입니다. 시작 시 현재 위치가 초소가 되며, 목록 방문 후 초소에 도착해야 `PatrolCompleted`가 1회 발생합니다. 자동 시작은 `AutoStartPatrol`, 다음 회차는 `StartPatrol()`로 요청합니다. 이미 회차·추적·보호가 진행 중이면 false를 반환합니다. 진행 중 배열을 바꿔 순찰을 재시작하지 말고, 초기화가 필요한 별도 상황에서 `ResetAgent()`를 사용하세요. Reset은 현재 위치를 새 초소로 정합니다.

`PatrolArea`, `PursuitArea`, `PostReturnArea`는 월드 XY의 `Rect`입니다. `(0,0,0,0)`이면 `World.Bounds`를 사용합니다. 추적 중에는 등록 추적 구역과 최초 이탈점 중심 `PursuitRadius` 원의 교집합이 유지됩니다. 목적지뿐 아니라 이동 선분과 적 반경까지 허용 구역 안에 있어야 하므로 벽/구역 밖 단서를 가장자리로 옮겨서 처리하지 않습니다.

`CurrentGoal`은 Utility가 선택한 실행 목표, `CurrentDuty`는 돌아가서 재개할 업무, `PursuitPhase`는 추적/복귀/복귀 차단 단계를 뜻합니다. 기존 `StateChanged`는 애니메이션·표시 연결용 요약으로 유지합니다. 행동을 선택하는 별도 FSM으로 사용하지 않습니다. `Detection`은 0~1의 시각 식별 누적, `Suspicion`은 0~100의 개인 증거 합계입니다.

`EpisodeId/Anchor/Deadline`은 같은 추적 동안 고정됩니다. 복귀 중에는 새 단서가 와도 복귀를 취소하지 않습니다. 실제 앵커 도착 후 원래 업무를 복원하고 약한 단서의 재조사만 기본 2초 기다립니다. 다음 방문·초소 도착은 대기 종료 조건이 아니며 현재 직접 식별한 적은 대기를 우회합니다. 필수 경로는 최소 1초 간격으로 3회 실패하면 제자리 경계·보고·허용된 방어를 수행하며 지도 Revision 변경이나 5초 재검사 때 같은 목적지로 재개합니다. 복귀 완료를 순간 이동으로 대신하지 않습니다.

게임에서는 자동 `Update`를 사용합니다. 수동 검사만 할 때는 자동 진행을 막고 `Tick(dt)`를 호출합니다. 수동 소음은 `PublishAt(..., agent.GameTime, ...)`, 수동 무전은 `Deliver(gameTime)`으로 동일한 게임 시계를 사용합니다. 일반 게임 입력은 기존 `NoiseBus.Publish`를 그대로 씁니다.

## 소음 입력과 출력 구독

소음은 발행 시점의 좌표만 전달합니다. 이후 플레이어의 위치를 소리만으로 추적하지 않습니다. 팀 이동/무기/문 코드가 해당 동작 순간 발행합니다.

```csharp
noiseBus.Publish(player.transform.position, 3f, 0.65f, NoiseKind.Footstep);
noiseBus.Publish(muzzle.position, 16f, 1f, NoiseKind.Gunshot);
noiseBus.Publish(door.transform.position, 7f, 0.8f, NoiseKind.Door);
```

소리는 거리와 벽 개수에 따라 줄어듭니다. 시각은 조도별 거리·원뿔 각도·Collider2D 차폐를 모두 만족해야 증가합니다. 기본 3단계는 Dark/Dim/Bright이며 팀 조명/은신 시스템이 EnemyTarget.Light에 값을 넣습니다.

```csharp
using NullTrace.AI;
using UnityEngine;

public sealed class TeamEnemyBridge : MonoBehaviour
{
    public EnemyAgent Enemy;

    void OnEnable()
    {
        Enemy.AttackRequested += RequestAttack;
        Enemy.SpeechRequested += ShowSpeech;
        Enemy.StateChanged += ChangeAnimation;
    }

    void OnDisable()
    {
        if (!Enemy) return;
        Enemy.AttackRequested -= RequestAttack;
        Enemy.SpeechRequested -= ShowSpeech;
        Enemy.StateChanged -= ChangeAnimation;
    }

    void RequestAttack(Vector2 aimPosition)
    {
        // 팀 전투 시스템에 조준 위치를 전달하고, 실제 발사/피해/아군 판정은 그 시스템에서 처리합니다.
    }

    void ShowSpeech(string text)
    {
        // 팀 말풍선/자막에 text를 표시합니다. 한글을 포함하는 폰트를 연결합니다.
    }

    void ChangeAnimation(EnemyState previous, EnemyState current)
    {
        // 팀 Animator에 상태를 전달합니다. 로직은 Animator 유무와 무관하게 실행됩니다.
    }
}
```

구독자는 OnDisable에서 해제해야 풀링·재활성화 후 중복 요청을 받지 않습니다. AI 내부 Noise 구독도 재활성/버스 교체를 검증합니다. 타겟 교체는 `SetTarget(nextTarget)`을 사용합니다. 대상 비활성/삭제/사망은 별도 통합 테스트에 포함됩니다.

선택적 모델 제공자는 `Task<string> GenerateAsync(EnemyDialogueContext context, CancellationToken cancellationToken)`만 구현합니다. context에는 추적/행동 ID, 확정된 사실과 기본 문장이 들어갑니다. 생성 문장은 AI 행동·의심도·경로를 결정하지 않습니다. 제공자 없음·실패·빈 응답·기본 2초 실제시간 초과는 기본 문장으로 끝나며, 취소/행동 교체 뒤 늦은 응답을 표시하지 않습니다. 일시정지 중에는 출력을 보류합니다. 실제 모델 설치와 네트워크 요청 코드는 이 전달물에 포함하지 않습니다.

## 문과 맵 변경

문 Collider2D의 활성 상태나 벽 위치를 바꾼 뒤 `Physics2D.SyncTransforms(); world.Rebuild();`를 호출합니다. 시야/소리는 실제 Collider2D를 검사하고, 경로는 새 Revision을 보고 재계산합니다. Origin/크기/반경/레이어를 바꾼 경우에도 Rebuild가 필요합니다. 경로 실패는 `PathFailed`, `PathError`, `PathRequestCount`로 확인할 수 있습니다.

격자는 칸을 반경만큼 넓혀 벽을 샘플링하므로 좁은 통로가 보수적으로 막힐 수 있습니다. 이동 가능한 복도 폭을 맵 CellSize/AgentRadius와 함께 맞추세요. 목표를 벽 내부나 격자 밖에 두지 않습니다. 자동으로 가까운 빈 칸에 붙이거나 벽을 통과하지 않습니다.

## 모델 교체와 데이터 경계

`EnemySandbox.prefab`의 Visual 자식만 교체하고 EnemyAgent가 있는 루트와 연결 필드는 유지합니다. 이동 중 FacingDirection을 팀 모델 회전/좌우 반전에 사용합니다. EnemyDebugView의 Visual 참조를 새 SpriteRenderer에 다시 연결하면 상태 색상도 계속 표시됩니다. 참조가 없거나 모델이 SpriteRenderer가 아니어도 Runtime은 계속 동작합니다. 최종 연출에서는 EnemyDebugView를 제거해도 됩니다. 현재 디버그 선은 이동 방향·단서·공격 요청을 보여줄 뿐 투사체가 아닙니다. EnemyDebugView와 SandboxController는 최종 HUD가 아니라 검증 도구입니다.

OS 한글 글꼴은 Demo에서만 동적으로 생성합니다. Runtime과 대사 이벤트는 UI/폰트에 의존하지 않습니다. 최종 게임은 팀이 배포 권한을 가진 한글 폰트를 별도로 연결하세요.

## 공식 API 확인

- [Unity 6000.3 배치 테스트](https://docs.unity.com/en-us/engine/6000.3/manual/programming-environment/test-framework-introduction/running-tests/run-tests-from-command-line)
- [Unity 6000.3 RenderTexture](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/rendertexture)
- TMP OS 폰트 생성과 비대화식 Essentials 가져오기는 설치된 uGUI 2.0.0 소스 `TMP_FontAsset.CreateFontAsset(familyName, styleName, pointSize)`, `TMP_PackageResourceImporter.ImportResources`를 대조했습니다.

캡처는 화면·마우스 조작 없이 카메라 RenderTexture를 읽습니다. `WaitForEndOfFrame`은 Editor batchmode에서 진행하지 않으므로 정상 렌더 프레임을 기다린 뒤 저장합니다. 통합 완료 기록은 `Results/2026-11-06/IntegrationStatus.md`, 새 실행 결과는 같은 날짜 폴더에 기록합니다. `Results/Verification.md`는 최신/이전 검사 보고서의 안내입니다.

## 2026-11-06 피드백 반영 연결

이 절과 [인수인계/사양 확인 문서](적_NPC_AI_인수인계_및_사양확인_2026-11-06.md)는 이전 재이탈 보호·문 설명보다 우선합니다. 약한 단서의 재조사 대기는 기본2초만 요구하며 현재 직접 식별한 적은 우회합니다. 다음 순찰점/초소 도착 조건은 제거했습니다. 일반 복귀 중에는 공격하지 않으며 3회 경로 실패로 정지한 동안만 제자리 방어를 허용합니다.

문에는 AiDoor2D의 World와 독립 DoorCollider를 연결하고 TrySetOpen/TrySetLocked/SetNpcCanOpen으로 상태를 바꿉니다. 문 상태와 지도 Revision이 함께 바뀝니다. 경로는 열 수 있는 문을 계획할 수 있지만 실제 이동은 가까이에서 연 후 물리 검사를 거칩니다. 최종 플레이어의 문 끼임 방지와 애니메이션 개방 완료 시점은 팀 문 코드에서 연결해야 합니다.

World의 PathRequestBudget은 기본4회/프레임으로 경로 계산을 분산합니다. 개인 유효 단서는8개, 제거 이력은32개, 무전 대기 사본은 채널당 기본256개입니다. 대사 생성은 NPC당현재요청1개/전체실제미완료작업2개가 기본입니다. 부하 초과로 오래된 정보/신규 무전이 버려지거나 대사가 기본 문장으로 바뀔 수 있으며, 게임 판단은 생성기를 기다리지 않습니다.
