using System.Collections.Generic;
using UnityEngine;

namespace GameAi
{
    // F 키 근접 제압. 소음이 없고 즉시 사망시킨다(7.5).
    // 조건 판정은 MeleeRules(순수 함수)가 하고, 이 클래스는 후보 적을 찾아 피해를 보낸다
    public sealed class MeleeTakedown
    {
        readonly GridMap grid;
        readonly MeleeConfig config;

        // 이 값(100) 이상이면 이미 발견된 적이라 제압할 수 없다
        readonly float detectGauge;
        readonly IReadOnlyList<EnemyController> enemies;

        public MeleeTakedown(GridMap grid, MeleeConfig config, float detectGauge, IReadOnlyList<EnemyController> enemies)
        {
            this.grid = grid;
            this.config = config;
            this.detectGauge = detectGauge;
            this.enemies = enemies;
        }

        // F를 눌렀을 때 제압할 적이 있으면 제압하고 true
        public bool Tick(bool pressed, Vector2 playerPos, CharacterView view)
        {
            if (!pressed) return false;
            EnemyController target = FindTarget(playerPos);
            if (target == null) return false;

            view.PlayMelee();

            // 남은 체력과 방탄을 합친 만큼의 피해를 한 번에 줘서 즉사시킨다. 소음 이벤트는 보내지 않는다
            EventBus.Publish(new DamageEvent
            {
                targetId = target.Target.Id,
                amount = target.Health.Hp + target.Health.Armor,
                attackerId = EntityIds.Player,
                hitPoint = target.Target.Position,
                kind = DamageKind.Melee,
                time = Time.time,
            });
            return true;
        }

        // 조건을 만족하는 적 중 가장 가까운 하나. 없으면 null
        EnemyController FindTarget(Vector2 playerPos)
        {
            EnemyController best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyController enemy = enemies[i];
                if (!enemy.Target.Alive) continue;
                if (!MeleeRules.CanTakedown(grid, playerPos, enemy.Target.Position, enemy.Facing, enemy.State, enemy.Gauge, detectGauge, config))
                    continue;
                float distance = Vector2.Distance(playerPos, enemy.Target.Position);
                if (distance < bestDistance)
                {
                    best = enemy;
                    bestDistance = distance;
                }
            }
            return best;
        }
    }
}
