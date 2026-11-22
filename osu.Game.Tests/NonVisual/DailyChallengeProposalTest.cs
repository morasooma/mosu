using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using osu.Framework.Configuration;
using osu.Framework.Localisation;
using osu.Framework.Testing;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Online.Rooms;
using osu.Game.Rulesets.Osu.Mods;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class DailyChallengeProposalTest
    {
        [Test]
        public void TestSettingsSurviveProposalJsonAndPlaylistConversion()
        {
            var proposal = JsonConvert.DeserializeObject<DailyChallengeProposal>("""
                {"id":12,"owner_id":42,"beatmap_id":100,"ruleset_id":0,"revision":3,"status":"approved",
                 "required_mods":[{"acronym":"DT","settings":{"speed_change":1.2}},{"acronym":"HD"}],
                 "allowed_mods":[],"freestyle":false}
                """)!;
            var item = proposal.ToPlaylistItem();
            var dt = (OsuModDoubleTime)item.RequiredMods[0].ToMod(new osu.Game.Rulesets.Osu.OsuRuleset());
            Assert.Multiple(() =>
            {
                Assert.That(item.OwnerID, Is.EqualTo(42));
                Assert.That(item.ID, Is.EqualTo(12));
                Assert.That(item.Beatmap.OnlineID, Is.EqualTo(100));
                Assert.That(dt.SpeedChange.Value, Is.EqualTo(1.2));
                Assert.That(new APIMod(dt).Settings["speed_change"], Is.EqualTo(1.2));
                Assert.That(item.RequiredMods.Select(m => m.Acronym), Is.EqualTo(new[] { "DT", "HD" }));
            });
        }

        [Test]
        public void TestRussianStringsResolveThroughRealClientResolver()
        {
            using var storage = new TemporaryNativeStorage("daily-proposal-localisation");
            using var config = new FrameworkConfigManager(storage);
            using var manager = new LocalisationManager(config);
            using var store = new ResourceManagerLocalisationStore("ru");
            manager.AddLanguage("ru", store);
            config.SetValue(FrameworkSetting.Locale, "ru");
            foreach (var property in typeof(DailyChallengeProposalStrings).GetProperties())
            {
                var text = (LocalisableString)property.GetValue(null)!;
                var translated = manager.GetLocalisedBindableString(text).Value;
                Assert.That(translated, Is.Not.Empty, property.Name);
                Assert.That(translated.Any(c => c is >= 'А' and <= 'я'), Is.True, property.Name + ": " + translated);
            }
            Assert.That(manager.GetLocalisedBindableString(DailyChallengeProposalStrings.Propose).Value, Is.EqualTo("Предложить карту"));
            Assert.That(manager.GetLocalisedBindableString(DailyChallengeProposalStrings.Quota(2, 3)).Value,
                Is.EqualTo("Сегодня отправлено: 2/3 · сброс в 00:00 UTC"));
            Assert.That(manager.GetLocalisedBindableString(DailyChallengeProposalStrings.Quota(1, 1)).Value,
                Is.EqualTo("Сегодня отправлено: 1/1 · сброс в 00:00 UTC"));
            Assert.That(manager.GetLocalisedBindableString(DailyChallengeProposalStrings.ProfileProposals(12)).Value,
                Is.EqualTo("Предложено карт дня: 12"));
            Assert.That(manager.GetLocalisedBindableString(DailyChallengeProposalStrings.ProfileMaps(4)).Value,
                Is.EqualTo("Стали картой дня: 4"));
        }
    }
}
