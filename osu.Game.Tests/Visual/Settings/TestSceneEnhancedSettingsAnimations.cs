using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings;
using osuTK;

namespace osu.Game.Tests.Visual.Settings
{
    public partial class TestSceneEnhancedSettingsAnimations : OsuTestScene
    {
        private TemporaryNativeStorage storage = null!;
        private OsuConfigManager config = null!;
        private BasicScrollContainer scroll = null!;
        private Drawable row = null!;
        private Drawable visual = null!;
        private Container neighbour = null!;
        private bool sawExpansion;
        private bool geometryChanged;
        private float rowHeight;
        private float neighbourY;
        private bool watchGeometry;
        private InterfaceAnimationMode previousMode;
        private bool previousEnhanced;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create isolated config", () =>
            {
                Clear();
                config?.Dispose();
                storage?.Dispose();
                storage = new TemporaryNativeStorage("enhanced-settings-" + Guid.NewGuid());
                config = new OsuConfigManager(storage);
                previousMode = InterfaceAnimation.Mode.Value;
                previousEnhanced = InterfaceAnimation.Enhanced.Value;
                InterfaceAnimation.Mode.Value = InterfaceAnimationMode.Full;
                watchGeometry = sawExpansion = geometryChanged = false;
            });
        }

        [TearDownSteps]
        public void TearDownSteps() => AddStep("restore animation policy", () =>
        {
            InterfaceAnimation.Mode.Value = previousMode;
            InterfaceAnimation.Enhanced.Value = previousEnhanced;
        });

        [TestCase("v2")]
        [TestCase("legacy")]
        [TestCase("button")]
        [TestCase("legacy button")]
        public void TestExpansionPreservesRowAndNeighbourGeometry(string kind)
        {
            AddStep("create actual setting row", () => create(kind));
            AddUntilStep("row loaded", () => row.IsLoaded && row.DrawHeight > 0);
            AddWaitStep("settle layout", 3);
            AddStep("watch geometry and enable", () =>
            {
                rowHeight = row.DrawHeight;
                neighbourY = neighbour.Y;
                watchGeometry = true;
                config.SetValue(OsuSetting.ForkEnhancedAnimations, true);
            });
            AddUntilStep("expanding content observed", () => sawExpansion);
            AddUntilStep("content fully expanded", () => visual.Scale == Vector2.One);
            AddAssert("row layout never animated", () => !geometryChanged && row.Scale == Vector2.One);
            AddStep("scroll row away", () => scroll.ScrollToEnd(false));
            AddWaitStep("settle offscreen", 3);
            AddStep("scroll back", () =>
            {
                sawExpansion = false;
                scroll.ScrollToStart(false);
            });
            AddUntilStep("re-entry animates", () => sawExpansion);
            AddStep("disable during expansion", () => config.SetValue(OsuSetting.ForkEnhancedAnimations, false));
            AddAssert("disabled effect finishes immediately", () => visual.Scale == Vector2.One);
            AddAssert("neighbour still in same place", () => !geometryChanged);
        }

        [TestCase(InterfaceAnimationMode.Minimal)]
        [TestCase(InterfaceAnimationMode.Disabled)]
        public void TestReducedMotionOverridesEnhancedAnimations(InterfaceAnimationMode mode)
        {
            AddStep("create row", () => create("v2"));
            AddUntilStep("row loaded", () => row.IsLoaded && row.DrawHeight > 0);
            AddStep("enable enhancement", () => config.SetValue(OsuSetting.ForkEnhancedAnimations, true));
            AddUntilStep("expansion running", () => sawExpansion);
            AddStep("reduce motion immediately", () => InterfaceAnimation.Mode.Value = mode);
            AddAssert("content restored immediately", () => visual.Scale == Vector2.One);
            AddStep("reset observation", () => sawExpansion = false);
            AddWaitStep("observe reduced mode", 5);
            AddAssert("no expansion in reduced mode", () => !sawExpansion && visual.Scale == Vector2.One);
        }

        [Test]
        public void TestSearchRevealAnimatesAgain()
        {
            AddStep("create row", () => create("v2"));
            AddUntilStep("row loaded", () => row.IsLoaded && row.DrawHeight > 0);
            AddStep("enable enhancement", () => config.SetValue(OsuSetting.ForkEnhancedAnimations, true));
            AddUntilStep("expansion running", () => sawExpansion);
            AddStep("filter row out", () => ((SettingsItemV2)row).MatchingFilter = false);
            AddWaitStep("filtered out", 3);
            AddStep("reveal search match", () =>
            {
                sawExpansion = false;
                ((SettingsItemV2)row).MatchingFilter = true;
            });
            AddUntilStep("search match expands again", () => sawExpansion);
        }

        [Test]
        public void TestOpeningAndReopeningActualSettingsPanel()
        {
            AnimationTestPanel panel = null!;
            AddStep("open real panel with enhancement already enabled", () =>
            {
                config.SetValue(OsuSetting.ForkEnhancedAnimations, true);
                Child = new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    CachedDependencies = new (Type, object)[] { (typeof(OsuConfigManager), config) },
                    Child = panel = new AnimationTestPanel { State = { Value = Visibility.Visible } },
                };
                row = panel.Row;
                row.OnUpdate += _ =>
                {
                    visual ??= row.ChildrenOfType<CompositeDrawable>().First(d => !ReferenceEquals(d, row));
                    sawExpansion |= visual.Scale.X < 0.99f;
                };
                visual = null!;
            });
            AddUntilStep("initial panel appearance expands row", () => sawExpansion);
            AddUntilStep("initial appearance completed", () => visual.Scale == Vector2.One);
            AddStep("close panel", () => panel.Hide());
            AddUntilStep("panel fully hidden", () => !panel.IsPresent);
            AddStep("reopen panel", () =>
            {
                sawExpansion = false;
                panel.Show();
            });
            AddUntilStep("reopening expands row again", () => sawExpansion);
            AddUntilStep("reopening completed", () => visual.Scale == Vector2.One);
            AddAssert("full width content centred in original row", () =>
                Math.Abs(visual.ScreenSpaceDrawQuad.Centre.X - row.ScreenSpaceDrawQuad.Centre.X) < 0.1f);
        }

        [TestCase(false, 600)]
        [TestCase(true, 220)]
        public void TestActualPanelUsesEnhancedTimingOnlyWhenEnabled(bool enhanced, double expectedDuration)
        {
            AnimationTestPanel panel = null!;
            AddStep("create actual settings panel", () =>
            {
                InterfaceAnimation.Enhanced.Value = enhanced;
                Child = new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    CachedDependencies = new (Type, object)[] { (typeof(OsuConfigManager), config) },
                    Child = panel = new AnimationTestPanel(),
                };
            });
            AddUntilStep("panel ready", () => panel.IsLoaded);
            AddStep("open and inspect slide", () =>
            {
                panel.FinishTransforms(true);
                panel.Show();
                Assert.That(panel.SlideDuration, Is.EqualTo(expectedDuration).Within(0.000001));
            });
            AddWaitStep("opening completes", 5);
            AddStep("reverse without jumping", () =>
            {
                float before = panel.SlidePosition;
                panel.Hide();
                Assert.That(panel.SlidePosition, Is.EqualTo(before));
                Assert.That(panel.SlideDuration, Is.EqualTo(expectedDuration).Within(0.000001));
            });
            AddUntilStep("fully closed", () => !panel.IsPresent);
            AddStep("switch mode and reopen", () =>
            {
                InterfaceAnimation.Enhanced.Value = !enhanced;
                panel.Show();
                Assert.That(panel.SlideDuration, Is.EqualTo(enhanced ? 600 : 220).Within(0.000001));
            });
        }

        private partial class AnimationTestPanel : SettingsPanel
        {
            public readonly SettingsItemV2 Row = new SettingsItemV2(new FormCheckBox { Caption = "Panel setting" });

            public float SlidePosition => ContentContainer.X;

            public double SlideDuration
            {
                get
                {
                    var slide = ContentContainer.TransformsForTargetMember(nameof(Drawable.X)).Last();
                    return slide.EndTime - slide.StartTime;
                }
            }

            public AnimationTestPanel()
                : base(false)
            {
                AddSection(new AnimationTestSection(Row));
            }
        }

        private partial class AnimationTestSection : SettingsSection
        {
            public override LocalisableString Header => "Animation test";
            public override Drawable CreateIcon() => new Container();

            public AnimationTestSection(SettingsItemV2 row)
            {
                Add(new AnimationTestSubsection(row));
            }
        }

        private partial class AnimationTestSubsection : SettingsSubsection
        {
            protected override LocalisableString Header => "Row test";

            public AnimationTestSubsection(SettingsItemV2 row)
            {
                Add(row);
            }
        }

        private void create(string kind)
        {
            row = kind switch
            {
                "legacy" => new SettingsCheckbox { LabelText = "Legacy setting" },
                "button" => new SettingsButtonV2 { Text = "Settings button" },
                "legacy button" => new SettingsButton { Text = "Legacy button" },
                _ => new SettingsItemV2(new FormCheckBox { Caption = "Setting", HintText = "A real settings control" }),
            };

            Child = new DependencyProvidingContainer
            {
                Size = new Vector2(400, 220),
                CachedDependencies = new (Type, object)[]
                {
                    (typeof(OsuConfigManager), config),
                    (typeof(OverlayColourProvider), new OverlayColourProvider(OverlayColourScheme.Purple)),
                },
                Child = scroll = new BasicScrollContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Children = new Drawable[]
                        {
                            row,
                            neighbour = new Container { RelativeSizeAxes = Axes.X, Height = 30 },
                            new Container { RelativeSizeAxes = Axes.X, Height = 1000 },
                        },
                    },
                },
            };
            row.OnUpdate += _ =>
            {
                visual ??= row.ChildrenOfType<CompositeDrawable>().First(d => !ReferenceEquals(d, row));
                if (visual.Scale.X < 0.99f)
                    sawExpansion = true;
                if (watchGeometry && (Math.Abs(row.DrawHeight - rowHeight) > 0.1f || Math.Abs(neighbour.Y - neighbourY) > 0.1f))
                    geometryChanged = true;
            };
            visual = null!;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            config?.Dispose();
            storage?.Dispose();
        }
    }
}
