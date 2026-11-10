using System;

namespace GameAi
{
    // difficulty.json: 난이도 배율 m(7.12). 시야 거리·수색 시간 × m, 반응 지연·조준 오차 × 1/m
    [Serializable]
    public class DifficultyConfig
    {
        public int version = 1;
        public DifficultyLevel[] levels =
        {
            new DifficultyLevel { id = "easy", mult = 0.8f },
            new DifficultyLevel { id = "normal", mult = 1f },
            new DifficultyLevel { id = "hard", mult = 1.2f },
        };

        public float MultFor(string id)
        {
            foreach (DifficultyLevel level in levels)
                if (level.id == id) return level.mult;
            return 1f;
        }
    }

    [Serializable]
    public class DifficultyLevel
    {
        public string id = "normal";
        public float mult = 1f;
    }
}
