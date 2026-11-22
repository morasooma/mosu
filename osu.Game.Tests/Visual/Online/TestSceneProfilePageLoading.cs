using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Framework.Development;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Testing;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Overlays.Profile;
using osu.Game.Overlays.Profile.Sections.Ranks;
using osu.Game.Rulesets.Osu;

namespace osu.Game.Tests.Visual.Online
{
    public partial class TestSceneProfilePageLoading : OsuTestScene
    {
        [Test]
        public void TestItemsConstructedOffUpdateAndPublishedInOrder()
        {
            PageProbe page = null!;
            AddStep("create page", () =>
            {
                ((DummyAPIAccess)API).HandleRequest = request =>
                {
                    if (request is not GetUserScoresRequest scores)
                        return false;
                    scores.TriggerSuccess(new List<SoloScoreInfo> { new SoloScoreInfo { PP = 10 }, new SoloScoreInfo { PP = 20 } });
                    return true;
                };
                Child = new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    CachedDependencies = new (Type, object)[] { (typeof(OverlayColourProvider), new OverlayColourProvider(OverlayColourScheme.Pink)) },
                    Child = page = new PageProbe(new Bindable<UserProfileData?>(new UserProfileData(
                        new APIUser { Id = 2 }, new OsuRuleset().RulesetInfo, null)))
                };
            });
            AddUntilStep("items published", () => page.ChildrenOfType<ItemProbe>().Count() == 2);
            AddAssert("construction never used Update", () => !page.ConstructedOnUpdate);
            AddAssert("order preserved", () => page.ChildrenOfType<ItemProbe>().OrderBy(i => i.Y).Select(i => i.Value), () => Is.EqualTo(new double?[] { 10, 20 }));
        }

        [Test]
        public void TestPageAdmissionIsBoundedPerUpdate()
        {
            PageProbe page = null!;
            int previous = 0;
            int largestAdmission = 0;
            bool observedPartial = false;
            AddStep("create page with many items", () =>
            {
                ((DummyAPIAccess)API).HandleRequest = request =>
                {
                    if (request is not GetUserScoresRequest scores)
                        return false;
                    scores.TriggerSuccess(Enumerable.Range(0, 21).Select(i => new SoloScoreInfo { PP = i }).ToList());
                    return true;
                };
                Child = new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    CachedDependencies = new (Type, object)[] { (typeof(OverlayColourProvider), new OverlayColourProvider(OverlayColourScheme.Pink)) },
                    Child = page = new PageProbe(new Bindable<UserProfileData?>(new UserProfileData(
                        new APIUser { Id = 2 }, new OsuRuleset().RulesetInfo, null)))
                };
                page.OnUpdate += _ =>
                {
                    int count = page.ChildrenOfType<ItemProbe>().Count();
                    largestAdmission = Math.Max(largestAdmission, count - previous);
                    observedPartial |= count > 0 && count < 20;
                    previous = count;
                };
            });
            AddUntilStep("all page items published", () => page.ChildrenOfType<ItemProbe>().Count() == 20);
            AddAssert("partial page observed", () => observedPartial);
            AddAssert("at most four admitted", () => largestAdmission <= 4);
        }

        [Test]
        public void TestCancelledResponseDoesNotConstructOrPublishItems()
        {
            PageProbe page = null!;
            AddStep("create empty page", () => Child = new DependencyProvidingContainer
            {
                RelativeSizeAxes = Axes.Both,
                CachedDependencies = new (Type, object)[] { (typeof(OverlayColourProvider), new OverlayColourProvider(OverlayColourScheme.Pink)) },
                Child = page = new PageProbe(new Bindable<UserProfileData?>())
            });
            AddStep("deliver obsolete response", () =>
            {
                using var cancelled = new CancellationTokenSource();
                cancelled.Cancel();
                page.Offer(new List<SoloScoreInfo> { new SoloScoreInfo { PP = 10 } }, cancelled);
            });
            AddWaitStep("allow callbacks", 5);
            AddAssert("no construction", () => page.ConstructedCount == 0);
            AddAssert("no publication", () => !page.ChildrenOfType<ItemProbe>().Any());
        }

        [Test]
        public void TestUserResetDuringAdmissionDropsOldPage()
        {
            PageProbe page = null!;
            var user = new Bindable<UserProfileData?>();
            bool resetDuringAdmission = false;
            AddStep("create page", () =>
            {
                ((DummyAPIAccess)API).HandleRequest = request =>
                {
                    if (request is not GetUserScoresRequest scores)
                        return false;
                    scores.TriggerSuccess(Enumerable.Range(0, 20).Select(i => new SoloScoreInfo { PP = i }).ToList());
                    return true;
                };
                Child = new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    CachedDependencies = new (Type, object)[] { (typeof(OverlayColourProvider), new OverlayColourProvider(OverlayColourScheme.Pink)) },
                    Child = page = new PageProbe(user)
                };
                page.OnUpdate += _ =>
                {
                    int count = page.ChildrenOfType<ItemProbe>().Count();
                    if (!resetDuringAdmission && count > 0 && count < 20)
                    {
                        resetDuringAdmission = true;
                        user.Value = null;
                    }
                };
                user.Value = new UserProfileData(new APIUser { Id = 2 }, new OsuRuleset().RulesetInfo, null);
            });
            AddUntilStep("reset happened on partial page", () => resetDuringAdmission);
            AddWaitStep("allow remaining callbacks", 5);
            AddAssert("old rows not republished", () => !page.ChildrenOfType<ItemProbe>().Any());
        }

        private partial class PageProbe : PaginatedScoreContainer
        {
            protected override int InitialItemsCount => 20;
            public bool ConstructedOnUpdate;
            public int ConstructedCount;

            public void Offer(List<SoloScoreInfo> items, CancellationTokenSource cancellation) => UpdateItems(items, cancellation);

            public PageProbe(Bindable<UserProfileData?> user) : base(ScoreType.Recent, user, "test") { }

            protected override Drawable CreateDrawableItem(SoloScoreInfo model)
            {
                ConstructedOnUpdate |= ThreadSafety.IsUpdateThread;
                Interlocked.Increment(ref ConstructedCount);
                return new ItemProbe { Value = model.PP, RelativeSizeAxes = Axes.X, Height = 20 };
            }
        }

        private partial class ItemProbe : Box
        {
            public double? Value;
        }
    }
}
