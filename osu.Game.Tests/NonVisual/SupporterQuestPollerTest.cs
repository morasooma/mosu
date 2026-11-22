using System;
using System.Collections.Generic;
using NUnit.Framework;
using osu.Framework.Threading;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Screens;
using osu.Game.Screens.Play;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public partial class SupporterQuestPollerTest
    {
        private TestApi api = null!;
        private SupporterQuestPoller poller = null!;
        private int responses;

        [SetUp]
        public void SetUp()
        {
            responses = 0;
            api = new TestApi();
            poller = new SupporterQuestPoller(api, _ => responses++);
        }

        [TearDown]
        public void TearDown()
        {
            poller.Dispose();
            api.Scheduler.Update();
            api.Dispose();
        }

        [Test]
        public void TestNoPollingThroughoutGameplay()
        {
            poller.Suspended = true;
            for (int i = 0; i <= 120; i++)
                poller.Poll(i * 1000);
            Assert.That(api.Requests, Is.Empty);

            poller.Suspended = false;
            poller.Poll(120000);
            Assert.That(api.Requests, Has.Count.EqualTo(1));
        }

        [Test]
        public void TestScreenChangesAndTimerShareIntervalAndPendingRequest()
        {
            poller.Poll(0);
            poller.Poll(15000);
            poller.Poll(30000);
            Assert.That(api.Requests, Has.Count.EqualTo(1), "only one in-flight request");

            complete(api.Requests[0]);
            poller.Poll(30001);
            Assert.That(api.Requests, Has.Count.EqualTo(2));
            complete(api.Requests[1]);

            for (int i = 1; i < 15; i++)
                poller.Poll(30001 + i * 1000);
            Assert.That(api.Requests, Has.Count.EqualTo(2), "screen changes cannot bypass the interval");

            poller.Poll(45001);
            Assert.That(api.Requests, Has.Count.EqualTo(3));
        }

        [Test]
        public void TestCancelledRequestCannotClearReplacement()
        {
            poller.Poll(0);
            var cancelled = api.Requests[0];
            poller.Suspended = true;
            Assert.That(cancelled.CompletionState, Is.EqualTo(APIRequestCompletionState.Failed));

            poller.Suspended = false;
            poller.Poll(1);
            Assert.That(api.Requests, Has.Count.EqualTo(2), "cancelled refresh retries even after a short play");

            // Deliver the old cancellation after the new request has been created.
            api.Scheduler.Update();
            poller.Poll(20000);
            Assert.That(api.Requests, Has.Count.EqualTo(2));
            complete(api.Requests[1]);
            Assert.That(responses, Is.EqualTo(1));
        }

        [Test]
        public void TestQueuedSuccessIsIgnoredAfterEnteringGameplay()
        {
            poller.Poll(0);
            api.Requests[0].TriggerSuccess(new List<APISupporterQuest>());
            poller.Suspended = true;
            api.Scheduler.Update();
            Assert.That(responses, Is.Zero);

            poller.Suspended = false;
            poller.Poll(1);
            complete(api.Requests[1]);
            Assert.That(responses, Is.EqualTo(1));
        }

        [Test]
        public void TestFailureRetriesWithoutRequestStorm()
        {
            poller.Poll(0);
            api.Requests[0].Fail(new InvalidOperationException("test failure"));
            api.Scheduler.Update();
            poller.Poll(1);
            Assert.That(api.Requests, Has.Count.EqualTo(1));
            poller.Poll(15000);
            Assert.That(api.Requests, Has.Count.EqualTo(2));
        }

        [Test]
        public void TestOfflineDoesNotPollOrPublishQueuedSuccess()
        {
            api.Logout();
            poller.Poll(0);
            Assert.That(api.Requests, Is.Empty);

            api.SkipSecondFactor();
            api.Login("test", "test");
            poller.Poll(0);
            Assert.That(api.Requests, Has.Count.EqualTo(1));
            api.Requests[0].TriggerSuccess(new List<APISupporterQuest>());
            api.Logout();
            api.Scheduler.Update();
            Assert.That(responses, Is.Zero);
        }

        [Test]
        public void TestDisposalCancelsAndSuppressesCallbacks()
        {
            poller.Poll(0);
            poller.Dispose();
            Assert.That(api.Requests[0].CompletionState, Is.EqualTo(APIRequestCompletionState.Failed));
            api.Scheduler.Update();
            poller.Poll(30000);
            Assert.That(api.Requests, Has.Count.EqualTo(1));
            Assert.That(responses, Is.Zero);
        }

        [Test]
        public void TestDisposalSuppressesAlreadyQueuedSuccess()
        {
            poller.Poll(0);
            api.Requests[0].TriggerSuccess(new List<APISupporterQuest>());
            poller.Dispose();
            api.Scheduler.Update();
            Assert.That(responses, Is.Zero);
        }

        [Test]
        public void TestLoadingAndPlayerScreenAreSuspendedEvenBeforePlayingStateChanges()
        {
            using var player = new SoloPlayer();
            using var loader = new PlayerLoader(() => player);
            using var menu = new TestMenu();
            Assert.That(player.PlayingState.Value, Is.EqualTo(LocalUserPlayingState.NotPlaying));
            Assert.That(OsuGame.ShouldPauseSupporterQuestPolling(loader), Is.True);
            Assert.That(OsuGame.ShouldPauseSupporterQuestPolling(player), Is.True);
            Assert.That(OsuGame.ShouldPauseSupporterQuestPolling(menu), Is.False);
        }

        private void complete(GetSupporterQuestsRequest request)
        {
            request.TriggerSuccess(new List<APISupporterQuest>());
            api.Scheduler.Update();
        }

        private partial class TestApi : DummyAPIAccess
        {
            public new Scheduler Scheduler => base.Scheduler;

            public readonly List<GetSupporterQuestsRequest> Requests = new List<GetSupporterQuestsRequest>();

            public override void Queue(APIRequest request)
            {
                request.AttachAPI(this);
                Requests.Add((GetSupporterQuestsRequest)request);
            }
        }

        private partial class TestMenu : OsuScreen
        {
        }
    }
}
