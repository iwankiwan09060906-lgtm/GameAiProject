using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace GameAi.EditorTools
{
    // Art/Audio/Fonts를 훑어 AssetCatalog.asset을 다시 만들고, 그림 임포트 설정을 규칙에 맞춘다(11.0).
    // AssetPostprocessor라서 파일을 넣거나 지우면 에디터에서 자동으로 갱신된다
    public sealed class AssetCatalogBuilder : AssetPostprocessor
    {
        public const string CatalogPath = "Assets/_Project/AssetCatalog.asset";
        const string ArtRoot = "Assets/_Project/Art";
        const string AudioRoot = "Assets/_Project/Audio";
        const string FontRoot = "Assets/_Project/Fonts";
        const string FontFile = FontRoot + "/Galmuri11.ttf";
        const string FontAssetPath = FontRoot + "/Galmuri11 SDF.asset";
        const string LitMaterial = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat";
        const string UnlitMaterial = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
        const string FogShader = "Assets/_Project/Shaders/VisionFog.shader";
        const string FogMaterial = "Assets/_Project/Shaders/VisionFog.mat";

        // 걷기 시트의 프레임 한 장 크기(px)
        const int FramePixels = 32;
        const int PixelsPerUnit = 32;

        // 9-slice 패널의 고정 테두리(px)
        const int PanelBorder = 2;

        // Galmuri11의 도트 격자가 12px이라 12의 배수여야 픽셀이 어긋나지 않는다
        const int FontSamplingSize = 24;
        const int FontPadding = 2;
        const int FontAtlasSize = 2048;

        // 빌드 도중 파일이 바뀌어 다시 빌드가 시작되는 것을 막는다
        static bool building;

        // 에셋이 임포트·삭제·이동될 때마다 불리는 유니티 콜백
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            // 배치 모드에서는 ProjectBuilder가 직접 Build를 부르므로 건너뛴다
            if (building || Application.isBatchMode) return;
            bool relevant = imported.Concat(deleted).Concat(moved).Any(IsWatched);

            // 임포트 중에 에셋을 만지면 안 되므로 한 박자 뒤로 미룬다
            if (relevant) EditorApplication.delayCall += () => Build();
        }

        // 카탈로그가 만든 .asset·.meta는 제외한다. 그걸 감시하면 서로를 계속 불러 끝나지 않는다
        static bool IsWatched(string path)
        {
            if (path.EndsWith(".asset") || path.EndsWith(".meta")) return false;
            return path.StartsWith(ArtRoot) || path.StartsWith(AudioRoot) || path.StartsWith(FontRoot);
        }

        // 카탈로그를 처음부터 다시 만든다. Tools/Build Project가 부른다
        public static AssetCatalog Build()
        {
            building = true;
            try
            {
                ApplyAllSpriteSettings();
                AssetDatabase.SaveAssets();

                AssetCatalog catalog = AssetDatabase.LoadAssetAtPath<AssetCatalog>(CatalogPath);
                if (catalog == null)
                {
                    catalog = ScriptableObject.CreateInstance<AssetCatalog>();
                    AssetDatabase.CreateAsset(catalog, CatalogPath);
                }

                catalog.SetContent(CollectSprites(), CollectClips(), EnsureFontAsset(),
                    AssetDatabase.LoadAssetAtPath<Material>(LitMaterial),
                    AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterial),
                    EnsureFogMaterial());
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssets();
                return catalog;
            }
            finally
            {
                building = false;
            }
        }

        // 폴더 안(하위 포함)의 해당 확장자 파일을 이름 순으로. 순서가 항상 같아야 같은 결과가 나온다
        static IEnumerable<string> FindFiles(string root, params string[] extensions)
        {
            if (!Directory.Exists(root)) return Enumerable.Empty<string>();
            return Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                .Select(p => p.Replace('\\', '/'))
                .Where(p => extensions.Contains(Path.GetExtension(p).ToLowerInvariant()))
                .OrderBy(p => Path.GetFileNameWithoutExtension(p), System.StringComparer.Ordinal);
        }

        // 가로로 이어 붙인 걷기 시트인지
        static bool IsSheet(string path)
        {
            return path.Contains("/chars/") && path.EndsWith("_walk.png");
        }

        // 모든 그림에 PPU 32, Point 필터, 압축 없음을 적용하고 시트는 32x32로 자른다
        static void ApplyAllSpriteSettings()
        {
            foreach (string path in FindFiles(ArtRoot, ".png"))
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                bool changed = ApplySpriteSettings(importer, path);
                if (IsSheet(path)) changed |= SliceSheet(importer);
                if (changed) importer.SaveAndReimport();
            }
        }

        // 설정이 이미 같으면 아무것도 하지 않는다(불필요한 재임포트를 피한다). 바꿨으면 true
        static bool ApplySpriteSettings(TextureImporter importer, string path)
        {
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);

            var mode = IsSheet(path) ? SpriteImportMode.Multiple : SpriteImportMode.Single;
            Vector4 border = Path.GetFileName(path).StartsWith("panel_") ? Vector4.one * PanelBorder : Vector4.zero;
            bool same = importer.textureType == TextureImporterType.Sprite
                && importer.spriteImportMode == mode
                && Mathf.Approximately(importer.spritePixelsPerUnit, PixelsPerUnit)
                && importer.filterMode == FilterMode.Point
                && importer.textureCompression == TextureImporterCompression.Uncompressed
                && !importer.mipmapEnabled
                && settings.spriteMeshType == SpriteMeshType.FullRect
                && settings.spriteBorder == border;
            if (same) return false;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = mode;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;

            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteBorder = border;
            settings.spriteExtrude = 0;
            importer.SetTextureSettings(settings);
            return true;
        }

        // 가로로 이어 붙인 시트를 32x32 프레임으로 자른다. 이미 같게 잘려 있으면 false
        static bool SliceSheet(TextureImporter importer)
        {
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();

            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            int frames = width / FramePixels;
            SpriteRect[] current = provider.GetSpriteRects();
            bool same = current.Length == frames;
            for (int i = 0; same && i < frames; i++)
                same = current[i].rect == new Rect(i * FramePixels, 0, FramePixels, height);
            if (same) return false;

            // 프레임 이름은 {파일이름}_{번호}
            string baseName = Path.GetFileNameWithoutExtension(importer.assetPath);
            var rects = new SpriteRect[frames];
            for (int i = 0; i < frames; i++)
            {
                rects[i] = new SpriteRect
                {
                    name = baseName + "_" + i,
                    spriteID = GUID.Generate(),
                    rect = new Rect(i * FramePixels, 0, FramePixels, height),
                    alignment = SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                };
            }
            provider.SetSpriteRects(rects);
            provider.Apply();
            return true;
        }

        // Art 폴더의 그림을 이름 규칙에 맞는 것만 모은다
        static List<AssetCatalog.SpriteEntry> CollectSprites()
        {
            var found = new Dictionary<string, string>();
            foreach (string path in FindFiles(ArtRoot, ".png"))
            {
                string name = Path.GetFileNameWithoutExtension(path);
                if (!AssetNames.IsKnown(name, path.Contains("/portraits/")))
                {
                    Debug.LogWarning("[AssetCatalog] 이름 규칙에 없는 파일이라 건너뜁니다: " + path);
                    continue;
                }
                if (!AssetNames.TryAddUnique(found, name, path))
                    Debug.LogWarning("[AssetCatalog] 같은 이름 파일이 있어 먼저 찾은 것을 씁니다: " + path);
            }

            // hit_blood_2가 hit_blood_10보다 앞에 오도록 숫자 순으로 정렬한다
            var names = found.Keys.ToList();
            names.Sort(AssetNames.NaturalCompare);

            var entries = new List<AssetCatalog.SpriteEntry>();
            foreach (string name in names)
            {
                // 시트는 자른 프레임들을 가로 위치 순으로, 일반 그림은 한 장을 담는다
                Sprite[] frames = AssetDatabase.LoadAllAssetRepresentationsAtPath(found[name])
                    .OfType<Sprite>().OrderBy(s => s.rect.x).ToArray();
                if (frames.Length == 0)
                {
                    Sprite single = AssetDatabase.LoadAssetAtPath<Sprite>(found[name]);
                    if (single == null) continue;
                    frames = new[] { single };
                }
                entries.Add(new AssetCatalog.SpriteEntry { name = name, frames = frames });
            }
            return entries;
        }

        // Audio 폴더의 소리를 이름 규칙에 맞는 것만 모은다. ogg·wav·mp3 모두 받는다
        static List<AssetCatalog.ClipEntry> CollectClips()
        {
            var found = new Dictionary<string, string>();
            foreach (string path in FindFiles(AudioRoot, ".ogg", ".wav", ".mp3"))
            {
                string name = Path.GetFileNameWithoutExtension(path);
                if (!AssetNames.IsKnown(name, false))
                {
                    Debug.LogWarning("[AssetCatalog] 이름 규칙에 없는 파일이라 건너뜁니다: " + path);
                    continue;
                }
                if (!AssetNames.TryAddUnique(found, name, path))
                    Debug.LogWarning("[AssetCatalog] 같은 이름 파일이 있어 먼저 찾은 것을 씁니다: " + path);
            }

            var entries = new List<AssetCatalog.ClipEntry>();
            foreach (var pair in found.OrderBy(p => p.Key, System.StringComparer.Ordinal))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(pair.Value);
                if (clip != null) entries.Add(new AssetCatalog.ClipEntry { name = pair.Key, clip = clip });
            }
            return entries;
        }

        // 시야 안개 재질을 만든다. 카탈로그가 참조해야 빌드에 셰이더가 포함된다
        static Material EnsureFogMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(FogMaterial);
            if (existing != null) return existing;

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(FogShader);
            if (shader == null)
            {
                Debug.LogWarning("[AssetCatalog] VisionFog.shader가 없어 안개 재질을 만들지 않습니다.");
                return null;
            }
            var material = new Material(shader) { name = "VisionFog" };
            AssetDatabase.CreateAsset(material, FogMaterial);
            return material;
        }

        // TMP 폰트 에셋을 만든다. 한글 글자가 매우 많아 미리 굽지 않고,
        // 동적 아틀라스(Dynamic)에 화면에 처음 나오는 글자만 그때그때 넣는다. 힌팅 없는 Raster라 도트가 번지지 않는다
        static TMP_FontAsset EnsureFontAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null) return existing;

            var font = AssetDatabase.LoadAssetAtPath<Font>(FontFile);
            if (font == null)
            {
                Debug.LogWarning("[AssetCatalog] Galmuri11.ttf가 없어 Unity 기본 폰트로 표시합니다.");
                return null;
            }

            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font, FontSamplingSize, FontPadding,
                GlyphRenderMode.RASTER, FontAtlasSize, FontAtlasSize, AtlasPopulationMode.Dynamic, true);
            if (asset == null)
            {
                Debug.LogError("[AssetCatalog] TMP 폰트 에셋을 만들지 못했습니다.");
                return null;
            }

            // 아틀라스와 재질을 폰트 에셋 파일 안에 함께 저장해야 다음 실행에서도 남는다
            asset.name = "Galmuri11 SDF";
            AssetDatabase.CreateAsset(asset, FontAssetPath);
            asset.atlasTexture.name = "Galmuri11 Atlas";
            asset.atlasTexture.filterMode = FilterMode.Point;
            asset.material.name = "Galmuri11 Material";
            AssetDatabase.AddObjectToAsset(asset.atlasTexture, asset);
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }
    }
}
