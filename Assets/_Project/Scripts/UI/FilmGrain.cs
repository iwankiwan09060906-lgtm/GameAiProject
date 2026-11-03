using UnityEngine;
using UnityEngine.UI;

namespace GameAi
{
    // 노이즈 텍스처를 화면 전체에 아주 옅게 깔고 매 프레임 위치를 옮겨 필름 입자 느낌을 낸다(Phase 2).
    // 야간 투시(Phase 9)가 같은 텍스처를 쓴다
    public sealed class FilmGrain
    {
        const float TexturePixels = 256f;

        readonly RawImage image;

        // 화면(960x540 기준)을 덮으려면 256px 텍스처를 가로·세로로 몇 번 반복해야 하는지
        readonly float tilesX;
        readonly float tilesY;

        public FilmGrain(UiRoot ui, GameConfig config)
        {
            // 화면 전체를 덮는 이미지. 가장 뒤(첫 자식)에 둬서 다른 UI 아래로 깔린다
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

        // 매 프레임 텍스처 시작 위치를 난수로 바꿔 입자가 반짝이게 한다(연출 난수)
        public void Tick()
        {
            image.uvRect = new Rect(Rng.Fx.Value(), Rng.Fx.Value(), tilesX, tilesY);
        }
    }
}
