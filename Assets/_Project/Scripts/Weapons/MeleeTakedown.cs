using System.Collections.Generic;
using UnityEngine;

namespace GameAi
{
    // F 키 근접 제압. 소음이 없고 즉시 사망시킨다(7.5)
    public sealed class MeleeTakedown
    {
        readonly GridMap grid;
        readonly MeleeConfig config;
        readonly float detectGauge;
        readonly IReadOnlyList<EnemyController> enemies;

        public MeleeTakedown(GridMap grid, MeleeConfig config, float detectGauge, IReadOnlyList<EnemyController> enemies)
        {
            this.grid = grid;
            this.config = config;
            this.detectGauge = detectGauge;
            this.enemies = enemies;
        }

        public bool Tick(bool pressed, Vector2 playerPos, CharacterView view)
        {
            if (!pressed) return false;
            EnemyController target = FindTarget(playerPos);
            if (target == null) return false;

            view.PlayMelee();
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

        EnemyController FindTarget(Vector2 playerPos)
        {
            EnemyController best = null;
            float bestDistance = float.MaxValue;
            foreach (var enemy in enemies)
            {
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
