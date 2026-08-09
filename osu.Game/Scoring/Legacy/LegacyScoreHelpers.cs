// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
using osu.Framework.Bindables;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Utils;
using SharpCompress.Compressors.LZMA;

namespace osu.Game.Scoring.Legacy
{
    internal static class LegacyScoreDataCompression
    {
        public static byte[] Compress(string data)
        {
            byte[] content = new ASCIIEncoding().GetBytes(data);

            using (var outStream = new MemoryStream())
            {
                using (var lzma = LzmaStream.Create(new LzmaEncoderProperties(false, 1 << 21, 255), false, outStream))
                {
                    outStream.Write(lzma.Properties);

                    long fileSize = content.Length;
                    for (int i = 0; i < 8; i++)
                        outStream.WriteByte((byte)(fileSize >> (8 * i)));

                    lzma.Write(content);
                }

                return outStream.ToArray();
            }
        }
    }

    internal static class LegacyScoreExportModConverter
    {
        public static Mod[] GetExportMods(Ruleset ruleset, IBeatmap? beatmap, IEnumerable<Mod> mods, out bool convertedClientMods)
        {
            var exportMods = new List<Mod>();
            convertedClientMods = false;

            foreach (var mod in ModUtils.FlattenMods(mods))
            {
                if (mod.Type == ModType.Mosu)
                {
                    convertedClientMods = true;

                    if (mod is IApplicableToRate)
                    {
                        if (!tryAddRateMod(ruleset, exportMods, mod))
                            exportMods.Add(mod.DeepClone());
                    }

                    continue;
                }

                exportMods.Add(mod.DeepClone());
            }

            applyDifficultyAdjustForLockedRateMods(ruleset, beatmap, exportMods);

            return exportMods.Distinct().ToArray();
        }

        private static void applyDifficultyAdjustForLockedRateMods(Ruleset ruleset, IBeatmap? beatmap, List<Mod> exportMods)
        {
            if (beatmap == null)
                return;

            if (!string.Equals(ruleset.ShortName, "osu", StringComparison.OrdinalIgnoreCase))
                return;

            var rateMods = exportMods.OfType<ModRateAdjust>().Where(m => m.LockDifficultyAdjust.Value).ToArray();

            if (rateMods.Length == 0)
                return;

            if (ruleset.CreateModFromAcronym("DA") is not Mod difficultyAdjust)
                return;

            var baseDifficulty = new BeatmapDifficulty(beatmap.Difficulty);

            foreach (var mod in exportMods.Where(m => m is not ModRateAdjust).OfType<IApplicableToDifficulty>())
                mod.ApplyToDifficulty(baseDifficulty);

            double rate = ModUtils.CalculateRateWithMods(exportMods);

            if (!double.IsFinite(rate) || rate <= 0)
                return;

            setDifficultySetting(difficultyAdjust, "approach_rate", computeExportApproachRate(baseDifficulty.ApproachRate, rate));
            setDifficultySetting(difficultyAdjust, "overall_difficulty", computeExportOverallDifficulty(baseDifficulty.OverallDifficulty, rate));

            var existingDifficultyAdjust = exportMods.FirstOrDefault(m => string.Equals(m.Acronym, "DA", StringComparison.OrdinalIgnoreCase));

            if (existingDifficultyAdjust != null)
            {
                setDifficultySetting(existingDifficultyAdjust, "approach_rate", getDifficultySetting(difficultyAdjust, "approach_rate"));
                setDifficultySetting(existingDifficultyAdjust, "overall_difficulty", getDifficultySetting(difficultyAdjust, "overall_difficulty"));
            }
            else
            {
                exportMods.Add(difficultyAdjust);
            }

            foreach (var rateMod in rateMods)
                rateMod.LockDifficultyAdjust.Value = false;

            static float? getDifficultySetting(Mod mod, string settingKey)
            {
                if (!mod.SettingsMap.TryGetValue(settingKey, out IBindable? bindable))
                    return null;

                return Convert.ToSingle(bindable.GetUnderlyingSettingValue());
            }

            static void setDifficultySetting(Mod mod, string settingKey, float? value)
            {
                if (value == null)
                    return;

                if (!mod.SettingsMap.TryGetValue(settingKey, out IBindable? bindable))
                    return;

                if (!bindable.TrySetExactNumericValue(value.Value))
                    BindableValueAccessor.SetValue(bindable, value.Value);
            }

            static float computeExportApproachRate(float baseApproachRate, double rate)
            {
                const double preempt_max = 1800;
                const double preempt_mid = 1200;
                const double preempt_min = 450;
                var osu_preempt_range = new DifficultyRange(preempt_max, preempt_mid, preempt_min);

                double targetPreempt = IBeatmapDifficultyInfo.DifficultyRangeInt(baseApproachRate, osu_preempt_range) * rate;
                return (float)IBeatmapDifficultyInfo.InverseDifficultyRange(targetPreempt, osu_preempt_range);
            }

            static float computeExportOverallDifficulty(float baseOverallDifficulty, double rate)
            {
                const double great_window_min = 80;
                const double great_window_mid = 50;
                const double great_window_max = 20;
                var great_range = new DifficultyRange(great_window_min, great_window_mid, great_window_max);

                const double ok_window_min = 140;
                const double ok_window_mid = 100;
                const double ok_window_max = 60;
                var ok_range = new DifficultyRange(ok_window_min, ok_window_mid, ok_window_max);

                const double meh_window_min = 200;
                const double meh_window_mid = 150;
                const double meh_window_max = 100;
                var meh_range = new DifficultyRange(meh_window_min, meh_window_mid, meh_window_max);

                double sourceGreatWindow = computeHitWindow(baseOverallDifficulty, great_range);
                double sourceOkWindow = computeHitWindow(baseOverallDifficulty, ok_range);
                double sourceMehWindow = computeHitWindow(baseOverallDifficulty, meh_range);

                double targetGreatWindow = sourceGreatWindow * rate;
                double targetOkWindow = sourceOkWindow * rate;
                double targetMehWindow = sourceMehWindow * rate;

                const double greatWeight = 1000;
                const double okWeight = 1;
                const double mehWeight = 1;

                double weightedCandidate =
                    (greatWeight * 6 * (79.5 - targetGreatWindow)
                     + okWeight * 8 * (139.5 - targetOkWindow)
                     + mehWeight * 10 * (199.5 - targetMehWindow))
                    / (greatWeight * 36 + okWeight * 64 + mehWeight * 100);

                double greatCandidate = (79.5 - targetGreatWindow) / 6;
                double okCandidate = (139.5 - targetOkWindow) / 8;
                double mehCandidate = (199.5 - targetMehWindow) / 10;

                double[] candidates = { weightedCandidate, greatCandidate, okCandidate, mehCandidate };
                double bestCandidate = candidates[0];
                double bestError = computeWindowError(bestCandidate);

                foreach (double candidate in candidates.Skip(1))
                {
                    double candidateError = computeWindowError(candidate);

                    if (candidateError < bestError)
                    {
                        bestCandidate = candidate;
                        bestError = candidateError;
                    }
                }

                return (float)bestCandidate;

                double computeHitWindow(double difficulty, DifficultyRange range)
                    => Math.Floor(IBeatmapDifficultyInfo.DifficultyRange(difficulty, range)) - 0.5;

                double computeWindowError(double difficulty)
                {
                    double greatError = computeHitWindow(difficulty, great_range) - targetGreatWindow;
                    double okError = computeHitWindow(difficulty, ok_range) - targetOkWindow;
                    double mehError = computeHitWindow(difficulty, meh_range) - targetMehWindow;

                    return greatWeight * greatError * greatError
                           + okWeight * okError * okError
                           + mehWeight * mehError * mehError;
                }
            }
        }

        private static bool tryAddRateMod(Ruleset ruleset, List<Mod> exportMods, Mod sourceMod)
        {
            if (!tryGetSpeedChange(sourceMod, out double speedChange))
                return false;

            if (Math.Abs(speedChange - 1) < 0.0001)
                return true;

            Mod? exportMod = ruleset.CreateModFromAcronym(speedChange >= 1 ? "DT" : "HT");
            if (exportMod == null)
                return false;

            exportMod.CopyCommonSettingsFrom(sourceMod);
            setSpeedChange(exportMod, speedChange);
            exportMods.Add(exportMod);
            return true;
        }

        private static bool tryGetSpeedChange(Mod sourceMod, out double speedChange)
        {
            if (sourceMod.SettingsMap.TryGetValue("speed_change", out var speedChangeBindable))
            {
                speedChange = Convert.ToDouble(speedChangeBindable.GetUnderlyingSettingValue());
                return true;
            }

            var property = sourceMod.GetType().GetProperty("SpeedChange", BindingFlags.Instance | BindingFlags.Public);
            if (property?.GetValue(sourceMod) is IBindable bindable)
            {
                speedChange = Convert.ToDouble(bindable.GetUnderlyingSettingValue());
                return true;
            }

            speedChange = 1;
            return false;
        }

        private static void setSpeedChange(Mod targetMod, double speedChange)
        {
            var property = targetMod.GetType().GetProperty("SpeedChange", BindingFlags.Instance | BindingFlags.Public);
            if (property?.GetValue(targetMod) is IBindable bindable)
                BindableValueAccessor.SetValue(bindable, speedChange);
        }
    }
}
