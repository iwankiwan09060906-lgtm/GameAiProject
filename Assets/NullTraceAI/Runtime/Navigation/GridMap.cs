// 역할: 맵의 각 칸이 이동 가능한지만 보관하는 격자 데이터다. 적을 직접 움직이지는 않는다.
// 연결: GridWorld2D가 실제 장애물을 검사해 이 지도를 만들고, AStarPathfinder가 길을 찾을 때 읽는다.
// 좌표: (x, y)는 월드 위치나 미터가 아니라 칸 번호다. 3×2 격자는 x=0~2, y=0~1을 가진다.
// 읽는 순서: 생성자 → Contains → IsWalkable → SetWalkable을 본 뒤 AStarPathfinder.cs로 넘어간다.
// NavigationTests.cs에서는 초기 상태, 범위 검사, 장애물 변경이 이 약속대로 작동하는지 확인한다.
using System;
using UnityEngine;

namespace NullTrace.AI
{
    // 너비·높이를 받아 고정 크기 지도를 만들고, 칸별 통행 여부를 조회하거나 바꾸는 클래스다.
    // 같은 지도에서 문을 닫고 여는 것은 가능하지만, 크기를 바꾸려면 새 GridMap을 만들어야 한다.
    public sealed class GridMap
    {
        // 배열은 [x, y] 순서로 읽는다. true는 '막힘'이므로 공개 API의 walkable과 뜻이 반대다.
        // bool 배열은 처음에 모두 false다. 따라서 별도 초기화 반복문 없이 모든 칸이 열린 상태가 된다.
        private readonly bool[,] blocked;

        // 칸 수를 알려 주는 읽기 전용 값이다. 이 값을 바꿔 배열 크기와 어긋나는 상황을 막는다.
        public int Width { get; }
        public int Height { get; }

        // 입력한 양수 너비·높이만큼 지도를 만들며, 생성 직후에는 모든 칸을 지나갈 수 있다.
        // 0칸·음수 크기는 잘못된 설정이므로 예외로 알린다. 아주 큰 크기는 가용 메모리에도 제한된다.
        public GridMap(int width, int height)
        {
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "격자 너비는 양수여야 합니다.");
            // 먼저 long으로 바꿔 곱해야 50,000×50,000처럼 큰 곱이 int 범위를 넘어 잘못 계산되지 않는다.
            if (height <= 0 || (long)width * height > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(height), "격자 높이는 양수이고 전체 칸 수는 int 범위여야 합니다.");

            Width = width;
            Height = height;
            blocked = new bool[width, height];
        }

        // 입력한 칸 번호가 지도 안에 있는지 반환한다. 장애물이 있는지는 여기서 판단하지 않는다.
        // 예: 너비가 3이면 x=2는 안쪽, x=3은 바깥쪽이다. 좌표가 0부터 시작하기 때문이다.
        public bool Contains(Vector2Int cell)
        {
            return cell.x >= 0 && cell.x < Width && cell.y >= 0 && cell.y < Height;
        }

        // 입력한 칸이 지도 안이고 막히지 않았을 때만 true다. 지도 밖은 지나갈 수 없는 곳으로 취급한다.
        // &&는 앞 조건이 false면 뒤를 읽지 않으므로, (-1, 0) 같은 좌표로 배열에 접근하지 않는다.
        public bool IsWalkable(Vector2Int cell)
        {
            return Contains(cell) && !blocked[cell.x, cell.y];
        }

        // 지정한 칸의 통행 여부를 바꾼다. 문을 닫을 때 false, 다시 열 때 true를 전달하면 된다.
        // 범위 밖 쓰기는 조용히 무시하면 장애물 등록 실수를 놓치므로 예외로 알린다.
        // 이미 반환된 경로는 자동 수정되지 않는다. 변경 뒤 AStarPathfinder를 다시 호출해야 한다.
        public void SetWalkable(Vector2Int cell, bool walkable)
        {
            if (!Contains(cell))
                throw new ArgumentOutOfRangeException(nameof(cell), cell, "격자 범위를 벗어난 칸입니다.");

            blocked[cell.x, cell.y] = !walkable;
        }
    }
}
