using UnityEngine;

namespace GameAi
{
    // 시야 밖을 덮는 안개를 그린다. 탐색 기억은 "1픽셀 = 1칸" 텍스처(부드럽게 보간)로 두고,
    // 지금 보이는 곳은 시야 모양(VisionFan) 메시의 정점 알파로 구멍을 뚫는다.
    // 메시: 플레이어 위치(맑음) → 레이 끝점(맑음) → 끝점 + 완충 거리(안개) → 아주 먼 곳(안개)의 고리 4겹
    public sealed class VisionRenderer
    {
        readonly FogOfWar fog;
        readonly VisionFan fan;
        readonly Texture2D texture;
        readonly Mesh mesh;

        // 매 프레임 다시 쓰는 배열(새로 만들지 않는다)
        readonly Color32[] pixels;
        readonly Vector3[] vertices;
        readonly Color32[] colors;
        readonly Vector2[] uvs;
        readonly Color32 unexplored;
        readonly Color32 memory;

        // 월드 좌표 → 텍스처 좌표 배율(1 / 맵 크기 m)
        readonly Vector2 uvScale;

        // 맵 어디에 서도 화면 끝까지 덮을 만큼 먼 고리의 반지름(m)
        readonly float farRadius;

        public VisionRenderer(GridMap grid, FogOfWar fog, FogConfig config, PlayerConfig player, AssetCatalog catalog, Transform parent)
        {
            this.fog = fog;
            fan = new VisionFan(grid, player.fov_deg, player.view_m, config);
            pixels = new Color32[grid.Width * grid.Height];

            // 기억은 거의 검은 회색에 살짝 푸른 기를 준다
            float gray = config.memory_gray;
            unexplored = new Color(0f, 0f, 0f, config.unexplored_alpha);
            memory = new Color(gray, gray, gray * 1.1f, config.memory_alpha);

            // 부드러운 보간을 켜면 기억 경계도 칸 모양 대신 번진 그림자처럼 보인다
            texture = new Texture2D(grid.Width, grid.Height, TextureFormat.RGBA32, false)
            {
                filterMode = config.smooth_memory ? FilterMode.Bilinear : FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };

            Vector2 size = new Vector2(grid.Width, grid.Height) * grid.CellSize;
            uvScale = new Vector2(1f / size.x, 1f / size.y);
            farRadius = size.magnitude + player.view_m * 2f;

            int rays = fan.Rays;
            vertices = new Vector3[1 + rays * 3];
            colors = new Color32[vertices.Length];
            uvs = new Vector2[vertices.Length];
            mesh = new Mesh { name = "VisionFog" };
            mesh.MarkDynamic();
            mesh.vertices = vertices;
            mesh.triangles = BuildTriangles(rays);

            // 조명을 받지 않는 전용 재질. 텍스처는 재질을 복제하지 않도록 PropertyBlock으로 넘긴다
            var go = new GameObject("FogOfWar", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = catalog.VisionFog;
            renderer.sortingOrder = SortOrder.Fog;
            var block = new MaterialPropertyBlock();
            block.SetTexture("_MainTex", texture);
            renderer.SetPropertyBlock(block);

            Refresh(size / 2f, Vector2.right);
        }

        // 정점 번호: 0 = 중심, 레이 i마다 1 + 3i = 맑은 끝, 2 + 3i = 완충 끝, 3 + 3i = 먼 고리.
        // 이웃한 두 레이 사이를 중심 삼각형 1개 + 사각형 2개(삼각형 4개)로 채운다
        static int[] BuildTriangles(int rays)
        {
            var triangles = new int[rays * 15];
            int t = 0;
            for (int i = 0; i < rays; i++)
            {
                int j = (i + 1) % rays;
                int a0 = 1 + i * 3, b0 = a0 + 1, c0 = a0 + 2;
                int a1 = 1 + j * 3, b1 = a1 + 1, c1 = a1 + 2;
                triangles[t++] = 0; triangles[t++] = a1; triangles[t++] = a0;
                triangles[t++] = a0; triangles[t++] = a1; triangles[t++] = b1;
                triangles[t++] = a0; triangles[t++] = b1; triangles[t++] = b0;
                triangles[t++] = b0; triangles[t++] = b1; triangles[t++] = c1;
                triangles[t++] = b0; triangles[t++] = c1; triangles[t++] = c0;
            }
            return triangles;
        }

        // 현재 탐색 상태와 시야 모양을 화면에 반영한다
        public void Refresh(Vector2 origin, Vector2 aimDir)
        {
            fog.WritePixels(pixels, unexplored, memory);
            texture.SetPixels32(pixels);
            texture.Apply(false);

            fan.Compute(origin, aimDir);
            SetVertex(0, origin, 0f);
            for (int i = 0; i < fan.Rays; i++)
            {
                Vector2 dir = fan.Directions[i];
                Vector2 inner = fan.Inner[i];
                SetVertex(1 + i * 3, inner, fan.InnerAlpha[i]);
                SetVertex(2 + i * 3, inner + dir * fan.Feather[i], 1f);
                SetVertex(3 + i * 3, origin + dir * farRadius, 1f);
            }

            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uvs);
            mesh.bounds = new Bounds(origin, new Vector3(farRadius * 2f, farRadius * 2f, 1f));
        }

        void SetVertex(int index, Vector2 position, float alpha)
        {
            vertices[index] = position;
            colors[index] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f));
            uvs[index] = new Vector2(position.x * uvScale.x, position.y * uvScale.y);
        }
    }
}
