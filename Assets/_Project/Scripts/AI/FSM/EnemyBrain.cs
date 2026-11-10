namespace GameAi
{
    // FSM 판단 입력(한 프레임 분)
    public struct BrainInput
    {
        public bool Dead;
        public float Gauge;
        public float DetectAt;
        public float SuspectAt;
        public bool PlayerVisible;
        public bool InRange;
        public bool NewClue;
        public bool ArrivedAtClue;
        public bool SearchDone;
        public bool RadioReceived;
    }

    // 7.7 전환 표. 우선순위: 사망 > 눈앞의 플레이어 > 새 단서 > 수색 종료
    public static class EnemyBrain
    {
        public static EnemyState Next(EnemyState current, in BrainInput i, out string reason)
        {
            reason = null;
            if (current == EnemyState.Dead) return current;
            if (i.Dead)
            {
                reason = "hp_zero";
                return EnemyState.Dead;
            }
            bool detected = i.Gauge >= i.DetectAt;
            switch (current)
            {
                case EnemyState.Patrol:
                    if (detected)
                    {
                        reason = "sight_gauge_100";
                        return i.InRange ? EnemyState.Engage : EnemyState.Chase;
                    }
                    if (i.Gauge >= i.SuspectAt)
                    {
                        reason = "sight_gauge_30";
                        return EnemyState.Investigate;
                    }
                    if (i.NewClue)
                    {
                        reason = i.RadioReceived ? "alert_received" : "new_clue";
                        return EnemyState.Investigate;
                    }
                    return current;

                case EnemyState.Investigate:
                    if (detected)
                    {
                        reason = "sight_gauge_100";
                        return i.InRange ? EnemyState.Engage : EnemyState.Chase;
                    }
                    if (i.ArrivedAtClue && !i.PlayerVisible)
                    {
                        reason = "clue_reached";
                        return EnemyState.Search;
                    }
                    return current;

                case EnemyState.Chase:
                    if (!detected)
                    {
                        reason = "lost_player";
                        return EnemyState.Search;
                    }
                    if (i.InRange && i.PlayerVisible)
                    {
                        reason = "in_range";
                        return EnemyState.Engage;
                    }
                    return current;

                case EnemyState.Engage:
                    if (!detected)
                    {
                        reason = "lost_player";
                        return EnemyState.Search;
                    }
                    return current;

                case EnemyState.Search:
                    if (detected)
                    {
                        reason = "sight_gauge_100";
                        return i.InRange ? EnemyState.Engage : EnemyState.Chase;
                    }
                    if (i.NewClue)
                    {
                        reason = i.RadioReceived ? "alert_received" : "new_clue";
                        return EnemyState.Investigate;
                    }
                    if (i.SearchDone)
                    {
                        reason = "search_timeout";
                        return EnemyState.Patrol;
                    }
                    return current;
            }
            return current;
        }
    }

    public enum CombatAction { TakeCover, Reload, Approach, Shoot, MoveToLastSeen }

    // 교전 행동 트리(Selector, 7.7). 위에서부터 먼저 맞는 것을 고른다
    public static class CombatTree
    {
        public static CombatAction Decide(float hpRatio, float coverHpRatio, bool reloading, bool magEmpty, bool lineBlocked,
            bool inRange, bool visible, bool coverAvailable, bool allowCover)
        {
            if (allowCover && coverAvailable && (hpRatio <= coverHpRatio || reloading || (lineBlocked && visible)))
                return CombatAction.TakeCover;
            if (magEmpty || reloading) return CombatAction.Reload;
            if (!inRange) return CombatAction.Approach;
            if (visible) return CombatAction.Shoot;
            return CombatAction.MoveToLastSeen;
        }
    }
}
