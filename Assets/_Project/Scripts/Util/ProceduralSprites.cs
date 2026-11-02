using System.Collections.Generic;
using UnityEngine;

namespace GameAi
{
    // 외부 파일이 없을 때 쓰는 임시 그림. 크기와 색은 ASSET_DIRECTION 3.1, MASTER_PROMPT 11.1과 같다
    public static class ProceduralSprites
    {
        const int Ppu = 32;

        static readonly Color32 Wall = Hex(0x0B, 0x0B, 0x0E);
        static readonly Color32 WallEdge = Hex(0x3A, 0x3A, 0x44);
        static readonly Color32 Paper = Hex(0xE8, 0xE4, 0xDA);
        static readonly Color32 Gold = Hex(0xC9, 0xA2, 0x4A);
        static readonly Color32 Red = Hex(0xC8, 0x32, 0x3C);
        static readonly Color32 Blood = Hex(0x6E, 0x0F, 0x0F);
        static readonly Color32 PanelFill = new Color32(0x0E, 0x0E, 0x12, 217);

        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        static Texture2D noise;

        sealed class Canvas
        {
            public readonly int W;
            public readonly int H;
            public readonly Color32[] Pixels;

            public Canvas(int w, int h)
            {
                W = w;
                H = h;
                Pixels = new Color32[w * h];
            }

            public void Set(int x, int y, Color32 c)
            {
                if (x >= 0 && y >= 0 && x < W && y < H) Pixels[y * W + x] = c;
            }

            public void Fill(Color32 c)
            {
                Rect(0, 0, W, H, c);
            }

            public void Rect(int x, int y, int w, int h, Color32 c)
            {
                for (int j = 0; j < h; j++)
                    for (int i = 0; i < w; i++)
                        Set(x + i, y + j, c);
            }

            public void Frame(int thickness, Color32 c)
            {
                Rect(0, 0, W, thickness, c);
                Rect(0, H - thickness, W, thickness, c);
                Rect(0, 0, thickness, H, c);
                Rect(W - thickness, 0, thickness, H, c);
            }

            public void Circle(float cx, float cy, float r, Color32 c)
            {
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        float dx = x + 0.5f - cx;
                        float dy = y + 0.5f - cy;
                        if (dx * dx + dy * dy <= r * r) Set(x, y, c);
                    }
            }

            public void Grid(int spacing, Color32 c)
            {
                for (int i = 0; i < W; i += spacing)
                {
                    Rect(i, 0, 1, H, c);
                    Rect(0, i, W, 1, c);
                }
            }
        }

        static Color32 Hex(byte r, byte g, byte b)
        {
            return new Color32(r, g, b, 255);
        }

        static Color32 Darker(Color32 c, float amount)
        {
            return new Color32((byte)(c.r * amount), (byte)(c.g * amount), (byte)(c.b * amount), c.a);
        }

        static Canvas Floor(Color32 color)
        {
            var c = new Canvas(32, 32);
            c.Fill(color);
            c.Grid(16, Darker(color, 0.85f));
            return c;
        }

        static Canvas Character(Color32 color, bool shooting)
        {
            var c = new Canvas(32, 32);
            c.Circle(16f, 16f, 13f, Darker(color, 0.6f));
            c.Circle(16f, 16f, 11f, color);
            c.Rect(20, 14, shooting ? 12 : 10, 4, Hex(0x2A, 0x2A, 0x2E));
            return c;
        }

        static Canvas Corpse(Color32 color)
        {
            var c = new Canvas(32, 32);
            c.Circle(14f, 15f, 12f, Blood);
            c.Rect(6, 10, 20, 12, color);
            return c;
        }

        static Canvas Crosshair()
        {
            var c = new Canvas(32, 32);
            Color32 white = Hex(255, 255, 255);
            c.Rect(15, 2, 2, 8, white);
            c.Rect(15, 22, 2, 8, white);
            c.Rect(2, 15, 8, 2, white);
            c.Rect(22, 15, 8, 2, white);
            return c;
        }

        static Canvas Symbol(Color32 color, bool question)
        {
            var c = new Canvas(16, 16);
            c.Rect(7, 2, 2, 2, color);
            if (question)
            {
                c.Rect(7, 5, 2, 4, color);
                c.Rect(5, 9, 6, 2, color);
                c.Rect(5, 11, 2, 2, color);
                c.Rect(9, 11, 2, 2, color);
            }
            else
            {
                c.Rect(7, 6, 2, 8, color);
            }
            return c;
        }

        static Canvas Stance(int headY, int bodyHeight)
        {
            var c = new Canvas(32, 64);
            c.Circle(16f, headY, 5f, Paper);
            c.Rect(11, headY - 6 - bodyHeight, 10, bodyHeight, Paper);
            return c;
        }

        static Canvas Gun()
        {
            var c = new Canvas(64, 32);
            c.Rect(8, 14, 40, 6, Red);
            c.Rect(10, 8, 8, 8, Red);
            return c;
        }

        static Canvas Panel(Color32 line, bool topOnly)
        {
            var c = new Canvas(16, 16);
            c.Fill(PanelFill);
            if (topOnly) c.Rect(0, 15, 16, 1, line);
            else c.Frame(1, line);
            return c;
        }

        static Canvas Draw(string name)
        {
            Canvas c;
            switch (name)
            {
                case "white":
                    c = new Canvas(1, 1);
                    c.Fill(Hex(255, 255, 255));
                    return c;
                case "tile_floor_wood": return Floor(Hex(0x6B, 0x4A, 0x2B));
                case "tile_floor_tile": return Floor(Hex(0x8A, 0x8A, 0x8A));
                case "tile_floor_carpet": return Floor(Hex(0x2F, 0x7C, 0x7A));
                case "tile_floor_outdoor": return Floor(Hex(0x1E, 0x2A, 0x22));
                case "tile_wall":
                    c = new Canvas(32, 32);
                    c.Fill(Wall);
                    c.Frame(1, WallEdge);
                    return c;
                case "tile_window":
                    c = new Canvas(32, 32);
                    c.Fill(Wall);
                    c.Frame(1, WallEdge);
                    c.Rect(2, 13, 28, 6, Paper);
                    return c;
                case "door_closed":
                    c = new Canvas(32, 8);
                    c.Fill(Gold);
                    return c;
                case "door_open":
                    c = new Canvas(8, 32);
                    c.Fill(Gold);
                    return c;
                case "cover_low":
                    c = new Canvas(32, 32);
                    c.Fill(Hex(0x7A, 0x55, 0x32));
                    for (int y = 4; y < 32; y += 8) c.Rect(0, y, 32, 2, Hex(0x5A, 0x3D, 0x22));
                    return c;
                case "cover_high":
                    c = new Canvas(32, 32);
                    c.Fill(Hex(0x4A, 0x32, 0x20));
                    return c;
                case "foliage":
                    c = new Canvas(32, 32);
                    c.Circle(16f, 16f, 14f, Hex(0x1F, 0x4A, 0x2A));
                    return c;
                case "lamp":
                    c = new Canvas(8, 8);
                    c.Circle(4f, 4f, 4f, Hex(0xFF, 0xC8, 0x73));
                    return c;
                case "casing":
                    c = new Canvas(2, 2);
                    c.Fill(Hex(0xD9, 0xB2, 0x4C));
                    return c;
                case "muzzle_flash":
                    c = new Canvas(16, 16);
                    c.Rect(0, 6, 16, 4, Hex(0xFF, 0xE6, 0x80));
                    c.Rect(6, 0, 4, 16, Hex(0xFF, 0xE6, 0x80));
                    return c;
                case "blood":
                    c = new Canvas(32, 32);
                    c.Circle(15f, 16f, 11f, Blood);
                    c.Circle(22f, 20f, 5f, Blood);
                    return c;
                case "hit_blood":
                    c = new Canvas(20, 20);
                    c.Circle(10f, 10f, 5f, Hex(0xB0, 0x1E, 0x1E));
                    return c;
                case "obj_target":
                    c = new Canvas(32, 32);
                    c.Rect(8, 6, 16, 20, Hex(0xF2, 0xC2, 0x30));
                    return c;
                case "obj_bomb":
                    c = new Canvas(32, 32);
                    c.Rect(6, 8, 20, 16, Red);
                    return c;
                case "obj_exit":
                    c = new Canvas(32, 32);
                    c.Frame(2, Hex(0x37, 0xC8, 0x71));
                    return c;
                case "obj_decoy":
                    c = new Canvas(32, 32);
                    c.Circle(16f, 16f, 7f, Hex(0xFF, 0x9A, 0x3C));
                    return c;
                case "crosshair": return Crosshair();
                case "icon_suspect": return Symbol(Hex(0xF2, 0xC2, 0x30), true);
                case "icon_detect": return Symbol(Red, false);
                case "icon_armor":
                    c = new Canvas(16, 16);
                    c.Rect(3, 3, 10, 10, Hex(0x8F, 0xD3, 0xF0));
                    return c;
                case "icon_light":
                    c = new Canvas(16, 16);
                    c.Circle(8f, 9f, 5f, Hex(0xF2, 0xE3, 0xA0));
                    return c;
                case "icon_noise":
                    c = new Canvas(16, 16);
                    c.Rect(2, 5, 8, 6, Paper);
                    return c;
                case "panel_dark": return Panel(Hex(0x2A, 0x2A, 0x33), false);
                case "panel_gold": return Panel(Gold, false);
                case "panel_dialogue": return Panel(Red, true);
                case "player_idle": case "player_walk": return Character(Hex(0xD8, 0xD8, 0xD8), false);
                case "player_shoot": return Character(Hex(0xD8, 0xD8, 0xD8), true);
                case "guard_idle": case "guard_walk": return Character(Hex(0x7A, 0x3B, 0x2E), false);
                case "guard_shoot": return Character(Hex(0x7A, 0x3B, 0x2E), true);
                case "rifleman_idle": case "rifleman_walk": return Character(Hex(0x4E, 0x5A, 0x3A), false);
                case "rifleman_shoot": return Character(Hex(0x4E, 0x5A, 0x3A), true);
                case "corpse_guard": return Corpse(Hex(0x7A, 0x3B, 0x2E));
                case "corpse_rifleman": return Corpse(Hex(0x4E, 0x5A, 0x3A));
                case "corpse_player": return Corpse(Hex(0xD8, 0xD8, 0xD8));
                case "stance_idle": case "stance_walk": return Stance(56, 30);
                case "stance_run": return Stance(54, 26);
                case "stance_crouch": return Stance(38, 20);
                case "weapon_pistol": case "weapon_silenced_pistol": case "weapon_rifle": return Gun();
                default: return null;
            }
        }

        public static bool Has(string name)
        {
            return Cache.ContainsKey(name) || Draw(name) != null;
        }

        public static Sprite Get(string name)
        {
            if (Cache.TryGetValue(name, out Sprite sprite) && sprite != null) return sprite;

            Canvas canvas = Draw(name);
            if (canvas == null)
            {
                Debug.LogWarning("[ProceduralSprites] 만들 수 없는 그림 이름입니다: " + name);
                canvas = new Canvas(32, 32);
                canvas.Fill(Hex(255, 0, 255));
            }

            var texture = new Texture2D(canvas.W, canvas.H, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = name,
            };
            texture.SetPixels32(canvas.Pixels);
            texture.Apply(false, true);

            float ppu = name == "white" ? 1f : Ppu;
            var border = name.StartsWith("panel_") ? new Vector4(2, 2, 2, 2) : Vector4.zero;
            sprite = Sprite.Create(texture, new Rect(0, 0, canvas.W, canvas.H), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect, border);
            sprite.name = name;
            Cache[name] = sprite;
            return sprite;
        }

        // 필름 그레인·야간 투시가 함께 쓰는 256x256 노이즈. 연출 난수로 만든다
        public static Texture2D NoiseTexture()
        {
            if (noise != null) return noise;
            const int Size = 256;
            var pixels = new Color32[Size * Size];
            for (int i = 0; i < pixels.Length; i++)
            {
                byte v = (byte)Rng.Fx.Int(0, 256);
                pixels[i] = new Color32(v, v, v, 255);
            }
            noise = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
                name = "film_noise",
            };
            noise.SetPixels32(pixels);
            noise.Apply(false, true);
            return noise;
        }
    }
}
