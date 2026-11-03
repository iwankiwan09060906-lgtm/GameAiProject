using System;

namespace GameAi
{
    // game.json의 모양. 기본값은 MASTER_PROMPT 7장·9.1과 같다.
    // 키 이름이 JSON과 똑같아야 읽히므로 snake_case를 그대로 쓴다.
    // 여기 적힌 기본값은 JSON이 없거나 값이 빠졌을 때만 쓰인다.
    [Serializable]
    public class GameConfig
    {
        public int version = 2;

        // UI 기준 해상도(Canvas Scaler)
        public int[] reference_resolution = { 1920, 1080 };

        // 픽셀 카메라 기준 해상도. 1920x1080에서 정확히 2배가 된다
        public int[] pixel_reference_resolution = { 960, 540 };
        public int pixels_per_unit = 32;

        // 카메라 세로 절반 크기(m). 540px / 32 / 2 = 8.4375
        public float camera_ortho_size = 8.4375f;

        // 카메라를 커서 쪽으로 앞당기는 비율과 최대 거리(m)
        public float camera_lookahead_ratio = 0.3f;
        public float camera_lookahead_max_m = 3f;

        public float interact_range_m = 1.2f;

        // 수직동기: 0이면 끔, 1이면 모니터 주사율에 맞춤
        public int vsync_count = 1;

        // 켜면 AI 난수가 매번 같아져 같은 입력이 같은 결과를 낸다(13.3)
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

    // 화면 조명 값. 색은 "#RRGGBB" 글자로 적는다
    [Serializable]
    public class LightingConfig
    {
        public string ambient_color = "#1A1830";

        // 화면용 전역광 세기 = 맵의 ambient × 이 값.
        // 판정용 밝기 0.15를 그대로 쓰면 어두운 색과 곱해져 화면이 완전히 검어져서 키워 준다
        public float global_intensity_scale = 10f;
        public string lamp_color = "#FFC873";
        public float lamp_falloff = 0.6f;
        public float shadow_strength = 0.9f;

        // 벽이 바닥과 맞닿은 변에 그리는 1px 밝은 선
        public string wall_rim_color = "#3A3A44";
        public string muzzle_light_color = "#FFD38A";
        public float muzzle_light_radius_m = 3f;
        public float muzzle_light_s = 0.05f;

        // 야간 투시(Phase 9)
        public string nvg_color = "#7CFF9A";
        public float nvg_global_intensity = 0.6f;
        public float film_grain_alpha = 0.04f;
    }

    [Serializable]
    public class AudioConfig
    {
        public float ambience_fade_s = 0.5f;
    }

    // 플레이어 이동·시야 값(7.2)
    [Serializable]
    public class PlayerConfig
    {
        public float walk_speed = 3.5f;

        // 달리기·웅크리기는 걷기 속도에 곱한다
        public float run_mult = 1.5f;
        public float crouch_mult = 0.5f;

        // 몸 크기. 지름 0.8m
        public float radius_m = 0.4f;
        public float fov_deg = 90f;
        public float view_m = 12f;

        // 시야 부채꼴에 쏘는 레이 개수. 많을수록 정확하고 느리다
        public int vision_rays = 90;
    }

    // 캐릭터 그림 배치. 모든 값은 몸 중심에서 본 거리(m)이고 오른쪽(+X)을 보는 기준이다
    [Serializable]
    public class CharacterViewConfig
    {
        // 총을 쥔 손의 위치
        public float hand_x_m = 0.25f;
        public float hand_y_m = -0.25f;

        // 총 그림의 중심이 손에서 얼마나 앞에 있는지(그림 폭의 절반)
        public float held_center_m = 0.16f;

        // 손에서 총구까지의 거리
        public float muzzle_m = 0.31f;

        // 걷기 애니메이션 초당 프레임 수와 사격·근접 그림을 보여 주는 시간
        public float walk_fps = 8f;
        public float shoot_frame_s = 0.1f;
        public float melee_frame_s = 0.2f;
        public float enemy_radius_m = 0.4f;
    }

    // 탐색 기억 덮개의 투명도와 색
    [Serializable]
    public class FogConfig
    {
        public float unexplored_alpha = 1f;
        public float memory_alpha = 0.65f;
        public float memory_gray = 0.04f;
    }

    // 전투 연출 값
    [Serializable]
    public class FxConfig
    {
        public float tracer_s = 0.1f;
        public string tracer_color = "#FFE680";
        public float muzzle_s = 0.05f;
        public float hit_flash_s = 0.1f;
        public float hit_blood_s = 0.3f;

        // 혈흔·탄피 오브젝트 풀 최대 개수. 넘으면 오래된 것부터 재사용한다
        public int pool_max = 512;
        public float casing_s = 0.15f;
        public float casing_dist_min_m = 0.4f;
        public float casing_dist_max_m = 0.8f;
        public float blood_scale_min = 0.5f;
        public float blood_scale_max = 0.9f;
        public float blood_scatter_m = 0.3f;
    }

    // 조준선. 벌어짐은 연출만이고 명중률과 무관하다
    [Serializable]
    public class CrosshairConfig
    {
        public float shot_spread = 1.35f;
        public float shot_spread_s = 0.15f;
        public float run_spread = 1.2f;
        public int display_scale = 1;
    }
}
