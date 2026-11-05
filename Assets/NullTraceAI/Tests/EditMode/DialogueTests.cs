// 역할: 실제 언어 모델 없이 느린 응답·실패·중단·일시정지를 만들어 대사 경계를 확인한다.
// 긴 추론을 돌리는 테스트가 아니라, 결과를 원하는 순간에 완료하는 가짜 생성기를 사용한다.
// 이렇게 해야 팀 PC의 성능이나 모델 설치 여부 때문에 순찰 AI 검증 결과가 달라지지 않는다.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace NullTrace.AI.Tests
{
    public sealed class DialogueTestProvider : MonoBehaviour, IEnemyDialogueProvider
    {
        public readonly Queue<Task<string>> Responses = new Queue<Task<string>>();
        public CancellationToken LastToken;
        public bool ThrowOnRequest;
        public int Calls;

        public Task<string> GenerateAsync(EnemyDialogueContext context, CancellationToken cancellationToken)
        {
            Calls++;
            LastToken = cancellationToken;
            if (ThrowOnRequest) throw new InvalidOperationException("가짜 생성기 실패");
            return Responses.Count > 0 ? Responses.Dequeue() : null;
        }
    }

    public sealed class DialogueTests
    {
        private GameObject owner;
        private EnemyDialogue dialogue;
        private DialogueTestProvider provider;
        private List<string> spoken;
        private float originalTimeScale;
        private int originalGlobalLimit;
        private readonly List<TaskCompletionSource<string>> pendingTasks = new List<TaskCompletionSource<string>>();

        [SetUp]
        public void SetUp()
        {
            originalTimeScale = Time.timeScale;
            originalGlobalLimit = EnemyDialogue.MaxConcurrentGenerations;
            EnemyDialogue.MaxConcurrentGenerations = 2;
            Time.timeScale = 1f;
            owner = new GameObject("대사 테스트");
            dialogue = owner.AddComponent<EnemyDialogue>();
            provider = owner.AddComponent<DialogueTestProvider>();
            spoken = new List<string>();
            dialogue.LineReady += spoken.Add;
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(owner);
            // Cancel은 자원을 억지로 해제하지 않는다. 가짜 제공자의 실제 Task도 끝내 다음 검사로 넘기지 않는다.
            foreach (var pending in pendingTasks) pending.TrySetCanceled();
            pendingTasks.Clear();
            EnemyDialogue.MaxConcurrentGenerations = originalGlobalLimit;
            Time.timeScale = originalTimeScale;
        }

        private TaskCompletionSource<string> Pending()
        {
            var task = new TaskCompletionSource<string>(); pendingTasks.Add(task); return task;
        }
        private EnemyDialogue OtherDialogue(string name, Task<string> task, out DialogueTestProvider otherProvider)
        {
            var go = new GameObject(name); go.transform.SetParent(owner.transform);
            var other = go.AddComponent<EnemyDialogue>();
            otherProvider = go.AddComponent<DialogueTestProvider>(); otherProvider.Responses.Enqueue(task);
            other.Provider = otherProvider; return other;
        }

        private void Request(int action = 1) => dialogue.Request(new EnemyDialogueContext(1, action, "문소리를 들었다", "확인하겠다."));

        [Test]
        public void MissingProvider_UsesFallbackOnceWithoutBlocking()
        {
            Request();
            dialogue.Tick(20f, false);
            CollectionAssert.AreEqual(new[] { "확인하겠다." }, spoken);
            Assert.That(dialogue.IsPending, Is.False);
        }

        [Test]
        public void CompletedProvider_UsesGeneratedLine()
        {
            dialogue.Provider = provider;
            provider.Responses.Enqueue(Task.FromResult("문 쪽을 확인한다."));
            Request();
            CollectionAssert.AreEqual(new[] { "문 쪽을 확인한다." }, spoken);
        }

        [Test]
        public void Timeout_UsesFallbackAndIgnoresLateCompletion()
        {
            var pending = Pending();
            dialogue.Provider = provider;
            provider.Responses.Enqueue(pending.Task);
            Request();
            dialogue.Tick(1.9f, false);
            Assert.That(spoken, Is.Empty);
            dialogue.Tick(0.11f, false);
            Assert.That(provider.LastToken.IsCancellationRequested, Is.True);
            pending.SetResult("이미 지난 상황");
            dialogue.Tick(1f, false);
            CollectionAssert.AreEqual(new[] { "확인하겠다." }, spoken);
        }

        [Test]
        public void Replacement_DoesNotDeliverPreviousRequest()
        {
            var old = Pending();
            var current = Pending();
            dialogue.Provider = provider;
            provider.Responses.Enqueue(old.Task);
            provider.Responses.Enqueue(current.Task);
            Request(1);
            CancellationToken oldToken = provider.LastToken;
            Request(2);
            old.SetResult("이전 대사");
            dialogue.Tick(0.1f, false);
            Assert.That(oldToken.IsCancellationRequested, Is.True);
            Assert.That(spoken, Is.Empty);
            current.SetResult("현재 대사");
            dialogue.Tick(0f, false);
            CollectionAssert.AreEqual(new[] { "현재 대사" }, spoken);
        }

        [Test]
        public void Cancel_DropsPreparedLineDuringPause()
        {
            Time.timeScale = 0f;
            Request();
            Assert.That(dialogue.IsPending, Is.True);
            Assert.That(spoken, Is.Empty);
            dialogue.Cancel();
            dialogue.Tick(0f, false);
            Assert.That(spoken, Is.Empty);
        }

        [Test]
        public void Pause_StillTimesOutButWaitsForResumeToSpeak()
        {
            dialogue.Provider = provider;
            provider.Responses.Enqueue(Pending().Task);
            Request();
            dialogue.Tick(3f, true);
            Assert.That(spoken, Is.Empty);
            Assert.That(dialogue.IsPending, Is.True);
            dialogue.Tick(0f, false);
            CollectionAssert.AreEqual(new[] { "확인하겠다." }, spoken);
        }

        [Test]
        public void Cancel_DropsPendingGenerationEvenWhenItCompletesLater()
        {
            var task = Pending();
            dialogue.Provider = provider;
            provider.Responses.Enqueue(task.Task);
            Request();
            // EditMode는 일반 MonoBehaviour의 OnDisable 실행을 보장하지 않는다.
            // 여기서는 취소 계약을 직접 검사하고, 실제 비활성화 콜백은 PlayMode에서 검증한다.
            dialogue.Cancel();
            task.SetResult("늦은 대사");
            dialogue.Tick(0.1f, false);
            Assert.That(provider.LastToken.IsCancellationRequested, Is.True);
            Assert.That(dialogue.IsPending, Is.False);
            Assert.That(spoken, Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ProviderFailure_FallsBackWithoutThrowing(bool synchronous)
        {
            dialogue.Provider = provider;
            provider.ThrowOnRequest = synchronous;
            if (!synchronous) provider.Responses.Enqueue(Task.FromException<string>(new Exception("실패")));
            Assert.DoesNotThrow(() => Request());
            CollectionAssert.AreEqual(new[] { "확인하겠다." }, spoken);
            Assert.That(dialogue.LastFailure, Is.Not.Empty);
        }

        [Test]
        public void EmptyResponse_UsesFallback()
        {
            dialogue.Provider = provider;
            provider.Responses.Enqueue(Task.FromResult("  \n "));
            Request();
            CollectionAssert.AreEqual(new[] { "확인하겠다." }, spoken);
        }

        [Test]
        public void InvalidElapsedTime_DoesNotExpireRequest()
        {
            var task = Pending();
            dialogue.Provider = provider;
            provider.Responses.Enqueue(task.Task);
            Request();
            dialogue.Tick(float.NaN, false);
            dialogue.Tick(-1f, false);
            dialogue.Tick(float.PositiveInfinity, false);
            Assert.That(spoken, Is.Empty);
            task.SetResult("정상 결과");
            dialogue.Tick(0f, false);
            CollectionAssert.AreEqual(new[] { "정상 결과" }, spoken);
        }
        [Test]
        public void GlobalLimitFallsBackWithoutCallingThirdProvider()
        {
            var firstTask = Pending(); var secondTask = Pending(); var thirdTask = Pending();
            dialogue.Provider = provider; provider.Responses.Enqueue(firstTask.Task); Request();
            var second = OtherDialogue("두 번째 NPC", secondTask.Task, out _);
            var third = OtherDialogue("세 번째 NPC", thirdTask.Task, out var thirdProvider);
            var lines = new List<string>(); third.LineReady += lines.Add;
            second.Request(new EnemyDialogueContext(1, 1, "경계", "기본2"));
            third.Request(new EnemyDialogueContext(1, 1, "경계", "기본3"));
            Assert.That(EnemyDialogue.ConcurrentGenerationCount, Is.EqualTo(2));
            Assert.That(thirdProvider.Calls, Is.Zero); CollectionAssert.AreEqual(new[] { "기본3" }, lines);
            firstTask.SetResult("완료1");
            third.Request(new EnemyDialogueContext(1, 2, "다음 상황", "기본3"));
            Assert.That(thirdProvider.Calls, Is.EqualTo(1)); Assert.That(EnemyDialogue.ConcurrentGenerationCount, Is.EqualTo(2));
        }
        [Test]
        public void CancelAndTimeoutKeepSlotUntilUnderlyingTaskActuallyFinishes()
        {
            EnemyDialogue.MaxConcurrentGenerations = 1;
            var stalled = Pending(); var next = Pending();
            dialogue.Provider = provider; provider.Responses.Enqueue(stalled.Task); Request();
            dialogue.Cancel(); Assert.That(EnemyDialogue.ConcurrentGenerationCount, Is.EqualTo(1));
            provider.Responses.Enqueue(next.Task); Request(2);
            Assert.That(provider.Calls, Is.EqualTo(1)); Assert.That(spoken, Is.EqualTo(new[] { "확인하겠다." }));
            stalled.SetException(new InvalidOperationException("취소를 무시한 작업의 늦은 실패"));
            Assert.That(EnemyDialogue.ConcurrentGenerationCount, Is.Zero);
            Request(3); dialogue.Tick(3, false);
            Assert.That(provider.Calls, Is.EqualTo(2)); Assert.That(EnemyDialogue.ConcurrentGenerationCount, Is.EqualTo(1));
            next.SetResult("너무 늦은 결과"); dialogue.Tick(0, false);
            Assert.That(EnemyDialogue.ConcurrentGenerationCount, Is.Zero);
            Assert.That(spoken, Is.EqualTo(new[] { "확인하겠다.", "확인하겠다." }));
        }
        [Test]
        public void NullThrowAndAlreadyCompletedTasksReturnTheirGlobalSlots()
        {
            EnemyDialogue.MaxConcurrentGenerations = 1; dialogue.Provider = provider;
            Request(); Assert.That(EnemyDialogue.ConcurrentGenerationCount, Is.Zero);
            provider.ThrowOnRequest = true; Request(2); Assert.That(EnemyDialogue.ConcurrentGenerationCount, Is.Zero);
            provider.ThrowOnRequest = false; provider.Responses.Enqueue(Task.FromResult("완료")); Request(3);
            Assert.That(EnemyDialogue.ConcurrentGenerationCount, Is.Zero); Assert.That(provider.Calls, Is.EqualTo(3));
        }
    }
}

