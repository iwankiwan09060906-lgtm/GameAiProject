// AssetSource/newasset의 시트를 잘라 맵 장식(deco_*)과 바닥(floor_*) 그림을 만든다.
// 실행: AssetSource/build_deco.ps1 (Unity 없이 .NET만 사용). build_assets와 따로 두어 기존 그림을 다시 만들지 않는다.
//
// 시트(파일 이름과 내용이 서로 어긋나 있어 내용 기준으로 적는다):
//   scifi_facility.png             금속 시설 소품(서버·콘솔·상자·드럼통·엘리베이터·화장실). 바닥 한 칸이 약 26.7px라 1.2배 키운다
//   Office_Icons_32x32.png         사무실 가구(책상·의자·캐비닛·화분·정수기). 32px = 1m 그대로
//   Office_Furniture_32x32.png     폐허(부서진 벽·잔해·경고판). 32px = 1m 그대로
//   Office_Walls_32x32.png         지면(흙·콘크리트·균열·웅덩이)
//   Office_Terrain_32x32.png       작은 물건 아이콘 7x6칸
// 나머지 시트(_Shadow, _WhiteOL)는 같은 그림의 그림자·외곽선 판이라 쓰지 않는다.
// 잘라 낼 영역은 덩어리 경계를 자동으로 찾은 값이고, 영역 안의 투명한 가장자리는 잘라 낸다(Trim).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

public static class DecoBuild
{
    static string src, art;

    // 시설 시트를 32px 격자에 맞추는 배율(26.7px → 32px)
    const double ScifiScale = 1.2;

    public static void Run(string srcDir, string projectDir)
    {
        src = Path.Combine(srcDir, "newasset");
        art = Path.Combine(projectDir, "Assets/_Project/Art");
        BuildFloors();
        BuildScifi();
        BuildOffice();
        BuildRuins();
        BuildTerrain();
        BuildItems();
    }

    // ---------- 바닥 ----------
    // 바닥 그림은 1m(32px) 또는 2m(64px) 무늬. 맵 렌더러가 0.5m 칸마다 해당 조각을 잘라 쓴다
    static void BuildFloors()
    {
        var scifi = Load("scifi_facility.png");
        var terrain = Load("Office_Walls_32x32.png");
        Save(Resize(Crop(scifi, 170, 4, 26, 27), 32, 32), "tiles/floor_metal_dark.png");
        Save(Resize(Crop(scifi, 90, 341, 54, 47), 64, 64), "tiles/floor_metal_plate.png");
        Save(Resize(Crop(scifi, 169, 341, 55, 47), 64, 64), "tiles/floor_metal_blue.png");
        Save(Crop(terrain, 96, 192, 64, 64), "tiles/floor_ground.png");
    }

    // ---------- 시설 소품 ----------
    static void BuildScifi()
    {
        var s = Opaque(Load("scifi_facility.png"));
        var parts = new Dictionary<string, int[]>
        {
            { "ladder", new[] { 46, 3, 22, 86 } },
            { "vent_1", new[] { 90, 25, 27, 27 } },
            { "vent_2", new[] { 118, 25, 27, 27 } },
            { "vent_3", new[] { 90, 53, 27, 27 } },
            { "vent_4", new[] { 118, 53, 27, 27 } },
            { "conveyor", new[] { 90, 104, 28, 106 } },
            { "floor_panel", new[] { 170, 89, 106, 39 } },
            { "hazard_pad", new[] { 168, 128, 109, 84 } },
            { "hatch_plate", new[] { 301, 2, 29, 50 } },
            { "hatch_window", new[] { 353, 2, 29, 50 } },
            { "window_small", new[] { 407, 23, 27, 29 } },
            { "airlock", new[] { 458, 4, 79, 80 } },
            { "window_long", new[] { 353, 75, 82, 29 } },
            { "hatch_arch", new[] { 301, 76, 29, 29 } },
            { "elevator", new[] { 301, 115, 54, 54 } },
            { "elevator_open", new[] { 380, 115, 55, 54 } },
            { "console_blue", new[] { 459, 209, 81, 52 } },
            { "console_green", new[] { 541, 209, 80, 52 } },
            { "terminal", new[] { 327, 177, 28, 23 } },
            { "canister", new[] { 361, 188, 11, 16 } },
            { "scrap", new[] { 387, 192, 13, 11 } },
            { "note", new[] { 414, 192, 12, 12 } },
            { "medkit", new[] { 440, 195, 12, 11 } },
            { "helmet", new[] { 328, 207, 28, 32 } },
            { "box", new[] { 356, 207, 28, 32 } },
            { "drone_parts", new[] { 380, 212, 45, 49 } },
            { "barrel_bio", new[] { 426, 210, 21, 52 } },
            { "crate_big", new[] { 203, 221, 45, 42 } },
            { "barrel_white", new[] { 247, 253, 23, 34 } },
            { "crate_small", new[] { 261, 229, 37, 26 } },
            { "cargo_pad", new[] { 90, 236, 81, 80 } },
            { "barrel_stack", new[] { 354, 239, 27, 51 } },
            { "gasmask", new[] { 332, 241, 19, 14 } },
            { "barrel_red", new[] { 384, 262, 20, 27 } },
            { "robot", new[] { 407, 263, 30, 28 } },
            { "barrier", new[] { 437, 263, 27, 28 } },
            { "keycard", new[] { 464, 263, 18, 28 } },
            { "debris", new[] { 331, 266, 20, 20 } },
            { "fire", new[] { 516, 266, 20, 22 } },
            { "toolbox", new[] { 484, 267, 30, 22 } },
            { "tablet", new[] { 544, 269, 17, 17 } },
            { "folder", new[] { 598, 269, 14, 12 } },
            { "badge", new[] { 570, 271, 16, 10 } },
            { "cables_1", new[] { 199, 277, 48, 31 } },
            { "cables_2", new[] { 260, 277, 39, 29 } },
            { "shower", new[] { 538, 289, 27, 51 } },
            { "sink", new[] { 459, 294, 31, 36 } },
            { "toilet", new[] { 505, 294, 28, 46 } },
            { "bin_bio", new[] { 596, 304, 22, 35 } },
            { "bin_bio_2", new[] { 569, 306, 22, 33 } },
            { "frame_big", new[] { 327, 315, 80, 73 } },
            { "frame_mid", new[] { 253, 336, 44, 34 } },
            { "rail_1", new[] { 253, 325, 44, 8 } },
            { "rail_2", new[] { 253, 374, 44, 14 } },
            { "frame_small", new[] { 428, 337, 37, 36 } },
            { "pipe", new[] { 485, 347, 81, 15 } },
            { "extinguisher", new[] { 520, 369, 11, 19 } },
            { "cone", new[] { 493, 374, 11, 14 } },
            { "bolt", new[] { 468, 377, 10, 10 } },
            { "scrap_2", new[] { 545, 377, 14, 10 } },
        };
        foreach (var p in parts)
            SaveScifi(s, p.Key, p.Value[0], p.Value[1], p.Value[2], p.Value[3]);

        // 서버 랙 6대는 한 덩어리로 붙어 있어 폭 26.83px씩 나눈다
        for (int k = 0; k < 6; k++)
        {
            int x = 459 + (int)Math.Round(k * 26.83);
            int next = 459 + (int)Math.Round((k + 1) * 26.83);
            SaveScifi(s, "server_" + (k + 1), x, 150, next - x, 58);
        }
    }

    static void SaveScifi(Bitmap sheet, string name, int x, int y, int w, int h)
    {
        Save(ScaleBy(Trim(Crop(sheet, x, y, w, h)), ScifiScale), "deco/deco_" + name + ".png");
    }

    // ---------- 사무실 가구 ----------
    static void BuildOffice()
    {
        var o = Load("Office_Icons_32x32.png");
        var parts = new List<object[]>
        {
            new object[] { "plant_tall", 387, 0, 25, 64 }, new object[] { "clock", 327, 7, 18, 19 },
            new object[] { "binders_1", 203, 8, 11, 18 }, new object[] { "binders_2", 235, 8, 11, 18 },
            new object[] { "book_1", 107, 9, 10, 15 }, new object[] { "book_2", 140, 9, 10, 15 }, new object[] { "book_3", 171, 9, 10, 15 },
            new object[] { "folder_1", 6, 11, 19, 14 }, new object[] { "folder_2", 38, 11, 19, 14 }, new object[] { "folder_3", 70, 11, 19, 14 },
            new object[] { "desk_side_1", 612, 11, 24, 51 }, new object[] { "desk_side_2", 612, 75, 24, 51 },
            new object[] { "plant_pole", 361, 14, 15, 39 },
            new object[] { "partition_1", 292, 17, 27, 74 }, new object[] { "partition_2", 272, 18, 15, 73 },
            new object[] { "desk_1", 424, 24, 48, 39 }, new object[] { "desk_2", 488, 24, 48, 39 }, new object[] { "desk_3", 553, 26, 48, 37 },
            new object[] { "desk_4", 424, 87, 48, 40 }, new object[] { "desk_5", 488, 87, 48, 40 }, new object[] { "desk_6", 552, 87, 48, 40 },
            new object[] { "cabinet_1", 10, 32, 44, 62 }, new object[] { "cabinet_2", 74, 32, 44, 64 },
            new object[] { "cabinet_3", 138, 32, 44, 64 }, new object[] { "cabinet_4", 202, 32, 44, 62 },
            new object[] { "plant_dry", 392, 67, 18, 59 }, new object[] { "plant_small", 358, 74, 23, 46 },
            new object[] { "cup", 332, 79, 8, 16 }, new object[] { "pot", 331, 106, 11, 15 },
            new object[] { "locker_green", 261, 103, 26, 57 }, new object[] { "partition_3", 304, 103, 15, 57 },
            new object[] { "counter_1", 10, 118, 42, 40 }, new object[] { "counter_2", 74, 118, 42, 40 },
            new object[] { "counter_3", 139, 118, 42, 40 }, new object[] { "counter_4", 203, 118, 42, 40 },
            new object[] { "file_tall_1", 485, 131, 22, 61 }, new object[] { "file_tall_2", 517, 131, 22, 61 },
            new object[] { "file_tall_3", 549, 131, 22, 61 }, new object[] { "file_tall_4", 579, 131, 29, 61 },
            new object[] { "file_tall_5", 622, 131, 18, 61 },
            new object[] { "file_1", 325, 145, 22, 47 }, new object[] { "file_2", 357, 145, 22, 47 },
            new object[] { "file_3", 389, 145, 22, 47 }, new object[] { "file_4", 419, 145, 29, 47 },
            new object[] { "file_5", 462, 145, 18, 47 },
            new object[] { "bookshelf_1", 106, 161, 44, 62 }, new object[] { "bookshelf_2", 170, 161, 44, 62 },
            new object[] { "bookshelf_3", 234, 161, 44, 62 },
            new object[] { "note_1", 74, 168, 12, 17 }, new object[] { "note_2", 74, 200, 12, 17 },
            new object[] { "partition_4", 304, 178, 15, 74 },
            new object[] { "pc_tower", 39, 181, 11, 29 }, new object[] { "pc_old", 13, 183, 22, 35 },
            new object[] { "bench", 327, 193, 18, 63 },
            new object[] { "file_green_1", 485, 195, 22, 61 }, new object[] { "file_green_2", 517, 195, 22, 61 },
            new object[] { "file_green_3", 549, 195, 22, 61 }, new object[] { "file_green_4", 579, 195, 29, 61 },
            new object[] { "file_green_5", 622, 195, 18, 61 },
            new object[] { "table_1", 355, 215, 58, 39 }, new object[] { "table_2", 419, 215, 58, 40 },
            new object[] { "printer_tall", 227, 224, 23, 46 }, new object[] { "copier", 199, 227, 23, 43 },
            new object[] { "paper_stack", 266, 233, 11, 15 },
            new object[] { "tower_1", 39, 245, 11, 29 }, new object[] { "tower_2", 103, 245, 11, 29 }, new object[] { "tower_3", 167, 245, 11, 29 },
            new object[] { "monitor_red", 11, 247, 24, 35 }, new object[] { "monitor_green", 75, 247, 24, 35 },
            new object[] { "monitor_dark", 139, 247, 24, 35 },
            new object[] { "paper_2", 274, 264, 11, 15 }, new object[] { "phone", 332, 266, 8, 14 }, new object[] { "mug", 364, 266, 8, 14 },
            new object[] { "envelope", 552, 266, 17, 14 }, new object[] { "sticky", 298, 267, 11, 12 },
            new object[] { "chair_1", 389, 276, 22, 43 }, new object[] { "chair_2", 421, 283, 22, 36 },
            new object[] { "chair_3", 454, 278, 18, 40 }, new object[] { "chair_4", 488, 278, 18, 40 },
            new object[] { "chair_5", 520, 276, 18, 38 }, new object[] { "chair_6", 552, 288, 18, 31 },
            new object[] { "chair_7", 583, 279, 17, 35 }, new object[] { "chair_8", 618, 279, 17, 35 },
            new object[] { "chair_9", 519, 340, 18, 38 }, new object[] { "chair_10", 551, 352, 18, 31 },
            new object[] { "chair_11", 582, 343, 17, 35 }, new object[] { "chair_12", 617, 343, 17, 35 },
            new object[] { "bin_white", 66, 288, 13, 30 }, new object[] { "table_small", 13, 291, 38, 27 },
            new object[] { "papers_1", 324, 293, 22, 19 }, new object[] { "papers_2", 290, 294, 14, 22 },
            new object[] { "frame_1", 202, 296, 12, 16 }, new object[] { "frame_2", 234, 296, 12, 16 },
            new object[] { "tower_white", 140, 299, 11, 29 }, new object[] { "printer", 154, 310, 26, 21 },
            new object[] { "seats_1", 200, 322, 17, 61 }, new object[] { "seats_2", 231, 322, 17, 61 },
            new object[] { "water_1", 488, 324, 16, 27 }, new object[] { "water_2", 488, 356, 16, 27 },
            new object[] { "box_open", 552, 324, 17, 20 }, new object[] { "stool", 38, 326, 18, 23 },
            new object[] { "papers_3", 362, 327, 15, 20 }, new object[] { "papers_4", 329, 329, 14, 15 },
            new object[] { "cooler_1", 391, 330, 18, 53 }, new object[] { "cooler_2", 423, 330, 18, 53 },
            new object[] { "cooler_3", 455, 330, 18, 53 },
            new object[] { "armchair_1", 5, 331, 22, 36 }, new object[] { "sofa_1", 67, 331, 58, 36 },
            new object[] { "armchair_2", 37, 353, 22, 29 }, new object[] { "sofa_2", 131, 354, 58, 29 },
            new object[] { "seat_1", 263, 342, 17, 37 }, new object[] { "seat_2", 296, 342, 17, 37 },
            new object[] { "papers_5", 329, 362, 14, 15 }, new object[] { "note_3", 364, 366, 11, 12 },
            new object[] { "keyboard_1", 71, 384, 16, 13 }, new object[] { "keyboard_2", 105, 385, 16, 12 },
            new object[] { "corkboard", 362, 385, 44, 30 },
            new object[] { "ldesk_1", 450, 385, 60, 62 }, new object[] { "ldesk_2", 514, 385, 60, 62 },
            new object[] { "ldesk_3", 578, 385, 60, 62 },
            new object[] { "safe", 258, 386, 28, 24 }, new object[] { "cabinet_low_1", 290, 386, 28, 30 },
            new object[] { "cabinet_low_2", 322, 386, 28, 30 },
            new object[] { "trash", 136, 388, 16, 27 }, new object[] { "trash_full", 168, 388, 16, 27 },
            new object[] { "phone_desk", 198, 391, 23, 24 }, new object[] { "calc_1", 233, 391, 15, 20 },
            new object[] { "mug_blue", 426, 394, 11, 14 }, new object[] { "stapler", 42, 400, 14, 16 },
            new object[] { "tape", 8, 401, 14, 15 },
            new object[] { "box_1", 326, 418, 18, 28 }, new object[] { "box_2", 359, 419, 16, 27 },
            new object[] { "box_3", 388, 421, 24, 24 }, new object[] { "box_4", 422, 422, 22, 23 },
            new object[] { "board_1", 64, 420, 31, 25 }, new object[] { "board_2", 96, 420, 31, 25 },
            new object[] { "board_3", 128, 420, 32, 25 },
            new object[] { "calc_2", 201, 423, 15, 20 }, new object[] { "calc_3", 233, 423, 15, 20 },
            new object[] { "cup_2", 171, 434, 11, 12 },
            new object[] { "partition_5", 272, 436, 15, 74 }, new object[] { "door_dark", 292, 436, 27, 75 },
            new object[] { "bookshelf_4", 10, 449, 44, 60 }, new object[] { "bookshelf_5", 74, 449, 44, 63 },
            new object[] { "bookshelf_6", 138, 449, 44, 63 }, new object[] { "cabinet_dark", 202, 449, 44, 60 },
            new object[] { "cabinet_corner", 462, 454, 43, 52 },
            new object[] { "locker_1", 322, 458, 28, 48 }, new object[] { "locker_2", 354, 458, 28, 52 },
            new object[] { "locker_3", 386, 458, 28, 52 }, new object[] { "locker_4", 418, 458, 28, 48 },
            new object[] { "wallboard_1", 512, 463, 32, 49 }, new object[] { "wallboard_2", 544, 463, 32, 49 },
            new object[] { "wallboard_3", 576, 463, 32, 49 }, new object[] { "wallboard_4", 608, 463, 32, 49 },
        };
        foreach (var p in parts)
            Save(Trim(Crop(o, (int)p[1], (int)p[2], (int)p[3], (int)p[4])), "deco/deco_office_" + p[0] + ".png");
    }

    // ---------- 폐허 ----------
    static void BuildRuins()
    {
        var r = Load("Office_Furniture_32x32.png");
        var parts = new List<object[]>
        {
            new object[] { "sign_noentry", 69, 2, 22, 28 }, new object[] { "sign_skull", 327, 37, 19, 25 },
            new object[] { "rad_1", 332, 5, 11, 10 }, new object[] { "rad_2", 323, 18, 10, 12 }, new object[] { "rad_3", 338, 20, 10, 11 },
            new object[] { "post", 352, 5, 30, 58 },
            new object[] { "scorch_1", 227, 8, 25, 18 }, new object[] { "scorch_2", 98, 9, 61, 18 },
            new object[] { "wall_pillar", 224, 23, 102, 73 }, new object[] { "wall_piece_1", 122, 26, 76, 70 },
            new object[] { "wall_piece_2", 26, 142, 38, 50 }, new object[] { "wall_long", 224, 120, 126, 72 },
            new object[] { "pillar_broken", 353, 71, 29, 53 },
            new object[] { "rubble_1", 320, 98, 31, 30 }, new object[] { "rubble_2", 352, 130, 31, 26 },
            new object[] { "rubble_3", 355, 164, 26, 22 }, new object[] { "rubble_4", 322, 166, 30, 23 },
            new object[] { "tile_1", 359, 199, 16, 17 }, new object[] { "tile_2", 327, 202, 16, 17 },
            new object[] { "wall_huge", 26, 212, 268, 78 }, new object[] { "tiles_pile", 321, 225, 62, 61 },
            new object[] { "rubble_long", 102, 290, 122, 29 }, new object[] { "rubble_5", 224, 291, 63, 29 },
            new object[] { "rubble_6", 35, 294, 61, 24 },
            new object[] { "tile_3", 324, 293, 22, 23 }, new object[] { "tile_4", 361, 296, 16, 17 },
            new object[] { "tile_5", 325, 324, 24, 26 }, new object[] { "tile_6", 360, 331, 22, 19 },
            new object[] { "tiles_2", 321, 353, 30, 31 }, new object[] { "tiles_3", 354, 355, 29, 27 },
            new object[] { "tiles_4", 321, 385, 55, 31 },
            new object[] { "wall_window", 26, 323, 69, 93 }, new object[] { "rubble_column", 98, 326, 28, 82 },
            new object[] { "wall_window_wide", 136, 333, 158, 83 },
        };
        foreach (var p in parts)
            Save(Trim(Crop(r, (int)p[1], (int)p[2], (int)p[3], (int)p[4])), "deco/deco_ruin_" + p[0] + ".png");
    }

    // ---------- 지면 장식 ----------
    static void BuildTerrain()
    {
        var t = Load("Office_Walls_32x32.png");
        var parts = new List<object[]>
        {
            new object[] { "plate_dirt", 16, 16, 64, 64 }, new object[] { "plate_gray", 16, 144, 64, 64 },
            new object[] { "slab_cracked", 208, 16, 64, 64 }, new object[] { "slab", 208, 144, 64, 64 },
            new object[] { "dirt_1", 4, 98, 21, 13 }, new object[] { "dirt_2", 35, 100, 26, 24 },
            new object[] { "dirt_3", 11, 114, 17, 11 }, new object[] { "dirt_4", 73, 109, 14, 7 },
            new object[] { "stain_1", 4, 226, 21, 13 }, new object[] { "stain_2", 35, 228, 26, 24 },
            new object[] { "stain_3", 11, 242, 17, 11 }, new object[] { "stain_4", 73, 237, 14, 7 },
            new object[] { "leaves", 296, 101, 15, 20 }, new object[] { "gold_pile", 323, 101, 27, 19 },
            new object[] { "puddle", 227, 231, 26, 21 }, new object[] { "weed", 203, 105, 9, 13 },
            new object[] { "rock_1", 201, 232, 15, 13 }, new object[] { "rock_2", 260, 227, 13, 9 },
            new object[] { "rock_3", 260, 241, 18, 11 }, new object[] { "rock_4", 275, 232, 10, 8 },
        };
        foreach (var p in parts)
            Save(Trim(Crop(t, (int)p[1], (int)p[2], (int)p[3], (int)p[4])), "deco/deco_terr_" + p[0] + ".png");
    }

    // ---------- 작은 물건 아이콘(7열 x 6행, 32px 칸) ----------
    static void BuildItems()
    {
        var icons = Load("Office_Terrain_32x32.png");
        for (int row = 0; row < 6; row++)
            for (int col = 0; col < 7; col++)
                Save(Trim(Crop(icons, col * 32, row * 32, 32, 32)), "deco/deco_item_" + (row * 7 + col) + ".png");
    }

    // ---------- 도구 ----------
    static Bitmap Load(string rel)
    {
        using (var b = new Bitmap(Path.Combine(src, rel)))
            return Crop(b, 0, 0, b.Width, b.Height);
    }

    static void Save(Bitmap b, string rel)
    {
        string path = Path.Combine(art, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        b.Save(path, ImageFormat.Png);
    }

    static Bitmap Crop(Bitmap b, int x, int y, int w, int h)
    {
        var r = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                int sx = x + i, sy = y + j;
                if (sx < b.Width && sy < b.Height) r.SetPixel(i, j, b.GetPixel(sx, sy));
            }
        return r;
    }

    // 투명한 가장자리를 잘라 낸다. 다 투명하면 1x1
    static Bitmap Trim(Bitmap b)
    {
        int minX = b.Width, minY = b.Height, maxX = -1, maxY = -1;
        for (int y = 0; y < b.Height; y++)
            for (int x = 0; x < b.Width; x++)
            {
                if (b.GetPixel(x, y).A == 0) continue;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        if (maxX < 0) return new Bitmap(1, 1, PixelFormat.Format32bppArgb);
        return Crop(b, minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    // 최근접 배율 조정(픽셀이 번지지 않는다)
    static Bitmap ScaleBy(Bitmap b, double k)
    {
        return Resize(b, Math.Max(1, (int)Math.Round(b.Width * k)), Math.Max(1, (int)Math.Round(b.Height * k)));
    }

    static Bitmap Resize(Bitmap b, int w, int h)
    {
        var r = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
                r.SetPixel(i, j, b.GetPixel(i * b.Width / w, j * b.Height / h));
        return r;
    }

    // 배경 제거 미리보기라 가장자리 알파가 흐릿하다. 픽셀아트답게 반투명을 0 또는 255로 나눈다
    static Bitmap Opaque(Bitmap b)
    {
        var r = new Bitmap(b.Width, b.Height, PixelFormat.Format32bppArgb);
        for (int y = 0; y < b.Height; y++)
            for (int x = 0; x < b.Width; x++)
            {
                Color c = b.GetPixel(x, y);
                r.SetPixel(x, y, c.A < 96 ? Color.FromArgb(0, 0, 0, 0) : Color.FromArgb(255, c.R, c.G, c.B));
            }
        return r;
    }
}
