// 역할: 사각형·추적 원의 경계에서 NPC 몸 전체와 직선 이동 구간이 허용되는지 검증한다.
// 연결: MovementConstraint2D의 순수 계산만 검사하며 물리 벽은 GridWorldTraversalTests에서 검사한다.
// 읽는 순서: 사각형 경계 → 원/교집합 → 구간 → 잘못된 입력과 큰 수의 오차.
using NUnit.Framework;
using UnityEngine;

namespace NullTrace.AI.Tests
{
    public sealed class MovementConstraintTests
    {
        // 중심만 안에 있고 몸이 밖으로 튀어나온 경우를 네 방향 모두 검사한다. 정확한 접촉은 허용한다.
        [TestCase(1f, 3f, 1f, true)]
        [TestCase(0.999f, 3f, 1f, false)]
        [TestCase(9f, 3f, 1f, true)]
        [TestCase(9.001f, 3f, 1f, false)]
        [TestCase(5f, 1f, 1f, true)]
        [TestCase(5f, 0.999f, 1f, false)]
        [TestCase(5f, 5f, 1f, true)]
        [TestCase(5f, 5.001f, 1f, false)]
        [TestCase(0f, 0f, 0f, true)]
        [TestCase(10f, 6f, 0f, true)]
        [TestCase(5f, 3f, 3.01f, false)]
        public void RectangleIncludesWholeBody(float x, float y, float bodyRadius, bool expected)
        {
            var area = new MovementConstraint2D(new Rect(0, 0, 10, 6));
            Assert.That(area.Contains(new Vector2(x, y), bodyRadius), Is.EqualTo(expected));
        }

        // 반경 5에서 중심 거리가 4이고 몸 반경이 1이면 정확히 들어간다. 중심이 안에 있어도 몸이 크면 거부한다.
        [TestCase(4f, 0f, 1f, true)]
        [TestCase(4.001f, 0f, 1f, false)]
        [TestCase(3f, 4f, 0f, true)]
        [TestCase(3f, 4f, 0.01f, false)]
        [TestCase(0f, 0f, 5f, true)]
        [TestCase(0f, 0f, 5.01f, false)]
        public void CircleIncludesWholeBody(float x, float y, float bodyRadius, bool expected)
        {
            var area = new MovementConstraint2D(new Rect(-10, -10, 20, 20), Vector2.zero, 5f);
            Assert.That(area.Contains(new Vector2(x, y), bodyRadius), Is.EqualTo(expected));
        }

        // 교집합에서는 두 조건을 모두 지켜야 한다. 원 또는 사각형 하나만 통과해도 되는 합집합이 아니다.
        [Test]
        public void IntersectionRequiresRectangleAndCircle()
        {
            var area = new MovementConstraint2D(new Rect(0, 0, 8, 4), new Vector2(2, 2), 3f);
            Assert.That(area.Contains(new Vector2(2, 2), 0.5f), Is.True);
            Assert.That(area.Contains(new Vector2(-0.25f, 2), 0f), Is.False);
            Assert.That(area.Contains(new Vector2(7, 2), 0f), Is.False);
        }

        // 볼록 영역 안의 직선은 전 구간이 안쪽이다. 반대로 한 끝의 몸이 밖이면 긴 구간도 제자리 구간도 거부한다.
        [Test]
        public void SegmentIncludesWholeSweptBody()
        {
            var area = new MovementConstraint2D(new Rect(-5, -5, 10, 10), Vector2.zero, 5f);
            Assert.That(area.AllowsSegment(new Vector2(-4, 0), new Vector2(4, 0), 1f), Is.True);
            Assert.That(area.AllowsSegment(Vector2.zero, new Vector2(4.01f, 0), 1f), Is.False);
            Assert.That(area.AllowsSegment(new Vector2(4.01f, 0), Vector2.zero, 1f), Is.False);
            Assert.That(area.AllowsSegment(new Vector2(4.01f, 0), new Vector2(4.01f, 0), 1f), Is.False);
        }

        // 0 반경은 잘못된 음수와 구별한다. 몸 반경도 0이고 중심과 정확히 같은 점만 허용한다.
        [Test]
        public void ZeroRadiusCircleAllowsOnlyItsAnchorPoint()
        {
            var area = new MovementConstraint2D(new Rect(-1, -1, 2, 2), Vector2.zero, 0f);
            Assert.That(area.Contains(Vector2.zero, 0f), Is.True);
            Assert.That(area.Contains(Vector2.zero, 0.001f), Is.False);
            Assert.That(area.Contains(new Vector2(0.001f, 0), 0f), Is.False);
        }

        // 설정 실수를 절댓값·좌표 정규화로 숨기지 않는다. 생성은 가능하지만 모든 조회가 안전하게 실패한다.
        [Test]
        public void InvalidRegionsFailClosed()
        {
            var validBounds = new Rect(-2, -2, 4, 4);
            var invalid = new[]
            {
                default(MovementConstraint2D),
                new MovementConstraint2D(new Rect(0, 0, 0, 1)),
                new MovementConstraint2D(new Rect(0, 0, 1, 0)),
                new MovementConstraint2D(new Rect(0, 0, -1, 1)),
                new MovementConstraint2D(new Rect(0, 0, 1, -1)),
                new MovementConstraint2D(new Rect(float.NaN, 0, 1, 1)),
                new MovementConstraint2D(new Rect(0, 0, float.PositiveInfinity, 1)),
                new MovementConstraint2D(new Rect(float.MaxValue, 0, float.MaxValue, 1)),
                new MovementConstraint2D(validBounds, Vector2.zero, -1f),
                new MovementConstraint2D(validBounds, Vector2.zero, float.NaN),
                new MovementConstraint2D(validBounds, Vector2.zero, float.PositiveInfinity),
                new MovementConstraint2D(validBounds, new Vector2(float.NegativeInfinity, 0), 1f)
            };
            foreach (MovementConstraint2D area in invalid)
            {
                Assert.That(area.Contains(Vector2.zero, 0f), Is.False);
                Assert.That(area.AllowsSegment(Vector2.zero, Vector2.zero, 0f), Is.False);
            }
        }

        // NaN은 일반 대소 비교만으로 걸러지지 않을 수 있다. 양 끝 좌표와 몸 반경의 비정상 값을 각각 거부한다.
        [Test]
        public void InvalidQueriesFailClosed()
        {
            var area = new MovementConstraint2D(new Rect(-2, -2, 4, 4));
            foreach (float value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                var bad = new Vector2(value, 0);
                Assert.That(area.Contains(bad, 0f), Is.False);
                Assert.That(area.Contains(new Vector2(0, value), 0f), Is.False);
                Assert.That(area.Contains(Vector2.zero, value), Is.False);
                Assert.That(area.AllowsSegment(bad, Vector2.zero, 0f), Is.False);
                Assert.That(area.AllowsSegment(Vector2.zero, bad, 0f), Is.False);
            }
            Assert.That(area.Contains(Vector2.zero, -0.1f), Is.False);
        }

        // float 제곱은 무한대로 넘칠 크기다. 바깥 거리와 반경 제곱이 모두 무한대가 되어 잘못 통과하면 안 된다.
        [Test]
        public void LargeFiniteCircleDoesNotAcceptOutsidePointThroughOverflow()
        {
            var area = new MovementConstraint2D(new Rect(-2e20f, -2e20f, 4e20f, 4e20f), Vector2.zero, 1e20f);
            Assert.That(area.Contains(new Vector2(0.5e20f, 0.5e20f), 0f), Is.True);
            Assert.That(area.Contains(new Vector2(1.5e20f, 0), 0f), Is.False);
            Assert.That(area.Contains(new Vector2(1e20f, 0), 1f), Is.False);
        }

        // 큰 좌표에서 작은 몸 반경을 float로 더하면 사라질 수 있다. 왼쪽 경계 위 중심은 반경 1을 담지 못한다.
        [Test]
        public void LargeRectangleStillRejectsBodyAcrossBoundary()
        {
            var area = new MovementConstraint2D(new Rect(100000000f, 0, 16, 16));
            Assert.That(area.Contains(new Vector2(100000000f, 8), 1f), Is.False);
            Assert.That(area.Contains(new Vector2(100000008f, 8), 1f), Is.True);
            var enormous = new MovementConstraint2D(new Rect(1e20f, 0, 1e20f, 16));
            Assert.That(enormous.Contains(new Vector2(1e20f, 8), 1f), Is.False);
        }
    }
}
