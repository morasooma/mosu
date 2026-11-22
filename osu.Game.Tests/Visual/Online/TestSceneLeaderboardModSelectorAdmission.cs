using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Overlays;
using osu.Game.Overlays.BeatmapSet;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Taiko;
using osu.Game.Rulesets.UI;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Online
{
    public partial class TestSceneLeaderboardModSelectorAdmission : OsuManualInputManagerTestScene
    {
        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Blue);

        private TestLeaderboardModSelector selector = null!;
        private int largestAdmission;
        private bool partialObserved;
        private int expectedCount;
        private Type[] expectedTypes = Array.Empty<Type>();

        [Test]
        public void TestAdmissionIsBoundedAndOrderPreserved()
        {
            createSelector();
            waitForComplete();
            AddAssert("at most four icons admitted per update", () => largestAdmission, () => Is.InRange(1, 4));
            AddAssert("partial selector observed", () => partialObserved);
            assertOrder();
        }

        [Test]
        public void TestRulesetReplacementDuringAdmission()
        {
            bool replaced = false;
            createSelector(() => Schedule(() =>
            {
                setRuleset(new TaikoRuleset());
                replaced = true;
            }));
            AddUntilStep("ruleset replaced during admission", () => replaced);
            waitForComplete();
            assertOrder();
            AddWaitStep("old pending work cannot reappear", 10);
            assertOrder();
        }

        [Test]
        public void TestNullRulesetDuringAdmissionClearsPending()
        {
            bool cleared = false;
            createSelector(() => Schedule(() =>
            {
                selector.Ruleset.Value = null;
                cleared = true;
            }));
            AddUntilStep("ruleset cleared during admission", () => cleared);
            AddWaitStep("allow pending updates", 10);
            AddAssert("icons remain empty", () => !selector.ChildrenOfType<ModIcon>().Any());
            AddAssert("selection empty", () => selector.SelectedMods, () => Is.Empty);
            AddStep("set ruleset again", () => setRuleset(new OsuRuleset()));
            waitForComplete();
            assertOrder();
        }

        [Test]
        public void TestSelectionAndDeselectAllAfterAdmission()
        {
            createSelector();
            waitForComplete();
            AddStep("click NoMod", () =>
            {
                InputManager.MoveMouseTo(selector.ChildrenOfType<ModIcon>().First());
                InputManager.Click(MouseButton.Left);
            });
            AddAssert("NoMod selected", () => selector.SelectedMods.SingleOrDefault(), () => Is.TypeOf<ModNoMod>());
            AddStep("deselect all", () => selector.DeselectAll());
            AddAssert("selection empty", () => selector.SelectedMods, () => Is.Empty);
            AddAssert("all icons deselected", () => selector.ChildrenOfType<ModIcon>().All(icon => !icon.Selected.Value));
        }

        [Test]
        public void TestDisposeDuringAdmissionDoesNotPublishMoreIcons()
        {
            bool removed = false;
            createSelector(() => Schedule(() =>
            {
                Clear();
                removed = true;
            }));
            AddUntilStep("selector removed during admission", () => removed);
            AddUntilStep("selector disposed", () => selector.Disposed);
            AddWaitStep("allow pending updates", 10);
            AddAssert("no attached icons", () => !this.ChildrenOfType<ModIcon>().Any());
        }

        [Test]
        public void TestMaskedSelectorResumesWhenScrolledIntoView()
        {
            BasicScrollContainer scroll = null!;
            AddStep("create selector below viewport", () =>
            {
                Child = scroll = new BasicScrollContainer
                {
                    Width = 800,
                    Height = 300,
                    Child = new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 2000,
                        Child = selector = new TestLeaderboardModSelector
                        {
                            Y = 1000,
                        },
                    },
                };
                setRuleset(new OsuRuleset());
            });
            AddWaitStep("offscreen layout settles", 10);
            AddStep("scroll to selector", () => scroll.ScrollTo(selector, false));
            waitForComplete();
            assertOrder();
        }

        private void createSelector(Action? duringAdmission = null) => AddStep("create selector", () =>
        {
            InputManager.MoveMouseTo(this);
            largestAdmission = 0;
            partialObserved = false;
            Child = selector = new TestLeaderboardModSelector
            {
                RelativeSizeAxes = Axes.None,
                Width = 800,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            };
            int previous = 0;
            selector.OnUpdate += _ =>
            {
                int count = selector.ChildrenOfType<ModIcon>().Count();
                largestAdmission = Math.Max(largestAdmission, count - previous);
                partialObserved |= count > 0 && count < expectedCount;
                previous = count;
                if (count > 0 && count < expectedCount && duringAdmission != null)
                {
                    var action = duringAdmission;
                    duringAdmission = null;
                    action();
                }
            };
            setRuleset(new OsuRuleset());
        });

        private partial class TestLeaderboardModSelector : LeaderboardModSelector
        {
            public bool Disposed => IsDisposed;
        }

        private void setRuleset(Ruleset ruleset)
        {
            expectedTypes = new[] { typeof(ModNoMod) }.Concat(ruleset.AllMods.Where(mod => mod.UserPlayable).Select(mod => mod.GetType())).ToArray();
            expectedCount = expectedTypes.Length;
            selector.Ruleset.Value = ruleset.RulesetInfo;
        }

        private void waitForComplete() => AddUntilStep("all icons published", () => selector.ChildrenOfType<ModIcon>().Count(), () => Is.EqualTo(expectedCount));

        private void assertOrder() => AddAssert("NoMod then original ruleset order", () => selector.ChildrenOfType<ModIcon>().Select(icon => icon.Mod.GetType()),
            () => Is.EqualTo(expectedTypes));
    }
}
