using NUnit.Framework;
using osu.Framework.Configuration;
using osu.Game.Performance.Diagnostics;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class MosuDiagnosticsRendererSelectionTest
    {
        [Test]
        public void TestVulkanIncludedWhenOriginalRendererIsOpenGL()
        {
            var supported = new[] { RendererType.Automatic, RendererType.OpenGL, RendererType.Deferred_OpenGL, RendererType.Deferred_Vulkan };
            Assert.That(MosuDiagnosticsRendererSelection.GetComparisonRenderers(supported, RendererType.OpenGL),
                Is.EqualTo(new[] { RendererType.Deferred_Vulkan, RendererType.Deferred_OpenGL, RendererType.OpenGL }));
        }

        [Test]
        public void TestMoraVulkanIsIncludedInComparison()
        {
            var supported = new[] { RendererType.Automatic, RendererType.OpenGL, RendererType.Mora_Vulkan };
            Assert.That(MosuDiagnosticsRendererSelection.GetComparisonRenderers(supported, RendererType.OpenGL),
                Is.EqualTo(new[] { RendererType.Mora_Vulkan, RendererType.OpenGL }));
        }

        [Test]
        public void TestOriginalVulkanRunsLast()
        {
            var supported = new[] { RendererType.Automatic, RendererType.OpenGL, RendererType.Deferred_Vulkan };
            Assert.That(MosuDiagnosticsRendererSelection.GetComparisonRenderers(supported, RendererType.Deferred_Vulkan),
                Is.EqualTo(new[] { RendererType.OpenGL, RendererType.Deferred_Vulkan }));
        }

        [Test]
        public void TestUnsupportedBackendsAreNotInvented()
        {
            var supported = new[] { RendererType.Automatic, RendererType.Metal, RendererType.Deferred_Metal };
            Assert.That(MosuDiagnosticsRendererSelection.GetComparisonRenderers(supported, RendererType.Metal),
                Is.EqualTo(new[] { RendererType.Deferred_Metal, RendererType.Metal }));
        }

        [Test]
        public void TestAutomaticAndDuplicatesExcluded()
        {
            var supported = new[] { RendererType.Automatic, RendererType.OpenGL, RendererType.OpenGL, RendererType.Automatic };
            Assert.That(MosuDiagnosticsRendererSelection.GetComparisonRenderers(supported, RendererType.OpenGL),
                Is.EqualTo(new[] { RendererType.OpenGL }));
        }

        [Test]
        public void TestEmptyExplicitBackendList()
        {
            Assert.That(MosuDiagnosticsRendererSelection.GetComparisonRenderers(new[] { RendererType.Automatic }, RendererType.OpenGL), Is.Empty);
        }
    }
}
