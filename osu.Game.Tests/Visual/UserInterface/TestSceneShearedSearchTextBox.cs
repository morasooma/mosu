// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.Tests.Visual.UserInterface
{
    public partial class TestSceneShearedSearchTextBox : OsuTestScene
    {
        [Test]
        public void TestAllColourSchemes()
        {
            foreach (var scheme in Enum.GetValues(typeof(OverlayColourScheme)).Cast<OverlayColourScheme>())
                AddStep($"set {scheme} scheme", () => Child = createContent(scheme));
        }

        [Test]
        public void TestHiddenFilterStatusCoalescesUpdates()
        {
            Drawable content = null!;
            ShearedFilterTextBox text = null!;
            int changes = 0;
            AddStep("load filter text", () => Child = content = createContent(OverlayColourScheme.Aquamarine));
            AddUntilStep("initial status applied", () =>
            {
                text = this.ChildrenOfType<ShearedFilterTextBox>().Single();
                return text.StatusText.ToString() == "12345 matches";
            });
            AddStep("detach hidden legacy filter", () => Remove(content, false));
            AddRepeatStep("change status without updating filter", () => text.StatusText = $"Obsolete matches {++changes}", 30);
            AddAssert("only one status publication queued", () =>
                ((ICollection)typeof(osu.Framework.Threading.Scheduler)
                    .GetField("runQueue", BindingFlags.NonPublic | BindingFlags.Instance)!
                    .GetValue(typeof(Drawable).GetProperty("Scheduler", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(text))!).Count == 1);
            AddStep("restore filter with current status", () =>
            {
                text.StatusText = "Latest match count";
                Add(content);
            });
            AddUntilStep("latest status published", () => text.StatusText.ToString() == "Latest match count");
        }

        private Drawable createContent(OverlayColourScheme colourScheme)
        {
            var colourProvider = new OverlayColourProvider(colourScheme);

            return new DependencyProvidingContainer
            {
                RelativeSizeAxes = Axes.Both,
                CachedDependencies = new (Type, object)[]
                {
                    (typeof(OverlayColourProvider), colourProvider)
                },
                Child = new FillFlowContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Spacing = new Vector2(0f, 5f),
                    Children = new Drawable[]
                    {
                        new ShearedSearchTextBox
                        {
                            Origin = Anchor.Centre,
                            Anchor = Anchor.Centre,
                            RelativeSizeAxes = Axes.X,
                            Width = 0.5f
                        },
                        new ShearedFilterTextBox
                        {
                            Origin = Anchor.Centre,
                            Anchor = Anchor.Centre,
                            RelativeSizeAxes = Axes.X,
                            Width = 0.5f,
                            StatusText = "12345 matches",
                        },
                    }
                },
            };
        }
    }
}
