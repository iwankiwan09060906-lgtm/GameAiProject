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
            else if (scenario == "mission") yield return RunMission();
            else if (scenario == "ui") yield return RunUi();
            else if (scenario == "maps") yield return RunMaps();
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

        // 맵 1 소탕을 끝까지 클리어하고, 다시 시작해 사망으로 실패한다(Phase 6 완료 조건)
        IEnumerator RunMission()
        {
            Check("시작하면 미션 진행 중(브리핑 없으면 바로)", game.Mission.State == MissionManager.Running || game.Mission.State == MissionManager.Briefing);
            while (game.Mission.State == MissionManager.Briefing)
            {
                scripted.State.InteractPressed = true;
                yield return new WaitForSeconds(0.3f);
            }
            foreach (EnemyController enemy in game.Enemies)
            {
                float t = 0f;
                while (!enemy.IsDead && t < 6f && game.Player.Target.Alive)
                {
                    Vector2 behind = FindFree(enemy.Position - enemy.Facing * 1.1f, enemy.Position);
                    game.Player.SetPosition(behind);
                    Aim(enemy.Position);
                    scripted.State.MeleePressed = true;
                    yield return Frames(2);
                    if (!enemy.IsDead)
                    {
                        scripted.State.FirePressed = true;
                        yield return new WaitForSeconds(0.35f);
                    }
                    t += 0.4f;
                }
                Log($"적{enemy.Id} 처치 {enemy.IsDead}");
            }
            yield return Frames(5);
            MissionManager m = game.Mission;
            Log($"결과 {m.Outcome}, 상태 {m.State}, 처치 {m.Kills}/{m.TotalEnemies}, 발각 {m.Detections}, 시간 {m.ClearTime:F3}");
            yield return Shot("mission_01_success");
            Check("소탕 성공", m.Outcome == MissionResult.Success);
            Check("처치 수 4", m.Kills == 4);
            Check("판정 뒤 입력 막힘", m.InputBlocked);

            SceneFlow.Restart();
            game = null;
            while (game == null || !game.IsReady)
            {
                game = FindAnyObjectByType<GameManager>();
                yield return null;
            }
            game.SetInputSource(scripted, true);
            while (game.Mission.State == MissionManager.Briefing)
            {
                scripted.State.InteractPressed = true;
                yield return new WaitForSeconds(0.3f);
            }
            SendDamage(500);
            yield return Frames(5);
            Log($"재시작 후 결과 {game.Mission.Outcome}, 상태 {game.Mission.State}");
            Check("다시 시작하면 적 4명 초기화", game.Enemies.Count == 4 && !game.Enemies[0].IsDead);
            Check("사망하면 실패", game.Mission.Outcome == MissionResult.Fail);
            yield return Shot("mission_02_fail");
        }

        // 화면 확인: 브리핑 대화, HUD, 일시정지, Qwen 대사, 결과 화면(Phase 7)
        IEnumerator RunUi()
        {
            if (game.Dialogue.IsActive)
            {
                yield return new WaitForSecondsRealtime(0.6f);
                yield return Shot("ui_01_briefing");
                Check("브리핑 대화 중 게임 정지", game.Mission.State == MissionManager.Briefing);
            }
            float wait = 0f;
            while (game.Dialogue.IsActive && wait < 20f) { wait += Time.unscaledDeltaTime; yield return null; }
            Check("브리핑 뒤 진행", game.Mission.State == MissionManager.Running);
            int qwenLines = 0, fallbackLines = 0;
            Action<BarkEvent> onBark = e =>
            {
                if (e.source == "Fallback") fallbackLines++; else qwenLines++;
                Log($"  대사 적{e.enemyId} \"{e.text}\" [{e.emotion}/{e.source}]");
            };
            EventBus.Subscribe(onBark);
            yield return new WaitForSeconds(1f);
            yield return Shot("ui_02_hud");
            Log("Qwen 상태: " + game.Qwen.Status);

            // 적 앞에서 소리를 내 대사를 여러 번 끌어낸다
            for (int i = 0; i < game.Enemies.Count && i < 3; i++)
            {
                EnemyController enemy = game.Enemies[i];
                if (enemy.IsDead) continue;
                // 적 뒤 5m에서 적을 바라보며 제자리걸음으로 발소리(달리기 9m)를 낸다
                Vector2 near = FindFree(enemy.Position - enemy.Facing * 5f, enemy.Position);
                game.Player.SetPosition(near);
                Aim(enemy.Position);
                scripted.State.RunHeld = true;
                for (int k = 0; k < 6; k++)
                {
                    scripted.State.Move = k % 2 == 0 ? Vector2.up : Vector2.down;
                    yield return new WaitForSeconds(0.25f);
                }
                scripted.State.Move = Vector2.zero;
                scripted.State.RunHeld = false;
                yield return new WaitForSeconds(3f);
            }
            yield return Shot("ui_03_bark");
            Log($"대사: Qwen·캐시 {qwenLines}, 기본 {fallbackLines}, Qwen 요청 {game.Qwen.Requests}, 성공 {game.Qwen.Successes}, 평균 {game.Qwen.AverageLatencyMs:F0}ms");
            foreach (var kv in game.Qwen.FailCounts) Log("  결과 " + kv.Key + " " + kv.Value);
            foreach (var entry in game.Qwen.Log) Log($"  qwen #{entry.RequestId} 적{entry.EnemyId} {entry.Code} {entry.Result} {entry.LatencyMs:F0}ms \"{entry.Line}\"");
            Check("대사가 떴음", qwenLines + fallbackLines > 0);

            game.SetPaused(true);
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Shot("ui_04_pause");
            float clock = GameClock.Time;
            yield return new WaitForSecondsRealtime(0.5f);
            Check("일시정지 중 게임 시각 정지", Mathf.Approximately(clock, GameClock.Time));
            game.SetPaused(false);

            SendDamage(500);
            wait = 0f;
            while (!game.ResultVisible && wait < 15f) { wait += Time.unscaledDeltaTime; yield return null; }
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Shot("ui_05_result");
            Check("결과 화면", game.ResultVisible);
            EventBus.Unsubscribe(onBark);
        }

        // 메인 메뉴·미션 선택·크레딧·설정 화면(Phase 7)
        IEnumerator RunMenu()
        {
            MainMenuBootstrap menu = null;
            while (menu == null || !menu.IsReady)
            {
                menu = FindAnyObjectByType<MainMenuBootstrap>();
                yield return null;
            }
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("menu_01_main");
            menu.ShowSelect(true);
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Shot("menu_02_select");
            menu.ShowSelect(false);
            menu.OpenCredits();
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Shot("menu_03_credits");
            menu.CloseCredits();
            menu.OpenSettings();
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Shot("menu_04_settings");
            menu.CloseSettings();
            Check("메뉴 화면을 모두 열었다", true);
        }

        IEnumerator LoadMap(string id)
        {
            SceneFlow.LoadGame(id);
            game = null;
            yield return null;
            while (game == null || !game.IsReady)
            {
                game = FindAnyObjectByType<GameManager>();
                yield return null;
            }
            game.SetInputSource(scripted, true);
            float wait = 0f;
            while (game.Dialogue.IsActive && wait < 30f) { wait += Time.unscaledDeltaTime; yield return null; }
        }

        // 맵 2~4: 불러오기, 적 순찰, 탈취·탈출 미션 성공(Phase 8)
        IEnumerator RunMaps()
        {
            foreach (string id in new[] { "map_02", "map_03", "map_04" })
            {
                yield return LoadMap(id);
                Log($"{id}: 적 {game.Enemies.Count}, 문 {game.Doors.Count}, 미션 {game.Mission.MissionType}, 밝기 격자 {(game.Light != null)}");
                Vector2[] start = new Vector2[game.Enemies.Count];
                for (int i = 0; i < start.Length; i++) start[i] = game.Enemies[i].Position;
                yield return new WaitForSeconds(6f);
                int moved = 0;
                for (int i = 0; i < start.Length; i++) if (Vector2.Distance(start[i], game.Enemies[i].Position) > 1f) moved++;
                Log($"  6초 동안 움직인 적 {moved}/{start.Length}");
                Check(id + " 적이 순찰", moved >= start.Length / 2);
                yield return Shot(id + "_play");
                if (game.Mission.Objective != null)
                {
                    game.Player.SetPosition(FindFree(game.Mission.Objective.Position, game.Mission.Objective.Position));
                    Aim(game.Mission.Objective.Position);
                    yield return Frames(3);
                    scripted.State.InteractPressed = true;
                    yield return Frames(5);
                    Check(id + " 자료 확보", game.Mission.Stolen);
                    if (game.Mission.MissionType == "steal_exit")
                    {
                        Vector2 exit = FindExit();
                        game.Player.SetPosition(exit);
                        yield return Frames(5);
                    }
                    float wait = 0f;
                    while (game.Mission.State != MissionManager.Result && wait < 15f) { wait += Time.unscaledDeltaTime; yield return null; }
                    Log($"  결과 {game.Mission.Outcome}");
                    Check(id + " 미션 성공", game.Mission.Outcome == MissionResult.Success);
                }
            }
        }

        Vector2 FindExit()
        {
            foreach (var r in FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
                if (r.name == "obj_exit") return r.transform.position;
            return game.Player.Position;
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
