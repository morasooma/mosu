using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using NUnit.Framework;
using osu.Game.Performance;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class GameplayWorkerPreparationTest
    {
        [Test]
        public async Task TestOutgoingOperationsAreOrderedOffCallerAndFailureDoesNotPoisonQueue()
        {
            var queue = new AsyncOperationQueue();
            int caller = Environment.CurrentManagedThreadId;
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var order = new List<int>();
            Task first = queue.Enqueue(async () =>
            {
                Assert.That(Environment.CurrentManagedThreadId, Is.Not.EqualTo(caller));
                order.Add(1);
                entered.SetResult();
                await release.Task;
                throw new InvalidOperationException("expected first failure");
            });
            // Keep the original caller occupied until dispatch enters: a pool thread may
            // otherwise be reused after an await, which would make the thread-ID check flaky.
            entered.Task.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            Task second = queue.Enqueue(() => { order.Add(2); return Task.CompletedTask; });
            Assert.That(second.IsCompleted, Is.False);
            release.SetResult();
            Assert.ThrowsAsync<InvalidOperationException>(async () => await first);
            await second;
            Assert.That(order, Is.EqualTo(new[] { 1, 2 }));
        }

        [Test]
        public async Task TestInvocationDispatchDoesNotWaitForNetworkResponse()
        {
            var queue = new AsyncOperationQueue();
            var response = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var invoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondInvoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Task<bool> first = queue.EnqueueInvocation(() => { invoked.SetResult(); return response.Task; });
            await invoked.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Task second = queue.EnqueueInvocation(() => { secondInvoked.SetResult(); return Task.CompletedTask; });
            await secondInvoked.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await second;
            Assert.That(first.IsCompleted, Is.False, "dispatch order must not depend on prior server acknowledgement");
            response.SetException(new InvalidOperationException("expected network failure"));
            Assert.ThrowsAsync<InvalidOperationException>(async () => await first);
        }

        [Test]
        public void TestJitPreparationDoesNotExecuteMethodsOrStaticConstructor()
        {
            sideEffects = 0;
            GameplayJitPrewarmer.PrepareType(typeof(Probe), CancellationToken.None);
            Assert.That(sideEffects, Is.Zero);
            Assert.Throws<OperationCanceledException>(() => GameplayJitPrewarmer.PrepareType(typeof(OtherProbe), new CancellationToken(true)));
            Assert.That(sideEffects, Is.Zero);
            Assert.Throws<OperationCanceledException>(() => GameplayJitPrewarmer.PrepareType(typeof(Probe), new CancellationToken(true)), "even an already-prepared type must honour cancellation");
        }

        [Test]
        public async Task TestKnownScoringTablePreparationPreservesContentsAndReuse()
        {
            await Task.Run(() => GameplayJitPrewarmer.PrepareScoringTables(CancellationToken.None));
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            var types = HitResultExtensions.ALL_TYPES;
            int perfectIndex = HitResult.Perfect.GetIndexForOrderedDisplay();
            long firstAccessBytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
            TestContext.Progress.WriteLine($"SCORING_TABLE_FIRST_ACCESS allocated_bytes={firstAccessBytes}");
            Assert.That(firstAccessBytes, Is.LessThan(256), "enum reflection/sorting must have completed on the worker; run this test alone in a fresh process too");
            Assert.That(types.Length, Is.EqualTo(17));
            Assert.That(types, Is.Unique);
            Assert.That(types, Does.Contain(HitResult.SliderTailHit));
            Assert.That(perfectIndex, Is.Zero);
            Assert.That(HitResult.Miss.GetIndexForOrderedDisplay(), Is.EqualTo(5));
            var ordering = types.Select(type => type.GetIndexForOrderedDisplay()).ToArray();
            await Task.Run(() => GameplayJitPrewarmer.PrepareScoringTables(CancellationToken.None));
            Assert.That(HitResultExtensions.ALL_TYPES, Is.SameAs(types));
            Assert.That(types.Select(type => type.GetIndexForOrderedDisplay()), Is.EqualTo(ordering));
            Assert.Throws<OperationCanceledException>(() => GameplayJitPrewarmer.PrepareScoringTables(new CancellationToken(true)));
        }

        [Test]
        public void TestActualRulesetPreparationAndRepeatedReuse()
        {
            var watch = Stopwatch.StartNew();
            GameplayJitPrewarmer.PrepareRuleset(typeof(OsuRuleset).Assembly, CancellationToken.None);
            long firstMs = watch.ElapsedMilliseconds;
            watch.Restart();
            GameplayJitPrewarmer.PrepareRuleset(typeof(OsuRuleset).Assembly, CancellationToken.None);
            TestContext.Progress.WriteLine($"GAMEPLAY_JIT_PREPARE ruleset=osu firstMs={firstMs} repeatMs={watch.ElapsedMilliseconds}");
        }

        [TestCase(typeof(osu.Game.Rulesets.Osu.UI.RelaxController))]
        [TestCase(typeof(osu.Game.Rulesets.Osu.UI.AimAssistController))]
        [TestCase(typeof(osu.Game.Rulesets.Osu.UI.Cursor.CursorTrail))]
        [TestCase(typeof(osu.Game.Rulesets.Osu.Skinning.Legacy.LegacyCursorTrail))]
        [TestCase(typeof(osu.Game.Rulesets.Osu.Skinning.Legacy.LegacySliderHeadHitCircle))]
        [TestCase(typeof(osu.Game.Rulesets.Osu.Replays.OsuFramedReplayInputHandler))]
        public void TestCapturedFirstUseFamiliesAreCandidates(Type type)
        {
            Assert.That(GameplayJitPrewarmer.IsRulesetCandidate(type), Is.True);
            Assert.Throws<OperationCanceledException>(() => GameplayJitPrewarmer.PrepareType(type, new CancellationToken(true)));
        }

        [TestCase(typeof(osu.Game.Screens.Play.HUD.ArgonHealthDisplay))]
        [TestCase(typeof(osu.Game.Screens.Play.HUD.ArgonSongProgress))]
        [TestCase(typeof(osu.Game.Screens.Play.HUD.GameplayAccuracyCounter))]
        [TestCase(typeof(osu.Game.Screens.Play.ArgonKeyCounter))]
        public void TestCapturedHudTypesAreCandidates(Type type)
        {
            Assert.That(GameplayJitPrewarmer.IsHudCandidate(type), Is.True);
        }

        [TestCase(typeof(osu.Game.Screens.Edit.Editor))]
        [TestCase(typeof(osu.Game.Overlays.SettingsOverlay))]
        [TestCase(typeof(osu.Game.Rulesets.Osu.Difficulty.OsuDifficultyCalculator))]
        public void TestUnrelatedTypesRemainOutsideHudPreparation(Type type)
        {
            Assert.That(GameplayJitPrewarmer.IsHudCandidate(type), Is.False);
        }

        [Test]
        public void TestDifficultyAndModsRemainOutsidePreparation()
        {
            Assert.That(GameplayJitPrewarmer.IsRulesetCandidate(typeof(osu.Game.Rulesets.Osu.Difficulty.OsuDifficultyCalculator)), Is.False);
            Assert.That(GameplayJitPrewarmer.IsRulesetCandidate(typeof(osu.Game.Rulesets.Osu.Mods.OsuModDoubleTime)), Is.False);
        }

        private static int sideEffects;
        private sealed class Probe
        {
            static Probe() => sideEffects++;
            public void Update() => sideEffects++;
        }
        private sealed class OtherProbe
        {
            public void Update() => sideEffects++;
        }
    }
}
