using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Textures;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Framework.Threading;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Screens;
using osu.Game.Screens.Select;
using osu.Game.Skinning;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public partial class SongSelectBeatmapPreloadTest
    {
        [TestCase(false)]
        [TestCase(true)]
        public void TestTransientSelectionsDoNotDecodeAndFinalSelectionLoads(bool legacy)
        {
            using var game = new PreloadGame();
            using var stack = new OsuScreenStack();
            stack.Push(legacy ? new Screens.SelectLegacy.PlaySongSelect() : new SoloSongSelect());
            game.SetScreenStack(stack);
            var clock = new ManualClock();
            game.PreloadScheduler.UpdateClock(clock);
            var maps = new List<PreloadBeatmap>();

            for (int i = 0; i < 8; i++)
            {
                var working = new PreloadBeatmap();
                maps.Add(working);
                game.Select(working);
                Assert.That(((IBeatSyncProvider)game).ControlPoints, Is.Null, "Beat sync must not force transient decoding.");
                clock.CurrentTime += 300;
                game.PreloadScheduler.Update();
                Assert.That(working.Decoded.Wait(50), Is.False, "Transient selection must not start full beatmap loading.");
            }

            Assert.That(maps.ConvertAll(m => m.DecodeCount), Is.All.Zero);
            clock.CurrentTime += SongSelect.PERFORMANCE_CALCULATION_DEBOUNCE;
            game.PreloadScheduler.Update();
            Assert.That(maps[^1].Decoded.Wait(10000), Is.True);
            Assert.That(maps[^1].Beatmap, Is.Not.Null);
            Assert.That(maps[^1].DecodeCount, Is.EqualTo(1));
            Assert.That(((IBeatSyncProvider)game).ControlPoints, Is.SameAs(maps[^1].Beatmap.ControlPointInfo));
            Assert.That(maps.GetRange(0, maps.Count - 1).ConvertAll(m => m.DecodeCount), Is.All.Zero);
        }

        [Test]
        public void TestExplicitBeatmapRequestDoesNotWaitForScheduledPreload()
        {
            using var game = new PreloadGame();
            using var stack = new OsuScreenStack();
            stack.Push(new SoloSongSelect());
            game.SetScreenStack(stack);
            var clock = new ManualClock();
            game.PreloadScheduler.UpdateClock(clock);
            var working = new PreloadBeatmap();
            game.Select(working);

            Assert.That(working.Beatmap, Is.Not.Null, "Gameplay/editor demand bypasses opportunistic preload delay.");
            Assert.That(working.DecodeCount, Is.EqualTo(1));
            clock.CurrentTime += SongSelect.PERFORMANCE_CALCULATION_DEBOUNCE;
            game.PreloadScheduler.Update();
            Assert.That(working.Beatmap, Is.Not.Null);
            Assert.That(working.DecodeCount, Is.EqualTo(1));
        }

        [Test]
        public void TestOutsideSongSelectPreloadRemainsImmediate()
        {
            using var game = new PreloadGame();
            using var stack = new OsuScreenStack();
            stack.Push(new Screen());
            game.SetScreenStack(stack);
            var working = new PreloadBeatmap();
            game.Select(working);
            Assert.That(working.Decoded.Wait(10000), Is.True);
            Assert.That(working.Beatmap, Is.Not.Null);
        }

        [Test]
        public void TestCachedSelectionDoesNotScheduleAnotherDecode()
        {
            using var game = new PreloadGame();
            using var stack = new OsuScreenStack();
            stack.Push(new SoloSongSelect());
            game.SetScreenStack(stack);
            var clock = new ManualClock();
            game.PreloadScheduler.UpdateClock(clock);
            var cached = new PreloadBeatmap();
            Assert.That(cached.Beatmap, Is.Not.Null);
            Assert.That(cached.BeatmapLoaded, Is.True);
            game.Select(cached);
            Assert.That(game.PreloadScheduler.HasPendingTasks, Is.False);
            Assert.That(cached.DecodeCount, Is.EqualTo(1));
        }

        [Test]
        public void TestChangeToNonSelectionScreenCancelsPreviousPendingMap()
        {
            using var game = new PreloadGame();
            using var selectionStack = new OsuScreenStack();
            selectionStack.Push(new SoloSongSelect());
            game.SetScreenStack(selectionStack);
            var clock = new ManualClock();
            game.PreloadScheduler.UpdateClock(clock);
            var abandoned = new PreloadBeatmap();
            game.Select(abandoned);
            using var otherStack = new OsuScreenStack();
            otherStack.Push(new Screen());
            game.SetScreenStack(otherStack);
            var current = new PreloadBeatmap();
            game.Select(current);
            Assert.That(current.Decoded.Wait(10000), Is.True);
            clock.CurrentTime += SongSelect.PERFORMANCE_CALCULATION_DEBOUNCE;
            game.PreloadScheduler.Update();
            Assert.That(abandoned.Decoded.Wait(50), Is.False);
        }

        [Test]
        public void TestDisposalCancelsPendingPreload()
        {
            var game = new PreloadGame();
            using var stack = new OsuScreenStack();
            stack.Push(new SoloSongSelect());
            game.SetScreenStack(stack);
            var clock = new ManualClock();
            Scheduler scheduler = game.PreloadScheduler;
            scheduler.UpdateClock(clock);
            var working = new PreloadBeatmap();
            game.Select(working);
            game.Dispose();
            clock.CurrentTime += SongSelect.PERFORMANCE_CALCULATION_DEBOUNCE;
            scheduler.Update();
            Assert.That(working.Decoded.Wait(50), Is.False);
        }

        private partial class PreloadGame : OsuGame
        {
            public Scheduler PreloadScheduler => Scheduler;
            private readonly Bindable<WorkingBeatmap> selected = new Bindable<WorkingBeatmap>();
            private readonly HeadlessGameHost host = new HeadlessGameHost();

            public PreloadGame()
            {
                // Discard unrelated constructor work which requires a loaded host/tablet graph.
                typeof(Drawable).GetField("scheduler", BindingFlags.Instance | BindingFlags.NonPublic)!
                                .SetValue(this, new Scheduler(() => true, null));
                typeof(OsuGameBase).GetProperty("Beatmap", BindingFlags.Instance | BindingFlags.NonPublic)!
                                   .SetValue(this, selected);
                typeof(osu.Framework.Game).GetProperty("Host", BindingFlags.Instance | BindingFlags.NonPublic)!
                                          .SetValue(this, host);
                selected.BindValueChanged(typeof(OsuGame).GetMethod("beatmapChanged", BindingFlags.Instance | BindingFlags.NonPublic)!
                                                        .CreateDelegate<Action<ValueChangedEvent<WorkingBeatmap>>>(this));
            }

            public void SetScreenStack(OsuScreenStack stack)
                => typeof(OsuGame).GetProperty(nameof(ScreenStack))!.SetValue(this, stack);

            // Exercise the actual selection callback, not only its admission helper.
            public void Select(WorkingBeatmap working) => selected.Value = working;

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);
                host.Dispose();
            }
        }

        private sealed class PreloadBeatmap : WorkingBeatmap
        {
            public readonly ManualResetEventSlim Decoded = new ManualResetEventSlim();
            private int decodeCount;
            public int DecodeCount => Volatile.Read(ref decodeCount);

            public PreloadBeatmap() : base(new BeatmapInfo(), null) { }

            protected override IBeatmap GetBeatmap()
            {
                Interlocked.Increment(ref decodeCount);
                Decoded.Set();
                return new Beatmap();
            }

            public override Texture? GetBackground() => null;
            public override Stream? GetStream(string storagePath) => null;
            protected override Track? GetBeatmapTrack() => null;
            protected internal override ISkin? GetSkin() => null;
        }
    }
}
