using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace GameAi
{
    // 화면 조명만 맡는다. 판정용 밝기(LightGrid)는 Phase 8에서 따로 만든다
    public sealed class MapLighting
    {
        readonly AssetCatalog catalog;
        readonly LightingConfig config;
        readonly Transform root;
        Sprite box;

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

        void CreateGlobal(float ambient)
        {
            var go = new GameObject("GlobalLight");
            go.transform.SetParent(root, false);
            GlobalLight = go.AddComponent<Light2D>();
            GlobalLight.lightType = Light2D.LightType.Global;
            GlobalLight.color = MathUtil.ParseColor(config.ambient_color, new Color(0.1f, 0.09f, 0.19f));
            GlobalLight.intensity = ambient * config.global_intensity_scale;
        }

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

        // 그림자 물체 수를 줄이려고 빛을 막는 고정 칸을 큰 사각형으로 합친다
        public static List<RectInt> MergeShadowCells(GridMap grid)
        {
            var result = new List<RectInt>();
            var used = new bool[grid.Width * grid.Height];
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    if (used[grid.Index(x, y)] || !IsSolid(grid, x, y)) continue;

                    int width = 1;
                    while (x + width < grid.Width && IsSolid(grid, x + width, y) && !used[grid.Index(x + width, y)])
                        width++;

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

        static bool IsSolid(GridMap grid, int x, int y)
        {
            TileType type = grid.GetTile(x, y);
            return type == TileType.Wall || type == TileType.CoverHigh;
        }

        static bool RowFree(GridMap grid, bool[] used, int x, int y, int width)
        {
            for (int i = 0; i < width; i++)
                if (used[grid.Index(x + i, y)] || !IsSolid(grid, x + i, y)) return false;
            return true;
        }

        // ShadowCaster2D는 런타임에 모양을 넘기는 API가 없어, 꺼 둔 렌더러의 범위로 사각형을 정한다
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
