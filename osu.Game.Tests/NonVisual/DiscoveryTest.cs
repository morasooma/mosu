using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using osu.Framework.Configuration;
using osu.Framework.Localisation;
using osu.Framework.Testing;
using osu.Game.Localisation;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays.Discovery;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class DiscoveryTest
    {
        [Test]
        public void TestHybridKeepsExactNeighbourDifficultyAndFilters()
        {
            var seed = set(1, 4);
            var candidate = set(2, 4);
            var sibling = new APIBeatmap { OnlineID = 21, OnlineBeatmapSetID = 2, RulesetID = 0, StarRating = 4.4, BPM = 174, Length = 180000 };
            candidate.Beatmaps = new[] { candidate.Beatmaps[0], sibling };
            var profile = new DiscoveryRecommendations(new[] { new SoloScoreInfo { Beatmap = seed.Beatmaps[0] } }, 0);
            var weights = new System.Collections.Generic.Dictionary<int, double> { [21] = .9 };
            var ranked = profile.Rank(new[] { candidate }, Array.Empty<int>().ToHashSet(), workMode: DiscoveryWorkMode.Gameplay, gameplayWeights: weights);
            Assert.That(ranked.Single().Map.OnlineID, Is.EqualTo(21));
            Assert.That(ranked.Single().Reason, Is.EqualTo(DiscoveryReason.Gameplay));
            Assert.That(profile.Rank(new[] { candidate }, Array.Empty<int>().ToHashSet(), new DiscoveryFilters(3, 5, osu.Game.Overlays.BeatmapListing.SearchCategory.Ranked), gameplayWeights: weights), Is.Empty);
            var history = new DiscoveryHistory();
            history.Like(candidate, 21);
            Assert.That(DiscoverySimilarity.Seeds(profile.Seeds, history.Saved).First().OnlineID, Is.EqualTo(21));
        }

        [Test]
        public void TestTasteUsesMusicNeighboursNotBpmStarsMapperOrGameplay()
        {
            var seed = set(1, 4);
            var a = set(2, 3.5); a.Artist = "New music"; a.Beatmaps[0].BPM = 80;
            var b = set(3, 4); b.Artist = a.Artist; b.AuthorString = "Different mapper"; b.Beatmaps[0].BPM = 300;
            var profile = new DiscoveryRecommendations(new[] { new SoloScoreInfo { Beatmap = seed.Beatmaps[0] } }, 0);
            profile.SetTaste(new osu.Game.Online.API.Requests.DiscoveryTasteProfile
            {
                RelatedArtists = new[] { new osu.Game.Online.API.Requests.DiscoveryTasteSignal { Name = "New music", Weight = .5 } },
            });
            var result = profile.Rank(new[] { a, b }, Array.Empty<int>().ToHashSet(), gameplayWeights: new System.Collections.Generic.Dictionary<int, double> { [30] = .99 });
            Assert.That(result[0].Weight, Is.EqualTo(result[1].Weight));
            Assert.That(result.All(c => c.Reason == DiscoveryReason.Listeners), Is.True);
            Assert.That(profile.Queries(0)[0], Does.Contain("new music"));
        }

        [Test]
        public void TestTasteWireGenreTagsAndPlayWeights()
        {
            var response = Newtonsoft.Json.JsonConvert.DeserializeObject<osu.Game.Online.API.Requests.DiscoveryTasteProfile>(
                "{\"artists\":[{\"name\":\"Common artist\",\"weight\":10,\"plays\":80,\"songs\":12}],\"related_artists\":[{\"name\":\"New music\",\"weight\":0.4}],\"genres\":[{\"name\":\"electronic\",\"weight\":4}],\"tags\":[{\"name\":\"breakcore\",\"weight\":3}],\"candidates\":[{\"set_id\":99,\"weight\":0.2,\"reason\":\"listeners\"}],\"played_sets\":[1,2]}")!;
            var ranking = new DiscoveryTasteRanking();
            ranking.SetProfile(response);
            Assert.That(response.Artists[0].Plays, Is.EqualTo(80));
            Assert.That(ranking.Genre(0), Is.EqualTo(osu.Game.Overlays.BeatmapListing.SearchGenre.Electronic));
            Assert.That(ranking.Queries(0, "star>=3").Any(q => q.StartsWith("new music", StringComparison.Ordinal)), Is.True);
            var music = set(3, 4); music.Artist = "Unknown"; music.Tags = "breakcore jumps";
            Assert.That(ranking.Rank(music).Reason, Is.EqualTo(DiscoveryReason.Music));
            music.Tags = "breakcoreish streams";
            Assert.That(ranking.Rank(music).Weight, Is.Zero);
            music.Genre = new osu.Game.Beatmaps.BeatmapSetOnlineGenre { Name = "Electronic", Id = 10 };
            Assert.That(ranking.Rank(music).Reason, Is.EqualTo(DiscoveryReason.Music));
        }

        [Test]
        public void TestPendingFeedbackSurvivesReloadWithSameId()
        {
            using var storage = new TemporaryNativeStorage("discovery-feedback-" + Guid.NewGuid());
            var history = new DiscoveryHistory();
            var feedback = new osu.Game.Online.API.Requests.DiscoveryFeedbackEvent { BeatmapId = 123, BeatmapSetId = 12, Action = "skip", ImpressionId = Guid.NewGuid() };
            history.PendingFeedback.Add(feedback);
            Assert.That(history.Save(storage, "outbox.json"), Is.True);
            Assert.That(DiscoveryHistory.Load(storage, "outbox.json").PendingFeedback.Single().EventId, Is.EqualTo(feedback.EventId));
        }

        [Test]
        public void TestShownMapsAndSongsSurviveReloadWithoutSkip()
        {
            using var storage = new TemporaryNativeStorage("discovery-seen-" + Guid.NewGuid());
            var shown = set(20, 4);
            var history = new DiscoveryHistory();
            history.RememberShown(shown);
            Assert.That(history.Save(storage, "history.json"), Is.True);
            var loaded = DiscoveryHistory.Load(storage, "history.json");
            Assert.That(loaded.HasSeen(shown), Is.True);
            var remap = set(21, 4); remap.Title = " SONG-20 ";
            Assert.That(loaded.HasSeen(remap), Is.True);
            Assert.That(loaded.HasSeen(set(22, 4)), Is.False);
            Assert.That(loaded.Recent.Single().Artist, Is.EqualTo(shown.Artist));
            Assert.That(loaded.Skipped, Is.Empty);
        }

        [Test]
        public void TestDifferentSetIdsForSameSongAreNotRecommendedTwice()
        {
            var seed = set(1, 4);
            var sameAsSeed = set(2, 4); sameAsSeed.Title = seed.Title;
            var first = set(3, 4);
            var remap = set(4, 4); remap.Title = first.Title;
            var profile = new DiscoveryRecommendations(new[] { new SoloScoreInfo { Beatmap = seed.Beatmaps[0] } }, 0);
            var ranked = profile.Rank(new[] { sameAsSeed, first, remap }, Array.Empty<int>().ToHashSet());
            Assert.That(ranked.Select(c => c.Map.OnlineBeatmapSetID), Does.Not.Contain(2));
            Assert.That(DiscoveryRecommendations.Diversify(ranked).Count, Is.EqualTo(1));
        }

        [Test]
        public void TestGameplayModeDoesNotUseArtistMapperBoosts()
        {
            var seed = set(1, 4);
            var profile = new DiscoveryRecommendations(new[] { new SoloScoreInfo { Beatmap = seed.Beatmaps[0] } }, 0);
            var familiar = set(2, 4);
            var other = set(3, 4); other.Artist = "Other"; other.AuthorString = "Someone";
            profile.SetTaste(new osu.Game.Online.API.Requests.DiscoveryTasteProfile
            {
                Artists = new[] { new osu.Game.Online.API.Requests.DiscoveryTasteArtist { Name = "Artist", Weight = 10, Plays = 20, Songs = 4 } },
            });
            Assert.That(profile.Rank(new[] { other, familiar }, Array.Empty<int>().ToHashSet())[0].Map.OnlineBeatmapSetID, Is.EqualTo(2));
            Assert.That(profile.Rank(new[] { other, familiar }, Array.Empty<int>().ToHashSet(), workMode: DiscoveryWorkMode.Gameplay)[0].Map.OnlineBeatmapSetID, Is.EqualTo(3));
            Assert.That(profile.Queries(0, workMode: DiscoveryWorkMode.Gameplay), Has.Length.EqualTo(1));
        }

        [Test]
        public void TestMetadataRankingBoundsAndDeduplication()
        {
            var seed = set(1, 4);
            var profile = new DiscoveryRecommendations(new[] { new SoloScoreInfo { Beatmap = seed.Beatmaps[0] } }, 0);
            var selected = set(2, 4.1);
            var wrongMode = set(3, 4); wrongMode.Beatmaps[0].RulesetID = 3;
            var explicitSet = set(4, 4); explicitSet.HasExplicitContent = true;
            var excluded = set(5, 4);
            var result = profile.Rank(new[] { seed, selected, selected, wrongMode, explicitSet, excluded, set(6, 7) }, new[] { 5 }.ToHashSet());
            Assert.That(result.Select(c => c.Map.OnlineBeatmapSetID), Is.EqualTo(new[] { 2 }));
            Assert.That(profile.Queries(0).Length, Is.LessThanOrEqualTo(3));
            Assert.That(profile.TargetStars, Is.EqualTo(4));
        }

        [Test]
        public void TestArtistAndMapperQuotasAreHardLimits()
        {
            var sets = Enumerable.Range(1, 30).Select(id =>
            {
                var mapset = set(id, 4);
                mapset.Artist = id <= 20 ? "Dominant artist" : "Artist " + id;
                mapset.AuthorString = id <= 15 ? "Dominant mapper" : "Mapper " + id;
                return new DiscoveryCandidate(mapset.Beatmaps[0], DiscoveryReason.Similar, 100 - id);
            }).ToArray();
            var diverse = DiscoveryRecommendations.Diversify(sets);
            Assert.That(diverse.Count, Is.GreaterThan(5));
            Assert.That(diverse.GroupBy(c => c.Map.BeatmapSet!.Artist).Max(g => g.Count()), Is.LessThanOrEqualTo(2));
            Assert.That(diverse.GroupBy(c => c.Map.BeatmapSet!.AuthorString).Max(g => g.Count()), Is.LessThanOrEqualTo(3));
            Assert.That(diverse.Zip(diverse.Skip(1)).All(p => p.First.Map.BeatmapSet!.Artist != p.Second.Map.BeatmapSet!.Artist), Is.True);
            Assert.That(DiscoveryRecommendations.Diversify(sets.Take(15)).Count, Is.EqualTo(1));
        }

        [Test]
        public void TestCustomStarsBpmAndLengthFilterBothQueriesAndRanking()
        {
            var seed = set(1, 4);
            var profile = new DiscoveryRecommendations(new[] { new SoloScoreInfo { Beatmap = seed.Beatmaps[0] } }, 0);
            var candidate = set(2, 7);
            var filters = new DiscoveryFilters(6, 8, MinimumBpm: 170, MaximumBpm: 180, MaximumLength: 200000);
            Assert.That(profile.Rank(new[] { candidate }, Array.Empty<int>().ToHashSet()).Count, Is.Zero);
            Assert.That(profile.Rank(new[] { candidate }, Array.Empty<int>().ToHashSet(), filters).Count, Is.EqualTo(1));
            Assert.That(profile.Queries(0, filters).All(q => q.Contains("star>=6") && q.Contains("star<=8") && q.Contains("length<=200")), Is.True);
            Assert.That(profile.Rank(new[] { candidate }, Array.Empty<int>().ToHashSet(), filters with { MaximumBpm = 160 }).Count, Is.Zero);
            Assert.That(profile.Rank(new[] { candidate }, Array.Empty<int>().ToHashSet(), filters with { MaximumLength = 120000 }).Count, Is.Zero);
        }

        [Test]
        public void TestLikesAffectRankingAndQueriesWithoutMovingSkillTarget()
        {
            var seed = set(1, 4);
            var liked = set(2, 4); liked.Artist = "New artist"; liked.AuthorString = "New mapper";
            var similar = set(3, 4); similar.Artist = liked.Artist; similar.AuthorString = liked.AuthorString;
            var unrelated = set(4, 4); unrelated.Artist = "Other artist"; unrelated.AuthorString = "Other mapper";
            var profile = new DiscoveryRecommendations(new[] { new SoloScoreInfo { Beatmap = seed.Beatmaps[0] } }, 0);
            var before = profile.Rank(new[] { similar, unrelated }, Array.Empty<int>().ToHashSet()).First(c => c.Map.OnlineBeatmapSetID == 3).Weight;
            profile.UpdateFeedback(new[] { liked, liked });
            var ranked = profile.Rank(new[] { unrelated, similar }, Array.Empty<int>().ToHashSet());
            Assert.That(ranked[0].Map.OnlineBeatmapSetID, Is.EqualTo(3));
            Assert.That(ranked[0].Weight, Is.GreaterThan(before));
            Assert.That(profile.Queries(0).Any(q => q.Contains("new artist", StringComparison.Ordinal)), Is.True);
            Assert.That(profile.Queries(0).Length, Is.LessThanOrEqualTo(3));
            Assert.That(profile.TargetStars, Is.EqualTo(4));
            profile.UpdateFeedback(Array.Empty<APIBeatmapSet>());
            Assert.That(profile.Rank(new[] { similar }, Array.Empty<int>().ToHashSet())[0].Weight, Is.EqualTo(before));
        }

        [Test]
        public void TestHistoryRoundTripAndMalformedNestedData()
        {
            using var storage = new TemporaryNativeStorage("discovery-" + Guid.NewGuid());
            var history = new DiscoveryHistory { Saved = new[] { set(2, 4.5) }.ToList(), Skipped = Enumerable.Range(1, 600).ToList() };
            string file = DiscoveryHistory.Filename("https://mosu.test/api", 42, 0);
            history.Like(set(2, 4.5));
            history.Like(set(2, 4.5));
            Assert.That(history.Saved.Count, Is.EqualTo(1));
            Assert.That(history.Skipped, Does.Not.Contain(2));
            Assert.That(history.Save(storage, file), Is.True);
            var restored = DiscoveryHistory.Load(storage, file);
            Assert.That(restored.Skipped.Count, Is.EqualTo(500));
            var map = restored.Saved.Single().Beatmaps.Single();
            Assert.That(map.RulesetID, Is.EqualTo(0));
            Assert.That(map.StarRating, Is.EqualTo(4.5));
            Assert.That(map.Length, Is.EqualTo(180000));
            Assert.That(map.BeatmapSet, Is.SameAs(restored.Saved[0]));
            Assert.That(DiscoveryHistory.Filename("https://other.test/api", 42, 0), Is.Not.EqualTo(file));
            Assert.That(DiscoveryHistory.Filename("https://mosu.test/api", 43, 0), Is.Not.EqualTo(file));
            using (var writer = new StreamWriter(storage.GetStream(file, FileAccess.Write, FileMode.Create)))
                writer.Write("{\"Saved\":[{\"id\":2,\"beatmaps\":null},{\"id\":3,\"beatmaps\":[null]}]}");
            Assert.DoesNotThrow(() => DiscoveryHistory.Load(storage, file));
        }

        [TestCase("en")]
        [TestCase("ru")]
        [TestCase("es")]
        [TestCase("zh")]
        [TestCase("ko")]
        public void TestRuntimeLocalisation(string locale)
        {
            System.Reflection.Assembly.Load("osu.Game.Resources");
            using var config = new TestConfig();
            using var manager = new LocalisationManager(config);
            using var store = new ResourceManagerLocalisationStore(locale);
            manager.AddLanguage(locale, store); config.SetValue(FrameworkSetting.Locale, locale);
            Assert.That(manager.GetLocalisedString(DiscoveryStrings.Title), Is.Not.Empty);
            if (locale != "en") Assert.That(manager.GetLocalisedString(DiscoveryStrings.Title), Is.Not.EqualTo("Discover"));
            foreach (var property in typeof(DiscoveryStrings).GetProperties().Where(p => p.PropertyType == typeof(LocalisableString)))
            {
                var text = (LocalisableString)property.GetValue(null)!;
                Assert.That(manager.GetLocalisedString(text), Is.Not.Empty, property.Name);
                if (locale != "en") Assert.That(manager.GetLocalisedString(text), Is.Not.EqualTo(new LocalisationManager(new TestConfig()).GetLocalisedString(text)), property.Name);
            }
            Assert.That(manager.GetLocalisedString(DiscoveryStrings.Progress(12, 3)), Does.Contain("12").And.Contain("3"));
            Assert.That(manager.GetLocalisedString(DiscoveryStrings.AudioProcessing("CPU")), Does.Contain("CPU").And.Not.Contain("{0}"));
            Assert.That(manager.GetLocalisedString(DiscoveryStrings.AudioReady("CPU")), Does.Contain("CPU").And.Not.Contain("{0}"));
        }

        private static APIBeatmapSet set(int id, double stars)
        {
            var result = new APIBeatmapSet { OnlineID = id, Artist = "Artist", Title = "Song " + id, AuthorString = "Mapper" };
            result.Beatmaps = new[] { new APIBeatmap { OnlineID = id * 10, OnlineBeatmapSetID = id, BeatmapSet = result, RulesetID = 0, StarRating = stars, BPM = 174, Length = 180000 } };
            return result;
        }

        private sealed class TestConfig : FrameworkConfigManager
        {
            protected override string Filename => null!;
            public TestConfig() : base(null!) { }
        }
    }
}
