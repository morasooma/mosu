using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.IO.Compression;
using NUnit.Framework;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Graphics.Audio;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Testing;
using osu.Framework.Threading;
using osu.Game.Audio;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.IO;
using osu.Game.Skinning;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    [NonParallelizable]
    public class InterfaceSoundAudioTest
    {
        private AudioThread thread = null!;
        private AudioManager audio = null!;
        private TemporaryNativeStorage storage = null!;
        private OsuConfigManager config = null!;
        private InterfaceSoundManager manager = null!;

        [SetUp]
        public void SetUp()
        {
            storage = new TemporaryNativeStorage("interface-sound-audio");
            config = new OsuConfigManager(storage);
            // Offline constructor is intentionally internal to the framework; use it here
            // rather than opening or muting the user's physical output device.
            thread = (AudioThread)Activator.CreateInstance(typeof(AudioThread),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                binder: null, args: new object[] { true }, culture: null)!;
            var samples = new ResourceStore<byte[]>();
            var resources = new ResourceStore<byte[]>();
            resources.AddStore(new NamespacedResourceStore<byte[]>(new DllResourceStore(typeof(Morasooma.Resources.ResourceAssembly).Assembly), "CompatibilityOverrides"));
            resources.AddStore(new NamespacedResourceStore<byte[]>(new DllResourceStore(typeof(Morasooma.Resources.ResourceAssembly).Assembly), "SoundOverrides"));
            resources.AddStore(new NamespacedResourceStore<byte[]>(new DllResourceStore(typeof(OsuGame).Assembly), "Resources"));
            resources.AddStore(new DllResourceStore(typeof(osu.Game.Resources.OsuResources).Assembly));
            samples.AddStore(new NamespacedResourceStore<byte[]>(resources, "Samples"));
            var tracks = new ResourceStore<byte[]>(new NamespacedResourceStore<byte[]>(resources, "Tracks"));
            audio = new AudioManager(thread, tracks, samples, null);
            manager = new InterfaceSoundManager(storage, config, audio, resources);
            thread.Start();
            Assert.That(manager.Ready.Wait(TimeSpan.FromSeconds(15)), Is.True);
        }

        [TearDown]
        public void TearDown()
        {
            manager.Dispose();
            config.Dispose();
            thread.Exit();
            Assert.That(SpinWait.SpinUntil(() => thread.Exited, TimeSpan.FromSeconds(10)), Is.True);
            storage.Dispose();
        }

        [TestCase("Intro/welcome")]
        [TestCase("Intro/seeya")]
        [TestCase("Intro/Welcome/welcome")]
        [TestCase("Intro/Welcome/seeya")]
        public void TestUpstreamVoicesAreNotOfferedBySoundPacks(string name)
        {
            Assert.That(InterfaceSoundManager.IsInterfaceEvent(name), Is.False);
            Assert.That(manager.Events, Does.Not.Contain(name));
            Assert.That(manager.Events, Does.Contain("Intro/Welcome/welcome_piano"));
        }

        [Test]
        public void TestLoadedControlSwitchesWithoutReloadingAndGameplayStaysUnwrapped()
        {
            Sample held = audio.Samples.Get("UI/check-on");
            Assert.That(held, Is.InstanceOf<InterfaceSoundSample>());
            Assert.That(audio.Samples.Get("Gameplay/normal-hitnormal"), Is.Not.InstanceOf<InterfaceSoundSample>());
            Assert.That(SpinWait.SpinUntil(() => held.IsLoaded, TimeSpan.FromSeconds(10)), Is.True);
            Assert.That(held.GetChannel().Name, Is.EqualTo("UI/check-on"));

            using var input = new MemoryStream(InterfaceSoundLibraryTest.wave());
            var pack = manager.Library.Replace(InterfaceSoundLibrary.DefaultPack, "UI/check-on", "custom.wav", input);
            manager.Select(pack.Id);
            Assert.That(manager.Ready.Wait(TimeSpan.FromSeconds(15)), Is.True);
            Assert.That(SpinWait.SpinUntil(() => held.GetChannel().Name.StartsWith("custom/", StringComparison.Ordinal), TimeSpan.FromSeconds(10)), Is.True);

            manager.Select(InterfaceSoundLibrary.DefaultPack);
            Assert.That(manager.Ready.Wait(TimeSpan.FromSeconds(15)), Is.True);
            Assert.That(held.GetChannel().Name, Is.EqualTo("UI/check-on"));
        }

        [Test]
        public void TestReplacementPlaysRealPcmAndPreservesVolumeAndDrawableLifetime()
        {
            Sample held = audio.Samples.Get("UI/check-on");
            using var input = new System.IO.MemoryStream(InterfaceSoundLibraryTest.wave());
            var pack = manager.Library.Replace(InterfaceSoundLibrary.DefaultPack, "UI/check-on", "test.wav", input);
            manager.Select(pack.Id);
            Assert.That(manager.Ready.Wait(TimeSpan.FromSeconds(15)), Is.True);
            Assert.That(SpinWait.SpinUntil(() => held.GetChannel().Name.StartsWith("custom/", StringComparison.Ordinal), TimeSpan.FromSeconds(10)), Is.True);

            held.Volume.Value = 0.25;
            audio.VolumeSample.Value = 0.5;
            using var drawable = new DrawableSample(held, disposeSampleOnDisposal: false);
            var channel = drawable.Play();
            Assert.That(SpinWait.SpinUntil(() => Math.Abs(channel.AggregateVolume.Value - 0.125) < 0.001, TimeSpan.FromSeconds(10)), Is.True);
            float[] pcm = new float[4410 * 2];
            Task<int> capture = audio.ReadSampleMixerDataAsync(pcm, 4410);
            Assert.That(capture.Wait(TimeSpan.FromSeconds(10)), Is.True);
            Assert.That(capture.GetAwaiter().GetResult(), Is.GreaterThan(0));
            Assert.That(pcm.Any(v => Math.Abs(v) > 0.0001), Is.True);
            Assert.That(channel.Playing, Is.True);
            channel.Stop();
            Assert.That(SpinWait.SpinUntil(() => !channel.Playing, TimeSpan.FromSeconds(10)), Is.True);
        }

        [Test]
        public void TestPortableIntentionalSilenceDoesNotFallBackToAudibleDefault()
        {
            Sample held = audio.Samples.Get("UI/check-on");
            using var silence = new MemoryStream(InterfaceSoundLibraryTest.wave(0));
            var pack = manager.Library.Replace(InterfaceSoundLibrary.DefaultPack, "UI/check-on", "silence.wav", silence);
            using var portable = new MemoryStream();
            manager.Library.Export(portable, pack.Id, key => (key + ".wav", InterfaceSoundLibraryTest.wave()));
            portable.Position = 0;
            var imported = manager.Library.Import(portable, "silence");
            manager.Select(imported.Id);
            Assert.That(manager.Ready.Wait(TimeSpan.FromSeconds(15)), Is.True);
            Assert.That(SpinWait.SpinUntil(() => held.IsLoaded && held.GetChannel().Name.StartsWith("custom/", StringComparison.Ordinal), TimeSpan.FromSeconds(10)), Is.True,
                "Intentional silence must not resolve to the audible built-in sample.");
            Assert.That(held.Length, Is.Zero);
            using var channel = held.Play();
            float[] pcm = new float[4410 * 2];
            var capture = audio.ReadSampleMixerDataAsync(pcm, 4410);
            Assert.That(capture.Wait(TimeSpan.FromSeconds(10)), Is.True);
            Assert.That(capture.GetAwaiter().GetResult(), Is.EqualTo(pcm.Length * sizeof(float)));
            Assert.That(pcm.All(v => float.IsFinite(v) && v == 0), Is.True);
        }

        [Test]
        public void TestMissingCustomFileFallsBackToDefault()
        {
            Sample held = audio.Samples.Get("UI/check-on");
            using var input = new MemoryStream(InterfaceSoundLibraryTest.wave());
            var pack = manager.Library.Replace(InterfaceSoundLibrary.DefaultPack, "UI/check-on", "test.wav", input);
            string path = storage.GetFullPath("interface-sounds/files/" + pack.Sounds["UI/check-on"]);
            File.Delete(path);
            manager.Select(pack.Id);
            Assert.That(manager.Ready.Wait(TimeSpan.FromSeconds(15)), Is.True);
            Assert.That(SpinWait.SpinUntil(() => held.GetChannel().Name == "UI/check-on", TimeSpan.FromSeconds(10)), Is.True);
        }

        [Test]
        public void TestRapidChangesKeepLatestSelection()
        {
            Sample held = audio.Samples.Get("UI/check-on");
            using var input = new MemoryStream(InterfaceSoundLibraryTest.wave());
            var pack = manager.Library.Replace(InterfaceSoundLibrary.DefaultPack, "UI/check-on", "custom.wav", input);
            for (int i = 0; i < 8; i++)
                manager.Select(i % 2 == 0 ? pack.Id : InterfaceSoundLibrary.DefaultPack);
            Assert.That(manager.Ready.Wait(TimeSpan.FromSeconds(15)), Is.True);
            Assert.That(held.GetChannel().Name, Is.EqualTo("UI/check-on"));
            Assert.That(manager.SelectedId, Is.EqualTo(InterfaceSoundLibrary.DefaultPack));
        }

        [Test]
        public void TestExplicitInterfaceStoresOptInWhileSkinStoresStayUntouched()
        {
            using var resources = new NamespacedResourceStore<byte[]>(new DllResourceStore(typeof(Morasooma.Resources.ResourceAssembly).Assembly), "SoundOverrides/Samples");
            using var skinStore = audio.GetSampleStore(resources, audio.SampleMixer);
            using var interfaceStore = audio.GetSampleStore(resources, audio.SampleMixer, useGlobalSampleTransform: true);
            Assert.That(skinStore.Get("UI/check-on"), Is.Not.Null.And.Not.InstanceOf<InterfaceSoundSample>());
            Assert.That(interfaceStore.Get("UI/check-on"), Is.InstanceOf<InterfaceSoundSample>());
        }

        [Test]
        public void TestInstalledSoundsDecodeThroughOfflineProductionAudio()
        {
            using var independent = new DllResourceStore(typeof(Morasooma.Resources.ResourceAssembly).Assembly);
            var manifest = Newtonsoft.Json.Linq.JObject.Parse(System.Text.Encoding.UTF8.GetString(independent.Get("sound-manifest.json")));
            var sounds = (Newtonsoft.Json.Linq.JArray)manifest["sounds"]!;
            Assert.That(sounds.Count, Is.EqualTo(363));
            Assert.That(manager.Events.Count, Is.EqualTo(209));
            using var legacyResources = new NamespacedResourceStore<byte[]>(new NamespacedResourceStore<byte[]>(
                new DllResourceStore(typeof(Morasooma.Resources.ResourceAssembly).Assembly), "SoundOverrides"), "Skins/Legacy");
            using var legacySamples = audio.GetSampleStore(legacyResources);
            using var retroResources = new NamespacedResourceStore<byte[]>(new NamespacedResourceStore<byte[]>(
                new DllResourceStore(typeof(Morasooma.Resources.ResourceAssembly).Assembly), "SoundOverrides"), "Skins/Retro");
            using var retroSamples = audio.GetSampleStore(retroResources);
            using var taikoResources = new osu.Game.Rulesets.Taiko.TaikoRuleset().CreateResourceStore();
            using var taikoSamples = audio.GetSampleStore(new NamespacedResourceStore<byte[]>(taikoResources, "Samples"));
            Assert.That(sounds.Count(e => (string)e["store"]! != "track"), Is.EqualTo(362));
            foreach (var entry in sounds.Where(e => (string)e["store"]! != "track"))
            {
                string key = (string)entry["event"]!;
                string lookup = key;
                Sample sample;
                if ((string)entry["store"]! == "taiko")
                {
                    lookup = key["TaikoResource/".Length..];
                    sample = taikoSamples.Get(lookup);
                }
                else if ((string)entry["scope"]! == "skin")
                {
                    lookup = key[(key.LastIndexOf('/') + 1)..];
                    sample = key.StartsWith("Skins/Retro/", StringComparison.Ordinal) ? retroSamples.Get(lookup) : legacySamples.Get(lookup);
                }
                else
                    sample = audio.Samples.Get(key);

                Assert.That(sample, Is.Not.Null, key);
                if ((string)entry["scope"]! == "interface")
                    Assert.That(sample, Is.InstanceOf<InterfaceSoundSample>(), key);
                else
                    Assert.That(sample, Is.Not.InstanceOf<InterfaceSoundSample>(), key);
                // SampleId becomes visible just before the audio thread publishes
                // Length. Wait for complete metadata, not only the nonzero ID.
                Assert.That(SpinWait.SpinUntil(() => sample.IsLoaded && sample.Length > 0, TimeSpan.FromSeconds(10)), Is.True, key);
                Assert.That(sample.Length, Is.EqualTo((double)entry["frames"]! / (int)entry["sample_rate"]! * 1000).Within(1), key);
                Assert.That(sample.GetChannel().Name, Is.EqualTo(lookup), key);
                Assert.That(audio.FlushSampleMixerAsync().Wait(TimeSpan.FromSeconds(10)), Is.True, key);
                if ((bool?)entry["intentional_silence"] == true)
                {
                    assertSilentPcm(sample, key);
                    continue;
                }
                using var channel = sample.GetChannel();
                channel.ManualFree = true;
                channel.Looping = (bool)entry["loop"]!;
                channel.Play();
                float[] pcm = new float[4800 * 2];
                // Play is queued on the sample/channel scheduler, while mixer reads use
                // AudioManager's scheduler. The first read may precede channel start.
                Assert.That(SpinWait.SpinUntil(() =>
                {
                    Array.Clear(pcm);
                    Task<int> capture = audio.ReadSampleMixerDataAsync(pcm, 4800);
                    Assert.That(capture.Wait(TimeSpan.FromSeconds(10)), Is.True, key);
                    Assert.That(capture.GetAwaiter().GetResult(), Is.GreaterThan(0), key);
                    Assert.That(pcm.All(float.IsFinite), Is.True, key);
                    return pcm.Any(v => Math.Abs(v) > 0.000001);
                }, TimeSpan.FromSeconds(5)), Is.True, key);
                if ((bool)entry["loop"]!)
                {
                    // The offline BASS mixer runs at 44.1kHz even for 48kHz WAVs.
                    // Decode beyond two complete wraps at each playback frequency,
                    // including long loops; a handful of short reads is insufficient.
                    const int mixer_sample_rate = 44100;
                    foreach (double frequency in new[] { 0.5, 1.5, 2.0 })
                    {
                        channel.Frequency.Value = frequency;
                        Assert.That(SpinWait.SpinUntil(() => channel.AggregateFrequency.Value == frequency,
                            TimeSpan.FromSeconds(5)), Is.True, key);
                        double cycleFrames = sample.Length / 1000 / frequency * mixer_sample_rate;
                        int framesToRead = (int)Math.Ceiling(cycleFrames * 2.25) + 4800;
                        double energyAfterTwoWraps = 0;
                        for (int framesRead = 0; framesRead < framesToRead;)
                        {
                            int frames = Math.Min(4800, framesToRead - framesRead);
                            Array.Clear(pcm);
                            var capture = audio.ReadSampleMixerDataAsync(pcm, frames);
                            Assert.That(capture.Wait(TimeSpan.FromSeconds(10)), Is.True, key);
                            Assert.That(capture.GetAwaiter().GetResult(), Is.EqualTo(frames * 2 * sizeof(float)), key);
                            Assert.That(pcm.All(float.IsFinite), Is.True, key);
                            for (int i = 0; i < frames; i++)
                            {
                                if (framesRead + i >= cycleFrames * 2)
                                    energyAfterTwoWraps += pcm[i * 2] * pcm[i * 2] + pcm[i * 2 + 1] * pcm[i * 2 + 1];
                            }
                            framesRead += frames;
                        }
                        Assert.That(energyAfterTwoWraps, Is.GreaterThan(0.000001), $"{key} at {frequency}x after two wraps");
                        Assert.That(channel.Playing, Is.True, key);
                    }
                }
                channel.Stop();
                Assert.That(SpinWait.SpinUntil(() => !channel.Playing, TimeSpan.FromSeconds(5)), Is.True, key);
            }
        }

        [TestCase(.5, false)]
        [TestCase(1, false)]
        [TestCase(1.5, false)]
        [TestCase(1, true)]
        public void TestSoftRankedCountersThroughOfflineProductionMixer(double frequency, bool animatePitch)
        {
            const string prefix = "Multiplayer/Matchmaking/Ranked/Results/";
            Sample tick = audio.Samples.Get(prefix + "score-tick");
            Sample counter = audio.Samples.Get(prefix + "pseudo-score-counter");
            Assert.That(SpinWait.SpinUntil(() => tick.IsLoaded && tick.Length > 0 && counter.IsLoaded && counter.Length > 0,
                TimeSpan.FromSeconds(10)), Is.True);
            Assert.That(tick.Length, Is.EqualTo(34.25).Within(.1));
            Assert.That(counter.Length, Is.EqualTo(2750).Within(.1));
            var enqueue = typeof(AudioComponent).GetMethod("EnqueueAction",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            void barrier(AudioComponent component)
            {
                var task = (Task)enqueue.Invoke(component, new object[] { (Action)(() => { }) })!;
                Assert.That(task.Wait(TimeSpan.FromSeconds(10)), Is.True);
            }

            using var player = tick.GetChannel();
            using var opponent = tick.GetChannel();
            using var damage = counter.GetChannel();
            player.ManualFree = opponent.ManualFree = damage.ManualFree = true;
            player.Looping = opponent.Looping = true;
            player.Balance.Value = -OsuGameBase.SFX_STEREO_STRENGTH;
            opponent.Balance.Value = OsuGameBase.SFX_STEREO_STRENGTH;
            player.Frequency.Value = opponent.Frequency.Value = animatePitch ? .5 : frequency;
            Assert.That(audio.FlushSampleMixerAsync().Wait(TimeSpan.FromSeconds(10)), Is.True);
            player.Play();
            opponent.Play();
            damage.Play();
            barrier(player);
            barrier(opponent);
            barrier(damage);
            Assert.That(player.Playing && opponent.Playing && damage.Playing, Is.True);

            const int mixer_rate = 44100;
            const int block_frames = 441;
            float[] pcm = new float[block_frames * 2];
            double[] energy = new double[mixer_rate * 3 + 1];
            double maximum = 0;
            try
            {
                for (int position = 0; position < mixer_rate * 3; position += block_frames)
                {
                    if (animatePitch && position < mixer_rate * 2)
                    {
                        double progress = position / (double)(mixer_rate * 2);
                        double easing = 1 - Math.Pow(1 - progress, 3);
                        player.Frequency.Value = .5 + .90 * easing;
                        opponent.Frequency.Value = .5 + .85 * easing;
                        barrier(player);
                        barrier(opponent);
                    }
                    if (position == mixer_rate * 2)
                    {
                        player.Looping = opponent.Looping = false;
                        barrier(player);
                        barrier(opponent);
                    }
                    Array.Fill(pcm, float.NaN);
                    var capture = audio.ReadSampleMixerDataAsync(pcm, block_frames);
                    Assert.That(capture.Wait(TimeSpan.FromSeconds(10)), Is.True);
                    Assert.That(capture.GetAwaiter().GetResult(), Is.EqualTo(pcm.Length * sizeof(float)));
                    for (int i = 0; i < block_frames; i++)
                    {
                        double power = 0;
                        for (int channel = 0; channel < 2; channel++)
                        {
                            float value = pcm[i * 2 + channel];
                            Assert.That(float.IsFinite(value), Is.True);
                            maximum = Math.Max(maximum, Math.Abs(value));
                            power += value * value / 2;
                        }
                        energy[position + i + 1] = energy[position + i] + power;
                    }
                }
                const int window = 1764; // 40 ms at the real offline mixer rate.
                double maxPower = 0;
                for (int i = window; i < energy.Length; i++)
                    maxPower = Math.Max(maxPower, energy[i] - energy[i - window]);
                double max40 = 10 * Math.Log10(maxPower / window);
                Assert.That(energy[^1], Is.GreaterThan(.01), "Counter remains audible, not silent");
                Assert.That(maximum, Is.LessThan(.35), "Both score loops plus damage, 100% effect/master");
                Assert.That(max40, Is.LessThan(-23), "No sustained loud counting bed");
                Assert.That(energy[^1] - energy[(int)(mixer_rate * 2.85)], Is.LessThan(.000001), "No stuck loop or late buzz");
                TestContext.WriteLine($"Ranked counters {frequency}x, animated={animatePitch}: peak {20 * Math.Log10(maximum):F2} / max40 {max40:F2} dBFS, offline BASS.");
            }
            finally
            {
                player.Stop();
                opponent.Stop();
                damage.Stop();
                barrier(player);
                barrier(opponent);
                barrier(damage);
            }
        }

        [Test]
        public void TestSelectedRankedMusicUsesTrackStoreAndLoopsOffline()
        {
            const string name = "rankedplay_bgm.ogg";
            using var independent = new DllResourceStore(typeof(Morasooma.Resources.ResourceAssembly).Assembly);
            var delivery = Newtonsoft.Json.Linq.JObject.Parse(Encoding.UTF8.GetString(independent.Get("sound-delivery.json")));
            var row = delivery["Tracks/rankedplay_bgm"]!;
            Assert.That((string)row["resource"]!, Is.EqualTo("Tracks/" + name));
            using (var input = audio.Tracks.GetStream(name))
            {
                Assert.That(input, Is.Not.Null);
                using var bytes = new MemoryStream();
                input.CopyTo(bytes);
                Assert.That(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes.ToArray())).ToLowerInvariant(),
                    Is.EqualTo((string)row["sha256"]!), "Global track namespace must resolve the selected i++ bytes");
            }
            Assert.That(audio.Samples.Get("rankedplay_bgm"), Is.Null, "Music must not enter the sample store");
            Assert.That(manager.Events, Does.Not.Contain("Tracks/rankedplay_bgm"));
            using var track = audio.Tracks.Get(name);
            Assert.That(track, Is.Not.Null);
            Assert.That(SpinWait.SpinUntil(() => track.IsLoaded && track.Length > 0, TimeSpan.FromSeconds(15)), Is.True);
            Assert.That(track.Length, Is.EqualTo(48000).Within(1));
            Assert.That(track.Name, Is.EqualTo(name));
            audio.VolumeTrack.Value = .37;
            audio.VolumeSample.Value = .19;
            track.Volume.Value = .5;
            Assert.That(SpinWait.SpinUntil(() => Math.Abs(track.AggregateVolume.Value - .185) < .001, TimeSpan.FromSeconds(5)), Is.True,
                "Track follows music, not effect volume");
            track.Volume.Value = 1;
            audio.VolumeTrack.Value = 1;
            bool completed = false;
            bool failed = false;
            track.Completed += () => completed = true;
            track.Failed += () => failed = true;
            track.Looping = true;
            Assert.That(track.StartAsync().Wait(TimeSpan.FromSeconds(10)), Is.True);
            Assert.That(SpinWait.SpinUntil(() => track.IsRunning, TimeSpan.FromSeconds(5)), Is.True);
            const int rate = 44100;
            int total = (int)Math.Ceiling(track.Length / 1000 * rate * 2.25) + 4800;
            double afterTwoWraps = 0;
            double maximum = 0;
            float[] pcm = new float[4800 * 2];
            for (int position = 0; position < total;)
            {
                int frames = Math.Min(4800, total - position);
                Array.Clear(pcm);
                var capture = audio.ReadMixerDataAsync(audio.TrackMixer, pcm, frames);
                Assert.That(capture.Wait(TimeSpan.FromSeconds(10)), Is.True);
                Assert.That(capture.GetAwaiter().GetResult(), Is.EqualTo(frames * 2 * sizeof(float)));
                Assert.That(pcm.All(float.IsFinite), Is.True);
                for (int i = 0; i < frames * 2; i++)
                {
                    maximum = Math.Max(maximum, Math.Abs(pcm[i]));
                    if (position + i / 2 >= track.Length / 1000 * rate * 2)
                        afterTwoWraps += pcm[i] * pcm[i];
                }
                position += frames;
            }
            Assert.That(afterTwoWraps, Is.GreaterThan(.01));
            Assert.That(maximum, Is.LessThan(.5));
            Assert.That(completed, Is.False);
            Assert.That(failed, Is.False);
            Assert.That(track.IsRunning, Is.True);
            Assert.That(track.StopAsync().Wait(TimeSpan.FromSeconds(10)), Is.True);
            var seek = track.SeekAsync(1234);
            Assert.That(seek.Wait(TimeSpan.FromSeconds(10)), Is.True);
            Assert.That(seek.GetAwaiter().GetResult(), Is.True);
            Assert.That(track.StartAsync().Wait(TimeSpan.FromSeconds(10)), Is.True);
            Assert.That(track.StopAsync().Wait(TimeSpan.FromSeconds(10)), Is.True);
            track.Reset();
            Assert.That(SpinWait.SpinUntil(() => !track.IsRunning && track.CurrentTime < 1, TimeSpan.FromSeconds(5)), Is.True);
            TestContext.WriteLine($"Selected ranked i++: 48 s, >2 track wraps, peak {20 * Math.Log10(maximum):F2} dBFS; offline only.");
        }

        [TestCase("Gameplay/")]
        [TestCase("Gameplay/Argon/")]
        [TestCase("Gameplay/ArgonPro/")]
        [TestCase("Skins/Legacy/")]
        [TestCase("TaikoResource/Gameplay/")]
        public void TestBalancedDenseHitsThroughOfflineProductionMixer(string bank)
        {
            using var legacyResources = new NamespacedResourceStore<byte[]>(new NamespacedResourceStore<byte[]>(
                new DllResourceStore(typeof(Morasooma.Resources.ResourceAssembly).Assembly), "SoundOverrides"), "Skins/Legacy");
            using var legacySamples = audio.GetSampleStore(legacyResources);
            using var taikoResources = new osu.Game.Rulesets.Taiko.TaikoRuleset().CreateResourceStore();
            using var taikoSamples = audio.GetSampleStore(new NamespacedResourceStore<byte[]>(taikoResources, "Samples"));
            legacySamples.PlaybackConcurrency = taikoSamples.PlaybackConcurrency = OsuGameBase.SAMPLE_CONCURRENCY;
            // Playback queues belong to individual channels, not the mixer-read queue.
            // A barrier on that same queue proves Play/Volume/Looping were applied before
            // each offline read. This never opens a physical audio output device.
            var enqueue = typeof(AudioComponent).GetMethod("EnqueueAction",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            using var independent = new DllResourceStore(typeof(Morasooma.Resources.ResourceAssembly).Assembly);
            var manifest = Newtonsoft.Json.Linq.JObject.Parse(System.Text.Encoding.UTF8.GetString(independent.Get("sound-manifest.json")));
            var intentionalSilence = ((Newtonsoft.Json.Linq.JArray)manifest["preserved_audio"]!)
                .Where(e => (string)e["reason"]! == "intentional_silence").Select(e => (string)e["event"]!).ToHashSet();
            foreach (string set in new[] { "normal", "soft", "drum" })
            {
                var samples = new Dictionary<string, Sample>();
                foreach (string suffix in new[] { "hitnormal", "hitclap", "hitfinish", "hitwhistle", "sliderslide", "sliderwhistle" })
                {
                    if (intentionalSilence.Contains(bank + set + "-" + suffix))
                        continue; // Preserve deliberately empty ArgonPro loops, never fill them.
                    Sample? sample = bank.StartsWith("Skins/", StringComparison.Ordinal) ? legacySamples.Get(set + "-" + suffix)
                        : bank.StartsWith("TaikoResource/", StringComparison.Ordinal) ? taikoSamples.Get("Gameplay/" + set + "-" + suffix)
                        : audio.Samples.Get(bank + set + "-" + suffix);
                    if (sample != null)
                    {
                        Assert.That(SpinWait.SpinUntil(() => sample.IsLoaded && sample.Length > 0, TimeSpan.FromSeconds(10)), Is.True);
                        samples[suffix] = sample;
                    }
                }
                if (!samples.ContainsKey("hitnormal"))
                    continue; // The isolated Taiko bank intentionally has no drum set.
                var channels = new List<SampleChannel>();
                var loops = new List<SampleChannel>();
                Assert.That(audio.FlushSampleMixerAsync().Wait(TimeSpan.FromSeconds(10)), Is.True);
                SampleChannel start(string suffix, bool looping = false)
                {
                    var channel = samples[suffix].GetChannel();
                    channels.Add(channel);
                    channel.ManualFree = true;
                    channel.Volume.Value = .6;
                    channel.Looping = looping;
                    channel.Play();
                    var barrier = (Task)enqueue.Invoke(channel, new object[] { (Action)(() => { }) })!;
                    Assert.That(barrier.Wait(TimeSpan.FromSeconds(10)), Is.True);
                    return channel;
                }
                foreach (string suffix in new[] { "sliderslide", "sliderwhistle" })
                {
                    if (samples.ContainsKey(suffix))
                        loops.Add(start(suffix, true));
                }
                const int mixer_rate = 44100;
                int position = 0;
                double maximum = 0;
                double energy = 0;
                float[] pcm = new float[4410 * 2];
                void readUntil(int end)
                {
                    while (position < end)
                    {
                        int frames = Math.Min(4410, end - position);
                        Array.Clear(pcm);
                        var capture = audio.ReadSampleMixerDataAsync(pcm, frames);
                        Assert.That(capture.Wait(TimeSpan.FromSeconds(10)), Is.True);
                        Assert.That(capture.GetAwaiter().GetResult(), Is.EqualTo(frames * 2 * sizeof(float)));
                        for (int i = 0; i < frames; i++)
                        {
                            double t = (position + i) / (double)mixer_rate;
                            // Independently authored diagnostic bed, never installed music.
                            double bed = .025 * (Math.Sin(2 * Math.PI * 174.61 * t)
                                                + Math.Sin(2 * Math.PI * 220 * t) + Math.Sin(2 * Math.PI * 261.63 * t));
                            for (int c = 0; c < 2; c++)
                            {
                                float value = pcm[i * 2 + c];
                                if (!float.IsFinite(value)) Assert.Fail("Non-finite production mixer PCM");
                                energy += value * value;
                                maximum = Math.Max(maximum, Math.Abs(value + bed));
                            }
                        }
                        position += frames;
                    }
                }
                try
                {
                    for (int i = 0; i < 64; i++)
                    {
                        start("hitnormal");
                        if (i % 4 == 0 && samples.ContainsKey("hitclap")) start("hitclap");
                        if (i % 16 == 0 && samples.ContainsKey("hitfinish")) start("hitfinish");
                        if (i % 8 == 0 && samples.ContainsKey("hitwhistle")) start("hitwhistle");
                        readUntil((int)Math.Round((i + 1) * mixer_rate / 16.0)); // 240 BPM sixteenths.
                    }
                    foreach (var loop in loops)
                    {
                        loop.Stop();
                        var barrier = (Task)enqueue.Invoke(loop, new object[] { (Action)(() => { }) })!;
                        Assert.That(barrier.Wait(TimeSpan.FromSeconds(10)), Is.True);
                    }
                    readUntil(mixer_rate * 6);
                    Assert.That(energy, Is.GreaterThan(0.01), bank + set);
                    Assert.That(maximum, Is.LessThan(.98), bank + set + " at 60% note volume with diagnostic bed");
                    TestContext.WriteLine($"{bank}{set}: peak {20 * Math.Log10(maximum):F2} dBFS, 60% note / 100% effect+master, 240 BPM.");
                }
                finally
                {
                    foreach (var channel in channels)
                        channel.Dispose();
                    Assert.That(SpinWait.SpinUntil(() => channels.All(c => c.IsDisposed), TimeSpan.FromSeconds(10)), Is.True);
                }
            }
        }

        [Test]
        public void TestInstalledSoundsResolveThroughRealSkinsAndCustomSkinWins()
        {
            using var resources = new ResourceStore<byte[]>();
            resources.AddStore(new NamespacedResourceStore<byte[]>(new DllResourceStore(typeof(Morasooma.Resources.ResourceAssembly).Assembly), "SoundOverrides"));
            resources.AddStore(new NamespacedResourceStore<byte[]>(new DllResourceStore(typeof(OsuGameBase).Assembly), "Resources"));
            resources.AddStore(new DllResourceStore(typeof(osu.Game.Resources.OsuResources).Assembly));
            var provider = new SoundTestResourceProvider(audio, resources);
            using var classic = new DefaultLegacySkin(provider);
            using var morasooma = new MorasoomaSkin(provider);
            using var triangles = new TrianglesSkin(provider);
            using var argon = new ArgonSkin(provider);
            using var pro = new ArgonProSkin(provider);
            using var independent = new DllResourceStore(typeof(Morasooma.Resources.ResourceAssembly).Assembly);
            var rows = (Newtonsoft.Json.Linq.JArray)Newtonsoft.Json.Linq.JObject.Parse(
                System.Text.Encoding.UTF8.GetString(independent.Get("sound-manifest.json")))["sounds"]!;

            void check(Skin skin, ISampleInfo info, string expectedEvent, string expectedName)
            {
                var sample = skin.GetSample(info) as Sample;
                Assert.That(sample, Is.Not.Null, expectedEvent);
                Assert.That(sample, Is.Not.InstanceOf<InterfaceSoundSample>(), expectedEvent);
                Assert.That(SpinWait.SpinUntil(() => sample!.IsLoaded && sample.Length > 0, TimeSpan.FromSeconds(10)), Is.True, expectedEvent);
                var row = rows.Single(r => (string)r["event"]! == expectedEvent);
                Assert.That(sample!.Length, Is.EqualTo((double)row["frames"]! / (int)row["sample_rate"]! * 1000).Within(1), expectedEvent);
                using var channel = sample.GetChannel();
                Assert.That(channel.Name, Is.EqualTo(expectedName), expectedEvent);
            }

            check(classic, new SampleInfo("Gameplay/combobreak"), "Skins/Legacy/combobreak", "combobreak");
            check(classic, new SampleInfo("Gameplay/failsound"), "Skins/Legacy/failsound", "failsound");
            check(classic, new SampleInfo("sectionpass"), "Skins/Legacy/sectionpass", "sectionpass");
            check(classic, new SampleInfo("applause", "Results/applause-s"), "Skins/Legacy/applause", "applause");
            check(triangles, new SampleInfo("Gameplay/pause-loop"), "Gameplay/pause-loop", "Gameplay/pause-loop");
            check(argon, new SampleInfo("Gameplay/pause-loop"), "Gameplay/pause-loop", "Gameplay/pause-loop");
            check(pro, new SampleInfo("Gameplay/pause-loop"), "Gameplay/pause-loop", "Gameplay/pause-loop");
            check(classic, new HitSampleInfo("hitnormal"), "Skins/Legacy/normal-hitnormal", "normal-hitnormal");
            check(morasooma, new HitSampleInfo("hitnormal"), "Skins/Legacy/normal-hitnormal", "normal-hitnormal");
            check(morasooma, new SampleInfo("Gameplay/combobreak"), "Skins/Legacy/combobreak", "combobreak");
            check(morasooma, new SampleInfo("applause", "Results/applause-s"), "Skins/Legacy/applause", "applause");
            check(triangles, new SampleInfo("Gameplay/combobreak"), "Gameplay/combobreak", "Gameplay/combobreak");
            check(argon, new SampleInfo("Gameplay/combobreak"), "Gameplay/Argon/combobreak", "Gameplay/Argon/combobreak");
            check(pro, new HitSampleInfo("slidertick"), "Gameplay/ArgonPro/normal-slidertick", "Gameplay/ArgonPro/normal-slidertick");

            using var userFiles = new OneSoundStore("combobreak.wav", InterfaceSoundLibraryTest.wave());
            using var custom = new SoundTestLegacySkin(provider, userFiles);
            var personal = custom.GetSample(new SampleInfo("Gameplay/combobreak")) as Sample;
            Assert.That(personal, Is.Not.Null);
            Assert.That(SpinWait.SpinUntil(() => personal!.IsLoaded, TimeSpan.FromSeconds(10)), Is.True);
            Assert.That(personal!.Length, Is.EqualTo(1000).Within(1), "Personal skin recording must precede built-in defaults");
            using var userApplause = new OneSoundStore("applause.wav", InterfaceSoundLibraryTest.wave());
            using var applauseSkin = new SoundTestLegacySkin(provider, userApplause);
            var customApplause = applauseSkin.GetSample(new SampleInfo("applause", "Results/applause-s")) as Sample;
            Assert.That(customApplause, Is.Not.Null);
            Assert.That(SpinWait.SpinUntil(() => customApplause!.IsLoaded && customApplause.Length > 0, TimeSpan.FromSeconds(10)), Is.True);
            Assert.That(customApplause!.Length, Is.EqualTo(1000).Within(1), "A personal applause recording must win over the new default");
        }

        [Test]
        public void TestHistoricalRetroSilenceBankAndPersonalSkinStillWins()
        {
            using var resources = new ResourceStore<byte[]>();
            resources.AddStore(new NamespacedResourceStore<byte[]>(new DllResourceStore(typeof(Morasooma.Resources.ResourceAssembly).Assembly), "SoundOverrides"));
            resources.AddStore(new NamespacedResourceStore<byte[]>(new DllResourceStore(typeof(OsuGameBase).Assembly), "Resources"));
            resources.AddStore(new DllResourceStore(typeof(osu.Game.Resources.OsuResources).Assembly));
            var provider = new SoundTestResourceProvider(audio, resources);
            // The built-in Retro skin is removed; retain accepted historical pack bytes/compatibility.
            using var retro = new SoundTestLegacySkin(provider, new NamespacedResourceStore<byte[]>(resources, "Skins/Retro"));
            using var independent = new DllResourceStore(typeof(Morasooma.Resources.ResourceAssembly).Assembly);
            var rows = (Newtonsoft.Json.Linq.JArray)Newtonsoft.Json.Linq.JObject.Parse(
                Encoding.UTF8.GetString(independent.Get("sound-manifest.json")))["sounds"]!;
            foreach (string name in new[] { "count1s", "count2s", "count3s", "readys", "gos" })
            {
                var sample = retro.GetSample(new SampleInfo(name)) as Sample;
                Assert.That(sample, Is.Not.Null, name);
                Assert.That(sample, Is.Not.InstanceOf<InterfaceSoundSample>(), name);
                Assert.That(SpinWait.SpinUntil(() => sample!.IsLoaded && sample.Length > 0, TimeSpan.FromSeconds(10)), Is.True, name);
                var row = rows.Single(r => (string)r["event"]! == "Skins/Retro/" + name);
                Assert.That(sample!.Length, Is.EqualTo((double)row["frames"]! / 48).Within(1), name);
                assertSilentPcm(sample, name);

                using var personalFiles = new OneSoundStore(name + ".wav", InterfaceSoundLibraryTest.wave());
                using var personalSkin = new SoundTestLegacySkin(provider, personalFiles);
                var personal = personalSkin.GetSample(new SampleInfo(name)) as Sample;
                Assert.That(personal, Is.Not.Null, name);
                Assert.That(SpinWait.SpinUntil(() => personal!.IsLoaded && personal.Length > 0, TimeSpan.FromSeconds(10)), Is.True, name);
                Assert.That(personal!.Length, Is.EqualTo(1000).Within(1), name + " personal recording must not be silenced");
                using var channel = personal.GetChannel();
                channel.Play();
                float[] pcm = new float[4410 * 2];
                Assert.That(SpinWait.SpinUntil(() =>
                {
                    var capture = audio.ReadSampleMixerDataAsync(pcm, 4410);
                    Assert.That(capture.Wait(TimeSpan.FromSeconds(10)), Is.True, name);
                    Assert.That(capture.GetAwaiter().GetResult(), Is.EqualTo(pcm.Length * sizeof(float)), name);
                    return pcm.Any(v => Math.Abs(v) > .0001);
                }, TimeSpan.FromSeconds(5)), Is.True, name);
                channel.Stop();
            }
        }

        private void assertSilentPcm(Sample sample, string name)
        {
            using var channel = sample.GetChannel();
            channel.ManualFree = true;
            channel.Play();
            // Await the channel's own queue so zero output cannot pass merely
            // because mixer reads raced ahead of a failed/unstarted Play.
            var enqueue = typeof(AudioComponent).GetMethod("EnqueueAction",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var started = (Task)enqueue.Invoke(channel, new object[] { (Action)(() => { }) })!;
            Assert.That(started.Wait(TimeSpan.FromSeconds(10)), Is.True, name);
            Assert.That(channel.Playing, Is.True, name);
            const int rate = 44100;
            int total = (int)Math.Ceiling(sample.Length / 1000 * rate) + 4800;
            float[] pcm = new float[4800 * 2];
            for (int read = 0; read < total;)
            {
                int frames = Math.Min(4800, total - read);
                Array.Fill(pcm, float.NaN);
                var capture = audio.ReadSampleMixerDataAsync(pcm, frames);
                Assert.That(capture.Wait(TimeSpan.FromSeconds(10)), Is.True, name);
                Assert.That(capture.GetAwaiter().GetResult(), Is.EqualTo(frames * 2 * sizeof(float)), name);
                Assert.That(pcm.Take(frames * 2).All(v => float.IsFinite(v) && v == 0), Is.True, name);
                read += frames;
            }
            channel.Stop();
            var stopped = (Task)enqueue.Invoke(channel, new object[] { (Action)(() => { }) })!;
            Assert.That(stopped.Wait(TimeSpan.FromSeconds(10)), Is.True, name);
            Assert.That(channel.Playing, Is.False, name);
        }

        private sealed class SoundTestResourceProvider : IStorageResourceProvider
        {
            public IRenderer Renderer { get; } = new DummyRenderer();
            public AudioManager AudioManager { get; }
            public IResourceStore<byte[]> Files { get; } = new ResourceStore<byte[]>();
            public IResourceStore<byte[]> Resources { get; }
            public RealmAccess RealmAccess => null!;
            public IResourceStore<TextureUpload> CreateTextureLoaderStore(IResourceStore<byte[]> underlyingStore) => new ResourceStore<TextureUpload>();

            public SoundTestResourceProvider(AudioManager audio, IResourceStore<byte[]> resources)
            {
                AudioManager = audio;
                Resources = resources;
            }
        }

        private sealed class SoundTestLegacySkin : LegacySkin
        {
            public SoundTestLegacySkin(IStorageResourceProvider provider, IResourceStore<byte[]> fallback)
                : base(new SkinInfo(), provider, fallback)
            {
            }
        }

        private sealed class OneSoundStore : IResourceStore<byte[]>
        {
            private readonly string name;
            private readonly byte[] data;

            public OneSoundStore(string name, byte[] data)
            {
                this.name = name;
                this.data = data;
            }

            public byte[] Get(string key) => key == name ? data : null!;
            public Task<byte[]> GetAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(Get(key));
            public Stream? GetStream(string key) => key == name ? new MemoryStream(data, false) : null;
            public IEnumerable<string> GetAvailableResources() => new[] { name };
            public void Dispose()
            {
            }
        }

        [Test]
        public void TestRemovedBankIsNotEmbeddedAndOldSelectionUsesDefault()
        {
            using var dll = new DllResourceStore(typeof(OsuGame).Assembly);
            Assert.That(dll.GetAvailableResources().Any(p => p.Contains("InterfaceSounds/Lazer2017", StringComparison.Ordinal)), Is.False);
            var modern = audio.Samples.Get("UI/notification-friend-online");
            manager.Select("lazer-2017");
            Assert.That(manager.SelectedId, Is.EqualTo(InterfaceSoundLibrary.DefaultPack));
            Assert.That(manager.Ready.Wait(TimeSpan.FromSeconds(15)), Is.True);
            Assert.That(modern.GetChannel().Name, Is.EqualTo("UI/notification-friend-online"));
        }

        [Test]
        public void TestHistoricalCustomNoticesRemainOnMorafxReexport()
        {
            // Historical notices come from the imported pack, not the current client.
            byte[] oldLicence = Encoding.UTF8.GetBytes("Historical test fixture notice: CC BY-NC. Preserve verbatim.\n");
            // A diagnostic recording represents an old personal snapshot; this test
            // does not need to copy any upstream audio to prove notice retention.
            using var previous = new MemoryStream();
            manager.Library.Export(previous, InterfaceSoundLibrary.DefaultPack,
                key => (key + ".wav", InterfaceSoundLibraryTest.wave()), new[] { oldLicence });
            previous.Position = 0;
            var pack = manager.Library.Import(previous, "historical personal snapshot");
            manager.Select(pack.Id);
            Assert.That(manager.Ready.Wait(TimeSpan.FromSeconds(30)), Is.True);
            var export = manager.Export();
            Assert.That(export.Wait(TimeSpan.FromSeconds(30)), Is.True);
            using var stream = File.OpenRead(export.GetAwaiter().GetResult());
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            var notices = archive.Entries.Where(e => e.FullName.StartsWith("notices/", StringComparison.Ordinal)).Select(e =>
            {
                using var entry = e.Open();
                using var bytes = new MemoryStream();
                entry.CopyTo(bytes);
                return bytes.ToArray();
            }).ToArray();
            Assert.That(notices.Any(n => n.SequenceEqual(oldLicence)), Is.True, "Never drop an imported recording's original NC notice");
            Assert.That(string.Join("\n", notices.Select(Encoding.UTF8.GetString)), Does.Contain("CC0 1.0 Universal"));
            using var reader = new StreamReader(archive.GetEntry("pack.json")!.Open());
            var manifest = Newtonsoft.Json.Linq.JObject.Parse(reader.ReadToEnd());
            string filename = (string)manifest["Sounds"]!["Intro/Welcome/welcome_piano"]!;
            using var sound = archive.GetEntry(filename)!.Open();
            using var copy = new MemoryStream();
            sound.CopyTo(copy);
            Assert.That(copy.ToArray(), Is.EqualTo(InterfaceSoundLibraryTest.wave()), "Selected personal recording still wins over the new built-in piano");
        }

        [Test]
        public void TestFullDefaultMorafxRoundTripLoadsAllInterfaceEvents()
        {
            var export = manager.Export();
            Assert.That(export.Wait(TimeSpan.FromSeconds(30)), Is.True);
            string path = export.GetAwaiter().GetResult();
            Assert.That(path, Does.EndWith(".morafx"));
            using var input = File.OpenRead(path);
            using (var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true))
            {
                string notices = string.Join("\n", archive.Entries.Where(e => e.FullName.StartsWith("notices/", StringComparison.Ordinal)).Select(e =>
                {
                    using var stream = e.Open();
                    using var reader = new StreamReader(stream, Encoding.UTF8);
                    return reader.ReadToEnd();
                }));
                Assert.That(notices, Does.Contain("Morasooma Original Resources"));
                Assert.That(notices, Does.Contain("CC0 1.0 Universal"));
                Assert.That(notices, Does.Contain("Joseph SARDIN"));
                Assert.That(notices, Does.Contain("Sam Gossner"));
                Assert.That(notices, Does.Contain("8125"));
                using var packReader = new StreamReader(archive.GetEntry("pack.json")!.Open());
                var exported = Newtonsoft.Json.Linq.JObject.Parse(packReader.ReadToEnd());
                using var bundled = new DllResourceStore(typeof(Morasooma.Resources.ResourceAssembly).Assembly);
                var installed = Newtonsoft.Json.Linq.JObject.Parse(Encoding.UTF8.GetString(bundled.Get("sound-manifest.json")));
                var delivery = Newtonsoft.Json.Linq.JObject.Parse(Encoding.UTF8.GetString(bundled.Get("sound-delivery.json")));
                foreach (var row in ((Newtonsoft.Json.Linq.JArray)installed["sounds"]!).Where(e => (string)e["scope"]! == "interface"))
                {
                    string key = (string)row["event"]!;
                    string filename = (string)exported["Sounds"]![key]!;
                    using var stream = archive.GetEntry(filename)!.Open();
                    using var bytes = new MemoryStream();
                    stream.CopyTo(bytes);
                    Assert.That(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes.ToArray())).ToLowerInvariant(),
                        Is.EqualTo((string)delivery[key]!["sha256"]!), key + " must export the installed replacement, never upstream fallback");
                }
            }
            input.Position = 0;
            var imported = manager.Library.Import(input, "round-trip");
            Assert.That(imported.Sounds.Keys, Is.EquivalentTo(manager.Events));
            Assert.That(imported.Sounds.Count, Is.EqualTo(209));
            Assert.That(imported.Notices.Count, Is.GreaterThanOrEqualTo(8));
            manager.Select(imported.Id);
            Assert.That(manager.Ready.Wait(TimeSpan.FromSeconds(30)), Is.True);
            var check = audio.Samples.Get("UI/check-on");
            Assert.That(SpinWait.SpinUntil(() => check.GetChannel().Name.StartsWith("custom/", StringComparison.Ordinal), TimeSpan.FromSeconds(10)), Is.True);
        }
    }
}
