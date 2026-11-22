using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Logging;
using osu.Game.Utils;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class CrashReportStoreTest
    {
        private string root = null!;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "mosu-crash-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
        }

        [TearDown]
        public void TearDown() => Directory.Delete(root, true);

        [Test]
        public void TestExceptionAndSessionLogsSurviveNextStartup()
        {
            string logs = Path.Combine(root, "logs");
            Directory.CreateDirectory(logs);
            string logName = Logger.GetLogger().Filename;
            File.WriteAllText(Path.Combine(logs, logName), "crashed session");
            File.WriteAllText(Path.Combine(logs, "old-session.runtime.log"), "not this session");
            var store = new CrashReportStore(root);
            Assert.That(store.Previous, Is.Null);
            store.Capture(new InvalidOperationException("fatal cause", new Exception("inner cause")), "test-version");
            File.WriteAllText(Path.Combine(logs, logName), "new session");
            var next = new CrashReportStore(root);
            Assert.That(next.Previous, Is.Not.Null);
            Assert.That(next.Previous!.Exception, Does.Contain("fatal cause").And.Contain("inner cause"));
            using (var zip = ZipFile.OpenRead(next.Export(next.Previous)))
            {
                Assert.That(zip.Entries.Select(e => e.FullName), Is.EquivalentTo(new[] { "crash.txt", logName }));
                using var reader = new StreamReader(zip.GetEntry(logName)!.Open());
                Assert.That(reader.ReadToEnd(), Is.EqualTo("crashed session"));
            }
            next.Acknowledge(next.Previous);
            Assert.That(new CrashReportStore(root).Previous, Is.Null);
            Assert.That(Directory.GetDirectories(Path.Combine(root, "crashes")), Has.Length.EqualTo(1));
        }

        [Test]
        public void TestGpuDiagnosticsSurvivePersistenceAndZipWithoutExportingOtherExceptionData()
        {
            var gpuFailure = new InvalidOperationException("original GPU stack");
            gpuFailure.Data["Mosu.D3D11.DeviceRemovedReason"] = "GetDeviceRemovedReason=0x887A0006 (DXGI_ERROR_DEVICE_HUNG)";
            gpuFailure.Data["private"] = "must-not-export";
            var exception = new AggregateException(new InvalidOperationException("unrelated"), new InvalidOperationException("wrapper", gpuFailure));
            new CrashReportStore(root).Capture(exception, "test");
            var next = new CrashReportStore(root);
            Assert.That(next.Previous!.Exception, Does.Contain("original GPU stack").And.Contain("DXGI_ERROR_DEVICE_HUNG").And.Not.Contain("must-not-export"));
            using var zip = ZipFile.OpenRead(next.Export(next.Previous));
            using var reader = new StreamReader(zip.GetEntry("crash.txt")!.Open());
            string report = reader.ReadToEnd();
            Assert.That(report, Does.Contain("0x887A0006").And.Not.Contain("must-not-export"));
            Assert.That(report.Split("GetDeviceRemovedReason=").Length, Is.EqualTo(2), "Diagnostic must appear exactly once.");
        }

        [Test]
        public void TestFirstFatalExceptionIsNotOverwritten()
        {
            var store = new CrashReportStore(root);
            store.Capture(new Exception("original"), "test");
            store.Capture(new Exception("secondary failure"), "test");
            Assert.That(new CrashReportStore(root).Previous!.Exception, Does.Contain("original").And.Not.Contain("secondary failure"));
        }

        [Test]
        public void TestDismissingOldReportDoesNotDeleteNewCrash()
        {
            new CrashReportStore(root).Capture(new Exception("first"), "test");
            var next = new CrashReportStore(root);
            next.Capture(new Exception("second"), "test");
            next.Acknowledge(next.Previous!);
            Assert.That(new CrashReportStore(root).Previous!.Exception, Does.Contain("second"));
        }

        [Test]
        public void TestDamagedReportDoesNotPreventStartup()
        {
            Directory.CreateDirectory(Path.Combine(root, "crashes"));
            File.WriteAllText(Path.Combine(root, "crashes", "pending.json"), "not json");
            Assert.That(new CrashReportStore(root).Previous, Is.Null);
        }

        [Test]
        public void TestExportWithoutLogsStillContainsException()
        {
            new CrashReportStore(root).Capture(new Exception("fatal"), "test");
            var next = new CrashReportStore(root);
            using var zip = ZipFile.OpenRead(next.Export(next.Previous!));
            using var reader = new StreamReader(zip.GetEntry("crash.txt")!.Open());
            Assert.That(reader.ReadToEnd(), Does.Contain("fatal"));
        }
    }
}
