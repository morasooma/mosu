using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using osu.Game.Screens.Select;

namespace osu.Game.Online.API.Requests
{
    /// <summary>Reuse song-select range semantics; never send numeric clauses as literal song names.</summary>
    internal sealed class BeatmapCatalogueFilters
    {
        private static readonly Regex clause = new Regex(@"\b(?<key>\w+)(?<op>!=|!:|>=|<=|>:|<:|>|<|=|:|~)(?<value>[^\s]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private readonly FilterCriteria criteria = new FilterCriteria();
        internal string Text { get; }
        internal bool HasFilters { get; private set; }

        internal BeatmapCatalogueFilters(string text)
        {
            Text = clause.Replace(text, match =>
            {
                string key = match.Groups["key"].Value.ToLowerInvariant();
                if (key is not ("star" or "stars" or "sr" or "ar" or "cs" or "hp" or "dr" or "od" or "bpm" or "length"))
                    throw new NotSupportedException($"Условие «{match.Value}» не поддерживается каталогом зеркал. Используйте текст, star, ar, cs, hp, od, bpm или length.");
                var parsed = new FilterCriteria();
                FilterQueryParser.ApplyQueries(parsed, match.Value);
                if (!string.IsNullOrWhiteSpace(parsed.SearchText))
                    throw new NotSupportedException($"Не удалось разобрать условие «{match.Value}».");
                HasFilters = true;
                return string.Empty;
            }).Trim();
            FilterQueryParser.ApplyQueries(criteria, text);
        }

        internal void AddNekohaBounds(IDictionary<string, string> values)
        {
            add("stars", criteria.StarDifficulty);
            add("ar", criteria.ApproachRate);
            add("cs", criteria.CircleSize);
            add("hp", criteria.DrainRate);
            add("od", criteria.OverallDifficulty);
            add("bpm", criteria.BPM);
            add("length", criteria.Length, 0.001);

            void add<T>(string name, FilterCriteria.OptionalRange<T> range, double scale = 1) where T : struct, IComparable<T>
            {
                if (range.InvertRange) return; // Not representable remotely; exact comparison remains local.
                if (range.Min is { } min) values[name + "_min"] = (Convert.ToDouble(min, CultureInfo.InvariantCulture) * scale).ToString("R", CultureInfo.InvariantCulture);
                if (range.Max is { } max) values[name + "_max"] = (Convert.ToDouble(max, CultureInfo.InvariantCulture) * scale).ToString("R", CultureInfo.InvariantCulture);
            }
        }

        internal string DirectQuery()
        {
            if (Text.Contains('[') || Text.Contains(']'))
                throw new NotSupportedException("Square brackets are reserved for osu.direct filters. Use star, ar, cs, hp, od, bpm or length conditions instead.");
            var bounds = new Dictionary<string, string>();
            AddNekohaBounds(bounds);
            var fields = new Dictionary<string, string>
            {
                ["stars"] = "difficulty_rating", ["ar"] = "ar", ["cs"] = "cs", ["hp"] = "drain",
                ["od"] = "accuracy", ["bpm"] = "bpm", ["length"] = "total_length",
            };
            var clauses = bounds.Select(bound => "beatmaps." + fields[bound.Key[..bound.Key.LastIndexOf('_')]]
                                                + (bound.Key.EndsWith("_min", StringComparison.Ordinal) ? " >= " : " <= ") + bound.Value).ToArray();
            return clauses.Length == 0 ? Text : "[" + string.Join(" AND ", clauses) + "] " + Text;
        }

        internal void AddHinamizawaBounds(IDictionary<string, string> values)
        {
            var bounds = new Dictionary<string, string>();
            AddNekohaBounds(bounds);
            foreach (var bound in bounds)
            {
                int separator = bound.Key.LastIndexOf('_');
                string field = bound.Key[..separator];
                if (field is "stars" or "bpm" or "length")
                {
                    string edge = bound.Key[(separator + 1)..];
                    double value = Math.Max(0, double.Parse(bound.Value, CultureInfo.InvariantCulture));
                    // The endpoint accepts only integer seconds, unlike the local song-select range.
                    // Widen remote bounds; Matches() still enforces the exact condition locally.
                    if (field == "length") value = edge == "min" ? Math.Floor(value) : Math.Ceiling(value);
                    values[edge + "_" + field] = value.ToString("R", CultureInfo.InvariantCulture);
                }
            }
        }

        internal bool Matches(JObject set, int mode)
        {
            if (!HasFilters && mode < 0) return true;
            return (set["beatmaps"] as JArray)?.OfType<JObject>().Any(map =>
                string.IsNullOrEmpty((string?)map["deleted_at"]) && string.IsNullOrEmpty((string?)map["deletedAt"])
                && (mode < 0 || (int?)map["mode_int"] == mode || (string?)map["mode"] == new[] { "osu", "taiko", "fruits", "mania" }[mode])
                && criteria.StarDifficulty.IsInRange((double?)map["difficulty_rating"] ?? 0)
                && criteria.ApproachRate.IsInRange((float?)map["ar"] ?? 0)
                && criteria.CircleSize.IsInRange((float?)map["cs"] ?? 0)
                && criteria.DrainRate.IsInRange((float?)map["drain"] ?? 0)
                && criteria.OverallDifficulty.IsInRange((float?)map["accuracy"] ?? 0)
                && criteria.BPM.IsInRange((double?)map["bpm"] ?? (double?)set["bpm"] ?? 0)
                && criteria.Length.IsInRange(((double?)map["total_length"] ?? 0) * 1000)) == true;
        }
    }
}
