// MIT-licensed resource and cross-compilation regression checks.
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Graphics.Shaders;
using System.Reflection;
using osu.Framework.IO.Stores;
using osu.Game.Resources;
using Veldrid.SPIRV;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class MorasoomaShaderResourcesTest
    {
        private static ResourceStore<byte[]> resources()
        {
            var store = new ResourceStore<byte[]>();
            store.AddStore(new NamespacedResourceStore<byte[]>(new DllResourceStore(typeof(Morasooma.Resources.ResourceAssembly).Assembly), "ShaderOverrides/Shaders"));
            store.AddStore(new NamespacedResourceStore<byte[]>(new DllResourceStore(typeof(osu.Framework.Game).Assembly), "Resources/Shaders"));
            return store;
        }

        [Test]
        public void TestReplacementHashesAndOriginalAbsence()
        {
            using var own = new DllResourceStore(typeof(Morasooma.Resources.ResourceAssembly).Assembly);
            using var upstream = new DllResourceStore(OsuResources.ResourceAssembly);
            var manifest = JObject.Parse(Encoding.UTF8.GetString(own.Get("shader-manifest.json")));
            Assert.That(manifest["files"]!.Count(), Is.EqualTo(13));
            foreach (var row in manifest["files"]!)
            {
                string key = (string)row["resource"]!;
                byte[] bytes = own.Get("ShaderOverrides/" + key);
                Assert.That(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), Is.EqualTo((string)row["sha256"]!), key);
                Assert.That(upstream.Get(key), Is.Null, key);
            }
            Assert.That(upstream.GetAvailableResources().Any(n => n.StartsWith("Shaders/", StringComparison.Ordinal)), Is.False);
            using var source = resources();
            Assert.That(source.Get("sh_LegacyComboFire.fs"), Is.Null);
            Assert.That(source.Get("sh_RankedPlayBackground.fs"), Is.Null);
            foreach (string include in new[] { "sh_Utils.h", "sh_Masking.h", "sh_CircularProgressUtils.h" })
                Assert.That(source.Get(include), Is.Not.Null);
            foreach (string notice in new[] { "NOTICE.txt", "Morasooma-MIT.txt", "osu-framework-MIT.txt" })
                Assert.That(own.Get("Licenses/Shaders/" + notice), Is.Not.Null);
        }

        [TestCase("Texture2D", "ArgonBarPath")]
        [TestCase("Texture2D", "ArgonBarPathBackground")]
        [TestCase("PositionAndColour", "CircularFlashlight")]
        [TestCase("PositionAndColour", "RectangularFlashlight")]
        [TestCase("Texture2D", "Ghost")]
        [TestCase("LogoAnimation", "LogoAnimation")]
        [TestCase("CursorTrail", "Texture")]
        [TestCase("Texture2D", "SpinnerGlow")]
        [TestCase("Texture2D", "TriangleBorder")]
        public void TestRealSpirvAndCrossCompilation(string vertex, string fragment)
        {
            using var source = resources();
            // Keep framework internals unchanged; use its exact stage preprocessor through reflection.
            var assembly = typeof(ShaderManager).Assembly;
            var rendererType = assembly.GetType("osu.Framework.Graphics.OpenGL.GLRenderer", true)!;
            var partType = assembly.GetType("osu.Framework.Graphics.OpenGL.Shaders.GLShaderPart", true)!;
            // No device/window is initialised; this object is only queried for preprocessor capabilities.
            object renderer = Activator.CreateInstance(rendererType, true)!;
            using var manager = new ShaderManager(new DummyRenderer(), source);
            using var v = (IDisposable)Activator.CreateInstance(partType, renderer, vertex, source.Get("sh_" + vertex + ".vs"), osuTK.Graphics.ES30.ShaderType.VertexShader, manager)!;
            using var f = (IDisposable)Activator.CreateInstance(partType, renderer, fragment, source.Get("sh_" + fragment + ".fs"), osuTK.Graphics.ES30.ShaderType.FragmentShader, manager)!;
            var raw = partType.GetMethod("GetRawText", BindingFlags.Instance | BindingFlags.Public)!;
            string vertexText = (string)raw.Invoke(v, null)!;
            string fragmentText = (string)raw.Invoke(f, null)!;
            var compiler = new ShaderCompilationStore();
            // Real Vulkan GLSL -> SPIR-V -> target compilers, not a DummyRenderer success.
            // No Vulkan surface/device is needed; this does not claim hardware execution.
            foreach (var target in new[] { CrossCompileTarget.GLSL, CrossCompileTarget.ESSL, CrossCompileTarget.HLSL, CrossCompileTarget.MSL })
            {
                var result = compiler.CompileVertexFragment(vertexText, fragmentText, target);
                Assert.That(result.VertexBytes.Length, Is.GreaterThan(20), target.ToString());
                Assert.That(result.FragmentBytes.Length, Is.GreaterThan(20), target.ToString());
                Assert.That(result.VertexText, Is.Not.Empty, target.ToString());
                Assert.That(result.FragmentText, Is.Not.Empty, target.ToString());
            }
        }
    }
}
