using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Threading;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Metadata;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    [NonParallelizable]
    public partial class ClientBeatmapDownloadHandlerTest
    {
        private bool thirdParty;
        private bool stable;

        [SetUp]
        public void SetUp()
        {
            thirdParty = MosuServerEnvironment.IsThirdPartyServer;
            stable = MosuServerEnvironment.UsesStableProtocol;
            MosuServerEnvironment.IsThirdPartyServer = false;
            MosuServerEnvironment.UsesStableProtocol = false;
        }

        [TearDown]
        public void TearDown()
        {
            MosuServerEnvironment.IsThirdPartyServer = thirdParty;
            MosuServerEnvironment.UsesStableProtocol = stable;
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task AcknowledgesActualDownloadAcceptance(bool accepted)
        {
            using var api = new TestApi();
            int downloads = 0;
            var handler = new ClientBeatmapDownloadHandler(api, action => action(), _ => { downloads++; return accepted; });
            var task = handler.TryDownloadAsync(100, () => true);
            Assert.That(task.IsCompleted, Is.False);
            api.Requests[0].TriggerSuccess(new APIBeatmapSet { OnlineID = 100 });
            api.Scheduler.Update();
            Assert.That(await task, Is.EqualTo(accepted));
            Assert.That(downloads, Is.EqualTo(1));
        }

        [Test]
        public async Task LookupFailureReturnsFalse()
        {
            using var api = new TestApi();
            var handler = new ClientBeatmapDownloadHandler(api, action => action(), _ => throw new AssertionException("must not download"));
            var task = handler.TryDownloadAsync(100, () => true);
            api.Requests[0].Fail(new InvalidOperationException("missing map"));
            api.Scheduler.Update();
            Assert.That(await task, Is.False);
        }

        [Test]
        public async Task TimeoutSuppressesLateScheduledDownload()
        {
            using var api = new TestApi();
            var scheduled = new Queue<Action>();
            var handler = new ClientBeatmapDownloadHandler(api, scheduled.Enqueue,
                _ => throw new AssertionException("late download"), TimeSpan.FromMilliseconds(50));
            var task = handler.TryDownloadAsync(100, () => true);
            scheduled.Dequeue()();
            api.Requests[0].TriggerSuccess(new APIBeatmapSet { OnlineID = 100 });
            api.Scheduler.Update();
            Assert.That(await task, Is.False);
            while (scheduled.TryDequeue(out var action)) action();
        }

        [TestCase("logout")]
        [TestCase("account")]
        [TestCase("api")]
        [TestCase("metadata")]
        [TestCase("route")]
        [TestCase("profile")]
        [TestCase("stable")]
        [TestCase("connection")]
        public async Task ContextChangeSuppressesQueuedDownload(string change)
        {
            using var api = new TestApi();
            var scheduled = new Queue<Action>();
            bool connectionTrusted = true;
            var handler = new ClientBeatmapDownloadHandler(api, scheduled.Enqueue, _ => throw new AssertionException("stale command"));
            var task = handler.TryDownloadAsync(100, () => connectionTrusted);
            scheduled.Dequeue()();
            api.Requests[0].TriggerSuccess(new APIBeatmapSet { OnlineID = 100 });
            api.Scheduler.Update();
            switch (change)
            {
                case "logout": api.Logout(); break;
                case "account": api.LocalUser.Value = new APIUser { Id = 999, Username = "other" }; break;
                case "api": api.Endpoints.APIUrl = "https://attacker.example"; break;
                case "metadata": api.Endpoints.MetadataUrl = "https://attacker.example/metadata"; break;
                case "route":
                    api.Endpoints.APIUrl = MosuServerEnvironment.SecondaryProxyServerUrl;
                    api.Endpoints.MetadataUrl = MosuServerEnvironment.SecondaryProxyServerUrl + "/metadata";
                    break;
                case "profile": MosuServerEnvironment.IsThirdPartyServer = true; break;
                case "stable": MosuServerEnvironment.UsesStableProtocol = true; break;
                case "connection": connectionTrusted = false; break;
            }
            while (scheduled.TryDequeue(out var action)) action();
            Assert.That(await task, Is.False);
        }

        [Test]
        public async Task ConnectionLostBeforeLookupDoesNotQueue()
        {
            using var api = new TestApi();
            var scheduled = new Queue<Action>();
            bool trusted = true;
            var handler = new ClientBeatmapDownloadHandler(api, scheduled.Enqueue, _ => true);
            var task = handler.TryDownloadAsync(100, () => trusted);
            trusted = false;
            scheduled.Dequeue()();
            Assert.That(await task, Is.False);
            Assert.That(api.Requests, Is.Empty);
        }

        [Test]
        public async Task SubstitutedBeatmapsetIsRejected()
        {
            using var api = new TestApi();
            var handler = new ClientBeatmapDownloadHandler(api, action => action(), _ => throw new AssertionException("substituted map"));
            var task = handler.TryDownloadAsync(100, () => true);
            api.Requests[0].TriggerSuccess(new APIBeatmapSet { OnlineID = 999 });
            api.Scheduler.Update();
            Assert.That(await task, Is.False);
        }

        [TestCase("https://attacker.example", "https://attacker.example/metadata", false, false)]
        [TestCase("https://morasooma.net", "https://morasooma.net/metadata", true, false)]
        [TestCase("https://morasooma.net", "https://morasooma.net/metadata", false, true)]
        public async Task UntrustedContextNeverQueues(string apiUrl, string metadataUrl, bool custom, bool stableProtocol)
        {
            using var api = new TestApi();
            api.Endpoints.APIUrl = apiUrl;
            api.Endpoints.MetadataUrl = metadataUrl;
            MosuServerEnvironment.IsThirdPartyServer = custom;
            MosuServerEnvironment.UsesStableProtocol = stableProtocol;
            var handler = new ClientBeatmapDownloadHandler(api, action => action(), _ => throw new AssertionException("untrusted"));
            Assert.That(await handler.TryDownloadAsync(100, () => true), Is.False);
            Assert.That(api.Requests, Is.Empty);
        }

        [Test]
        public async Task InvalidIdOfflineAndUntrustedConnectionNeverQueue()
        {
            using var api = new TestApi();
            var handler = new ClientBeatmapDownloadHandler(api, action => action(), _ => true);
            Assert.That(await handler.TryDownloadAsync(0, () => true), Is.False);
            Assert.That(await handler.TryDownloadAsync(100, () => false), Is.False);
            api.Logout();
            Assert.That(await handler.TryDownloadAsync(100, () => true), Is.False);
            Assert.That(api.Requests, Is.Empty);
        }

        private partial class TestApi : DummyAPIAccess
        {
            public TestApi()
            {
                Endpoints.APIUrl = MosuServerEnvironment.PublicServerUrl;
                Endpoints.MetadataUrl = MosuServerEnvironment.PublicServerUrl + "/metadata";
            }

            public new Scheduler Scheduler => base.Scheduler;
            public readonly List<GetBeatmapSetRequest> Requests = new List<GetBeatmapSetRequest>();
            public override void Queue(APIRequest request)
            {
                request.AttachAPI(this);
                Requests.Add((GetBeatmapSetRequest)request);
            }
        }
    }
}
