using System;
using osu.Framework.Localisation;
using osu.Game.Localisation;

namespace osu.Game.Graphics.Backgrounds.AI
{
    public enum BackgroundFilterMode
    {
        [LocalisableDescription(typeof(AiFunctionsStrings), nameof(AiFunctionsStrings.Explicit))]
        Explicit,
        [LocalisableDescription(typeof(AiFunctionsStrings), nameof(AiFunctionsStrings.Suggestive))]
        Suggestive,
        [LocalisableDescription(typeof(AiFunctionsStrings), nameof(AiFunctionsStrings.Hentai))]
        Hentai,
    }

    /// <summary>
    /// Version-keyed probability envelope in fork.realm. Legacy MobileNet rows retain five categories.
    /// SwiftFormer uses [NSFL, NSFW, SFW, 0, 0]; Anime Rating uses [safe, r15, r18, 0, 0].
    /// All scores are retained so thresholds and categories can change without inference.
    /// </summary>
    public readonly record struct BackgroundClassification(float Drawings, float Hentai, float Neutral, float Porn, float Sexy)
    {
        public bool IsValid => valid(Drawings) && valid(Hentai) && valid(Neutral) && valid(Porn) && valid(Sexy)
                               && Math.Abs(Drawings + Hentai + Neutral + Porn + Sexy - 1) < 0.02f;

        private static bool valid(float value) => float.IsFinite(value) && value >= 0 && value <= 1;

    }
}
