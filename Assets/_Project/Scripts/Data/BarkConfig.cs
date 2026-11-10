using System;
using System.Collections.Generic;

namespace GameAi
{
    // barks.json: 기본 대사(bark_fallback)·성격·금지어(MASTER 9.8)
    [Serializable]
    public class BarkConfig
    {
        public int version = 1;
        public BarkEntry[] barks = DefaultBarks();
        [OptionalKey] public PersonaDef[] personas = DefaultPersonas();
        [OptionalKey] public string[] banned_words = { "AI", "모델", "플레이어", "게임" };

        static BarkEntry[] DefaultBarks()
        {
            return new[]
            {
                new BarkEntry { when = "investigate_noise", lines = new[] { "뭐지, 방금 그 소리?", "누구 있어?" }, emotion = "의심" },
                new BarkEntry { when = "investigate_sight", lines = new[] { "저기... 뭐가 움직였나?", "잘못 봤나?" }, emotion = "의심" },
                new BarkEntry { when = "detect", lines = new[] { "저기 있다!", "침입자다!", "거기 서!" }, emotion = "놀람" },
                new BarkEntry { when = "hit", lines = new[] { "윽!", "맞았다!" }, emotion = "분노" },
                new BarkEntry { when = "reload", lines = new[] { "장전 중! 엄호해!" }, emotion = "긴장" },
                new BarkEntry { when = "lost", lines = new[] { "어디 숨었어...", "놓쳤다, 찾아!" }, emotion = "긴장" },
                new BarkEntry { when = "search_end", lines = new[] { "기분 탓인가.", "아무도 없군." }, emotion = "안도" },
                new BarkEntry { when = "alert_received", lines = new[] { "연락 받았다, 간다." }, emotion = "긴장" },
            };
        }

        static PersonaDef[] DefaultPersonas()
        {
            return new[]
            {
                new PersonaDef { id = "veteran", label = "결단한 고참", style = "짧고 단정적, 명령조" },
                new PersonaDef { id = "rookie", label = "겁 많은 신참", style = "더듬거림, 혼잣말 많음" },
                new PersonaDef { id = "grumpy", label = "투덜이", style = "짜증, 귀찮음" },
                new PersonaDef { id = "cautious", label = "신중한 감시병", style = "차분, 상황 보고조" },
            };
        }

        public PersonaDef FindPersona(string id)
        {
            if (personas != null)
                foreach (PersonaDef p in personas)
                    if (p.id == id) return p;
            return null;
        }
    }

    [Serializable]
    public class BarkEntry
    {
        // 상태 계기(investigate_noise 등) 또는 이벤트 코드(SND_DOOR 등)
        public string when = "";
        public string[] lines = new string[0];
        public string emotion = "";
        // 긴장도 이름(평상·주의…) 또는 any
        public string tension = "any";
        // 성격 ID 또는 any
        public string persona = "any";
        // manual / pregen
        public string source = "manual";
    }

    [Serializable]
    public class PersonaDef
    {
        public string id = "veteran";
        public string label = "결단한 고참";
        public string style = "";
    }

    // 기본 대사 고르기(순수). 연출 난수원을 쓴다
    public static class BarkFallback
    {
        // 이벤트 코드에 기본 대사가 없을 때 대신 쓸 상태 계기
        public static string Alias(string when)
        {
            if (when == null) return null;
            if (when == "SIG_INTRUDER") return "detect";
            if (when == "SIG_GLIMPSE") return "investigate_sight";
            if (when == "CLU_RADIO_HELP") return "alert_received";
            if (when.StartsWith("SND_", StringComparison.Ordinal)) return "investigate_noise";
            if (when.StartsWith("SIG_", StringComparison.Ordinal) || when.StartsWith("CLU_", StringComparison.Ordinal)) return "investigate_sight";
            return null;
        }

        // 맞는 후보에서 최근 대사와 다른 것을 고른다. 없으면 null
        public static string Pick(BarkConfig config, string when, string tensionKo, string persona, IList<string> recent, RandomSource rng, out string emotion)
        {
            emotion = "";
            var candidates = new List<(string line, string emotion)>();
            for (int pass = 0; pass < 2 && candidates.Count == 0; pass++)
            {
                string key = pass == 0 ? when : Alias(when);
                if (key == null) continue;
                // 긴장도·성격이 맞는 후보 → 없으면 any 후보
                Collect(config, key, tensionKo, persona, true, candidates);
                if (candidates.Count == 0) Collect(config, key, tensionKo, persona, false, candidates);
            }
            if (candidates.Count == 0) return null;
            var fresh = candidates.FindAll(c => recent == null || !recent.Contains(c.line));
            var pool = fresh.Count > 0 ? fresh : candidates;
            var chosen = pool[rng.Int(0, pool.Count)];
            emotion = chosen.emotion;
            return chosen.line;
        }

        // specificOnly면 긴장도·성격이 지정된(any가 아닌) 후보만 모은다
        static void Collect(BarkConfig config, string key, string tensionKo, string persona, bool specificOnly, List<(string, string)> into)
        {
            foreach (BarkEntry entry in config.barks)
            {
                if (entry == null || entry.when != key || entry.lines == null) continue;
                bool tAny = string.IsNullOrEmpty(entry.tension) || entry.tension == "any";
                bool pAny = string.IsNullOrEmpty(entry.persona) || entry.persona == "any";
                if (!(tAny || entry.tension == tensionKo) || !(pAny || entry.persona == persona)) continue;
                if (specificOnly && tAny && pAny) continue;
                foreach (string line in entry.lines)
                    if (!string.IsNullOrEmpty(line)) into.Add((line, entry.emotion));
            }
        }
    }
}
