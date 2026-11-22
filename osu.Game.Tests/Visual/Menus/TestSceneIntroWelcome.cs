// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Audio.Track;
using osu.Game.Screens.Menu;

namespace osu.Game.Tests.Visual.Menus
{
    [TestFixture]
    public partial class TestSceneIntroWelcome : IntroTestScene
    {
        protected override bool IntroReliesOnTrack => false;
        protected override IntroScreen CreateScreen() => new IntroWelcome();

        public override void TestPlayIntro()
        {
            base.TestPlayIntro();

            AddUntilStep("wait for load", () => MusicController.TrackLoaded);
            AddAssert("silent placeholder instead of removed theme", () => Intro.Beatmap.Value.Track is TrackVirtual);
            AddAssert("check if menu music loops", () => MusicController.CurrentTrack.Looping);
        }
    }
}
