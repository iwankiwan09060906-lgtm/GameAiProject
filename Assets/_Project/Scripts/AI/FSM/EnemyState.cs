namespace GameAi
{
    // 적의 상태 6가지(7.7 FSM 전환 표). Phase 5에서 EnemyBrain이 전환을 맡는다.
    // 순찰 → 조사 → 추격/교전 → 수색 → 순찰로 돌아오는 흐름이고, 체력이 0이면 어느 상태에서든 사망
    public enum EnemyState { Patrol, Investigate, Chase, Engage, Search, Dead }
}
