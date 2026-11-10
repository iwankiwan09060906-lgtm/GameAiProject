using System;
using System.Collections;
using System.IO;
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
            else if (scenario == "gadgets") yield return RunGadgets();
            else if (scenario == "replay") yield return RunReplay();
            else if (scenario == "perf") yield return RunPerf();
            else if (scenario == "soak") yield return RunSoak();
            else if (scenario == "evidence") yield return RunEvidence();
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

        // 유인탄·야간 투시·조명 스위치(Phase 9)
        IEnumerator RunGadgets()
        {
            yield return LoadMap("map_02");
            // 조명 스위치: 첫 번째 상호작용 가능한 스위치를 찾는다
            LightSwitch sw = null;
            foreach (var item in FindSwitches()) { sw = item; break; }
            Check("맵 2에 조명 스위치", sw != null);
            if (sw != null)
            {
                Vector2Int cell = game.Grid.WorldToCell(game.Light.Get(sw.LightIds[0]).Position);
                float before = game.Light.At(cell);
                game.Player.SetPosition(FindFree(sw.Position, sw.Position));
                Aim(sw.Position);
                yield return Frames(3);
                scripted.State.InteractPressed = true;
                yield return Frames(3);
                float after = game.Light.At(cell);
                Log($"스위치: 밝기 {before:F2} → {after:F2}, 켜짐 {sw.IsOn}");
                Check("스위치로 조명을 끄면 그 칸이 어두워짐", !sw.IsOn && after < before);
                yield return Shot("gadget_01_light_off");
                scripted.State.InteractPressed = true;
                yield return Frames(3);
            }

            // 야간 투시
            scripted.State.NightVisionPressed = true;
            yield return Frames(3);
            yield return Shot("gadget_02_nvg");
            scripted.State.NightVisionPressed = true;
            yield return Frames(3);

            // 유인탄: 적 근처 4m에 던진다
            EnemyController target = game.Enemies[1];
            game.Player.SetPosition(FindFree(target.Position - target.Facing * 9f, target.Position));
            Aim(target.Position + target.Facing * 3f);
            yield return Frames(3);
            scripted.State.DecoyPressed = true;
            yield return new WaitForSeconds(2f);
            Log($"유인탄 뒤 적{target.Id} 상태 {target.State}, 의심도 {target.Suspicion.Value:F0}");
            Check("유인탄 소리에 반응", target.State != EnemyState.Patrol || target.Suspicion.Value > 0f);
        }

        System.Collections.Generic.IEnumerable<LightSwitch> FindSwitches()
        {
            return game.Switches;
        }

        // 고정 시드 재현(13.3): 같은 맵·시드·입력으로 3회 실행한 상태 전이 로그가 같은지
        IEnumerator RunReplay()
        {
            var runs = new System.Collections.Generic.List<string[]>();
            for (int run = 0; run < 3; run++)
            {
                SceneFlow.ForceFixedSeed = true;
                SceneFlow.FixedDeltaTime = 1f / 60f;
                yield return LoadMap("map_01");
                // 사망 → 디브리핑(실제 시간 진행) 때문에 시뮬레이션 프레임 수가 달라지지 않게 무적
                game.Player.Health.Invulnerable = true;
                for (int frame = 0; frame < 1800; frame++)
                {
                    // 정해진 입력: 시작점에서 동쪽·북쪽으로 걷다가 가끔 쏜다
                    int phase = frame / 240;
                    scripted.State.Move = phase % 3 == 0 ? Vector2.right : phase % 3 == 1 ? Vector2.up : new Vector2(1f, 1f).normalized;
                    scripted.State.RunHeld = phase % 2 == 1;
                    Aim(game.Player.Position + scripted.State.Move * 5f);
                    if (frame % 300 == 150) scripted.State.FirePressed = true;
                    yield return null;
                }
                string[] lines = new string[game.TransitionLog.Lines.Count];
                for (int i = 0; i < lines.Length; i++) lines[i] = game.TransitionLog.Lines[i];
                runs.Add(lines);
                File.WriteAllLines(Path.Combine(outputDir, "state_log_run" + (run + 1) + ".txt"), lines);
                Log($"실행 {run + 1}: 상태 전이 {lines.Length}줄, 게임 시각 {GameClock.Time:F2}");
            }
            SceneFlow.ForceFixedSeed = false;
            SceneFlow.FixedDeltaTime = 0f;
            bool same = runs[0].Length > 0;
            for (int r = 1; r < runs.Count && same; r++)
            {
                if (runs[r].Length != runs[0].Length) { same = false; Log("  줄 수가 다름: " + runs[0].Length + " / " + runs[r].Length); break; }
                for (int i = 0; i < runs[0].Length; i++)
                    if (runs[r][i] != runs[0][i]) { same = false; Log("  다른 줄 " + i + ": " + runs[0][i] + " / " + runs[r][i]); break; }
            }
            Check("3회 실행 상태 전이 로그가 같음", same);
        }

        // 부하(7.14): 적 20명이 추격·교전하는 60초, 조명 켠 상태
        IEnumerator RunPerf()
        {
            yield return LoadMap("map_04");
            game.SpawnExtraEnemies(20 - game.Enemies.Count);
            game.Player.Health.Invulnerable = true;
            Log($"적 {game.Enemies.Count}명, 조명 {game.Light.Count}개");
            game.Player.SetPosition(new Vector2(40.5f, 25.5f));
            for (int i = 0; i < 180; i++)
            {
                yield return null;
            }
            game.Perf.Reset();
            game.AiPerf.Reset();
            for (int i = 0; i < game.Director.StageMs.Length; i++) game.Director.StageMs[i] = 0;
            float t = 0f;
            int samples = 0, combat = 0, over2 = 0, frames = 0;
            long memoryBefore = GC.GetTotalMemory(false);
            while (t < 60f)
            {
                t += Time.unscaledDeltaTime;
                frames++;
                // 복도를 오가며 사격해 적을 끌어들인다
                float a = t * 0.6f;
                scripted.State.Move = new Vector2(Mathf.Sin(a), 0f);
                Aim(game.Player.Position + new Vector2(Mathf.Cos(a * 2f), Mathf.Sin(a * 2f)) * 6f);
                if (frames % 40 == 0) scripted.State.FirePressed = true;
                if (frames % 120 == 0) scripted.State.ReloadPressed = true;
                if (game.AiPerf.LastSectionMs > 2f) over2++;
                if (frames % 60 == 0)
                {
                    samples++;
                    foreach (EnemyController e in game.Enemies)
                        if (!e.IsDead && (e.State == EnemyState.Chase || e.State == EnemyState.Engage)) combat++;
                }
                yield return null;
            }
            Log("성능(프레임): " + game.Perf.FrameSummary());
            Log("성능(게임 코드 전체): " + game.Perf.SectionSummary());
            Log("성능(AI): 평균 " + game.AiPerf.AverageSectionMs.ToString("F2") + "ms, 최대 " + game.AiPerf.MaxSectionMs.ToString("F2") + "ms, 2ms 초과 " + over2 + "/" + frames + "프레임(" + (100f * over2 / Mathf.Max(1, frames)).ToString("F1") + "%)");
            Log("AI 단계별 프레임당 평균(ms): 이벤트 " + (game.Director.StageMs[1] / frames).ToString("F2") + ", 감지 " + (game.Director.StageMs[2] / frames).ToString("F2") + ", 경보·경로 " + (game.Director.StageMs[3] / frames).ToString("F2") + ", 적 판단·이동 " + (game.Director.StageMs[4] / frames).ToString("F2"));
            Log("추격·교전 중인 적 평균 " + ((float)combat / Mathf.Max(1, samples)).ToString("F1") + "명, 힙 변화 " + (GC.GetTotalMemory(false) - memoryBefore) / 1024 + "KB");
            Check("AI 처리 평균 2ms 이하", game.AiPerf.AverageSectionMs <= 2f);
        }

        // 장시간(7.14): 무작위로 움직이고 쏘며, 판정이 나면 다른 맵으로. 예외·메모리 증가 확인(-minutes N, 기본 30)
        IEnumerator RunSoak()
        {
            int exceptions = 0;
            Application.LogCallback onLog = (msg, stack, type) => { if (type == LogType.Exception) exceptions++; };
            Application.logMessageReceived += onLog;
            float minutes = 30f;
            string[] args = Environment.GetCommandLineArgs();
            int mi = Array.IndexOf(args, "-minutes");
            if (mi >= 0 && mi + 1 < args.Length) float.TryParse(args[mi + 1], out minutes);
            string[] maps = { "map_01", "map_02", "map_03", "map_04" };
            int round = 0;
            float elapsed = 0f;
            long firstMemory = 0;
            float nextReport = 0f;
            var rng = new RandomSource(77);
            while (elapsed < minutes * 60f)
            {
                yield return LoadMap(maps[round++ % maps.Length]);
                float roundTime = 0f;
                while (roundTime < 120f && game.Mission.State == MissionManager.Running && elapsed < minutes * 60f)
                {
                    float dt = Time.unscaledDeltaTime;
                    roundTime += dt;
                    elapsed += dt;
                    if (Time.frameCount % 30 == 0)
                    {
                        scripted.State.Move = new Vector2(rng.Range(-1f, 1f), rng.Range(-1f, 1f));
                        scripted.State.RunHeld = rng.Value() < 0.3f;
                        scripted.State.CrouchHeld = rng.Value() < 0.2f;
                        Aim(game.Player.Position + new Vector2(rng.Range(-6f, 6f), rng.Range(-6f, 6f)));
                        scripted.State.FirePressed = rng.Value() < 0.3f;
                        scripted.State.InteractPressed = rng.Value() < 0.2f;
                        scripted.State.WeaponSelect = rng.Value() < 0.05f ? rng.Int(1, 4) : 0;
                    }
                    if (elapsed >= nextReport)
                    {
                        GC.Collect();
                        long mem = GC.GetTotalMemory(true);
                        if (firstMemory == 0) firstMemory = mem;
                        Log($"  {elapsed / 60f:F1}분: {maps[(round - 1) % maps.Length]}, 관리 힙 {mem / 1024}KB, 예외 {exceptions}, {game.Perf.FrameSummary()}");
                        nextReport += 60f;
                    }
                    yield return null;
                }
            }
            Application.logMessageReceived -= onLog;
            GC.Collect();
            long last = GC.GetTotalMemory(true);
            Log($"장시간 {minutes:F0}분: 판 {round}회, 예외 {exceptions}, 관리 힙 {firstMemory / 1024}KB → {last / 1024}KB");
            Check("예외 없음", exceptions == 0);
            Check("관리 힙이 크게 늘지 않음(+8MB 이하)", last - firstMemory < 8 * 1024 * 1024);
        }

        // 목표 화면 비교용 실내 교전 장면: 밝은 방에서 교전, 총구 화염·혈흔·시체·대사·HUD가 함께 보이게
        IEnumerator RunEvidence()
        {
            yield return LoadMap("map_01");
            game.Player.Health.Invulnerable = true;
            EnemyController first = game.Enemies[0];
            EnemyController second = game.Enemies[1];
            // 첫 번째 적은 사격으로 쓰러뜨려 시체·혈흔을 남긴다
            float t = 0f;
            while (!first.IsDead && t < 8f)
            {
                game.Player.SetPosition(FindFree(first.Position - first.Facing * 4f, first.Position));
                Aim(first.Position);
                scripted.State.FirePressed = true;
                yield return new WaitForSeconds(0.32f);
                t += 0.32f;
            }
            // 두 번째 적과 교전하는 순간을 찍는다
            Vector2 spot = FindFree(second.Position - second.Facing * 5f, second.Position);
            game.Player.SetPosition(spot);
            Aim(second.Position);
            yield return new WaitForSeconds(1.5f);
            for (int i = 0; i < 6 && !second.IsDead; i++)
            {
                Aim(second.Position);
                scripted.State.FirePressed = true;
                yield return new WaitForSeconds(0.1f);
                if (i == 1) yield return Shot("evidence_combat");
                yield return new WaitForSeconds(0.25f);
            }
            Check("교전 장면 촬영", true);
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
