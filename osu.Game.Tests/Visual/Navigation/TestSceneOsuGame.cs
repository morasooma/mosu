// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Graphics.Textures;
using osu.Framework.Platform;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Input;
using osu.Game.Input.Bindings;
using osu.Game.Online.API;
using osu.Game.Online.Chat;
using osu.Game.Overlays;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Screens.Menu;
using osu.Game.Skinning;
using osuTK;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Navigation
{
    [TestFixture]
    public partial class TestSceneOsuGame : OsuGameTestScene
    {
        private IReadOnlyList<Type> requiredGameDependencies => new[]
        {
            typeof(OsuGame),
            typeof(OsuLogo),
            typeof(IdleTracker),
            typeof(OnScreenDisplay),
            typeof(INotificationOverlay),
            typeof(BeatmapListingOverlay),
            typeof(DashboardOverlay),
            typeof(NewsOverlay),
            typeof(ChannelManager),
            typeof(ChatOverlay),
            typeof(SettingsOverlay),
            typeof(UserProfileOverlay),
            typeof(BeatmapSetOverlay),
            typeof(LoginOverlay),
            typeof(MusicController),
            typeof(AccountCreationOverlay),
            typeof(IDialogOverlay),
            typeof(ScreenshotManager)
        };

        private IReadOnlyList<Type> requiredGameBaseDependencies => new[]
        {
            typeof(OsuGameBase),
            typeof(Bindable<RulesetInfo>),
            typeof(IBindable<RulesetInfo>),
            typeof(Bindable<IReadOnlyList<Mod>>),
            typeof(IBindable<IReadOnlyList<Mod>>),
            typeof(LargeTextureStore),
            typeof(OsuConfigManager),
            typeof(SkinManager),
            typeof(ISkinSource),
            typeof(IAPIProvider),
            typeof(RulesetStore),
            typeof(ScoreManager),
            typeof(BeatmapManager),
            typeof(IRulesetConfigCache),
            typeof(OsuColour),
            typeof(IBindable<WorkingBeatmap>),
            typeof(Bindable<WorkingBeatmap>),
            typeof(GlobalActionContainer),
            typeof(PreviewTrackManager),
        };

        [Resolved]
        private OsuGameBase gameBase { get; set; }

        [Test]
        public void TestCursorHidesWhenIdle()
        {
            AddStep("move mouse inside game bounds", () => InputManager.MoveMouseTo(Game.ScreenSpaceDrawQuad.TopLeft + new Vector2(20)));
            AddStep("click mouse", () => InputManager.Click(MouseButton.Left));
            AddUntilStep("wait until idle", () => Game.IsIdle.Value);
            AddUntilStep("menu cursor hidden", () => Game.GlobalCursorDisplay.MenuCursor.ActiveCursor.Alpha == 0);
            AddStep("click mouse", () => InputManager.Click(MouseButton.Left));
            AddUntilStep("menu cursor shown", () => Game.GlobalCursorDisplay.MenuCursor.ActiveCursor.Alpha == 1);
        }

        [Test]
        public void TestNullRulesetHandled()
        {
            RulesetInfo ruleset = null;

            AddStep("store current ruleset", () => ruleset = Ruleset.Value);
            AddStep("set global ruleset to null value", () => Ruleset.Value = null);

            AddAssert("ruleset still valid", () => Ruleset.Value.Available);
            AddAssert("ruleset unchanged", () => ReferenceEquals(Ruleset.Value, ruleset));
        }

        [Test]
        public void TestSeparateSkinsPerRuleset()
        {
            SkinManager skins = null;
            RulesetStore rulesets = null;

            AddStep("prepare ruleset skins", () =>
            {
                skins = Game.Dependencies.Get<SkinManager>();
                rulesets = Game.Dependencies.Get<RulesetStore>();

                Game.LocalConfig.SetValue(OsuSetting.ForkSeparateSkinsPerRuleset, false);
                Game.LocalConfig.SetValue(OsuSetting.Skin, SkinInfo.ARGON_SKIN.ToString());
                Game.LocalConfig.SetValue(OsuSetting.ForkOsuSkin, SkinInfo.ARGON_SKIN.ToString());
                Game.LocalConfig.SetValue(OsuSetting.ForkManiaSkin, SkinInfo.TRIANGLES_SKIN.ToString());
                skins.SetSkinFromConfiguration(SkinInfo.ARGON_SKIN.ToString());
                Game.Ruleset.Value = rulesets.GetRuleset(0);
            });

            AddStep("enable ruleset skins", () => Game.LocalConfig.SetValue(OsuSetting.ForkSeparateSkinsPerRuleset, true));
            AddAssert("osu skin applied", () => skins.CurrentSkinInfo.Value.ID == SkinInfo.ARGON_SKIN);

            AddStep("switch to mania", () => Game.Ruleset.Value = rulesets.GetRuleset(3));
            AddAssert("mania skin applied", () => skins.CurrentSkinInfo.Value.ID == SkinInfo.TRIANGLES_SKIN);

            AddStep("change active skin", () => skins.CurrentSkinInfo.Value = skins.DefaultClassicSkin.SkinInfo);
            AddAssert("mania skin remembered", () => Game.LocalConfig.Get<string>(OsuSetting.ForkManiaSkin) == SkinInfo.CLASSIC_SKIN.ToString());
            AddAssert("osu skin unchanged", () => Game.LocalConfig.Get<string>(OsuSetting.ForkOsuSkin) == SkinInfo.ARGON_SKIN.ToString());

            AddStep("set missing mania skin", () => Game.LocalConfig.SetValue(OsuSetting.ForkManiaSkin, Guid.NewGuid().ToString()));
            AddAssert("falls back to global skin", () => skins.CurrentSkinInfo.Value.ID == SkinInfo.ARGON_SKIN);
            AddAssert("normalises missing skin", () => Game.LocalConfig.Get<string>(OsuSetting.ForkManiaSkin) == SkinInfo.ARGON_SKIN.ToString());

            AddStep("disable ruleset skins", () => Game.LocalConfig.SetValue(OsuSetting.ForkSeparateSkinsPerRuleset, false));
            AddAssert("global skin restored", () => skins.CurrentSkinInfo.Value.ID == SkinInfo.ARGON_SKIN);
        }

        [Test]
        public void TestSwitchThreadExecutionMode()
        {
            AddStep("Change thread mode to multi threaded", () => { Game.Dependencies.Get<FrameworkConfigManager>().SetValue(FrameworkSetting.ExecutionMode, ExecutionMode.MultiThreaded); });
            AddStep("Change thread mode to single thread", () => { Game.Dependencies.Get<FrameworkConfigManager>().SetValue(FrameworkSetting.ExecutionMode, ExecutionMode.SingleThread); });
        }

        [Test]
        public void TestUnavailableRulesetHandled()
        {
            RulesetInfo ruleset = null;

            AddStep("store current ruleset", () => ruleset = Ruleset.Value);
            AddStep("set global ruleset to invalid value", () => Ruleset.Value = new RulesetInfo
            {
                Name = "unavailable",
                Available = false,
            });

            AddAssert("ruleset still valid", () => Ruleset.Value.Available);
            AddAssert("ruleset unchanged", () => ReferenceEquals(Ruleset.Value, ruleset));
        }

        [Test]
        public void TestAvailableDependencies()
        {
            AddAssert("check OsuGame DI members", () =>
            {
                foreach (var type in requiredGameDependencies)
                {
                    if (Game.Dependencies.Get(type) == null)
                        throw new InvalidOperationException($"{type} has not been cached");
                }

                return true;
            });

            AddAssert("check OsuGameBase DI members", () =>
            {
                foreach (var type in requiredGameBaseDependencies)
                {
                    if (gameBase.Dependencies.Get(type) == null)
                        throw new InvalidOperationException($"{type} has not been cached");
                }

                return true;
            });
        }
    }
}
