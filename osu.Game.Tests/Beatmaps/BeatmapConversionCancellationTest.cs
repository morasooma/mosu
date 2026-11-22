using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Tests.Beatmaps
{
    [TestFixture]
    public class BeatmapConversionCancellationTest
    {
        [Test]
        public void TestCancelledBeforeConversionDoesNotConstructObjects()
        {
            using var cancellation = new CancellationTokenSource();
            var converter = new ProbeConverter(createBeatmap(), _ => { });
            cancellation.Cancel();
            Assert.Throws<OperationCanceledException>(() => converter.Convert(cancellation.Token));
            Assert.That(converter.ConvertedObjects, Is.Zero);
        }

        [Test]
        public void TestCancellationStopsObjectConversionAndAllowsReuse()
        {
            using var cancellation = new CancellationTokenSource();
            var converter = new ProbeConverter(createBeatmap(), count =>
            {
                if (count == 16)
                    cancellation.Cancel();
            });
            Assert.Throws<OperationCanceledException>(() => converter.Convert(cancellation.Token));
            Assert.That(converter.ConvertedObjects, Is.EqualTo(16));
            Assert.That(converter.Convert().HitObjects.Count, Is.EqualTo(1000));
        }

        private static IBeatmap createBeatmap() => new Beatmap
        {
            HitObjects = Enumerable.Range(0, 1000).Select(i => new HitObject { StartTime = i }).ToList(),
        };

        private class ProbeConverter : BeatmapConverter<HitCircle>
        {
            private readonly Action<int> onConverted;
            public int ConvertedObjects { get; private set; }

            public ProbeConverter(IBeatmap beatmap, Action<int> onConverted)
                : base(beatmap, new OsuRuleset()) => this.onConverted = onConverted;

            public override bool CanConvert() => true;

            protected override IEnumerable<HitCircle> ConvertHitObject(HitObject original, IBeatmap beatmap, CancellationToken cancellationToken)
            {
                onConverted(++ConvertedObjects);
                yield return new HitCircle { StartTime = original.StartTime };
            }
        }
    }
}
