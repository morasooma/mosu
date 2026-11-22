// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics.Textures;
using osu.Framework.Localisation;
using Moq;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Skinning;

namespace osu.Game.Tests.Beatmaps
{
    [TestFixture]
    public class WorkingBeatmapTest
    {
        [Test]
        public void TestGetPlayableSuccess()
        {
            var working = new TestNeverLoadsWorkingBeatmap();

            working.ResetEvent.Set();

            ClassicAssert.NotNull(working.GetPlayableBeatmap(new OsuRuleset().RulesetInfo));
        }

        [Test]
        public void TestGetPlayableCancellationToken()
        {
            var working = new TestNeverLoadsWorkingBeatmap();

            var cts = new CancellationTokenSource();
            var loadStarted = new ManualResetEventSlim();
            var loadCompleted = new ManualResetEventSlim();

            Task.Factory.StartNew(() =>
            {
                loadStarted.Set();
                Assert.Throws<OperationCanceledException>(() => working.GetPlayableBeatmap(new OsuRuleset().RulesetInfo, Array.Empty<Mod>(), cts.Token));
                loadCompleted.Set();
            }, TaskCreationOptions.LongRunning);

            ClassicAssert.True(loadStarted.Wait(10000));

            cts.Cancel();

            ClassicAssert.True(loadCompleted.Wait(10000));

            working.ResetEvent.Set();
        }

        [Test]
        public void TestGetPlayableDefaultTimeout()
        {
            var working = new TestNeverLoadsWorkingBeatmap();

            Assert.Throws(Is.InstanceOf<TimeoutException>(), () => working.GetPlayableBeatmap(new OsuRuleset().RulesetInfo));

            working.ResetEvent.Set();
        }

        [Test]
        public void TestGetPlayableRulesetLoadFailure()
        {
            var working = new TestWorkingBeatmap(new Beatmap());

            // by default mocks return nulls if not set up, which is actually desired here to simulate a ruleset load failure scenario.
            var ruleset = new Mock<IRulesetInfo>();

            Assert.Throws<RulesetLoadException>(() => working.GetPlayableBeatmap(ruleset.Object));
        }

        [Test]
        public void TestCancelRunningDecodeReusesResult()
        {
            using var working = new ControlledWorkingBeatmap();
            working.BeginAsyncLoad();
            ClassicAssert.True(working.DecodeStarted.Wait(10000));

            try
            {
                working.CancelAsyncLoad();
                working.BeginAsyncLoad();

                // The first decode cannot be canceled once inside GetBeatmap(). A reload must not start another.
                ClassicAssert.False(working.SecondDecodeStarted.Wait(1000));
            }
            finally
            {
                working.ReleaseDecode.Set();
            }

            ClassicAssert.NotNull(working.Beatmap);
            ClassicAssert.True(working.BeatmapLoaded);
            ClassicAssert.AreEqual(1, working.DecodeCount);
            working.CancelAsyncLoad();
            ClassicAssert.AreEqual(working.Beatmap, working.Beatmap);
            ClassicAssert.AreEqual(1, working.DecodeCount);
        }

        [Test]
        public void TestRunningDecodeFailureRemainsCachedAfterCancel()
        {
            using var working = new ControlledWorkingBeatmap { FailDecode = true };
            working.BeginAsyncLoad();
            ClassicAssert.True(working.DecodeStarted.Wait(10000));
            try
            {
                working.CancelAsyncLoad();
                working.BeginAsyncLoad();
                ClassicAssert.False(working.SecondDecodeStarted.Wait(1000));
            }
            finally
            {
                working.ReleaseDecode.Set();
            }

            ClassicAssert.IsNull(working.Beatmap);
            working.CancelAsyncLoad();
            working.BeginAsyncLoad();
            ClassicAssert.IsNull(working.Beatmap);
            ClassicAssert.AreEqual(1, working.DecodeCount);
        }

        [Test]
        public void TestCancelQueuedDecodeAllowsRetry()
        {
            using var working = new ControlledWorkingBeatmap();
            // Hold the gate so cancellation wins before GetBeatmap() can start.
            // This does not require the first delegate to have started waiting on the gate.
            var gate = (Lock)typeof(WorkingBeatmap).GetField("beatmapFetchLock", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(working)!;
            lock (gate)
            {
                working.BeginAsyncLoad();
                working.CancelAsyncLoad();
                working.BeginAsyncLoad();
            }

            ClassicAssert.True(working.DecodeStarted.Wait(10000));
            working.ReleaseDecode.Set();
            ClassicAssert.NotNull(working.Beatmap);
            ClassicAssert.AreEqual(1, working.DecodeCount);
        }

        [Test]
        public void TestCancelBeforeLoadAllowsNewLoad()
        {
            using var working = new ControlledWorkingBeatmap();
            working.CancelAsyncLoad();
            working.ReleaseDecode.Set();
            working.BeginAsyncLoad();
            ClassicAssert.True(working.DecodeStarted.Wait(10000));
            ClassicAssert.NotNull(working.Beatmap);
            ClassicAssert.AreEqual(1, working.DecodeCount);
        }

        [Test]
        public void TestAlreadyCancelledPlayableDoesNotDecodeAndAllowsReuse()
        {
            using var working = new ControlledWorkingBeatmap();
            working.ReleaseDecode.Set();
            Assert.Throws<OperationCanceledException>(() => working.GetPlayableBeatmap(new OsuRuleset().RulesetInfo, Array.Empty<Mod>(), new CancellationToken(true)));
            Assert.That(working.DecodeCount, Is.Zero);
            Assert.That(working.GetPlayableBeatmap(new OsuRuleset().RulesetInfo, Array.Empty<Mod>(), CancellationToken.None), Is.Not.Null);
            Assert.That(working.DecodeCount, Is.EqualTo(1));
        }

        [Test]
        public void TestCancellationAfterConversionModSkipsProcessorStageAndCanReuse()
        {
            var working = new TestWorkingBeatmap(new OsuBeatmap());
            using var cancellation = new CancellationTokenSource();
            var mod = new CancellingConversionMod(cancellation);
            Assert.Throws<OperationCanceledException>(() => working.GetPlayableBeatmap(new OsuRuleset().RulesetInfo, new Mod[] { mod }, cancellation.Token));
            Assert.That(mod.ProcessorCalls, Is.Zero);
            Assert.That(working.GetPlayableBeatmap(new OsuRuleset().RulesetInfo, new Mod[] { mod }, CancellationToken.None), Is.Not.Null);
            Assert.That(mod.ProcessorCalls, Is.EqualTo(1));
        }

        private sealed class CancellingConversionMod : Mod, IApplicableAfterBeatmapConversion, IApplicableToBeatmapProcessor
        {
            private readonly CancellationTokenSource source;
            public int ProcessorCalls;
            public override string Name => "Cancellation probe";
            public override string Acronym => "CP";
            public override LocalisableString Description => "Test only";
            public CancellingConversionMod(CancellationTokenSource source) => this.source = source;
            public void ApplyToBeatmap(IBeatmap beatmap) => source.Cancel();
            public void ApplyToBeatmapProcessor(IBeatmapProcessor processor) => ProcessorCalls++;
        }

        private sealed class ControlledWorkingBeatmap : WorkingBeatmap, IDisposable
        {
            public readonly ManualResetEventSlim DecodeStarted = new ManualResetEventSlim();
            public readonly ManualResetEventSlim SecondDecodeStarted = new ManualResetEventSlim();
            public readonly ManualResetEventSlim ReleaseDecode = new ManualResetEventSlim();

            public bool FailDecode;
            public int DecodeCount;

            public ControlledWorkingBeatmap()
                : base(new BeatmapInfo(), null)
            {
            }

            protected override IBeatmap GetBeatmap()
            {
                if (Interlocked.Increment(ref DecodeCount) > 1)
                    SecondDecodeStarted.Set();
                DecodeStarted.Set();
                if (!ReleaseDecode.Wait(10000))
                    throw new TimeoutException("Test decode was not released.");
                if (FailDecode)
                    throw new InvalidOperationException("Test decode failed.");
                return new Beatmap();
            }

            public override Texture GetBackground() => null;
            protected override Track GetBeatmapTrack() => null;
            protected internal override ISkin GetSkin() => null;
            public override Stream GetStream(string storagePath) => null;

            public void Dispose()
            {
                ReleaseDecode.Set();
                DecodeStarted.Dispose();
                SecondDecodeStarted.Dispose();
                ReleaseDecode.Dispose();
            }
        }

        public class TestNeverLoadsWorkingBeatmap : TestWorkingBeatmap
        {
            public ManualResetEventSlim ResetEvent = new ManualResetEventSlim();

            public TestNeverLoadsWorkingBeatmap()
                : base(new Beatmap())
            {
            }

            protected override IBeatmapConverter CreateBeatmapConverter(IBeatmap beatmap, Ruleset ruleset) => new TestConverter(beatmap, ResetEvent);

            public class TestConverter : IBeatmapConverter
            {
                private readonly ManualResetEventSlim resetEvent;

                public TestConverter(IBeatmap beatmap, ManualResetEventSlim resetEvent)
                {
                    this.resetEvent = resetEvent;
                    Beatmap = beatmap;
                }

#pragma warning disable CS0067
                [CanBeNull]
                public event Action<HitObject, IEnumerable<HitObject>> ObjectConverted;
#pragma warning restore CS0067

                public IBeatmap Beatmap { get; }

                public bool CanConvert() => true;

                public IBeatmap Convert(CancellationToken cancellationToken = default)
                {
                    resetEvent.Wait(cancellationToken);
                    return new OsuBeatmap();
                }
            }
        }
    }
}
