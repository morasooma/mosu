using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Game.Configuration;
using osu.Game.Online;
using osu.Game.Online.Metadata;
using osu.Game.Users;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class MetadataActivityDispatchTest
    {
        [Test]
        public void TestEveryActivityUnionSnapshotPreservesSubtypeAndWireBytes()
        {
            var protocol = new MessagePackHubProtocol(Options.Create(new MessagePackHubProtocolOptions
            {
                SerializerOptions = SignalRUnionWorkaroundResolver.OPTIONS,
            }));
            foreach (Type type in typeof(UserActivity).GetNestedTypes().Where(t => !t.IsAbstract && typeof(UserActivity).IsAssignableFrom(t)))
            {
                var activity = (UserActivity)Activator.CreateInstance(type)!;
                foreach (PropertyInfo property in type.GetProperties().Where(p => p.CanWrite))
                {
                    if (property.PropertyType == typeof(string)) property.SetValue(activity, "original");
                    if (property.PropertyType == typeof(int)) property.SetValue(activity, 42);
                    if (property.PropertyType == typeof(long)) property.SetValue(activity, 123L);
                    if (property.PropertyType == typeof(string[])) property.SetValue(activity, new[] { "DT", "HR" });
                }
                byte[] wire = protocol.GetMessageBytes(new InvocationMessage("1", "UpdateActivity", new object?[] { activity })).ToArray();
                var snapshot = activity.Snapshot();
                Assert.That(snapshot.GetType(), Is.EqualTo(type));
                Assert.That(snapshot, Is.Not.SameAs(activity));
                foreach (PropertyInfo property in type.GetProperties().Where(p => p.CanWrite))
                {
                    if (property.PropertyType == typeof(string)) property.SetValue(activity, "mutated");
                    if (property.PropertyType == typeof(int)) property.SetValue(activity, 99);
                    if (property.PropertyType == typeof(long)) property.SetValue(activity, 999L);
                }
                if (activity is UserActivity.InGame game) game.ModAcronyms![0] = "NC";
                Assert.That(protocol.GetMessageBytes(new InvocationMessage("1", "UpdateActivity", new object?[] { snapshot })).ToArray(), Is.EqualTo(wire), type.Name);
            }
        }

        [Test]
        public async Task TestRealClientDispatchSnapshotsAndOrdersWithoutWaitingForAcknowledgement()
        {
            using var gateEntered = new ManualResetEventSlim();
            var releaseGate = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var response = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var received = new List<(string Name, object? Argument)>();
            var second = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            int caller = Environment.CurrentManagedThreadId;
            using var services = new ServiceCollection().BuildServiceProvider();
            var hub = new Mock<HubConnection>(Mock.Of<IConnectionFactory>(), new JsonHubProtocol(),
                new UriEndPoint(new Uri("http://127.0.0.1/unused")), services, NullLoggerFactory.Instance);
            // InvokeCoreAsync is mocked; this test never opens a connection.
            hub.Setup(h => h.InvokeCoreAsync(It.IsAny<string>(), It.IsAny<Type>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
               .Returns((string name, Type _, object?[] args, CancellationToken __) =>
               {
                   lock (received) received.Add((name, args[0]));
                   second.TrySetResult();
                   return response.Task;
               });
            var connected = new Bindable<bool>(true);
            var connector = new Mock<IHubClientConnector>();
            connector.SetupGet(c => c.IsConnected).Returns(connected);
            HubConnection? currentConnection = hub.Object;
            connector.SetupGet(c => c.CurrentConnection).Returns(() => currentConnection);
            using var client = new OnlineMetadataClient(new EndpointConfiguration());
            typeof(OnlineMetadataClient).GetField("connector", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(client, connector.Object);
            var queue = (osu.Game.Performance.AsyncOperationQueue)typeof(OnlineMetadataClient).GetField("outgoingPresence", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(client)!;
            Task gate = queue.Enqueue(() => { Assert.That(Environment.CurrentManagedThreadId, Is.Not.EqualTo(caller)); gateEntered.Set(); return releaseGate.Task; });
            Assert.That(gateEntered.Wait(TimeSpan.FromSeconds(5)), Is.True);
            var activity = new UserActivity.InSoloGame { BeatmapID = 42, ModAcronyms = new[] { "DT" } };
            Task first = client.UpdateActivity(activity);
            Task status = client.UpdateStatus(UserStatus.Online);
            activity.BeatmapID = 99;
            activity.ModAcronyms[0] = "NC";
            releaseGate.SetResult(null);
            await gate;
            await second.Task.WaitAsync(TimeSpan.FromSeconds(5));
            // A barrier follows both invocation initiations, not their pending responses.
            await queue.EnqueueInvocation(() => Task.CompletedTask);
            Assert.That(received.Select(x => x.Name), Is.EqualTo(new[] { "UpdateActivity", "UpdateStatus" }));
            Assert.That(((UserActivity.InSoloGame)received[0].Argument!).BeatmapID, Is.EqualTo(42));
            Assert.That(((UserActivity.InSoloGame)received[0].Argument!).ModAcronyms, Is.EqualTo(new[] { "DT" }));
            Assert.That(first.IsCompleted, Is.False);
            Assert.That(status.IsCompleted, Is.False);
            response.SetException(new InvalidOperationException("expected invocation failure"));
            Assert.ThrowsAsync<InvalidOperationException>(async () => await first);
            Assert.ThrowsAsync<InvalidOperationException>(async () => await status);
            // Prior failed responses must not poison a later invocation.
            hub.Setup(h => h.InvokeCoreAsync(It.IsAny<string>(), It.IsAny<Type>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync((object?)null);
            await client.UpdateActivity(null);
            var releaseDisconnect = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            Task blocker = queue.Enqueue(() => releaseDisconnect.Task);
            Task stale = client.UpdateActivity(new UserActivity.ChoosingBeatmap());
            currentConnection = null; // queued payload belongs to the old connection
            releaseDisconnect.SetResult(null);
            await blocker;
            Assert.ThrowsAsync<TaskCanceledException>(async () => await stale);
            connected.Value = false;
            Assert.ThrowsAsync<TaskCanceledException>(async () => await client.UpdateActivity(null));
        }
    }
}
