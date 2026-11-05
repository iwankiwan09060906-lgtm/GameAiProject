// 11월 6일 피드백의 실제 플레이 회귀입니다. 같은 partial 클래스의 기존 픽스처와 정리 코드를 재사용합니다.
// Runtime의 비공개 상태를 강제로 바꾸지 않고, 소리·타겟·벽·실제 게임 시간을 입력해 결과를 관찰합니다.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NullTrace.AI.Tests
{
    public sealed partial class IntegrationTests
    {
        // 멀리 있는 첫 순찰점으로 아주 천천히 이동하게 합니다. 보호 해제 전에 다음 점에 도착할 수 없습니다.
        // 가까운 약한 소리는 제자리 확인만 시작하고, 짧은 추적 마감 뒤 이탈점 복귀와 보호로 이어집니다.
        private IEnumerator BeginSlowPatrolRecovery()
        {
            target.enabled = false;
            profile.MoveSpeed = 0.01f;
            profile.EpisodeDuration = 0.2f;
            profile.InspectDuration = 1;
            profile.RecoveryDuration = 2;
            profile.SuspicionDecayPerSecond = 0;
            var visit = Child("UnfinishedVisit").transform;
            visit.position = new Vector2(6, 0);
            agent.PatrolPoints = new[] { visit };
            agent.ResetAgent();
            noise.Publish(agent.transform.position, 4, 1, NoiseKind.Footstep);
            yield return Until(() => agent.IsRecoveryProtected, 2, "A short investigation must return and begin protection.");
            Assert.That(agent.NextPatrolIndex, Is.Zero);
            Assert.That(agent.EpisodeId, Is.Zero);
            // 뒤에서 새로 생길 추적은 보호 해제 여부를 관찰할 만큼 오래 유지합니다.
            profile.EpisodeDuration = 20;
        }

        [UnityTest]
        public IEnumerator WeakClueProtectionExpiresByTimeWithoutNextPatrolArrival()
        {
            yield return BeginSlowPatrolRecovery();
            double protectedAt = agent.GameTime;
            noise.Publish((Vector2)agent.transform.position + Vector2.up * 0.5f, 100, 1, NoiseKind.Impact);
            yield return new WaitForSeconds(0.7f);
            Assert.That(agent.IsRecoveryProtected, Is.True);
            Assert.That(agent.EpisodeId, Is.Zero, "A weak clue must remain in memory without interrupting protected duty.");
            Assert.That(agent.CurrentDuty, Is.EqualTo(EnemyDuty.Patrol));
            Assert.That(agent.NextPatrolIndex, Is.Zero);
            yield return Until(() => !agent.IsRecoveryProtected, 2, "Only the two-second timer is required, not a waypoint arrival.");
            Assert.That(agent.GameTime - protectedAt, Is.GreaterThanOrEqualTo(1.9));
            Assert.That(agent.NextPatrolIndex, Is.Zero, "The old next-waypoint condition would leave protection stuck here.");
            yield return Until(() => agent.EpisodeId != 0, 1, "The still-live weak clue can be handled after protection expires.");
            Assert.That(attacks, Is.Zero);
        }

        [UnityTest]
        public IEnumerator DirectIdentifiedEnemyCanBeAttackedBeforeWeakClueProtectionExpires()
        {
            yield return BeginSlowPatrolRecovery();
            double protectedAt = agent.GameTime;
            profile.DetectionGain = 100;
            target.transform.position = agent.transform.position + Vector3.right * 2;
            target.enabled = true;
            yield return Until(() => attacks > 0, 1, "Current direct identification must bypass weak-clue protection.");
            Assert.That(agent.IsTargetIdentified, Is.True);
            Assert.That(agent.GameTime - protectedAt, Is.LessThan(2));
            Assert.That(agent.NextPatrolIndex, Is.Zero);
        }

        // 추적 중 건넌 통로를 막아 '반드시 원래 이탈점으로 돌아가야 하는 이동'을 차단합니다.
        // 단순한 새 전투 목표로의 전환과 구분하려고 동일 추적 ID와 복귀 차단 단계를 계속 검사합니다.
        [UnityTest]
        public IEnumerator PermanentlyBlockedReturnDefendsInPlaceThenResumesOriginalAnchor()
        {
            target.enabled = false;
            profile.EpisodeDuration = 1.5f;
            profile.DetectionGain = 100;
            agent.transform.position = new Vector2(-3.25f, 0.25f);
            agent.ResetAgent();
            Vector2 anchor = agent.transform.position;
            noise.Publish(new Vector2(3.25f, 0.25f), 100, 1, NoiseKind.Gunshot);
            yield return Until(() => agent.transform.position.x > 0.8f, 2, "The NPC must first move across the open passage.");
            int episode = agent.EpisodeId;
            var barrier = Wall(Vector2.zero, new Vector2(0.6f, 12));
            yield return Until(() => agent.PursuitPhase == PursuitPhase.ReturnBlocked, 7, "Three failed mandatory routes must enter blocked return.");
            Vector2 stopped = agent.transform.position;
            int requests = agent.PathRequestCount;
            target.transform.position = (Vector3)stopped + Vector3.right;
            target.enabled = true;
            yield return Until(() => attacks > 0, 1, "Blocked mandatory movement must still detect and fire at a directly visible nearby enemy.");
            Assert.That(agent.EpisodeId, Is.EqualTo(episode));
            Assert.That(agent.PursuitPhase, Is.EqualTo(PursuitPhase.ReturnBlocked));
            Assert.That(Vector2.Distance(stopped, agent.transform.position), Is.LessThan(0.02f));
            Assert.That(agent.PathRequestCount, Is.EqualTo(requests), "In-place defense must not turn the blocked retry wait into per-frame A* requests.");
            target.enabled = false;
            barrier.SetActive(false);
            world.Rebuild();
            yield return Until(() => agent.IsRecoveryProtected, 6, "After reopening, the original anchor must be reached before recovery.");
            Assert.That(Vector2.Distance(anchor, agent.transform.position), Is.LessThanOrEqualTo(0.15f));
            Assert.That(agent.EpisodeId, Is.Zero);
        }

        // 문은 독립 Collider를 가지며, 실제 잠금/개방 변경은 연결 API로만 수행합니다.
        // sealedPassage=true이면 맵 위·아래까지 벽을 이어 문 외의 우회로를 없앱니다.
        private AiDoor2D CreateFeedbackDoor(bool locked, bool sealedPassage)
        {
            var doorObject = Wall(new Vector2(0, 0.25f), new Vector2(0.6f, sealedPassage ? 1.5f : 3));
            var door = doorObject.AddComponent<AiDoor2D>();
            door.World = world; door.DoorCollider = doorObject.GetComponent<Collider2D>();
            if (sealedPassage)
            {
                Wall(new Vector2(0, 3.5f), new Vector2(0.6f, 5));
                Wall(new Vector2(0, -3.25f), new Vector2(0.6f, 5.5f));
            }
            Assert.That(door.TrySetLocked(locked), Is.True);
            world.Rebuild();
            return door;
        }

        private void BeginCrossDoorPatrol()
        {
            target.enabled = false;
            agent.transform.position = new Vector2(-3.25f, 0.25f);
            var visit = Child("AcrossDoorVisit").transform;
            visit.position = new Vector2(3.25f, 0.25f);
            agent.PatrolPoints = new[] { visit };
            profile.PatrolPause = 0.05f;
            agent.ResetAgent();
        }

        [UnityTest]
        public IEnumerator NpcPlansThroughOrdinaryDoorButOpensOnlyWhenNearAndThenPasses()
        {
            BeginCrossDoorPatrol();
            var door = CreateFeedbackDoor(false, true);
            int revision = world.Revision, openings = 0;
            Vector2 openingPosition = new Vector2(float.NaN, float.NaN);
            door.StateChanged += changed =>
            { if (changed.IsOpen) { openings++; openingPosition = agent.transform.position; } };
            Assert.That(world.CanPlanTraverse(agent.transform.position, agent.PatrolPoints[0].position), Is.True);
            Assert.That(world.CanTraverse(agent.transform.position, agent.PatrolPoints[0].position), Is.False);
            float deadline = Time.time + 5;
            while (!door.IsOpen && Time.time < deadline) yield return null;
            Assert.That(door.IsOpen, Is.True, $"The NPC must open a nearby ordinary door: position={agent.transform.position}, "
                + $"goal={agent.CurrentGoal}, blocked={agent.IsPathBlocked}, requests={agent.PathRequestCount}, error={agent.PathError}");
            Assert.That(openings, Is.EqualTo(1));
            Assert.That(Vector2.Distance(openingPosition, door.transform.position), Is.LessThan(1), "Planning must not open the door remotely.");
            Assert.That(world.Revision, Is.GreaterThan(revision));
            yield return Until(() => agent.transform.position.x > 2.5f, 4, "An opened door must actually be crossed.");
            Assert.That(agent.PathFailed, Is.False);
            Assert.That(world.CanOccupy(agent.transform.position), Is.True);
        }

        [UnityTest]
        public IEnumerator LockedDoorIsNotOpenedAndNpcUsesAvailableDetour()
        {
            BeginCrossDoorPatrol();
            var door = CreateFeedbackDoor(true, false);
            float maxVerticalOffset = 0;
            float deadline = Time.time + 6;
            while (agent.transform.position.x < 2.5f && Time.time < deadline)
            {
                Assert.That(door.IsOpen, Is.False);
                Assert.That(world.CanOccupy(agent.transform.position), Is.True);
                maxVerticalOffset = Mathf.Max(maxVerticalOffset, Mathf.Abs(agent.transform.position.y - 0.25f));
                yield return null;
            }
            Assert.That(agent.transform.position.x, Is.GreaterThanOrEqualTo(2.5f));
            Assert.That(maxVerticalOffset, Is.GreaterThan(1.6f), "The capsule must go around the locked door's end.");
            Assert.That(door.IsLocked, Is.True);
            Assert.That(door.IsOpen, Is.False);
        }

        [UnityTest]
        public IEnumerator LockedOnlyPassageDefendsWithoutNewEpisodeAndUnlockResumesPatrol()
        {
            BeginCrossDoorPatrol();
            var door = CreateFeedbackDoor(true, true);
            yield return Until(() => agent.IsPathBlocked, 5, "A locked mandatory route must enter stationary guard after bounded retries.");
            Vector2 stopped = agent.transform.position;
            int run = agent.PatrolRunId;
            profile.DetectionGain = 100;
            target.transform.position = (Vector3)stopped + Vector3.right;
            target.enabled = true;
            yield return Until(() => attacks > 0, 1, "Blocked patrol must also defend, not only blocked episode return.");
            Assert.That(agent.EpisodeId, Is.Zero, "Stationary guard must not create a replacement pursuit episode.");
            Assert.That(agent.NextPatrolIndex, Is.Zero);
            Assert.That(agent.PatrolRunId, Is.EqualTo(run));
            Assert.That(Vector2.Distance(stopped, agent.transform.position), Is.LessThan(0.02f));
            Assert.That(door.IsLocked, Is.True);
            target.enabled = false;
            Assert.That(door.TrySetLocked(false), Is.True);
            yield return Until(() => agent.transform.position.x > 2.5f, 6, "Unlocking must recover the same patrol rather than skip its visit.");
            Assert.That(door.IsOpen, Is.True);
            Assert.That(agent.PatrolRunId, Is.EqualTo(run));
            Assert.That(agent.IsPathBlocked, Is.False);
        }

        [UnityTest]
        public IEnumerator RadioBurstCoalescesDuplicatesBoundsQueueAndEventuallyDrains()
        {
            target.enabled = false;
            var radio = Child("BoundedRadio").AddComponent<RadioChannel>();
            radio.DeliveryDelay = 1; radio.SendInterval = 0.05f; radio.MaxPendingDeliveries = 32;
            agent.Radio = radio; agent.ResetAgent();
            var listeners = new List<EnemyAgent> { agent };
            for (int i = 1; i < 20; i++)
            {
                var go = Child("RadioGuard" + i); go.SetActive(false);
                var receiver = go.AddComponent<EnemyAgent>();
                receiver.Profile = profile; receiver.World = world; receiver.Radio = radio; receiver.AutoStartPatrol = false;
                go.SetActive(true); listeners.Add(receiver);
            }
            yield return null;
            double occurred = Time.timeAsDouble;
            long sharedId = AiEventIds.Next();
            // 같은 사건을 여러 발신자가 알렸어도 수신자별로 한 사본만 대기해야 합니다.
            ClueReport Report(long id, EnemyAgent sender) => new ClueReport(id, ClueKind.Sound, NoiseKind.Footstep,
                Vector2.zero, occurred, occurred, occurred + 5, 1, sender.GetInstanceID(), 0, 0.1f, 1);
            foreach (var sender in listeners) radio.Send(sender, Report(sharedId, sender), Time.timeAsDouble);
            Assert.That(radio.PendingCount, Is.LessThanOrEqualTo(listeners.Count));
            Assert.That(radio.CoalescedCount, Is.GreaterThan(0));
            yield return new WaitForSeconds(0.06f);
            foreach (var sender in listeners) radio.Send(sender, Report(AiEventIds.Next(), sender), Time.timeAsDouble);
            Assert.That(radio.PendingCount, Is.EqualTo(32));
            Assert.That(radio.DroppedCount, Is.GreaterThan(0), "The queue limit must be exercised, not merely configured.");
            int scans = radio.DeliveryScanCount;
            yield return new WaitForSeconds(0.2f);
            Assert.That(radio.DeliveryScanCount, Is.EqualTo(scans), "Repeated NPC Deliver calls must not rescan before the first due time.");
            Assert.That(listeners.Sum(a => a.Memory.Count), Is.Zero, "Undelivered radio facts are not personal knowledge.");
            yield return Until(() => radio.PendingCount == 0, 2, "The bounded queue must drain without disabling the channel.");
            Assert.That(listeners.Sum(a => a.RadioReceivedCount), Is.GreaterThan(0));
            Assert.That(listeners.Max(a => a.Memory.Count), Is.LessThanOrEqualTo(8));
            yield return new WaitForSeconds(0.2f);
            Assert.That(radio.PendingCount, Is.Zero, "Receiving a report must not automatically rebroadcast it.");
        }
    }
}
