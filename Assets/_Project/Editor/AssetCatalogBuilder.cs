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
    // Art/Audio/Fonts를 훑어 AssetCatalog.asset을 다시 만들고 임포트 설정을 맞춘다
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
        const int FramePixels = 32;
        const int PixelsPerUnit = 32;
        const int PanelBorder = 2;
        const int FontSamplingSize = 24;
        const int FontPadding = 2;
        const int FontAtlasSize = 2048;

        static bool building;

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (building || Application.isBatchMode) return;
            bool relevant = imported.Concat(deleted).Concat(moved).Any(IsWatched);
            if (relevant) EditorApplication.delayCall += () => Build();
        }

        static bool IsWatched(string path)
        {
            if (path.EndsWith(".asset") || path.EndsWith(".meta")) return false;
            return path.StartsWith(ArtRoot) || path.StartsWith(AudioRoot) || path.StartsWith(FontRoot);
        }

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
                    AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterial));
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssets();
                return catalog;
            }
            finally
            {
                building = false;
            }
        }

        static IEnumerable<string> FindFiles(string root, params string[] extensions)
        {
            if (!Directory.Exists(root)) return Enumerable.Empty<string>();
            return Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                .Select(p => p.Replace('\\', '/'))
                .Where(p => extensions.Contains(Path.GetExtension(p).ToLowerInvariant()))
                .OrderBy(p => Path.GetFileNameWithoutExtension(p), System.StringComparer.Ordinal);
        }

        static bool IsSheet(string path)
        {
            return path.Contains("/chars/") && path.EndsWith("_walk.png");
        }

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

        // 가로로 이어 붙인 시트를 32x32 프레임으로 자른다
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

            var names = found.Keys.ToList();
            names.Sort(AssetNames.NaturalCompare);

            var entries = new List<AssetCatalog.SpriteEntry>();
            foreach (string name in names)
            {
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

        // 한글이 많아 글자를 미리 굽지 않고 동적 아틀라스에 필요한 글자만 넣는다
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
