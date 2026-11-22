using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Graphics.Carousel;
using osu.Game.Screens.Select;

namespace osu.Game.Tests.NonVisual.Filtering
{
    [TestFixture]
    public class FilterMatchingCancellationTest
    {
        [Test]
        public async Task CancellationDuringEnumerationStopsObsoleteQuery()
        {
            using var cancellation = new CancellationTokenSource();
            int visited = 0;
            var filter = new BeatmapCarouselFilterMatching(() => new FilterCriteria());
            IEnumerable<CarouselItem> items()
            {
                for (int i = 0; i < 10000; i++)
                {
                    visited++;
                    if (i == 4)
                        cancellation.Cancel();
                    // Hidden rows avoid unrelated metadata setup in this lifetime test.
                    yield return new CarouselItem(new BeatmapInfo { Hidden = true });
                }
            }

            try
            {
                await filter.Run(items(), cancellation.Token);
                Assert.Fail("An obsolete query must not finish enumerating the library");
            }
            catch (System.OperationCanceledException)
            {
                Assert.That(visited, Is.LessThanOrEqualTo(5));
            }
        }
    }
}
