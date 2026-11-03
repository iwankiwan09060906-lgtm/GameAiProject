using System;

namespace GameAi
{
    // weapons.json: 무기 3종과 전환·근접 값(7.3)
    [Serializable]
    public class WeaponConfig
    {
        public int version = 2;
        public WeaponDef[] weapons = DefaultWeapons();

        // 무기를 바꾼 뒤 쏠 수 있을 때까지의 지연
        public float switch_delay_s = 0.3f;
        public PlayerLoadout player = new PlayerLoadout();
        public MeleeConfig melee = new MeleeConfig();

        // JSON이 없을 때 쓰는 문서 7.3 표의 값
        static WeaponDef[] DefaultWeapons()
        {
            return new[]
            {
                new WeaponDef { id = "pistol", display_name = "P-9", damage = 20, range_m = 15, fire_interval_s = 0.3f, noise_radius_m = 12, mag_size = 12, reserve = 36, reload_s = 1.5f, auto = false },
                new WeaponDef { id = "silenced_pistol", display_name = "P-9 SD", damage = 17, range_m = 12, fire_interval_s = 0.3f, noise_radius_m = 3, mag_size = 10, reserve = 30, reload_s = 1.8f, auto = false },
                new WeaponDef { id = "rifle", display_name = "AR-30", damage = 30, range_m = 30, fire_interval_s = 0.12f, noise_radius_m = 25, mag_size = 30, reserve = 90, reload_s = 2.5f, auto = true },
            };
        }
    }

    // 무기 하나의 수치
    [Serializable]
    public class WeaponDef
    {
        // 그림 이름(held_{id})과 소리 이름에도 쓰이는 ID
        public string id = "pistol";
        public string display_name = "P-9";
        public int damage = 20;
        public float range_m = 15f;
        public float fire_interval_s = 0.3f;

        // 쐈을 때 소리가 퍼지는 반경(Phase 4)
        public float noise_radius_m = 12f;
        public int mag_size = 12;
        public int reserve = 36;
        public float reload_s = 1.5f;

        // true면 누르고 있는 동안 연사
        public bool auto = false;
    }

    // 플레이어의 시작 장비
    [Serializable]
    public class PlayerLoadout
    {
        public int hp = 100;
        public int armor = 50;
        public int decoys = 2;
        public float nvg_battery_s = 60f;
        public float nvg_recharge_per_s = 0.5f;
    }

    // 근접 제압 조건(7.5)
    [Serializable]
    public class MeleeConfig
    {
        public float range_m = 1.5f;

        // 적의 뒤쪽 몇 도 부채꼴 안에서만 가능한지
        public float back_arc_deg = 120f;
    }

    // ai.json. 지금은 근접 제압이 쓰는 게이지 값만 읽는다. 나머지는 Phase 5에서 더한다
    [Serializable]
    public class AiConfig
    {
        public int version = 2;
        public GaugeConfig gauge = new GaugeConfig();
    }

    [Serializable]
    public class GaugeConfig
    {
        // 의심(30)과 발견(100) 기준
        public float suspect = 30f;
        public float detect = 100f;
    }

    // enemies.json: 적 종류별 수치(7.7)
    [Serializable]
    public class EnemyConfig
    {
        public int version = 2;
        public EnemyTypeDef[] types = DefaultTypes();
        public EnemyMoveConfig move = new EnemyMoveConfig();

        static EnemyTypeDef[] DefaultTypes()
        {
            return new[]
            {
                new EnemyTypeDef
                {
                    id = "guard", hp = 50, fov_deg = 90, view_m = 10, alert = "shout", sprite = "guard", debug_color = "#E04545",
                    weapon = new EnemyWeaponDef { damage = 12, interval_s = 0.5f, range_m = 12, mag = 12, reload_s = 1.5f },
                },
                new EnemyTypeDef
                {
                    id = "rifleman", hp = 60, fov_deg = 70, view_m = 14, alert = "radio", sprite = "rifleman", debug_color = "#A82020",
                    weapon = new EnemyWeaponDef { damage = 8, interval_s = 0.25f, range_m = 20, mag = 30, reload_s = 2.5f },
                },
            };
        }

        // ID로 종류를 찾는다. 없으면 null
        public EnemyTypeDef Find(string id)
        {
            foreach (var t in types)
                if (t.id == id) return t;
            return null;
        }
    }

    [Serializable]
    public class EnemyTypeDef
    {
        public string id = "guard";
        public int hp = 50;
        public float fov_deg = 90f;
        public float view_m = 10f;
        public EnemyWeaponDef weapon = new EnemyWeaponDef();

        // 경보 방식: shout(외침) 또는 radio(무전)
        public string alert = "shout";

        // 그림 이름 접두사(guard → guard_idle 등)
        public string sprite = "guard";
        public string debug_color = "#E04545";
    }

    // 적이 쓰는 무기. 적은 예비 탄약이 무제한이라 예비 항목이 없다
    [Serializable]
    public class EnemyWeaponDef
    {
        public int damage = 12;
        public float interval_s = 0.5f;
        public float range_m = 12f;
        public int mag = 12;
        public float reload_s = 1.5f;
    }

    [Serializable]
    public class EnemyMoveConfig
    {
        public float patrol_speed = 2f;
        public float chase_speed = 3.5f;
    }

    // missions.json: 맵 목록과 폭탄 해체 설정
    [Serializable]
    public class MissionConfig
    {
        public int version = 1;
        public MissionMapEntry[] maps =
        {
            new MissionMapEntry { id = "map_01", file = "maps/map_01.json", mission = "eliminate" },
        };
        public BombConfig bomb = new BombConfig();
    }

    [Serializable]
    public class MissionMapEntry
    {
        public string id = "map_01";

        // Config 폴더 기준 맵 파일 경로
        public string file = "maps/map_01.json";

        // eliminate(소탕), steal(자료 탈취), steal_exit(탈취 후 탈출)
        public string mission = "eliminate";
    }

    [Serializable]
    public class BombConfig
    {
        public bool enabled = false;
        public float time_limit_s = 180f;
        public float defuse_s = 5f;
        public bool keep_progress = true;
    }
}
