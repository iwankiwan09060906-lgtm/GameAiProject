using UnityEngine;
using UnityEngine.UI;

namespace GameAi
{
    // 노이즈 텍스처를 화면 전체에 옅게 깔고 매 프레임 UV를 옮긴다
    public sealed class FilmGrain
    {
        const float TexturePixels = 256f;

        readonly RawImage image;
        readonly float tilesX;
        readonly float tilesY;

        public FilmGrain(UiRoot ui, GameConfig config)
        {
            RectTransform rect = UiRoot.CreateRect("FilmGrain", ui.Rect);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetAsFirstSibling();

            image = rect.gameObject.AddComponent<RawImage>();
            image.texture = ProceduralSprites.NoiseTexture();
            // Linear 색공간에서는 알파가 그대로 더해져 어두운 곳이 뿌옇게 뜨므로 화면 밝기 기준으로 바꾼다
            float alpha = Mathf.GammaToLinearSpace(config.lighting.film_grain_alpha);
            image.color = new Color(1f, 1f, 1f, alpha);
            image.raycastTarget = false;
            tilesX = config.pixel_reference_resolution[0] / TexturePixels;
            tilesY = config.pixel_reference_resolution[1] / TexturePixels;
        }

        public void Tick()
        {
            image.uvRect = new Rect(Rng.Fx.Value(), Rng.Fx.Value(), tilesX, tilesY);
        }
    }
}
