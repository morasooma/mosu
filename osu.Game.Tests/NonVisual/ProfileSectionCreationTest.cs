using System;
using System.Linq;
using System.Reflection;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Overlays.Profile.Sections.Ranks;
using NUnit.Framework;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Overlays.Profile;
using osu.Game.Overlays.Profile.Sections;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public partial class ProfileSectionCreationTest
    {
        [TestCase(false)]
        [TestCase(true)]
        public void TestOnlyRequestedSupportedSectionsAreCreated(bool thirdParty)
        {
            var user = new APIUser
            {
                ProfileOrder = new[] { "beatmaps", "unknown", "top_ranks", "medals", "beatmaps", "recent_activity", "historical", "kudosu", "me" }
            };
            var sections = UserProfileOverlay.CreateSections(user, thirdParty);
            try
            {
                string[] expected = thirdParty
                    ? new[] { "beatmaps", "top_ranks", "recent_activity", "historical", "kudosu" }
                    : new[] { "beatmaps", "top_ranks", "medals", "recent_activity", "historical", "kudosu" };
                Assert.That(sections.Select(s => s.Identifier), Is.EqualTo(expected));
            }
            finally
            {
                foreach (var section in sections)
                    section.Dispose();
            }
        }

        [Test]
        public void TestSparseProfileAvoidsUnusedSectionAllocations()
        {
            var user = new APIUser { ProfileOrder = new[] { "recent_activity" } };
            static void dispose(ProfileSection[] sections)
            {
                foreach (var section in sections)
                    section.Dispose();
            }
            static ProfileSection[] original() => new ProfileSection[]
            {
                new RecentSection(), new RanksSection(), new MedalsSection(),
                new HistoricalSection(), new BeatmapsSection(), new KudosuSection()
            };
            // Warm both constructor paths before measuring managed allocations.
            dispose(original());
            dispose(UserProfileOverlay.CreateSections(user, false));
            long start = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 20; i++)
                dispose(original());
            long before = GC.GetAllocatedBytesForCurrentThread() - start;
            start = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 20; i++)
                dispose(UserProfileOverlay.CreateSections(user, false));
            long after = GC.GetAllocatedBytesForCurrentThread() - start;
            TestContext.Progress.WriteLine($"Sparse profile section construction, 20 opens: original={before} bytes, requested-only={after} bytes");
            Assert.That(after, Is.LessThan(before));
        }

        [Test]
        public void TestBestScoreBatchWeightsStayIndependentAcrossUserReset()
        {
            using var page = new WeightedPageProbe();
            var oldFactory = page.Factory(2);
            page.Reset();
            var newFactory = page.Factory(2);
            var nextFactory = page.Factory(1);
            // Interleave obsolete and current page construction, as background
            // cancellation can overlap the next user's load.
            using var oldFirst = oldFactory(new SoloScoreInfo());
            using var newFirst = newFactory(new SoloScoreInfo());
            using var oldSecond = oldFactory(new SoloScoreInfo());
            using var newSecond = newFactory(new SoloScoreInfo());
            using var next = nextFactory(new SoloScoreInfo());
            static double weight(Drawable item) => (double)typeof(DrawableProfileWeightedScore)
                .GetField("weight", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(item)!;
            Assert.That(weight(oldFirst!), Is.EqualTo(1));
            Assert.That(weight(newFirst!), Is.EqualTo(1));
            Assert.That(weight(oldSecond!), Is.EqualTo(0.95));
            Assert.That(weight(newSecond!), Is.EqualTo(0.95));
            Assert.That(weight(next!), Is.EqualTo(Math.Pow(0.95, 2)));
        }

        private partial class WeightedPageProbe : PaginatedScoreContainer
        {
            public WeightedPageProbe() : base(ScoreType.Best, new Bindable<UserProfileData?>(), "test") { }
            public Func<SoloScoreInfo, Drawable?> Factory(int count) => CreateDrawableItemFactory(count);
            public void Reset() => OnItemsReceived(new System.Collections.Generic.List<SoloScoreInfo>());
        }

        [Test]
        public void TestBotAndMissingOrderCreateNoSections()
        {
            Assert.That(UserProfileOverlay.CreateSections(new APIUser { IsBot = true, ProfileOrder = new[] { "beatmaps" } }, false), Is.Empty);
            Assert.That(UserProfileOverlay.CreateSections(new APIUser { ProfileOrder = null }, false), Is.Empty);
            Assert.That(UserProfileOverlay.CreateSections(new APIUser { ProfileOrder = Array.Empty<string>() }, false), Is.Empty);
        }
    }
}
