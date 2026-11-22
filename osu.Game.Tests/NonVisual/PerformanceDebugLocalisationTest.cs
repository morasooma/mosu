using System;
using System.Runtime;
using System.Reflection;
using NUnit.Framework;
using osu.Framework.Configuration;
using osu.Framework.Localisation;
using osu.Game.Localisation;
using osu.Game.Performance.Debug;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class PerformanceDebugLocalisationTest
    {
        [TestCase("en", "STUTTER", "texture upload", "RAM", "interactive")]
        [TestCase("ru", "ФРИЗ", "загрузка текстур", "ОЗУ", "интерактивный")]
        [TestCase("es", "TIRÓN", "carga de texturas", "RAM", "interactivo")]
        [TestCase("zh", "卡顿", "纹理上传", "内存", "交互式")]
        public void TestPanelsAndNestedTranslations(string locale, string alert, string cause, string memory, string gcMode)
        {
            // The client loads this assembly during startup; non-visual tests must load it explicitly.
            Assembly.Load("osu.Game.Resources");
            using var config = new TestConfig();
            using var manager = new LocalisationManager(config);
            manager.AddLanguage(locale, new ResourceManagerLocalisationStore(locale));
            config.SetValue(FrameworkSetting.Locale, locale);

            var sample = new PerformanceDebugSnapshot
            {
                DrawFrameMs = 42,
                WorkingSetMb = 123,
                GcHeapMb = 45,
                AllocationRateMbPerSecond = 6,
                GcMode = GCLatencyMode.Interactive,
            };
            var freeze = new PerformanceFreezeEvent(1, DateTimeOffset.Now, sample, PerformanceFreezeCause.TextureUpload);
            string message = manager.GetLocalisedString(PerformanceDebugStrings.FreezeAlert(freeze));
            Assert.That(message, Does.Contain(alert).And.Contain(cause).And.Contain("42"));
            Assert.That(manager.GetLocalisedString(PerformanceDebugStrings.CompactMetrics(sample)), Does.Contain(memory).And.Contain("123"));
            Assert.That(manager.GetLocalisedString(PerformanceDebugStrings.DetailedMetrics(sample)), Does.Contain(memory).And.Contain(gcMode));
            Assert.That(manager.GetLocalisedString(PerformanceDebugStrings.MemoryTooltip(sample)), Does.Contain(gcMode).And.Contain("123"));
            Assert.That(manager.GetLocalisedString(PerformanceDebugStrings.MemoryToolbar(sample)), Does.Contain(memory).And.Contain("123"));

            foreach (PerformanceFreezeCause value in Enum.GetValues<PerformanceFreezeCause>())
                Assert.That(manager.GetLocalisedString(PerformanceDebugStrings.FreezeCause(value)), Is.Not.Empty);

            foreach (GCLatencyMode value in Enum.GetValues<GCLatencyMode>())
                Assert.That(manager.GetLocalisedString(PerformanceDebugStrings.GcMode(value)), Is.Not.Empty);
        }

        private sealed class TestConfig : FrameworkConfigManager
        {
            protected override string Filename => null!;

            public TestConfig()
                : base(null!)
            {
            }
        }
    }
}
