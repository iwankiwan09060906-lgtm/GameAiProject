# 자체 격자 A* 길찾기

이익재 담당 길찾기 구현이다. NavMesh나 외부 알고리즘 패키지 없이 `NullTrace.AI`의 `GridMap`과 `AStarPathfinder`를 사용한다. 2026-11-05 보완 설계에 따라 `MovementConstraint2D`와 구간 제한 A*를 추가했다. 영역 계산은 Unity의 `Rect`·`Vector2`, 격자는 `Vector2Int`를 사용하며, 실제 물리 충돌 검사는 `GridWorld2D`가 맡는다.

## API와 사용

```csharp
var grid = new GridMap(20, 12); // 처음에는 모든 칸이 이동 가능
grid.SetWalkable(new Vector2Int(5, 3), false);
var path = new List<Vector2Int>();
bool found = AStarPathfinder.TryFindPath(
    grid, new Vector2Int(1, 1), new Vector2Int(10, 8), path);
```

- `GridMap(int width, int height)`: 양수 크기, 전체 칸 수는 `int.MaxValue` 이하. 잘못된 크기는 `ArgumentOutOfRangeException`이다. 실제 생성 가능한 크기는 가용 메모리에도 제한된다.
- 읽기 전용 `Width`, `Height`와 `Contains(Vector2Int cell)`로 범위를 확인한다.
- `IsWalkable(Vector2Int cell)`은 범위 밖이면 `false`다. `SetWalkable(Vector2Int cell, bool walkable)`은 범위 밖이면 `ArgumentOutOfRangeException`이다.
- `TryFindPath(GridMap grid, Vector2Int start, Vector2Int goal, List<Vector2Int> path, bool allowDiagonal = true)`는 먼저 출력 목록을 비운다. 성공 시 시작과 도착을 포함한 경로를 저장한다.
- 경로 없음, 범위 밖, 막힌 시작 또는 도착은 `false`와 빈 경로다. 이동 가능한 같은 시작·도착은 그 한 칸으로 성공한다.
- `grid`나 `path`가 `null`이면 `ArgumentNullException`이다. `grid`만 `null`인 경우에도 전달된 출력 목록은 비운다.
- 기존 오버로드를 유지하며, `TryFindPath(grid, start, goal, path, allowDiagonal, canTraverse)`를 추가했다. 마지막 인자는 `Func<Vector2Int, Vector2Int, bool>`이며 `null`이면 기존 동작이다.
- `canTraverse(from, to)`는 실제 후보 구간의 통행 여부를 판정한다. 시작과 목표에는 각각 `(cell, cell)`을 전달하므로 유효한 제자리 구간도 허용해야 한다. 제한에 걸린 끝점이나 경로는 `false`와 빈 목록으로 반환한다. 검사 함수가 던진 예외는 호출 코드의 오류로 보고 그대로 전달한다.

## 알고리즘 핵심

1. 열린 목록에서 `f = g + h`가 가장 작은 칸을 선택한다. `g`는 시작부터 실제로 이동한 비용, `h`는 도착까지 남은 비용의 하한이다.
2. 이웃으로 가는 비용이 기존 기록보다 작으면 비용과 부모 칸을 갱신한다. 이미 열린 목록에 있던 칸도 최신 비용으로 비교한다.
3. 도착을 열린 목록에서 꺼냈을 때 부모를 역추적하고 뒤집어 경로를 만든다. 열린 목록이 소진되면 실패다.

직교 이동 비용은 **10**, 대각 이동은 **14**다. 대각 이동은 목적 칸뿐 아니라 양쪽 직교 이웃도 모두 이동 가능해야 한다. 벽 모서리를 비스듬히 통과하지 않는다. `allowDiagonal = false`면 직교 4방향만 탐색한다.

`dx`, `dy`를 목표와의 좌표 차이의 절댓값이라고 하면 4방향의 휴리스틱은 `10 × (dx + dy)`인 맨해튼 거리다. 8방향은 `14 × min(dx, dy) + 10 × |dx - dy|`인 옥타일 거리다. 장애물이 없는 최단 비용이므로 장애물이 생겨도 과대평가하지 않는다. 두 휴리스틱은 인접 칸 사이의 이동 비용에 대해 일관적이므로, 확정한 칸을 다시 열지 않아도 최단 경로를 보장한다.

동점은 **f → h → y → x** 오름차순으로 해소한다. 같은 비용의 부모 후보는 먼저 선택된 부모를 유지한다. 따라서 같은 격자와 인자는 같은 경로를 반환한다. 누적 비용과 휴리스틱 계산은 `long`을 사용한다.

구간 제한은 경로가 완성된 뒤에만 검사하는 것이 아니라 이웃을 탐색할 때 적용한다. 아래쪽 우회로가 구역 밖이어도 위쪽에 허용된 길이 있으면 계속 탐색한다. 제한은 이동 가능한 간선을 제거할 뿐 비용을 낮추지 않으므로 기존 휴리스틱의 허용성과 일관성이 유지된다. 한 탐색 동안 지도와 검사 함수의 결과는 일정해야 한다. 대각선 옆 두 칸의 물리 벽 검사는 그대로 유지하지만, 이동하지 않는 옆 칸의 중심까지 NPC별 허용 영역 안에 있을 필요는 없다.

## 몸체를 포함한 영역 제한

`public readonly struct MovementConstraint2D`의 계약은 다음과 같다.

| API | 의미 |
|---|---|
| `MovementConstraint2D(Rect bounds)` | 사각형 구역만 허용한다. |
| `MovementConstraint2D(Rect bounds, Vector2 anchor, float radius)` | 사각형과 고정 원의 교집합만 허용한다. |
| `Contains(Vector2 center, float bodyRadius)` | 그 위치에 원형 몸 전체가 들어가는지 검사한다. |
| `AllowsSegment(Vector2 start, Vector2 end, float bodyRadius)` | 직선 이동 중 몸 전체가 영역 안에 유지되는지 검사한다. |

사각형은 양수 너비·높이여야 하고 모든 값은 유한해야 한다. 원·몸체 반경은 0 이상이다. 0 반경 몸체는 점, 0 반경 원은 중심 한 점을 의미한다. 음수·NaN·무한대·빈 사각형·끝 경계가 넘치는 사각형·기본값 구조체는 조회 시 `false`다. 생성자는 값을 자동 보정하거나 예외를 던지지 않는다. 경계에 정확히 닿는 것은 허용하지만 밖으로 나가는 오차 여유는 추가하지 않는다.

예를 들어 x=0~10인 사각형에서 몸체 반경이 1이면 중심은 x=1~9에 있어야 한다. 추적 원의 반경이 5이고 몸체 반경이 1이면 중심 간 거리는 4 이하여야 한다. 내부의 거리·제곱 계산은 `double`로 하여 큰 유한 좌표에서 `float` 계산이 넘치거나 몸체 여유가 사라지는 문제를 줄인다.

사각형과 원, 그 교집합은 **볼록 영역**이다. 몸체 반경만큼 줄인 영역도 볼록하므로 양 끝의 몸 전체가 들어가면 두 끝을 잇는 직선 구간 전체도 들어간다. 따라서 `AllowsSegment`의 끝점 판정은 이 형태에 대해 전 구간을 판정하는 방식이며, 듬성듬성 중간 점을 검사하는 근사가 아니다. 오목한 구역이나 여러 사각형의 합집합은 표현하지 않는다. L자 복도의 외접 사각형을 대신 넣으면 복도 밖까지 허용하므로 그렇게 사용하면 안 된다.

## 갱신과 통합

탐색 비용·부모·열린/닫힌 목록은 호출마다 새로 만든다. `SetWalkable`로 문이나 장애물을 바꾼 뒤 다시 호출하면 바뀐 격자를 사용하며 이전 탐색 상태를 재사용하지 않는다. 기존에 반환한 경로는 자동으로 바뀌지 않으므로 장애물 변경 시 호출자가 재탐색한다. 하나의 탐색 도중 다른 스레드가 같은 격자를 변경하는 사용은 지원하지 않는다.

월드 좌표와 격자 좌표의 변환, 충돌체를 이용한 장애물 등록, 적의 실제 이동과 재탐색 주기는 Unity 어댑터가 맡는다. `GridWorld2D.Bounds`는 전체 월드 사각형이며 NPC별 구역을 대신하지 않는다. `CanTraverse(start, end)`는 실제 Collider2D를 향해 몸체 원을 쓸어 보는 기존 `TryMove` 검사를 재사용한다. 이 조회는 Transform을 옮기지 않으며 끝점이 열려 있어도 중간 벽이나 몸체 옆부분이 걸리면 실패한다. 물리 API는 [Unity 6000.3 CircleCast 문서](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/physics2d/circlecast)를 확인했다.

`TryMove`는 일부만 움직였으면 `false`와 가능한 위치를 반환한다. 아주 짧은 구간도 목적지·벽 검사를 생략하지 않으며, 완전히 통과했을 때만 `true`를 반환한다. 기존 `Skin` 여유 거리는 유지하므로 실제로 벽과 가까운 구간을 보수적으로 거부할 수 있다.

AI 연결 순서는 다음과 같다.

1. 이동 목적에 맞는 사각형과, 추적/이탈점 복귀인 경우 같은 추적에 저장된 원으로 제약을 만든다. 순찰 권한을 추적에 합치거나 복귀 시 전체 맵으로 넓히지 않는다.
2. 정확한 현재 위치와 목표 위치에 `Contains(..., world.AgentRadius)` 및 실제 점유 검사를 적용한다. 범위 밖 목표를 경계에 보정하지 않는다.
3. `TryGetCell`로 시작·목표 칸을 구한다. A*의 검사 함수는 아래처럼 칸 중심 간 영역 검사와 실제 충돌 검사를 함께 수행한다.
4. 실제 위치→시작 칸 중심, 목표 칸 중심→정확한 목표 위치도 같은 두 검사로 확인한다. 이 연결 구간은 A*의 칸 목록만으로 검증되지 않는다.
5. 실제 이동 직전에 같은 제약과 `CanTraverse`/`TryMove`를 다시 적용한다. 문 변경 시 `Rebuild()`로 지도 버전을 갱신하고 경로를 다시 구한다. 실패 횟수·보고·복귀 유지·순찰 재개는 AI 상태 코드가 맡는다.

```csharp
bool CanTraverseCell(Vector2Int from, Vector2Int to)
{
    Vector2 a = world.CellCenter(from);
    Vector2 b = world.CellCenter(to);
    return constraint.AllowsSegment(a, b, world.AgentRadius) && world.CanPlanTraverse(a, b);
}

bool found = AStarPathfinder.TryFindPath(world.Map, start, goal, path, true, CanTraverseCell);
```

같은 셀 안의 실제 위치가 허용되어도 그 셀 중심이 허용되지 않으면 현재 구현은 경로를 거부할 수 있다. 셀 중심 기반 탐색의 보수적인 한계이며, 구역을 확대하거나 제한을 무시해 성공으로 바꾸지는 않는다. 작은 구역과 경계 근처의 목표는 셀 크기·몸체 반경을 함께 검증한다.

열린 목록은 설명하기 쉬운 선형 탐색이다. 방문 칸 수를 V라 하면 탐색 시간의 최악 상한은 O(V²), 탐색용 메모리는 O(V)다. 큰 맵에서 여러 적의 반복 호출이 병목으로 확인되면 동점 규칙을 유지하는 최소 힙으로 교체할 수 있다.

## 검증 범위

`Assets/NullTraceAI/Tests/EditMode/NavigationTests.cs`에 크기·범위 예외, 초기 이동 가능 상태, 최단 우회, 대각 모서리 차단, 4/8방향 비용, 실패 시 출력 초기화, 같은 시작·끝, 동적 차단·복원, 반복 호출의 결정성을 작성했다.

고정 시드 `20261031`로 7×6 격자를 60번 다시 설정하고 매번 6쌍을 탐색한다. 4방향과 8방향 각각 검사하여 총 720개 쿼리의 성공 여부·경로 비용을 독립적인 Dijkstra 구현과 비교한다. 반환 경로의 이동 가능 상태, 인접성, 모서리 통과, 시작·끝, 순환 여부도 검사한다.

2026-11-05 추가한 검사는 구간 제한 안의 대체 경로, 끝점 제한, 대각 구간과 모서리 규칙의 구분, 구간 차단·복구, 제한이 있는 무작위 480개 쿼리의 독립 Dijkstra 비용 비교다. `MovementConstraintTests.cs`는 몸체 포함 사각형/원 경계·교집합·구간·잘못된 수·큰 수 계산을 검사하고, `GridWorldTraversalTests.cs`는 실제 벽·몸체 폭·문 변경·구역 밖 우회·짧은 이동의 끝점 검사를 담당한다.

테스트 어셈블리는 NullTrace.AI.EditModeTests에서 Runtime을 참조한다. 최종 경로 관련 74개를 포함한 EditMode 전체 138/138, PlayMode 18/18 통과. 11월5일 보존 결과 (원본 작업 폴더의 보존 기록)를 참조한다. CircleCast 접촉 오차로 끝점이 겹칠 때는 안전한 출발점과 첫 접촉 이하 구간을 최대 12번 좁혀 실제로 설 수 있는 부분 이동 끝점을 반환한다. 끝점은 실제 OverlapCircle 검사를 통과하며 완전한 경로 성공으로 취급하지 않는다.

## 2026-11-06 문·요청 예산 변경

문 상태와 격자 계약은 [문 연결 상세](Navigation-Feedback-Implementation-2026-11-06.md)를 따른다. 계획은 CanPlanTraverse로 개방 가능한 문을 포함하고 실제 도착/이동은 CanTraverse/TryMove로 검사한다. Movement는 가까운 문에 TryOpenDoorAhead를 호출한 뒤 실제 충돌 이동을 수행한다. 임의의 목표를 닫힌 문 내부에 두는 것은 피한다.

PathRequestBudget은 World별 기본4회/프레임이며 FIFO로 대기한다. 예산이 없으면 실패로 세지 않고 다음 프레임에 신청한다. 3회 필수 경로 실패 뒤에는 제자리 경계와 허용된 방어를 하며 복구를 확인한다. 최종 실행 결과는 [11월6일 검증](../Results/2026-11-06/Verification.md)과 인수인계 문서를 기준으로 한다. 이전 절의 11월5일 검사 수치는 역사 기록이다.
