using System;

namespace GameAi
{
    // ai.json: 적 감지·의심도·교전·순찰·수색·경보·대사·길찾기 값(MASTER 9.5)
    [Serializable]
    public class AiConfig
    {
        public int version = 3;
        public GaugeConfig gauge = new GaugeConfig();
        public AiLightConfig light = new AiLightConfig();
        public HearingConfig hearing = new HearingConfig();
        public WindConfig wind = new WindConfig();
        public SuspicionConfig suspicion = new SuspicionConfig();
        public ClueConfig clue = new ClueConfig();
        public MemoryConfig memory = new MemoryConfig();
        public CombatConfig combat = new CombatConfig();
        public PatrolConfig patrol = new PatrolConfig();
        public SearchConfig search = new SearchConfig();
        public AlertConfig alert = new AlertConfig();
        public BarkTimingConfig bark = new BarkTimingConfig();
        public NavConfig nav = new NavConfig();
        public PassageConfig passage = new PassageConfig();
        public int perception_phase_count = 4;
    }

    // 인지 게이지(시각 식별, 7.7)
    [Serializable]
    public class GaugeConfig
    {
        public float suspect = 30f;
        public float detect = 100f;
        public float rise_per_s = 40f;
        public float hold_after_lost_s = 1f;
        public float fall_per_s = 15f;
        public float near_dist_m = 3f;
        public float near_mult = 2f;
        public float run_mult = 1.5f;
        public float crouch_mult = 0.5f;
    }

    // 밝기가 감지에 주는 영향(7.17)
    [Serializable]
    public class AiLightConfig
    {
        public float gauge_min_mult = 0.3f;
        public float view_min_mult = 0.5f;
        public float shot_flash_s = 0.3f;
        public float dark_threshold = 0.15f;
        public float dark_visible_m = 4f;
    }

    [Serializable]
    public class HearingConfig
    {
        public float wall_mult = 0.5f;
        public float react_threshold = 0.2f;
        public FloorMult floor_mult = new FloorMult();
    }

    // 바닥 종류별 발소리 반경 배율(S)
    [Serializable]
    public class FloorMult
    {
        public float t = 1.2f;
        public float w = 1f;
        public float k = 0.6f;
        public float o = 0.8f;

        public float For(FloorType floor)
        {
            switch (floor)
            {
                case FloorType.Tile: return t;
                case FloorType.Carpet: return k;
                case FloorType.Outdoor: return o;
                default: return w;
            }
        }
    }

    // 야외 돌풍(S)
    [Serializable]
    public class WindConfig
    {
        public float period_s = 20f;
        public float gust_s = 4f;
        public float mult = 0.6f;
    }

    // 의심도·긴장도(7.21)
    [Serializable]
    public class SuspicionConfig
    {
        public float quiet_s = 3f;
        public float decay_per_s = 6f;
        public float new_clue_min = 10f;
        public float detected_floor = 90f;
        public float radio_confidence = 0.8f;
        public float[] tension_bands = { 10f, 30f, 50f, 70f, 90f };
        public int search_points_high = 5;
        public float search_points_high_at = 50f;
    }

    // 사물 단서 조건과 단서 관련 주기
    [Serializable]
    public class ClueConfig
    {
        public float min_light = 0.15f;
        public float small_min_light = 0.3f;
        public float radio_check_s = 45f;
        public float dropped_weapon_offset_m = 0.4f;
        public float can_cooldown_s = 2f;
    }

    [Serializable]
    public class MemoryConfig
    {
        public float sound_clue_s = 15f;
        public float last_seen_s = 8f;
    }

    [Serializable]
    public class CombatConfig
    {
        public float reaction_delay_s = 0.5f;
        public float first_shot_s = 0.8f;
        public float accuracy = 0.6f;
        public float aim_error_deg = 5f;
        public float run_aim_mult = 1.5f;
        public float cover_search_radius_m = 6f;
        public float cover_hp_ratio = 0.4f;
    }

    [Serializable]
    public class PatrolConfig
    {
        public float wait_min_s = 2f;
        public float wait_max_s = 3f;
    }

    [Serializable]
    public class SearchConfig
    {
        public float radius_m = 8f;
        public int points = 3;
        public float total_s = 10f;
        // 전역 경계도가 경계 이상일 때 수색 시간 배율(7.8)
        [OptionalKey] public float alert_mult = 1.2f;
        // 수색 지점에 도착한 뒤 둘러보는 시간
        [OptionalKey] public float look_s = 1f;
    }

    [Serializable]
    public class AlertConfig
    {
        public float shout_radius_m = 12f;
        public float cooldown_s = 5f;
        public int max_per_detection = 2;
        [OptionalKey] public float radio_delay_s = 0.5f;
    }

    [Serializable]
    public class BarkTimingConfig
    {
        public float show_s = 2f;
        public float fade_s = 0.3f;
        public float cooldown_s = 3f;
        public float offset_m = 0.9f;
    }

    [Serializable]
    public class NavConfig
    {
        public float recalc_min_interval_s = 0.3f;
        public float blocked_s = 0.5f;
        public int chase_move_cells = 3;
        public float retry_delay_s = 1f;
        public int max_retry = 3;
        public float door_open_wait_s = 0.5f;
        public int max_paths_per_frame = 2;
    }

    [Serializable]
    public class PassageConfig
    {
        public float overlap_after_s = 2f;
    }
}
