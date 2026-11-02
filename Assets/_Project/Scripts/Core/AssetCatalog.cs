using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace GameAi
{
    // 파일 이름 → 에셋 연결표. Tools/Build Project가 채우고, 없는 항목은 코드 생성 에셋을 돌려준다
    public sealed class AssetCatalog : ScriptableObject
    {
        [Serializable]
        public class SpriteEntry
        {
            public string name;
            public Sprite[] frames;
        }

        [Serializable]
        public class ClipEntry
        {
            public string name;
            public AudioClip clip;
        }

        [SerializeField] List<SpriteEntry> sprites = new List<SpriteEntry>();
        [SerializeField] List<ClipEntry> clips = new List<ClipEntry>();
        [SerializeField] TMP_FontAsset font;
        [SerializeField] Material spriteLit;
        [SerializeField] Material spriteUnlit;

        Dictionary<string, Sprite[]> spriteIndex;
        Dictionary<string, AudioClip> clipIndex;
        Material fallbackLit;
        Material fallbackUnlit;

        public TMP_FontAsset Font { get { return font; } }

        public Material SpriteLit
        {
            get
            {
                if (spriteLit != null) return spriteLit;
                if (fallbackLit == null) fallbackLit = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default"));
                return fallbackLit;
            }
        }

        public Material SpriteUnlit
        {
            get
            {
                if (spriteUnlit != null) return spriteUnlit;
                if (fallbackUnlit == null) fallbackUnlit = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
                return fallbackUnlit;
            }
        }

        public void SetContent(List<SpriteEntry> spriteEntries, List<ClipEntry> clipEntries, TMP_FontAsset fontAsset, Material lit, Material unlit)
        {
            sprites = spriteEntries;
            clips = clipEntries;
            font = fontAsset;
            spriteLit = lit;
            spriteUnlit = unlit;
            spriteIndex = null;
            clipIndex = null;
        }

        void EnsureIndex()
        {
            if (spriteIndex != null) return;
            spriteIndex = new Dictionary<string, Sprite[]>();
            foreach (var entry in sprites)
                if (entry.frames != null && entry.frames.Length > 0)
                    spriteIndex[entry.name] = entry.frames;
            clipIndex = new Dictionary<string, AudioClip>();
            foreach (var entry in clips)
                if (entry.clip != null)
                    clipIndex[entry.name] = entry.clip;
        }

        // 파일이 없으면 코드 생성 그림. 둘 다 없는 이름이면 null
        public Sprite GetSprite(string spriteName)
        {
            EnsureIndex();
            if (spriteIndex.TryGetValue(spriteName, out Sprite[] frames)) return frames[0];
            return ProceduralSprites.Has(spriteName) ? ProceduralSprites.Get(spriteName) : null;
        }

        public Sprite[] GetFrames(string spriteName)
        {
            EnsureIndex();
            if (spriteIndex.TryGetValue(spriteName, out Sprite[] frames)) return frames;
            Sprite single = GetSprite(spriteName);
            return single != null ? new[] { single } : new Sprite[0];
        }

        // "blood"면 blood_1, blood_2 ... 를 모은다. 하나도 없으면 코드 생성 그림 1장
        public Sprite[] GetVariants(string prefix)
        {
            EnsureIndex();
            var result = new List<Sprite>();
            foreach (var entry in sprites)
                if (entry.name.StartsWith(prefix + "_", StringComparison.Ordinal) && entry.frames.Length > 0)
                    result.Add(entry.frames[0]);
            if (result.Count == 0) result.Add(ProceduralSprites.Get(prefix));
            return result.ToArray();
        }

        public AudioClip GetClip(string id)
        {
            EnsureIndex();
            return clipIndex.TryGetValue(id, out AudioClip clip) ? clip : null;
        }
    }

    public static class AssetNames
    {
        static readonly string[] Exact =
        {
            "tile_floor_wood", "tile_floor_tile", "tile_floor_carpet", "tile_floor_outdoor", "tile_wall", "tile_window",
            "door_closed", "door_open", "lamp", "muzzle_flash", "casing",
            "obj_target", "obj_bomb", "obj_exit", "obj_decoy",
            "crosshair", "icon_suspect", "icon_detect", "icon_armor", "icon_light", "icon_noise",
            "panel_dark", "panel_gold", "panel_dialogue",
        };

        static readonly string[] Prefixes =
        {
            "cover_low_", "cover_high_", "foliage_", "player_", "guard_", "rifleman_", "held_", "corpse_",
            "muzzle_flash_", "blood_", "hit_blood_", "stance_", "weapon_", "sfx_", "bgm_", "amb_",
        };

        // 이름 규칙에 없는 파일은 카탈로그에 넣지 않는다. 초상화는 폴더로 구분한다
        public static bool IsKnown(string name, bool isPortrait)
        {
            if (isPortrait) return true;
            if (Array.IndexOf(Exact, name) >= 0) return true;
            foreach (string prefix in Prefixes)
                if (name.StartsWith(prefix, StringComparison.Ordinal)) return true;
            return false;
        }

        // 같은 이름이 또 나오면 먼저 찾은 것을 쓰고 false를 돌려준다
        public static bool TryAddUnique<T>(Dictionary<string, T> map, string name, T value)
        {
            if (map.ContainsKey(name)) return false;
            map.Add(name, value);
            return true;
        }

        // blood_2가 blood_10보다 앞에 오게 끝 번호까지 비교한다
        public static int NaturalCompare(string a, string b)
        {
            int underscoreA = a.LastIndexOf('_');
            int underscoreB = b.LastIndexOf('_');
            if (underscoreA > 0 && underscoreB > 0 && a.Substring(0, underscoreA) == b.Substring(0, underscoreB)
                && int.TryParse(a.Substring(underscoreA + 1), out int numberA)
                && int.TryParse(b.Substring(underscoreB + 1), out int numberB))
            {
                return numberA.CompareTo(numberB);
            }
            return string.CompareOrdinal(a, b);
        }
    }
}
