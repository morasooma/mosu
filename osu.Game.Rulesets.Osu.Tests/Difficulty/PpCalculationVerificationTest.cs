using System;
using System.Linq;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using System.Threading;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Osu.Difficulty.Diagnostics;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.Difficulty
{
    [TestFixture]
    public class PpCalculationVerificationTest
    {
        [Test]
        public void TestLongMapThroughRulesetDebugEntryPoint()
        {
            using var stream = typeof(PpCalculationVerificationTest).Assembly.GetManifestResourceStream(
                "osu.Game.Rulesets.Osu.Tests.Resources.Testing.Beatmaps.4766797.osu")!;
            using var reader = new LineBufferedReader(stream);
            var working = new FlatWorkingBeatmap(Decoder.GetDecoder<Beatmap>(reader).Decode(reader));
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(4));
            var verifier = (osu.Game.Rulesets.IPpCalculationVerification)new OsuRuleset();
            string result = verifier.VerifyPpCalculation(working, (_, _) => { }, timeout.Token);
            TestContext.Out.WriteLine(result);
            Assert.That(result.Split('\n').Count(line => line.Contains("все 4506 префиксов совпали")), Is.EqualTo(7));
        }

        [Test]
        public void TestDebugVerifierChecksAllModSetsAndSupportsCancellation()
        {
            var map = new Beatmap<OsuHitObject>
            {
                HitObjects = Enumerable.Range(0, 140).Select(i => (OsuHitObject)new HitCircle
                {
                    StartTime = i * 300,
                    Position = new Vector2(100 + i % 3 * 150, 100 + i % 2 * 150)
                }).ToList()
            };
            var working = new FlatWorkingBeatmap(map);
            var ruleset = new OsuRuleset().RulesetInfo;
            int progress = 0;
            float previous = -1;
            string result = PpCalculationVerification.Run(ruleset, working, (_, value) =>
            {
                Assert.That(value, Is.GreaterThanOrEqualTo(previous));
                previous = value;
                progress++;
            }, CancellationToken.None);
            foreach (string mod in new[] { "NM", "HD", "HR", "DT", "HT", "HD+FL", "HD+HR+DT" })
                Assert.That(result, Does.Contain(mod + ": 140 объектов;"));
            Assert.That(progress, Is.GreaterThanOrEqualTo(84));
            using var duringDt = new CancellationTokenSource();
            bool reachedDt = false;
            Assert.Throws<OperationCanceledException>(() => PpCalculationVerification.Run(ruleset, working, (stage, _) =>
            {
                if (stage == "DT: прогрев старого алгоритма")
                {
                    reachedDt = true;
                    duringDt.Cancel();
                }
            }, duringDt.Token));
            Assert.That(reachedDt, Is.True);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            Assert.Throws<OperationCanceledException>(() => PpCalculationVerification.Run(ruleset, working, (_, _) => { }, cancellation.Token));
        }
    }
}
