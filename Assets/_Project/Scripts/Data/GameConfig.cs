using System;

namespace GameAi
{
    // game.json. 기본값은 MASTER_PROMPT 7장·9.1과 같다. 키 이름이 JSON과 같아야 해서 snake_case를 쓴다.
    [Serializable]
    public class GameConfig
    {
        public int version = 2;
        public int[] reference_resolution = { 1920, 1080 };
        public int[] pixel_reference_resolution = { 960, 540 };
        public int pixels_per_unit = 32;
        public float camera_ortho_size = 8.4375f;
        public float camera_lookahead_ratio = 0.3f;
        public float camera_lookahead_max_m = 3f;
        public float interact_range_m = 1.2f;
        public bool fixed_seed_enabled = false;
        public int fixed_seed = 12345;
        public string default_difficulty = "normal";
        public LightingConfig lighting = new LightingConfig();
        public AudioConfig audio = new AudioConfig();
        public PlayerConfig player = new PlayerConfig();
        public CharacterViewConfig view = new CharacterViewConfig();
        public FogConfig fog = new FogConfig();
        public FxConfig fx = new FxConfig();
        public CrosshairConfig crosshair = new CrosshairConfig();
    }

    [Serializable]
    public class LightingConfig
    {
        public string ambient_color = "#1A1830";
        public float global_intensity_scale = 10f;
        public string lamp_color = "#FFC873";
        public float lamp_falloff = 0.6f;
        public float shadow_strength = 0.9f;
        public string wall_rim_color = "#3A3A44";
        public string muzzle_light_color = "#FFD38A";
        public float muzzle_light_radius_m = 3f;
        public float muzzle_light_s = 0.05f;
        public string nvg_color = "#7CFF9A";
        public float nvg_global_intensity = 0.6f;
        public float film_grain_alpha = 0.04f;
    }

    [Serializable]
    public class AudioConfig
    {
        public float ambience_fade_s = 0.5f;
    }

    [Serializable]
    public class PlayerConfig
    {
        public float walk_speed = 3.5f;
        public float run_mult = 1.5f;
        public float crouch_mult = 0.5f;
        public float radius_m = 0.4f;
        public float fov_deg = 90f;
        public float view_m = 12f;
        public int vision_rays = 90;
    }

    [Serializable]
    public class CharacterViewConfig
    {
        public float hand_x_m = 0.25f;
        public float hand_y_m = -0.25f;
        public float held_center_m = 0.16f;
        public float muzzle_m = 0.31f;
        public float walk_fps = 8f;
        public float shoot_frame_s = 0.1f;
        public float melee_frame_s = 0.2f;
        public float enemy_radius_m = 0.4f;
    }

    [Serializable]
    public class FogConfig
    {
        public float unexplored_alpha = 1f;
        public float memory_alpha = 0.65f;
        public float memory_gray = 0.04f;
    }

    [Serializable]
    public class FxConfig
    {
        public float tracer_s = 0.1f;
        public string tracer_color = "#FFE680";
        public float muzzle_s = 0.05f;
        public float hit_flash_s = 0.1f;
        public float hit_blood_s = 0.3f;
        public int pool_max = 512;
        public float casing_s = 0.15f;
        public float casing_dist_min_m = 0.4f;
        public float casing_dist_max_m = 0.8f;
        public float blood_scale_min = 0.5f;
        public float blood_scale_max = 0.9f;
        public float blood_scatter_m = 0.3f;
    }

    [Serializable]
    public class CrosshairConfig
    {
        public float shot_spread = 1.35f;
        public float shot_spread_s = 0.15f;
        public float run_spread = 1.2f;
        public int display_scale = 1;
    }
}
