using System;
using System.Reflection;
using NUnit.Framework;
using osu.Framework.Audio.Sample;
using osu.Framework.Graphics.Audio;
using osu.Framework.Graphics.Containers;
using osu.Game.Audio;
using osu.Game.Skinning;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class SkinnableSoundAllocationTest
    {
        private const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void TestRepeatedStopDoesNotAllocateAndStopsEveryChannel()
        {
            using var sound = new SkinnableSound();
            var container = (AudioContainer<PoolableSkinnableSample>)typeof(SkinnableSound).GetField("samplesContainer", instance)!.GetValue(sound)!;
            var first = new PoolableSkinnableSample(new SampleInfo("sectionpass"));
            var second = new PoolableSkinnableSample(new SampleInfo("sectionfail"));
            container.Add(first);
            container.Add(second);

            using var firstChannel = new SampleVirtual().GetChannel();
            using var secondChannel = new SampleVirtual().GetChannel();
            typeof(PoolableSkinnableSample).GetField("activeChannel", instance)!.SetValue(first, firstChannel);
            typeof(PoolableSkinnableSample).GetField("activeChannel", instance)!.SetValue(second, secondChannel);
            sound.Stop();
            Assert.That(typeof(PoolableSkinnableSample).GetField("activeChannel", instance)!.GetValue(first), Is.Null);
            Assert.That(typeof(PoolableSkinnableSample).GetField("activeChannel", instance)!.GetValue(second), Is.Null);

            for (int i = 0; i < 1000; i++)
                sound.Stop();
            long start = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++)
                sound.Stop();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - start;
            Assert.That(allocated, Is.Zero, "The legacy break overlay stops these sounds on every update.");
        }
    }
}
