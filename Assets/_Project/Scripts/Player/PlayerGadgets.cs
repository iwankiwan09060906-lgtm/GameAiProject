using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace GameAi
{
    // 유인탄(7.4, S): G로 커서 위치(최대 10m, 벽 앞까지)에 던진다. 0.5초 비행 후 착지점에서 3초 동안 0.5초마다 금속음(반경 10m)
    public sealed class DecoySystem
    {
        public const float MaxRange = 10f;
        public const float FlightSeconds = 0.5f;
        public const float NoiseSeconds = 3f;
        public const float NoiseInterval = 0.5f;

        readonly GridMap grid;
        readonly SpriteRenderer view;
        Vector2 from, to;
        float clock = -1f;
        float nextNoise;

        public int Count { get; private set; }
        public bool Active { get { return clock >= 0f; } }
        public Vector2 LandingPoint { get { return to; } }

        public DecoySystem(GridMap grid, int count, SpriteRenderer renderer)
        {
            this.grid = grid;
            Count = count;
            view = renderer;
            if (view != null) view.enabled = false;
        }

        // 던질 수 있으면 착지점을 계산해 비행을 시작한다
        public bool Throw(Vector2 player, Vector2 cursor)
        {
            if (Count <= 0 || Active) return false;
            Vector2 delta = cursor - player;
            float distance = Mathf.Min(MaxRange, delta.magnitude);
            if (distance < 0.1f) return false;
            Vector2 dir = delta.normalized;
            RayHit hit = Raycast.Cast(grid, player, dir, distance, RayMask.Bullet);
            float reach = hit.Hit ? Mathf.Max(0f, hit.Distance - 0.3f) : distance;
            Count--;
            from = player;
            to = player + dir * reach;
            clock = 0f;
            nextNoise = FlightSeconds;
            if (view != null) view.enabled = true;
            return true;
        }

        public void Tick(float dt)
        {
            if (!Active) return;
            clock += dt;
            float t = Mathf.Clamp01(clock / FlightSeconds);
            if (view != null) view.transform.position = Vector2.Lerp(from, to, t) + Vector2.up * Mathf.Sin(t * Mathf.PI) * 0.6f;
            while (clock >= nextNoise && nextNoise <= FlightSeconds + NoiseSeconds - 1e-4f)
            {
                WorldEvents.Raise("SND_DECOY", to, EntityIds.Player);
                nextNoise += NoiseInterval;
            }
            if (clock >= FlightSeconds + NoiseSeconds)
            {
                clock = -1f;
                if (view != null) view.enabled = false;
            }
        }
    }

    // 배터리 계산(순수): 켜면 초당 1 소모, 끄면 초당 0.5 충전, 0이면 자동으로 꺼짐(7.20)
    public sealed class NightVisionBattery
    {
        readonly float capacity;
        readonly float recharge;

        public float Seconds { get; private set; }
        public bool On { get; private set; }
        public float Ratio { get { return capacity > 0f ? Seconds / capacity : 0f; } }

        public NightVisionBattery(float capacity, float recharge)
        {
            this.capacity = capacity;
            this.recharge = recharge;
            Seconds = capacity;
        }

        public bool Toggle()
        {
            if (!On && Seconds <= 0f) return false;
            On = !On;
            return true;
        }

        // 자동으로 꺼졌으면 true
        public bool Tick(float dt)
        {
            if (On)
            {
                Seconds = Mathf.Max(0f, Seconds - dt);
                if (Seconds <= 0f)
                {
                    On = false;
                    return true;
                }
                return false;
            }
            Seconds = Mathf.Min(capacity, Seconds + recharge * dt);
            return false;
        }
    }

    // 야간 투시 화면(S): 녹색 단색 + 노이즈 + 비네팅, 화면용 전역광을 올린다. 밝기 값·AI 감지에는 영향 없음
    public sealed class NightVision
    {
        readonly NightVisionBattery battery;
        readonly RawImage tint;
        readonly RawImage noise;
        readonly Image vignette;
        readonly Light2D global;
        readonly float normalIntensity;
        readonly float nvgIntensity;
        readonly Color normalColor;
        readonly Color nvgColor;
        readonly System.Collections.Generic.List<Light2D> lamps;
        readonly System.Collections.Generic.List<Color> lampColors = new System.Collections.Generic.List<Color>();

        public bool On { get { return battery.On; } }
        public float Ratio { get { return battery.Ratio; } }

        public NightVision(UiRoot ui, LightingConfig lighting, Light2D globalLight, System.Collections.Generic.List<Light2D> mapLamps, float capacity, float recharge)
        {
            lamps = mapLamps ?? new System.Collections.Generic.List<Light2D>();
            foreach (Light2D lamp in lamps) lampColors.Add(lamp != null ? lamp.color : Color.white);
            battery = new NightVisionBattery(capacity, recharge);
            global = globalLight;
            normalIntensity = globalLight != null ? globalLight.intensity : 1f;
            normalColor = globalLight != null ? globalLight.color : Color.white;
            // 화면용 세기는 판정값 × 배율인데, 녹색 전역광은 그대로 쓰면 과다 노출되어 1/4로 쓴다
            nvgIntensity = lighting.nvg_global_intensity * lighting.global_intensity_scale * 0.25f;
            Color green = MathUtil.ParseColor(lighting.nvg_color, new Color(0.49f, 1f, 0.6f));
            // 화면 전역광을 녹색으로 바꿔 곱하기처럼 보이게 한다(어두운 곳·미탐색은 그대로 검다)
            nvgColor = green;
            RectTransform root = UiRoot.CreateRect("NightVision", ui.Rect);
            Widgets.Stretch(root);
            root.SetAsFirstSibling();
            tint = root.gameObject.AddComponent<RawImage>();
            tint.texture = Texture2D.whiteTexture;
            // Linear 색공간이라 UI 알파가 진하게 보인다. 덧칠은 끄고 노이즈만 아주 옅게(필름 그레인과 같은 보정)
            tint.color = new Color(green.r, green.g, green.b, 0f);
            tint.raycastTarget = false;
            noise = UiRoot.CreateRect("Noise", root).gameObject.AddComponent<RawImage>();
            Widgets.Stretch(noise.rectTransform);
            noise.texture = ProceduralSprites.NoiseTexture();
            noise.color = new Color(green.r, green.g, green.b, Mathf.GammaToLinearSpace(0.05f));
            noise.raycastTarget = false;
            vignette = UiRoot.CreateImage("Vignette", root, VignetteSprite());
            Widgets.Stretch(vignette.rectTransform);
            vignette.color = new Color(0f, 0f, 0f, 0.85f);
            root.gameObject.SetActive(false);
        }

        static Sprite vignetteSprite;

        static Sprite VignetteSprite()
        {
            if (vignetteSprite != null) return vignetteSprite;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size - 0.5f, dy = (y + 0.5f) / size - 0.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / 0.7071f;
                    byte a = (byte)(Mathf.Clamp01((d - 0.55f) / 0.45f) * 255f);
                    pixels[y * size + x] = new Color32(0, 0, 0, a);
                }
            tex.SetPixels32(pixels);
            tex.Apply();
            vignetteSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return vignetteSprite;
        }

        // 켜짐 상태가 바뀌었으면 true(효과음용)
        public bool Tick(float dt, bool togglePressed)
        {
            bool before = battery.On;
            if (togglePressed) battery.Toggle();
            battery.Tick(dt);
            bool on = battery.On;
            tint.gameObject.SetActive(on);
            if (on) noise.uvRect = new Rect(Rng.Fx.Value(), Rng.Fx.Value(), 4f, 2.25f);
            if (global != null)
            {
                global.intensity = on ? nvgIntensity : normalIntensity;
                global.color = on ? nvgColor : normalColor;
            }
            if (on != before)
                for (int i = 0; i < lamps.Count; i++)
                    if (lamps[i] != null) lamps[i].color = on ? nvgColor : lampColors[i];
            return on != before;
        }
    }
}
