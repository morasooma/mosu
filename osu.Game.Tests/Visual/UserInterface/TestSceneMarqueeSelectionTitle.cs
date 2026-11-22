using System;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Framework.Testing;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;

namespace osu.Game.Tests.Visual.UserInterface
{
    public partial class TestSceneMarqueeSelectionTitle : OsuTestScene
    {
        protected override bool UseFreshStoragePerRun => true;

        private MarqueeContainer marquee = null!;
        private Container widthContainer = null!;
        private int constructions;
        private int disposals;
        private string expectedRomanised = string.Empty;

        [Test]
        public void TestSelectionTitleFitOverflowAndReplacement()
        {
            AddStep("load title container", () =>
            {
                Child = widthContainer = new Container
                {
                    Width = 600,
                    AutoSizeAxes = Axes.Y,
                    Child = marquee = new MarqueeContainer
                    {
                        OverflowSpacing = 50,
                        CreateContent = () => new OsuSpriteText { Text = "Initial", Font = OsuFont.Style.Title },
                    },
                };
            });
            AddUntilStep("marquee loaded", () => marquee.IsLoaded);
            setTitle("Short", "短い");
            AddWaitStep("fitting layout", 3);
            AddAssert("fitting title has no filler", () => marquee.ChildrenOfType<OsuSpriteText>().Count() == 1);
            AddAssert("fitting title is exact", () => marquee.ChildrenOfType<OsuSpriteText>().All(t => t.Text.ToString() == expectedRomanised));
            AddStep("record fitting construction count", () =>
            {
                TestContext.Progress.WriteLine($"MARQUEE fitting constructions={constructions}");
                Assert.That(constructions, Is.EqualTo(1));
            });
            setTitle(new string('W', 100), "長いタイトル");
            AddWaitStep("overflow layout", 3);
            AddAssert("overflow title has visible filler", () => marquee.ChildrenOfType<OsuSpriteText>().Count() == 2
                                                                      && marquee.ChildrenOfType<OsuSpriteText>().All(d => d.Alpha == 1));
            AddAssert("overflow primary and filler exact, with 50px gap", () =>
            {
                var labels = marquee.ChildrenOfType<OsuSpriteText>().ToArray();
                return labels.Length == 2 && labels.All(t => t.Text.ToString() == expectedRomanised)
                       && Math.Abs(labels[1].X - labels[0].X - labels[0].DrawWidth - 50) < 1;
            });
            AddStep("record overflow construction count", () =>
            {
                TestContext.Progress.WriteLine($"MARQUEE overflow constructions={constructions}");
                Assert.That(constructions, Is.EqualTo(2));
            });
            setTitle(new string('X', 100), "別の長いタイトル");
            AddWaitStep("replacement layout", 3);
            AddStep("record replacement construction count", () =>
            {
                TestContext.Progress.WriteLine($"MARQUEE replacement constructions={constructions}");
                Assert.That(constructions, Is.EqualTo(2));
                Assert.That(disposals, Is.EqualTo(2), "replaced primary and filler disposed");
            });
            AddAssert("replacement primary and filler exact", () => marquee.ChildrenOfType<OsuSpriteText>().All(t => t.Text.ToString() == expectedRomanised));
            AddStep("fit again", () => widthContainer.Width = 5000);
            AddWaitStep("fitting resize", 3);
            AddAssert("filler hidden while fitting", () => marquee.ChildrenOfType<OsuSpriteText>().Count(d => d.Alpha == 0) == 1);
            AddStep("overflow again", () => widthContainer.Width = 200);
            AddWaitStep("overflow resize", 3);
            AddAssert("existing filler reused on resize", () => constructions == 2);
            AddAssert("scroll uses the original 50px gap", () =>
            {
                var labels = marquee.ChildrenOfType<OsuSpriteText>().ToArray();
                return labels.Length == 2 && labels.All(t => t.Text.ToString() == expectedRomanised)
                       && Math.Abs(labels[1].X - labels[0].X - labels[0].DrawWidth - 50) < 1;
            });
            AddStep("fit last", () => widthContainer.Width = 5000);
            AddWaitStep("settle resized title", 3);
            AddAssert("filler hidden again", () => marquee.ChildrenOfType<OsuSpriteText>().Count(d => d.Alpha == 0) == 1);
            AddAssert("filler remains reusable", () => constructions == 2);
            AddAssert("title remains visible", () => marquee.ChildrenOfType<OsuSpriteText>().Any());

            void setTitle(string romanised, string unicode)
            {
                AddStep($"set {romanised[..Math.Min(romanised.Length, 8)]} title", () =>
                {
                    constructions = 0;
                    disposals = 0;
                    expectedRomanised = romanised;
                    marquee.CreateContent = () =>
                    {
                        constructions++;
                        return new TrackedTitleText(() => disposals++)
                        {
                            Text = new RomanisableString(unicode, romanised),
                            Font = OsuFont.Style.Title,
                        };
                    };
                });
            }
        }

        private partial class TrackedTitleText : OsuSpriteText
        {
            private readonly Action disposed;

            public TrackedTitleText(Action disposed) => this.disposed = disposed;

            protected override void Dispose(bool isDisposing)
            {
                disposed();
                base.Dispose(isDisposing);
            }
        }

        [Test]
        public void TestRepeatedFittingTitleSelectionCost()
        {
            AddStep("load fitting title", () =>
            {
                Child = widthContainer = new Container
                {
                    Width = 700,
                    AutoSizeAxes = Axes.Y,
                    Child = marquee = new MarqueeContainer
                    {
                        OverflowSpacing = 50,
                        CreateContent = () => new OsuSpriteText { Text = "Initial", Font = OsuFont.Style.Title },
                    },
                };
            });
            AddUntilStep("marquee loaded", () => marquee.IsLoaded);
            AddStep("warm up repeated changes", () => measure(30, "cold"));
            AddStep("measure repeated changes", () => measure(200, "warm"));
            AddWaitStep("settle last title", 3);
            AddAssert("last title remains visible", () => marquee.ChildrenOfType<OsuSpriteText>().Any());
        }

        private void measure(int iterations, string label)
        {
            int constructed = 0;
            long bytesBefore = GC.GetAllocatedBytesForCurrentThread();
            long started = Stopwatch.GetTimestamp();
            for (int i = 0; i < iterations; i++)
            {
                string text = $"Song {i:000} — タイトル";
                marquee.CreateContent = () =>
                {
                    constructed++;
                    return new OsuSpriteText { Text = text, Font = OsuFont.Style.Title };
                };
            }
            Assert.That(constructed, Is.EqualTo(iterations));
            TestContext.Progress.WriteLine($"MARQUEE {label} iterations={iterations} constructions={constructed} "
                                           + $"bytes={GC.GetAllocatedBytesForCurrentThread() - bytesBefore} "
                                           + $"ms={Stopwatch.GetElapsedTime(started).TotalMilliseconds:F3}");
        }
    }
}
