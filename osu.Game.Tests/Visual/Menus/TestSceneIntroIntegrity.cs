// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Framework.IO.Stores;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Screens.Menu;
using osu.Game.Seasonal;
using osu.Game.Overlays;
using osu.Game.Overlays.CustomTheme;
using osu.Game.Overlays.Settings;
using osu.Game.Overlays.Settings.Sections.UserInterface;
using osu.Game.Tests.NonVisual;
using osu.Game.Tests.Resources;
using IntroSequence = osu.Game.Configuration.IntroSequence;

namespace osu.Game.Tests.Visual.Menus
{
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneIntroIntegrity : IntroTestScene
    {
        private IntroSequence sequence;
        private readonly List<Guid> seededSetIds = new List<Guid>();

        [BackgroundDependencyLoader]
        private void load() => Dependencies.Cache(new OverlayColourProvider(OverlayColourScheme.Blue));

        [TearDownSteps]
        public void TearDownSteps() => AddStep("remove only test-seeded sets", () =>
        {
            Dependencies.Get<RealmAccess>().Write(r =>
            {
                foreach (Guid id in seededSetIds)
                {
                    var set = r.Find<BeatmapSetInfo>(id);
                    if (set != null)
                        r.Remove(set);
                }
            });
            seededSetIds.Clear();
        });

        [TestCase(IntroSequence.Circles)]
        [TestCase(IntroSequence.Welcome)]
        [TestCase(IntroSequence.Triangles)]
        [TestCase(IntroSequence.Random)]
        public void TestRemovedBeatmapsAreNotImportedAndAllIntrosFinish(IntroSequence introSequence)
        {
            AddStep("select silent startup", () =>
            {
                sequence = introSequence;
                Dependencies.Get<OsuConfigManager>().SetValue(OsuSetting.MenuMusic, true);
            });
            AddAssert("no bundled intro archives", () =>
            {
                using var resources = new DllResourceStore(typeof(osu.Game.Resources.OsuResources).Assembly);
                return !resources.GetAvailableResources().Any(p => p.StartsWith("Tracks/", System.StringComparison.Ordinal)
                                                                  && p.EndsWith(".osz", System.StringComparison.OrdinalIgnoreCase));
            });

            RestartIntro();
            WaitForMenu();
            AddAssert("silent virtual track", () => Intro.Beatmap.Value.Track is TrackVirtual);
            AddAssert("no theme imported", () => Dependencies.Get<RealmAccess>().Run(r => r.All<BeatmapSetInfo>().Count()) == 0);
            AddAssert("placeholder loops", () => MusicController.CurrentTrack.Looping);

            RestartIntro();
            WaitForMenu();
            AddAssert("restart still silent without repairing missing archives", () => Intro.Beatmap.Value.Track is TrackVirtual);
            AddAssert("no theme imported on restart", () => Dependencies.Get<RealmAccess>().Run(r => r.All<BeatmapSetInfo>().Count()) == 0);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TestIncompleteLocalIntroMapsStaySilentWithoutRepair(bool menuMusic)
        {
            AddStep("keep legacy imported maps in isolated library", () =>
            {
                sequence = IntroSequence.Triangles;
                Dependencies.Get<OsuConfigManager>().SetValue(OsuSetting.MenuMusic, menuMusic);
                string[] hashes =
                {
                    "3c8b1fcc9434dbb29e2fb613d3b9eada9d7bb6c125ceb32396c3b53437280c83",
                    "64e00d7022195959bfa3109d09c2e2276c8f12f486b91fcf6175583e973b48f2",
                    "a1556d0801b3a6b175dda32ef546f0ec812b400499f575c44fccbe9c67f9b1e5",
                    IntroChristmas.CHRISTMAS_BEATMAP_SET_HASH,
                };
                Dependencies.Get<RealmAccess>().Write(r =>
                {
                    for (int i = 0; i < hashes.Length; i++)
                    {
                        var set = TestResources.CreateTestBeatmapSetInfo(1);
                        set.Hash = hashes[i];
                        set.Protected = i % 2 == 0;
                        r.Add(set, update: true);
                        seededSetIds.Add(set.ID);
                    }
                });
            });
            RestartIntro();
            WaitForMenu();
            AddAssert("incomplete theme not selected at startup", () => Intro.Beatmap.Value is DummyWorkingBeatmap);
            AddAssert("legacy models neither repaired nor deleted", () => Dependencies.Get<RealmAccess>().Run(r => r.All<BeatmapSetInfo>().Count()) == 4);
        }

        [TestCase(IntroSequence.Circles, 0, false)]
        [TestCase(IntroSequence.Welcome, 1, true)]
        [TestCase(IntroSequence.Triangles, 2, false)]
        [TestCase(IntroSequence.Random, 3, true)]
        public void TestExistingLocalThemeProvidesMusic(IntroSequence introSequence, int index, bool isProtected)
        {
            Guid installedId = Guid.Empty;
            AddStep("install synthetic local map with an old theme identity", () =>
            {
                sequence = introSequence;
                Dependencies.Get<OsuConfigManager>().SetValue(OsuSetting.MenuMusic, true);
                installedId = installLocalTheme(index, isProtected);
            });
            RestartIntro();
            WaitForMenu();
            AddAssert("matching installed theme selected", () => Intro.Beatmap.Value.BeatmapSetInfo.ID == installedId);
            AddAssert("real local audio, not virtual silence", () => Intro.Beatmap.Value.Track is TrackBass);
            AddUntilStep("local track playing", () => MusicController.IsPlaying);
            AddAssert("no extra set imported or protection rewritten", () => Dependencies.Get<RealmAccess>().Run(r =>
                r.All<BeatmapSetInfo>().Count() == 1 && r.Find<BeatmapSetInfo>(installedId)!.Protected == isProtected));

            if (index >= 2)
            {
                AddStep("stop local theme", () => MusicController.CurrentTrack.Stop());
                AddUntilStep("local theme stopped", () => !MusicController.IsPlaying);
                AddStep("seek before old voice ending", () => MusicController.CurrentTrack.Seek(100));
                AddUntilStep("old embedded voice stays muted", () => MusicController.CurrentTrack.AggregateVolume.Value == 0);
                AddStep("seek to music", () => MusicController.CurrentTrack.Seek(7000));
                AddUntilStep("local music can be heard", () => MusicController.CurrentTrack.AggregateVolume.Value > 0);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestMissingOrDeletedLocalThemeStaysSilent(bool deletePending)
        {
            int previousCount = 0;
            Guid installedId = Guid.Empty;
            AddStep("install then remove local theme availability", () =>
            {
                sequence = IntroSequence.Triangles;
                Dependencies.Get<OsuConfigManager>().SetValue(OsuSetting.MenuMusic, true);
                installedId = installLocalTheme(2, true);
                previousCount = Dependencies.Get<RealmAccess>().Run(r => r.All<BeatmapSetInfo>().Count());
                Dependencies.Get<RealmAccess>().Write(r =>
                {
                    var set = r.Find<BeatmapSetInfo>(installedId)!;
                    if (deletePending)
                        set.DeletePending = true;
                    else
                        set.Beatmaps[0].Metadata.AudioFile = "missing.wav";
                });
            });
            RestartIntro();
            WaitForMenu();
            AddAssert("missing local audio uses silence", () => Intro.Beatmap.Value is DummyWorkingBeatmap);
            AddAssert("placeholder still loops", () => MusicController.CurrentTrack.Looping);
            AddAssert("no reimport or repair", () => Dependencies.Get<RealmAccess>().Run(r =>
            {
                var set = r.Find<BeatmapSetInfo>(installedId);
                return r.All<BeatmapSetInfo>().Count() == previousCount && set != null
                       && set.DeletePending == deletePending
                       && (deletePending || set.Beatmaps[0].Metadata.AudioFile == "missing.wav");
            }));
        }

        [Test]
        public void TestThemeMusicChoiceOnlyShownForLocalThemes()
        {
            MainMenuSettings settings = null!;
            Guid installedId = Guid.Empty;
            AddStep("open menu settings without local themes", () => Add(settings = new MainMenuSettings()));
            AddUntilStep("settings loaded", () => settings.IsLoaded);
            AddUntilStep("theme music choice hidden", () => !settings.ChildrenOfType<SettingsItemV2>()
                .Single(i => i.FavouriteId == "OsuSetting.MenuMusic").CanBeShown.Value);
            AddStep("install local theme", () => installedId = installLocalTheme(0, true));
            AddUntilStep("theme music choice shown", () => settings.ChildrenOfType<SettingsItemV2>()
                .Single(i => i.FavouriteId == "OsuSetting.MenuMusic").CanBeShown.Value);
            AddStep("mark local theme deleted", () => Dependencies.Get<RealmAccess>().Write(r =>
                r.Find<BeatmapSetInfo>(installedId)!.DeletePending = true));
            AddUntilStep("theme music choice hidden again", () => !settings.ChildrenOfType<SettingsItemV2>()
                .Single(i => i.FavouriteId == "OsuSetting.MenuMusic").CanBeShown.Value);
            AddStep("close settings", () => Remove(settings, true));
        }

        private Guid installLocalTheme(int index, bool isProtected)
        {
            // Use newly generated maps/audio, never a retired original archive or recording.
            using var source = ThemePreviewGame.CreatePreviewBeatmapArchive(index);
            using var output = new MemoryStream();
            using (var original = new ZipArchive(source, ZipArchiveMode.Read))
            using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
            {
                using (var reader = new StreamReader(original.GetEntry("preview.osu")!.Open()))
                using (var writer = new StreamWriter(archive.CreateEntry("local.osu").Open(), new UTF8Encoding(false)))
                    writer.Write(reader.ReadToEnd().Replace("AudioFilename:", "AudioFilename: local.wav", StringComparison.Ordinal));
                using var audio = archive.CreateEntry("local.wav").Open();
                audio.Write(InterfaceSoundLibraryTest.wave(12));
            }
            output.Position = 0;
            var imported = Dependencies.Get<BeatmapManager>().Import(new ImportTask(output, $"local-{index}.osz")).GetResultSafely()!;
            string[] hashes =
            {
                "3c8b1fcc9434dbb29e2fb613d3b9eada9d7bb6c125ceb32396c3b53437280c83",
                "64e00d7022195959bfa3109d09c2e2276c8f12f486b91fcf6175583e973b48f2",
                "a1556d0801b3a6b175dda32ef546f0ec812b400499f575c44fccbe9c67f9b1e5",
                IntroChristmas.CHRISTMAS_BEATMAP_SET_HASH,
            };
            imported.PerformWrite(set =>
            {
                set.Hash = hashes[index];
                set.Protected = isProtected;
            });
            seededSetIds.Add(imported.ID);
            return imported.ID;
        }

        protected override bool IntroReliesOnTrack => false;
        protected override IntroScreen CreateScreen() => sequence switch
        {
            IntroSequence.Welcome => new IntroWelcome(),
            IntroSequence.Triangles => new IntroTriangles(),
            IntroSequence.Random => new IntroChristmas(),
            _ => new IntroCircles(),
        };
    }
}
