using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Overlays;
using osu.Game.Screens;
using osu.Game.Screens.SelectLegacy;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public partial class LegacyPreviewPreparationTest
    {
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void TestColdSeekDoesNotBlockAndStaleCompletionIsIgnored(int invalidation)
        {
            using var screen = new PreviewScreen();
            using var track = new DelayedTrack();
            var working = new PreviewBeatmap(track);
            initialise(screen, working);

            invoke(screen, "ensureTrackLooping", working, TrackChangeDirection.None);
            Assert.That(track.AsyncSeekCount, Is.EqualTo(1));
            Assert.That(track.SyncSeekCount, Is.Zero);
            Assert.That(getPending(screen), Is.SameAs(working));

            using var replacement = new DelayedTrack();
            if (invalidation == 1)
                setField(screen, "isHandlingLooping", false);
            else if (invalidation == 2)
            {
                working.ReplaceTrack(replacement);
                invoke(screen, "ensureTrackLooping", working, TrackChangeDirection.None);
            }
            else
                screen.Beatmap.Value = new TestWorkingBeatmap(new Beatmap());

            track.CompleteSeek();
            drainCompletion(screen);
            Assert.That(working.PrepareCount, Is.Zero, "A stale seek must not change preview playback.");
            if (invalidation == 2)
            {
                Assert.That(getPending(screen), Is.SameAs(working));
                replacement.CompleteSeek();
                drainCompletion(screen);
                Assert.That(working.PrepareCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void TestCurrentColdTrackFinishesPreparationAfterSeek()
        {
            using var screen = new PreviewScreen();
            using var track = new DelayedTrack();
            var working = new PreviewBeatmap(track);
            initialise(screen, working);
            invoke(screen, "ensureTrackLooping", working, TrackChangeDirection.None);
            Assert.That(working.PrepareCount, Is.Zero);
            Assert.That(track.SyncSeekCount, Is.Zero);

            track.CompleteSeek();
            drainCompletion(screen);
            Assert.That(working.PrepareCount, Is.EqualTo(1));
            Assert.That(getPending(screen), Is.Null);
            Assert.That(track.RestartPoint, Is.EqualTo(400 - MusicController.DELAY_BEFORE_FADE).Within(0.001));
        }

        private static void initialise(PreviewScreen screen, WorkingBeatmap working)
        {
            typeof(OsuScreen).GetProperty(nameof(OsuScreen.Beatmap))!.SetValue(screen, new Bindable<WorkingBeatmap>(working));
            setField(screen, "isHandlingLooping", true);
        }

        private static object? invoke(PreviewScreen screen, string method, params object[] args)
            => typeof(SongSelect).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(screen, args);

        private static void setField(PreviewScreen screen, string name, object value)
            => typeof(SongSelect).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(screen, value);

        private static object? getPending(PreviewScreen screen)
            => typeof(SongSelect).GetField("pendingPreviewBeatmap", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(screen);

        private static void drainCompletion(PreviewScreen screen)
        {
            Scheduler scheduler = getScheduler(screen);
            Assert.That(SpinWait.SpinUntil(() => scheduler.HasPendingTasks, TimeSpan.FromSeconds(5)), Is.True,
                "The actual asynchronous seek continuation must publish its callback.");
            scheduler.Update();
        }

        private static Scheduler getScheduler(PreviewScreen screen)
            => (Scheduler)typeof(Drawable).GetProperty("Scheduler", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(screen)!;

        private partial class PreviewScreen : PlaySongSelect
        {
            protected override bool ControlGlobalMusic => false;
        }

        private class PreviewBeatmap : TestWorkingBeatmap
        {
            private Track previewTrack;
            public int PrepareCount => previewTrack.RestartPoint == 0 ? 0 : 1;

            public PreviewBeatmap(Track track)
                : base(new Beatmap())
            {
                previewTrack = track;
                LoadTrack();
            }

            public void ReplaceTrack(Track next)
            {
                previewTrack = next;
                LoadTrack();
            }

            protected override Track GetBeatmapTrack() => previewTrack;
        }

        private class DelayedTrack : Track
        {
            private readonly TaskCompletionSource<bool> seek = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            private bool loaded;
            public int AsyncSeekCount { get; private set; }
            public int SyncSeekCount { get; private set; }
            public override bool IsLoaded => loaded;
            public override double CurrentTime => 0;
            public override bool IsRunning => false;

            public DelayedTrack() : base("delayed preview") => Length = 1000;

            public void CompleteSeek()
            {
                loaded = true;
                seek.SetResult(true);
            }

            public override bool Seek(double position)
            {
                SyncSeekCount++;
                throw new InvalidOperationException("Cold synchronous seek blocks the update thread.");
            }

            public override Task<bool> SeekAsync(double position)
            {
                AsyncSeekCount++;
                return seek.Task;
            }

            public override Task StartAsync() => Task.CompletedTask;
            public override Task StopAsync() => Task.CompletedTask;
            public override void Start() { }
            public override void Stop() { }
        }
    }
}
