// Adapted from taziksfear’s MosuPP 2.0.0 (PR #7, 3d3a196ed6540a3099d246dd5c6f662366c28db8).

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Utils;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax.TazikPpRelax
{
    /// <summary>Private calculation snapshot in played milliseconds. Never modifies gameplay objects.</summary>
    internal static class RxPhysicalPlay
    {
        public static bool Enabled => RelaxPpSystemSelection.Current == ForkRelaxPpSystem.TazikPp
                                      && TazikPpBalance.Current.OwnerRules && TazikPpBalance.Current.UsePhysicalEquivalence;

        // Difficulty changes are already baked into the snapshot. Their labels must not select different PP rules.
        public static Mod[] Mods(IEnumerable<Mod> mods) => mods.Where(m => m is not IApplicableToRate && m.Acronym is not ("HR" or "EZ" or "DA")).ToArray();

        public static IBeatmap Create(IBeatmap source, Mod[] mods)
        {
            double rate = ModUtils.CalculateRateWithMods(mods);
            bool locked = RxLockedDifficulty.IsLocked(mods);
            var difficulty = source.Difficulty.Clone();
            if (!locked)
            {
                difficulty.ApproachRate = RxLockedDifficulty.OnScreenAr(difficulty.ApproachRate, rate);
                difficulty.OverallDifficulty = (float)((80 - (80 - 6 * difficulty.OverallDifficulty) / rate) / 6);
            }

            var output = new Beatmap<OsuHitObject> { BeatmapVersion = source.BeatmapVersion, StackLeniency = source.StackLeniency, BeatmapInfo = source.BeatmapInfo.Clone(), Difficulty = difficulty };
            output.BeatmapInfo.Difficulty = source.BeatmapInfo.Difficulty.Clone();
            output.ControlPointInfo = (ControlPointInfo)Activator.CreateInstance(source.ControlPointInfo.GetType())!;
            foreach (ControlPoint point in source.ControlPointInfo.AllControlPoints)
            {
                ControlPoint copy = point.DeepClone();
                if (copy is TimingControlPoint timing)
                    timing.BeatLength /= rate;
                output.ControlPointInfo.Add(point.Time / rate, copy);
            }

            foreach (OsuHitObject original in source.HitObjects.OfType<OsuHitObject>())
            {
                OsuHitObject copy = original switch
                {
                    Slider slider => new Slider { Path = slider.Path, RepeatCount = slider.RepeatCount,
                        SliderVelocityMultiplier = slider.SliderVelocityMultiplier, GenerateTicks = slider.GenerateTicks },
                    Spinner spinner => new Spinner { Duration = spinner.Duration / rate },
                    _ => new HitCircle(),
                };
                copy.StartTime = original.StartTime / rate;
                copy.Position = original.Position;
                copy.StackHeight = original.StackHeight;
                // Reflection has no intrinsic aim cost. Canonical orientation also keeps stack offsets identical to DA.
                if (mods.Any(m => m is OsuModHardRock))
                    OsuHitObjectGenerationUtils.ReflectVerticallyAlongPlayfield(copy);
                copy.ApplyDefaults(output.ControlPointInfo, difficulty);
                output.HitObjects.Add(copy);
            }
            OsuBeatmapProcessor.ApplyStacking(output);
            return output;
        }
    }
}
