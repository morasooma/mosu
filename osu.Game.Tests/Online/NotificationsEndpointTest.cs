using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Notifications.WebSocket;

namespace osu.Game.Tests.Online
{
    [TestFixture]
    public partial class NotificationsEndpointTest
    {
        private partial class ImmediateApi : DummyAPIAccess, IAPIProvider
        {
            void IAPIProvider.Schedule(Action action) => action();
        }

        private partial class DeferredApi : DummyAPIAccess, IAPIProvider
        {
            public readonly Queue<Action> Callbacks = new Queue<Action>();
            void IAPIProvider.Schedule(Action action) => Callbacks.Enqueue(action);
        }

        [Test]
        public void TestWaitsForScheduledFailureInsteadOfReadingNullResponse()
        {
            using var api = new DeferredApi();
            var failure = new WebException("502 Bad Gateway");
            api.HandleRequest = request =>
            {
                request.Fail(failure);
                return true;
            };
            var endpoint = WebSocketNotificationsClientConnector.GetEndpointAsync(api, CancellationToken.None);
            Assert.That(endpoint.IsCompleted, Is.False);
            Assert.That(api.Callbacks, Has.Count.EqualTo(1));
            api.Callbacks.Dequeue()();
            Assert.That(Assert.ThrowsAsync<WebException>(async () => await endpoint), Is.SameAs(failure));
        }

        [Test]
        public async Task TestValidEndpoint()
        {
            using var api = new ImmediateApi();
            api.HandleRequest = request =>
            {
                ((GetNotificationsRequest)request).TriggerSuccess(new APINotificationsBundle { Endpoint = "wss://example.test/notifications" });
                return true;
            };
            Assert.That(await WebSocketNotificationsClientConnector.GetEndpointAsync(api, CancellationToken.None),
                Is.EqualTo("wss://example.test/notifications"));
        }

        [Test]
        public void TestFailedRequestPreservesOriginalException()
        {
            using var api = new ImmediateApi();
            var failure = new WebException("502 Bad Gateway");
            api.HandleRequest = request =>
            {
                request.Fail(failure);
                return true;
            };
            Assert.That(Assert.ThrowsAsync<WebException>(async () =>
                await WebSocketNotificationsClientConnector.GetEndpointAsync(api, CancellationToken.None)), Is.SameAs(failure));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("https://example.test/not-a-websocket")]
        public void TestInvalidEndpoint(string? value)
        {
            using var api = new ImmediateApi();
            api.HandleRequest = request =>
            {
                ((GetNotificationsRequest)request).TriggerSuccess(new APINotificationsBundle { Endpoint = value! });
                return true;
            };
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await WebSocketNotificationsClientConnector.GetEndpointAsync(api, CancellationToken.None));
        }

        [Test]
        public void TestCancellationCancelsRequestWithoutWaitingForUpdate()
        {
            using var api = new ImmediateApi();
            APIRequest? pending = null;
            api.HandleRequest = request =>
            {
                pending = request;
                return true;
            };
            using var cancellation = new CancellationTokenSource();
            var endpoint = WebSocketNotificationsClientConnector.GetEndpointAsync(api, cancellation.Token);
            cancellation.Cancel();
            Assert.That(Assert.CatchAsync<OperationCanceledException>(async () => await endpoint), Is.Not.Null);
            Assert.That(pending!.CompletionState, Is.EqualTo(APIRequestCompletionState.Failed));
        }

        [Test]
        public void TestAlreadyCancelledDoesNotPerformRequest()
        {
            using var api = new ImmediateApi();
            bool requested = false;
            api.HandleRequest = _ => requested = true;
            Assert.CatchAsync<OperationCanceledException>(async () =>
                await WebSocketNotificationsClientConnector.GetEndpointAsync(api, new CancellationToken(true)));
            Assert.That(requested, Is.False);
        }
    }
}
