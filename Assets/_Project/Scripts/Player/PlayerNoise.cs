using UnityEngine;

namespace GameAi
{
    // 이동 모드별 발소리(7.2). 정지하면 소리가 없고, 바닥 배율(S)을 곱한다
    public sealed class PlayerNoise
    {
        readonly GridMap grid;
        readonly PlayerConfig config;
        readonly FloorMult floorMult;
        float timer;

        public PlayerNoise(GridMap grid, PlayerConfig config, FloorMult floorMult)
        {
            this.grid = grid;
            this.config = config;
            this.floorMult = floorMult;
        }

        public static float RadiusFor(MoveMode mode, PlayerConfig config)
        {
            switch (mode)
            {
                case MoveMode.Run: return config.footstep_run_m;
                case MoveMode.Crouch: return config.footstep_crouch_m;
                default: return config.footstep_walk_m;
            }
        }

        public static float IntervalFor(MoveMode mode, PlayerConfig config)
        {
            switch (mode)
            {
                case MoveMode.Run: return config.step_run_s;
                case MoveMode.Crouch: return config.step_crouch_s;
                default: return config.step_walk_s;
            }
        }

        // 발소리를 냈으면 true
        public bool Tick(float dt, bool moving, MoveMode mode, Vector2 position)
        {
            if (!moving)
            {
                timer = 0f;
                return false;
            }
            timer -= dt;
            if (timer > 0f) return false;
            timer = IntervalFor(mode, config);
            Vector2Int cell = grid.WorldToCell(position);
            float radius = RadiusFor(mode, config) * floorMult.For(grid.GetFloor(cell.x, cell.y));
            WorldEvents.Raise("SND_FOOTSTEP", position, EntityIds.Player, -1, radius);
            return true;
        }
    }

    // 깡통 소품(`k`): 웅크리지 않고 들어가면 깡통마다 일정 간격으로 소리
    public sealed class CanSystem
    {
        readonly GridMap grid;
        readonly Vector2Int[] cans;
        readonly float[] lastTime;
        readonly float cooldown;
        Vector2Int lastCell = new Vector2Int(int.MinValue, 0);

        public int Count { get { return cans.Length; } }

        public CanSystem(GridMap grid, System.Collections.Generic.List<Vector2Int> cans, float cooldown)
        {
            this.grid = grid;
            this.cans = cans.ToArray();
            this.cooldown = cooldown;
            lastTime = new float[this.cans.Length];
            for (int i = 0; i < lastTime.Length; i++) lastTime[i] = float.NegativeInfinity;
        }

        public Vector2Int CanCell(int index) { return cans[index]; }

        // 소리를 낸 깡통 번호, 없으면 -1
        public int Tick(Vector2 playerPos, MoveMode mode, float now)
        {
            Vector2Int cell = grid.WorldToCell(playerPos);
            bool entered = cell != lastCell;
            lastCell = cell;
            if (!entered || mode == MoveMode.Crouch) return -1;
            for (int i = 0; i < cans.Length; i++)
            {
                if (cans[i] != cell || now - lastTime[i] < cooldown) continue;
                lastTime[i] = now;
                WorldEvents.Raise("SND_CAN", grid.CellCenter(cell), EntityIds.Player, i);
                return i;
            }
            return -1;
        }
    }
}
