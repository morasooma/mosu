using System.Collections;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Development;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Platform;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;

namespace osu.Game.Tests.Visual.UserInterface
{
    public partial class TestSceneAsyncSongSelectText : OsuTestScene
    {
        [Test]
        public void TestColdDynamicGlyphsNeverLoadOnUpdate()
        {
            RecordingGlyphStore store = null!;
            FontStore fonts = null!;
            OsuSpriteText text = null!;
            AddStep("register isolated cold font", () =>
            {
                store = new RecordingGlyphStore(new ResourceStore<byte[]>(new NamespacedResourceStore<byte[]>(Resources, "Fonts")), "Torus/Torus-Regular");
                fonts = new FontStore(Dependencies.Get<GameHost>().Renderer, minFilterMode: TextureFilteringMode.Linear);
                fonts.AddTextureSource(store);
                Dependencies.CacheAs(fonts);
                Child = text = new OsuSpriteText { Font = OsuFont.Default };
            });
            AddStep("change dynamic label 100 times", () =>
            {
                for (int i = 0; i < 100; i++)
                    text.AsyncText = $"obsolete {i}";
                text.AsyncText = "Final ABC XYZ 123";
            });
            AddUntilStep("latest text published", () => text.Text.ToString() == "Final ABC XYZ 123" && text.DrawWidth > 0);
            AddAssert("no glyph disk loading in Update", () => store.UpdateLoads, () => Is.Zero);
            AddStep("same font refresh is a no-op", () =>
            {
                for (int i = 0; i < 100; i++)
                    text.RefreshAsyncTextFont();
                Assert.That(text.Text.ToString(), Is.EqualTo("Final ABC XYZ 123"));
            });
            AddStep("clear while pending", () =>
            {
                text.AsyncText = "Must not return";
                text.AsyncText = string.Empty;
            });
            AddWaitStep("drain cancelled work", 5);
            AddAssert("empty remains empty", () => text.Text.ToString() == string.Empty);
            AddStep("remove isolated font", () =>
            {
                Clear();
                fonts.Dispose();
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestDetachedTextDoesNotAccumulatePublicationCallbacks(bool clearBeforeReuse)
        {
            OsuSpriteText text = null!;
            int changes = 0;
            AddStep("load dynamic text", () => Child = text = new OsuSpriteText { AsyncText = "Initial label" });
            AddUntilStep("initial text published", () => text.Text.ToString() == "Initial label");
            AddStep("detach without disposing as in a pool", () => Remove(text, false));
            AddRepeatStep("prepare labels while detached", () => text.AsyncText = $"Obsolete label {++changes}", 30);
            AddWaitStep("allow worker publications to queue", 5);
            AddStep("optionally clear returned label", () =>
            {
                if (clearBeforeReuse)
                    text.AsyncText = string.Empty;
            });
            AddAssert("at most one publication is queued", () =>
                ((ICollection)typeof(osu.Framework.Threading.Scheduler)
                    .GetField("runQueue", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(typeof(osu.Framework.Graphics.Drawable)
                        .GetProperty("Scheduler", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(text))!).Count <= 1);
            AddStep("reuse for a new label", () =>
            {
                text.AsyncText = "Current reused label";
                Add(text);
            });
            AddUntilStep("only the new label is published", () => text.Text.ToString() == "Current reused label");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestDisposedTextIgnoresLateUpdates(bool pending)
        {
            OsuSpriteText text = null!;
            AddStep("load dynamic text", () => Child = text = new OsuSpriteText { AsyncText = "Initial label" });
            AddUntilStep("initial text published", () => text.Text.ToString() == "Initial label");
            AddStep("dispose text and simulate late completion", () =>
            {
                if (pending)
                    text.AsyncText = "Pending label";
                Remove(text, false);
                text.Dispose();
                text.AsyncText = "Late difficulty result";
                text.RefreshAsyncTextFont();
                text.Dispose();
            });
            AddAssert("late update was ignored", () => text.AsyncText.ToString() != "Late difficulty result");
        }

        private class RecordingGlyphStore : GlyphStore
        {
            private int updateLoads;
            public int UpdateLoads => Volatile.Read(ref updateLoads);

            public RecordingGlyphStore(ResourceStore<byte[]> resources, string asset) : base(resources, asset) { }

            protected override TextureUpload LoadCharacter(SharpFNT.Character character)
            {
                if (ThreadSafety.IsUpdateThread)
                    Interlocked.Increment(ref updateLoads);
                return base.LoadCharacter(character);
            }
        }
    }
}
