// 역할: 이번 이동에서 허용된 구역 안에 NPC의 몸 전체가 들어가는지 계산한다. 물리 벽은 별도로 검사한다.
// 연결: EnemyAgent가 순찰 구역 또는 추적의 고정 사각형·원을 만들고, A*와 실제 이동 양쪽에 적용한다.
// 읽는 순서: 생성자 → Contains의 몸체 포함 판정 → AllowsSegment의 전 구간 판정.
// 좌표·반경은 모두 XY 월드 단위다. 칸 번호를 직접 넣지 말고 GridWorld2D.CellCenter로 변환한다.
using UnityEngine;

namespace NullTrace.AI
{
    /// <summary>
    /// 축 정렬 사각형과 선택적인 원의 교집합을 표현한다. 생성 뒤 영역이 변하지 않는 값 형식이다.
    /// 잘못된 설정을 자동 보정하거나 예외로 중단하지 않고, 조회 시 false로 안전하게 거부한다.
    /// </summary>
    public readonly struct MovementConstraint2D
    {
        private readonly Rect bounds;
        private readonly Vector2 anchor;
        private readonly float radius;
        private readonly bool hasCircle;

        // 순찰·초소 복귀처럼 사각형 구역만 제한할 때 사용한다. 너비와 높이는 양수여야 한다.
        public MovementConstraint2D(Rect bounds)
        {
            this.bounds = bounds;
            anchor = Vector2.zero;
            radius = 0f;
            hasCircle = false;
        }

        // 추적·이탈점 복귀처럼 사각형과 고정 반경을 함께 제한할 때 사용한다.
        // radius=0은 중심 한 점만 허용하며, 음수·NaN·무한대는 허용하지 않는다.
        public MovementConstraint2D(Rect bounds, Vector2 anchor, float radius)
        {
            this.bounds = bounds;
            this.anchor = anchor;
            this.radius = radius;
            hasCircle = true;
        }

        /// <summary>
        /// center에 bodyRadius 크기의 원형 NPC가 온전히 설 수 있으면 true다. 경계에 정확히 닿는 것은 허용한다.
        /// 몸체 반경 0은 점 검사다. 음수·비유한 값이나 비어 있는 사각형은 false이며 목적지를 옮기지 않는다.
        /// </summary>
        public bool Contains(Vector2 center, float bodyRadius)
        {
            // Rect는 음수 크기도 저장할 수 있으므로 사용 전에 직접 검증한다. default 구조체도 여기서 거부된다.
            // 각 필드가 유한해도 x+width가 float 범위를 넘을 수 있어 계산된 끝 경계도 검사한다.
            if (!AiMath.IsFinite(center) || !AiMath.IsFinite(bodyRadius) || bodyRadius < 0f
                || !AiMath.IsFinite(bounds.position) || !AiMath.IsFinite(bounds.size)
                || bounds.width <= 0f || bounds.height <= 0f
                || !AiMath.IsFinite(bounds.xMax) || !AiMath.IsFinite(bounds.yMax))
                return false;

            // 중심만 구역 안이면 몸의 옆부분이 튀어나갈 수 있다. 네 방향으로 몸체 반경만큼 여유가 필요하다.
            // 예: x=0~10, 몸체 반경 1이면 중심은 x=1~9에 있어야 한다. 먼저 경계까지의 여유 거리를 구한다.
            // 큰 월드 좌표에 작은 반경을 직접 더하면 반경이 반올림으로 사라질 수 있어 상대 거리로 비교한다.
            double localX = (double)center.x - bounds.x;
            double localY = (double)center.y - bounds.y;
            if (localX < bodyRadius || localY < bodyRadius
                || (double)bounds.width - localX < bodyRadius || (double)bounds.height - localY < bodyRadius)
                return false;
            if (!hasCircle)
                return true;
            if (!AiMath.IsFinite(anchor) || !AiMath.IsFinite(radius) || radius < 0f || bodyRadius > radius)
                return false;

            // 원 안에 몸 전체를 넣으려면 중심 간 거리 + 몸체 반경 <= 추적 반경이어야 한다.
            // double은 큰 유한 좌표를 제곱해도 float처럼 넘치지 않는다. 먼저 원의 남은 여유를 구해 몸 크기와 비교한다.
            // 반경이 아주 클 때 radius-bodyRadius에서 작은 몸 크기가 사라져, 경계 위 몸을 통과시키는 오류도 피한다.
            double dx = (double)center.x - anchor.x;
            double dy = (double)center.y - anchor.y;
            double distance = System.Math.Sqrt(dx * dx + dy * dy);
            return bodyRadius <= (double)radius - distance;
        }

        /// <summary>
        /// start에서 end로 직선 이동하는 동안 몸 전체가 구역 안에 있으면 true다. 제자리 이동도 같은 기준이다.
        /// 사각형과 원, 그 교집합은 모두 볼록하다. 몸체 반경만큼 줄인 영역도 볼록하므로
        /// 양 끝의 몸체가 들어가면 그 사이 모든 위치도 들어간다. 중간 점을 듬성듬성 검사하는 근사가 아니다.
        /// 장애물이 없는지는 GridWorld2D.CanTraverse로 따로 확인한다. 오목한 구역·여러 구역 합집합에는 이 공식을 쓰지 않는다.
        /// </summary>
        public bool AllowsSegment(Vector2 start, Vector2 end, float bodyRadius)
        {
            return Contains(start, bodyRadius) && Contains(end, bodyRadius);
        }
    }
}
