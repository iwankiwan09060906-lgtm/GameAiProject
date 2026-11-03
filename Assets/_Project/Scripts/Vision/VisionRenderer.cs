using UnityEngine;

namespace GameAi
{
    // 탐색 기억을 "1픽셀 = 1칸" 텍스처 한 장으로 만들어 조명 위에 덮는다.
    // 텍스처를 월드 크기로 늘려 그리고 Point 필터를 써서 칸 단위의 또렷한 경계가 된다
    public sealed class VisionRenderer
    {
        readonly FogOfWar fog;
        readonly Texture2D texture;

        // 매 프레임 다시 쓰는 픽셀 배열(새로 만들지 않는다)
        readonly Color32[] pixels;
        readonly Color32 unexplored;
        readonly Color32 memory;

        public VisionRenderer(GridMap grid, FogOfWar fog, FogConfig config, AssetCatalog catalog, Transform parent)
        {
            this.fog = fog;
            pixels = new Color32[grid.Width * grid.Height];

            // 기억은 거의 검은 회색에 살짝 푸른 기를 준다
            float gray = config.memory_gray;
            unexplored = new Color(0f, 0f, 0f, config.unexplored_alpha);
            memory = new Color(gray, gray, gray * 1.1f, config.memory_alpha);

            texture = new Texture2D(grid.Width, grid.Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };

            // pixelsPerUnit 1: 텍스처 한 픽셀이 월드 1m(= 1칸)가 된다. 조명을 받지 않는 재질로 항상 같은 색을 낸다
            var go = new GameObject("FogOfWar", typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = Sprite.Create(texture, new Rect(0, 0, grid.Width, grid.Height), Vector2.zero, 1f);
            renderer.sharedMaterial = catalog.SpriteUnlit;
            renderer.sortingOrder = SortOrder.Fog;
            Refresh();
        }

        // 현재 탐색 상태를 텍스처에 반영한다
        public void Refresh()
        {
            fog.WritePixels(pixels, unexplored, memory);
            texture.SetPixels32(pixels);
            texture.Apply(false);
        }
    }
}
