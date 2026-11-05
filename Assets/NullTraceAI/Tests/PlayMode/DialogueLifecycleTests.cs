// 실제 Unity의 비활성화와 일시정지는 수동 Tick 검사만으로 확인할 수 없습니다.
// 이 검사는 플레이 루프를 흘려 OnDisable/Update가 늦은 모델 결과와 출력 보류를 처리하는지 확인합니다.
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NullTrace.AI.Tests
{
    public sealed class PlayModeDialogueProvider : MonoBehaviour, IEnemyDialogueProvider
    {
        // 취소 요청을 받아도 완료할 수 있는 제공자로, 응답이 뒤늦게 오는 외부 서비스 상황을 만듭니다.
        public readonly TaskCompletionSource<string> Response = new TaskCompletionSource<string>();
        public CancellationToken Token { get; private set; }
        public int Calls { get; private set; }
        public Task<string> GenerateAsync(EnemyDialogueContext context, CancellationToken cancellationToken)
        { Calls++; Token = cancellationToken; return Response.Task; }
    }

    public sealed class DialogueLifecycleTests
    {
        private GameObject root;
        private EnemyDialogue dialogue;
        private PlayModeDialogueProvider provider;
        private readonly List<string> lines = new List<string>();
        private float originalScale;

        [SetUp]
        public void SetUp()
        {
            originalScale = Time.timeScale;
            Time.timeScale = 1;
            root = new GameObject("DialogueLifecycle");
            provider = root.AddComponent<PlayModeDialogueProvider>();
            dialogue = root.AddComponent<EnemyDialogue>();
            dialogue.Provider = provider;
            lines.Clear();
            dialogue.LineReady += lines.Add;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = originalScale;
            // UI 취소만으로 실제 생성 Task가 끝난 것은 아닙니다. 테스트 제공자도 정리 때 실제 완료시킵니다.
            foreach (var pending in root.GetComponentsInChildren<PlayModeDialogueProvider>(true))
                pending.Response.TrySetCanceled();
            Object.DestroyImmediate(root);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ActualDisableCancelsRequestAndDiscardsLateResponse()
        {
            dialogue.Request(new EnemyDialogueContext(1, 1, "표적 확인", "기본 대사"));
            yield return null;
            dialogue.enabled = false;
            Assert.That(provider.Token.IsCancellationRequested, Is.True, "OnDisable must cancel the provider.");
            provider.Response.SetResult("늦은 대사");
            yield return null;
            dialogue.enabled = true;
            yield return null;
            Assert.That(dialogue.IsPending, Is.False);
            Assert.That(lines, Is.Empty, "Reactivation must not revive cancelled output.");
        }

        [UnityTest]
        public IEnumerator ActualPauseHoldsCompletedOutputUntilResume()
        {
            dialogue.Request(new EnemyDialogueContext(2, 1, "수색 중", "기본 대사"));
            Time.timeScale = 0;
            provider.Response.SetResult("수색을 계속한다");
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.That(lines, Is.Empty);
            Assert.That(dialogue.IsPending, Is.True);
            Time.timeScale = 1;
            yield return null; yield return null;
            Assert.That(lines, Is.EqualTo(new[] { "수색을 계속한다" }));
        }

        [UnityTest]
        public IEnumerator PausedTimeoutUsesOneFallbackAndLateResultCannotSpeakAgain()
        {
            dialogue.TimeoutSeconds = 0.05f;
            dialogue.Request(new EnemyDialogueContext(3, 1, "복귀 중", "복귀한다"));
            Time.timeScale = 0;
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(lines, Is.Empty, "Real-time timeout must not speak while game time is paused.");
            Assert.That(provider.Token.IsCancellationRequested, Is.True);
            Time.timeScale = 1;
            yield return null; yield return null;
            Assert.That(lines, Is.EqualTo(new[] { "복귀한다" }));
            provider.Response.SetResult("기한 지난 응답");
            yield return null; yield return null;
            Assert.That(lines.Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator GenerationLimitUsesFallbackAndCancelledWorkKeepsSlotUntilActuallyFinished()
        {
            int previousLimit = EnemyDialogue.MaxConcurrentGenerations;
            var providers = new List<PlayModeDialogueProvider>();
            var dialogues = new List<EnemyDialogue>();
            try
            {
                Assert.That(EnemyDialogue.ConcurrentGenerationCount, Is.Zero, "Earlier tests must finish their provider tasks instead of resetting the global counter.");
                EnemyDialogue.MaxConcurrentGenerations = 2;
                for (int i = 0; i < 5; i++)
                {
                    var go = new GameObject("ContendingDialogue" + i); go.transform.SetParent(root.transform);
                    var fake = go.AddComponent<PlayModeDialogueProvider>(); providers.Add(fake);
                    var output = go.AddComponent<EnemyDialogue>(); dialogues.Add(output);
                    output.Provider = fake; output.LineReady += lines.Add;
                    output.Request(new EnemyDialogueContext(i + 10, 1, "동시 발견", "기본 경계 대사"));
                }
                yield return null;
                Assert.That(providers[0].Calls + providers[1].Calls, Is.EqualTo(2));
                Assert.That(providers[2].Calls + providers[3].Calls + providers[4].Calls, Is.Zero);
                Assert.That(lines.Count, Is.EqualTo(3), "Overflow must fall back without starting more model work.");
                Assert.That(EnemyDialogue.ConcurrentGenerationCount, Is.EqualTo(2));
                dialogues[0].Cancel(); dialogues[1].Cancel();
                Assert.That(EnemyDialogue.ConcurrentGenerationCount, Is.EqualTo(2), "Cancellation must not hide providers that are still running.");
                providers[0].Response.SetResult("취소 뒤 늦은 결과");
                float deadline = Time.realtimeSinceStartup + 1;
                while (EnemyDialogue.ConcurrentGenerationCount != 1 && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(EnemyDialogue.ConcurrentGenerationCount, Is.EqualTo(1));
                dialogues[2].Request(new EnemyDialogueContext(12, 2, "새 상황", "기본 경계 대사"));
                Assert.That(providers[2].Calls, Is.EqualTo(1), "A physically finished task must release capacity for a new request.");
                Assert.That(EnemyDialogue.ConcurrentGenerationCount, Is.EqualTo(2));
                Assert.That(lines.Count, Is.EqualTo(3), "The cancelled late result must not be spoken.");
            }
            finally
            {
                foreach (var output in dialogues) output.Cancel();
                foreach (var fake in providers) fake.Response.TrySetCanceled();
                EnemyDialogue.MaxConcurrentGenerations = previousLimit;
            }
        }
    }
}
