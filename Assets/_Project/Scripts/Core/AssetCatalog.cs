using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace GameAi
{
    // 파일 이름 → 에셋 연결표(11.0). 빌드된 게임은 Assets 폴더를 직접 읽지 못하므로,
    // 에디터(AssetCatalogBuilder)가 Art/Audio/Fonts를 훑어 이 ScriptableObject에 담아 두고 씬이 참조한다.
    // 이름에 해당하는 파일이 없으면 코드로 만든 그림(ProceduralSprites)을 돌려줘서 에셋이 없어도 실행된다.
    public sealed class AssetCatalog : ScriptableObject
    {
        // 그림 하나(또는 시트를 자른 프레임들)
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
        [SerializeField] Material visionFog;

        // 이름으로 빨리 찾으려고 처음 쓸 때 한 번 만드는 색인(저장되지 않음)
        Dictionary<string, Sprite[]> spriteIndex;
        Dictionary<string, AudioClip> clipIndex;
        Material fallbackLit;
        Material fallbackUnlit;
        Material fallbackFog;

        public TMP_FontAsset Font { get { return font; } }

        // 조명을 받는 스프라이트 재질. 카탈로그에 없으면 URP 기본 셰이더로 직접 만든다
        public Material SpriteLit
        {
            get
            {
                if (spriteLit != null) return spriteLit;
                if (fallbackLit == null) fallbackLit = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default"));
                return fallbackLit;
            }
        }

        // 조명을 받지 않는 재질(안개, 총구 화염, 벽 윤곽선처럼 항상 같은 밝기여야 하는 것)
        public Material SpriteUnlit
        {
            get
            {
                if (spriteUnlit != null) return spriteUnlit;
                if (fallbackUnlit == null) fallbackUnlit = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
                return fallbackUnlit;
            }
        }

        // 시야 안개 메시 재질(GameAi/VisionFog). 빌드에 셰이더가 들어가도록 카탈로그가 재질을 참조한다.
        // 카탈로그가 없으면 셰이더를 이름으로 찾는다(에디터에서는 항상 찾힌다)
        public Material VisionFog
        {
            get
            {
                if (visionFog != null) return visionFog;
                if (fallbackFog == null)
                {
                    Shader shader = Shader.Find("GameAi/VisionFog");
                    if (shader == null)
                    {
                        Debug.LogWarning("[AssetCatalog] GameAi/VisionFog 셰이더가 없어 안개 가장자리가 부드럽지 않습니다.");
                        return SpriteUnlit;
                    }
                    fallbackFog = new Material(shader);
                }
                return fallbackFog;
            }
        }

        // 에디터 빌더가 내용을 채울 때 부른다
        public void SetContent(List<SpriteEntry> spriteEntries, List<ClipEntry> clipEntries, TMP_FontAsset fontAsset, Material lit, Material unlit, Material fog)
        {
            sprites = spriteEntries;
            clips = clipEntries;
            font = fontAsset;
            spriteLit = lit;
            spriteUnlit = unlit;
            visionFog = fog;
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

        // 이름의 그림 한 장. 파일이 없으면 코드 생성 그림, 그것도 없는 이름이면 null
        public Sprite GetSprite(string spriteName)
        {
            EnsureIndex();
            if (spriteIndex.TryGetValue(spriteName, out Sprite[] frames)) return frames[0];
            return ProceduralSprites.Has(spriteName) ? ProceduralSprites.Get(spriteName) : null;
        }

        // 시트를 자른 프레임 전체(걷기 애니메이션). 시트가 아니면 한 장짜리 배열
        public Sprite[] GetFrames(string spriteName)
        {
            EnsureIndex();
            if (spriteIndex.TryGetValue(spriteName, out Sprite[] frames)) return frames;
            Sprite single = GetSprite(spriteName);
            return single != null ? new[] { single } : new Sprite[0];
        }

        // "blood"를 주면 blood_1, blood_2 ... 처럼 접두사가 같은 그림을 모두 모은다(변형 파일, 11장).
        // 하나도 없으면 코드 생성 그림 1장
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

    // 에셋 파일 이름 규칙(ASSET_DIRECTION 3장)을 검사하는 도구. 에디터 빌더와 테스트가 함께 쓴다
    public static class AssetNames
    {
        // 이름이 정확히 일치해야 하는 파일
        static readonly string[] Exact =
        {
            "tile_floor_wood", "tile_floor_tile", "tile_floor_carpet", "tile_floor_outdoor", "tile_wall", "tile_window",
            "door_closed", "door_open", "lamp", "muzzle_flash", "casing",
            "obj_target", "obj_bomb", "obj_exit", "obj_decoy", "obj_can", "obj_switch", "cloud",
            "crosshair", "icon_suspect", "icon_detect", "icon_armor", "icon_light", "icon_noise",
            "panel_dark", "panel_gold", "panel_dialogue",
        };

        // 접두사만 맞으면 되는 파일(변형, 캐릭터 시트, 효과음 등)
        static readonly string[] Prefixes =
        {
            "cover_low_", "cover_high_", "foliage_", "player_", "guard_", "rifleman_", "held_", "corpse_",
            "muzzle_flash_", "blood_", "hit_blood_", "stance_", "weapon_", "sfx_", "bgm_", "amb_",
            "deco_", "floor_",
        };

        // 이름 규칙에 없는 파일은 카탈로그에 넣지 않는다. 초상화는 이름이 화자 ID라서 폴더로 구분한다
        public static bool IsKnown(string name, bool isPortrait)
        {
            if (isPortrait) return true;
            if (Array.IndexOf(Exact, name) >= 0) return true;
            foreach (string prefix in Prefixes)
                if (name.StartsWith(prefix, StringComparison.Ordinal)) return true;
            return false;
        }

        // 같은 이름이 또 나오면 먼저 찾은 것을 쓰고 false를 돌려준다(경고는 부르는 쪽이 한다)
        public static bool TryAddUnique<T>(Dictionary<string, T> map, string name, T value)
        {
            if (map.ContainsKey(name)) return false;
            map.Add(name, value);
            return true;
        }

        // 끝 번호까지 숫자로 비교한다. 글자 순서로는 blood_10이 blood_2보다 앞에 오기 때문
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
