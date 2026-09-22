// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Online.API.Requests;
using osu.Game.Overlays;
using osu.Game.Graphics.UserInterfaceV2;

namespace osu.Game.Tests.Visual.Menus
{
    public partial class TestSceneAccountRestrictionBanner : OsuTestScene
    {
        private AccountRestrictionBanner banner = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create banner", () => Child = banner = new AccountRestrictionBanner(false));
        }

        [Test]
        public void TestRestrictionStates()
        {
            AddStep("show generic restriction", () => banner.DisplayStatus(new AccountRestrictionStatusResponse
            {
                IsRestricted = true,
                Category = "other",
            }));
            AddUntilStep("banner shown", () => banner.Alpha, () => Is.EqualTo(1).Within(0.01));
            AddAssert("actions hidden", () => banner.ChildrenOfType<FillFlowContainer<RoundedButton>>().Single().Alpha, () => Is.Zero);

            AddStep("show cheating restriction", () => banner.DisplayStatus(new AccountRestrictionStatusResponse
            {
                IsRestricted = true,
                Category = "cheating",
                DiscordUsername = "xtillius1",
                DiscordUrl = "https://discord.gg/bRSDssrgUE",
            }));
            AddAssert("actions shown", () => banner.ChildrenOfType<FillFlowContainer<RoundedButton>>().Single().Alpha, () => Is.EqualTo(1));

            AddStep("remove restriction", () => banner.DisplayStatus(new AccountRestrictionStatusResponse()));
            AddAssert("banner hidden", () => banner.Alpha, () => Is.Zero);
        }
    }
}
