using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace GameAi
{
    // 상황 상태값(7.22): 숫자 대신 말로 된 구간값만 담는다. 플레이어 위치·체력·무기, 의심도 숫자, 다른 적 상태, 좌표는 넣지 않는다
    public sealed class BarkContext
    {
        public string Event = "";
        public string Category = "";
        public string Route = "직접";
        public string Direction = "";
        public string Distance = "";
        public string When = "";
        public string Repeat = "처음";
        public readonly List<KeyValuePair<string, string>> Detail = new List<KeyValuePair<string, string>>();
        public string Role = "";
        public string Persona = "";
        public string PersonaStyle = "";
        public string Task = "";
        public string Tension = "";
        public string Body = "";
        public string Allies = "";
        public string Place = "";
        public string Light = "";
        public string Wind = "";
        public string Alert = "";
        public readonly List<string> Recent = new List<string>();
        public string DefaultEmotion = "의심";

        public static string DirectionOf(Vector2 facing, Vector2 from, Vector2 to)
        {
            Vector2 d = to - from;
            if (d.sqrMagnitude < 1e-4f || facing.sqrMagnitude < 1e-6f) return "앞";
            float angle = Vector2.SignedAngle(facing, d);
            if (Mathf.Abs(angle) <= 45f) return "앞";
            if (Mathf.Abs(angle) >= 135f) return "뒤";
            return angle > 0f ? "왼쪽" : "오른쪽";
        }

        public static string DistanceOf(float meters) { return meters < 4f ? "가까움" : meters <= 10f ? "중간" : "멀음"; }
        public static string WhenOf(float ageSeconds) { return ageSeconds < 2f ? "방금" : ageSeconds < 10f ? "조금 전" : "오래 전"; }
        public static string RepeatOf(int previous) { return previous <= 0 ? "처음" : previous == 1 ? "두 번째" : "여러 번"; }
        public static string BodyOf(float hpRatio) { return hpRatio >= 0.7f ? "멀쩡" : hpRatio >= 0.3f ? "부상" : "위독"; }
        public static string AlliesOf(int count) { return count <= 0 ? "없음" : count == 1 ? "한 명" : "여럿"; }

        public static string TaskOf(EnemyState state, bool moving, bool returning)
        {
            switch (state)
            {
                case EnemyState.Investigate: return "확인";
                case EnemyState.Search: return "수색";
                case EnemyState.Chase:
                case EnemyState.Engage: return "교전 준비";
                default: return returning ? "복귀" : moving ? "순찰" : "초소 대기";
            }
        }

        public static string AlertOf(int level) { return level >= 2 ? "전투" : level == 1 ? "경계" : "평상"; }

        // 캐시 키: 코드·경로·방향·거리·반복·업무·긴장도·성격
        public string CacheKey(string code)
        {
            return code + "|" + Route + "|" + Direction + "|" + Distance + "|" + Repeat + "|" + Task + "|" + Tension + "|" + Persona;
        }

        public string DetailValue(string key)
        {
            foreach (var kv in Detail)
                if (kv.Key == key) return kv.Value;
            return null;
        }

        // 사용자 메시지 JSON(4장 입력 예시와 같은 형식)
        public string ToJson()
        {
            var sb = new StringBuilder("{");
            Field(sb, "사건", Event);
            Field(sb, "분류", Category);
            Field(sb, "알게 된 경로", Route);
            if (!string.IsNullOrEmpty(Direction)) Field(sb, "방향", Direction);
            Field(sb, "거리", Distance);
            Field(sb, "시점", When);
            Field(sb, "반복", Repeat);
            sb.Append("\"세부\":{");
            for (int i = 0; i < Detail.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(Quote(Detail[i].Key)).Append(':').Append(Quote(Detail[i].Value));
            }
            sb.Append("},");
            Field(sb, "역할", Role);
            Field(sb, "성격", Persona);
            Field(sb, "업무", Task);
            Field(sb, "긴장도", Tension);
            Field(sb, "몸 상태", Body);
            Field(sb, "주변 동료", Allies);
            Field(sb, "장소", Place);
            Field(sb, "밝기", Light);
            Field(sb, "바람", Wind);
            Field(sb, "전체 경계", Alert);
            sb.Append("\"최근 대사\":[");
            for (int i = 0; i < Recent.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(Quote(Recent[i]));
            }
            sb.Append("]}");
            return sb.ToString();
        }

        static void Field(StringBuilder sb, string key, string value)
        {
            sb.Append(Quote(key)).Append(':').Append(Quote(value ?? "")).Append(',');
        }

        public static string Quote(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in s ?? "")
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }
    }

    // Qwen 응답 검증(7.22). 하나라도 실패하면 기본 대사
    public static class BarkValidator
    {
        public static readonly string[] Emotions = { "의심", "놀람", "긴장", "분노", "안도" };
        // 방향을 말하는 표현과 그 방향. "뒤져"처럼 다른 낱말이 걸리지 않게 두 글자 이상만 본다
        static readonly string[,] Directions = { { "왼쪽", "왼쪽" }, { "오른쪽", "오른쪽" }, { "뒤쪽", "뒤" }, { "뒤에서", "뒤" }, { "앞쪽", "앞" }, { "앞에서", "앞" } };
        static readonly string[] WeaponWords = { "권총", "소총", "소음권총", "AR-30", "P-9", "라이플", "샷건" };
        static readonly string[] Ranks = { "이병", "일병", "상병", "병장", "하사", "중사", "상사", "소위", "중위", "대위" };

        // <think> 태그·따옴표·앞뒤 공백 제거
        public static string Clean(string raw)
        {
            if (raw == null) return "";
            string s = raw;
            int think = s.IndexOf("</think>", System.StringComparison.Ordinal);
            if (think >= 0) s = s.Substring(think + 8);
            s = s.Replace("<think>", "").Trim();
            s = s.Trim('"', '\'', '“', '”', '‘', '’', '「', '」').Trim();
            return s;
        }

        public static int Length(string s) { return new StringInfo(s ?? "").LengthInTextElements; }

        public static string Normalize(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s ?? "")
                if (char.IsLetterOrDigit(c)) sb.Append(c);
            return sb.ToString();
        }

        static bool IsHangul(char c) { return c >= 0xAC00 && c <= 0xD7A3 || c >= 0x3131 && c <= 0x318E; }

        // 통과하면 "OK", 아니면 실패 코드
        public static string Validate(string line, BarkContext context, IList<string> bannedWords, IList<string> recentNpc, IList<string> recentGlobal)
        {
            if (string.IsNullOrEmpty(line)) return "EMPTY";
            if (line.IndexOf('\n') >= 0 || line.IndexOf('\r') >= 0) return "MULTI_LINE";
            if (Length(line) > 20) return "TOO_LONG";

            string callsign = context.DetailValue("동료 이름") ?? "";
            string withoutName = callsign.Length > 0 ? line.Replace(callsign, "") : line;
            int letters = 0, hangul = 0;
            foreach (char c in withoutName)
            {
                if (char.IsDigit(c)) return "NOT_KOREAN";
                if (c < 128 && char.IsLetter(c)) return "NOT_KOREAN";
                if (char.IsLetter(c)) letters++;
                if (IsHangul(c)) hangul++;
            }
            if (letters > 0 && hangul < letters * 0.6f) return "NOT_KOREAN";

            // 입력에 없는 방향·무기 이름·호칭을 말하면 정보 누출
            for (int i = 0; i < Directions.GetLength(0); i++)
                if (withoutName.Contains(Directions[i, 0]) && Directions[i, 1] != context.Direction) return "LEAK";
            foreach (string w in WeaponWords)
                if (withoutName.Contains(w)) return "LEAK";
            foreach (string r in Ranks)
                if (withoutName.Contains(r)) return "LEAK";

            if (bannedWords != null)
                foreach (string b in bannedWords)
                    if (!string.IsNullOrEmpty(b) && line.IndexOf(b, System.StringComparison.OrdinalIgnoreCase) >= 0) return "BANNED";

            string n = Normalize(line);
            if (recentNpc != null)
                foreach (string r in recentNpc) if (Normalize(r) == n) return "REPEAT";
            if (recentGlobal != null)
                foreach (string r in recentGlobal) if (Normalize(r) == n) return "REPEAT";
            return "OK";
        }

        public static string FixEmotion(string emotion, string fallback)
        {
            foreach (string e in Emotions)
                if (e == emotion) return e;
            return fallback;
        }
    }
}
