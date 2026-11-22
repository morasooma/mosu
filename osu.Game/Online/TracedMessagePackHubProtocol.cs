using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using osu.Framework.Statistics;

namespace osu.Game.Online
{
    /// <summary>
    /// Delegates the unchanged wire protocol, measuring synchronous serialization/parsing at
    /// the actual protocol boundary (including work performed after an asynchronous send resumes).
    /// Does not log message arguments, tokens, user identifiers or serialized payloads.
    /// </summary>
    internal sealed class TracedMessagePackHubProtocol : IHubProtocol
    {
        private readonly MessagePackHubProtocol inner;

        public TracedMessagePackHubProtocol(IOptions<MessagePackHubProtocolOptions> options)
        {
            inner = new MessagePackHubProtocol(options);
        }

        internal static void Register(IServiceCollection services)
        {
            // Keep JSON and any other protocols. Replace only the stock MessagePack registration.
            for (int i = services.Count - 1; i >= 0; i--)
            {
                if (services[i].ServiceType == typeof(IHubProtocol) && services[i].ImplementationType == typeof(MessagePackHubProtocol))
                    services.RemoveAt(i);
            }
            services.AddSingleton<IHubProtocol, TracedMessagePackHubProtocol>();
        }

        public string Name => inner.Name;
        public int Version => inner.Version;
        public TransferFormat TransferFormat => inner.TransferFormat;
        public bool IsVersionSupported(int version) => inner.IsVersionSupported(version);

        public bool TryParseMessage(ref ReadOnlySequence<byte> input, IInvocationBinder binder, [NotNullWhen(true)] out HubMessage? message)
        {
            using var operation = StutterTraceEventSource.Log.Measure("SignalR.MessagePack.Parse", input.Length);
            return inner.TryParseMessage(ref input, binder, out message);
        }

        public void WriteMessage(HubMessage message, IBufferWriter<byte> output)
        {
            using var operation = StutterTraceEventSource.Log.Measure("SignalR.MessagePack.Write");
            inner.WriteMessage(message, output);
        }

        public ReadOnlyMemory<byte> GetMessageBytes(HubMessage message)
        {
            using var operation = StutterTraceEventSource.Log.Measure("SignalR.MessagePack.GetBytes");
            return inner.GetMessageBytes(message);
        }
    }
}
