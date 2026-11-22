using System;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Performance;
using osu.Game.Performance.Debug;

namespace osu.Game.Tests.Visual.Performance
{
    [HeadlessTest]
    public partial class TestScenePerformanceDebugService : OsuTestScene
    {
        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        private PerformanceDebugService service = null!;
        private ProcessHeapSampler sampler = null!;
        private Task stopped = null!;

        private ProcessHeapSampler? currentSampler => (ProcessHeapSampler?)typeof(PerformanceDebugService)
            .GetField("processSampler", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(service);

        [SetUpSteps]
        public void SetUpSteps() => AddStep("add enabled telemetry", () =>
        {
            config.SetValue(OsuSetting.ForkDebugHudMode, DebugHudMode.Disabled);
            config.SetValue(OsuSetting.ForkShowMemoryInToolbar, true);
            config.SetValue(OsuSetting.ForkDebugFreezeAlerts, false);
            Child = service = new PerformanceDebugService();
        });

        [Test]
        public void TestTelemetryUsesOwnedSamplerAndStopsWhenDisabled()
        {
            AddUntilStep("memory published", () => service.Latest.WorkingSetMb > 0 && service.Latest.GcHeapMb > 0);
            AddStep("record worker", () => sampler = currentSampler!);
            AddWaitStep("read multiple snapshots", 5);
            AddAssert("same bounded poller", () => ReferenceEquals(currentSampler, sampler));
            AddAssert("sampler healthy", () => sampler.Failure == null);
            AddStep("disable all debug consumers", () => config.SetValue(OsuSetting.ForkShowMemoryInToolbar, false));
            AddUntilStep("sampler released", () => currentSampler == null && sampler.Completion.IsCompletedSuccessfully);
            AddStep("re-enable memory", () => config.SetValue(OsuSetting.ForkShowMemoryInToolbar, true));
            AddUntilStep("fresh poller started", () => currentSampler != null && !ReferenceEquals(currentSampler, sampler));
            AddStep("dispose service", () =>
            {
                stopped = currentSampler!.Completion;
                Clear();
            });
            AddUntilStep("disposed poller stopped", () => stopped.IsCompletedSuccessfully);
        }
    }
}
