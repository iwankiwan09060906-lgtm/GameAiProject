// 맵 1(map_01.json)을 코드로 배치해 만든다. 실행: AssetSource/build_map.ps1 (Unity 없이 .NET만 사용)
//
// 손으로 80x60 문자 격자를 쓰면 소품 그림과 막힌 칸이 어긋나기 쉬워서, 방·문·소품을 m 단위로 놓으면
// 소품 그림의 바닥 면적만큼 엄폐 칸(c 낮음, C 높음)을 자동으로 찍는다.
// 실행하면 맵 JSON과 함께 AssetSource/map_preview.png(바닥·벽·소품 합성)를 만들고,
// 시작점에서 모든 적·순찰 지점·문 앞까지 걸어갈 수 있는지 검사해 문제를 출력한다.
//
// 좌표: x 오른쪽, y 위쪽, 단위 m. 맵 40m x 30m, 칸 0.5m(80 x 60칸). 벽 두께 = 한 칸 = 0.5m
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Text;

public static class MapBuild
{
    const float Cell = 0.5f;
    const int W = 80, H = 60;
    const int Ppu = 32;

    static char[,] grid;
    static char[,] floor;
    static readonly List<string> props = new List<string>();
    static readonly List<string> lights = new List<string>();
    static readonly List<object[]> previewProps = new List<object[]>();
    static readonly List<string> problems = new List<string>();
    static string deco, tiles;

    public static string Run(string srcDir, string projectDir)
    {
        deco = Path.Combine(projectDir, "Assets/_Project/Art/deco");
        tiles = Path.Combine(projectDir, "Assets/_Project/Art/tiles");
        grid = new char[W, H];
        floor = new char[W, H];
        props.Clear(); lights.Clear(); previewProps.Clear(); problems.Clear();

        Layout();
        Yard();
        Storage();
        Corridor();
        Lobby();
        Restroom();
        ServerRoom();
        BreakRoom();
        Office();
        Markers();

        Validate();
        string json = ToJson();
        File.WriteAllText(Path.Combine(projectDir, "Assets/StreamingAssets/Config/maps/map_01.json"), json, new UTF8Encoding(false));
        Preview(Path.Combine(srcDir, "map_preview.png"));
        return problems.Count == 0 ? "문제 없음, 소품 " + props.Count + "개, 조명 " + lights.Count + "개" : string.Join("\n", problems.ToArray());
    }

    // ---------- 벽·문·바닥 ----------
    // 방 배치(m):
    //   마당(야외)      x 0~14,  y 0~12   플레이어 시작
    //   휴게실          x 14~25, y 0~12
    //   사무실          x 25~40, y 0~12
    //   복도            x 0~40,  y 12.5~15.5
    //   창고            x 0~14,  y 16~30
    //   엘리베이터 홀   x 14~25, y 16~30 (북쪽 2m는 엘리베이터가 박힌 벽, 북동쪽은 화장실)
    //   서버실          x 25~40, y 16~30
    static void Layout()
    {
        Fill(0, 0, 40, 30, '.');
        FloorFill(0, 0, 40, 30, 'w');

        // 바깥 벽
        Fill(0, 0, 40, 0.5f, '#'); Fill(0, 29.5f, 40, 30, '#');
        Fill(0, 0, 0.5f, 30, '#'); Fill(39.5f, 0, 40, 30, '#');

        // 복도 남북 벽과 방 사이 벽
        Fill(0, 12, 40, 12.5f, '#');
        Fill(0, 15.5f, 40, 16, '#');
        Fill(14, 0, 14.5f, 12, '#'); Fill(14, 16, 14.5f, 30, '#');
        Fill(25, 0, 25.5f, 12, '#'); Fill(25, 16, 25.5f, 30, '#');

        // 화장실 벽과 엘리베이터가 박힌 두꺼운 북쪽 벽
        Fill(21, 22, 21.5f, 30, '#'); Fill(21, 22, 25, 22.5f, '#');
        Fill(14.5f, 27.5f, 21, 29.5f, '#');

        // 바닥 종류: o 야외, w 어두운 금속 타일, t 금속 판, k 푸른 금속 판
        FloorFill(0.5f, 0.5f, 14, 12, 'o');
        FloorFill(14.5f, 0.5f, 25, 12, 't');
        FloorFill(25.5f, 0.5f, 39.5f, 12, 'k');
        FloorFill(14.5f, 16, 25, 29.5f, 't');
        FloorFill(25.5f, 16, 39.5f, 29.5f, 'k');

        // 문(폭 1.5m = 3칸). 이어진 D 칸이 문 하나가 된다
        Fill(6, 12, 7.5f, 12.5f, 'D');        // 마당 ↔ 복도
        Fill(14, 5, 14.5f, 6.5f, 'D');        // 마당 ↔ 휴게실
        Fill(19, 12, 20.5f, 12.5f, 'D');      // 복도 ↔ 휴게실
        Fill(31.5f, 12, 33, 12.5f, 'D');      // 복도 ↔ 사무실
        Fill(6, 15.5f, 7.5f, 16, 'D');        // 복도 ↔ 창고
        Fill(31.5f, 15.5f, 33, 16, 'D');      // 복도 ↔ 서버실
        Fill(21, 24.5f, 21.5f, 26, 'D');      // 홀 ↔ 화장실
        Fill(14, 22.5f, 14.5f, 24, 'D');      // 창고 ↔ 홀
        Fill(25, 5, 25.5f, 6.5f, 'D');        // 휴게실 ↔ 사무실
        Fill(25, 18.5f, 25.5f, 20, 'D');      // 홀 ↔ 서버실

        // 홀은 복도로 트인 입구(문 없음)
        Fill(16.5f, 15.5f, 19, 16, '.');
        FloorFill(16.5f, 15.5f, 19, 16, 'w');

        // 창문: 시야·빛은 통과, 이동·총알은 막는다
        Fill(2, 12, 4.5f, 12.5f, 'W');
        Fill(22, 12, 24, 12.5f, 'W');
        Fill(27, 12, 30, 12.5f, 'W');
        Fill(34.5f, 15.5f, 38, 16, 'W');
    }

    // ---------- 방별 소품 ----------
    // Prop(이름, 중심 x, 중심 y, 막는 칸, 층, 바닥 깊이)
    //   막는 칸: 'c' 낮은 엄폐(시야 통과), 'C' 높은 엄폐(시야 차단), ' ' 막지 않음
    //   층: 0 바닥에 붙은 것, 1 세운 물건, 2 물건 위(책상 위 소품)
    //   바닥 깊이: 위에서 내려다본 듯 비스듬한 그림은 아래쪽 이 길이(m)만 바닥을 차지한다. 0이면 그림 전체

    static void Yard()
    {
        Prop("terr_slab", 3.0f, 9.5f, ' ', 0); Prop("terr_slab_cracked", 5.0f, 9.5f, ' ', 0);
        Prop("terr_plate_gray", 9.5f, 3.5f, ' ', 0); Prop("terr_plate_dirt", 11.5f, 3.5f, ' ', 0);
        Prop("ruin_scorch_2", 5.5f, 2.0f, ' ', 0); Prop("ruin_scorch_1", 10.5f, 6.0f, ' ', 0);
        Prop("terr_gold_pile", 2.4f, 5.0f, ' ', 0); Prop("terr_puddle", 8.0f, 6.2f, ' ', 0);
        Prop("terr_dirt_2", 4.2f, 4.4f, ' ', 0); Prop("terr_dirt_1", 12.0f, 8.0f, ' ', 0);
        Prop("terr_dirt_3", 1.5f, 8.0f, ' ', 0); Prop("terr_dirt_4", 9.0f, 10.8f, ' ', 0);
        Prop("terr_stain_2", 6.8f, 3.6f, ' ', 0); Prop("terr_stain_1", 11.0f, 1.2f, ' ', 0);
        Prop("terr_leaves", 13.0f, 6.8f, ' ', 0); Prop("terr_weed", 1.2f, 3.5f, ' ', 0);
        Prop("terr_weed", 7.5f, 8.6f, ' ', 0); Prop("terr_rock_1", 3.6f, 1.2f, ' ', 0);
        Prop("terr_rock_2", 12.6f, 9.6f, ' ', 0); Prop("terr_rock_3", 6.0f, 10.9f, ' ', 0);
        Prop("terr_rock_4", 9.4f, 7.2f, ' ', 0);
        Prop("ruin_tile_1", 2.2f, 7.0f, ' ', 0); Prop("ruin_tile_3", 7.8f, 2.4f, ' ', 0);
        Prop("ruin_tile_5", 12.2f, 11.0f, ' ', 0); Prop("ruin_tiles_2", 4.6f, 6.6f, ' ', 0);

        // 부서진 벽 조각은 아래쪽 0.6m가 높은 엄폐물
        Prop("ruin_wall_long", 9.6f, 8.6f, 'C', 1, 0.6f);
        Prop("ruin_wall_piece_1", 2.6f, 6.0f, 'C', 1, 0.6f);
        Prop("ruin_wall_piece_2", 12.6f, 6.0f, 'C', 1, 0.6f);
        Prop("ruin_pillar_broken", 6.0f, 9.0f, 'C', 1, 0.6f);
        Prop("ruin_rubble_long", 8.6f, 1.0f, 'c', 1);
        Prop("ruin_tiles_pile", 1.6f, 1.6f, 'c', 1);
        Prop("ruin_rubble_1", 6.4f, 5.0f, 'c', 1);
        Prop("ruin_rubble_5", 3.4f, 11.2f, 'c', 1);
        Prop("ruin_rubble_6", 11.6f, 11.3f, 'c', 1);
        Prop("ruin_rubble_2", 13.2f, 2.6f, 'c', 1);
        Prop("ruin_rubble_3", 4.4f, 3.0f, ' ', 1);
        Prop("ruin_rubble_4", 10.6f, 5.0f, ' ', 1);
        Prop("ruin_tiles_3", 8.8f, 4.6f, ' ', 1);
        Prop("ruin_tiles_4", 2.8f, 9.4f, ' ', 0);
        Prop("ruin_post", 13.2f, 10.0f, 'c', 1, 0.5f);
        Prop("ruin_sign_noentry", 13.3f, 0.95f, ' ', 1);
        Prop("ruin_sign_skull", 0.9f, 11.4f, ' ', 1);
        Prop("ruin_rad_1", 5.2f, 11.6f, ' ', 1); Prop("ruin_rad_2", 8.6f, 11.6f, ' ', 1);
        Prop("ruin_rad_3", 0.9f, 5.4f, ' ', 1);
        Prop("barrel_red", 1.0f, 10.2f, 'c', 1); Prop("debris", 11.2f, 7.4f, ' ', 0);
    }

    static void Storage()
    {
        Prop("frame_big", 7.0f, 22.5f, ' ', 0);
        Prop("vent_1", 8.6f, 17.0f, ' ', 0); Prop("vent_3", 4.0f, 22.4f, ' ', 0);
        Prop("cables_1", 5.6f, 25.6f, ' ', 0); Prop("cables_2", 11.6f, 18.6f, ' ', 0);
        Prop("terr_stain_3", 9.6f, 22.0f, ' ', 0); Prop("terr_stain_4", 2.8f, 18.6f, ' ', 0);
        Prop("scrap", 8.2f, 26.2f, ' ', 0); Prop("bolt", 3.6f, 20.0f, ' ', 0); Prop("scrap_2", 12.2f, 23.6f, ' ', 0);

        // 북쪽 벽을 따라 쌓인 화물
        Prop("cargo_pad", 2.1f, 27.9f, 'C', 1);
        Prop("crate_big", 4.8f, 28.6f, 'C', 1);
        Prop("crate_small", 6.6f, 28.9f, 'c', 1);
        Prop("barrel_bio", 7.9f, 28.5f, 'c', 1, 0.8f);
        Prop("barrel_white", 8.9f, 28.8f, 'c', 1);
        Prop("barrel_red", 9.8f, 28.9f, 'c', 1);
        Prop("barrel_stack", 10.8f, 28.5f, 'c', 1, 0.9f);
        Prop("hatch_arch", 12.2f, 28.9f, ' ', 1);
        Prop("conveyor", 13.45f, 26.8f, 'C', 1);

        // 서쪽 벽
        Prop("ladder", 0.95f, 19.4f, ' ', 1);
        Prop("hatch_window", 0.95f, 22.6f, 'C', 1, 0.7f);
        Prop("extinguisher", 0.8f, 24.2f, ' ', 1);
        Prop("hatch_plate", 0.95f, 25.2f, 'C', 1, 0.7f);

        // 가운데 엄폐물
        Prop("crate_big", 4.4f, 19.6f, 'C', 1);
        Prop("crate_small", 6.0f, 19.2f, 'c', 1);
        Prop("box", 2.0f, 17.0f, 'c', 1);
        Prop("drone_parts", 10.2f, 23.4f, 'c', 1);
        Prop("robot", 11.8f, 21.6f, 'c', 1);
        Prop("toolbox", 9.4f, 20.4f, ' ', 1);
        Prop("helmet", 8.4f, 24.6f, ' ', 1);
        Prop("gasmask", 7.4f, 23.4f, ' ', 1);
        Prop("barrier", 10.4f, 17.0f, 'c', 1);
        Prop("bin_bio", 13.3f, 17.1f, 'c', 1, 0.6f);
        Prop("fire", 12.4f, 19.8f, ' ', 1);
        Prop("canister", 3.0f, 24.0f, ' ', 1);
        Prop("medkit", 5.4f, 21.0f, ' ', 0);
        Prop("box", 12.6f, 25.0f, 'c', 1);
        Prop("item_9", 4.2f, 28.9f, ' ', 2); Prop("item_17", 6.6f, 29.1f, ' ', 2);
        Prop("item_26", 2.6f, 17.4f, ' ', 2); Prop("item_36", 9.0f, 21.2f, ' ', 0);
        Prop("item_22", 11.2f, 26.6f, ' ', 0);
    }

    static void Corridor()
    {
        Prop("pipe", 3.0f, 15.25f, ' ', 0); Prop("pipe", 11.0f, 15.25f, ' ', 0);
        Prop("pipe", 23.0f, 15.25f, ' ', 0); Prop("pipe", 27.6f, 15.25f, ' ', 0);
        Prop("rail_1", 10.5f, 12.75f, ' ', 0); Prop("rail_2", 28.5f, 12.8f, ' ', 0);
        Prop("rail_1", 36.5f, 12.75f, ' ', 0);
        Prop("floor_panel", 17.75f, 14.0f, ' ', 0);
        Prop("vent_2", 13.0f, 13.6f, ' ', 0); Prop("vent_4", 25.6f, 14.0f, ' ', 0);
        Prop("vent_1", 35.0f, 14.0f, ' ', 0);
        Prop("terr_stain_1", 4.0f, 13.4f, ' ', 0); Prop("terr_stain_2", 22.0f, 13.6f, ' ', 0);
        Prop("cone", 24.4f, 13.0f, ' ', 1); Prop("cone", 24.9f, 13.2f, ' ', 1);
        Prop("extinguisher", 13.6f, 15.05f, ' ', 1);
        Prop("bin_bio_2", 39.0f, 14.8f, 'c', 1, 0.6f);
        Prop("barrier", 1.2f, 14.6f, 'c', 1);
        Prop("keycard", 30.0f, 13.6f, ' ', 0);
    }

    static void Lobby()
    {
        Prop("hazard_pad", 17.75f, 25.4f, ' ', 0);
        Prop("elevator", 16.4f, 28.5f, ' ', 1);
        Prop("elevator_open", 19.2f, 28.5f, ' ', 1);
        Prop("terminal", 17.8f, 27.0f, 'c', 1);
        Prop("frame_small", 23.3f, 18.6f, ' ', 0);
        Prop("office_bench", 14.85f, 19.4f, 'c', 1);
        Prop("office_plant_tall", 15.0f, 27.0f, 'c', 1, 0.6f);
        Prop("office_plant_tall", 20.5f, 27.0f, 'c', 1, 0.6f);
        Prop("office_cooler_1", 24.6f, 16.9f, 'C', 1, 0.6f);
        Prop("office_seats_1", 21.85f, 17.6f, 'c', 1);
        Prop("office_seats_2", 24.65f, 20.9f, 'c', 1, 0.0f);
        Prop("window_long", 23.0f, 21.45f, ' ', 1);
        Prop("badge", 19.6f, 20.4f, ' ', 0);
        Prop("tablet", 16.2f, 22.0f, ' ', 0);
        Prop("office_trash", 20.7f, 16.5f, 'c', 1);
        Prop("office_locker_2", 22.0f, 21.0f, 'C', 1, 0.6f);
        Prop("office_seat_1", 16.2f, 17.0f, ' ', 1); Prop("office_seat_2", 19.4f, 17.0f, ' ', 1);
        Prop("item_14", 22.6f, 19.2f, ' ', 0); Prop("item_28", 15.6f, 24.4f, ' ', 0);
    }

    static void Restroom()
    {
        Prop("toilet", 22.3f, 28.6f, 'c', 1);
        Prop("toilet", 23.9f, 28.6f, 'c', 1);
        Prop("sink", 24.4f, 26.0f, 'c', 1);
        Prop("shower", 24.5f, 23.5f, 'c', 1);
        Prop("bin_bio", 22.0f, 23.2f, 'c', 1, 0.6f);
        Prop("terr_puddle", 23.0f, 25.0f, ' ', 0);
    }

    static void ServerRoom()
    {
        // 북쪽 벽의 서버 랙 두 줄과 가운데 관제창
        float rackY = 29.5f - 2.13f / 2f;
        for (int i = 0; i < 5; i++) Prop("server_" + (i + 1), 26.0f + i, rackY, 'C', 1, 1.0f);
        for (int i = 0; i < 5; i++) Prop("server_" + (i + 2), 35.0f + i, rackY, 'C', 1, 1.0f);
        Prop("airlock", 32.5f, 28.0f, 'C', 1, 1.2f);

        // 관제 콘솔 두 줄. 서버실 입구(x 31.5~33)에서 북쪽으로 길을 비운다
        Prop("console_blue", 28.4f, 23.4f, 'c', 1);
        Prop("console_green", 36.6f, 23.4f, 'c', 1);
        Prop("console_green", 28.4f, 19.4f, 'c', 1);
        Prop("console_blue", 36.6f, 19.4f, 'c', 1);
        Prop("office_chair_2", 27.6f, 22.0f, ' ', 1); Prop("office_chair_7", 29.4f, 21.9f, ' ', 1);
        Prop("office_chair_2", 36.0f, 22.0f, ' ', 1); Prop("office_chair_8", 37.6f, 21.9f, ' ', 1);
        Prop("office_chair_3", 28.0f, 18.0f, ' ', 1); Prop("office_chair_4", 36.8f, 18.0f, ' ', 1);
        Prop("frame_small", 32.25f, 21.4f, ' ', 0);
        Prop("cables_1", 32.6f, 25.2f, ' ', 0); Prop("cables_2", 26.6f, 25.8f, ' ', 0);
        Prop("keycard", 31.0f, 22.6f, ' ', 0); Prop("tablet", 33.6f, 19.6f, ' ', 0);
        Prop("terminal", 38.9f, 25.6f, 'c', 1);
        Prop("bin_bio_2", 26.0f, 16.8f, 'c', 1, 0.6f);
        Prop("robot", 38.8f, 16.9f, 'c', 1);
        Prop("drone_parts", 38.6f, 21.4f, 'c', 1);
        Prop("folder", 30.6f, 17.2f, ' ', 0); Prop("note", 34.6f, 17.0f, ' ', 0);

        // 콘솔 위 서류·물건(아이콘 시트)
        Prop("item_0", 27.4f, 23.2f, ' ', 2); Prop("item_7", 29.6f, 23.0f, ' ', 2);
        Prop("item_19", 37.6f, 23.0f, ' ', 2); Prop("item_24", 35.6f, 19.0f, ' ', 2);
        Prop("item_30", 29.4f, 19.0f, ' ', 2); Prop("item_33", 27.2f, 19.2f, ' ', 2);
        Prop("badge", 38.0f, 19.1f, ' ', 2);
    }

    static void BreakRoom()
    {
        // 남쪽 벽 조리대와 정수기
        Prop("office_counter_1", 15.3f, 1.15f, 'c', 1); Prop("office_counter_2", 16.6f, 1.15f, 'c', 1);
        Prop("office_counter_3", 17.9f, 1.15f, 'c', 1); Prop("office_counter_4", 19.2f, 1.15f, 'c', 1);
        Prop("office_water_1", 20.4f, 0.95f, 'c', 1); Prop("office_water_2", 21.0f, 0.95f, 'c', 1);
        Prop("office_trash_full", 21.8f, 0.95f, 'c', 1);
        Prop("office_cooler_2", 24.6f, 10.9f, 'C', 1, 0.6f);
        Prop("office_cooler_3", 24.6f, 9.0f, 'C', 1, 0.6f);

        // 소파와 탁자
        Prop("office_sofa_1", 17.2f, 10.9f, 'c', 1);
        Prop("office_sofa_2", 22.6f, 11.0f, 'c', 1);
        Prop("office_locker_1", 15.0f, 7.2f, 'C', 1, 0.6f);
        Prop("office_partition_3", 23.4f, 6.6f, 'c', 1, 0.5f);
        Prop("office_bin_white", 20.9f, 9.6f, ' ', 1);
        Prop("office_box_1", 23.9f, 3.8f, 'c', 1); Prop("office_box_2", 24.5f, 3.8f, 'c', 1);
        Prop("office_armchair_1", 15.2f, 8.6f, 'c', 1);
        Prop("office_armchair_2", 22.0f, 8.6f, 'c', 1);
        Prop("office_table_1", 18.4f, 8.2f, 'c', 1);
        Prop("office_table_small", 21.6f, 4.6f, 'c', 1);
        Prop("office_stool", 20.6f, 3.6f, ' ', 1); Prop("office_stool", 22.6f, 3.6f, ' ', 1);
        Prop("office_plant_small", 24.4f, 1.4f, 'c', 1, 0.6f);
        Prop("office_plant_pole", 14.95f, 3.0f, 'c', 1, 0.5f);
        Prop("office_clock", 21.0f, 11.7f, ' ', 1);
        Prop("window_small", 15.4f, 11.4f, ' ', 1);
        Prop("frame_mid", 18.0f, 4.6f, ' ', 0);
        Prop("office_cup", 18.0f, 8.4f, ' ', 2); Prop("office_mug", 18.8f, 8.3f, ' ', 2);
        Prop("office_cup_2", 21.5f, 4.8f, ' ', 2); Prop("office_pot", 16.3f, 1.5f, ' ', 2);
        Prop("terr_stain_3", 16.6f, 6.0f, ' ', 0);
    }

    static void Office()
    {
        // 북쪽: ㄱ자 책상 3개와 화이트보드
        Prop("office_ldesk_1", 27.6f, 9.7f, 'c', 1);
        Prop("office_ldesk_2", 35.6f, 9.7f, 'c', 1);
        Prop("office_ldesk_3", 38.4f, 9.7f, 'c', 1);
        Prop("office_chair_1", 27.8f, 8.1f, ' ', 1); Prop("office_chair_5", 35.4f, 8.2f, ' ', 1);
        Prop("office_chair_9", 38.2f, 8.2f, ' ', 1);
        Prop("office_board_1", 29.6f, 11.6f, ' ', 1); Prop("office_board_2", 34.6f, 11.6f, ' ', 1);
        Prop("office_board_3", 36.6f, 11.6f, ' ', 1);
        Prop("office_copier", 34.0f, 11.0f, 'C', 1, 0.6f);
        Prop("office_plant_dry", 25.9f, 11.0f, 'c', 1, 0.5f);
        Prop("office_printer", 30.6f, 11.3f, 'c', 1);

        // 가운데 책상 두 무리. 입구(x 31.5~33)에서 남쪽으로 길을 비운다
        Prop("office_desk_1", 27.0f, 5.4f, 'c', 1); Prop("office_desk_2", 28.6f, 5.4f, 'c', 1);
        Prop("office_desk_4", 35.0f, 5.4f, 'c', 1); Prop("office_desk_5", 36.6f, 5.4f, 'c', 1);
        Prop("office_desk_6", 38.4f, 5.4f, 'c', 1);
        Prop("office_monitor_green", 27.0f, 5.9f, ' ', 2); Prop("office_monitor_dark", 28.6f, 5.9f, ' ', 2);
        Prop("office_monitor_red", 35.0f, 5.9f, ' ', 2); Prop("office_monitor_green", 36.6f, 5.9f, ' ', 2);
        Prop("office_pc_old", 38.4f, 5.9f, ' ', 2);
        Prop("office_chair_6", 27.0f, 4.0f, ' ', 1); Prop("office_chair_10", 28.6f, 4.0f, ' ', 1);
        Prop("office_chair_11", 35.0f, 4.0f, ' ', 1); Prop("office_chair_12", 36.6f, 4.0f, ' ', 1);
        Prop("office_chair_8", 38.4f, 4.0f, ' ', 1);
        Prop("office_tower_1", 29.6f, 5.2f, ' ', 1); Prop("office_tower_2", 34.0f, 5.2f, ' ', 1);
        Prop("office_keyboard_1", 27.2f, 5.0f, ' ', 2); Prop("office_keyboard_2", 36.4f, 5.0f, ' ', 2);
        Prop("office_papers_1", 28.9f, 5.0f, ' ', 2); Prop("office_phone_desk", 35.6f, 9.9f, ' ', 2);
        Prop("office_calc_1", 38.0f, 9.8f, ' ', 2); Prop("office_mug_blue", 27.2f, 10.0f, ' ', 2);
        Prop("office_stapler", 38.8f, 5.2f, ' ', 2); Prop("office_papers_3", 35.4f, 5.0f, ' ', 2);

        // 남쪽 벽: 서류함·책장
        float[] fileX = { 26.0f, 26.75f, 27.5f, 28.3f, 29.05f };
        string[] files = { "office_file_1", "office_file_2", "office_file_3", "office_file_4", "office_file_5" };
        for (int i = 0; i < files.Length; i++) Prop(files[i], fileX[i], 0.5f + 1.47f / 2f, 'C', 1, 0.7f);
        Prop("office_bookshelf_4", 30.6f, 0.5f + 1.88f / 2f, 'C', 1, 0.7f);
        Prop("office_bookshelf_5", 34.4f, 0.5f + 1.97f / 2f, 'C', 1, 0.7f);
        Prop("office_cabinet_1", 35.8f, 0.5f + 1.94f / 2f, 'C', 1, 0.7f);
        float[] greenX = { 37.2f, 37.95f, 38.75f };
        string[] greens = { "office_file_green_1", "office_file_green_2", "office_file_green_3" };
        for (int i = 0; i < greens.Length; i++) Prop(greens[i], greenX[i], 0.5f + 1.91f / 2f, 'C', 1, 0.7f);
        Prop("office_safe", 39.0f, 3.2f, 'c', 1);
        Prop("office_trash", 33.9f, 3.6f, 'c', 1);
        Prop("office_box_3", 26.1f, 3.0f, 'c', 1); Prop("office_box_open", 26.0f, 7.6f, ' ', 1);
        Prop("office_papers_4", 31.4f, 7.0f, ' ', 0); Prop("office_paper_2", 33.4f, 8.6f, ' ', 0);
        Prop("terr_stain_4", 30.4f, 4.2f, ' ', 0);
        Prop("office_cabinet_low_1", 26.0f, 9.0f, 'c', 1);
        Prop("office_partition_2", 30.0f, 5.6f, 'c', 1, 0.5f);
        Prop("office_partition_4", 34.0f, 7.6f, 'c', 1, 0.5f);
        Prop("office_wallboard_1", 31.0f, 3.6f, ' ', 1);
        Prop("office_corkboard", 26.8f, 11.55f, ' ', 1);
        Prop("office_frame_1", 38.9f, 11.7f, ' ', 1); Prop("office_frame_2", 32.9f, 11.7f, ' ', 1);
        Prop("office_envelope", 29.2f, 9.9f, ' ', 2); Prop("office_sticky", 36.2f, 10.0f, ' ', 2);
        Prop("office_note_1", 37.2f, 5.6f, ' ', 2); Prop("office_tape", 28.2f, 5.2f, ' ', 2);
        Prop("office_book_1", 39.1f, 9.9f, ' ', 2); Prop("office_folder_1", 26.9f, 9.9f, ' ', 2);
    }

    // 플레이어 시작·적 스폰은 칸 문자, 조명·적 순찰은 JSON 목록
    static void Markers()
    {
        Mark(3.0f, 3.0f, 'P');
        Mark(10.25f, 25.25f, '1');
        Mark(36.25f, 25.75f, '2');
        Mark(33.25f, 6.75f, '3');
        Mark(17.75f, 21.25f, '4');

        // 창고: 차가운 흰빛 천장등 + 붉은 경고등 + 불붙은 잔해의 주황빛
        Light(4.5f, 23.0f, 7f, 0.85f, "#C8D8FF", false);
        Light(11.0f, 24.5f, 6f, 0.75f, "#C8D8FF", false);
        Light(13.2f, 29.0f, 3.5f, 1.0f, "#FF4A3A", true);
        Light(12.4f, 19.9f, 2.6f, 0.9f, "#FF9A40", true);

        // 복도: 일정 간격 천장등
        foreach (float x in new[] { 4.0f, 11.5f, 24.0f, 31.5f, 38.0f })
            Light(x, 14.0f, 5f, 0.65f, "#C8D8FF", false);

        // 엘리베이터 홀: 푸른빛
        Light(17.75f, 23.5f, 7f, 1.0f, "#8AB4FF", false);
        Light(17.8f, 27.0f, 2.5f, 0.8f, "#6FD0FF", true);
        Light(23.2f, 19.0f, 4f, 0.6f, "#A8C4FF", false);

        // 화장실
        Light(23.25f, 26.0f, 4f, 0.75f, "#E8F0FF", false);

        // 서버실: 천장등 + 랙·콘솔 불빛
        Light(29.0f, 24.0f, 6f, 0.75f, "#BFD4FF", false);
        Light(36.0f, 24.0f, 6f, 0.75f, "#BFD4FF", false);
        Light(28.0f, 27.4f, 2.6f, 0.9f, "#57FF9A", true);
        Light(37.0f, 27.4f, 2.6f, 0.9f, "#57FF9A", true);
        Light(32.5f, 27.0f, 2.4f, 0.8f, "#6FD0FF", true);
        Light(28.4f, 23.8f, 2.0f, 0.8f, "#5AA8FF", true);
        Light(36.6f, 23.8f, 2.0f, 0.8f, "#57FF9A", true);
        Light(28.4f, 19.8f, 2.0f, 0.8f, "#57FF9A", true);
        Light(36.6f, 19.8f, 2.0f, 0.8f, "#5AA8FF", true);

        // 휴게실: 따뜻한 빛
        Light(19.0f, 6.5f, 7f, 0.9f, "#FFC873", false);
        Light(23.0f, 2.0f, 3.5f, 0.6f, "#FFB46A", true);

        // 사무실: 차가운 흰빛 + 모니터 불빛
        Light(28.5f, 7.0f, 6f, 0.75f, "#D6E4FF", false);
        Light(36.5f, 7.0f, 6f, 0.75f, "#D6E4FF", false);
        Light(27.0f, 6.0f, 1.8f, 0.8f, "#6CFFB0", true);
        Light(35.0f, 6.0f, 1.8f, 0.8f, "#FF6060", true);
        Light(36.6f, 6.0f, 1.8f, 0.8f, "#6CFFB0", true);
    }

    // 적 순찰 경로(m). 모두 걸을 수 있는 칸이어야 한다(Validate가 검사)
    static readonly float[][][] Patrols =
    {
        new[] { new[] { 10.25f, 25.25f }, new[] { 3.25f, 21.25f }, new[] { 8.25f, 17.25f }, new[] { 12.25f, 22.75f } },
        new[] { new[] { 36.25f, 25.75f }, new[] { 32.25f, 21.25f }, new[] { 32.25f, 14.0f }, new[] { 38.25f, 14.0f } },
        new[] { new[] { 33.25f, 6.75f }, new[] { 32.25f, 2.75f }, new[] { 20.25f, 6.25f }, new[] { 31.25f, 8.75f } },
        new[] { new[] { 17.75f, 21.25f }, new[] { 17.75f, 14.0f }, new[] { 8.25f, 14.0f }, new[] { 23.25f, 14.0f } },
    };
    static readonly string[] EnemyTypes = { "guard", "guard", "guard", "rifleman" };

    // ---------- 배치 도구 ----------
    static int C(float m) { return (int)Math.Floor(m / Cell + 1e-4f); }

    // [x0, x1) x [y0, y1) 범위(m)의 칸을 채운다
    static void Fill(float x0, float y0, float x1, float y1, char c)
    {
        for (int y = C(y0); y < C(y1); y++)
            for (int x = C(x0); x < C(x1); x++)
                if (x >= 0 && y >= 0 && x < W && y < H) grid[x, y] = c;
    }

    static void FloorFill(float x0, float y0, float x1, float y1, char c)
    {
        for (int y = C(y0); y < C(y1); y++)
            for (int x = C(x0); x < C(x1); x++)
                if (x >= 0 && y >= 0 && x < W && y < H) floor[x, y] = c;
    }

    static void Mark(float x, float y, char c)
    {
        int cx = C(x), cy = C(y);
        if (grid[cx, cy] != '.') problems.Add("표시 '" + c + "' 자리가 비어 있지 않습니다: (" + x + ", " + y + ") = " + grid[cx, cy]);
        grid[cx, cy] = c;
    }

    static readonly Dictionary<string, Size> sizes = new Dictionary<string, Size>();

    static Size SpriteSize(string name)
    {
        Size size;
        if (sizes.TryGetValue(name, out size)) return size;
        string path = Path.Combine(deco, "deco_" + name + ".png");
        if (!File.Exists(path))
        {
            problems.Add("그림이 없습니다: deco_" + name);
            size = new Size(32, 32);
        }
        else using (var img = Image.FromFile(path)) size = img.Size;
        sizes[name] = size;
        return size;
    }

    static void Prop(string name, float x, float y, char block, int layer, float depth = 0f)
    {
        Size px = SpriteSize(name);
        float w = px.Width / (float)Ppu, h = px.Height / (float)Ppu;
        props.Add(string.Format(CultureInfo.InvariantCulture,
            "    {{ \"sprite\": \"deco_{0}\", \"pos\": [{1:0.###}, {2:0.###}], \"rot\": 0, \"layer\": {3} }}", name, x, y, layer));
        previewProps.Add(new object[] { name, x, y, layer });
        if (block == ' ') return;

        // 그림 아래쪽 depth(m)만 바닥을 차지한다. 가장자리 0.12m는 걸쳐도 막지 않는다
        float left = x - w / 2f + 0.12f, right = x + w / 2f - 0.12f;
        float bottom = y - h / 2f + 0.12f;
        float top = depth > 0f ? y - h / 2f + depth - 0.06f : y + h / 2f - 0.12f;
        int marked = 0;
        for (int cy = 0; cy < H; cy++)
        {
            for (int cx = 0; cx < W; cx++)
            {
                float mx = (cx + 0.5f) * Cell, my = (cy + 0.5f) * Cell;
                if (mx < left || mx > right || my < bottom || my > top) continue;
                if (grid[cx, cy] != '.' && grid[cx, cy] != 'c') continue;
                grid[cx, cy] = block;
                marked++;
            }
        }
        // 칸보다 가는 물건은 바닥 면적 가운데 칸 하나를 막는다
        if (marked == 0)
        {
            int cx = C(x), cy = C((bottom + top) / 2f);
            if (grid[cx, cy] == '.' || grid[cx, cy] == 'c') grid[cx, cy] = block;
            else problems.Add("막을 칸이 없습니다: " + name + " (" + x + ", " + y + ")");
        }
    }

    static void Light(float x, float y, float radius, float intensity, string color, bool noShadows)
    {
        lights.Add(string.Format(CultureInfo.InvariantCulture,
            "    {{ \"pos\": [{0:0.##}, {1:0.##}], \"radius_m\": {2:0.##}, \"intensity\": {3:0.##}, \"color\": \"{4}\", \"no_shadows\": {5} }}",
            x, y, radius, intensity, color, noShadows ? "true" : "false"));
        char c = grid[C(x), C(y)];
        if (c == '#') problems.Add("조명이 벽 안에 있습니다: (" + x + ", " + y + ")");
        if (floor[C(x), C(y)] == 'o') problems.Add("조명이 야외에 있습니다: (" + x + ", " + y + ")");
    }

    // ---------- 검사 ----------
    static bool Walkable(int x, int y)
    {
        if (x < 0 || y < 0 || x >= W || y >= H) return false;
        char c = grid[x, y];
        return c == '.' || c == 'D' || c == 'P' || (c >= '1' && c <= '9');
    }

    // 몸 반지름 0.4m가 지나가려면 걷는 칸 주변 1칸(0.5m)도 막혀 있지 않아야 하는 곳이 많다.
    // 단순하게 "가로·세로로 2칸 폭이 확보된 칸"만 이어서 걸어 본다
    static bool Roomy(int x, int y)
    {
        if (!Walkable(x, y)) return false;
        bool horizontal = Walkable(x - 1, y) || Walkable(x + 1, y);
        bool vertical = Walkable(x, y - 1) || Walkable(x, y + 1);
        return horizontal && vertical;
    }

    static void Validate()
    {
        int sx = C(3.0f), sy = C(3.0f);
        var seen = new bool[W, H];
        var queue = new Queue<int[]>();
        queue.Enqueue(new[] { sx, sy });
        seen[sx, sy] = true;
        int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };
        while (queue.Count > 0)
        {
            int[] p = queue.Dequeue();
            for (int k = 0; k < 4; k++)
            {
                int nx = p[0] + dx[k], ny = p[1] + dy[k];
                if (nx < 0 || ny < 0 || nx >= W || ny >= H || seen[nx, ny] || !Roomy(nx, ny)) continue;
                seen[nx, ny] = true;
                queue.Enqueue(new[] { nx, ny });
            }
        }

        for (int i = 0; i < Patrols.Length; i++)
            foreach (float[] point in Patrols[i])
            {
                int cx = C(point[0]), cy = C(point[1]);
                if (!Walkable(cx, cy)) problems.Add("적 " + (i + 1) + " 순찰 지점이 막힌 칸: " + point[0] + ", " + point[1]);
                else if (!seen[cx, cy]) problems.Add("적 " + (i + 1) + " 순찰 지점에 갈 수 없음: " + point[0] + ", " + point[1]);
            }

        // 모든 문 칸의 양옆(또는 위아래)에 갈 수 있어야 한다
        for (int y = 1; y < H - 1; y++)
            for (int x = 1; x < W - 1; x++)
            {
                if (grid[x, y] != 'D') continue;
                bool ok = (seen[x - 1, y] && seen[x + 1, y]) || (seen[x, y - 1] && seen[x, y + 1]);
                if (!ok) problems.Add("문 양쪽에 갈 수 없음: 칸 (" + x + ", " + y + ")");
            }
    }

    // ---------- 출력 ----------
    static string ToJson()
    {
        var sb = new StringBuilder();
        sb.Append("{\n");
        sb.Append("  \"version\": 2,\n");
        sb.Append("  \"id\": \"map_01\",\n");
        sb.Append("  \"name\": \"맵 1 - 소탕\",\n");
        sb.Append("  \"mission\": \"eliminate\",\n");
        sb.Append("  \"target_time_s\": 300,\n");
        sb.Append("  \"ambient\": { \"indoor\": 0.15, \"outdoor\": 0.08 },\n");
        sb.Append("  \"cell_m\": 0.5,\n");
        sb.Append("  \"rows\": [\n");
        for (int y = H - 1; y >= 0; y--)
        {
            var row = new StringBuilder();
            for (int x = 0; x < W; x++) row.Append(grid[x, y]);
            sb.Append("    \"").Append(row).Append(y > 0 ? "\",\n" : "\"\n");
        }
        sb.Append("  ],\n");
        sb.Append("  \"floor_rows\": [\n");
        for (int y = H - 1; y >= 0; y--)
        {
            var row = new StringBuilder();
            for (int x = 0; x < W; x++) row.Append(floor[x, y]);
            sb.Append("    \"").Append(row).Append(y > 0 ? "\",\n" : "\"\n");
        }
        sb.Append("  ],\n");
        sb.Append("  \"floor_sprites\": [\"floor_metal_dark\", \"floor_metal_plate\", \"floor_metal_blue\", \"floor_ground\"],\n");
        sb.Append("  \"lights\": [\n").Append(string.Join(",\n", lights.ToArray())).Append("\n  ],\n");
        sb.Append("  \"enemies\": [\n");
        for (int i = 0; i < Patrols.Length; i++)
        {
            var points = new List<string>();
            foreach (float[] p in Patrols[i])
                points.Add(string.Format(CultureInfo.InvariantCulture, "[{0:0.##},{1:0.##}]", p[0], p[1]));
            sb.AppendFormat("    {{ \"spawn\": {0}, \"type\": \"{1}\", \"patrol\": [{2}] }}{3}\n",
                i + 1, EnemyTypes[i], string.Join(",", points.ToArray()), i < Patrols.Length - 1 ? "," : "");
        }
        sb.Append("  ],\n");
        sb.Append("  \"props\": [\n").Append(string.Join(",\n", props.ToArray())).Append("\n  ]\n");
        sb.Append("}\n");
        return sb.ToString();
    }

    // 바닥·벽·소품을 합성한 미리보기(1칸 = 16px). 엄폐 칸은 붉은(C)·노란(c) 테두리로 표시한다
    static void Preview(string path)
    {
        int cp = (int)(Ppu * Cell);
        var bmp = new Bitmap(W * cp, H * cp);
        var floorImages = new Dictionary<char, Bitmap>
        {
            { 'w', new Bitmap(Path.Combine(tiles, "floor_metal_dark.png")) },
            { 't', new Bitmap(Path.Combine(tiles, "floor_metal_plate.png")) },
            { 'k', new Bitmap(Path.Combine(tiles, "floor_metal_blue.png")) },
            { 'o', new Bitmap(Path.Combine(tiles, "floor_ground.png")) },
        };
        using (var g = Graphics.FromImage(bmp))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int px = x * cp, py = (H - 1 - y) * cp;
                    char c = grid[x, y];
                    if (c == '#') { g.FillRectangle(new SolidBrush(Color.FromArgb(14, 14, 18)), px, py, cp, cp); continue; }
                    Bitmap f = floorImages[floor[x, y]];
                    int n = f.Width / cp;
                    int sx = (x % n) * cp, sy = f.Height - cp - (y % n) * cp;
                    g.DrawImage(f, new Rectangle(px, py, cp, cp), new Rectangle(sx, sy, cp, cp), GraphicsUnit.Pixel);
                    if (c == 'W') g.FillRectangle(new SolidBrush(Color.FromArgb(160, 120, 180, 220)), px, py, cp, cp);
                    if (c == 'D') g.FillRectangle(new SolidBrush(Color.FromArgb(200, 201, 162, 74)), px, py, cp, cp);
                }
            foreach (int layer in new[] { 0, 1, 2 })
                foreach (object[] p in previewProps)
                {
                    if ((int)p[3] != layer) continue;
                    string file = Path.Combine(deco, "deco_" + p[0] + ".png");
                    if (!File.Exists(file)) continue;
                    using (var img = new Bitmap(file))
                    {
                        float cx = (float)p[1] * Ppu, cy = (30f - (float)p[2]) * Ppu;
                        g.DrawImage(img, (int)(cx - img.Width / 2f), (int)(cy - img.Height / 2f), img.Width, img.Height);
                    }
                }
            var high = new Pen(Color.FromArgb(200, 255, 60, 60));
            var low = new Pen(Color.FromArgb(200, 255, 220, 60));
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    char c = grid[x, y];
                    int px = x * cp, py = (H - 1 - y) * cp;
                    if (c == 'C') g.DrawRectangle(high, px + 1, py + 1, cp - 3, cp - 3);
                    else if (c == 'c') g.DrawRectangle(low, px + 1, py + 1, cp - 3, cp - 3);
                    else if (c == 'P' || (c >= '1' && c <= '9'))
                    {
                        g.FillEllipse(c == 'P' ? Brushes.White : Brushes.OrangeRed, px - 4, py - 4, cp + 8, cp + 8);
                        g.DrawString(c.ToString(), new Font("Arial", 9, FontStyle.Bold), Brushes.Black, px + 2, py);
                    }
                }
        }
        bmp.Save(path, ImageFormat.Png);
    }
}
