// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Dodge.Beatmaps;
using osu.Game.Rulesets.Dodge.Judgements;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Framework.Graphics;
using Newtonsoft.Json;

namespace osu.Game.Rulesets.Dodge.Objects
{
    public abstract class DodgeHitObject : HitObject, IHasTimePreempt
    {
        public const float DEFAULT_WAVE_AMPLITUDE = 32;
        public const int DEFAULT_WAVE_CYCLES = 2;

        public double TimePreempt { get; private set; } = DodgeBeatmapSettings.APPEARANCE_DURATION_MID;

        /// <summary>
        /// Per-object fill colour. For arena changes this is the arena background colour.
        /// </summary>
        [JsonIgnore]
        public Colour4 Colour { get; set; } = Colour4.White;

        [JsonIgnore]
        public Colour4 OutlineColour { get; set; } = Colour4.White;

        [JsonProperty("colour")]
        private string SerialisedColour
        {
            get => Colour.ToHex(true);
            set => Colour = parseColour(value);
        }

        [JsonProperty("outline_colour")]
        private string SerialisedOutlineColour
        {
            get => OutlineColour.ToHex(true);
            set => OutlineColour = parseColour(value);
        }

        public float Opacity { get; set; } = 1;

        public float OutlineThickness { get; set; }

        /// <summary>
        /// Movement and approach-guide properties used by projectile objects.
        /// Arena changes intentionally ignore these movement properties.
        /// </summary>
        public DodgeMovementType MovementType { get; set; }

        public float WaveAmplitude { get; set; } = DEFAULT_WAVE_AMPLITUDE;

        public int WaveCycles { get; set; } = DEFAULT_WAVE_CYCLES;

        public float WavePhase { get; set; }

        public DodgeTrajectoryGuideStyle TrajectoryGuideStyle { get; set; } = DodgeTrajectoryGuideStyle.Arrow;

        public override Judgement CreateJudgement() => new DodgeJudgement();

        protected override void ApplyDefaultsToSelf(ControlPointInfo controlPointInfo, IBeatmapDifficultyInfo difficulty)
        {
            base.ApplyDefaultsToSelf(controlPointInfo, difficulty);
            TimePreempt = DodgeBeatmapSettings.GetAppearanceDuration(difficulty);
        }

        private static Colour4 parseColour(string? value)
            => Colour4.TryParseHex(value ?? string.Empty, out Colour4 colour) ? colour : Colour4.White;
    }
}
