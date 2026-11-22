using System;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Rooms;
using osu.Game.Online.Spectator;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Play.HUD;
using osu.Game.Screens.Play.Leaderboards;
using osu.Game.Skinning;
using osu.Game.Tests.Gameplay;

namespace osu.Game.Tests.Skins
{
    [TestFixture]
    public class GameplayLeaderboardSettingsTest
    {
        [Test]
        public void TestSnapshotAndSidecarRoundtrip()
        {
            using var component = new DrawableGameplayLeaderboard();
            component.MosuLeaderboardStyle.Value = GameplayLeaderboardStyle.Morasooma;
            component.MosuLeaderboardPanels.Value = GameplayLeaderboardPanelStyle.Solid;
            component.MosuLeaderboardMisses.Value = component.MosuLeaderboardPp.Value = component.MosuLeaderboardMods.Value = true;
            component.MosuLeaderboardAccuracy.Value = component.MosuLeaderboardCombo.Value = false;
            var snapshot = component.CreateSerialisedInfo();
            snapshot.Settings = JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string, object>>(JsonConvert.SerializeObject(snapshot.Settings))!;
            using var copy = (DrawableGameplayLeaderboard)snapshot.CreateInstance();
            assertSettings(copy);
            var settings = new MosuSkinSettings();
            SkinComponentGeometrySerializer.Store(settings, GlobalSkinnableContainers.MainHUDComponents, null, new[] { snapshot });
            Assert.That(snapshot.Settings.Keys.Any(k => k.StartsWith("mosu_leaderboard_", StringComparison.Ordinal)), Is.False);
            Assert.That(snapshot.Settings.ContainsKey("collapse_during_gameplay"), Is.True);
            var loaded = JsonConvert.DeserializeObject<MosuSkinSettings>(JsonConvert.SerializeObject(settings))!;
            using var restored = (DrawableGameplayLeaderboard)snapshot.CreateInstance();
            SkinComponentGeometrySerializer.Apply(loaded, GlobalSkinnableContainers.MainHUDComponents, new osu.Game.Rulesets.RulesetInfo { ShortName = "mania" }, new[] { restored });
            Assert.That(restored.MosuLeaderboardStyle.Value, Is.EqualTo(GameplayLeaderboardStyle.Classic));
            SkinComponentGeometrySerializer.Apply(loaded, GlobalSkinnableContainers.MainHUDComponents, null, new[] { restored });
            assertSettings(restored);
            SkinComponentGeometrySerializer.Clear(loaded, GlobalSkinnableContainers.MainHUDComponents, null);
            Assert.That(loaded.ComponentSettings, Is.Empty);
        }

        private static void assertSettings(DrawableGameplayLeaderboard value)
        {
            Assert.That(value.MosuLeaderboardStyle.Value, Is.EqualTo(GameplayLeaderboardStyle.Morasooma));
            Assert.That(value.MosuLeaderboardPanels.Value, Is.EqualTo(GameplayLeaderboardPanelStyle.Solid));
            Assert.That(value.MosuLeaderboardMisses.Value && value.MosuLeaderboardPp.Value && value.MosuLeaderboardMods.Value, Is.True);
            Assert.That(value.MosuLeaderboardAccuracy.Value || value.MosuLeaderboardCombo.Value, Is.False);
        }

        [Test]
        public void TestSavedScoreStatisticsAndMissingData()
        {
            var info = new ScoreInfo { PP = 123.45 };
            var score = new GameplayLeaderboardScore(info, false, GameplayLeaderboardScore.ComboDisplayMode.Highest);
            Assert.That(score.GetMisses(), Is.Null);
            Assert.That(score.GetPerformance(), Is.EqualTo(123.45));
            info.Statistics[HitResult.Great] = 200;
            Assert.That(score.GetMisses(), Is.Zero);
            info.Statistics[HitResult.Miss] = 4;
            Assert.That(score.GetMisses(), Is.EqualTo(4));
            var multiplayer = new MultiplayerScore { User = new APIUser(), Mods = Array.Empty<osu.Game.Online.API.APIMod>() };
            var entry = new GameplayLeaderboardScore(multiplayer, false, GameplayLeaderboardScore.ComboDisplayMode.Highest);
            Assert.That(entry.GetPerformance(), Is.Null);
            Assert.That(entry.GetMisses(), Is.Null);
            multiplayer.Statistics[HitResult.Miss] = 7;
            multiplayer.PP = 321;
            Assert.That(entry.GetMisses(), Is.EqualTo(7));
            Assert.That(entry.GetPerformance(), Is.EqualTo(321));
        }

        [Test]
        public void TestLocalAndSpectatorStatistics()
        {
            var state = TestGameplayState.Create(new OsuRuleset());
            var score = new GameplayLeaderboardScore(state, true, GameplayLeaderboardScore.ComboDisplayMode.Current);
            Assert.That(score.GetMisses(), Is.Zero);
            Assert.That(score.GetPerformance(), Is.Null);
            state.LivePerformance.Value = 42;
            Assert.That(score.GetPerformance(), Is.EqualTo(42));
            using var spectator = new SpectatorScoreProcessor(1);
            var watched = new GameplayLeaderboardScore(new APIUser(), spectator, false, GameplayLeaderboardScore.ComboDisplayMode.Current);
            Assert.That(watched.GetMisses(), Is.Null);
            spectator.ApplyStableFrame(new osu.Game.Online.Legacy.StableBanchoScoreFrame { CountMiss = 9 });
            Assert.That(watched.GetMisses(), Is.EqualTo(9));
            Assert.That(watched.GetPerformance(), Is.Null);
        }
    }
}
