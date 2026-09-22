using System;
using Realms;

namespace osu.Game.Database
{
    [Explicit]
    public class ForkSeasonalBackgroundData : RealmObject
    {
        [PrimaryKey]
        public string Hash { get; set; } = string.Empty;

        public byte[] Data { get; set; } = Array.Empty<byte>();

        public DateTimeOffset LastAccessed { get; set; }
    }
}
