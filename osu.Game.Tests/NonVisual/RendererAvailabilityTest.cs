using System.Linq;
using NUnit.Framework;
using osu.Framework.Configuration;
using osu.Game.Overlays.Settings.Sections.Graphics;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class RendererAvailabilityTest
    {
        [Test]
        public void TestMoraVulkanIsSelectableWithoutRenumberingUpstream()
        {
            var supported = new[] { RendererType.Automatic, RendererType.OpenGL, RendererType.Mora_Vulkan };
            Assert.Multiple(() =>
            {
                Assert.That(RendererSettings.GetAvailableRenderers(supported), Does.Contain(RendererType.Mora_Vulkan));
                Assert.That((int)RendererType.Deferred_Vulkan, Is.EqualTo(33));
                Assert.That((int)RendererType.Deferred_OpenGL, Is.EqualTo(35));
                Assert.That((int)RendererType.Mora_Vulkan, Is.EqualTo(1000));
            });
        }

        [Test]
        public void TestExperimentalVulkanIsSelectable()
        {
            var supported = new[] { RendererType.Automatic, RendererType.OpenGL, RendererType.Deferred_Vulkan };
            Assert.That(RendererSettings.GetAvailableRenderers(supported), Is.EquivalentTo(supported));
        }

        [Test]
        public void TestUnsupportedVulkanIsNotAdded()
        {
            var supported = new[] { RendererType.Automatic, RendererType.Metal, RendererType.Deferred_Metal };
            Assert.That(RendererSettings.GetAvailableRenderers(supported), Is.EquivalentTo(supported));
        }

        [Test]
        public void TestPlatformPreferenceOrderIsNotMutated()
        {
            var supported = new[] { RendererType.Automatic, RendererType.Deferred_Direct3D11, RendererType.OpenGL, RendererType.Deferred_Vulkan };
            var original = supported.ToArray();
            _ = RendererSettings.GetAvailableRenderers(supported).ToArray();
            Assert.That(supported, Is.EqualTo(original));
        }

        [Test]
        public void TestChoicesAreDistinctAndSorted()
        {
            var supported = new[] { RendererType.Deferred_Vulkan, RendererType.OpenGL, RendererType.Automatic, RendererType.Deferred_Vulkan };
            Assert.That(RendererSettings.GetAvailableRenderers(supported),
                Is.EqualTo(new[] { RendererType.Automatic, RendererType.OpenGL, RendererType.Deferred_Vulkan }));
        }
    }
}
