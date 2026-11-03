using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace GameAi
{
    // 화면 조명만 맡는다(Light2D). 적 감지에 쓰는 판정용 밝기(LightGrid)는 Phase 8에서 따로 만든다.
    // 전역광 하나 + 천장등마다 점광원 하나 + 벽·높은 엄폐물·닫힌 문에 그림자 상자를 둔다
    public sealed class MapLighting
    {
        readonly AssetCatalog catalog;
        readonly LightingConfig config;
        readonly Transform root;

        // 그림자 상자의 크기 기준이 되는 1x1 흰 그림
        readonly Sprite box;

        public Light2D GlobalLight { get; private set; }

        public MapLighting(AssetCatalog catalog, LightingConfig config, Transform parent)
        {
            this.catalog = catalog;
            this.config = config;
            root = new GameObject("Lighting").transform;
            root.SetParent(parent, false);
            box = ProceduralSprites.Get("white");
        }

        public void Build(ParsedMap map, MapData data)
        {
            CreateGlobal(data.ambient.indoor);
            foreach (MapLight light in data.lights)
                CreateLamp(light);
            CreateWallShadows(map.Grid);
        }

        // 맵 전체에 깔리는 어두운 남색 전역광
        void CreateGlobal(float ambient)
        {
            var go = new GameObject("GlobalLight");
            go.transform.SetParent(root, false);
            GlobalLight = go.AddComponent<Light2D>();
            GlobalLight.lightType = Light2D.LightType.Global;
            GlobalLight.color = MathUtil.ParseColor(config.ambient_color, new Color(0.1f, 0.09f, 0.19f));
            GlobalLight.intensity = ambient * config.global_intensity_scale;
        }

        // 천장등 하나: 따뜻한 노란 점광원 + 위치 표시 점
        void CreateLamp(MapLight data)
        {
            var go = new GameObject("Lamp");
            go.transform.SetParent(root, false);
            go.transform.position = data.Position;

            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = MathUtil.ParseColor(config.lamp_color, new Color(1f, 0.78f, 0.45f));
            light.intensity = data.intensity;
            light.pointLightInnerRadius = 0f;
            light.pointLightOuterRadius = data.radius_m;
            light.falloffIntensity = config.lamp_falloff;
            light.shadowsEnabled = true;
            light.shadowIntensity = config.shadow_strength;

            // 조명 위치를 알려 주는 작은 점. 조명을 받지 않게 해서 항상 밝게 보인다
            var marker = go.AddComponent<SpriteRenderer>();
            marker.sprite = catalog.GetSprite("lamp");
            marker.sharedMaterial = catalog.SpriteUnlit;
            marker.sortingOrder = SortOrder.Prop;
        }

        void CreateWallShadows(GridMap grid)
        {
            foreach (RectInt rect in MergeShadowCells(grid))
                CreateShadowBox(rect, root);
        }

        // 그림자 물체 수를 줄이려고 빛을 막는 고정 칸(벽·높은 엄폐물)을 큰 사각형으로 합친다.
        // 칸마다 하나씩 만들면 맵 1에서만 수백 개가 되어 조명 계산이 느려진다.
        // 방법: 왼쪽 아래부터 훑다가 아직 안 쓴 막힌 칸을 만나면 오른쪽으로 최대한, 위로 최대한 넓힌다
        public static List<RectInt> MergeShadowCells(GridMap grid)
        {
            var result = new List<RectInt>();
            var used = new bool[grid.Width * grid.Height];
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    if (used[grid.Index(x, y)] || !IsSolid(grid, x, y)) continue;

                    // 오른쪽으로 연속된 막힌 칸만큼 폭을 넓힌다
                    int width = 1;
                    while (x + width < grid.Width && IsSolid(grid, x + width, y) && !used[grid.Index(x + width, y)])
                        width++;

                    // 위쪽 줄이 같은 폭 전체가 막혀 있는 동안 높이를 넓힌다
                    int height = 1;
                    while (y + height < grid.Height && RowFree(grid, used, x, y + height, width))
                        height++;

                    for (int j = 0; j < height; j++)
                        for (int i = 0; i < width; i++)
                            used[grid.Index(x + i, y + j)] = true;

                    result.Add(new RectInt(x, y, width, height));
                }
            }
            return result;
        }

        // 항상 빛을 막는 칸(벽, 높은 엄폐물). 문은 열고 닫히므로 별도 상자를 쓴다
        static bool IsSolid(GridMap grid, int x, int y)
        {
            TileType type = grid.GetTile(x, y);
            return type == TileType.Wall || type == TileType.CoverHigh;
        }

        // y줄의 x부터 width칸이 모두 막힌 칸이고 아직 안 쓴 칸인지
        static bool RowFree(GridMap grid, bool[] used, int x, int y, int width)
        {
            for (int i = 0; i < width; i++)
                if (used[grid.Index(x + i, y)] || !IsSolid(grid, x + i, y)) return false;
            return true;
        }

        // 사각형 모양의 그림자 상자를 만든다. 문도 이 함수로 하나씩 만든다.
        // ShadowCaster2D는 런타임에 모양을 넘기는 공개 API가 없어서, 꺼 둔 SpriteRenderer의 범위(= 크기를 늘린 1x1 상자)를 모양으로 쓴다
        public ShadowCaster2D CreateShadowBox(RectInt rect, Transform parent)
        {
            var go = new GameObject("ShadowBox");
            go.transform.SetParent(parent, false);
            go.transform.position = rect.center;
            go.transform.localScale = new Vector3(rect.width, rect.height, 1f);

            var bounds = go.AddComponent<SpriteRenderer>();
            bounds.sprite = box;
            bounds.enabled = false;

            var caster = go.AddComponent<ShadowCaster2D>();
            caster.castingOption = ShadowCaster2D.ShadowCastingOptions.CastShadow;
            return caster;
        }
    }
}
