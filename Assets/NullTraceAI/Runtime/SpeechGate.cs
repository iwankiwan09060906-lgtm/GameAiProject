// 역할: 상태 변경이나 공격 때 같은 대사를 너무 자주 출력하지 않도록 허용 여부를 결정한다.
// 연결: EnemyAgent.Speak가 이 문을 통과한 문장만 SpeechRequested 이벤트로 UI에 보낸다.
// 읽는 순서: 두 종류의 시각 기록 → TrySpeak의 공통 간격/같은 문장 간격 → Reset.
using System.Collections.Generic;

namespace NullTrace.AI
{
    /// <summary>UI나 실제 시간에 의존하지 않는 대사 간격 검사기. 적마다 별도 인스턴스를 갖는다.</summary>
    public sealed class SpeechGate
    {
        // Dictionary는 '문장 → 그 문장을 마지막으로 허용한 시각(초)' 대응표다.
        private readonly Dictionary<string, double> lastSpoken = new Dictionary<string, double>();
        // 문장이 무엇이든 마지막으로 말한 시각. -무한대는 첫 대사를 즉시 허용하기 위한 초기값이다.
        private double lastAny = double.NegativeInfinity;

        /// <summary>
        /// line을 now 시각에 말해도 되는지 검사한다. interval은 모든 대사 사이 최소 초,
        /// sameInterval은 같은 문장을 다시 쓰기까지의 최소 초다. now는 계속 증가하는 초 단위 시각을 넣는다.
        /// true일 때만 기록을 갱신한다. false인 요청은 대기열에 넣지 않으며 다음 기회에 다시 검사한다.
        /// </summary>
        public bool TrySpeak(string line, double now, float interval, float sameInterval)
        {
            // 1. 빈 문장/잘못된 시각을 제외하고, 직전에 다른 말을 했더라도 최소 간격을 지킨다.
            if (string.IsNullOrEmpty(line) || double.IsNaN(now) || double.IsInfinity(now)
                || !AiMath.IsFinite(interval) || !AiMath.IsFinite(sameInterval)
                || interval < 0f || sameInterval < 0f || now - lastAny < interval) return false;
            // 2. 공통 간격을 넘겼어도 같은 문장에는 더 긴 제한을 둘 수 있다.
            // 예: 전체 간격 2초, 반복 간격 8초이면 2초 뒤 다른 말은 가능하지만 같은 말은 아직 불가능하다.
            if (lastSpoken.TryGetValue(line, out double last) && now - last < sameInterval) return false;
            // 3. 두 검사를 통과한 요청만 두 기록을 함께 갱신한다. 거절한 요청이 대기 시간을 늘리지는 않는다.
            lastAny = now;
            lastSpoken[line] = now;
            return true;
        }

        // 재시작 때 대사 이력을 비운다. EnemyAgent의 자체 시계도 0으로 돌아가므로 오래된 시각을 남기면 안 된다.
        public void Reset()
        {
            lastAny = double.NegativeInfinity;
            lastSpoken.Clear();
        }
    }
}
