using System;
using Newtonsoft.Json;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.UI;
using osu.Game.Configuration;
using osu.Game.Localisation;

namespace osu.Game.Rulesets.Mods
{
    /// <summary>
    /// Applies a deterministic tempo plan, optionally baking it into the playable timeline.
    /// Only tempo is adjusted; original map/audio files are never rewritten.
    /// </summary>
    public abstract class ModSegmentRate : Mod, IApplicableToBeatmap, IApplicableToRate, IUpdatableByPlayfield, IApplicableToBeatmapProcessor
    {
        public sealed override ModType Type => ModType.Mosu;
        public sealed override bool Ranked => false;
        public sealed override bool ValidForFreestyleAsRequiredMod => true;
        public sealed override bool ValidForMultiplayerAsFreeMod => false;
        public override Type[] IncompatibleMods => new[] { typeof(IApplicableToRate) };

        [SettingSource(typeof(SegmentRateModsStrings), nameof(SegmentRateModsStrings.BakedTiming), nameof(SegmentRateModsStrings.BakedTimingHint), -1)]
        public virtual BindableBool BakedTiming { get; } = new BindableBool(true);

        [JsonIgnore]
        public SegmentRatePlan RatePlan { get; protected set; } = SegmentRatePlan.Empty;

        [JsonIgnore]
        internal ControlPointInfo? SourceControlPoints { get; private set; }

        [JsonIgnore]
        internal double SourceBeatLength { get; private set; }

        // Not a mod setting: never serialize frame-dependent state into replays/API mods.
        private readonly BindableDouble speed = new BindableDouble(1);
        private IAdjustableAudioComponent? rateTrack;

        private IBeatmapProcessor? beatmapProcessor;

        public void ApplyToBeatmapProcessor(IBeatmapProcessor processor) => beatmapProcessor = processor;

        public virtual void ApplyToBeatmap(IBeatmap beatmap)
        {
            RatePlan = SegmentRatePlan.Empty;
            SourceControlPoints = beatmap.ControlPointInfo.DeepClone();
            SourceBeatLength = beatmap.GetMostCommonBeatLength();
            speed.Value = 1;
        }

        public double ApplyToRate(double time, double rate = 1)
            => BakedTiming.Value ? rate : rate * RatePlan.RateAt(time);

        public void ApplyToTrack(IAdjustableAudioComponent track)
        {
            rateTrack?.RemoveAdjustment(AdjustableProperty.Tempo, speed);
            // Player supplies a SafeRateAdjustableAudioComponent. Keep it alive: it owns
            // the intermediate bindings; otherwise GC silently disconnects rate updates.
            rateTrack = track;
            speed.Value = 1;
            rateTrack.AddAdjustment(AdjustableProperty.Tempo, speed);
        }

        public void ApplyToSample(IAdjustableAudioComponent sample)
        {
            // Samples are not stretched: sample playback must not inherit a frequency change.
            // Some sample backends don't support independent tempo at all.
        }

        public void Update(Playfield playfield)
        {
            // In baked mode the track-clock adapter updates tempo using raw audio time.
            if (!BakedTiming.Value)
                UpdateRate(playfield.Clock.CurrentTime);
        }

        protected void ApplyTiming(IBeatmap beatmap)
        {
            if (BakedTiming.Value && RatePlan.Segments.Count > 0)
            {
                SegmentBeatmapTiming.Apply(beatmap, RatePlan, TransformObjectTiming);
                // Recalculate stacking/hyperdash using the original processor and its mod settings.
                beatmapProcessor?.PostProcess();
            }
        }

        protected virtual void TransformObjectTiming(HitObject hitObject, double originalStart, double originalEnd, SegmentRatePlan plan)
        {
        }

        internal void UpdateRate(double sourceTime) => speed.Value = RatePlan.RateAt(sourceTime);

        protected virtual bool IsNeutralObject(HitObject hitObject) => false;
        protected virtual bool PreserveObjectDuration(HitObject hitObject) => true;

        private protected SegmentRateGeometry CreateGeometry(IBeatmap beatmap) => new SegmentRateGeometry(beatmap, IsNeutralObject, PreserveObjectDuration);
    }
}
