using Realms;

namespace osu.Game.Database
{
    [Explicit]
    public class ForkBackgroundClassificationData : RealmObject
    {
        [PrimaryKey]
        public string Key { get; set; } = string.Empty;

        public float Drawings { get; set; }
        public float Hentai { get; set; }
        public float Neutral { get; set; }
        public float Porn { get; set; }
        public float Sexy { get; set; }
    }
}
