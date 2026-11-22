using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.IO;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu;

namespace osu.Game.Tests.Database
{
    [TestFixture]
    public class StableBeatmapProviderTest
    {
        [TestCase(20200101)]
        [TestCase(20250107)]
        public void TestCachedStatisticsAndPlayHistory(int version)
        {
            using var storage = new TemporaryNativeStorage($"stable-db-{Guid.NewGuid():N}");
            var played = new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
            WriteDatabase(storage.GetFullPath("osu!.db"), version, played);
            using var rulesets = new StableTestRulesetStore();
            var result = new StableBeatmapProvider(rulesets).GetBeatmaps(new StableStorage(storage.GetFullPath("."), null!));
            var maps = result.BeatmapSets.SelectMany(s => s.Beatmaps).ToArray();

            Assert.That(maps, Has.Length.EqualTo(2));
            Assert.Multiple(() =>
            {
                Assert.That(maps[0].BPM, Is.EqualTo(180).Within(0.001), "offset 12345 must never be used as milliseconds per beat");
                Assert.That(maps[1].BPM, Is.EqualTo(240).Within(0.001), "zero/negative offsets are valid");
                Assert.That(maps[0].TotalObjectCount, Is.EqualTo(17));
                Assert.That(maps[0].EndTimeObjectCount, Is.EqualTo(7));
                Assert.That(maps[1].TotalObjectCount, Is.EqualTo(40007), "object counters are unsigned 16-bit values");
                Assert.That(maps[0].LastPlayed, Is.EqualTo(played));
                Assert.That(maps[1].LastPlayed, Is.Null, "unplayed flag takes precedence over a cached timestamp");
                Assert.That(maps[0].StarRating, Is.EqualTo(4.5));
                Assert.That(maps[1].OnlineID, Is.EqualTo(2), "the next entry must remain aligned");
                Assert.That(result.BeatmapPaths, Has.Count.EqualTo(2));
            });
        }

        internal static void WriteDatabase(string path, int version, DateTimeOffset played)
        {
            using var writer = new BinaryWriter(File.Create(path));
            writer.Write(version);
            writer.Write(2); // folders
            writer.Write(true);
            writer.Write(0L);
            writeString(writer, "test");
            writer.Write(2); // beatmaps
            for (int i = 0; i < 2; i++)
            {
                foreach (string value in new[] { "Artist", "", $"Title {i}", "", "Mapper", "Hard", "audio.mp3", $"checksum-{i}", "map.osu" })
                    writeString(writer, value);
                writer.Write((byte)4);
                writer.Write((ushort)(i == 0 ? 10 : 40000));
                writer.Write((ushort)5);
                writer.Write((ushort)2);
                writer.Write(played.Ticks);
                for (int j = 0; j < 4; j++) writer.Write(5f);
                writer.Write(1.4d);
                for (int j = 0; j < 4; j++)
                {
                    writer.Write(1);
                    writer.Write((byte)8);
                    writer.Write(0);
                    writer.Write((byte)(version >= 20250107 ? 12 : 13));
                    if (version >= 20250107) writer.Write(4.5f);
                    else writer.Write(4.5d);
                }
                writer.Write(100);
                writer.Write(120000);
                writer.Write(1000);
                writer.Write(2); // timing points, beat length FIRST
                writer.Write(-100d);
                writer.Write(-1000d);
                writer.Write(false);
                writer.Write(60000d / (i == 0 ? 180 : 240));
                writer.Write(i == 0 ? 12345d : 0d);
                writer.Write(true);
                writer.Write(i + 1);
                writer.Write(i + 10);
                writer.Write(0);
                writer.Write(new byte[4]); // grades
                writer.Write((short)0);
                writer.Write(0.7f);
                writer.Write((byte)0);
                writeString(writer, "");
                writeString(writer, "");
                writer.Write((short)0);
                writeString(writer, "");
                writer.Write(i != 0); // unplayed
                writer.Write(played.Ticks);
                writer.Write(false);
                writeString(writer, $"folder-{i}");
                writer.Write(played.Ticks);
                writer.Write(new byte[5]);
                writer.Write(0);
                writer.Write((byte)0);
            }
            writer.Write(1); // permissions
        }

        private static void writeString(BinaryWriter writer, string text)
        {
            writer.Write((byte)11);
            writer.Write(text);
        }

        private sealed class StableTestRulesetStore : RulesetStore
        {
            public override IEnumerable<RulesetInfo> AvailableRulesets { get; } = new[] { new OsuRuleset().RulesetInfo };
        }
    }
}
