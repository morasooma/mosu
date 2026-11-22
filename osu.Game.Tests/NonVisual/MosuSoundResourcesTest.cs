using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Morasooma.Resources;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.IO.Stores;
using osu.Game.Audio;
using osu.Game.Resources;
using osu.Game.Rulesets.Taiko;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class MosuSoundResourcesTest
    {
        [Test]
        public async Task TestAcceptedSoundsResolveAndSupersededOriginalsAreNotEmbedded()
        {
            using var independent = new DllResourceStore(typeof(ResourceAssembly).Assembly);
            var manifest = JObject.Parse(System.Text.Encoding.UTF8.GetString(independent.Get("sound-manifest.json")));
            var entries = (JArray)manifest["sounds"]!;
            var delivery = JObject.Parse(System.Text.Encoding.UTF8.GetString(independent.Get("sound-delivery.json")));
            Assert.That(entries.Count, Is.EqualTo(363));
            Assert.That(delivery.Properties().Select(p => p.Name), Is.EquivalentTo(entries.Select(e => (string)e["event"]!)));
            var preserved = (JArray)manifest["preserved_interface"]!;
            Assert.That(preserved.Count, Is.EqualTo(1));
            Assert.That(entries.Select(e => (string)e["event"]!).Distinct().Count(), Is.EqualTo(entries.Count));
            Assert.That(entries.Count(e => (string)e["scope"]! == "interface"), Is.EqualTo(208));
            Assert.That(entries.Count(e => (string)e["scope"]! == "gameplay"), Is.EqualTo(90));
            Assert.That(entries.Count(e => (string)e["scope"]! == "skin"), Is.EqualTo(52));
            Assert.That(entries.Count(e => (string)e["scope"]! == "voice"), Is.EqualTo(4));
            Assert.That(entries.Count(e => (string)e["scope"]! == "ruleset"), Is.EqualTo(8));
            Assert.That(entries.Count(e => (string)e["scope"]! == "music"), Is.EqualTo(1));
            Assert.That(entries.Count(e => (bool)e["third_party_recording"]!), Is.EqualTo(65));
            Assert.That(independent.GetAvailableResources().Any(p => p.StartsWith("SoundDesign/", StringComparison.Ordinal) || p.StartsWith("Sources/", StringComparison.Ordinal)), Is.False);

            using var resources = new ResourceStore<byte[]>();
            resources.AddStore(new NamespacedResourceStore<byte[]>(new DllResourceStore(typeof(ResourceAssembly).Assembly), "IconOverrides"));
            resources.AddStore(new NamespacedResourceStore<byte[]>(new DllResourceStore(typeof(ResourceAssembly).Assembly), "CompatibilityOverrides"));
            resources.AddStore(new NamespacedResourceStore<byte[]>(new DllResourceStore(typeof(ResourceAssembly).Assembly), "SoundOverrides"));
            resources.AddStore(new NamespacedResourceStore<byte[]>(new DllResourceStore(typeof(OsuGameBase).Assembly), "Resources"));
            resources.AddStore(new DllResourceStore(OsuResources.ResourceAssembly));
            using var taiko = new TaikoRuleset().CreateResourceStore();
            using var soundOverrides = new NamespacedResourceStore<byte[]>(new DllResourceStore(typeof(ResourceAssembly).Assembly), "SoundOverrides");
            Assert.That(soundOverrides.GetAvailableResources(),
                Is.EquivalentTo(delivery.Properties().Select(p => (string)p.Value["override_resource"]!)));
            Assert.That(resources.Get("Tracks/rankedplay_bgm.wav"), Is.Null);

            foreach (var entry in entries)
            {
                var delivered = delivery[(string)entry["event"]!]!;
                string key = (string)delivered["resource"]!;
                var source = (string)entry["store"]! == "taiko" ? taiko : resources;
                byte[] bytes = source.Get(key);
                Assert.That(bytes, Is.Not.Null, key);
                Assert.That(hash(bytes), Is.EqualTo((string)delivered["sha256"]!), key);
                if ((bool?)entry["intentional_silence"] == true)
                    Assert.That(hash(bytes), Is.EqualTo((string)entry["sha256"]!), key);
                else if ((string)entry["store"]! == "track")
                    Assert.That(bytes[..4], Is.EqualTo("OggS"u8.ToArray()), key);
                else
                    Assert.That(System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(34)), Is.EqualTo(16), key);
                Assert.That(await source.GetAsync(key), Is.EqualTo(bytes), key);
                using Stream stream = source.GetStream(key);
                using var copy = new MemoryStream();
                stream.CopyTo(copy);
                Assert.That(copy.ToArray(), Is.EqualTo(bytes), key);
                if ((string)entry["store"]! != "track")
                    Assert.That(InterfaceSoundLibrary.ValidateAudio(key, bytes), Is.GreaterThan(0), key);
                // Music has a separate streamed track store; the 30-second personal
                // interface-sample limit must not be imposed on ranked BGM.
            }

            var actualEvents = resources.GetAvailableResources()
                                        .Where(p => p.StartsWith("Samples/", StringComparison.Ordinal))
                                        .Where(p => Path.GetExtension(p) is ".wav" or ".mp3")
                                        .Select(p => p[8..^Path.GetExtension(p).Length])
                                        .Where(InterfaceSoundManager.IsInterfaceEvent).Distinct().OrderBy(p => p);
            Assert.That(actualEvents, Is.EqualTo(entries.Where(e => (string)e["scope"]! == "interface").Concat(preserved).Select(e => (string)e["event"]!).OrderBy(p => p)));
            var compatibility = JObject.Parse(System.Text.Encoding.UTF8.GetString(independent.Get("texture-manifest.json")));
            var emptyEvents = ((JArray)compatibility["intentional_silence"]!).ToDictionary(e => (string)e["resource"]!);
            foreach (var entry in preserved)
            {
                string key = (string)entry["resource"]!;
                Assert.That(soundOverrides.Get(key), Is.Null, key);
                Assert.That(hash(resources.Get(key)), Is.EqualTo((string)emptyEvents[key]["sha256"]!), key);
                Assert.That(InterfaceSoundLibrary.IsIntentionalSilence(key, resources.Get(key)), Is.True, key);
            }

            // Intentional zero-frame events remain present via independently
            // constructed headers; original recordings stay only in source history.
            foreach (var entry in (JArray)manifest["preserved_audio"]!)
            {
                string path = (string)entry["source_path"]!;
                if (path.Contains("/InterfaceSounds/Lazer2017/", StringComparison.Ordinal))
                {
                    Assert.That(resources.Get(path["osu-fork/osu.Game/Resources/".Length..]), Is.Null, path);
                    continue;
                }
                string prefix = path.StartsWith("osu-resources/", StringComparison.Ordinal)
                    ? "osu-resources/osu.Game.Resources/" : "osu-fork/osu.Game/Resources/";
                string key = path[prefix.Length..];
                Assert.That(soundOverrides.Get(key), Is.Null, key);
                Assert.That(hash(resources.Get(key)), Is.EqualTo((string)emptyEvents[key]["sha256"]!), key);
                Assert.That(InterfaceSoundLibrary.IsIntentionalSilence(key, resources.Get(key)), Is.True, key);
            }

            // Assert absence in every original assembly, not just replacement priority.
            using var upstream = new DllResourceStore(OsuResources.ResourceAssembly);
            using var client = new NamespacedResourceStore<byte[]>(new DllResourceStore(typeof(OsuGameBase).Assembly), "Resources");
            using var originalTaiko = new NamespacedResourceStore<byte[]>(new DllResourceStore(typeof(TaikoRuleset).Assembly), "Resources");
            var originals = (JArray)manifest["originals"]!;
            Assert.That(originals.Count, Is.EqualTo(408));
            foreach (var entry in originals)
            {
                string path = (string)entry["path"]!;
                if (path.Contains("/InterfaceSounds/Lazer2017/", StringComparison.Ordinal))
                {
                    Assert.That(client.Get(path["osu-fork/osu.Game/Resources/".Length..]), Is.Null, path);
                    continue;
                }
                IResourceStore<byte[]> source;
                string prefix;
                if (path.StartsWith("osu-resources/", StringComparison.Ordinal))
                {
                    source = upstream;
                    prefix = "osu-resources/osu.Game.Resources/";
                }
                else if (path.StartsWith("osu-fork/osu.Game.Rulesets.Taiko/", StringComparison.Ordinal))
                {
                    source = originalTaiko;
                    prefix = "osu-fork/osu.Game.Rulesets.Taiko/Resources/";
                }
                else
                {
                    source = client;
                    prefix = "osu-fork/osu.Game/Resources/";
                }
                Assert.That(source.Get(path[prefix.Length..]), Is.Null, "Superseded original must not remain embedded: " + path);
            }

            Assert.That(resources.Get("Samples/Gameplay/normal-hitnormal.wav"), Is.Not.EqualTo(taiko.Get("Samples/Gameplay/normal-hitnormal.wav")), "Taiko and generic sample namespaces must not collide");
            Assert.That(resources.Get("Skins/Legacy/combobreak.wav"), Is.Not.EqualTo(resources.Get("Samples/Gameplay/combobreak.wav")), "Legacy selection must not replace the separate modern combobreak");
        }

        [Test]
        public void TestBalancedBankLevelsAndProtectedSounds()
        {
            using var independent = new DllResourceStore(typeof(ResourceAssembly).Assembly);
            var manifest = JObject.Parse(System.Text.Encoding.UTF8.GetString(independent.Get("sound-manifest.json")));
            Assert.That((int)manifest["version"]!, Is.EqualTo(8));
            var entries = (JArray)manifest["sounds"]!;
            var protectedHashes = new System.Collections.Generic.Dictionary<string, string>
            {
                ["UI/button-hover"] = "c841b9cca8b28b80f425bcc0b4faae98f2819b7a523ddb58ff129ad5a0e84ba5",
                ["Menu/button-hover"] = "18fb6d51bc5c21e21e41efcc3c4714acb22ab38ad7d9071cf548af9433009d7f",
                ["UI/check-on"] = "2c7cfc70051265ec195503ca816ed67981a92238671cad25808d06fedc3b8d5b",
                ["UI/check-off"] = "c7b2ef699888da6fdb2f6eac58c280989e228d27b3786a88032fc91b3ebf934f",
            };
            int balanced = 0;
            int correctedSlides = 0;
            foreach (var entry in entries)
            {
                string key = (string)entry["event"]!;
                byte[] bytes = readMaster(entry);
                if (protectedHashes.TryGetValue(key, out string? expected))
                {
                    Assert.That(hash(bytes), Is.EqualTo(expected), key);
                    Assert.That(entry["balance"], Is.Null, key);
                    continue;
                }
                // Later selections, silence and the two redesigned ranked counters
                // are not outputs of the historical scalar-gain operation.
                if (entry["owner_selection"] != null || entry["ranked_counter_revision"] != null || (bool?)entry["intentional_silence"] == true)
                {
                    Assert.That(entry["balance"], Is.Null, key);
                    continue;
                }
                balanced++;
                var balance = entry["balance"]!;
                double gain = (double)balance["gain_db"]!;
                Assert.That(gain, Is.GreaterThanOrEqualTo(0.49), key);
                Assert.That((string)balance["processing"]!, Does.StartWith("Constant scalar gain only"), key);
                Assert.That((double)balance["after_4x_peak_dbfs"]!, Is.LessThanOrEqualTo(-2.499), key);

                // All rendered replacements use canonical PCM24 stereo/48k WAV.
                // Check measured output, not only self-reported manifest values.
                using var reader = new BinaryReader(new MemoryStream(bytes));
                Assert.That(new string(reader.ReadChars(4)), Is.EqualTo("RIFF"), key);
                reader.ReadInt32();
                Assert.That(new string(reader.ReadChars(4)), Is.EqualTo("WAVE"), key);
                Assert.That(new string(reader.ReadChars(4)), Is.EqualTo("fmt "), key);
                Assert.That(reader.ReadInt32(), Is.EqualTo(16), key);
                Assert.That(reader.ReadInt16(), Is.EqualTo(1), key);
                Assert.That(reader.ReadInt16(), Is.EqualTo(2), key);
                Assert.That(reader.ReadInt32(), Is.EqualTo(48000), key);
                reader.ReadInt32();
                Assert.That(reader.ReadInt16(), Is.EqualTo(6), key);
                Assert.That(reader.ReadInt16(), Is.EqualTo(24), key);
                Assert.That(new string(reader.ReadChars(4)), Is.EqualTo("data"), key);
                int size = reader.ReadInt32();
                Assert.That(size, Is.EqualTo((int)entry["frames"]! * 6), key);
                double peak = 0;
                double[] energy = new double[size / 6 + 1];
                for (int i = 0; i < size / 6; i++)
                {
                    double power = 0;
                    for (int channel = 0; channel < 2; channel++)
                    {
                        int value = reader.ReadByte() | reader.ReadByte() << 8 | reader.ReadByte() << 16;
                        double sample = ((value ^ 0x800000) - 0x800000) / 8388608.0;
                        peak = Math.Max(peak, Math.Abs(sample));
                        power += sample * sample / 2;
                    }
                    energy[i + 1] = energy[i] + power;
                }
                const int window = 1920;
                double maximum = energy.Length <= window ? energy[^1] : 0;
                for (int i = window; i < energy.Length; i++)
                    maximum = Math.Max(maximum, energy[i] - energy[i - window]);
                double level = 10 * Math.Log10(maximum / window);
                Assert.That(level, Is.EqualTo((double)entry["metrics"]!["max40_dbfs"]!).Within(0.001), key);
                Assert.That(level - (double)balance["before_max40_dbfs"]!, Is.EqualTo(gain).Within(0.001), key);
                Assert.That(20 * Math.Log10(peak), Is.EqualTo((double)entry["metrics"]!["peak_dbfs"]!).Within(0.001), key);
                Assert.That(peak, Is.LessThan(0.751), key);
                if (entry["corrected_from"] != null)
                {
                    correctedSlides++;
                    Assert.That(((JArray)entry["provenance"]!["layers"]!).Any(l => (string?)l["source"] == "bass_rub"), Is.False, key);
                }
            }
            Assert.That(balanced, Is.EqualTo(338));
            Assert.That(correctedSlides, Is.EqualTo(7));
            // Identical source aliases stay identical after gain balancing.
            foreach (var group in entries.Where(e => e["gain_balanced_from"] != null)
                                         .GroupBy(e => (string)e["gain_balanced_from"]!["sha256"]!))
                Assert.That(group.Select(e => (string)e["sha256"]!).Distinct().Count(), Is.EqualTo(1));
        }

        [TestCase("pseudo-score-counter", 132000, -33, "ff1b84e16210ef2db142345d26ec24ec91b09f584665315aa3ffbb91fd4559e4")]
        [TestCase("score-tick", 1644, -34, "422583923cacb1ec2995b58828f2a12c1bd23fed41fedbcf374fc8ba0ae5d747")]
        public void TestRankedCountersAreQuietNonTonalPcm(string name, int frames, double expectedLevel, string expectedHash)
        {
            using var independent = new DllResourceStore(typeof(ResourceAssembly).Assembly);
            var manifest = JObject.Parse(System.Text.Encoding.UTF8.GetString(independent.Get("sound-manifest.json")));
            var revised = ((JArray)manifest["sounds"]!).Where(e => e["ranked_counter_revision"] != null).ToArray();
            Assert.That(revised.Length, Is.EqualTo(2));
            var row = revised.Single(e => (string)e["event"]! == "Multiplayer/Matchmaking/Ranked/Results/" + name);
            Assert.That(row["balance"], Is.Null);
            Assert.That(row["gain_balanced_from"], Is.Null);
            Assert.That((bool)row["provenance"]!["oscillators"]!, Is.False);
            Assert.That((bool)row["provenance"]!["fm"]!, Is.False);
            Assert.That((bool)row["loop"]!, Is.EqualTo(name == "score-tick"));
            byte[] bytes = readMaster(row);
            Assert.That(hash(bytes), Is.EqualTo(expectedHash));
            using var reader = new BinaryReader(new MemoryStream(bytes));
            Assert.That(new string(reader.ReadChars(4)), Is.EqualTo("RIFF"));
            Assert.That(reader.ReadInt32(), Is.EqualTo(bytes.Length - 8));
            Assert.That(new string(reader.ReadChars(4)), Is.EqualTo("WAVE"));
            Assert.That(new string(reader.ReadChars(4)), Is.EqualTo("fmt "));
            Assert.That(reader.ReadInt32(), Is.EqualTo(16));
            Assert.That(reader.ReadInt16(), Is.EqualTo(1));
            Assert.That(reader.ReadInt16(), Is.EqualTo(2));
            Assert.That(reader.ReadInt32(), Is.EqualTo(48000));
            Assert.That(reader.ReadInt32(), Is.EqualTo(288000));
            Assert.That(reader.ReadInt16(), Is.EqualTo(6));
            Assert.That(reader.ReadInt16(), Is.EqualTo(24));
            Assert.That(new string(reader.ReadChars(4)), Is.EqualTo("data"));
            Assert.That(reader.ReadInt32(), Is.EqualTo(frames * 6));
            double peak = 0;
            double[] energy = new double[frames + 1];
            for (int i = 0; i < frames; i++)
            {
                double power = 0;
                for (int channel = 0; channel < 2; channel++)
                {
                    int value = reader.ReadByte() | reader.ReadByte() << 8 | reader.ReadByte() << 16;
                    double sample = ((value ^ 0x800000) - 0x800000) / 8388608.0;
                    if (i == 0 || i == frames - 1)
                        Assert.That(sample, Is.Zero, "Silent endpoints avoid a hard loop splice");
                    power += sample * sample / 2;
                    peak = Math.Max(peak, Math.Abs(sample));
                }
                energy[i + 1] = energy[i] + power;
            }
            const int window = 1920;
            double maximum = frames < window ? energy[^1] : 0;
            for (int i = window; i <= frames; i++)
                maximum = Math.Max(maximum, energy[i] - energy[i - window]);
            Assert.That(10 * Math.Log10(maximum / window), Is.EqualTo(expectedLevel).Within(.001));
            Assert.That(peak, Is.LessThan(.12));
            Assert.That(energy[^1], Is.GreaterThan(.001), "Quiet, not replaced with silence");
        }

        [Test]
        public void TestDeferredSelectionsAreExactAndSeparateFromGainOnlyBank()
        {
            using var independent = new DllResourceStore(typeof(ResourceAssembly).Assembly);
            var manifest = JObject.Parse(System.Text.Encoding.UTF8.GetString(independent.Get("sound-manifest.json")));
            var entries = (JArray)manifest["sounds"]!;
            var selected = entries.Where(e => e["owner_selection"] != null).ToArray();
            var expected = new System.Collections.Generic.Dictionary<string, string>
            {
                ["Gameplay/pause-loop"] = "9e0edc60b9d1ee1705df9257945f43996b6a1a77cbab0d4d732ed2031cba1096",
                ["Intro/Welcome/welcome_piano"] = "4dc10b2cacbebd3f0591aa3b99271faf595ff13ae6a57d83eadf848c828a3728",
                ["MedalSplash/medal-get"] = "c63283f9ca04ab4c2ad66f443dd9e1f1395f2d699476d6126d8d7539cd06d26e",
                ["Results/applause-d"] = "e26f177cabc6e815ac2266dc342bf0db0c271b0d042038dfd17b47b67fbe963e",
                ["Results/applause-c"] = "510177e03b30852acb6bce6ccd5812e2c69afd155daedbb392d1f4a42b51d0c3",
                ["Results/applause-b"] = "049462e50dc2539f1b482061c7f8dfa4d2d5c3e21535e669cf0e6cbb35d45008",
                ["Results/applause-a"] = "c4533b9b6089b08d86c6f1a94845ddef05a7ab84cfa08119d6bb94f553e505c2",
                ["Results/applause-s"] = "10d8b1adc8539f27e4ed510e96b2f4b20ac717f22d8994818c277c8289503930",
                ["Skins/Legacy/applause"] = "9ce45ed5e00f196b3f4ec1b92d9ea6a658942f4e70460dbbfbfc306165ecad47",
                ["Tracks/rankedplay_bgm"] = "ee74c197314e512e3539e78df99eeaaf2212437a66e1a5bc34d77a06afc8d8ee",
            };
            Assert.That(selected.Select(e => (string)e["event"]!), Is.EquivalentTo(expected.Keys));
            foreach (var entry in selected)
            {
                string key = (string)entry["event"]!;
                Assert.That(hash(readMaster(entry)), Is.EqualTo(expected[key]), key);
                Assert.That(entry["balance"], Is.Null, key);
                Assert.That((bool)entry["owner_selection"]!["installed_without_rerender_gain_or_dsp"]!, Is.True, key);
                Assert.That((bool)entry["third_party_recording"]!, Is.True, key);
                Assert.That(((JArray)entry["sources"]!).All(s => (string)s["licence"]! == "CC0-1.0"), Is.True, key);
            }
            var medal = selected.Single(e => (string)e["event"]! == "MedalSplash/medal-get");
            Assert.That((int)medal["provenance"]!["effects"]!["additional_effect_layers"]!, Is.Zero);
            var applause = selected.Single(e => (string)e["event"]! == "Results/applause-s");
            Assert.That((double)applause["provenance"]!["effects"]!["compressor"]!["ratio"]!, Is.EqualTo(4));
            var music = selected.Single(e => (string)e["event"]! == "Tracks/rankedplay_bgm");
            Assert.That((string)music["store"]!, Is.EqualTo("track"));
            Assert.That((int)music["frames"]!, Is.EqualTo(2304000));
            foreach (string name in new[] { "Deferred-CC0-LICENSE.txt", "VSCO-CC0-LICENSE.txt", "VSCO-README.md", "Deferred-NOTICE.txt" })
                Assert.That(independent.Get("Licenses/Sounds/" + name), Is.Not.Null, name);
            Assert.That(System.Text.Encoding.UTF8.GetString(independent.Get("Licenses/Sounds/Deferred-NOTICE.txt")),
                Does.Contain("Gichco").And.Contain("isaiah658").And.Contain("8125").And.Contain("Joseph SARDIN"));
        }

        [Test]
        public void TestNineBuiltInVoicesResolveToExplicitSilentPcm()
        {
            using var independent = new DllResourceStore(typeof(ResourceAssembly).Assembly);
            var manifest = JObject.Parse(System.Text.Encoding.UTF8.GetString(independent.Get("sound-manifest.json")));
            var silent = ((JArray)manifest["sounds"]!).Where(e => (bool?)e["intentional_silence"] == true).ToArray();
            string[] expected =
            {
                "Intro/welcome", "Intro/seeya", "Intro/Welcome/welcome", "Intro/Welcome/seeya",
                "Skins/Retro/count1s", "Skins/Retro/count2s", "Skins/Retro/count3s", "Skins/Retro/readys", "Skins/Retro/gos",
            };
            Assert.That(silent.Select(e => (string)e["event"]!), Is.EquivalentTo(expected));
            using var resources = new ResourceStore<byte[]>();
            resources.AddStore(new NamespacedResourceStore<byte[]>(new DllResourceStore(typeof(ResourceAssembly).Assembly), "SoundOverrides"));
            resources.AddStore(new NamespacedResourceStore<byte[]>(new DllResourceStore(typeof(OsuGameBase).Assembly), "Resources"));
            resources.AddStore(new DllResourceStore(OsuResources.ResourceAssembly));
            foreach (var row in silent)
            {
                string key = (string)row["event"]!;
                byte[] bytes = resources.Get((string)row["resource"]!);
                Assert.That(hash(bytes), Is.EqualTo((string)row["sha256"]!), key);
                Assert.That((bool)row["third_party_recording"]!, Is.False, key);
                Assert.That(row["balance"], Is.Null, key);
                Assert.That(row["owner_selection"], Is.Null, key);
                using var reader = new BinaryReader(new MemoryStream(bytes));
                Assert.That(new string(reader.ReadChars(4)), Is.EqualTo("RIFF"), key);
                Assert.That(reader.ReadInt32(), Is.EqualTo(bytes.Length - 8), key);
                Assert.That(new string(reader.ReadChars(4)), Is.EqualTo("WAVE"), key);
                Assert.That(new string(reader.ReadChars(4)), Is.EqualTo("fmt "), key);
                Assert.That(reader.ReadInt32(), Is.EqualTo(16), key);
                Assert.That(reader.ReadInt16(), Is.EqualTo(1), key);
                Assert.That(reader.ReadInt16(), Is.EqualTo(2), key);
                Assert.That(reader.ReadInt32(), Is.EqualTo(48000), key);
                Assert.That(reader.ReadInt32(), Is.EqualTo(288000), key);
                Assert.That(reader.ReadInt16(), Is.EqualTo(6), key);
                Assert.That(reader.ReadInt16(), Is.EqualTo(24), key);
                Assert.That(new string(reader.ReadChars(4)), Is.EqualTo("data"), key);
                int size = reader.ReadInt32();
                Assert.That(size, Is.EqualTo((int)row["frames"]! * 6), key);
                Assert.That(size, Is.GreaterThan(0), key);
                Assert.That(reader.ReadBytes(size).All(v => v == 0), Is.True, key);
                Assert.That(reader.BaseStream.Position, Is.EqualTo(reader.BaseStream.Length), key);
                double previousDuration = (double)row["provenance"]!["original_inventory"]!["duration"]!;
                Assert.That((double)row["frames"]! / (int)row["sample_rate"]!, Is.EqualTo(previousDuration).Within(.5 / 48000), key);
                Assert.That(InterfaceSoundManager.IsInterfaceEvent(key), Is.False, key);
            }
            var preserved = (JArray)manifest["preserved_audio"]!;
            Assert.That(preserved.Count, Is.EqualTo(45));
            Assert.That(preserved.Count(e => ((string)e["source_path"]!).Contains("/InterfaceSounds/Lazer2017/", StringComparison.Ordinal)), Is.EqualTo(38));
            Assert.That(preserved.Count(e => (string)e["reason"]! == "intentional_silence"), Is.EqualTo(7));
            Assert.That(preserved.Any(e => (string)e["status"]! == "voice_audition"
                                         && !((string)e["source_path"]!).Contains("/InterfaceSounds/Lazer2017/", StringComparison.Ordinal)), Is.False);
        }

        private static byte[] readMaster(JToken row)
        {
            string root = typeof(MosuSoundResourcesTest).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                                                        .Single(a => a.Key == "MosuSoundMasters").Value!;
            return File.ReadAllBytes(Path.Combine(root, (string)row["override_resource"]!));
        }

        private static string hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
