using System;

namespace GameAi
{
    // 시드를 정하면 항상 같은 수열을 내는 난수원(xorshift32)
    public sealed class RandomSource
    {
        uint state;

        public RandomSource(int seed)
        {
            state = (uint)seed;
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

        // 0 이상 1 미만
        public float Value()
        {
            return (NextUInt() >> 8) * (1f / (1 << 24));
        }

        public float Range(float min, float max)
        {
            return min + (max - min) * Value();
        }

        // min 이상 maxExclusive 미만
        public int Int(int min, int maxExclusive)
        {
            if (maxExclusive <= min) return min;
            return min + (int)(NextUInt() % (uint)(maxExclusive - min));
        }
    }

    // AI와 연출의 난수를 분리해 연출이 AI 재현성에 영향을 주지 않게 한다
    public static class Rng
    {
        public static RandomSource Ai { get; private set; } = new RandomSource(1);
        public static RandomSource Fx { get; private set; } = new RandomSource(2);

        public static void Init(bool useFixedSeed, int fixedSeed)
        {
            int aiSeed = useFixedSeed ? fixedSeed : Environment.TickCount;
            Ai = new RandomSource(aiSeed);
            Fx = new RandomSource(Environment.TickCount ^ 0x5F3759DF);
        }
    }
}
