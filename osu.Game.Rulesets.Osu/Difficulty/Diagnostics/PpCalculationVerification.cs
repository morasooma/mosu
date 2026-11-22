using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Framework.Logging;

namespace osu.Game.Rulesets.Osu.Difficulty.Diagnostics
{
    internal static class PpCalculationVerification
    {
        private static readonly PropertyInfo[] difficultyProperties = numericProperties(typeof(OsuDifficultyAttributes));
        private static readonly PropertyInfo[] performanceProperties = numericProperties(typeof(OsuPerformanceAttributes));

        public static string Run(IRulesetInfo ruleset, IWorkingBeatmap working, Action<string, float> progress, CancellationToken token)
        {
            string[] names = { "NM", "HD", "HR", "DT", "HT", "HD+FL", "HD+HR+DT" };
            var report = new StringBuilder("Проверка агрегации сложности и ПП\n");
            report.AppendLine("Карта: " + working.BeatmapInfo);
            report.AppendLine("Старый = агрегация до оптимизации; подготовка и evaluators общие. Не проверка RX.");
            report.AppendLine("CalculateTimed + PP каждого префикса (SS, 100-ки, промахи/срыв комбо). 3 парных прохода после прогрева; декодирование вне замера.");
            for (int variant = 0; variant < names.Length; variant++)
            {
                token.ThrowIfCancellationRequested();
                int stageIndex = -1;
                void reportStage(string text)
                {
                    stageIndex++;
                    Logger.Log("Проверка ПП: " + names[variant] + ": " + text);
                    progress(names[variant] + ": " + text, (variant + stageIndex / 14f) / names.Length);
                }
                reportStage("подготовка");
                Mod[] mods = variant switch
                {
                    1 => new Mod[] { new OsuModHidden() },
                    2 => new Mod[] { new OsuModHardRock() },
                    3 => new Mod[] { new OsuModDoubleTime() },
                    4 => new Mod[] { new OsuModHalfTime() },
                    5 => new Mod[] { new OsuModHidden(), new OsuModFlashlight() },
                    6 => new Mod[] { new OsuModHidden(), new OsuModHardRock(), new OsuModDoubleTime() },
                    _ => Array.Empty<Mod>()
                };
                var prepared = new PreparedWorkingBeatmap(working.GetPlayableBeatmap(ruleset, mods, token));
                if (prepared.Beatmap.HitObjects.Count > 10000)
                    throw new InvalidOperationException("Для проверки выбери карту до 10 000 объектов.");
                string currentStage = "";
                int prefixes = 0;
                var heartbeat = Stopwatch.StartNew();
                void prefixAdded()
                {
                    prefixes++;
                    if (heartbeat.ElapsedMilliseconds < 500)
                        return;
                    progress($"{names[variant]}: {currentStage}, объект {prefixes}/{prepared.Beatmap.HitObjects.Count}",
                        (variant + (stageIndex + 0.95f * prefixes / Math.Max(1, prepared.Beatmap.HitObjects.Count)) / 14f) / names.Length);
                    heartbeat.Restart();
                }
                DifficultyCalculator calculator(bool old, string stage)
                {
                    prefixes = 0;
                    currentStage = stage;
                    reportStage(stage);
                    return old ? new LegacyAggregationCalculator(ruleset, prepared, prefixAdded)
                        : new ProgressCalculator(ruleset, prepared, prefixAdded);
                }
                // Same workload and fresh state on both sides, outside persistent client caches.
                var warmOld = calculator(true, "прогрев старого алгоритма").CalculateTimed(mods, token);
                var warmNew = calculator(false, "прогрев нового алгоритма").CalculateTimed(mods, token);
                reportStage("сверка прогрева и ПП");
                compare(warmOld, warmNew, mods, token);
                reportStage("сверка нового live-пути по запросу");
                using (var cursor = new OsuDifficultyCalculator(ruleset, prepared).CreateProgressiveCalculation(mods, token))
                {
                    var requested = new List<TimedDifficultyAttributes>();
                    var selected = new List<TimedDifficultyAttributes>();
                    foreach (var entry in warmNew)
                    {
                        requested.Add(new TimedDifficultyAttributes(entry.Time, cursor!.GetAttributesAtTime(entry.Time, token)!));
                        // Match the existing HUD lookup even for overlapping object end times.
                        int index = warmNew.BinarySearch(new TimedDifficultyAttributes(entry.Time, null!));
                        if (index < 0)
                            index = ~index - 1;
                        selected.Add(new TimedDifficultyAttributes(entry.Time, warmNew[Math.Clamp(index, 0, warmNew.Count - 1)].Attributes));
                    }
                    compare(selected, requested, mods, token);
                }
                var oldTimes = new List<double>();
                var newTimes = new List<double>();
                for (int run = 0; run < 3; run++)
                {
                    string pass = $"проход {run + 1}/3";
                    List<TimedDifficultyAttributes> oldResult;
                    List<TimedDifficultyAttributes> newResult;
                    if (run % 2 == 0)
                    {
                        oldResult = measure(calculator(true, pass + ", старый алгоритм"), mods, token, oldTimes);
                        newResult = measure(calculator(false, pass + ", новый алгоритм"), mods, token, newTimes);
                    }
                    else
                    {
                        newResult = measure(calculator(false, pass + ", новый алгоритм"), mods, token, newTimes);
                        oldResult = measure(calculator(true, pass + ", старый алгоритм"), mods, token, oldTimes);
                    }
                    reportStage(pass + ", сверка difficulty и ПП");
                    compare(oldResult, newResult, mods, token);
                }
                oldTimes.Sort();
                newTimes.Sort();
                var firstResultTimes = new List<double>();
                if (warmNew.Count > 0)
                {
                    for (int run = 0; run < 3; run++)
                    {
                        var timer = Stopwatch.StartNew();
                        using var cursor = new OsuDifficultyCalculator(ruleset, prepared).CreateProgressiveCalculation(mods, token);
                        var first = (OsuDifficultyAttributes)cursor!.GetAttributesAtTime(warmNew[0].Time, token)!;
                        new OsuPerformanceCalculator().Calculate(createScore(first, mods, 0), first);
                        firstResultTimes.Add(timer.Elapsed.TotalMilliseconds);
                    }
                }
                firstResultTimes.Sort();
                string live = firstResultTimes.Count > 0 ? $" Первый live-результат: {firstResultTimes[1]:F2} мс (без очереди/UI)." : "";
                string result = $"{names[variant]}: {prepared.Beatmap.HitObjects.Count} объектов; {oldTimes[1]:F1} → {newTimes[1]:F1} мс; ×{oldTimes[1] / newTimes[1]:F2}; все {warmNew.Count} префиксов совпали." + live;
                report.AppendLine(result);
                Logger.Log("Проверка ПП: " + result);
            }
            return report.AppendLine("Числовые поля difficulty и PP совпали побитово. Реальные игровые FPS этим не измеряются.").ToString();
        }

        private static List<TimedDifficultyAttributes> measure(DifficultyCalculator calculator, Mod[] mods, CancellationToken token, List<double> times)
        {
            var timer = Stopwatch.StartNew();
            var result = calculator.CalculateTimed(mods, token);
            var performance = new OsuPerformanceCalculator();
            foreach (var entry in result)
            {
                token.ThrowIfCancellationRequested();
                for (int scenario = 0; scenario < 3; scenario++)
                    performance.Calculate(createScore((OsuDifficultyAttributes)entry.Attributes, mods, scenario), entry.Attributes);
            }
            times.Add(timer.Elapsed.TotalMilliseconds);
            return result;
        }

        private static void compare(List<TimedDifficultyAttributes> oldValues, List<TimedDifficultyAttributes> newValues, Mod[] mods, CancellationToken token)
        {
            if (oldValues.Count != newValues.Count)
                throw new InvalidOperationException("Разное количество префиксов.");
            var oldPerformance = new OsuPerformanceCalculator();
            var newPerformance = new OsuPerformanceCalculator();
            for (int i = 0; i < oldValues.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                if (BitConverter.DoubleToInt64Bits(oldValues[i].Time) != BitConverter.DoubleToInt64Bits(newValues[i].Time))
                    throw new InvalidOperationException($"Время префикса {i} отличается.");
                compareProperties(oldValues[i].Attributes, newValues[i].Attributes, difficultyProperties, i);
                for (int scenario = 0; scenario < 3; scenario++)
                {
                    var score = createScore((OsuDifficultyAttributes)oldValues[i].Attributes, mods, scenario);
                    compareProperties(oldPerformance.Calculate(score, oldValues[i].Attributes), newPerformance.Calculate(score, newValues[i].Attributes), performanceProperties, i);
                }
            }
        }

        private static void compareProperties(object oldValue, object newValue, PropertyInfo[] properties, int prefix)
        {
            foreach (var property in properties)
            {
                object? a = property.GetValue(oldValue);
                object? b = property.GetValue(newValue);
                bool equal = a is double x && b is double y ? BitConverter.DoubleToInt64Bits(x) == BitConverter.DoubleToInt64Bits(y) : EqualityComparer<object?>.Default.Equals(a, b);
                if (!equal)
                    throw new InvalidOperationException($"Префикс {prefix + 1}, {property.Name}: старый={a}, новый={b}");
            }
        }

        private static PropertyInfo[] numericProperties(Type type) => type.GetProperties().Where(p =>
            p.PropertyType == typeof(double) || p.PropertyType == typeof(int) || p.PropertyType == typeof(double?)).ToArray();

        private static ScoreInfo createScore(OsuDifficultyAttributes attributes, Mod[] mods, int scenario)
        {
            int count = attributes.HitCircleCount + attributes.SliderCount + attributes.SpinnerCount;
            int misses = scenario == 2 ? count / 50 : 0;
            int ok = scenario == 1 ? count / 33 : scenario == 2 ? count / 10 : 0;
            int great = count - misses - ok;
            return new ScoreInfo
            {
                Mods = mods,
                Accuracy = count == 0 ? 1 : (great * 6.0 + ok * 2.0) / (count * 6.0),
                MaxCombo = scenario == 2 ? attributes.MaxCombo / 2 : attributes.MaxCombo,
                TotalScore = scenario == 0 ? 1000000 : scenario == 1 ? 980000 : 900000,
                Statistics = new Dictionary<HitResult, int>
                {
                    [HitResult.Great] = great,
                    [HitResult.Ok] = ok,
                    [HitResult.Miss] = misses,
                    [HitResult.SliderTailHit] = scenario == 2 ? attributes.SliderCount * 9 / 10 : attributes.SliderCount
                }
            };
        }

        private sealed class ProgressCalculator : OsuDifficultyCalculator
        {
            private readonly Action prefixAdded;
            public ProgressCalculator(IRulesetInfo ruleset, IWorkingBeatmap working, Action prefixAdded)
                : base(ruleset, working) => this.prefixAdded = prefixAdded;

            protected override void OnTimedPrefixAdded(IBeatmap beatmap, osu.Game.Rulesets.Objects.HitObject addedObject)
            {
                base.OnTimedPrefixAdded(beatmap, addedObject);
                prefixAdded();
            }
        }

        private sealed class PreparedWorkingBeatmap : WorkingBeatmap
        {
            private readonly IBeatmap prepared;
            public PreparedWorkingBeatmap(IBeatmap prepared) : base(prepared.BeatmapInfo, null) => this.prepared = prepared;
            protected override IBeatmap GetBeatmap() => prepared;
            public override IBeatmap GetPlayableBeatmap(IRulesetInfo ruleset, IReadOnlyList<Mod> mods, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                return prepared;
            }
            public override osu.Framework.Graphics.Textures.Texture GetBackground() => throw new NotSupportedException();
            protected override osu.Framework.Audio.Track.Track GetBeatmapTrack() => throw new NotSupportedException();
            protected override osu.Game.Skinning.ISkin GetSkin() => throw new NotSupportedException();
            public override System.IO.Stream GetStream(string path) => throw new NotSupportedException();
        }
    }
}
