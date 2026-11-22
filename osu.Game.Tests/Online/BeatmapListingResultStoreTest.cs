using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays.BeatmapListing;

namespace osu.Game.Tests.Online
{
    [TestFixture]
    public class BeatmapListingResultStoreTest
    {
        [Test]
        public void ThousandsOfVisitedSetsHaveBoundedResidentMetadataAndRestoreExactly()
        {
            using var store = new BeatmapListingResultStore();
            var handles = Enumerable.Range(1, 1200).Select(id =>
            {
                var set = new APIBeatmapSet
                {
                    OnlineID = id, Title = "title " + id, TitleUnicode = "曲 " + id,
                    AuthorID = id + 10, AuthorString = "mapper " + id,
                    Covers = new BeatmapSetOnlineCovers { Card = "https://example.test/card/" + id },
                    HasFavourited = true, FavouriteCount = id,
                    Beatmaps = new[] { new APIBeatmap { OnlineID = id * 10, DifficultyName = "diff", Length = 123456, HitLength = 98765 } }
                };
                set.Beatmaps[0].BeatmapSet = set;
                return store.Add(set);
            }).ToArray();
            for (int i = 0; i < handles.Length; i++)
            {
                var set = handles[i].Get();
                Assert.That(set.OnlineID, Is.EqualTo(i + 1));
            }
            var cache = (IDictionary)typeof(BeatmapListingResultStore).GetField("cache", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(store)!;
            Assert.That(cache.Count, Is.LessThanOrEqualTo(96));
            var restored = handles[0].Get();
            Assert.Multiple(() =>
            {
                Assert.That(restored.Title, Is.EqualTo("title 1"));
                Assert.That(restored.TitleUnicode, Is.EqualTo("曲 1"));
                Assert.That(restored.Author.Username, Is.EqualTo("mapper 1"));
                Assert.That(restored.Covers.Card, Is.EqualTo("https://example.test/card/1"));
                Assert.That(restored.Beatmaps[0].BeatmapSet, Is.SameAs(restored));
                Assert.That(restored.Beatmaps[0].Length, Is.EqualTo(123456));
                Assert.That(restored.Beatmaps[0].HitLength, Is.EqualTo(98765));
                Assert.That(handles[0].Favourites.Favourited, Is.True);
            });
        }

        [Test]
        public void FileLivesUntilLastEntryOwnerAndIsDeletedOnClose()
        {
            var store = new BeatmapListingResultStore();
            var file = (System.IO.FileStream)typeof(BeatmapListingResultStore).GetField("file", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(store)!;
            string path = file.Name;
            var handle = store.Add(new APIBeatmapSet { OnlineID = 1 });
            handle.AddOwner();
            store.Dispose();
            Assert.That(handle.Get().OnlineID, Is.EqualTo(1));
            handle.ReleaseOwner();
            Assert.That(System.IO.File.Exists(path), Is.False);
        }
    }
}
