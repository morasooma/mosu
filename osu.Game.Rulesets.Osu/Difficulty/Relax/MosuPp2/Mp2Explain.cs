// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPp2
{
    /// <summary>One part of the map (fixed span of map time) and what it earned.</summary>
    public sealed class Mp2Section
    {
        /// <summary>Start and end, map seconds (as in the editor).</summary>
        public double From { get; init; }

        public double To { get; init; }

        /// <summary>Share of the SS PP earned here.</summary>
        public double Share { get; init; }

        public double Pp { get; init; }

        /// <summary>Note type that earned most of it, and the skill.</summary>
        public string Type { get; init; } = "";

        public string Skill { get; init; } = "";
    }

    /// <summary>What a switchable part of the model gave (positive) or took (negative), PP of an SS.</summary>
    public sealed class Mp2Effect
    {
        public string Name { get; init; } = "";
        public string Title { get; init; } = "";
        public double Pp { get; init; }
    }

    /// <summary>Full explanation of an SS result: map type, where the PP comes from and what each rule changed.</summary>
    public sealed class Mp2Explanation
    {
        public double PpSs { get; init; }

        /// <summary>Owner-style map type (stream / slop / control / tech / reading / arles), sums to 1.</summary>
        public IReadOnlyDictionary<string, double> MapType { get; init; } = new Dictionary<string, double>();

        /// <summary>Share of the PP per note type (<see cref="Mp2Explain.TYPE_NAMES"/> keys + "track"), sums to 1.</summary>
        public IReadOnlyDictionary<string, double> TypeShares { get; init; } = new Dictionary<string, double>();

        /// <summary>Share of the notes per type (composition, not weighted by difficulty).</summary>
        public IReadOnlyDictionary<string, double> Composition { get; init; } = new Dictionary<string, double>();

        /// <summary>Map-level evidence behind the map type (slider share in the hard part, AR, Arles parts…).</summary>
        public IReadOnlyDictionary<string, double> Evidence { get; init; } = new Dictionary<string, double>();

        public IReadOnlyDictionary<string, double> SkillShares { get; init; } = new Dictionary<string, double>();

        /// <summary>Sections sorted by PP, hardest first.</summary>
        public IReadOnlyList<Mp2Section> Sections { get; init; } = Array.Empty<Mp2Section>();

        public IReadOnlyList<Mp2Effect> Effects { get; init; } = Array.Empty<Mp2Effect>();

        public double LevelPp { get; init; }
        public double HardSeconds { get; init; }

        /// <summary>The explanation as text, Russian (for players).</summary>
        public IReadOnlyList<string> Lines { get; init; } = Array.Empty<string>();
    }

    /// <summary>
    /// Explanation of MosuPp 2 (review/explanation.md): the exact Euler split of D over notes (<see cref="Mp2Trace.Contribution"/>),
    /// then notes → note types (classifier memberships) and skills (norm split), map sections, and remove-one counterfactuals for
    /// every rule that discounts or adds (what it gave / took, in PP).
    /// </summary>
    public static class Mp2Explain
    {
        /// <summary>Section length, map seconds.</summary>
        public const double SECTION_SECONDS = 5;

        public static readonly IReadOnlyDictionary<string, string> TYPE_NAMES = new Dictionary<string, string>
        {
            ["free"] = "стопки и ноты без движения",
            ["simple"] = "простые движения",
            ["stream"] = "стримы",
            ["spaced_stream"] = "разнесённые стримы",
            ["burst"] = "бёрсты",
            ["zigzag"] = "зигзаги",
            ["jitter"] = "джиттер",
            ["jump_bf"] = "джампы туда-обратно",
            ["jump_angular"] = "угловые джампы",
            ["jump_flow"] = "флоу-джампы",
            ["track"] = "слайдеры",
        };

        public static readonly IReadOnlyDictionary<string, string> MAP_TYPE_NAMES = new Dictionary<string, string>
        {
            ["stream"] = "стримы",
            ["slop"] = "аим-слоп",
            ["control"] = "аим-контроль",
            ["tech"] = "техно",
            ["reading"] = "чтение",
            ["arles"] = "арлес",
        };

        public static readonly IReadOnlyDictionary<string, string> SKILL_NAMES = new Dictionary<string, string>
        {
            ["aim"] = "прицел (долететь вовремя)",
            ["agility"] = "смена скорости",
            ["control"] = "контроль (резкие повороты)",
            ["track"] = "слайдеры",
        };

        private static readonly (Mp2Switch Switch, string Name, string Title)[] effects =
        {
            (Mp2Switch.NoReading, "reading", "чтение (AR, ноты на экране, HD / FL)"),
            (Mp2Switch.NoSize, "size", "размер круга (CS) на быстрых движениях"),
            (Mp2Switch.NoTypeScale, "type", "множители типов паттернов"),
            (Mp2Switch.NoAwkward, "awkward", "неудобные джампы и зигзаги"),
            (Mp2Switch.NoRepetition, "repetition", "скидка за повторяющиеся движения"),
            (Mp2Switch.NoHeadroom, "headroom", "скидка за запас времени на остановку"),
            (Mp2Switch.NoCap, "cap", "потолок нагрузки (аккорды, телепорты)"),
            (Mp2Switch.NoLength, "length", "длина тяжёлой части"),
            (Mp2Switch.NoStamina, "stamina", "выносливость: долгий контроль без отдыха"),
        };

        public static readonly string[] TYPE_KEYS =
            { "free", "simple", "stream", "spaced_stream", "burst", "zigzag", "jitter", "jump_bf", "jump_angular", "jump_flow" };

        /// <param name="noMod">Features of the same map in Relax only (easy-map mod gain cap); none = no cap.</param>
        public static Mp2Explanation Explain(Mp2Features f, Mp2Parameters p, Mp2Features? noMod = null)
        {
            Mp2Difficulty d = Mp2Model.Evaluate(f, p, trace: true, noMod: noMod);
            Mp2Trace? trace = d.Trace;
            int n = f.Count;
            if (trace == null || n == 0 || d.Difficulty <= 0)
                return new Mp2Explanation { Lines = new[] { "Карта пустая: PP нет." } };

            double total = trace.Contribution.Sum();
            var weight = trace.Contribution.Select(c => Math.Max(0, c)).ToArray();

            // notes → types: the movement part goes to the note's types, the tracking part to "track".
            var typePp = new double[Mp2NoteTypes.COUNT + 1];
            var composition = new double[Mp2NoteTypes.COUNT];
            for (int i = 0; i < n; i++)
            {
                double c = trace.Contribution[i];
                double trackShare = trace.Skill[i][3];
                typePp[Mp2NoteTypes.COUNT] += c * trackShare;
                for (int k = 0; k < Mp2NoteTypes.COUNT; k++)
                {
                    typePp[k] += c * (1 - trackShare) * trace.Types.Membership[i][k];
                    composition[k] += trace.Types.Membership[i][k] / n;
                }
            }

            var typeShares = new Dictionary<string, double>();
            for (int k = 0; k < Mp2NoteTypes.COUNT; k++)
                typeShares[TYPE_KEYS[k]] = typePp[k] / total;
            typeShares["track"] = typePp[Mp2NoteTypes.COUNT] / total;

            var evidence = new Dictionary<string, double>();
            var mapType = Mp2Classifier.MapType(f, trace.Types, weight, evidence);

            // sections of map time
            double start = f.MapTime[0];
            int count = (int)Math.Floor((f.MapTime[n - 1] - start) / SECTION_SECONDS) + 1;
            var sectionPp = new double[count];
            var sectionType = new double[count][];
            var sectionSkill = new double[count][];
            for (int s = 0; s < count; s++)
            {
                sectionType[s] = new double[Mp2NoteTypes.COUNT + 1];
                sectionSkill[s] = new double[Mp2Model.SKILLS.Length];
            }

            for (int i = 0; i < n; i++)
            {
                int s = Math.Min(count - 1, (int)((f.MapTime[i] - start) / SECTION_SECONDS));
                double c = trace.Contribution[i];
                sectionPp[s] += c;
                double trackShare = trace.Skill[i][3];
                sectionType[s][Mp2NoteTypes.COUNT] += c * trackShare;
                for (int k = 0; k < Mp2NoteTypes.COUNT; k++)
                    sectionType[s][k] += c * (1 - trackShare) * trace.Types.Membership[i][k];
                for (int k = 0; k < Mp2Model.SKILLS.Length; k++)
                    sectionSkill[s][k] += c * trace.Skill[i][k];
            }

            var sections = Enumerable.Range(0, count)
                                     .Where(s => sectionPp[s] > 0)
                                     .OrderByDescending(s => sectionPp[s])
                                     .Select(s => new Mp2Section
                                     {
                                         From = start + s * SECTION_SECONDS,
                                         To = start + (s + 1) * SECTION_SECONDS,
                                         Share = sectionPp[s] / total,
                                         Pp = d.PpSs * sectionPp[s] / total,
                                         Type = argMax(sectionType[s]) is int t && t < Mp2NoteTypes.COUNT ? TYPE_KEYS[t] : "track",
                                         Skill = Mp2Model.SKILLS[argMax(sectionSkill[s])],
                                     })
                                     .ToList();

            var effectList = effects.Select(e => new Mp2Effect
            {
                Name = e.Name,
                Title = e.Title,
                Pp = d.PpSs - Mp2Model.Evaluate(f, p, e.Switch, noMod: noMod).PpSs,
            }).ToList();

            if (d.ModCapLoss > 0)
                effectList.Add(new Mp2Effect { Name = "modcap", Title = "потолок модов на лёгкой карте", Pp = -d.ModCapLoss });

            var explanation = new Mp2Explanation
            {
                PpSs = d.PpSs,
                MapType = mapType,
                TypeShares = typeShares,
                Composition = Enumerable.Range(0, Mp2NoteTypes.COUNT).ToDictionary(k => TYPE_KEYS[k], k => composition[k]),
                Evidence = evidence,
                SkillShares = d.SkillShares,
                Sections = sections,
                Effects = effectList,
                LevelPp = Mp2Model.PpOfDifficulty(d.Level, p),
                HardSeconds = d.HardSeconds,
            };

            return new Mp2Explanation
            {
                PpSs = explanation.PpSs, MapType = explanation.MapType, TypeShares = explanation.TypeShares, Composition = explanation.Composition, Evidence = explanation.Evidence,
                SkillShares = explanation.SkillShares, Sections = explanation.Sections, Effects = explanation.Effects,
                LevelPp = explanation.LevelPp, HardSeconds = explanation.HardSeconds,
                Lines = text(explanation, trace),
            };
        }

        private static List<string> text(Mp2Explanation e, Mp2Trace trace)
        {
            var lines = new List<string>
            {
                $"PP за SS: {pp(e.PpSs)}.",
                "Тип карты: " + list(e.MapType, MAP_TYPE_NAMES, 0.05, 4) + ".",
                "За что даёт PP (по паттернам): " + list(e.TypeShares, TYPE_NAMES, 0.03, 5, e.PpSs) + ".",
                "Навыки: " + list(e.SkillShares, SKILL_NAMES, 0.03, 4) + ".",
                $"Уровень самых тяжёлых моментов стоит {pp(e.LevelPp)} PP; тяжёлой игры на нём {e.HardSeconds.ToString("0", CultureInfo.InvariantCulture)} с"
                + $" (длина ×{trace.LengthFactor.ToString("0.00", CultureInfo.InvariantCulture)}"
                + (trace.ShortFactor < 0.995 ? $", короткая карта ×{trace.ShortFactor.ToString("0.00", CultureInfo.InvariantCulture)}" : "") + ").",
            };

            var top = e.Sections.Take(3).ToList();
            if (top.Count > 0)
            {
                lines.Add("Самые дорогие места: " + string.Join("; ", top.Select(s =>
                    $"{clock(s.From)}–{clock(s.To)} — {percent(s.Share)} ({pp(s.Pp)} PP, {TYPE_NAMES[s.Type]}, {SKILL_NAMES[s.Skill]})")) + ".");
            }

            var gave = e.Effects.Where(x => x.Pp >= 0.5).OrderByDescending(x => x.Pp).ToList();
            var took = e.Effects.Where(x => x.Pp <= -0.5).OrderBy(x => x.Pp).ToList();
            if (gave.Count > 0)
                lines.Add("Что добавило: " + string.Join("; ", gave.Select(x => $"{x.Title} +{pp(x.Pp)}")) + ".");
            if (took.Count > 0)
                lines.Add("Что сняло: " + string.Join("; ", took.Select(x => $"{x.Title} −{pp(-x.Pp)}")) + ".");

            int cappedNotes = trace.Capped.Count(c => c);
            if (cappedNotes > 0)
                lines.Add($"Нот, упёршихся в потолок нагрузки: {cappedNotes}.");

            return lines;
        }

        private static string list(IReadOnlyDictionary<string, double> shares, IReadOnlyDictionary<string, string> names, double min, int take, double? pp = null)
            => string.Join(", ", shares.Where(s => s.Value >= min).OrderByDescending(s => s.Value).Take(take)
                                       .Select(s => $"{names[s.Key]} {percent(s.Value)}" + (pp is double total ? $" ({Mp2Explain.pp(total * s.Value)} PP)" : "")));

        private static int argMax(double[] values)
        {
            int best = 0;
            for (int i = 1; i < values.Length; i++)
            {
                if (values[i] > values[best])
                    best = i;
            }

            return best;
        }

        private static string pp(double value) => Math.Round(value).ToString("0", CultureInfo.InvariantCulture);

        private static string percent(double share) => Math.Round(share * 100).ToString("0", CultureInfo.InvariantCulture) + "%";

        private static string clock(double seconds)
        {
            int s = (int)Math.Max(0, Math.Round(seconds));
            return $"{s / 60}:{s % 60:00}";
        }
    }
}
