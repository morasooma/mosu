using System;
using System.Collections.Concurrent;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio.Sample;
using osu.Framework.Audio.Track;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Screens.Menu;
using osu.Game.Seasonal;
using IntroSequence = osu.Game.Configuration.IntroSequence;

namespace osu.Game.Tests.Visual.Menus
{
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneIntroWithoutVoices : IntroTestScene
    {
        private IntroSequence sequence;
        private bool christmas;
        private readonly ConcurrentQueue<string> requestedSamples = new ConcurrentQueue<string>();
        private Func<string, Sample, Sample>? previousTransform;

        protected override bool IntroReliesOnTrack => false;

        protected override IntroScreen CreateScreen() => christmas ? new IntroChristmas() : sequence switch
        {
            IntroSequence.Welcome => new IntroWelcome(),
            IntroSequence.Triangles => new IntroTriangles(),
            _ => new IntroCircles(),
        };

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("watch requested samples", () =>
            {
                requestedSamples.Clear();
                previousTransform = Audio.GlobalSampleTransform;
                Audio.GlobalSampleTransform = (name, sample) =>
                {
                    requestedSamples.Enqueue(name);
                    return previousTransform?.Invoke(name, sample) ?? sample;
                };
            });
        }

        [TearDownSteps]
        public void TearDownSteps() => AddStep("restore sample transform", () => Audio.GlobalSampleTransform = previousTransform);

        [TestCase(IntroSequence.Circles, false, false)]
        [TestCase(IntroSequence.Circles, true, true)]
        [TestCase(IntroSequence.Welcome, false, false)]
        [TestCase(IntroSequence.Welcome, true, true)]
        [TestCase(IntroSequence.Triangles, false, false)]
        [TestCase(IntroSequence.Triangles, true, true)]
        [TestCase(IntroSequence.Random, false, false)]
        [TestCase(IntroSequence.Random, true, true)]
        public void TestNoWelcomeOrExitSamples(IntroSequence introSequence, bool supporter, bool menuMusic)
        {
            AddStep("configure intro with legacy voice setting enabled", () =>
            {
                sequence = introSequence;
                christmas = introSequence == IntroSequence.Random;
                API.LocalUser.Value.IsSupporter = supporter;
                Dependencies.Get<OsuConfigManager>().SetValue(OsuSetting.MenuVoice, true);
                Dependencies.Get<OsuConfigManager>().SetValue(OsuSetting.MenuMusic, menuMusic);
            });

            RestartIntro();
            WaitForMenu();

            AddAssert("no upstream welcome loaded", () => !Array.Exists(requestedSamples.ToArray(), isVoice));
            AddStep("start exit transition", () => Intro.OnResuming(new ScreenTransitionEvent(null, Intro)));
            AddAssert("no upstream exit voice loaded", () => !Array.Exists(requestedSamples.ToArray(), isVoice));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestRemovedThemeUsesSilentVirtualTrack(bool seasonal)
        {
            AddStep("select themed intro", () =>
            {
                sequence = IntroSequence.Triangles;
                christmas = seasonal;
                Dependencies.Get<OsuConfigManager>().SetValue(OsuSetting.MenuMusic, true);
            });

            RestartIntro();
            WaitForMenu();

            AddAssert("silent placeholder, not a decoded theme", () => Intro.Beatmap.Value.Track is TrackVirtual);
            AddAssert("silence loops without advancing playlist", () => MusicController.CurrentTrack.Looping);
            AddStep("seek into silent track", () => MusicController.CurrentTrack.Seek(100));
            AddAssert("seek cannot restore removed music", () => Intro.Beatmap.Value.Track is TrackVirtual);
        }

        private static bool isVoice(string name) => name is "Intro/welcome" or "Intro/seeya" or "Intro/Welcome/welcome" or "Intro/Welcome/seeya";
    }
}
