using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using osu.Framework.Statistics;
using osu.Game.Online;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class TracedMessagePackHubProtocolTest
    {
        private static IOptions<MessagePackHubProtocolOptions> options => Options.Create(new MessagePackHubProtocolOptions
        {
            SerializerOptions = SignalRUnionWorkaroundResolver.OPTIONS,
        });

        [Test]
        public void TestRegistrationRetainsJsonAndOneMeasuredMessagePackProtocol()
        {
            var builder = new HubConnectionBuilder().AddMessagePackProtocol();
            TracedMessagePackHubProtocol.Register(builder.Services);
            using var provider = builder.Services.BuildServiceProvider();
            var protocols = provider.GetServices<IHubProtocol>().ToArray();
            Assert.That(protocols.Count(p => p.Name == "messagepack"), Is.EqualTo(1));
            Assert.That(protocols.Single(p => p.Name == "messagepack"), Is.TypeOf<TracedMessagePackHubProtocol>());
            Assert.That(protocols.Any(p => p.Name == "json"), Is.True);
        }

        [Test]
        public void TestWireBytesParsingAndPropertiesMatchOriginal()
        {
            var original = new MessagePackHubProtocol(options);
            var measured = new TracedMessagePackHubProtocol(options);
            var message = new InvocationMessage("1", "Test", new object[] { 42, "hello" });
            byte[] expected = original.GetMessageBytes(message).ToArray();
            Assert.That(measured.GetMessageBytes(message).ToArray(), Is.EqualTo(expected));
            var output = new ArrayBufferWriter<byte>();
            measured.WriteMessage(message, output);
            Assert.That(output.WrittenSpan.ToArray(), Is.EqualTo(expected));
            var input = new ReadOnlySequence<byte>(expected);
            Assert.That(measured.TryParseMessage(ref input, new Binder(), out var parsed), Is.True);
            Assert.That(input.Length, Is.Zero);
            Assert.That(parsed, Is.TypeOf<InvocationMessage>());
            Assert.That(((InvocationMessage)parsed!).Arguments, Is.EqualTo(message.Arguments));
            Assert.That(measured.Name, Is.EqualTo(original.Name));
            Assert.That(measured.Version, Is.EqualTo(original.Version));
            Assert.That(measured.TransferFormat, Is.EqualTo(original.TransferFormat));
            Assert.That(measured.IsVersionSupported(original.Version), Is.True);
        }

        [Test]
        public void TestActualMessagePackParsingBoundaryEmitsOperationWithoutArguments()
        {
            var protocol = new TracedMessagePackHubProtocol(options);
            var bytes = protocol.GetMessageBytes(new InvocationMessage("Test", new object[] { 42, "private test argument" }));
            using var listener = new Listener();
            var input = new ReadOnlySequence<byte>(bytes);
            Assert.That(protocol.TryParseMessage(ref input, new Binder(slow: true), out _), Is.True);
            Assert.That(listener.Names, Does.Contain("SignalR.MessagePack.Parse"));
            Assert.That(listener.Names, Does.Not.Contain("private test argument"));
        }

        private sealed class Binder : IInvocationBinder
        {
            private readonly bool slow;
            public Binder(bool slow = false) => this.slow = slow;
            public Type GetReturnType(string invocationId) => typeof(object);
            public Type GetStreamItemType(string streamId) => typeof(object);
            public IReadOnlyList<Type> GetParameterTypes(string methodName)
            {
                if (slow) Thread.Sleep(10);
                return new[] { typeof(int), typeof(string) };
            }
        }

        private sealed class Listener : EventListener
        {
            public List<string>? Names = new();
            protected override void OnEventSourceCreated(EventSource source)
            {
                if (source.Name == StutterTraceEventSource.PROVIDER_NAME)
                    EnableEvents(source, EventLevel.Informational);
            }
            protected override void OnEventWritten(EventWrittenEventArgs e)
            {
                if (e.EventId == 5) Names?.Add((string)e.Payload![0]!);
            }
        }
    }
}
