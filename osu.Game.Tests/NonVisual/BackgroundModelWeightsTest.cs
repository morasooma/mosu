using System;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Graphics.Backgrounds.AI;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class BackgroundModelWeightsTest
    {
        [Test]
        [Explicit("Pinned original source files required via MOSU_BACKGROUND_SOURCE.")]
        public void TestRealWeightPreparation()
        {
            string root = Environment.GetEnvironmentVariable("MOSU_BACKGROUND_SOURCE") ?? string.Empty;
            Assert.That(Directory.Exists(root), Is.True);
            using var unet = new BackgroundModelWeights.TorchFile(Path.Combine(root, "tiny/unet/diffusion_pytorch_model.bin"));
            using var lora = new BackgroundModelWeights.SafeFile(Path.Combine(root, "lcm-adapter/adapter_model.safetensors"));
            string output = Environment.GetEnvironmentVariable("MOSU_BACKGROUND_PREPARED") ?? throw new InvalidOperationException("MOSU_BACKGROUND_PREPARED required");
            Directory.CreateDirectory(output);
            int merged = 0;
            BackgroundModelWeights.WriteSafe(Path.Combine(output, "unet.safetensors"), unet.Names, name =>
            {
                var weight = unet.Read(name);
                string prefix = "base_model.model." + name[..^7];
                if (name.EndsWith(".weight", StringComparison.Ordinal) && lora.Contains(prefix + ".lora_A.weight"))
                {
                    merged++;
                    return BackgroundModelWeights.Merge(weight, lora.Read(prefix + ".lora_A.weight"), lora.Read(prefix + ".lora_B.weight"), CancellationToken.None);
                }
                return weight;
            }, BackgroundModelWeights.UnetName, true, CancellationToken.None);
            Assert.That(merged, Is.EqualTo(147));
            using var clip = new BackgroundModelWeights.TorchFile(Path.Combine(root, "tiny/text_encoder/pytorch_model.bin"));
            BackgroundModelWeights.WriteClip(Path.Combine(output, "clip.gguf"), clip, CancellationToken.None);
            using var tae = new BackgroundModelWeights.SafeFile(Path.Combine(root, "taesd/diffusion_pytorch_model.safetensors"));
            BackgroundModelWeights.WriteSafe(Path.Combine(output, "tae.safetensors"), tae.Names, tae.Read, n => n, false, CancellationToken.None);
        }

        [Test]
        public void TestMergeScalingAndNames()
        {
            var weight = new BackgroundModelWeights.Tensor(new[] { 1, 2 }, new float[] { 10, 20 });
            var down = new BackgroundModelWeights.Tensor(new[] { 64, 2 }, Enumerable.Repeat(1f, 128).ToArray());
            var up = new BackgroundModelWeights.Tensor(new[] { 1, 64 }, Enumerable.Repeat(2f, 64).ToArray());
            Assert.That(BackgroundModelWeights.Merge(weight, down, up, CancellationToken.None).Values, Is.EqualTo(new float[] { 26, 36 }));
            Assert.That(BackgroundModelWeights.UnetName("down_blocks.0.resnets.0.conv1.weight"), Is.EqualTo("model.diffusion_model.input_blocks.1.0.in_layers.2.weight"));
        }
    }
}
