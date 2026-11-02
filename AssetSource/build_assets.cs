// AssetSource 원본을 잘라 Assets/_Project 아래의 게임용 파일로 만든다.
// 실행: AssetSource/build_assets.ps1 (Unity 없이 .NET만 사용)
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

public static class AssetBuild
{
    static string src, art, sfx;

    static readonly Color WindowBar = Hex("#E8E4DA");
    static readonly Color DoorGold = Hex("#C9A24A");
    static readonly Color Carpet = Hex("#2F7C7A");
    static readonly Color Blood = Hex("#6E0F0F");
    static readonly Color ArmorBlue = Hex("#6EC6E6");
    static readonly Color Suspect = Hex("#F2C230");
    static readonly Color HudRed = Hex("#C8323C");

    public static void Run(string srcDir, string projectDir)
    {
        src = srcDir;
        art = Path.Combine(projectDir, "Assets/_Project/Art");
        sfx = Path.Combine(projectDir, "Assets/_Project/Audio/sfx");
        BuildTiles();
        BuildProps();
        BuildChars();
        BuildFx();
        BuildObjAndUi();
        BuildDrawn();
        BuildStances();
        BuildPanels();
        Shot("X_39P.wav", "sfx_shot_pistol.wav");
        Shot("D_32P.wav", "sfx_shot_rifle.wav");
    }

    // ---------- 타일 ----------
    static void BuildTiles()
    {
        var mh = Load("modern_houses/tiletest.png");
        var grass = Load("grass_tiles/grass.png");
        var tiny = Load("tiny_top_down/tiny_top_down_32.png");
        var dyru = Load("dyru/shooter_up.png");

        // 16px 타일 2배 확대, 채도 낮춤(ASSET_DIRECTION 2.0)
        Save(Desat(Scale(Crop(mh, 96, 32, 16, 16), 2), 0.15), "tiles/tile_floor_wood.png");
        Save(Desat(Scale(Crop(mh, 208, 112, 16, 16), 2), 0.85), "tiles/tile_floor_tile.png");
        Save(Tint(Scale(Crop(mh, 96, 90, 16, 16), 2), Carpet), "tiles/tile_floor_carpet.png");
        Save(Crop(grass, 0, 0, 32, 32), "tiles/tile_floor_outdoor.png");

        // 벽은 거의 검정. 방 쪽 1px 선은 맵 렌더러가 그린다(DECISIONS)
        var wall = Map(Crop(tiny, 32, 160, 32, 32), delegate(Color c)
        {
            return Color.FromArgb(c.A, (int)(c.R * 0.16), (int)(c.G * 0.16), (int)(c.B * 0.18));
        });
        Save(wall, "tiles/tile_wall.png");

        var window = new Bitmap(wall);
        for (int y = 13; y <= 18; y++)
            for (int x = 2; x <= 29; x++)
                window.SetPixel(x, y, WindowBar);
        Save(window, "tiles/tile_window.png");

        var door = Tint(Crop(dyru, 176, 140, 32, 8), DoorGold);
        Save(door, "tiles/door_closed.png");
        var open = new Bitmap(door);
        open.RotateFlip(RotateFlipType.Rotate90FlipNone);
        Save(open, "tiles/door_open.png");
    }

    // ---------- 소품 ----------
    static void BuildProps()
    {
        var mh = Load("modern_houses/tiletest.png");
        var dyru = Load("dyru/shooter_up.png");
        var trees = Load("trees_bushes/trees-and-bushes.png");
        var grass = Load("grass_tiles/grass-V2.png");

        string[] low = {
            "sofa_down_1", "sofa_down_2", "sofa_left_1", "sofa_left_2",
            "sofa_right_1", "sofa_right_2", "sofa_up_1", "sofa_up_2",
            "bed_wooden_E", "bed_wooden_N", "bed_wooden_S", "bed_wooden_W",
            "bath_E", "bath_N", "bath_S", "bath_W", "desk1"
        };
        foreach (var n in low)
            Save(Load("crimelike/" + n + ".png"), "props/cover_low_" + n.ToLowerInvariant() + ".png");
        Save(Pad(Crop(mh, 304, 69, 32, 26), 32, 32), "props/cover_low_table_white.png");
        Save(Pad(Crop(mh, 304, 112, 32, 24), 32, 32), "props/cover_low_table_red.png");
        Save(Scale(Crop(dyru, 176, 32, 16, 16), 2), "props/cover_low_crate.png");

        string[] high = { "counter_metal", "counter_wooden_red", "counter_wooden_red2", "oven1", "hifi" };
        foreach (var n in high)
            Save(Load("crimelike/" + n + ".png"), "props/cover_high_" + n + ".png");
        Save(Pad(Crop(mh, 320, 32, 32, 28), 32, 32), "props/cover_high_cabinet.png");
        Save(Pad(Crop(mh, 352, 32, 16, 28), 32, 32), "props/cover_high_fridge.png");
        Save(Scale(Crop(dyru, 192, 32, 16, 16), 2), "props/cover_high_crate.png");

        // Trees & Bushes는 잔디가 배경에 구워져 있어 바깥 잔디만 지운다
        var keyed = KeyOutGrass(trees, Crop(trees, 0, 124, 22, 36));
        Save(Pad(Crop(keyed, 240, 116, 32, 32), 32, 32), "props/foliage_bush.png");
        Save(Pad(Crop(keyed, 258, 0, 30, 32), 32, 32), "props/foliage_pine.png");
        Save(Crop(grass, 224, 32, 32, 32), "props/foliage_plants.png");
        Save(Crop(grass, 128, 64, 32, 32), "props/foliage_leaves.png");
        Save(Crop(grass, 160, 64, 32, 32), "props/foliage_reeds.png");
        Save(Crop(grass, 128, 96, 32, 32), "props/foliage_fir.png");
        Save(Crop(grass, 160, 96, 32, 32), "props/foliage_fern.png");
    }

    // ---------- 캐릭터 ----------
    // 시트 열: 0 걷기(손 없음) / 1 총 들기 / 2 사격 / 3 근접, 행: 캐릭터 5명. 모두 오른쪽(+X)을 본다.
    static void BuildChars()
    {
        var dyru = Load("dyru/shooter_up.png");
        Func<Color, Color> player = delegate(Color c)
        {
            double h, s, v; ToHsv(c, out h, out s, out v);
            if (s < 0.25 && v > 0.15) v = Math.Min(1.0, 0.30 + v * 1.45);
            return FromHsv(c.A, h, s, v);
        };
        Func<Color, Color> guard = delegate(Color c)
        {
            double h, s, v; ToHsv(c, out h, out s, out v);
            if (s > 0.45 && h > 5 && h < 40) { h = 10; s *= 0.62; v *= 0.62; }
            return FromHsv(c.A, h, s, v);
        };
        Func<Color, Color> rifleman = delegate(Color c)
        {
            double h, s, v; ToHsv(c, out h, out s, out v);
            return FromHsv(c.A, h, s * 0.8, v * 0.85);
        };

        Sheets(dyru, 3, "player", player, true);
        Sheets(dyru, 1, "guard", guard, false);
        Sheets(dyru, 0, "rifleman", rifleman, false);

        Save(Crop(dyru, 128, 0, 16, 16), "chars/held_pistol.png");
        Save(Crop(dyru, 128, 16, 16, 16), "chars/held_silenced_pistol.png");
        Save(Crop(dyru, 144, 16, 16, 16), "chars/held_rifle.png");

        Save(Corpse(dyru, 1, guard, 0), "chars/corpse_guard.png");
        Save(Corpse(dyru, 0, rifleman, 2), "chars/corpse_rifleman.png");
    }

    static void Sheets(Bitmap sheet, int row, string name, Func<Color, Color> paint, bool melee)
    {
        Func<int, Bitmap> frame = delegate(int col) { return Map(Crop(sheet, col * 32, row * 32, 32, 32), paint); };
        Save(frame(1), "chars/" + name + "_idle.png");
        Save(Strip(frame(1), frame(2)), "chars/" + name + "_walk.png");
        Save(frame(2), "chars/" + name + "_shoot.png");
        if (melee) Save(frame(3), "chars/" + name + "_melee.png");
    }

    static Bitmap Corpse(Bitmap sheet, int row, Func<Color, Color> paint, int bloodIndex)
    {
        var body = Map(Crop(sheet, 0, row * 32, 32, 32), paint);
        body.RotateFlip(RotateFlipType.Rotate90FlipNone);
        var pool = Tint(Crop(Load("blood_splatters/blood_red.png"), 0, bloodIndex * 32, 32, 32), Blood);
        var result = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(result))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.DrawImageUnscaled(pool, 0, 2);
            g.DrawImageUnscaled(body, 0, 0);
        }
        return result;
    }

    // ---------- 전투 연출 ----------
    static void BuildFx()
    {
        var splats = Load("blood_splatters/blood_red.png");
        for (int i = 0; i < 4; i++)
            Save(Tint(Crop(splats, 0, i * 32, 32, 32), Blood), "fx/blood_" + (i + 1) + ".png");

        // 불꽃 왼쪽 끝이 총구. 모두 24x20으로 맞춘다
        int[] fires = { 35, 40, 50, 28 };
        for (int i = 0; i < fires.Length; i++)
            Save(PadLeft(Load("muzzle/fire_" + fires[i] + ".png"), 24, 20), "fx/muzzle_flash_" + (i + 1) + ".png");

        for (int i = 1; i <= 9; i++)
            Save(Load("gore/Blood2_Burst_" + i + ".png"), "fx/hit_blood_" + i + ".png");
    }

    // ---------- 목표물·UI ----------
    static void BuildObjAndUi()
    {
        var dyru = Load("dyru/shooter_up.png");
        Save(Scale(Crop(dyru, 176, 16, 16, 16), 2), "obj/obj_target.png");
        Save(Tint(Crop(dyru, 192, 16, 16, 16), ArmorBlue), "ui/icon_armor.png");

        // 타일시트 칸: 64px + 여백 10, 바깥 여백 5. 1행 0열이 가운데가 빈 4갈래
        var cross = Load("crosshair/tilesheet-light.png");
        Save(Outline(Half(Crop(cross, 5, 79, 64, 64))), "ui/crosshair.png");

        Save(Fit16(Load("emotes/emote_question.png"), Suspect), "ui/icon_suspect.png");
        Save(Fit16(Load("emotes/emote_exclamation.png"), HudRed), "ui/icon_detect.png");

        var pistol = Load("firearm_icons/turacept_pistol.png");
        Save(Pad(HudGun(pistol), 64, 32), "ui/weapon_pistol.png");
        Save(Pad(HudGun(Suppressed(pistol)), 64, 32), "ui/weapon_silenced_pistol.png");
        Save(Pad(HudGun(Scale(Load("weapon_pack/Low_Res_Gun_05.png"), 2)), 64, 32), "ui/weapon_rifle.png");
    }

    // 권총 오른쪽 총구 끝에 소음기를 덧붙인다
    static Bitmap Suppressed(Bitmap gun)
    {
        int muzzleX = -1, muzzleY = 0;
        for (int x = gun.Width - 1; x >= 0 && muzzleX < 0; x--)
            for (int y = 0; y < gun.Height; y++)
                if (gun.GetPixel(x, y).A > 128) { muzzleX = x; muzzleY = y; break; }
        var r = new Bitmap(Math.Min(62, muzzleX + 13), gun.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(r)) g.DrawImageUnscaled(gun, 0, 0);
        for (int x = muzzleX; x < r.Width; x++)
            for (int y = Math.Max(0, muzzleY - 1); y < Math.Min(r.Height, muzzleY + 4); y++)
                r.SetPixel(x, y, Color.FromArgb(255, 90, 90, 90));
        return r;
    }

    // 빨강 윤곽 + 어두운 안쪽(HUD 무기 그림)
    static Bitmap HudGun(Bitmap b)
    {
        var r = new Bitmap(b.Width, b.Height, PixelFormat.Format32bppArgb);
        for (int y = 0; y < b.Height; y++)
            for (int x = 0; x < b.Width; x++)
            {
                if (b.GetPixel(x, y).A < 128) continue;
                bool edge = Empty(b, x - 1, y) || Empty(b, x + 1, y) || Empty(b, x, y - 1) || Empty(b, x, y + 1);
                double l = Math.Min(1.0, b.GetPixel(x, y).GetBrightness() + 0.1);
                r.SetPixel(x, y, edge ? HudRed : Color.FromArgb(255, (int)(HudRed.R * l * 0.6), (int)(HudRed.G * l * 0.6), (int)(HudRed.B * l * 0.6)));
            }
        return r;
    }

    // ---------- 직접 그린 도트 ----------
    // drawn/*.txt: 위쪽은 "문자 #RRGGBB" 팔레트, --- 아래는 격자. '.'은 투명
    static void BuildDrawn()
    {
        Save(Grid("accomplice"), "portraits/accomplice.png");
        Save(Grid("player"), "portraits/player.png");
        Save(Grid("obj_bomb"), "obj/obj_bomb.png");
        Save(Grid("obj_exit"), "obj/obj_exit.png");
        Save(Grid("obj_decoy"), "obj/obj_decoy.png");
        Save(Grid("icon_light"), "ui/icon_light.png");
        Save(Grid("icon_noise"), "ui/icon_noise.png");
        Save(Grid("lamp"), "props/lamp.png");
        Save(Grid("casing"), "fx/casing.png");
    }

    static Bitmap Grid(string name)
    {
        var lines = File.ReadAllLines(Path.Combine(src, "drawn/" + name + ".txt"));
        var palette = new Dictionary<char, Color>();
        int i = 0;
        for (; lines[i] != "---"; i++)
            palette[lines[i][0]] = Hex(lines[i].Substring(2).Trim());
        i++;
        int height = lines.Length - i;
        int width = lines[i].Length;
        var r = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        for (int y = 0; y < height; y++)
        {
            string row = lines[i + y];
            if (row.Length != width) throw new InvalidDataException(name + " 격자 폭 오류: " + y + "행 " + row.Length);
            for (int x = 0; x < width; x++)
                if (row[x] != '.') r.SetPixel(x, y, palette[row[x]]);
        }
        return r;
    }

    // ---------- 자세 실루엣 ----------
    // 관절 좌표로 굵은 선을 이어 그린다. 오른쪽을 보는 옆모습, 32x64
    static void BuildStances()
    {
        Save(Stance(new[,] {
            {16,15,16,33, 6,4}, {11,15,9,24, 2,2}, {9,24,10,32, 2,2}, {21,15,23,24, 2,2}, {23,24,22,32, 2,2},
            {14,33,13,47, 3,3}, {13,47,13,60, 3,2}, {18,33,19,47, 3,3}, {19,47,19,60, 3,2},
            {13,60,10,61, 2,1}, {19,60,22,61, 2,1} }, 16, 7), "ui/stance_idle.png");
        Save(Stance(new[,] {
            {16,14,16,33, 5,4}, {16,15,20,24, 2,2}, {20,24,23,31, 2,2}, {16,15,12,24, 2,2}, {12,24,10,31, 2,2},
            {16,33,20,45, 3,3}, {20,45,22,59, 3,2}, {16,33,13,46, 3,3}, {13,46,8,58, 3,2},
            {22,59,26,60, 2,1}, {8,58,11,61, 2,1} }, 16, 7), "ui/stance_walk.png");
        Save(Stance(new[,] {
            {18,16,14,34, 5,4}, {18,17,24,23, 2,2}, {24,23,28,17, 2,2}, {18,17,12,24, 2,2}, {12,24,8,19, 2,2},
            {14,34,24,40, 3,3}, {24,40,23,53, 3,2}, {14,34,10,46, 3,3}, {10,46,2,54, 3,2},
            {23,53,27,54, 2,1}, {2,54,3,59, 2,1} }, 20, 9), "ui/stance_run.png");
        Save(Stance(new[,] {
            {17,32,12,48, 5,4}, {17,33,21,41, 2,2}, {21,41,25,38, 2,2},
            {12,48,23,49, 3,3}, {23,49,19,60, 3,2}, {12,48,9,57, 3,3}, {9,57,3,60, 3,2},
            {19,60,24,61, 2,1} }, 20, 25), "ui/stance_crouch.png");
    }

    static Bitmap Stance(int[,] limbs, int headX, int headY)
    {
        var r = new Bitmap(32, 64, PixelFormat.Format32bppArgb);
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 32; x++)
            {
                bool on = (x - headX) * (x - headX) + (y - headY) * (y - headY) <= 22;
                for (int i = 0; i < limbs.GetLength(0) && !on; i++)
                    on = OnLimb(x, y, limbs[i, 0], limbs[i, 1], limbs[i, 2], limbs[i, 3], limbs[i, 4], limbs[i, 5]);
                if (on) r.SetPixel(x, y, WindowBar);
            }
        return r;
    }

    // 점이 두 관절을 잇는 굵은 선(양 끝 반지름이 다름) 안에 있는지
    static bool OnLimb(int x, int y, double ax, double ay, double bx, double by, double ra, double rb)
    {
        double dx = bx - ax, dy = by - ay;
        double t = Math.Max(0, Math.Min(1, ((x - ax) * dx + (y - ay) * dy) / (dx * dx + dy * dy)));
        double px = ax + dx * t - x, py = ay + dy * t - y, radius = ra + (rb - ra) * t;
        return px * px + py * py <= radius * radius;
    }

    // ---------- UI 패널 ----------
    // 9-slice용 16x16. 테두리 2px를 고정 영역으로 쓴다
    static void BuildPanels()
    {
        var fill = Color.FromArgb(217, 0x0E, 0x0E, 0x12);
        Save(Panel(fill, Color.FromArgb(255, 0x2A, 0x2A, 0x33), false), "ui/panel_dark.png");
        Save(Panel(fill, DoorGold, false), "ui/panel_gold.png");
        Save(Panel(fill, HudRed, true), "ui/panel_dialogue.png");
    }

    static Bitmap Panel(Color fill, Color line, bool topOnly)
    {
        var r = new Bitmap(16, 16, PixelFormat.Format32bppArgb);
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                bool edge = topOnly ? y == 0 : x == 0 || y == 0 || x == 15 || y == 15;
                r.SetPixel(x, y, edge ? line : fill);
            }
        return r;
    }

    // ---------- 사격음 ----------
    // 96kHz 24bit 스테레오 녹음에서 첫 발만 0.12초 잘라 48kHz 16bit 모노로 저장
    static void Shot(string input, string output)
    {
        int rate, channels;
        double[] mono = ReadMono(Path.Combine(src, "firearm_sound/" + input), out rate, out channels);
        double peak = 0;
        foreach (var s in mono) peak = Math.Max(peak, Math.Abs(s));
        int onset = 0;
        while (Math.Abs(mono[onset]) < peak * 0.5) onset++;
        onset = Math.Max(0, onset - rate / 500);

        int factor = rate / 48000;
        int outRate = rate / factor;
        int length = (int)(outRate * 0.12);
        int fade = (int)(outRate * 0.03);
        var pcm = new short[length];
        double gain = 0.89 / peak;
        for (int i = 0; i < length; i++)
        {
            double sum = 0;
            for (int k = 0; k < factor; k++) sum += mono[onset + i * factor + k];
            double value = sum / factor * gain;
            if (i >= length - fade) value *= (length - i) / (double)fade;
            pcm[i] = (short)(value * short.MaxValue);
        }
        WriteWav(Path.Combine(sfx, output), pcm, outRate);
    }

    static double[] ReadMono(string path, out int rate, out int channels)
    {
        using (var r = new BinaryReader(File.OpenRead(path)))
        {
            r.ReadBytes(12);
            rate = 0; channels = 0;
            int bits = 0;
            while (true)
            {
                string id = new string(r.ReadChars(4));
                uint size = r.ReadUInt32();
                if (id == "fmt ")
                {
                    r.ReadInt16();
                    channels = r.ReadInt16();
                    rate = r.ReadInt32();
                    r.ReadBytes(6);
                    bits = r.ReadInt16();
                    r.BaseStream.Seek(size - 16, SeekOrigin.Current);
                }
                else if (id == "data")
                {
                    if (bits != 24) throw new InvalidDataException("24bit WAV만 지원: " + path);
                    byte[] data = r.ReadBytes((int)size);
                    int frames = data.Length / (3 * channels);
                    var mono = new double[frames];
                    for (int f = 0; f < frames; f++)
                    {
                        double sum = 0;
                        for (int c = 0; c < channels; c++)
                        {
                            int p = (f * channels + c) * 3;
                            int v = (data[p] << 8) | (data[p + 1] << 16) | (data[p + 2] << 24);
                            sum += (v >> 8) / 8388608.0;
                        }
                        mono[f] = sum / channels;
                    }
                    return mono;
                }
                else r.BaseStream.Seek(size + (size & 1), SeekOrigin.Current);
            }
        }
    }

    static void WriteWav(string path, short[] pcm, int rate)
    {
        using (var w = new BinaryWriter(File.Create(path)))
        {
            int bytes = pcm.Length * 2;
            w.Write(new[] { 'R', 'I', 'F', 'F' }); w.Write(36 + bytes);
            w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' }); w.Write(16);
            w.Write((short)1); w.Write((short)1); w.Write(rate); w.Write(rate * 2);
            w.Write((short)2); w.Write((short)16);
            w.Write(new[] { 'd', 'a', 't', 'a' }); w.Write(bytes);
            foreach (var s in pcm) w.Write(s);
        }
    }

    // ---------- 이미지 도구 ----------
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
                r.SetPixel(i, j, b.GetPixel(x + i, y + j));
        return r;
    }

    static Bitmap Scale(Bitmap b, int k)
    {
        var r = new Bitmap(b.Width * k, b.Height * k, PixelFormat.Format32bppArgb);
        for (int j = 0; j < r.Height; j++)
            for (int i = 0; i < r.Width; i++)
                r.SetPixel(i, j, b.GetPixel(i / k, j / k));
        return r;
    }

    static Bitmap Half(Bitmap b)
    {
        var r = new Bitmap(b.Width / 2, b.Height / 2, PixelFormat.Format32bppArgb);
        for (int j = 0; j < r.Height; j++)
            for (int i = 0; i < r.Width; i++)
                r.SetPixel(i, j, b.GetPixel(i * 2, j * 2));
        return r;
    }

    static Bitmap Map(Bitmap b, Func<Color, Color> f)
    {
        var r = new Bitmap(b.Width, b.Height, PixelFormat.Format32bppArgb);
        for (int j = 0; j < b.Height; j++)
            for (int i = 0; i < b.Width; i++)
            {
                var c = b.GetPixel(i, j);
                r.SetPixel(i, j, c.A == 0 ? c : f(c));
            }
        return r;
    }

    static Bitmap Desat(Bitmap b, double amount)
    {
        return Map(b, delegate(Color c)
        {
            double l = 0.299 * c.R + 0.587 * c.G + 0.114 * c.B;
            return Color.FromArgb(c.A, Mix(c.R, l, amount), Mix(c.G, l, amount), Mix(c.B, l, amount));
        });
    }

    static int Mix(double a, double b, double t) { return Clamp(a + (b - a) * t); }
    static int Clamp(double v) { return (int)Math.Max(0, Math.Min(255, v)); }

    // 명암은 유지하고 색만 바꾼다. 가장 밝은 픽셀이 target보다 약간 밝아지게 맞춘다
    static Bitmap Tint(Bitmap b, Color target)
    {
        double max = 1;
        for (int j = 0; j < b.Height; j++)
            for (int i = 0; i < b.Width; i++)
            {
                var c = b.GetPixel(i, j);
                if (c.A > 0) max = Math.Max(max, Math.Max(c.R, Math.Max(c.G, c.B)));
            }
        return Map(b, delegate(Color c)
        {
            double l = Math.Max(c.R, Math.Max(c.G, c.B)) / max * 1.15;
            return Color.FromArgb(c.A, Clamp(target.R * l), Clamp(target.G * l), Clamp(target.B * l));
        });
    }

    static Bitmap Pad(Bitmap b, int w, int h)
    {
        var r = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(r)) g.DrawImageUnscaled(b, (w - b.Width) / 2, (h - b.Height) / 2);
        return r;
    }

    static Bitmap PadLeft(Bitmap b, int w, int h)
    {
        var r = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(r)) g.DrawImageUnscaled(b, 0, (h - b.Height) / 2);
        return r;
    }

    static Bitmap Strip(params Bitmap[] frames)
    {
        var r = new Bitmap(frames.Length * 32, 32, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(r))
            for (int i = 0; i < frames.Length; i++) g.DrawImageUnscaled(frames[i], i * 32, 0);
        return r;
    }

    static bool Empty(Bitmap b, int x, int y)
    {
        return x < 0 || y < 0 || x >= b.Width || y >= b.Height || b.GetPixel(x, y).A < 128;
    }

    // 조준선에 1px 어두운 외곽선을 둘러 밝은 바닥에서도 보이게 한다
    static Bitmap Outline(Bitmap b)
    {
        var r = new Bitmap(b);
        for (int y = 0; y < b.Height; y++)
            for (int x = 0; x < b.Width; x++)
                if (Empty(b, x, y) && (!Empty(b, x - 1, y) || !Empty(b, x + 1, y) || !Empty(b, x, y - 1) || !Empty(b, x, y + 1)))
                    r.SetPixel(x, y, Color.FromArgb(200, 10, 10, 14));
        return r;
    }

    // 16칸 안의 기호 부분만 남기고 색을 입힌다
    static Bitmap Fit16(Bitmap b, Color color)
    {
        var r = new Bitmap(16, 16, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(r))
            g.DrawImageUnscaled(Tint(b, color), (16 - b.Width) / 2, (16 - b.Height) / 2);
        return r;
    }

    // 테두리에서 시작해 잔디 팔레트에 있는 색만 지운다
    static Bitmap KeyOutGrass(Bitmap b, Bitmap sample)
    {
        var palette = new HashSet<int>();
        for (int y = 0; y < sample.Height; y++)
            for (int x = 0; x < sample.Width; x++)
                palette.Add(sample.GetPixel(x, y).ToArgb());
        var r = new Bitmap(b);
        var seen = new bool[b.Width, b.Height];
        var open = new Stack<Point>();
        for (int x = 0; x < b.Width; x++) { open.Push(new Point(x, 0)); open.Push(new Point(x, b.Height - 1)); }
        for (int y = 0; y < b.Height; y++) { open.Push(new Point(0, y)); open.Push(new Point(b.Width - 1, y)); }
        // 나무 사이 잔디도 지우기 위해 표본 구역에서도 시작한다
        for (int y = 124; y < 160; y++) for (int x = 200; x < 240; x++) open.Push(new Point(x, y));
        while (open.Count > 0)
        {
            var p = open.Pop();
            if (p.X < 0 || p.Y < 0 || p.X >= b.Width || p.Y >= b.Height || seen[p.X, p.Y]) continue;
            seen[p.X, p.Y] = true;
            if (!palette.Contains(b.GetPixel(p.X, p.Y).ToArgb())) continue;
            r.SetPixel(p.X, p.Y, Color.Transparent);
            open.Push(new Point(p.X + 1, p.Y)); open.Push(new Point(p.X - 1, p.Y));
            open.Push(new Point(p.X, p.Y + 1)); open.Push(new Point(p.X, p.Y - 1));
        }
        return r;
    }

    static Color Hex(string s) { return ColorTranslator.FromHtml(s); }

    static void ToHsv(Color c, out double h, out double s, out double v)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min;
        v = max; s = max == 0 ? 0 : d / max; h = 0;
        if (d == 0) return;
        if (max == r) h = 60 * (((g - b) / d + 6) % 6);
        else if (max == g) h = 60 * ((b - r) / d + 2);
        else h = 60 * ((r - g) / d + 4);
    }

    static Color FromHsv(int a, double h, double s, double v)
    {
        double c = v * s, x = c * (1 - Math.Abs(h / 60 % 2 - 1)), m = v - c, r = 0, g = 0, b = 0;
        int i = (int)(h / 60) % 6;
        if (i == 0) { r = c; g = x; } else if (i == 1) { r = x; g = c; }
        else if (i == 2) { g = c; b = x; } else if (i == 3) { g = x; b = c; }
        else if (i == 4) { r = x; b = c; } else { r = c; b = x; }
        return Color.FromArgb(a, Clamp((r + m) * 255), Clamp((g + m) * 255), Clamp((b + m) * 255));
    }
}
