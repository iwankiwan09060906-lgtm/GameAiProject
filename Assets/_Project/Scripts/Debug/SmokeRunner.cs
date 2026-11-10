using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace GameAi
{
    // 빌드를 `-smoke <폴더>`로 실행하면 정해진 조작을 재생해 판정 결과와 스크린샷을 남기고 스스로 종료한다.
    // 사람 입력 없이 실제 화면과 게임 흐름을 확인하는 용도다. 일반 실행에서는 아무 일도 하지 않는다
    public sealed partial class SmokeRunner : MonoBehaviour
    {
        // 키보드 대신 정해 둔 입력을 돌려주는 입력원. 한 번 읽히면 "누른 순간" 값은 꺼진다
        sealed class ScriptedInput : IInputSource
        {
            public PlayerInputState State;

            public PlayerInputState Read(Camera camera)
            {
                PlayerInputState current = State;
                State.FirePressed = false;
                State.InteractPressed = false;
                State.DecoyPressed = false;
                State.NightVisionPressed = false;
                State.MeleePressed = false;
                State.ReloadPressed = false;
                State.WeaponSelect = 0;
                return current;
            }
        }

        readonly List<string> report = new List<string>();
        readonly List<string> hitchLines = new List<string>();
        readonly ScriptedInput scripted = new ScriptedInput();
        string outputDir;
        GameManager game;
        int failures;

        // 씬이 불린 직후 실행 인자에 -smoke가 있을 때만 스스로를 설치한다
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-smoke");
            if (index < 0 || index + 1 >= args.Length) return;

            var go = new GameObject("SmokeRunner");
            DontDestroyOnLoad(go);
            var runner = go.AddComponent<SmokeRunner>();
            runner.outputDir = args[index + 1];
            int scenarioIndex = Array.IndexOf(args, "-scenario");
            runner.scenario = scenarioIndex >= 0 && scenarioIndex + 1 < args.Length ? args[scenarioIndex + 1] : "phase3";
        }

        // 0.025초(40fps 미만)를 넘긴 프레임을 직전 로그 줄과 함께 남긴다
        void Update()
        {
            if (Time.unscaledDeltaTime <= 0.025f) return;
            string stage = report.Count > 0 ? report[report.Count - 1] : "시작 전";
            hitchLines.Add("끊김 " + (Time.unscaledDeltaTime * 1000f).ToString("F0") + "ms, 프레임 " + Time.frameCount + ", 직전 단계: " + stage);
        }

        IEnumerator Start()
        {
            Directory.CreateDirectory(outputDir);
            if (scenario == "menu")
            {
                yield return RunMenu();
                File.WriteAllLines(Path.Combine(outputDir, "smoke_report.txt"), report);
                Application.Quit(failures == 0 ? 0 : 1);
                yield break;
            }
            while (game == null || !game.IsReady)
            {
                game = FindAnyObjectByType<GameManager>();
                yield return null;
            }

            game.SetInputSource(scripted, true);
            yield return RunScenario();

            report.AddRange(hitchLines);
            File.WriteAllLines(Path.Combine(outputDir, "smoke_report.txt"), report);

            // 하나라도 실패하면 종료 코드 1
            Application.Quit(failures == 0 ? 0 : 1);
        }

        // 전체 시나리오. 순서대로 실행한다
        IEnumerator Run()
        {
            PlayerController player = game.Player;
            GridMap grid = game.Grid;
            Log("시작 위치 " + player.Position + ", 맵 " + grid.Width + "x" + grid.Height + ", 적 " + game.Enemies.Count + ", 문 " + game.Doors.Count);
            Check("적 4명", game.Enemies.Count == 4);

            Aim(new Vector2(10f, 9f));
            yield return Frames(5);
            yield return Shot("01_yard_start");

            // 걷기 속도: 1초에 약 3.5m
            Vector2 before = player.Position;
            scripted.State.Move = Vector2.right;
            yield return new WaitForSeconds(1f);
            scripted.State.Move = Vector2.zero;
            float walked = Vector2.Distance(before, player.Position);
            Log("걷기 1초 이동 거리 " + walked.ToString("F2"));
            Check("걷기 속도", walked > 2.8f && walked < 4.2f);

            // 벽을 뚫지 못한다. 바깥 벽(x 0 ~ 0.5m)에서 몸 반지름만큼 떨어져 멈춘다
            scripted.State.Move = Vector2.left;
            yield return new WaitForSeconds(4f);
            scripted.State.Move = Vector2.zero;
            Log("서쪽 벽에서 멈춘 위치 " + player.Position.x.ToString("F2"));
            Check("마당 서쪽 벽에서 멈춤", player.Position.x > 0.85f && player.Position.x < 1.0f);

            yield return DoorAndRoomTest();
            yield return CombatTest();
            yield return MeleeTest();
            yield return WallBlockTest();
            yield return WeaponTest();

            Log("탄피·혈흔 " + game.Fx.DecalCount + ", 시체 " + game.Fx.CorpseCount);
            Check("전투 흔적이 남음", game.Fx.DecalCount > 0 && game.Fx.CorpseCount >= 2);

            // 성능 측정은 위치와 탄약을 바꾸므로 다른 시험이 모두 끝난 뒤에 한다
            yield return PerfTest();
            yield return PlayerDamageTest();
        }

        // 불 켜진 방 안에서 달리며 사격하는 동안 프레임 시간과 게임 코드 시간, 메모리 할당을 잰다.
        // 프레임은 길고 게임 코드가 짧으면 끊김의 원인은 게임 밖(전원·수직동기·GPU)이다
        IEnumerator PerfTest()
        {
            // 조명·소품이 가장 많은 서버실 가운데
            game.Player.SetPosition(new Vector2(32.25f, 21f));
            scripted.State.RunHeld = true;
            yield return Frames(30);

            game.Perf.Reset();
            long memoryBefore = GC.GetTotalMemory(false);
            int collectionsBefore = GC.CollectionCount(0);
            for (int i = 0; i < 300; i++)
            {
                // 원을 그리며 달려 시야 방향과 위치가 계속 바뀌게 한다
                float angle = i * 0.08f;
                scripted.State.Move = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Aim(game.Player.Position + new Vector2(Mathf.Cos(angle * 2f), Mathf.Sin(angle * 2f)) * 5f);

                // 소총을 들고 있으므로 누르고 있는 동안 연사된다. 30프레임마다 10프레임씩 쏜다
                scripted.State.FireHeld = i % 30 < 10;
                yield return null;
            }
            scripted.State.RunHeld = false;
            scripted.State.FireHeld = false;
            scripted.State.Move = Vector2.zero;

            long allocated = GC.GetTotalMemory(false) - memoryBefore;
            Log("성능: " + game.Perf.FrameSummary());
            Log("성능: " + game.Perf.SectionSummary());
            Log("성능: 300프레임 동안 GC 수집 " + (GC.CollectionCount(0) - collectionsBefore) + "회, 힙 변화 " + allocated / 1024 + "KB");
            Log("성능: vSync " + QualitySettings.vSyncCount + ", 목표 프레임 " + Application.targetFrameRate);
        }

        // 마당 북쪽 문(폭 3칸) 앞에서 문을 열고, 복도를 지나며 탐색 기억을 확인한다
        IEnumerator DoorAndRoomTest()
        {
            GridMap grid = game.Grid;
            Vector2Int doorCell = grid.WorldToCell(new Vector2(6.75f, 12.25f));
            Door door = game.Doors.First(d => d.Area.Contains(doorCell));
            Check("이어진 D 칸이 문 하나(폭 1.5m)", door.Area.width * grid.CellSize > 1.4f);
            game.Player.SetPosition(new Vector2(6.75f, 11.5f));
            Aim(new Vector2(6.75f, 20f));
            yield return Frames(3);
            yield return Shot("02_door_closed_prompt");
            Check("문 앞 안내가 뜸", game.Prompt.CurrentAction == "열기");
            Check("닫힌 문이 이동을 막음", grid.BlocksMove(doorCell.x, doorCell.y));

            scripted.State.InteractPressed = true;
            yield return Frames(3);
            Check("Space로 열림", door.IsOpen);
            Check("열린 문은 이동 가능", !grid.BlocksMove(doorCell.x, doorCell.y));
            Check("안내가 닫기로 바뀜", game.Prompt.CurrentAction == "닫기");
            yield return Shot("03_door_open");

            // 복도를 지나가며 본 뒤 반대쪽을 바라보면 지나온 칸이 기억으로 남아야 한다
            game.Player.SetPosition(new Vector2(14f, 14f));
            Aim(new Vector2(26f, 14f));
            yield return Frames(3);
            Aim(new Vector2(2f, 14f));
            yield return Frames(3);
            yield return Shot("04_corridor_memory");
            Vector2Int seen = grid.WorldToCell(new Vector2(24f, 14f));
            Vector2Int never = grid.WorldToCell(new Vector2(3f, 25f));
            Check("지나온 칸이 기억으로 남음", game.Fog.GetState(seen.x, seen.y) == FogState.Memory);
            Check("가 본 적 없는 칸은 미탐색", game.Fog.GetState(never.x, never.y) == FogState.Unexplored);
        }

        // 서버실 문을 열고 들어가 서버실 경비병을 사격한다
        IEnumerator CombatTest()
        {
            Vector2Int doorCell = game.Grid.WorldToCell(new Vector2(32.25f, 15.75f));
            Door door = game.Doors.First(d => d.Area.Contains(doorCell));
            game.Player.SetPosition(new Vector2(32.25f, 15.0f));
            Aim(new Vector2(32.25f, 26f));
            yield return Frames(3);
            scripted.State.InteractPressed = true;
            yield return Frames(2);
            Check("서버실 문 열림", door.IsOpen);

            EnemyController guard = game.Enemies.First(e => e.name.Contains("guard") && e.Target.Position.x > 30f && e.Target.Position.y > 20f);
            game.Player.SetPosition(guard.Target.Position + new Vector2(-4f, 0f));
            Aim(guard.Target.Position);
            yield return Frames(5);
            Check("시야 안 적은 보임", game.Vision.IsVisible(guard.Target.Position));

            // 권총 한 발: 피해 20, 탄창 1 감소
            int hp = guard.Health.Hp;
            int magBefore = game.Weapons.Inventory.Current.Mag;
            scripted.State.FirePressed = true;
            yield return Frames(2);
            yield return Shot("05_shot_flash");
            Check("권총 피해 20", hp - guard.Health.Hp == 20);
            Check("탄창 1 감소", game.Weapons.Inventory.Current.Mag == magBefore - 1);

            // 발사 간격(0.3초)을 두고 두 발 더 쏴서 50 체력을 모두 깎는다
            for (int i = 0; i < 2; i++)
            {
                yield return new WaitForSeconds(0.35f);
                scripted.State.FirePressed = true;
                yield return Frames(2);
            }
            yield return Frames(3);
            Check("세 발에 경비병 사망", guard.Health.IsDead);
            Check("사망 후 대상에서 빠짐", !guard.Target.Alive);
            yield return Frames(12);
            yield return Shot("06_corpse_blood");
        }

        // 사무실 경비병은 등 뒤에서만 제압된다
        IEnumerator MeleeTest()
        {
            EnemyController enemy = game.Enemies.First(e => e.name.Contains("guard") && e.Target.Alive && e.Target.Position.y < 15f);
            Vector2 front = enemy.Target.Position + enemy.Facing * 1f;
            Vector2 back = enemy.Target.Position - enemy.Facing * 1f;

            game.Player.SetPosition(front);
            Aim(enemy.Target.Position);
            yield return Frames(2);
            scripted.State.MeleePressed = true;
            yield return Frames(3);
            Check("정면 근접은 실패", enemy.Target.Alive);

            game.Player.SetPosition(back);
            Aim(enemy.Target.Position);
            yield return Frames(2);
            scripted.State.MeleePressed = true;
            yield return Frames(3);
            Check("뒤쪽 근접 제압 성공", !enemy.Target.Alive);
            yield return Frames(3);
            yield return Shot("07_takedown");
        }

        // 벽 너머 소총병(엘리베이터 홀)은 창고에서 보이지도 맞지도 않는다
        IEnumerator WallBlockTest()
        {
            EnemyController rifleman = game.Enemies.First(e => e.name.Contains("rifleman"));
            game.Player.SetPosition(new Vector2(12.5f, rifleman.Target.Position.y));
            Aim(rifleman.Target.Position);
            yield return Frames(3);
            Check("벽 너머 적은 보이지 않음", !game.Vision.IsVisible(rifleman.Target.Position));

            int hp = rifleman.Health.Hp;
            scripted.State.WeaponSelect = 3;
            yield return new WaitForSeconds(0.4f);
            scripted.State.FireHeld = true;
            yield return new WaitForSeconds(0.5f);
            scripted.State.FireHeld = false;
            yield return Frames(3);
            Check("벽 너머 소총 사격은 맞지 않음", rifleman.Health.Hp == hp);
            Check("소총 연사로 탄이 줄어듦", game.Weapons.Inventory.Current.Mag <= 26);
        }

        // 재장전 시작 → 전환으로 취소(잔탄 유지) → 다시 재장전해서 완료
        IEnumerator WeaponTest()
        {
            WeaponInventory inventory = game.Weapons.Inventory;
            WeaponSlot rifle = inventory.Current;
            int mag = rifle.Mag;
            int reserve = rifle.Reserve;

            scripted.State.ReloadPressed = true;
            yield return Frames(2);
            Check("재장전 시작", inventory.IsReloading);
            scripted.State.WeaponSelect = 1;
            yield return Frames(2);
            Check("전환으로 재장전 취소", !inventory.IsReloading);
            Check("취소해도 잔탄 유지", rifle.Mag == mag && rifle.Reserve == reserve);

            yield return new WaitForSeconds(0.4f);
            scripted.State.WeaponSelect = 3;
            yield return new WaitForSeconds(0.4f);
            scripted.State.ReloadPressed = true;
            yield return new WaitForSeconds(2.7f);
            Check("재장전 완료", rifle.Mag == rifle.Def.mag_size);
        }

        // 방탄 → 체력 순서와 사망 1회를 확인한다. 적 공격은 Phase 5에서 생기므로 피해를 직접 보낸다
        IEnumerator PlayerDamageTest()
        {
            int deaths = 0;
            Action<DeathEvent> onDeath = e => { if (e.isPlayer) deaths++; };
            EventBus.Subscribe(onDeath);

            Health health = game.Player.Health;
            SendDamage(60);
            yield return Frames(2);
            Check("방탄 먼저, 넘친 피해는 체력으로", health.Armor == 0 && health.Hp == 90);

            // 이미 죽은 뒤에 또 피해가 와도 DeathEvent는 한 번만 나와야 한다
            SendDamage(200);
            yield return Frames(2);
            SendDamage(200);
            yield return Frames(2);
            Check("플레이어 사망은 한 번만", deaths == 1 && health.IsDead);

            Vector2 before = game.Player.Position;
            scripted.State.Move = Vector2.right;
            yield return new WaitForSeconds(0.2f);
            scripted.State.Move = Vector2.zero;
            Check("사망 후 입력을 막음", game.Player.Position == before);
            EventBus.Unsubscribe(onDeath);
        }

        void SendDamage(int amount)
        {
            EventBus.Publish(new DamageEvent
            {
                targetId = EntityIds.Player,
                amount = amount,
                attackerId = 1,
                hitPoint = game.Player.Position,
                kind = DamageKind.Bullet,
                time = Time.time,
            });
        }

        void Aim(Vector2 world)
        {
            scripted.State.AimWorld = world;
        }

        static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
                yield return null;
        }

        // 프레임 렌더링이 끝난 뒤 화면을 저장한다
        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(outputDir, name + ".png"));
            yield return null;
        }

        void Check(string label, bool ok)
        {
            if (!ok) failures++;
            Log((ok ? "[통과] " : "[실패] ") + label);
        }

        void Log(string line)
        {
            report.Add(line);
            Debug.Log("[Smoke] " + line);
        }
    }
}
