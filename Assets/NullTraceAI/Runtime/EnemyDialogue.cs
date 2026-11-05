// 역할: 이미 정해진 AI 상황을 문장으로 표현하는 선택적 생성기를 연결한다.
// 연결: EnemyAgent가 Request로 사실과 기본 문장을 전달하고, LineReady를 기존 SpeechRequested에 연결한다.
// 읽는 순서: Context(알려 줄 사실) → Request(요청 시작) → Tick(결과/시간 확인) → Cancel(지난 상황 폐기).
// 이 컴포넌트는 공격 여부, 의심도, 이동 목표를 결정하지 않는다. Qwen이 없어도 기본 문장으로 동작한다.
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace NullTrace.AI
{
    /// <summary>
    /// 대사 생성기에 전달하는 읽기 전용 상황 사본이다. Facts에는 이 NPC가 실제로 아는 사실만 넣는다.
    /// 예: "문소리를 들어 조사 중"은 가능하지만, 보지 못한 플레이어의 현재 좌표를 넣어서는 안 된다.
    /// EpisodeId/ActionId는 어떤 추적과 행동에서 요청했는지 식별하는 번호이며 판단 명령이 아니다.
    /// </summary>
    public readonly struct EnemyDialogueContext
    {
        public int EpisodeId { get; }
        public int ActionId { get; }
        public string Facts { get; }
        public string FallbackLine { get; }

        public EnemyDialogueContext(int episodeId, int actionId, string facts, string fallbackLine)
        {
            EpisodeId = episodeId;
            ActionId = actionId;
            Facts = facts ?? string.Empty;
            FallbackLine = fallbackLine ?? string.Empty;
        }
    }

    /// <summary>
    /// 나중에 로컬 Qwen 등을 연결할 때 구현할 약속이다. Inspector에는 이 인터페이스를 구현한
    /// MonoBehaviour를 Provider로 연결한다. 생성 결과는 대사 문자열 하나만 반환한다.
    /// GenerateAsync는 Task를 신속히 반환해야 한다. 오래 걸리는 추론을 Unity 주 스레드에서
    /// 동기 실행하면 이 컴포넌트의 시간 제한 검사도 멈추므로, 생성기에서 비동기로 처리해야 한다.
    /// </summary>
    public interface IEnemyDialogueProvider
    {
        Task<string> GenerateAsync(EnemyDialogueContext context, CancellationToken cancellationToken);
    }

    /// <summary>
    /// NPC당 하나의 현재 대사 요청만 유지한다. 새 요청은 이전 것을 취소하고 결과를 기다리지 않는다.
    /// Task는 작업 완료를 나타내는 표이고, Tick은 그 표를 살펴보므로 게임 화면을 멈추지 않는다.
    /// 결과 이벤트는 Unity 주 스레드의 Request/Tick에서만 발생한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyDialogue : MonoBehaviour
    {
        [Tooltip("선택 사항. IEnemyDialogueProvider를 구현한 컴포넌트. 없으면 즉시 기본 문장을 사용합니다.")]
        public MonoBehaviour Provider;

        [Tooltip("게임 일시정지와 무관하게 기다리는 실제 초. 초과하면 기본 문장으로 바꿉니다.")]
        public float TimeoutSeconds = 2f;

        // NPC별 대기 요청은 하나지만 취소를 무시한 생성기는 뒤에서 계속 일할 수 있다.
        // 이 전역 카운터는 UI 대기 수가 아니라 '제공자가 반환한 Task가 실제 끝나지 않은 수'를 센다.
        private static int concurrentGenerations;
        private static int maxConcurrentGenerations = 2;
        public static int MaxConcurrentGenerations
        {
            get => Volatile.Read(ref maxConcurrentGenerations);
            set => Volatile.Write(ref maxConcurrentGenerations, Math.Max(1, Math.Min(32, value)));
        }
        public static int ConcurrentGenerationCount => Volatile.Read(ref concurrentGenerations);

        // UI와 EnemyAgent가 이 소식을 구독한다. 생성기가 직접 AI 기억이나 목표를 고치는 통로는 없다.
        public event Action<string> LineReady;
        // 일시정지 때문에 준비된 문장을 아직 표시하지 못한 경우도 대기 중으로 센다.
        public bool IsPending => pending;
        public string LastFailure { get; private set; }

        private EnemyDialogueContext context;
        private Task<string> generation;
        private CancellationTokenSource cancellation;
        private float elapsed;
        private bool pending, prepared;
        private string preparedLine;
        private const int MaxLineLength = 160;

        private void Update() => Tick(Time.unscaledDeltaTime, Time.timeScale <= 0f);
        private void OnDisable() => Cancel();
        private void OnDestroy() => Cancel();

        /// <summary>
        /// 새 상황의 문장을 요청한다. 이전 요청은 번호나 내용이 같더라도 먼저 폐기한다.
        /// 생성기가 없다면 기본 대사가 즉시 준비되며, 일시정지가 아니면 같은 호출에서 표시한다.
        /// 기본 대사를 비워 두면 그 상황에서는 말하지 않는다. AI의 동작은 영향을 받지 않는다.
        /// </summary>
        public void Request(EnemyDialogueContext next)
        {
            Cancel();
            LastFailure = null;
            if (!isActiveAndEnabled) return;
            context = next;
            pending = true;
            elapsed = 0f;
            if (Provider && Provider.isActiveAndEnabled && Provider is IEnemyDialogueProvider provider)
            {
                if (!TryReserveGeneration()) UseFallback("전체 대사 생성 한도에 도달했습니다.");
                else
                {
                    cancellation = new CancellationTokenSource();
                    try
                    {
                        generation = provider.GenerateAsync(context, cancellation.Token);
                        if (generation == null)
                        {
                            Interlocked.Decrement(ref concurrentGenerations);
                            UseFallback("생성기가 작업 결과를 반환하지 않았습니다.");
                        }
                        else
                        {
                            // 취소/timeout/새 요청과 무관하게 실제 완료에서만 자리를 반환한다.
                            // 이미 끝난 Task는 즉시 실행하고, 버린 Task의 예외도 여기서 관찰한다.
                            generation.ContinueWith(completed =>
                            {
                                if (completed.IsFaulted) _ = completed.Exception;
                                Interlocked.Decrement(ref concurrentGenerations);
                            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                        }
                    }
                    catch (Exception error)
                    {
                        Interlocked.Decrement(ref concurrentGenerations);
                        // 선택적 생성기의 실패가 순찰·공격 같은 필수 AI 동작까지 중단시키면 안 된다.
                        UseFallback("대사 요청 실패: " + error.Message);
                    }
                }
            }
            else
            {
                // 생성기 미연결은 정상적인 사용 방법이다. 오류를 띄우지 않고 기본 대사를 사용한다.
                UseFallback(null);
            }
            Tick(0f, Time.timeScale <= 0f);
        }

        /// <summary>
        /// 흐른 실제 초를 받아 현재 요청의 결과를 확인한다. 테스트에서는 직접 호출할 수 있다.
        /// paused=true여도 제한 시간은 흐르지만, 준비된 문장을 화면에 내보내지는 않는다.
        /// 예: 정지 중 2초가 지났다면 기본 문장을 준비하고, 재개 때도 상황이 유지된 경우에만 내보낸다.
        /// 상황이 바뀌었다면 호출 측에서 Cancel 또는 새 Request를 사용해야 한다.
        /// </summary>
        public void Tick(float unscaledDeltaTime, bool paused)
        {
            if (!pending || !isActiveAndEnabled) return;
            if (AiMath.IsFinite(unscaledDeltaTime) && unscaledDeltaTime > 0f)
                elapsed += unscaledDeltaTime;
            float timeout = AiMath.IsFinite(TimeoutSeconds) && TimeoutSeconds > 0f ? TimeoutSeconds : 2f;

            if (!prepared)
            {
                // 제한 시간이 된 뒤 확인한 응답은 사용하지 않는다. 느린 답변 때문에 지난 상황을 말하지 않게 한다.
                if (elapsed >= timeout) UseFallback("대사 생성 제한 시간을 초과했습니다.");
                else if (generation != null && generation.IsCompleted)
                {
                    if (generation.Status == TaskStatus.RanToCompletion)
                    {
                        preparedLine = Normalize(generation.Result);
                        if (string.IsNullOrEmpty(preparedLine)) UseFallback("생성된 문장이 비어 있습니다.");
                        else { prepared = true; StopGeneration(); }
                    }
                    else
                    {
                        // Exception을 읽어 작업 오류를 관찰한다. 게임 콘솔에 예외를 다시 던지지는 않는다.
                        string reason = generation.IsFaulted ? generation.Exception?.GetBaseException().Message : "요청 취소";
                        UseFallback("대사 생성 실패: " + reason);
                    }
                }
            }

            if (!prepared || paused) return;
            string line = preparedLine;
            // 이벤트 수신자가 곧바로 다음 문장을 요청해도 이전 요청 정리가 새 요청을 지우지 않도록 먼저 비운다.
            pending = prepared = false;
            preparedLine = null;
            if (!string.IsNullOrEmpty(line)) LineReady?.Invoke(line);
        }

        /// <summary>
        /// 목표 변경·추적 종료·비활성화 때 호출한다. 준비된 문장까지 지우므로 나중에 표시되지 않는다.
        /// 생성기가 취소 신호를 무시하고 완료해도, 이전 Task를 더는 읽지 않으므로 결과가 새 요청에 섞이지 않는다.
        /// </summary>
        public void Cancel()
        {
            pending = prepared = false;
            preparedLine = null;
            elapsed = 0f;
            StopGeneration();
        }

        private void UseFallback(string reason)
        {
            LastFailure = reason;
            preparedLine = Normalize(context.FallbackLine);
            prepared = true;
            StopGeneration();
        }

        private void StopGeneration()
        {
            generation = null;
            if (cancellation == null) return;
            try { cancellation.Cancel(); }
            catch (AggregateException)
            {
                // 외부 생성기가 등록한 취소 콜백의 오류도 필수 게임 진행으로 전파하지 않는다.
                if (string.IsNullOrEmpty(LastFailure)) LastFailure = "생성기의 취소 처리에 오류가 있습니다.";
            }
            finally { cancellation.Dispose(); cancellation = null; }
        }

        private static bool TryReserveGeneration()
        {
            // 제공자가 즉시 다른 NPC의 요청을 만드는 재진입도 상한을 넘지 않도록 호출 전에 예약한다.
            int active = Volatile.Read(ref concurrentGenerations);
            while (active < MaxConcurrentGenerations)
            {
                int previous = Interlocked.CompareExchange(ref concurrentGenerations, active + 1, active);
                if (previous == active) return true;
                active = previous;
            }
            return false;
        }

        private static string Normalize(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return string.Empty;
            // 말풍선을 여러 줄의 긴 답변이 채우지 않도록 줄바꿈을 정리하고 길이에 상한을 둔다.
            line = line.Trim().Replace('\r', ' ').Replace('\n', ' ');
            return line.Length <= MaxLineLength ? line : line.Substring(0, MaxLineLength);
        }
    }
}
