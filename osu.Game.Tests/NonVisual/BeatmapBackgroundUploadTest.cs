using System;
using System.Reflection;
using NUnit.Framework;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using osu.Game.Beatmaps;
using osu.Game.Graphics.Backgrounds;
using osu.Game.Screens.Backgrounds;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class BeatmapBackgroundUploadTest
    {
        private const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void TestPreviousBackgroundRemainsUntilUploadFinishes()
        {
            var beatmap = new TestWorkingBeatmap(new Beatmap());
            using var screen = new ProbeScreen(beatmap);
            var previous = new BeatmapBackground(beatmap);
            set(typeof(BackgroundScreenBeatmap), screen, "Background", previous);
            var (next, native) = create(beatmap);
            switchBackground(screen, next);
            Assert.That(screen.Current, Is.SameAs(previous));
            screen.Tick();
            Assert.That(screen.Current, Is.SameAs(previous));
            Assert.That(disposed(next), Is.False);
            native.Complete = true;
            screen.Tick();
            Assert.That(screen.Current, Is.SameAs(next));
            Assert.That(native.Native.Identifier, Is.EqualTo("-"), "Readiness polling must not bind or upload on Update.");
            previous.Dispose();
        }

        [Test]
        public void TestSelectionChangeRejectsLateReadyBackground()
        {
            var beatmap = new TestWorkingBeatmap(new Beatmap());
            using var screen = new ProbeScreen(beatmap);
            var (next, native) = create(beatmap);
            switchBackground(screen, next);
            screen.Beatmap = new TestWorkingBeatmap(new Beatmap());
            native.Complete = true;
            screen.Tick();
            Assert.That(screen.Current, Is.Null);
            Assert.That(disposed(next), Is.True);
        }

        [Test]
        public void TestDisposalReleasesPendingBackgroundWithoutPublication()
        {
            var beatmap = new TestWorkingBeatmap(new Beatmap());
            var screen = new ProbeScreen(beatmap);
            var (next, native) = create(beatmap);
            switchBackground(screen, next);
            screen.Dispose();
            native.Complete = true;
            screen.Tick();
            Assert.That(screen.Current, Is.Null);
            Assert.That(disposed(next), Is.True);
        }

        [Test]
        public void TestRepeatedRequestForPendingBeatmapDoesNotDisposeIt()
        {
            var beatmap = new TestWorkingBeatmap(new Beatmap());
            using var screen = new ProbeScreen(beatmap);
            var (next, native) = create(beatmap);
            switchBackground(screen, next);
            typeof(BackgroundScreenBeatmap).GetMethod("queueBackgroundLoad", hidden)!.Invoke(screen, new object[] { false });
            Assert.That(disposed(next), Is.False);
            native.Complete = true;
            screen.Tick();
            Assert.That(screen.Current, Is.SameAs(next));
        }

        [Test]
        public void TestSafetyCoverDoesNotHideOriginalUploadReadiness()
        {
            var beatmap = new TestWorkingBeatmap(new Beatmap());
            var (next, native) = create(beatmap);
            using (next)
            {
                next.Sprite.Texture = null;
                Assert.That(next.TextureUploadComplete, Is.False);
                native.Complete = true;
                Assert.That(next.TextureUploadComplete, Is.True);
            }
        }

        [Test]
        public void TestGlobalBackgroundWaitsAndThenPublishes()
        {
            using var screen = new ProbeGlobalScreen();
            var previous = new BeatmapBackground(null);
            set(typeof(BackgroundScreenDefault), screen, "background", previous);
            var (next, native) = create(new TestWorkingBeatmap(new Beatmap()));
            displayGlobal(screen, next);
            screen.Tick();
            Assert.That(getGlobal(screen), Is.SameAs(previous));
            native.Complete = true;
            screen.Tick();
            Assert.That(getGlobal(screen), Is.SameAs(next));
            previous.Dispose();
        }

        [Test]
        public void TestGlobalBackgroundDisposalRejectsPendingPublication()
        {
            var screen = new ProbeGlobalScreen();
            var (next, native) = create(new TestWorkingBeatmap(new Beatmap()));
            displayGlobal(screen, next);
            screen.Dispose();
            native.Complete = true;
            screen.Tick();
            Assert.That(getGlobal(screen), Is.Null);
            Assert.That(disposed(next), Is.True);
        }

        private static void displayGlobal(BackgroundScreenDefault screen, BeatmapBackground background)
            => typeof(BackgroundScreenDefault).GetMethod("displayNext", hidden)!.Invoke(screen, new object[] { background });

        private static Background? getGlobal(BackgroundScreenDefault screen)
            => (Background?)typeof(BackgroundScreenDefault).GetField("background", hidden)!.GetValue(screen);

        private partial class ProbeGlobalScreen : BackgroundScreenDefault
        {
            public void Tick() => Scheduler.Update();
        }

        private static (BeatmapBackground Background, PendingTexture Native) create(WorkingBeatmap beatmap)
        {
            var native = new PendingTexture();
            var texture = (Texture)Activator.CreateInstance(typeof(Texture), hidden, null, new object[] { native.Native, WrapMode.None, WrapMode.None }, null)!;
            var background = new BeatmapBackground(beatmap);
            set(typeof(BeatmapBackground), background, "sourceTexture", texture);
            background.Sprite.Texture = texture;
            return (background, native);
        }

        private static void switchBackground(BackgroundScreenBeatmap screen, BeatmapBackground background)
            => typeof(BackgroundScreenBeatmap).GetMethod("switchBackground", hidden)!.Invoke(screen, new object[] { background });

        private static void set(Type type, object target, string field, object value) => type.GetField(field, hidden)!.SetValue(target, value);
        private static bool disposed(BeatmapBackground background) => (bool)typeof(osu.Framework.Graphics.Drawable).GetProperty("IsDisposed", hidden)!.GetValue(background)!;

        private partial class ProbeScreen : BackgroundScreenBeatmap
        {
            public ProbeScreen(WorkingBeatmap beatmap) : base(beatmap)
            {
                // Drain the constructor's load request without DI. The actual publication callback
                // is tested below with a controlled completion flag, native rendering separately.
                Scheduler.Update();
            }
            protected override bool CanReuseCurrentBackground(WorkingBeatmap beatmap) => true;
            public void Tick() => Scheduler.Update();
            public Background? Current => Background;
        }

        private sealed class PendingTexture
        {
            private static readonly Type nativeType = typeof(Texture).Assembly.GetType("osu.Framework.Graphics.OpenGL.Textures.GLTexture")!;
            public readonly INativeTexture Native = (INativeTexture)Activator.CreateInstance(nativeType,
                new object?[] { Activator.CreateInstance(typeof(Texture).Assembly.GetType("osu.Framework.Graphics.OpenGL.GLRenderer")!, true), 4200, 2363, osuTK.Graphics.ES30.TextureComponentCount.Rgba8, true, osuTK.Graphics.ES30.All.Linear, null })!;
            public bool Complete
            {
                get => Native.UploadComplete;
                set => nativeType.GetField("uploadComplete", hidden)!.SetValue(Native, value);
            }
            public PendingTexture() => Complete = false;
        }
    }
}
