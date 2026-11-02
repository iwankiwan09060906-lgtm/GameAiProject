using UnityEngine;

namespace GameAi
{
    // 탐색 기억을 1픽셀 = 1칸 텍스처로 조명 위에 덮는다
    public sealed class VisionRenderer
    {
        readonly FogOfWar fog;
        readonly Texture2D texture;
        readonly Color32[] pixels;
        readonly Color32 unexplored;
        readonly Color32 memory;

        public VisionRenderer(GridMap grid, FogOfWar fog, FogConfig config, AssetCatalog catalog, Transform parent)
        {
            this.fog = fog;
            pixels = new Color32[grid.Width * grid.Height];
            float gray = config.memory_gray;
            unexplored = new Color(0f, 0f, 0f, config.unexplored_alpha);
            memory = new Color(gray, gray, gray * 1.1f, config.memory_alpha);

            texture = new Texture2D(grid.Width, grid.Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };

            var go = new GameObject("FogOfWar", typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = Sprite.Create(texture, new Rect(0, 0, grid.Width, grid.Height), Vector2.zero, 1f);
            renderer.sharedMaterial = catalog.SpriteUnlit;
            renderer.sortingOrder = SortOrder.Fog;
            Refresh();
        }

        public void Refresh()
        {
            fog.WritePixels(pixels, unexplored, memory);
            texture.SetPixels32(pixels);
            texture.Apply(false);
        }
    }
}
