// 역할: 감각 계산·설정 보정·대사 제한·소음 전달의 작은 규칙을 각각 검증하는 기존 EditMode 테스트다.
// 연결: AiMath, EnemyProfile, SpeechGate, NoiseBus를 직접 호출해 EnemyAgent 전체 흐름과 분리해서 확인한다.
// 읽는 순서: 시야 → 소리 수식 → 설정 보정 → 대사 간격 → 소음 사건. 행동 흐름은 AiAgentTests를 이어서 읽는다.
// [Test]는 NUnit이 실행할 검사라는 표시다. Assert.That(실제값, 기대조건)이 맞지 않으면 그 검사가 실패한다.
using NUnit.Framework;
using UnityEngine;

namespace NullTrace.AI.Tests
{
    /// <summary>
    /// 특정 입력에서 계산 결과/사건 횟수가 맞는지 확인한다. 사람이 눈으로 데모를 보는 검증을 보완한다.
    /// 부동소수점 수는 계산 오차가 있을 수 있어 일부 값은 Within(허용 오차)로 비교한다.
    /// </summary>
    public sealed class AiLogicTests
    {
        // 전체 시야각 90도의 경계인 (2,2)는 정면에서 45도이므로 통과해야 한다.
        // 조금 더 바깥 각도/거리 초과/방향 없음/NaN은 거절하고, 방향 벡터 길이가 7이어도 방향만 사용해야 한다.
        [Test]
        public void ViewUsesRangeAndFullAngleWithoutRequiringNormalizedFacing()
        {
            Assert.That(AiMath.InView(Vector2.zero, Vector2.right * 7f, new Vector2(2, 2), 3, 90), Is.True);
            Assert.That(AiMath.InView(Vector2.zero, Vector2.right, new Vector2(2, 2.1f), 3, 90), Is.False);
            Assert.That(AiMath.InView(Vector2.zero, Vector2.right, new Vector2(4, 0), 3, 90), Is.False);
            Assert.That(AiMath.InView(Vector2.zero, Vector2.right, Vector2.left, 3, 360), Is.True);
            Assert.That(AiMath.InView(Vector2.zero, Vector2.zero, Vector2.right, 3, 90), Is.False);
            Assert.That(AiMath.InView(Vector2.zero, Vector2.right, new Vector2(float.NaN, 0), 3, 90), Is.False);
        }

        // 거리 2/반경 10이면 0.8, 벽 두 개를 각각 0.5로 감쇠하면 0.2가 되는지 확인한다.
        // 경계 밖/0 반경/NaN/완전 차단 벽도 함께 넣어 계산 불가능한 소리가 유효한 단서가 되지 않게 한다.
        [Test]
        public void SoundAttenuatesWithDistanceAndEveryCollider()
        {
            Assert.That(AiMath.HeardStrength(2, 10, 1, 0, 0.5f), Is.EqualTo(0.8f).Within(0.0001));
            Assert.That(AiMath.HeardStrength(2, 10, 1, 2, 0.5f), Is.EqualTo(0.2f).Within(0.0001));
            Assert.That(AiMath.HeardStrength(10, 10, 1, 0, 0.5f), Is.Zero);
            Assert.That(AiMath.HeardStrength(2, 0, 1, 0, 0.5f), Is.Zero);
            Assert.That(AiMath.HeardStrength(float.NaN, 10, 1, 0, 0.5f), Is.Zero);
            Assert.That(AiMath.HeardStrength(0, 10, 1, 1, 0), Is.Zero);
        }

        // 잘못된 튜닝을 일부러 주입한 뒤 보정 결과를 확인한다. 입력값 복사가 아니라 안전 범위 유지가 검사 목적이다.
        // 특히 어둠 거리 7보다 희미함/밝음 거리가 짧지 않고, 같은 문장 제한이 전체 대사 제한보다 짧지 않아야 한다.
        [Test]
        public void ProfileRepairsNonFiniteValuesAndPreservesLightOrdering()
        {
            var profile = ScriptableObject.CreateInstance<EnemyProfile>();
            try
            {
                // 준비: 숫자가 아닌 값/음수/뒤집힌 거리 순서 등 Inspector나 외부 코드에서 생길 실수를 재현한다.
                profile.ViewAngle = float.NaN;
                profile.DetectionGain = -1;
                profile.RepathInterval = 0;
                profile.AttackInterval = float.PositiveInfinity;
                profile.DarkViewDistance = 7;
                profile.DimViewDistance = 2;
                profile.BrightViewDistance = 3;
                profile.SpeechInterval = 5;
                profile.SameSpeechInterval = 1;
                // 실행은 보정 한 번이다. 그 다음 기대값을 검사해 어떤 안전 규칙이 깨졌는지 드러낸다.
                profile.ValidateValues();
                Assert.That(profile.ViewAngle, Is.EqualTo(100));
                Assert.That(profile.DetectionGain, Is.GreaterThan(0));
                Assert.That(profile.RepathInterval, Is.GreaterThan(0));
                Assert.That(profile.AttackInterval, Is.EqualTo(0.8f));
                Assert.That(profile.ViewDistance(LightLevel.Dark), Is.EqualTo(7));
                Assert.That(profile.ViewDistance(LightLevel.Dim), Is.GreaterThanOrEqualTo(7));
                Assert.That(profile.ViewDistance(LightLevel.Bright), Is.GreaterThanOrEqualTo(profile.DimViewDistance));
                Assert.That(profile.SameSpeechInterval, Is.GreaterThanOrEqualTo(profile.SpeechInterval));
            }
            // 검사 도중 실패해도 임시 에셋을 지워 다음 테스트에 영향을 남기지 않는다.
            finally { Object.DestroyImmediate(profile); }
        }

        // 외부 시각을 직접 넘기므로 실제로 8초를 기다릴 필요 없이 간격 정책을 검증할 수 있다.
        // 다른 문장은 2초 뒤 허용되지만 같은 문장은 8초까지 금지, Reset 뒤에는 첫 문장을 즉시 허용해야 한다.
        [Test]
        public void SpeechLimitsBothAllLinesAndRepeatedLinesAndCanReset()
        {
            var gate = new SpeechGate();
            Assert.That(gate.TrySpeak("발견", 0, 2, 8), Is.True);
            Assert.That(gate.TrySpeak("수색", 1, 2, 8), Is.False);
            Assert.That(gate.TrySpeak("수색", 2, 2, 8), Is.True);
            Assert.That(gate.TrySpeak("발견", 4, 2, 8), Is.False);
            Assert.That(gate.TrySpeak("발견", 8, 2, 8), Is.True);
            Assert.That(gate.TrySpeak("기타", double.NaN, 2, 8), Is.False);
            gate.Reset();
            Assert.That(gate.TrySpeak("발견", 0, 2, 8), Is.True);
        }

        // 사건을 받은 뒤 원래 position 변수를 바꿔도 사건 좌표는 바뀌지 않아야 한다.
        // 비정상/비활성 발행은 수신 횟수를 늘리지 않고 재활성화 후 정상 발행은 다시 받아야 한다.
        [Test]
        public void NoiseBusCopiesPositionAndRejectsInvalidOrInactiveInput()
        {
            var go = new GameObject("Noise test");
            try
            {
                var bus = go.AddComponent<NoiseBus>();
                int count = 0;
                NoiseEvent snapshot = default;
                // 간단한 구독자로 사건 개수와 마지막 내용을 기록한다. 실제 EnemyAgent.Hear 대신 관찰만 하는 함수다.
                bus.Emitted += value => { count++; snapshot = value; };
                var position = new Vector2(3, 4);
                bus.Publish(position, 10, 4, NoiseKind.Door);
                position = Vector2.zero;
                Assert.That(snapshot.Position, Is.EqualTo(new Vector2(3, 4)));
                Assert.That(snapshot.Loudness, Is.EqualTo(1));
                Assert.That(snapshot.Kind, Is.EqualTo(NoiseKind.Door));
                // 각각 독립적인 잘못된 입력이다. 위의 정상 사건 한 번 이후에는 아무 통지도 오면 안 된다.
                bus.Publish(position, -1);
                bus.Publish(position, float.NaN);
                bus.Publish(position, 10, float.PositiveInfinity);
                bus.Publish(new Vector2(float.NaN, 0), 10);
                bus.enabled = false;
                bus.Publish(position, 10);
                Assert.That(count, Is.EqualTo(1));
                bus.enabled = true;
                bus.Publish(position, 10);
                Assert.That(count, Is.EqualTo(2));
            }
            // 테스트용 버스 오브젝트만 제거한다. 씬의 실제 버스나 설정 에셋을 수정하는 검사가 아니다.
            finally { Object.DestroyImmediate(go); }
        }
    }
}
