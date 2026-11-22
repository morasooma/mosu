using osu.Game.Rulesets.Mods;
using osu.Framework.Bindables;
using osu.Game.Configuration;
using osu.Game.Localisation;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Dodge.Mods
{
    public class DodgeModGlitch : ModGlitch
    {
        [SettingSource(typeof(SegmentRateModsStrings), nameof(SegmentRateModsStrings.ConstantAppearance), nameof(SegmentRateModsStrings.ConstantAppearanceHint), -1)]
        public override BindableBool BakedTiming => base.BakedTiming;
        // Projectile lifetimes overlap for whole songs; they are not sustained musical notes.
        protected override bool PreserveObjectDuration(HitObject hitObject) => BakedTiming.Value;

        protected override void TransformObjectTiming(HitObject hitObject, double originalStart, double originalEnd, SegmentRatePlan plan)
        {
            if (hitObject is DodgeEmitter emitter)
            {
                double interval = emitter.EffectiveBurstInterval;
                double lastEmission = originalStart + emitter.EmissionDuration;
                emitter.Duration = plan.Map(originalEnd) - plan.Map(lastEmission);
                emitter.BurstBeatDivisor = 0;
                emitter.BurstInterval = plan.Map(originalStart + interval) - plan.Map(originalStart);
            }
        }
    }
}
