using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Game.Performance;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class BoundedPerformanceFileSinkTest
    {
        [Test]
        public async Task TestBlockedWriterOrderingOverflowAndDrain()
        {
            using var entered = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            var output = new StringWriter();
            var sink = new BoundedPerformanceFileSink(_ => new BlockingWriter(output, entered, release), 2);
            Assert.That(sink.TryWrite("one", "a"), Is.True);
            Assert.That(entered.Wait(TimeSpan.FromSeconds(5)), Is.True);
            Assert.That(sink.TryWrite("one", "b"), Is.True);
            Assert.That(sink.TryWrite("one", "c"), Is.True);
            Assert.That(sink.TryWrite("one", "d"), Is.False);
            Assert.That(sink.Dropped, Is.EqualTo(1));
            sink.Stop();
            Assert.That(sink.Completion.IsCompleted, Is.False);
            release.Set();
            await sink.Completion.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.That(output.ToString(), Is.EqualTo("abc"));
            Assert.That(sink.Failure, Is.Null);
        }

        [Test]
        public async Task TestFailureCountsPendingAndSkipsSummary()
        {
            bool summary = false;
            var sink = new BoundedPerformanceFileSink(_ => new FailingWriter(), afterDrain: () => summary = true);
            Assert.That(sink.TryWrite("one", "a"), Is.True);
            sink.Stop();
            await sink.Completion.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.That(sink.Failure, Is.TypeOf<IOException>());
            Assert.That(summary, Is.False);
            Assert.That(sink.TryWrite("one", "b"), Is.False);
            Assert.That(sink.Dropped, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public async Task TestSuccessiveSessionsAreIndependent()
        {
            var files = new ConcurrentDictionary<string, StringWriter>();
            BoundedPerformanceFileSink create() => new BoundedPerformanceFileSink(path => files[path] = new StringWriter());
            var first = create();
            first.TryWrite("first", "old");
            first.Stop();
            var second = create();
            second.TryWrite("second", "new");
            second.Stop();
            await Task.WhenAll(first.Completion, second.Completion).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Multiple(() =>
            {
                Assert.That(files["first"].ToString(), Is.EqualTo("old"));
                Assert.That(files["second"].ToString(), Is.EqualTo("new"));
            });
        }

        private sealed class BlockingWriter(StringWriter output, ManualResetEventSlim entered, ManualResetEventSlim release) : TextWriter
        {
            public override System.Text.Encoding Encoding => output.Encoding;
            public override void Write(string? value)
            {
                entered.Set();
                if (!release.Wait(TimeSpan.FromSeconds(5)))
                    throw new TimeoutException();
                output.Write(value);
            }
        }

        private sealed class FailingWriter : StringWriter
        {
            public override void Write(string? value) => throw new IOException("controlled failure");
        }
    }
}
