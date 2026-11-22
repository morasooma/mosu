using osu.Framework.Audio;
using osu.Framework.Localisation;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Difficulty
{
    /// <summary>Calculation-only clock rate, never offered to players or serialized.</summary>
    internal sealed class LocalDifficultyRateMod : Mod, IApplicableToRate
    {
        private readonly double clockRate;

        public LocalDifficultyRateMod(double clockRate) => this.clockRate = clockRate;

        public override string Name => "Local difficulty rate";
        public override string Acronym => string.Empty;
        public override LocalisableString Description => string.Empty;
        public double ApplyToRate(double time, double rate = 1) => rate * clockRate;
        public void ApplyToTrack(IAdjustableAudioComponent track) { }
        public void ApplyToSample(IAdjustableAudioComponent sample) { }
    }
}
