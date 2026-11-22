// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Configuration;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Components;
using osu.Game.Screens.Edit.Components.Menus;

namespace osu.Game.Tests.Visual.Editing
{
    public partial class TestSceneEditorScreenModes : EditorTestScene
    {
        [Resolved]
        private FrameworkConfigManager frameworkConfig { get; set; } = null!;

        private string? previousLocale;

        protected override bool UseFreshStoragePerRun => true;

        protected override Ruleset CreateEditorRuleset() => new OsuRuleset();

        [TestCase("en", "Playback speed")]
        [TestCase("ru", "Скорость воспроизведения")]
        [TestCase("es", "Velocidad de reproducción")]
        [TestCase("ja", "再生速度")]
        public void TestLocalisedEditorLabelsFit(string locale, string expectedPlaybackLabel)
        {
            AddStep("switch editor language", () =>
            {
                previousLocale = frameworkConfig.Get<string>(FrameworkSetting.Locale);
                frameworkConfig.SetValue(FrameworkSetting.Locale, locale);
            });

            var menu = () => Editor.ChildrenOfType<EditorMenuBar>().Single();
            var brand = () => menu().ChildrenOfType<TextFlowContainer>().Single();
            var playback = () => Editor.ChildrenOfType<PlaybackControl>().Single();
            var label = () => playback().ChildrenOfType<OsuSpriteText>().Single(t => t.Text.Equals(EditorStrings.PlaybackSpeed));

            AddUntilStep("playback label uses selected language", () => label().Current.Value == expectedPlaybackLabel);
            AddAssert("editor labels fit without overlaps", labelsFit);

            // Simulate a wider UI font without relying on a font installed on the test machine.
            AddStep("increase playback label size", () => label().Font = label().Font.With(size: 22));
            AddAssert("wider label still fits without overlaps", labelsFit);

            AddStep("select 50% speed", () => playback().ChildrenOfType<PlaybackControl.PlaybackTabControl>().Single().Current.Value = 0.5);
            AddAssert("speed control still changes tempo", () => Beatmap.Value.Track.AggregateTempo.Value == 0.5);

            bool labelsFit()
            {
                var title = brand().ScreenSpaceDrawQuad;
                var icon = menu().ChildrenOfType<SpriteIcon>().First().ScreenSpaceDrawQuad;
                var firstItem = menu().ChildrenOfType<EditorMenuBar.DrawableEditorBarMenuItem>().First().ScreenSpaceDrawQuad;
                var speedLabel = label().ScreenSpaceDrawQuad;
                var tabs = playback().ChildrenOfType<PlaybackControl.PlaybackTabControl>().Single().ScreenSpaceDrawQuad;
                return title.TopLeft.X > icon.TopRight.X && title.TopRight.X < firstItem.TopLeft.X
                       && speedLabel.Width > 0 && speedLabel.TopLeft.X >= tabs.TopLeft.X - 0.01f
                       && speedLabel.TopRight.X <= tabs.TopRight.X + 0.01f && speedLabel.BottomLeft.Y <= tabs.TopLeft.Y + 0.01f;
            }
        }

        public override void TearDownSteps()
        {
            base.TearDownSteps();
            AddStep("restore previous language", () =>
            {
                if (previousLocale == null)
                    return;

                frameworkConfig.SetValue(FrameworkSetting.Locale, previousLocale);
                previousLocale = null;
            });
        }

        [Test]
        public void TestSwitchScreensInstantaneously()
        {
            AddStep("switch between all screens at once", () =>
            {
                foreach (var screen in Enum.GetValues(typeof(EditorScreenMode)).Cast<EditorScreenMode>())
                    Editor.Mode.Value = screen;
            });
        }
    }
}
