using System;
using System.Collections;
using UnityEngine;

namespace GameAi
{
    // 스모크 시나리오 선택. -scenario phase3(기존) / ai(적 AI 동작 확인)
    public sealed partial class SmokeRunner
    {
        string scenario = "phase3";

        IEnumerator RunScenario()
        {
            Log("시나리오: " + scenario);
            if (scenario == "ai") yield return RunAi();
            else yield return Run();
        }

        // 적이 순찰하고, 소리에 조사하고, 보면 추격·사격하고, 놓치면 수색 후 순찰로 돌아가는지(Phase 5 완료 조건)
        IEnumerator RunAi()
        {
            int transitions = 0, detections = 0, barks = 0, playerHits = 0;
            Action<EnemyStateChangedEvent> onState = e =>
            {
                transitions++;
                Log($"  전이 t={e.time:F1} 적{e.enemyId} {e.from}→{e.to} ({e.reason})");
            };
            Action<DetectionEvent> onDetect = e => detections++;
            Action<BarkEvent> onBark = e => { barks++; Log($"  대사 적{e.enemyId} \"{e.text}\" [{e.emotion}/{e.source}]"); };
            Action<DamageEvent> onDamage = e => { if (e.targetId == EntityIds.Player) playerHits++; };
            EventBus.Subscribe(onState);
            EventBus.Subscribe(onDetect);
            EventBus.Subscribe(onBark);
            EventBus.Subscribe(onDamage);

            // 플레이어를 적에게서 먼 시작점에 둔 채 순찰을 지켜본다
            Vector2[] start = new Vector2[game.Enemies.Count];
            for (int i = 0; i < start.Length; i++) start[i] = game.Enemies[i].Position;
            yield return new WaitForSeconds(8f);
            int moved = 0;
            for (int i = 0; i < start.Length; i++)
            {
                float d = Vector2.Distance(start[i], game.Enemies[i].Position);
                Log($"적{game.Enemies[i].Id} 8초 이동 {d:F1}m, 상태 {game.Enemies[i].State}");
                if (d > 1f) moved++;
            }
            Check("적이 순찰로 움직임(2명 이상)", moved >= 2);

            // 소리: 적 뒤쪽 5m에서 권총을 벽 쪽으로 쏜다
            EnemyController target = game.Enemies[0];
            Vector2 behind = FindFree(target.Position - target.Facing * 5f, target.Position);
            game.Player.SetPosition(behind);
            Aim(behind - target.Facing * 3f);
            yield return Frames(3);
            scripted.State.FirePressed = true;
            yield return new WaitForSeconds(1.2f);
            Log($"총소리 뒤 적{target.Id} 상태 {target.State}, 의심도 {target.Suspicion.Value:F0}");
            Check("총소리에 조사·추격으로 반응", target.State != EnemyState.Patrol);
            yield return Shot("ai_01_heard");

            // 목격: 적 정면 3m에 서 있는다
            int hitsBefore = playerHits;
            Vector2 front = FindFree(target.Position + target.Facing * 3f, target.Position);
            game.Player.SetPosition(front);
            Aim(target.Position);
            float waited = 0f;
            while (waited < 4f && target.State != EnemyState.Chase && target.State != EnemyState.Engage)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            Log($"정면 노출 {waited:F1}초 뒤 적{target.Id} 상태 {target.State}, 게이지 {target.Gauge:F0}");
            Check("정면에서 발견해 추격·교전", target.State == EnemyState.Chase || target.State == EnemyState.Engage);
            Check("발견 이벤트", detections > 0);
            yield return new WaitForSeconds(2.5f);
            yield return Shot("ai_02_engage");
            Log($"교전 중 플레이어 피격 {playerHits - hitsBefore}회, 체력 {game.Player.Health.Hp}/{game.Player.Health.Armor}");
            Check("적이 플레이어를 쏨", playerHits > hitsBefore);

            // 놓침: 멀리 숨으면 수색 후 순찰
            bool searched = false, returned = false;
            Action<EnemyStateChangedEvent> watch = e =>
            {
                if (e.enemyId != target.Id) return;
                if (e.to == EnemyState.Search) searched = true;
                if (searched && e.to == EnemyState.Patrol) returned = true;
            };
            EventBus.Subscribe(watch);
            game.Player.SetPosition(new Vector2(3f, 3f));
            float t = 0f;
            while (t < 30f && !returned && game.Player.Target.Alive)
            {
                t += Time.deltaTime;
                yield return null;
            }
            EventBus.Unsubscribe(watch);
            Log($"숨은 뒤 {t:F1}초: 수색 {searched}, 순찰 복귀 {returned}, 상태 {target.State}");
            Check("놓치면 수색", searched);
            Check("수색 후 순찰 복귀", returned);

            Log($"전이 {transitions}회, 발견 {detections}회, 대사 {barks}회");
            Check("대사가 떴음", barks > 0);
            Log("성능: " + game.Perf.FrameSummary());
            Log("성능: " + game.Perf.SectionSummary());

            EventBus.Unsubscribe(onState);
            EventBus.Unsubscribe(onDetect);
            EventBus.Unsubscribe(onBark);
            EventBus.Unsubscribe(onDamage);
        }

        // 원하는 위치 근처에서 플레이어가 설 수 있는 칸(기준점까지 시야가 트인 곳)
        Vector2 FindFree(Vector2 want, Vector2 lookAt)
        {
            GridMap grid = game.Grid;
            for (float r = 0f; r < 4f; r += 0.5f)
            {
                for (int k = 0; k < 16; k++)
                {
                    Vector2 p = want + MathUtil.FromAngleDeg(k * 22.5f) * r;
                    if (PlayerMotor.PushOutOfCells(grid, p, 0.4f) != p) continue;
                    if (!LineOfSight.IsClear(grid, p, lookAt, RayMask.Sight)) continue;
                    return p;
                }
            }
            return want;
        }
    }
}
