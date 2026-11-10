using System;
using System.Collections.Generic;

namespace GameAi
{
    // events.json: 월드 이벤트 22종(MASTER 7.21, 9.11)
    [Serializable]
    public class EventTypeConfig
    {
        public int version = 1;
        public EventTypeDef[] events = Defaults();

        // 파일이 없을 때 쓰는 기본 22종. 열: 코드, 분류, 이름, 반경/인지 거리, 점수, 소음권총 점수, 수명(−1 미션 끝), 대사, 소음권총 대사, 우선순위, 감정, 세부, 최소 밝기, 1회
        static readonly string[] Table =
        {
            "SND_FOOTSTEP|sound|발소리|0|15|0|15|qwen||2|의심|바닥|0|0",
            "SND_DOOR|sound|문소리|6|25|0|15|qwen||2|의심||0|0",
            "SND_GUNSHOT|sound|총소리|0|60|30|15|instant|qwen|4|긴장||0|0",
            "SND_RELOAD|sound|장전소리|4|20|0|15|qwen||2|의심||0|0",
            "SND_SCREAM|sound|비명소리|10|50|0|15|qwen||4|긴장||0|0",
            "SND_WIND|sound|바람소리|0|0|0|4|chatter||1|안도||0|0",
            "SND_CAN|sound|깡통 차는 소리|8|30|0|15|qwen||3|놀람||0|0",
            "SND_DECOY|sound|연막탄 터지는 소리|10|30|0|15|qwen||3|의심||0|0",
            "CLU_CORPSE|clue|죽은 동료 발견|0|60|0|-1|qwen||5|놀람|동료 이름|0.15|1",
            "CLU_DOOR_OPEN|clue|문이 열려 있음|8|30|0|30|qwen||2|의심|평소 문 상태|0.15|1",
            "CLU_DOOR_CLOSED|clue|열려 있어야 할 문이 닫힘|8|30|0|30|qwen||2|의심|평소 문 상태|0.15|1",
            "CLU_CASING|clue|탄피 발견|4|25|0|60|qwen||2|의심||0.3|1",
            "CLU_BLOOD|clue|혈흔|6|35|0|60|qwen||3|긴장||0.3|1",
            "CLU_RADIO_HELP|clue|무전 지원요청|0|0|0|0|qwen||3|긴장||0|0",
            "CLU_WEAPON|clue|떨어진 무기 발견|6|40|0|-1|qwen||3|긴장||0.15|1",
            "CLU_NO_ANSWER|clue|동료 무응답|0|35|0|30|qwen||3|긴장|동료 이름|0|1",
            "SIG_FLASH|sight|번쩍임|0|45|0|15|qwen||3|놀람||0|0",
            "SIG_INTRUDER|sight|침입자 발견|0|90|0|8|instant||5|놀람||0|0",
            "SIG_GLIMPSE|sight|움직임 포착|0|20|0|8|qwen||3|의심||0|0",
            "SIG_LIGHT_OFF|sight|불 꺼짐|0|40|0|30|qwen||3|놀람||0|0",
            "SIG_ALLY_DOWN|sight|동료가 쓰러지는 장면|0|80|0|15|instant||5|분노|동료 이름|0|1",
            "SIG_DOOR_MOVE|sight|문이 저절로 움직임|0|35|0|15|qwen||3|의심||0|0",
        };

        static float F(string s) { return float.Parse(s, System.Globalization.CultureInfo.InvariantCulture); }

        public static EventTypeDef[] Defaults()
        {
            var list = new List<EventTypeDef>();
            foreach (string row in Table)
            {
                string[] c = row.Split('|');
                list.Add(new EventTypeDef
                {
                    code = c[0],
                    category = c[1],
                    name_ko = c[2],
                    radius_m = c[1] == "sound" ? F(c[3]) : 0f,
                    range_m = c[1] == "sound" ? 0f : F(c[3]),
                    score = F(c[4]),
                    score_silenced = F(c[5]),
                    lifetime_s = F(c[6]),
                    bark = c[7],
                    bark_silenced = c[8],
                    priority = int.Parse(c[9]),
                    default_emotion = c[10],
                    detail_fields = c[11].Length > 0 ? new[] { c[11] } : new string[0],
                    min_light = F(c[12]),
                    once = c[13] == "1",
                });
            }
            return list.ToArray();
        }
    }

    [Serializable]
    public class EventTypeDef
    {
        public string code = "";
        // sound / clue / sight
        public string category = "sound";
        public string name_ko = "";
        // 소리 반경. 0이면 발생한 쪽이 정한 반경(발소리·무기)
        public float radius_m;
        // 단서·시야 인지 거리. 0이면 적 시야 거리
        public float range_m;
        public float score;
        public float score_silenced;
        // 단서 수명(초). −1이면 미션 끝까지, 0이면 원본 수명(무전)
        public float lifetime_s = 15f;
        public bool once;
        // instant / qwen / chatter
        public string bark = "qwen";
        public string bark_silenced = "";
        public int priority = 1;
        public string default_emotion = "의심";
        public string[] detail_fields = new string[0];
        public float min_light;

        public bool IsSound { get { return category == "sound"; } }
        public bool IsSight { get { return category == "sight"; } }
        public string CategoryKo { get { return category == "sound" ? "소리" : category == "clue" ? "단서" : "시야"; } }
    }

    // 코드로 빠르게 찾는 표
    public sealed class EventTypeTable
    {
        readonly Dictionary<string, EventTypeDef> byCode = new Dictionary<string, EventTypeDef>();

        public int Count { get { return byCode.Count; } }

        public EventTypeTable(EventTypeConfig config)
        {
            foreach (EventTypeDef def in config.events)
                if (def != null && !string.IsNullOrEmpty(def.code)) byCode[def.code] = def;
            // 파일에 빠진 코드는 기본값으로 채운다
            foreach (EventTypeDef def in EventTypeConfig.Defaults())
            {
                if (byCode.ContainsKey(def.code)) continue;
                UnityEngine.Debug.LogWarning("[Events] events.json에 없어 기본값을 사용합니다: " + def.code);
                byCode[def.code] = def;
            }
        }

        public EventTypeDef Get(string code)
        {
            return code != null && byCode.TryGetValue(code, out EventTypeDef def) ? def : null;
        }

        public IEnumerable<EventTypeDef> All { get { return byCode.Values; } }
    }
}
