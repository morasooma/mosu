// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using MosuRxPureCs;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax.Realistik
{
    internal static class RealistikBeatmapConverter
    {
        public static RxBeatmap Convert(IBeatmap beatmap)
        {
            var output = new RxBeatmap
            {
                Version = beatmap.BeatmapVersion,
                ApproachRate = beatmap.Difficulty.ApproachRate,
                OverallDifficulty = beatmap.Difficulty.OverallDifficulty,
                CircleSize = beatmap.Difficulty.CircleSize,
                HpDrainRate = beatmap.Difficulty.DrainRate,
                SliderMultiplier = beatmap.Difficulty.SliderMultiplier,
                SliderTickRate = beatmap.Difficulty.SliderTickRate,
            };

            foreach (TimingControlPoint point in beatmap.ControlPointInfo.TimingPoints)
                output.TimingPoints.Add(new RxTimingPoint(point.Time, point.BeatLength));

            foreach (ControlPointGroup group in beatmap.ControlPointInfo.Groups)
            {
                foreach (DifficultyControlPoint point in group.ControlPoints.OfType<DifficultyControlPoint>())
                {
                    output.DifficultyPoints.Add(new RxDifficultyPoint(
                        group.Time,
                        point.SliderVelocity,
                        point.GenerateTicks));
                }
            }

            foreach (HitObject hitObject in beatmap.HitObjects)
            {
                if (hitObject is not OsuHitObject osuObject)
                    continue;

                var converted = new RxHitObject
                {
                    StartTime = osuObject.StartTime,
                    Position = new RxVec2(osuObject.X, osuObject.Y),
                    Kind = osuObject switch
                    {
                        HitCircle => RxHitObjectKind.Circle,
                        Slider => RxHitObjectKind.Slider,
                        Spinner => RxHitObjectKind.Spinner,
                        _ => RxHitObjectKind.Spinner,
                    },
                };

                if (osuObject is Slider slider)
                    converted.Slider = convertSlider(slider);

                output.HitObjects.Add(converted);
            }

            return output;
        }

        private static RxSliderData convertSlider(Slider slider)
        {
            var output = new RxSliderData
            {
                ExpectedDistance = slider.Path.ExpectedDistance.Value,
                Repeats = slider.RepeatCount,
            };

            foreach (PathControlPoint point in slider.Path.ControlPoints)
            {
                output.ControlPoints.Add(new RxPathControlPoint(
                    new RxVec2(point.Position.X, point.Position.Y),
                    point.Type.HasValue ? convertPathType(point.Type.Value.Type) : null));
            }

            return output;
        }

        private static RxPathType convertPathType(SplineType type)
            => type switch
            {
                SplineType.Catmull => RxPathType.Catmull,
                SplineType.BSpline => RxPathType.Bezier,
                SplineType.PerfectCurve => RxPathType.PerfectCurve,
                _ => RxPathType.Linear,
            };
    }
}
