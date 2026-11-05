// 역할: 샌드박스에서 키보드/버튼 입력을 AI가 이해하는 위치·조도·소음으로 바꾸고 결과를 화면에 보여 줍니다.
// 연결: SandboxBuilder가 Player, Agent, Noise, World, Door, UI 텍스트를 저장 씬에 연결합니다.
// EnemyAgent가 판단을 담당하고, 이 파일은 테스트 입력과 표시만 담당합니다. 팀의 실제 플레이어 코드는 아닙니다.
// 읽는 순서: Awake(처음 준비) → OnEnable/OnDisable(알림 연결) → Update(매 프레임 입력/표시)
// → 아래 공개 메서드(키와 UI 버튼이 함께 쓰는 명령). EnemyDebugView는 같은 AI 결과를 색과 선으로 보여 줍니다.
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NullTrace.AI.Demo
{
    // 체력·무기·미션 없이 AI를 시험하기 위한 임시 조작기입니다.
    // 실제 게임에서는 팀 플레이어에 EnemyTarget을 붙이고 필요한 순간 NoiseBus.Publish를 호출하면 됩니다.
    public sealed class SandboxController : MonoBehaviour
    {
        // Inspector에 연결되는 씬 참조입니다. Player/Agent/Noise/World/Door는 생성기가 반드시 연결합니다.
        // Status/Events는 화면 표시용이므로 아래에서 존재할 때만 갱신합니다.
        public EnemyTarget Player;
        public EnemyAgent Agent;
        // A는 순찰, B는 초소 경비입니다. 각자의 기억·무전 도착 시각을 같은 화면에서 비교합니다.
        public EnemyAgent Partner;
        public NoiseBus Noise;
        public GridWorld2D World;
        public GameObject Door;
        public AiDoor2D DoorControl;
        public TMP_Text Status;
        public TMP_Text Events;
        // 초당 월드 단위입니다. 프레임당 이동 거리는 Update에서 Time.deltaTime을 곱해 계산합니다.
        public float MoveSpeed = 4f;
        public bool Hidden { get; private set; }
        public int AttackCount { get; private set; }
        public int SpeechCount { get; private set; }
        public string LastSpeech { get; private set; } = "";
        // 시작 위치는 R 초기화에 사용합니다. nextFootstep은 '다음 발소리가 허용되는 게임 시각(초)'입니다.
        private Vector3 playerStart;
        private Vector3 agentStart;
        private Vector3 partnerStart;
        private float nextFootstep;
        private SpriteRenderer playerVisual;
        private SpriteRenderer doorVisual;
        private string doorNotice = "";
        private TMP_FontAsset koreanFont;

        // Unity가 이 컴포넌트를 처음 준비할 때 호출합니다. 시작 좌표와 외형 참조를 한 번 보관합니다.
        // 입력은 Inspector 연결값이며, 결과는 초기화에 쓸 기준점과 한글을 표시할 임시 폰트입니다.
        private void Awake()
        {
            // 다른 창을 보거나 창 없는 검증을 해도 시뮬레이션이 계속 진행되게 합니다.
            Application.runInBackground = true;
            playerStart = Player.transform.position;
            agentStart = Agent.transform.position;
            if (Partner) partnerStart = Partner.transform.position;
            playerVisual = Player.GetComponentInChildren<SpriteRenderer>();
            doorVisual = Door.GetComponent<SpriteRenderer>();
            if (doorVisual && DoorControl) doorVisual.enabled = !DoorControl.IsOpen;
            // 폰트 파일을 배포물에 복사하지 않고 현재 컴퓨터에 설치된 한글 폰트를 순서대로 찾습니다.
            // 하나를 찾으면 충분합니다. 없을 때는 Update의 안내 문구를 쓰고 실제 대사는 LastSpeech에 보관합니다.
            foreach (string family in new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR" })
            {
                koreanFont = TMP_FontAsset.CreateFontAsset(family, "Regular", 48);
                if (koreanFont) break;
            }
            if (koreanFont && Events) Events.font = koreanFont;
            if (koreanFont && Status) Status.font = koreanFont;
        }

        // 씬을 닫거나 이 오브젝트가 삭제될 때 실행됩니다. Awake에서 직접 만든 폰트 자원만 정리합니다.
        // atlas는 글자 모양을 저장한 이미지입니다. 공유된 프로젝트 폰트가 아니라 이 시연 전용 자원입니다.
        private void OnDestroy()
        {
            if (!koreanFont) return;
            foreach (Texture2D atlas in koreanFont.atlasTextures) if (atlas) Destroy(atlas);
            Destroy(koreanFont.material);
            Destroy(koreanFont);
        }

        // 활성화될 때마다 AI의 출력 이벤트를 구독합니다. '+='는 알림을 받을 함수를 등록한다는 뜻입니다.
        // 공격/대사가 실제로 요청된 순간을 받아야 단순히 상태가 Combat인 프레임 수와 혼동하지 않습니다.
        private void OnEnable()
        {
            Agent.AttackRequested += OnAttack;
            Agent.SpeechRequested += OnSpeech;
            if (Partner) { Partner.AttackRequested += OnAttack; Partner.SpeechRequested += OnSpeech; }
            if (DoorControl) DoorControl.StateChanged += OnDoorChanged;
        }

        // 비활성화·씬 종료 시 등록했던 함수를 뺍니다. 그래야 다시 켰을 때 한 요청을 두 번 세지 않습니다.
        // 씬 종료 순서상 Agent가 먼저 삭제될 수 있으므로 존재 여부도 검사합니다.
        private void OnDisable()
        {
            if (Agent) { Agent.AttackRequested -= OnAttack; Agent.SpeechRequested -= OnSpeech; }
            if (Partner) { Partner.AttackRequested -= OnAttack; Partner.SpeechRequested -= OnSpeech; }
            if (DoorControl) DoorControl.StateChanged -= OnDoorChanged;
        }

        // position은 AI가 공격하고 싶다고 알린 XY 조준점입니다. 여기서는 횟수만 세며 피해를 주지 않습니다.
        private void OnAttack(Vector2 position) => AttackCount++;
        // AI가 간격 제한까지 통과시켜 보낸 실제 문장을 저장합니다. UI는 이 마지막 문장을 계속 표시합니다.
        private void OnSpeech(string text) { SpeechCount++; LastSpeech = text; }

        // 활성 상태에서 Unity가 매 프레임 호출합니다. 입력을 적용한 뒤 현재 AI 상태를 읽어 표시합니다.
        // 여기서 State/Detection 등을 결정하지 않습니다. 판단은 별도 EnemyAgent의 Update/Tick에서 합니다.
        private void Update()
        {
            var keyboard = Keyboard.current;
            // 배치 테스트처럼 키보드 장치가 없는 환경에서도 AI와 상태 표시는 계속 작동해야 합니다.
            if (keyboard != null)
            {
                // 오른쪽-왼쪽, 위쪽-아래쪽을 각각 계산합니다. 반대 방향 키를 동시에 누르면 그 축은 0입니다.
                Vector2 input = new Vector2(
                    (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0) -
                    (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0),
                    (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0) -
                    (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0));
                if (!Hidden && input.sqrMagnitude > 0)
                {
                    // 대각선 (1,1)을 그대로 쓰면 더 빨라집니다. normalized로 길이를 1로 맞춘 뒤
                    // '초당 속도 × 지난 프레임의 초'를 곱해 프레임률이 달라도 비슷한 속도로 움직입니다.
                    Vector2 step = input.normalized * (MoveSpeed * Time.deltaTime);
                    Vector2 position = Player.transform.position;
                    // 끝 위치만 검사하면 느린 프레임에 얇은 벽을 뛰어넘을 수 있습니다.
                    // GridWorld2D.TryMove가 이동 구간 전체를 검사하고 out position에 실제 도달 위치를 줍니다.
                    // X, Y를 나누면 한 축이 벽에 막혀도 다른 축으로 벽을 따라 움직일 수 있습니다.
                    Vector2 horizontal = position + new Vector2(step.x, 0);
                    World.TryMove(position, horizontal, out position);
                    Vector2 vertical = position + new Vector2(0, step.y);
                    World.TryMove(position, vertical, out position);
                    Player.transform.position = position;
                    // 이동 입력이 있는 동안 0.45초 간격으로 작은 소음을 냅니다. 매 프레임 발행하지 않습니다.
                    // 반경 3은 월드 거리, 0.65는 0~1의 소리 크기입니다. 실제 청취 여부는 AI가 판단합니다.
                    if (Time.time >= nextFootstep)
                    {
                        Noise.Publish(position, 3f, 0.65f, NoiseKind.Footstep);
                        nextFootstep = Time.time + 0.45f;
                    }
                }
                // wasPressedThisFrame은 누른 첫 프레임만 참입니다. 키를 누르고 있어 문이 계속 뒤집히는 일을 막습니다.
                // 버튼도 아래의 같은 공개 메서드를 부르므로 입력 방식에 따라 결과가 달라지지 않습니다.
                if (keyboard.spaceKey.wasPressedThisFrame) EmitNoise();
                if (keyboard.digit1Key.wasPressedThisFrame) SetLight(0);
                if (keyboard.digit2Key.wasPressedThisFrame) SetLight(1);
                if (keyboard.digit3Key.wasPressedThisFrame) SetLight(2);
                if (keyboard.hKey.wasPressedThisFrame) ToggleHidden();
                if (keyboard.eKey.wasPressedThisFrame) ToggleDoor();
                if (keyboard.rKey.wasPressedThisFrame) ResetSandbox();
                if (keyboard.pKey.wasPressedThisFrame) StartPatrol();
            }
            // 키보드 유무와 관계없이 화면을 갱신합니다. '마지막 단서'는 현재 플레이어 위치와 다를 수 있습니다.
            // 시간·프레임·적 좌표를 함께 표시해 정지 화면이 아닌 실제 실행 상태를 확인할 수 있습니다.
            if (Status)
                Status.text = Describe("A / circle", Agent) + (Partner ? "\n" + Describe("B / square", Partner) : "") +
                    $"\n{Player.Light} / hidden {Hidden} / door {DoorStatus()} {doorNotice}\n" +
                    $"t {Time.timeSinceLevelLoad:F1}s / frame {Time.frameCount}";
            // 요청 횟수와 실제 대사를 표시할 뿐, 공격/대사를 새로 발생시키지는 않습니다.
            if (Events)
                Events.text = $"Attacks {AttackCount} / speech {SpeechCount}\n" +
                    (koreanFont ? LastSpeech : $"Speech text: see LastSpeech in Inspector/log (Korean font unavailable)");
        }

        // Detection은 눈으로 대상을 식별하는 0~1 누적량, Suspicion은 사건별 증거 합계 0~100입니다.
        // Episode의 남은 시간은 목표가 바뀌어도 같은 마감에서 계산합니다. 표시가 AI 상태를 수정하지 않습니다.
        private static string Describe(string label, EnemyAgent npc) =>
            $"{label}: {npc.CurrentGoal} / {npc.CurrentDuty}\n" +
            $"Detection {npc.Detection:F2}/1 / Suspicion {npc.Suspicion:F1}/100\n" +
            $"Episode {npc.EpisodeId} {npc.PursuitPhase} / {System.Math.Max(0, npc.EpisodeDeadline - npc.GameTime):F1}s\n" +
            $"Anchor {npc.EpisodeAnchor:F1} / next {npc.NextPatrolIndex} / 약한 단서 대기 {npc.IsRecoveryProtected}\n" +
            $"Memory {npc.Memory.Count} / radio {npc.RadioReceivedCount} / {npc.GoalReason}";

        // 유한 순찰의 다음 회차는 외부 명령으로만 시작합니다. 진행 중 요청을 거절하는 판단은 Agent가 담당합니다.
        public void StartPatrol() => Agent.StartPatrol();

        // 현재 플레이어 위치를 복사해 큰 충돌음을 발행합니다. 그 후 플레이어가 움직여도 이 소리의 위치는 고정됩니다.
        public void EmitNoise() => Noise.Publish(Player.transform.position, 12f, 1f, NoiseKind.Impact);
        // UI에서 받은 정수를 0~2로 제한한 뒤 3단계 조도 값으로 전달합니다. 실제 조명 밝기를 바꾸는 코드는 아닙니다.
        public void SetLight(int level) => Player.Light = (LightLevel)Mathf.Clamp(level, 0, 2);

        // H/버튼용 테스트 명령입니다. EnemyTarget만 끄므로 위치와 시연 외형은 남고 AI가 대상을 보지 못하게 됩니다.
        // 죽음(IsAlive=false)이나 대상 교체와는 별개 입력이며, AI가 마지막 단서를 수색하는지 시험할 수 있습니다.
        public void ToggleHidden()
        {
            Hidden = !Hidden;
            Player.enabled = !Hidden;
            if (playerVisual) playerVisual.color = Hidden ? new Color(0.2f, 0.55f, 0.6f, 0.25f) : new Color(0.25f, 0.95f, 1f);
        }

        // 문 연결부가 잠금과 NPC 끼임을 검사하고 물리 상태·격자를 함께 갱신합니다.
        // GameObject를 끄면 AiDoor2D까지 비활성화되므로 문 그림/충돌만 연결부의 상태에 따라 바꿉니다.
        public void ToggleDoor()
        {
            if (!DoorControl) { doorNotice = "Rebuild sandbox"; return; }
            if (!DoorControl.TrySetOpen(!DoorControl.IsOpen))
                doorNotice = DoorControl.IsLocked ? "LOCKED" : "NPC in doorway";
        }

        private string DoorStatus() => !DoorControl ? "UNCONNECTED" :
            DoorControl.IsLocked ? "LOCKED" : DoorControl.IsOpen ? "OPEN" : "CLOSED";

        // 사람의 E 조작뿐 아니라 NPC가 직접 문을 연 경우도 동일한 표시·문소리를 사용합니다.
        private void OnDoorChanged(AiDoor2D changed)
        {
            doorNotice = "";
            if (doorVisual) doorVisual.enabled = !changed.IsOpen;
            Noise.Publish(changed.transform.position, 7f, 0.8f, NoiseKind.Door);
        }

        // 같은 장면을 반복 시험하도록 위치·조도·문·AI 기억·요청 카운터를 시작 조건으로 돌립니다.
        // 대상과 맵을 먼저 복구하고 마지막에 ResetAgent를 호출해야 새 시작 위치/장애물로 판단을 시작합니다.
        public void ResetSandbox()
        {
            if (Hidden) ToggleHidden();
            Player.IsAlive = true;
            Player.Light = LightLevel.Bright;
            Player.transform.position = playerStart;
            Agent.transform.position = agentStart;
            if (Partner) Partner.transform.position = partnerStart;
            if (DoorControl) { DoorControl.TrySetLocked(false); DoorControl.TrySetOpen(false); }
            doorNotice = "";
            Physics2D.SyncTransforms();
            World.Rebuild();
            Agent.SetTarget(Player);
            Agent.ResetAgent();
            if (Partner) { Partner.SetTarget(Player); Partner.ResetAgent(); }
            AttackCount = SpeechCount = 0;
            LastSpeech = "";
        }
    }
}
