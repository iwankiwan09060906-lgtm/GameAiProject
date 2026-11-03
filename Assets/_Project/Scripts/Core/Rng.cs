using System;

namespace GameAi
{
    // 시드를 정하면 항상 같은 수열을 내는 난수원(xorshift32).
    // UnityEngine.Random은 전역 상태 하나뿐이라, 재현이 필요한 AI용과 연출용을 나눠 쓰려고 직접 만들었다.
    public sealed class RandomSource
    {
        uint state;

        public RandomSource(int seed)
        {
            state = (uint)seed;

            // xorshift는 상태가 0이면 영원히 0만 내므로 피한다
            if (state == 0) state = 0x9E3779B9u;
        }

        public uint NextUInt()
        {
            uint x = state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            state = x;
            return x;
        }

        // 0 이상 1 미만. 상위 24비트만 써서 float 정밀도 안에서 고르게 나온다
        public float Value()
        {
            return (NextUInt() >> 8) * (1f / (1 << 24));
        }

        public float Range(float min, float max)
        {
            return min + (max - min) * Value();
        }

        // min 이상 maxExclusive 미만의 정수
        public int Int(int min, int maxExclusive)
        {
            if (maxExclusive <= min) return min;
            return min + (int)(NextUInt() % (uint)(maxExclusive - min));
        }
    }

    // 난수는 반드시 여기서만 만든다(5.1).
    // AI용(순찰 대기, 수색 지점, 조준 오차)과 연출용(대사, 혈흔 모양, 탄피 방향, 총구 화염)을 나눠서
    // 연출이 아무리 난수를 써도 AI의 재현성이 깨지지 않게 한다.
    public static class Rng
    {
        public static RandomSource Ai { get; private set; } = new RandomSource(1);
        public static RandomSource Fx { get; private set; } = new RandomSource(2);

        // 게임 시작 때 한 번 부른다. 고정 시드 모드면 AI 난수가 매번 같아진다
        public static void Init(bool useFixedSeed, int fixedSeed)
        {
            int aiSeed = useFixedSeed ? fixedSeed : Environment.TickCount;
            Ai = new RandomSource(aiSeed);

            // 연출 난수는 고정 시드 모드여도 고정하지 않는다
            Fx = new RandomSource(Environment.TickCount ^ 0x5F3759DF);
        }
    }
}
