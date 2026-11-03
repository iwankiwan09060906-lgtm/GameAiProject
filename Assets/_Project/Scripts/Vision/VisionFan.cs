using UnityEngine;

namespace GameAi
{
    // 화면에 그릴 시야 모양(부채꼴 + 몸 주변 작은 원)을 레이 끝점으로 구한다. 판정용 시야(PlayerVision)와 따로 둔 표시 전용이다.
    // 360°를 고르게 나눠 레이를 쏘고, 레이마다 "여기까지 맑음(Inner)"과 "여기서부터 어두워지는 거리(Feather)"를 정한다.
    // 칸 단위가 아니라 벽에 닿은 실제 지점을 쓰므로 경계가 계단 모양이 되지 않는다
    public sealed class VisionFan
    {
        readonly GridMap grid;
        readonly float fovDeg;
        readonly float viewM;
        readonly FogConfig config;

        public int Rays { get; }

        // 레이 방향(길이 1)
        public readonly Vector2[] Directions;

        // 맑게 보이는 끝 지점(벽에 닿은 곳 또는 시야 거리 끝)
        public readonly Vector2[] Inner;

        // Inner 지점의 어두움(0 = 맑음, 1 = 안개). 부채꼴 좌우 가장자리에서 0 → 1로 바뀐다
        public readonly float[] InnerAlpha;

        // Inner에서 완전히 어두워질 때까지의 거리(m)
        public readonly float[] Feather;

        public VisionFan(GridMap grid, float fovDeg, float viewM, FogConfig config)
        {
            this.grid = grid;
            this.fovDeg = fovDeg;
            this.viewM = viewM;
            this.config = config;
            Rays = Mathf.Max(8, config.soft_rays);
            Directions = new Vector2[Rays];
            Inner = new Vector2[Rays];
            InnerAlpha = new float[Rays];
            Feather = new float[Rays];
        }

        // origin에서 aimDir 쪽을 보는 시야 모양을 다시 구한다. 0번 레이는 정확히 등 뒤다
        public void Compute(Vector2 origin, Vector2 aimDir)
        {
            float center = MathUtil.AngleDeg(aimDir);
            float half = fovDeg / 2f;
            float soft = Mathf.Max(0f, config.cone_soft_deg);
            for (int i = 0; i < Rays; i++)
            {
                float angle = center - 180f + 360f * i / Rays;
                Vector2 dir = MathUtil.FromAngleDeg(angle);
                float offset = Mathf.Abs(Mathf.DeltaAngle(angle, center));

                // 부채꼴(+ 가장자리 완충 각도) 안은 시야 거리까지, 밖은 몸 주변 작은 원까지만 맑다
                float length, alpha, feather;
                if (offset <= half + soft)
                {
                    length = viewM;
                    alpha = offset <= half || soft <= 0f ? 0f : Smooth01((offset - half) / soft);
                    feather = config.edge_range_m;
                }
                else
                {
                    length = config.near_m;
                    alpha = 0f;
                    feather = config.edge_near_m;
                }

                // 벽·높은 가구에 막히면 그 물체의 두께만큼은 윗면이 보이고, 반대쪽 끝에서 짧게 어두워진다.
                // 물체 너머(옆 방)는 드러나지 않는다
                RayHit hit = Raycast.Cast(grid, origin, dir, length, RayMask.Sight);
                float inner = hit.Distance;
                if (hit.Hit)
                {
                    float exit = hit.Distance + BlockDepth(origin, dir, hit.Distance);
                    inner = Mathf.Max(hit.Distance, exit - config.edge_wall_m);
                    feather = exit - inner;
                }

                Directions[i] = dir;
                Inner[i] = origin + dir * inner;
                InnerAlpha[i] = alpha;
                Feather[i] = Mathf.Max(0.01f, feather);
            }
        }

        // start 거리에서 시작해 막힌 칸이 이어지는 길이(m). 맵 밖에 닿거나 reveal_depth_m을 넘으면 거기서 끊는다
        float BlockDepth(Vector2 origin, Vector2 dir, float start)
        {
            float step = grid.CellSize * 0.25f;
            float max = Mathf.Max(step, config.reveal_depth_m);
            float depth = 0f;
            while (depth < max)
            {
                Vector2 point = origin + dir * (start + depth + step * 0.5f);
                Vector2Int cell = grid.WorldToCell(point);
                if (!grid.InBounds(cell.x, cell.y) || !grid.BlocksRay(cell.x, cell.y, RayMask.Sight)) break;
                depth += step;
            }
            return depth;
        }

        static float Smooth01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
