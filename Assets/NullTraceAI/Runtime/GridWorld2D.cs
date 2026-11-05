// 역할: 실제 Unity 2D 벽을 A*용 격자로 바꾸고, 감각과 이동에서 실제 벽도 검사한다.
// 연결: Rebuild → Navigation/GridMap → AStarPathfinder → EnemyAgent.Move의 순서로 경로를 만든다.
//       EnemyAgent.Sense/Hear는 HasLineOfSight/CountWalls를, 이동은 TryMove를 호출한다.
// 읽는 순서: 좌표 필드/Bounds → Rebuild → TryGetCell/CellCenter → CanOccupy/CountWalls → CanTraverse/TryMove.
// 격자는 재구축 순간의 지도다. 이동 직전의 실제 충돌 검사를 따로 하는 이유는 문이 나중에 닫힐 수 있기 때문이다.
using System.Collections.Generic;
using UnityEngine;

namespace NullTrace.AI
{
    /// <summary>
    /// XY 평면에서만 동작하는 격자/물리 연결 컴포넌트. 자체 Transform의 회전·스케일로 격자를 변형하지 않는다.
    /// 벽은 ObstacleMask 레이어의 비 Trigger Collider2D로 구성한다. 경로 계산 자체는 Navigation 코드가 담당한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GridWorld2D : MonoBehaviour
    {
        // 격자 전체의 왼쪽 아래 모서리(월드 좌표). (0,0) 칸의 중심 좌표가 아니라는 점에 주의한다.
        public Vector2 Origin = new Vector2(-10f, -10f);
        // 가로/세로 '칸 수'. 실제 맵 너비/높이는 각각 이 값에 CellSize를 곱한 월드 거리다.
        public int Width = 40;
        public int Height = 40;
        // 정사각형 한 칸의 변 길이(월드 단위). 작게 하면 지도를 촘촘히 표현하지만 같은 면적의 칸 수가 늘어난다.
        public float CellSize = 0.5f;
        // 적을 원으로 보았을 때 벽과 떨어져야 하는 반지름(월드 단위). 그림 크기와 자동 연동되지 않는다.
        public float AgentRadius = 0.15f;
        [Tooltip("벽 전용 레이어. 0은 오류이며 적/플레이어 레이어를 포함하지 마세요.")]
        public LayerMask ObstacleMask;

        // 전체 격자를 월드 좌표의 사각형으로 표현한다. NPC별 순찰/추적 구역과는 다른 '맵 전체'의 경계다.
        // 설정 변경 직후에는 이 값만 보고 이동하지 말고 IsReady와 구역 제약을 함께 확인한다.
        public Rect Bounds => new Rect(Origin, new Vector2(Width * CellSize, Height * CellSize));

        // A*의 계획 지도다. NPC가 열 수 있는 문은 지나갈 후보로 넣되 실제 충돌체를 끄지는 않는다.
        // 재구축이 실패하면 null로 남겨 이전 지도를 잘못 사용하지 않게 한다.
        public GridMap Map { get; private set; }
        // Rebuild 호출마다 증가하는 지도 버전. 적은 이 값이 달라지면 이전 경로를 버린다.
        public int Revision { get; private set; }
        public string LastError { get; private set; } = "Rebuild()가 필요합니다.";
        // 지도 생성 때 저장한 값과 현재 Inspector 값이 같아야 사용 가능하다.
        // 예: CellSize만 고치고 재구축하지 않으면 격자와 좌표 변환이 어긋나므로 일단 정지시킨다.
        public bool IsReady => isActiveAndEnabled && Map != null && bakedOrigin == Origin
            && bakedWidth == Width && bakedHeight == Height && bakedSize == CellSize
            && bakedRadius == AgentRadius && bakedMask == ObstacleMask.value;

        // baked*는 마지막으로 성공한 재구축의 설정 사본이다. 살아 있는 Inspector 값과 구별해 둔다.
        private Vector2 bakedOrigin;
        private int bakedWidth, bakedHeight, bakedMask;
        private float bakedSize, bakedRadius;
        // 물리 조회에 벽 레이어만 포함하고 Trigger는 제외하는 공통 필터.
        private ContactFilter2D filter;
        // 조회 결과 목록을 매번 새로 만들지 않고 비워서 재사용한다. HashSet은 같은 벽의 중복 집계를 막는다.
        private readonly List<Collider2D> overlaps = new List<Collider2D>();
        private readonly List<RaycastHit2D> hits = new List<RaycastHit2D>();
        private readonly HashSet<Collider2D> walls = new HashSet<Collider2D>();
        // 문 검색은 Rebuild 때 한 번만 한다. A*가 각 간선을 검사할 때 씬 전체를 다시 검색하지 않는다.
        private readonly Dictionary<Collider2D, AiDoor2D> doors = new Dictionary<Collider2D, AiDoor2D>();
        // 부동소수점 오차로 벽에 아주 조금 끼지 않도록 충돌 지점 앞에 남기는 여유 거리(월드 단위).
        private const float Skin = 0.002f;

        // 씬 시작 시 아직 준비되지 않았다면 지도를 한 번 만든다. 문 변경 뒤 재구축은 호출자가 요청해야 한다.
        private void Start()
        {
            if (!IsReady) Rebuild();
        }

        /// <summary>
        /// 격자를 만들 수 있는 설정인지 검사한다. true면 error=null, false면 한국어 원인을 돌려준다.
        /// 설정을 임의로 고치지 않는다. 벽 마스크 0을 허용하면 모든 벽이 사라진 것처럼 보여 오류로 취급한다.
        /// </summary>
        public bool ValidateConfiguration(out string error)
        {
            error = null;
            if (ObstacleMask.value == 0)
                error = "ObstacleMask가 0입니다. 벽 Collider2D의 레이어를 지정하세요.";
            // 곱하기 전에 long으로 넓혀 큰 정수 곱의 넘침을 피하고, 지나친 지도 크기로 인한 작업량을 제한한다.
            else if (Width <= 0 || Height <= 0 || (long)Width * Height > 262144)
                error = "Width/Height는 양수이고 전체 격자는 262144칸 이하여야 합니다.";
            // 반지름은 양수여야 하고 한 칸의 절반보다 작아야 이 격자 이동 모델에서 적 한 몸이 칸에 들어간다.
            else if (!AiMath.IsFinite(Origin) || !AiMath.IsFinite(CellSize)
                || CellSize < 0.01f || CellSize > 100f || !AiMath.IsFinite(AgentRadius)
                || AgentRadius <= 0f || AgentRadius >= CellSize * 0.5f)
                error = "Origin/CellSize/AgentRadius가 잘못되었습니다. 0 < AgentRadius < CellSize/2가 필요합니다.";
            return error == null;
        }

        /// <summary>
        /// 현재 필드와 씬의 벽 Collider를 읽어 Map을 다시 만든다. 반환값 대신 IsReady/LastError로 성공을 확인한다.
        /// 문을 열거나 닫은 코드에서 호출한다. 매 프레임 전체 격자를 재구축하는 용도로 사용하지 않는다.
        /// </summary>
        public void Rebuild()
        {
            // 1. 이전 지도부터 무효화한다. 새 설정이 잘못되어도 이전 경로가 계속 유효한 것처럼 보이면 안 된다.
            Map = null;
            Revision++;
            doors.Clear();
            if (!ValidateConfiguration(out string error))
            {
                LastError = error;
                return;
            }
            // 2. 물리 검사 조건을 준비하고 Transform 변경을 물리 엔진에 반영한다.
            // 화면에서 문을 옮겼더라도 물리 정보 갱신 전이라면 옛 위치가 검사될 수 있어 동기화한다.
            filter = new ContactFilter2D();
            filter.SetLayerMask(ObstacleMask);
            filter.useTriggers = false;
            Physics2D.SyncTransforms();
            foreach (var door in FindObjectsByType<AiDoor2D>(FindObjectsSortMode.None))
            {
                if (door.World != this || !door.ValidateConfiguration(out _)) continue;
                // 한 Collider에 문 소유자가 둘이면 권한이 모호하다. 그 Collider는 일반 벽으로 남긴다.
                if (doors.ContainsKey(door.DoorCollider)) doors[door.DoorCollider] = null;
                else doors.Add(door.DoorCollider, door);
            }
            var map = new GridMap(Width, Height);
            // 3. 칸마다 '적이 통과해도 되는가'를 기록한다. 중심점만 검사하면 칸 안의 얇은 벽을 놓칠 수 있다.
            // 칸 전체를 반지름만큼 사방으로 확장하므로 변 길이는 CellSize + 2×AgentRadius가 된다.
            // 안전 쪽으로 넓게 막는 방식이라 좁은 통로가 막힌 칸으로 나올 수 있다. 반지름/칸 크기를 맵에 맞춰야 한다.
            Vector2 size = Vector2.one * (CellSize + 2f * AgentRadius);
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                var cell = new Vector2Int(x, y);
                // 일반 벽·잠긴 문은 막는다. 열 수 있는 문만 계획에서 제외하며, 물리 상태는 바꾸지 않는다.
                Physics2D.OverlapBox(CellCenter(cell), size, 0f, filter, overlaps);
                map.SetWalkable(cell, PlanningOverlapsAreClear());
            }
            // 4. 모든 칸을 만든 뒤 설정 사본과 지도를 함께 공개한다. 중간 상태의 지도를 적이 쓰지 않게 한다.
            bakedOrigin = Origin;
            bakedWidth = Width;
            bakedHeight = Height;
            bakedSize = CellSize;
            bakedRadius = AgentRadius;
            bakedMask = ObstacleMask.value;
            Map = map;
            LastError = null;
        }

        /// <summary>
        /// XY 월드 좌표 position이 들어 있는 격자 칸 번호 cell을 구한다. 지도 밖/미준비 상태면 false다.
        /// '어느 칸인가'만 답하며, 그 칸이 벽인지 통행 가능한지는 Map.IsWalkable로 따로 확인해야 한다.
        /// </summary>
        public bool TryGetCell(Vector2 position, out Vector2Int cell)
        {
            cell = default;
            if (!IsReady || !AiMath.IsFinite(position)) return false;
            // 원점을 빼고 칸 크기로 나누면 칸 단위의 위치가 된다. 예: Origin.x=0, CellSize=0.5,
            // position.x=0.75라면 local.x=1.5이고 아래 Floor(내림) 결과는 1번 칸이다.
            Vector2 local = (position - Origin) / CellSize;
            if (local.x < 0f || local.y < 0f || local.x >= Width || local.y >= Height) return false;
            cell = new Vector2Int(Mathf.FloorToInt(local.x), Mathf.FloorToInt(local.y));
            return true;
        }

        /// <summary>
        /// 격자 칸 번호를 그 칸 중심의 XY 월드 좌표로 바꾼다. +0.5는 모서리에서 반 칸 안쪽으로 이동하는 뜻이다.
        /// 입력 칸의 범위는 검사하지 않으므로 이미 유효한 칸임을 알고 호출한다.
        /// </summary>
        public Vector2 CellCenter(Vector2Int cell)
            => Origin + new Vector2(cell.x + 0.5f, cell.y + 0.5f) * CellSize;

        /// <summary>
        /// position에 반지름 AgentRadius인 적 원이 겹침 없이 설 수 있는지 검사한다.
        /// 지도 바깥으로 몸 일부가 튀어나가거나 현재 물리 벽과 겹치면 false다. 과거 Map의 칸 정보 대신 실제 벽을 본다.
        /// </summary>
        public bool CanOccupy(Vector2 position)
        {
            // 맵 경계와 NPC별 영역이 같은 몸체 포함 규칙을 사용하도록 공통 기하 판정을 재사용한다.
            if (!IsReady || !new MovementConstraint2D(Bounds).Contains(position, AgentRadius)) return false;
            return Physics2D.OverlapCircle(position, AgentRadius, filter, overlaps) == 0;
        }

        // Collider 하나에 대한 문 예외다. 권한·활성 상태를 다시 확인해 오래된 캐시로 잠긴 문을 무시하지 않는다.
        private AiDoor2D OpenableDoor(Collider2D obstacle)
            => obstacle && doors.TryGetValue(obstacle, out var door) && door && door.World == this
                && door.DoorCollider == obstacle && door.CanNpcOpen ? door : null;

        private bool PlanningOverlapsAreClear()
        {
            foreach (var obstacle in overlaps) if (!OpenableDoor(obstacle)) return false;
            return true;
        }

        /// <summary>
        /// 계획 전용 끝점 검사. 열 수 있는 닫힌 문의 자리도 후보가 될 수 있다. 실제 도착 판정에는 쓰지 않는다.
        /// 실제 출발점은 CanOccupy로, A*의 셀 중심·목적지 후보는 이 함수로 검사한다.
        /// </summary>
        public bool CanPlanOccupy(Vector2 position)
        {
            if (!IsReady || !new MovementConstraint2D(Bounds).Contains(position, AgentRadius)) return false;
            Physics2D.OverlapCircle(position, AgentRadius, filter, overlaps);
            return PlanningOverlapsAreClear();
        }

        /// <summary>
        /// 문을 열 수 있다고 가정한 전 구간 검사. 문을 실제로 열거나 NPC를 옮기지 않는다.
        /// A*와 실제 위치↔셀 중심 연결에 사용하며, NPC별 허용 영역 검사는 호출자가 함께 적용한다.
        /// </summary>
        public bool CanPlanTraverse(Vector2 start, Vector2 end)
        {
            if (!CanPlanOccupy(start) || !CanPlanOccupy(end)) return false;
            Vector2 delta = end - start;
            float distance = delta.magnitude;
            if (!AiMath.IsFinite(distance)) return false;
            if (distance == 0f) return start.Equals(end);
            Physics2D.CircleCast(start, AgentRadius, delta / distance, filter, hits, distance + Skin);
            foreach (var hit in hits)
                if (!OpenableDoor(hit.collider) && hit.distance - Skin < distance) return false;
            return true;
        }

        /// <summary>
        /// 실제 한 걸음을 옮기기 직전에 호출한다. 최대 0.5 월드 단위 앞의 첫 장애물이 열 수 있는 문일 때만 연다.
        /// true면 문이 열려 Revision도 바뀌었다. false는 문이 없거나 열 수 없다는 뜻이며, 이동 허가가 아니다.
        /// 호출자는 이후에도 TryMove를 실행해야 한다. 먼 목적지를 넣어도 원격 문이나 벽 뒤의 문은 열지 않는다.
        /// </summary>
        public bool TryOpenDoorAhead(Vector2 start, Vector2 desired)
        {
            if (!CanOccupy(start) || !new MovementConstraint2D(Bounds).Contains(desired, AgentRadius)) return false;
            Vector2 delta = desired - start;
            float distance = delta.magnitude;
            if (!AiMath.IsFinite(distance) || distance == 0f) return false;
            // 빠른 프레임에서는 한 걸음이 물리 접촉 여유보다 짧아 문을 찾기 전에 몸이 멈출 수 있다.
            // 걸음 크기와 별개로 이동 방향의 가까운 0.5 단위만 확인한다. 실제 이동은 여전히 TryMove가 검사한다.
            Physics2D.CircleCast(start, AgentRadius, delta / distance, filter, hits, 0.5f + Skin);
            if (hits.Count == 0) return false;
            // CircleCast 결과는 가까운 순서다. 같은 거리에 일반 벽도 있으면 그 뒤 문을 열지 않는다.
            var nearest = hits[0];
            var door = OpenableDoor(nearest.collider);
            if (!door) return false;
            foreach (var hit in hits)
                if (hit.distance <= nearest.distance && hit.collider != nearest.collider) return false;
            // 열기는 Rebuild를 호출하므로 위 hits 목록 사용을 끝낸 뒤 실행한다.
            return door.TrySetOpen(true);
        }

        /// <summary>
        /// 두 월드 좌표를 잇는 선분과 양 끝점에 겹친 고유 벽 Collider 개수를 반환한다.
        /// 한 CompositeCollider 안의 여러 벽 구간도 한 개로 센다. 재질/굴절까지 계산하는 소리 전파 모델은 아니다.
        /// 잘못된 입력은 매우 큰 값을 돌려 시야가 열렸다고 판단하지 않게 한다.
        /// </summary>
        public int CountWalls(Vector2 start, Vector2 end)
        {
            if (!IsReady || !AiMath.IsFinite(start) || !AiMath.IsFinite(end)) return int.MaxValue;
            // 선분 검사와 끝점 검사가 같은 벽을 찾아도 HashSet에 넣으면 한 번만 센다.
            walls.Clear();
            Physics2D.Linecast(start, end, filter, hits);
            foreach (RaycastHit2D hit in hits) if (hit.collider) walls.Add(hit.collider);
            // 전역 queriesStartInColliders 설정과 무관하게 벽 속의 양 끝점도 차폐한다.
            Physics2D.OverlapPoint(start, filter, overlaps);
            foreach (Collider2D item in overlaps) walls.Add(item);
            Physics2D.OverlapPoint(end, filter, overlaps);
            foreach (Collider2D item in overlaps) walls.Add(item);
            return walls.Count;
        }

        // 두 점 사이에 물리 벽이 0개인지 반환한다. 시야 거리/각도는 AiMath.InView가 맡는다.
        public bool HasLineOfSight(Vector2 start, Vector2 end) => CountWalls(start, end) == 0;

        /// <summary>
        /// XY의 start→end 전 구간을 원형 몸체가 실제 벽에 닿지 않고 이동할 수 있는지 검사한다.
        /// 양 끝이 비어 있어도 중간에 벽이 있을 수 있어 TryMove의 원형 쓸기 검사를 재사용한다.
        /// Transform은 변경하지 않는다. 추적 반경 등 NPC별 정책은 MovementConstraint2D와 함께 검사한다.
        /// </summary>
        public bool CanTraverse(Vector2 start, Vector2 end)
        {
            return CanOccupy(end) && TryMove(start, end, out _);
        }

        /// <summary>
        /// start에서 desired까지 원형 몸체를 이동시켜도 안전한지 검사하고 가능한 끝점 reached를 돌려준다.
        /// true는 요청 거리 전부 이동 가능, false는 벽 등으로 일부만 이동하거나 전혀 이동하지 못했다는 뜻이다.
        /// 실제 Transform은 바꾸지 않는다. EnemyAgent가 reached만 적용하므로 false여도 반환 좌표를 읽어야 한다.
        /// </summary>
        public bool TryMove(Vector2 start, Vector2 desired, out Vector2 reached)
        {
            // 1. 벽 안에서 시작했다면 '탈출할 곳'을 임의로 정해 순간이동하지 않고 제자리를 반환한다.
            reached = start;
            if (!CanOccupy(start) || !AiMath.IsFinite(desired)) return false;
            // 2. 이번 요청의 방향과 길이를 구한다. 같은 점만 즉시 성공한다. 짧은 이동도 벽을 건너뛸 수 없다.
            Vector2 delta = desired - start;
            float distance = delta.magnitude;
            if (!AiMath.IsFinite(distance)) return false;
            if (distance == 0f) return start.Equals(desired);
            float travel = distance;
            // 3. 원을 출발점에서 목적지 쪽으로 쓸어 가며 중간 벽을 찾는다. 도착점만 검사하는 것과 달리
            // 빠르게 이동해 얇은 벽의 반대편으로 넘어가는 상황도 잡을 수 있다.
            Physics2D.CircleCast(start, AgentRadius, delta / distance, filter, hits, distance + Skin);
            // 가장 가까운 충돌보다 Skin만큼 앞에서 멈춘다. 검사한 모든 벽 중 최소 이동 거리만 허용한다.
            foreach (RaycastHit2D hit in hits) travel = Mathf.Min(travel, Mathf.Max(0f, hit.distance - Skin));
            // 4. 계산한 끝점도 지도 범위와 겹침 검사를 통과해야 반환한다. 반올림 오차 등 마지막 안전 확인이다.
            // travel은 충돌이 없을 때 처음의 distance 그대로다. 조금 덜 갔더라도 완전 통과로 보고하지 않는다.
            // 완전히 갈 수 있으면 정확한 요청 끝점을 반환해 정규화 계산의 오차가 도착 좌표에 남지 않게 한다.
            bool complete = travel >= distance;
            Vector2 candidate = complete ? desired : start + delta / distance * travel;
            if (!CanOccupy(candidate))
            {
                // 쓸기 검사의 접촉 거리에는 작은 오차가 있어, 그 직전 점도 겹침 검사에서는 벽 안일 수 있다.
                // 실제 Unity 검증에서 1.700 거리의 벽이 1.705로 보고되어 기존 0.002 여백만으로 부족했다.
                // 이미 안전한 출발점과 첫 접촉 이하 거리 사이를 절반씩 좁혀, 실제로 설 수 있는 끝점을 찾는다.
                // 충돌 뒤쪽을 찾거나 벽을 넘지는 않는다. 예외 경계에서만 최대 12번 조회하므로 무한 반복도 없다.
                float safe = 0f, blocked = travel;
                for (int i = 0; i < 12; i++)
                {
                    float middle = (safe + blocked) * 0.5f;
                    if (CanOccupy(start + delta / distance * middle)) safe = middle;
                    else blocked = middle;
                }
                reached = start + delta / distance * safe;
                return false; // 일부 전진했어도 원래 목적지까지 완전히 통과한 것은 아니다.
            }
            reached = candidate;
            return complete;
        }
    }
}
