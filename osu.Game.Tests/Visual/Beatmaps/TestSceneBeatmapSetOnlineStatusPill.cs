// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Development;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Configuration;
using osu.Game.Overlays;
using osu.Game.Tests.Visual.UserInterface;
using SharpFNT;
using osuTK;

namespace osu.Game.Tests.Visual.Beatmaps
{
    public partial class TestSceneBeatmapSetOnlineStatusPill : ThemeComparisonTestScene
    {
        private bool showUnknownStatus;
        private bool animated = true;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        protected override Drawable CreateContent() => new FillFlowContainer
        {
            AutoSizeAxes = Axes.Both,
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Direction = FillDirection.Vertical,
            Spacing = new Vector2(0, 10),
            ChildrenEnumerable = Enum.GetValues(typeof(BeatmapOnlineStatus)).Cast<BeatmapOnlineStatus>().Select(status => new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = 20,
                Children = new Drawable[]
                {
                    new BeatmapSetOnlineStatusPill
                    {
                        ShowUnknownStatus = showUnknownStatus,
                        Animated = animated,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Status = status
                    },
                }
            })
        };

        private IEnumerable<BeatmapSetOnlineStatusPill> statusPills => this.ChildrenOfType<BeatmapSetOnlineStatusPill>();

        [Test]
        public void TestFixedWidth()
        {
            AddStep("create themed content", () => CreateThemedContent(OverlayColourScheme.Red));

            AddStep("set fixed width", () => statusPills.ForEach(pill =>
            {
                pill.AutoSizeAxes = Axes.Y;
                pill.Width = 90;
            }));

            AddStep("toggle show unknown", () =>
            {
                showUnknownStatus = !showUnknownStatus;
                CreateThemedContent(OverlayColourScheme.Red);
            });

            AddStep("toggle animate", () =>
            {
                animated = !animated;
                CreateThemedContent(OverlayColourScheme.Red);
            });

            AddStep("unset fixed width", () => statusPills.ForEach(pill => pill.AutoSizeAxes = Axes.Both));
        }

        [Test]
        public void TestNonAnimatedStatusChangesDoNotScheduleAlphaTransforms()
        {
            BeatmapSetOnlineStatusPill pill = null!;

            AddStep("add non-animated pill", () => Add(pill = new BeatmapSetOnlineStatusPill
            {
                Animated = false,
                ShowUnknownStatus = true,
                Status = BeatmapOnlineStatus.Graveyard,
            }));
            AddUntilStep("pill loaded", () => pill.IsLoaded);
            AddAssert("initial status visible without alpha transform", () => pill.Alpha == 1 && !pill.TransformsForTargetMember(nameof(Drawable.Alpha)).Any());

            AddStep("change to loved", () => pill.Status = BeatmapOnlineStatus.Loved);
            AddAssert("new status still visible without alpha transform", () => pill.Alpha == 1 && !pill.TransformsForTargetMember(nameof(Drawable.Alpha)).Any());

            BeatmapSetOnlineStatusPill hiddenPill = null!;
            AddStep("add non-animated hidden pill", () => Add(hiddenPill = new BeatmapSetOnlineStatusPill
            {
                Animated = false,
                Status = BeatmapOnlineStatus.Graveyard,
            }));
            AddUntilStep("hidden pill loaded", () => hiddenPill.IsLoaded);
            AddStep("hide unknown status", () => hiddenPill.Status = BeatmapOnlineStatus.None);
            AddAssert("unknown status hidden without alpha transform", () => hiddenPill.Alpha == 0 && !hiddenPill.TransformsForTargetMember(nameof(Drawable.Alpha)).Any());
            AddStep("restore known status", () => hiddenPill.Status = BeatmapOnlineStatus.Ranked);
            AddAssert("known status visible without alpha transform", () => hiddenPill.Alpha == 1 && !hiddenPill.TransformsForTargetMember(nameof(Drawable.Alpha)).Any());
        }

        [TestCase("Default", "Torus")]
        [TestCase("Inter", "Inter")]
        public void TestAsyncLoadWarmsDynamicStatusVocabularyBeforeUpdate(string configuredFamily, string resourceFamily)
        {
            CountingGlyphStore glyphs = null!;
            FontStore fonts = null!;
            BeatmapSetOnlineStatusPill pill = null!;
            string previousFamily = null!;
            AddStep("load status with isolated font cache", () =>
            {
                previousFamily = config.Get<string>(OsuSetting.ForkCustomUIFont);
                config.SetValue(OsuSetting.ForkCustomUIFont, configuredFamily);
                var resources = new NamespacedResourceStore<byte[]>(new DllResourceStore(Assembly.Load("osu.Game.Resources")), $"Fonts/{resourceFamily}");
                glyphs = new CountingGlyphStore(resources, $"{resourceFamily}-Bold");
                fonts = new FontStore(new DummyRenderer(), glyphs, minFilterMode: TextureFilteringMode.Linear);
                pill = new BeatmapSetOnlineStatusPill { Animated = false, ShowUnknownStatus = true, Status = BeatmapOnlineStatus.None };
                var container = new DependencyProvidingContainer
                {
                    AutoSizeAxes = Axes.Both,
                    CachedDependencies = new (Type, object)[] { (typeof(FontStore), fonts) },
                    Child = pill,
                };
                LoadComponentAsync(container, loaded => Add(loaded));
            });
            AddUntilStep("loaded and fonts warmed", () => pill.IsLoaded && glyphs.WorkerReads > 0);
            foreach (var status in Enum.GetValues<BeatmapOnlineStatus>())
            {
                AddStep($"set {status}", () => pill.Status = status);
                AddWaitStep("complete layout", 2);
                AddAssert("no glyph pixel read on Update", () => glyphs.UpdateReads == 0);
            }
            AddStep("release isolated font", () =>
            {
                Clear();
                fonts.Dispose();
                config.SetValue(OsuSetting.ForkCustomUIFont, previousFamily);
            });
        }

        private class CountingGlyphStore : GlyphStore
        {
            public int WorkerReads;
            public int UpdateReads;

            public CountingGlyphStore(ResourceStore<byte[]> resources, string fontName)
                : base(resources, fontName)
            {
            }

            protected override TextureUpload LoadCharacter(Character character)
            {
                if (ThreadSafety.IsUpdateThread)
                    UpdateReads++;
                else
                    WorkerReads++;
                return base.LoadCharacter(character);
            }
        }

        [Test]
        public void TestRepeatedStatusDoesNotRestartFade()
        {
            BeatmapSetOnlineStatusPill pill = null!;
            AddStep("add animated pill", () => Add(pill = new BeatmapSetOnlineStatusPill { Status = BeatmapOnlineStatus.Graveyard }));
            AddUntilStep("pill loaded", () => pill.IsLoaded);
            AddStep("hide pill", () => pill.Status = BeatmapOnlineStatus.None);
            AddUntilStep("pill hidden", () => pill.Alpha == 0);
            AddStep("change to loved", () => pill.Status = BeatmapOnlineStatus.Loved);
            AddAssert("fade started", () => pill.TransformsForTargetMember(nameof(Drawable.Alpha)).Any());
            AddStep("repeat loved without changing transforms", () =>
            {
                var before = pill.TransformsForTargetMember(nameof(Drawable.Alpha)).ToArray();
                pill.Status = BeatmapOnlineStatus.Loved;
                Assert.That(pill.TransformsForTargetMember(nameof(Drawable.Alpha)), Is.EquivalentTo(before));
            });
            AddStep("change to unknown", () => pill.Status = BeatmapOnlineStatus.None);
            AddUntilStep("unknown hidden", () => pill.Alpha == 0);
            AddStep("restore ranked", () => pill.Status = BeatmapOnlineStatus.Ranked);
            AddUntilStep("ranked visible", () => pill.Alpha == 1);
        }

        [Test]
        public void TestChangeLabels()
        {
            AddStep("Change labels", () =>
            {
                foreach (var pill in this.ChildrenOfType<BeatmapSetOnlineStatusPill>())
                {
                    switch (pill.Status)
                    {
                        // cycle at end
                        case BeatmapOnlineStatus.Loved:
                            pill.Status = BeatmapOnlineStatus.LocallyModified;
                            break;

                        default:
                            pill.Status = (pill.Status + 1);
                            break;
                    }
                }
            });
        }
    }
}
