// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using MosuPpRxCs;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPpRelax
{
    /// <summary>
    /// MosuPp RX calculator: an exact, self-contained copy of the MosuPp development calculator (Realistik core +
    /// all MosuPp rules), used only when the Relax PP system is MosuPp. The "Mosu" system keeps using the Realistik
    /// calculator in Relax/Realistik; nothing here is shared with it.
    /// </summary>
    internal static class MosuPpRelaxCalculator
    {
        private static readonly ConcurrentDictionary<int, RxBeatmap> beatmaps = new ConcurrentDictionary<int, RxBeatmap>();

        /// <summary>
        /// MosuPp: speed PP / aim PP of an SS on the same beatmap + mods. The map-type decisions (aim-focused map, stream map,
        /// stream-only abuse) use these instead of the score's own values, so accuracy and misses cannot move a score across
        /// a rule threshold (a score with misses could otherwise get more PP than an SS).
        /// Cached per prepared <see cref="RxBeatmap"/> instance (a new one is created by every <see cref="Prepare"/>) and per
        /// mods / clock rate, so different beatmaps can never share an entry even when their hash is missing.
        /// </summary>
        private static readonly ConditionalWeakTable<RxBeatmap, ConcurrentDictionary<string, ReferenceRatios>> reference_ratios =
            new ConditionalWeakTable<RxBeatmap, ConcurrentDictionary<string, ReferenceRatios>>();

        // BeforeFlowGuard: ratio where the Aim-Focused Flow Guard runs; Final: after all aim/speed rules (Simple Stream Nerf, Stream-Only Guard).
        private readonly record struct ReferenceRatios(double BeforeFlowGuard, double Final, double StreamWeight, double SkillPp);

        /// <summary>
        /// MosuPp "RP Balance (RX)": respect points of the prepared beatmap (computed in <see cref="Prepare"/> from the playable beatmap,
        /// which has the stacking, slider paths and mods the RX representation does not keep).
        /// </summary>
        private static readonly ConditionalWeakTable<RxBeatmap, StrongBox<RxRespectPoints.Result>> respect_points =
            new ConditionalWeakTable<RxBeatmap, StrongBox<RxRespectPoints.Result>>();

        /// <summary>MosuPp "Aim Control (RX)": control PP of the prepared beatmap per clock rate.</summary>
        private static readonly ConditionalWeakTable<RxBeatmap, ConcurrentDictionary<double, double>> control_pp =
            new ConditionalWeakTable<RxBeatmap, ConcurrentDictionary<double, double>>();

        /// <summary>MosuPp "RP Balance (RX)": total PP of an SS on the same prepared beatmap + mods (the RP multiplier reference).</summary>
        private static readonly ConditionalWeakTable<RxBeatmap, ConcurrentDictionary<string, double>> ss_totals =
            new ConditionalWeakTable<RxBeatmap, ConcurrentDictionary<string, double>>();

        // CS Gain: RP and Arles of the playable beatmap rated at the reference CS (prepared with it), final SS PP there per mods.
        private readonly record struct CsReference(float Cs, RxRespectPoints.Result Rp, RxArles.Analysis Arles);

        private static readonly ConditionalWeakTable<RxBeatmap, StrongBox<CsReference>> cs_reference =
            new ConditionalWeakTable<RxBeatmap, StrongBox<CsReference>>();

        private static readonly ConditionalWeakTable<RxBeatmap, ConcurrentDictionary<string, double>> cs_reference_pp =
            new ConditionalWeakTable<RxBeatmap, ConcurrentDictionary<string, double>>();

        /// <summary>The map-level MosuPp multipliers of one map + mods (from an SS).</summary>
        private readonly record struct MapLevel(double ControlPp, double Control, double Rp, double Arles, double TypeScale)
        {
            public double Product => Control * Rp * Arles * TypeScale;
        }

        private static readonly ConditionalWeakTable<RxBeatmap, StrongBox<RxArles.Analysis>> arles =
            new ConditionalWeakTable<RxBeatmap, StrongBox<RxArles.Analysis>>();

        public static bool IsRelax(IEnumerable<Mod> mods) => RealistikRelaxBalance.IsRelax(mods);

        public static void Prepare(IBeatmap beatmap, Mod[] mods)
        {
            if (!IsRelax(mods))
                return;

            try
            {
                RxBeatmap converted = RealistikBeatmapConverter.Convert(beatmap);
                respect_points.AddOrUpdate(converted, new StrongBox<RxRespectPoints.Result>(RxRespectPoints.Calculate(beatmap, mods)));
                arles.AddOrUpdate(converted, new StrongBox<RxArles.Analysis>(RxArles.Analyze(beatmap)));

                float referenceCs = RxCsGain.ReferenceCs(beatmap.BeatmapInfo.Difficulty.CircleSize, mods.Any(m => m.Acronym == "HR"));
                if (beatmap.Difficulty.CircleSize > referenceCs + 1e-6)
                {
                    cs_reference.AddOrUpdate(converted, new StrongBox<CsReference>(new CsReference(referenceCs,
                        RxRespectPoints.Calculate(beatmap, mods, referenceCs), RxArles.Analyze(beatmap, referenceCs))));
                }
                beatmaps[createCacheKey(beatmap.BeatmapInfo, mods)] = converted;
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"Managed RX beatmap preparation failed for '{beatmap.BeatmapInfo.Hash}'.",
                    exception);
            }
        }

        public static OsuPerformanceAttributes CalculatePerformance(ScoreInfo score)
        {
            if (score.BeatmapInfo is null)
                throw new InvalidOperationException("Managed RX calculation requires BeatmapInfo.");
            if (!IsRelax(score.Mods))
                throw new InvalidOperationException("Managed RX calculator was called for a non-RX score.");

            int key = createCacheKey(score.BeatmapInfo, score.Mods);
            if (!beatmaps.TryGetValue(key, out RxBeatmap? beatmap))
            {
                throw new InvalidOperationException(
                    $"Managed RX beatmap was not prepared for '{score.BeatmapInfo.Hash}'. "
                    + "Run the matching difficulty calculation before performance calculation.");
            }

            try
            {
                OsuPerformanceAttributes attributes = calculate(beatmap, createScoreState(score), score.Mods);

                if (RelaxPpSystemSelection.Current != ForkRelaxPpSystem.MosuPp)
                    return attributes;

                double ssTotal = getSsTotal(beatmap, score.Mods);

                // MosuPp "Aim Control (RX)", "RP Balance (RX)" (with "Section Types (RX)"), "Arles (RX)" and the type scale: one
                // multiplier each per map + mods, from an SS (see mapLevel).
                respect_points.TryGetValue(beatmap, out StrongBox<RxRespectPoints.Result>? respectPoints);
                arles.TryGetValue(beatmap, out StrongBox<RxArles.Analysis>? shape);
                double clockRate = ModUtils.CalculateRateWithMods(score.Mods);
                MapLevel level = mapLevel(beatmap, clockRate, ssTotal, attributes.MosuPpStreamWeight ?? 0, respectPoints?.Value, shape?.Value);

                scale(attributes, level.Control);
                attributes.MosuPpBreakdown?.Add(new("control", level.Control));
                attributes.MosuPpControlPp = level.ControlPp;
                attributes.MosuPpControlMultiplier = level.Control;

                if (respectPoints != null)
                {
                    RxRespectPoints.Result rp = respectPoints.Value;
                    scale(attributes, level.Rp);
                    attributes.MosuPpBreakdown?.Add(new("rp", level.Rp));
                    attributes.MosuPpRespectPoints = rp.Value;
                    attributes.MosuPpRpType = rp.Type.ToString();
                    attributes.MosuPpRpRemainderType = rp.Type == RxRespectPoints.MapType.Stream ? rp.RemainderType.ToString() : null;
                    attributes.MosuPpComposition = rp.Composition;
                    attributes.MosuPpRespectPointsMultiplier = level.Rp;
                }

                if (shape != null)
                {
                    scale(attributes, level.Arles);
                    attributes.MosuPpBreakdown?.Add(new("arles", level.Arles));
                    attributes.MosuPpArlesConfidence = shape.Value.Confidence;
                    attributes.MosuPpArlesMultiplier = level.Arles;
                }

                scale(attributes, level.TypeScale);
                attributes.MosuPpBreakdown?.Add(new("typeScale", level.TypeScale));

                // MosuPp "CS Reduction (RX)": bigger circles than the map has (DA lower CS, EZ) — applied last, so nothing absorbs it.
                double csReduction = RxCsReduction.Multiplier(score.BeatmapInfo.Difficulty.CircleSize, beatmap.CircleSize, MosuPpBalance.Current.CsReductionStrength);
                scale(attributes, csReduction);
                attributes.MosuPpBreakdown?.Add(new("csReduction", csReduction));
                attributes.MosuPpCsReductionMultiplier = csReduction;

                // MosuPp "CS Gain (RX)": smaller circles than the map's own CS (at most CS 5) pay only on maps that are hard there.
                if (cs_reference.TryGetValue(beatmap, out StrongBox<CsReference>? csReference))
                {
                    double referencePp = getCsReferencePp(beatmap, score.Mods, clockRate, csReference.Value);
                    double csGain = RxCsGain.Multiplier(referencePp > 0 ? ssTotal * level.Product / referencePp : 1, referencePp);
                    scale(attributes, csGain);
                    attributes.MosuPpBreakdown?.Add(new("csGain", csGain));
                    attributes.MosuPpCsReferencePp = referencePp;
                }

                return attributes;
            }
            catch (Exception exception) when (exception is not InvalidOperationException)
            {
                throw new InvalidOperationException(
                    $"Managed RX PP calculation failed for '{score.BeatmapInfo.Hash}'.",
                    exception);
            }
        }

        private static void scale(OsuPerformanceAttributes attributes, double multiplier)
        {
            attributes.Aim *= multiplier;
            attributes.Speed *= multiplier;
            attributes.Accuracy *= multiplier;
            attributes.Reading *= multiplier;
            attributes.Total *= multiplier;
        }

        private static double getControlPp(RxBeatmap beatmap, double clockRate)
            => control_pp.GetValue(beatmap, _ => new ConcurrentDictionary<double, double>())
                         .GetOrAdd(clockRate, rate => RxAimControl.ControlPp(beatmap, rate));

        private static MapLevel mapLevel(RxBeatmap beatmap, double clockRate, double ssTotal, double streamWeight, RxRespectPoints.Result? respectPoints,
                                         RxArles.Analysis? shape)
        {
            // "Aim Control (RX)": fast direction changes at small / medium spacing join the total as a third skill.
            double controlPp = getControlPp(beatmap, clockRate) * MosuPpBalance.Current.ControlScale;
            double control = RxAimControl.Multiplier(controlPp, ssTotal);
            double rpMultiplier = 1, typeScale = 1;

            // "RP Balance (RX)": the total moves to the map's RP, within ±20%. "Section Types (RX)": a stream map is a stream map
            // only in its streams — RP and the type scale of the rest come from its type without streams, by the streams' PP share.
            if (respectPoints is RxRespectPoints.Result rp)
            {
                bool stream = rp.Type == RxRespectPoints.MapType.Stream;
                double rpValue = stream ? streamWeight * rp.Value + (1 - streamWeight) * rp.RemainderValue : rp.Value;
                rpMultiplier = Math.Pow(RxRespectPoints.Multiplier(rpValue, ssTotal * control), MosuPpBalance.Current.RpStrength);
                typeScale = stream
                    ? streamWeight * MosuPpBalance.Current.TypeScale(rp.Type) + (1 - streamWeight) * MosuPpBalance.Current.TypeScale(rp.RemainderType)
                    : MosuPpBalance.Current.TypeScale(rp.Type);
            }

            // "Arles (RX)" is applied after RP so its clamp cannot absorb the bonus.
            return new MapLevel(controlPp, control, rpMultiplier, shape?.Multiplier ?? 1, MosuPpBalance.Current.GlobalScale * typeScale);
        }

        /// <summary>CS Gain: final SS PP of the map + mods at the reference CS (before CS Reduction / CS Gain).</summary>
        private static double getCsReferencePp(RxBeatmap beatmap, IReadOnlyList<Mod> mods, double clockRate, CsReference reference)
        {
            var perBeatmap = cs_reference_pp.GetValue(beatmap, _ => new ConcurrentDictionary<string, double>());
            string key = string.Join(",", mods.Select(m => $"{m.Acronym}({string.Join(";", m.SettingDescription.Select(d => $"{d.setting}={d.value}"))})")
                                              .OrderBy(a => a, StringComparer.Ordinal));

            return perBeatmap.GetOrAdd(key, _ =>
            {
                RxBeatmap copy = withCircleSize(beatmap, reference.Cs);
                respect_points.AddOrUpdate(copy, new StrongBox<RxRespectPoints.Result>(reference.Rp));

                uint objects = (uint)copy.HitObjects.Count;
                var ss = new RxScoreState { Count300 = objects, PassedObjects = objects > 0 ? objects : null };
                OsuPerformanceAttributes ssAttributes = calculate(copy, ss, mods);
                return ssAttributes.Total * mapLevel(copy, clockRate, ssAttributes.Total, ssAttributes.MosuPpStreamWeight ?? 0, reference.Rp, reference.Arles).Product;
            });
        }

        private static RxBeatmap withCircleSize(RxBeatmap beatmap, float circleSize)
        {
            var copy = new RxBeatmap
            {
                Version = beatmap.Version,
                ApproachRate = beatmap.ApproachRate,
                CircleSize = circleSize,
                HpDrainRate = beatmap.HpDrainRate,
                OverallDifficulty = beatmap.OverallDifficulty,
                SliderMultiplier = beatmap.SliderMultiplier,
                SliderTickRate = beatmap.SliderTickRate,
            };
            copy.TimingPoints.AddRange(beatmap.TimingPoints);
            copy.DifficultyPoints.AddRange(beatmap.DifficultyPoints);
            copy.HitObjects.AddRange(beatmap.HitObjects);
            return copy;
        }

        private static double getSsTotal(RxBeatmap beatmap, IReadOnlyList<Mod> mods)
        {
            var perBeatmap = ss_totals.GetValue(beatmap, _ => new ConcurrentDictionary<string, double>());
            // Mod settings are part of the key (rate, AR/OD lock and DA values are settings, not acronyms).
            string modsKey = string.Join(",", mods.Select(m => $"{m.Acronym}({string.Join(";", m.SettingDescription.Select(d => $"{d.setting}={d.value}"))})")
                                                  .OrderBy(a => a, StringComparer.Ordinal));

            return perBeatmap.GetOrAdd(modsKey, _ =>
            {
                uint objects = (uint)beatmap.HitObjects.Count;
                var ss = new RxScoreState { Count300 = objects, PassedObjects = objects > 0 ? objects : null };
                return calculate(beatmap, ss, mods).Total;
            });
        }

        /// <summary>The MosuPp / MosuRealistik pipeline for one score state (everything except "RP Balance (RX)").</summary>
        private static OsuPerformanceAttributes calculate(RxBeatmap beatmap, RxScoreState scoreState, IEnumerable<Mod> scoreMods)
        {
            bool mosuPp = RelaxPpSystemSelection.Current == ForkRelaxPpSystem.MosuPp;

            // MosuPp "Traceable = Hidden (RX)": Traceable (TC) is calculated exactly like Hidden (HD) — reading bonus,
            // HD reading guard and every mod check below see HD. MosuRealistik keeps the original mods.
            Mod[] ppMods = mosuPp
                ? scoreMods.Select(m => m.Acronym == "TC" ? new OsuModHidden() : m).ToArray()
                : scoreMods.ToArray();

            RxMods performanceMods = getPerformanceMods(ppMods);
            double clockRate = ModUtils.CalculateRateWithMods(ppMods);

            // MosuPp "AR/OD Lock (RX)": a rate mod with "Lock AR/OD" is calculated with the AR / OD really played.
            bool lockedDifficulty = mosuPp && RxLockedDifficulty.IsLocked(ppMods);
            float effectiveAr = lockedDifficulty ? RxLockedDifficulty.Ar(beatmap.ApproachRate, clockRate) : beatmap.ApproachRate;
            float effectiveOd = lockedDifficulty ? RxLockedDifficulty.Od(beatmap.OverallDifficulty, clockRate) : beatmap.OverallDifficulty;

            RxNativePerformanceResult result = MosuRxCalculator.Calculate(new RxCalculationRequest
            {
                Beatmap = beatmap,
                Mods = performanceMods,
                Score = scoreState,
                EffectiveAr = effectiveAr,
                EffectiveOd = effectiveOd,
                EffectiveCs = beatmap.CircleSize,
                EffectiveHp = beatmap.HpDrainRate,
                ClockRate = clockRate,
            });

            double nativeMultiplier = calculateNativeMultiplier(result, scoreState);
            RealistikRelaxBalance.ApplyComponentGuards(
                result,
                ppMods,
                scoreState,
                beatmap,
                (float)result.Difficulty.Cs,
                clockRate);

            // Diagnostics for the site's "why did the PP change": every rule's multiplier in calculation order (1 = no effect).
            // Aim-scoped entries are ratios of the aim PP, the others multiply the whole total.
            var breakdown = new List<KeyValuePair<string, double>>();
            double aimBefore;

            double patternMultiplier = RealistikRelaxBalance.PatternMultiplier(ppMods, result);
            breakdown.Add(new("pattern", patternMultiplier));

            // Its parts, for the site only ("pattern" above is their product, clamped to 0.5–1).
            var patternParts = RealistikRelaxBalance.PatternBreakdown(ppMods, result);
            breakdown.Add(new("pattern.antiFarm", patternParts.AntiFarm));
            breakdown.Add(new("pattern.flowOverboost", patternParts.FlowOverboost));
            breakdown.Add(new("pattern.hrWideStream", patternParts.HrWideStream));

            // MosuPp "Spike Filler Guard (RX)": maps with several hard moments are not "one spike in filler" — only the anti-farm
            // part of the pattern multiplier is lifted.
            if (mosuPp && respect_points.TryGetValue(beatmap, out StrongBox<RxRespectPoints.Result>? moments))
            {
                double unguarded = patternMultiplier;
                patternMultiplier = RxSpikeFillerGuard.Apply(patternMultiplier, result.Difficulty.JumpSpikeFillerWeight, moments.Value.HardMoments);
                breakdown.Add(new("spikeFillerGuard", patternMultiplier / unguarded));
            }

            double lengthAimMultiplier = RealistikRelaxBalance.LengthAimMultiplier(ppMods, result);
            result.PpAim *= lengthAimMultiplier;
            breakdown.Add(new("aim:lengthAim", lengthAimMultiplier));

            ReferenceRatios reference = mosuPp
                ? getReferenceRatios(beatmap, performanceMods, ppMods, clockRate, effectiveAr, effectiveOd)
                : default;

            if (mosuPp)
            {
                // MosuPp "Aim-Focused Flow Guard (RX)": no flow bonus for aim-focused maps (flow comes from their spaced streams).
                aimBefore = result.PpAim;
                RxAimFocusedFlowGuard.Apply(result, reference.BeforeFlowGuard);
                breakdown.Add(new("aim:flowGuard", ratio(result.PpAim, aimBefore)));

                // MosuPp "Length Bonus (RX)" + "Spike Nerf (RX)": many hard moments are worth more than one short spike.
                aimBefore = result.PpAim;
                RxLengthBonus.Apply(result);
                breakdown.Add(new("aim:lengthBonus", ratio(result.PpAim, aimBefore)));

                // MosuPp "Point Variety Nerf (RX)": 2–4 spot back-and-forth spam loses most aim/speed PP.
                aimBefore = result.PpAim;
                RxPointVarietyNerf.Apply(result, beatmap);
                breakdown.Add(new("aim:pointVariety", ratio(result.PpAim, aimBefore)));

                // MosuPp "Extreme Jump Nerf (RX)": maps with 80+ jumps faster than 4.5 px/ms (glass beach) lose up to 31% aim PP.
                aimBefore = result.PpAim;
                RxExtremeJumpNerf.Apply(result, beatmap);
                breakdown.Add(new("aim:extremeJump", ratio(result.PpAim, aimBefore)));

                // MosuPp "Accuracy Gate (RX)": accuracy PP is paid by how hard the map is (aim + speed PP of an SS).
                double accuracyGate = RxAccuracyGate.Multiplier(reference.SkillPp);
                result.PpAccuracy *= accuracyGate;
                breakdown.Add(new("acc:accuracyGate", accuracyGate));
            }

            result.Pp = RealistikRelaxBalance.RebuildTotal(result, scoreState, nativeMultiplier);

            if (RealistikRelaxBalance.IsVerticalBalanceEligible(ppMods))
            {
                double verticalPressure = RealistikRelaxBalance.VerticalPressure(
                    beatmap,
                    (float)result.Difficulty.Cs,
                    scoreState.PassedObjects,
                    clockRate);
                if (verticalPressure > 0)
                {
                    result.PpAim *= 1 - 0.06 * verticalPressure;
                    result.Pp = RealistikRelaxBalance.RebuildTotal(result, scoreState, nativeMultiplier);
                }
            }

            double odMultiplier = RealistikRelaxBalance.OdMultiplier(result.Difficulty.Od);
            breakdown.Add(new("od", odMultiplier));

            // Product of the MosuPp total-PP rules below (1 for MosuRealistik).
            double mosuPpMultiplier = 1.0;

            if (mosuPp)
            {
                // MosuPp "Stream Map Nerf (RX)": stream maps (speed PP ≥ 1.0–1.3× aim PP) lose up to 35% of the PP their streams earn.
                mosuPpMultiplier *= record(breakdown, "streamMap", RxStreamMapNerf.Multiplier(reference.Final, reference.StreamWeight));

                // MosuPp "Stream-Only Guard (RX)": speed PP ≥ 5–10× aim PP (streams into one point): speed PP stops counting
                // (none from 10×) and the rest keeps ≤ 10% of the PP.
                mosuPpMultiplier *= record(breakdown, "streamOnly", RxStreamOnlyGuard.Apply(result, scoreState, nativeMultiplier, reference.Final));

                // MosuPp "One-Point Map Guard (RX)": every note stacked on 1–2 spots (e.g. Iyul' [HS]) keeps ~3% of the PP.
                mosuPpMultiplier *= record(breakdown, "onePoint", RxOnePointMapGuard.Multiplier(beatmap));

                // MosuPp "Short High CS Nerf (RX)": CS 5–6+ maps with 300–700 objects lose up to 15% (smooth in CS and length).
                mosuPpMultiplier *= record(breakdown, "shortHighCs", RxShortHighCsNerf.Multiplier(beatmap));

                // MosuPp "Visible Notes Reading (RX)": more spaced notes on screen at once (lower AR, higher BPM) → up to +20%.
                mosuPpMultiplier *= record(breakdown, "visibleNotes", RxVisibleNotesReading.Multiplier(beatmap, effectiveAr));

                // MosuPp "CS PP Buff (RX)": CS 4 +5%, 5 +10%, 6 +15%, 7 +25%, 8 +50% ... (smooth, CS incl. HR/EZ/DA).
                mosuPpMultiplier *= record(breakdown, "csBuff", RxCsPpBuff.Multiplier(beatmap.CircleSize));

                // MosuPp "Heavy Miss Penalty (RX)": every miss above 15 removes another ~10% (× e^(−0.11 · (misses − 15))).
                mosuPpMultiplier *= record(breakdown, "heavyMiss", RxHeavyMissPenalty.Multiplier(scoreState.Misses));
            }

            return new OsuPerformanceAttributes
            {
                Aim = result.PpAim * odMultiplier * patternMultiplier * mosuPpMultiplier,
                Speed = result.PpSpeed * odMultiplier * mosuPpMultiplier,
                Accuracy = result.PpAccuracy * odMultiplier * mosuPpMultiplier,
                Reading = result.PpReading * odMultiplier * mosuPpMultiplier,
                Flashlight = 0,
                EffectiveMissCount = result.EffectiveMissCount,
                Total = result.Pp * odMultiplier * patternMultiplier * mosuPpMultiplier,
                MosuPpSpikeFillerWeight = mosuPp ? result.Difficulty.JumpSpikeFillerWeight : null,
                MosuPpBreakdown = mosuPp ? breakdown : null,
                MosuPpStreamWeight = mosuPp ? reference.StreamWeight : null,
            };
        }

        private static double ratio(double after, double before) => before > 0 ? after / before : 1;

        private static double record(List<KeyValuePair<string, double>> breakdown, string key, double multiplier)
        {
            breakdown.Add(new(key, multiplier));
            return multiplier;
        }

        /// <summary>
        /// Runs the same pipeline as <see cref="CalculatePerformance"/> for an SS (up to the MosuPp map-type rules) and returns
        /// its speed PP / aim PP ratios.
        /// </summary>
        private static ReferenceRatios getReferenceRatios(RxBeatmap beatmap, RxMods performanceMods, IEnumerable<Mod> mods, double clockRate,
                                                          float effectiveAr, float effectiveOd)
        {
            var perBeatmap = reference_ratios.GetValue(beatmap, _ => new ConcurrentDictionary<string, ReferenceRatios>());
            // AR / OD are part of the key: "Lock AR/OD" is a mod setting, not an acronym.
            string modsKey = $"{(uint)performanceMods}|{clockRate:R}|{effectiveAr:R}|{effectiveOd:R}|"
                             + string.Join(",", mods.Select(m => m.Acronym).OrderBy(a => a, StringComparer.Ordinal));
            return perBeatmap.GetOrAdd(modsKey, _ => calculateReferenceRatios(beatmap, performanceMods, mods, clockRate, effectiveAr, effectiveOd));
        }

        private static ReferenceRatios calculateReferenceRatios(RxBeatmap beatmap, RxMods performanceMods, IEnumerable<Mod> mods, double clockRate,
                                                                float effectiveAr, float effectiveOd)
        {
            uint objects = (uint)beatmap.HitObjects.Count;
            var ss = new RxScoreState { Count300 = objects, PassedObjects = objects > 0 ? objects : null };

            RxNativePerformanceResult result = MosuRxCalculator.Calculate(new RxCalculationRequest
            {
                Beatmap = beatmap,
                Mods = performanceMods,
                Score = ss,
                EffectiveAr = effectiveAr,
                EffectiveOd = effectiveOd,
                EffectiveCs = beatmap.CircleSize,
                EffectiveHp = beatmap.HpDrainRate,
                ClockRate = clockRate,
            });

            RealistikRelaxBalance.ApplyComponentGuards(result, mods, ss, beatmap, (float)result.Difficulty.Cs, clockRate);
            result.PpAim *= RealistikRelaxBalance.LengthAimMultiplier(mods, result);

            double beforeFlowGuard = RxStreamOnlyGuard.SpeedAimRatio(result);

            RxAimFocusedFlowGuard.Apply(result, beforeFlowGuard);
            RxLengthBonus.Apply(result);
            RxPointVarietyNerf.Apply(result, beatmap);
            RxExtremeJumpNerf.Apply(result, beatmap);

            if (RealistikRelaxBalance.IsVerticalBalanceEligible(mods))
            {
                double verticalPressure = RealistikRelaxBalance.VerticalPressure(beatmap, (float)result.Difficulty.Cs, ss.PassedObjects, clockRate);
                if (verticalPressure > 0)
                    result.PpAim *= 1 - 0.06 * verticalPressure;
            }

            return new ReferenceRatios(beforeFlowGuard, RxStreamOnlyGuard.SpeedAimRatio(result), RxSectionTypes.StreamShare(beatmap, result),
                result.PpAim + result.PpSpeed);
        }

        private static RxScoreState createScoreState(ScoreInfo score)
        {
            uint count300 = (uint)Math.Max(0, score.Statistics.GetValueOrDefault(HitResult.Great));
            uint count100 = (uint)Math.Max(0, score.Statistics.GetValueOrDefault(HitResult.Ok));
            uint count50 = (uint)Math.Max(0, score.Statistics.GetValueOrDefault(HitResult.Meh));
            uint misses = (uint)Math.Max(0, score.Statistics.GetValueOrDefault(HitResult.Miss));
            uint passedObjects = count300 + count100 + count50 + misses;

            return new RxScoreState
            {
                Count300 = count300,
                Count100 = count100,
                Count50 = count50,
                Misses = misses,
                MaxCombo = score.MaxCombo > 0 ? (uint)score.MaxCombo : null,
                PassedObjects = passedObjects > 0 ? passedObjects : null,
            };
        }

        private static RxMods getPerformanceMods(IEnumerable<Mod> mods)
        {
            RxMods value = RxMods.Relax;

            foreach (Mod mod in mods)
            {
                value |= mod.Acronym switch
                {
                    "NF" => RxMods.NoFail,
                    "TD" => RxMods.TouchDevice,
                    "HD" => RxMods.Hidden,
                    "EZ" => RxMods.Easy,
                    "FL" => RxMods.Flashlight,
                    "SO" => RxMods.SpunOut,
                    _ => RxMods.None,
                };
            }

            return value;
        }

        private static double calculateNativeMultiplier(RxNativePerformanceResult result, RxScoreState score)
        {
            double componentNorm = RealistikRelaxBalance.RebuildTotal(result, score, 1);
            if (componentNorm <= 0)
                return 1.09;

            // Preserves the exact native global multiplier, including SpunOut.
            return result.Pp / componentNorm;
        }

        private static int createCacheKey(IBeatmapInfo beatmapInfo, IEnumerable<Mod> mods)
        {
            var hash = new HashCode();
            hash.Add(beatmapInfo.Hash);

            foreach (Mod mod in mods.OrderBy(m => m.Acronym))
                hash.Add(mod);

            return hash.ToHashCode();
        }
    }
}
