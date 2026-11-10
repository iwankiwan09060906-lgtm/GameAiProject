using UnityEngine;

namespace GameAi
{
    // 월드 이벤트를 만드는 한곳. 고유 ID를 붙이고, 소리 이벤트면 HUD용 NoiseEvent도 같이 보낸다
    public static class WorldEvents
    {
        static int nextId;

        public static EventTypeTable Table { get; private set; } = new EventTypeTable(new EventTypeConfig());

        // 새 판을 시작할 때 부른다(씬 재로드)
        public static void Reset(EventTypeTable table)
        {
            nextId = 0;
            Table = table ?? Table;
        }

        // radius가 0이면 events.json의 반경을 쓴다. 돌려주는 값은 이벤트 ID
        public static int Raise(string code, Vector2 position, int sourceId, int objectId = -1, float radius = 0f, bool flag = false, float intensity = 1f)
        {
            EventTypeDef def = Table.Get(code);
            if (def == null)
            {
                Debug.LogWarning("[Events] 알 수 없는 이벤트 코드: " + code);
                return -1;
            }
            if (radius <= 0f && def.IsSound) radius = def.radius_m;
            int id = ++nextId;
            EventBus.Publish(new WorldEvent
            {
                eventId = id,
                code = code,
                position = position,
                time = GameClock.Time,
                intensity = intensity,
                radius = radius,
                sourceId = sourceId,
                objectId = objectId,
                flag = flag,
            });
            if (def.IsSound && TryNoiseKind(code, out NoiseKind kind))
            {
                EventBus.Publish(new NoiseEvent { position = position, kind = kind, radius = radius, sourceId = sourceId, time = GameClock.Time });
            }
            return id;
        }

        public static bool TryNoiseKind(string code, out NoiseKind kind)
        {
            switch (code)
            {
                case "SND_FOOTSTEP": kind = NoiseKind.Footstep; return true;
                case "SND_GUNSHOT": kind = NoiseKind.Shot; return true;
                case "SND_DECOY": kind = NoiseKind.Decoy; return true;
                case "SND_DOOR": kind = NoiseKind.Door; return true;
                case "SND_RELOAD": kind = NoiseKind.Reload; return true;
                case "SND_SCREAM": kind = NoiseKind.Scream; return true;
                case "SND_CAN": kind = NoiseKind.Can; return true;
                default: kind = NoiseKind.Footstep; return false;
            }
        }
    }
}
