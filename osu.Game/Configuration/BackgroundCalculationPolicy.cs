using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace osu.Game.Configuration
{
    [Flags]
    public enum LibraryCalculation
    {
        None = 0,
        Difficulty = 1,
        Performance = 2,
        RelaxDifficulty = 4,
        RelaxPerformance = 8,
        RefreshOutdated = 16,
        Import = 32,
        All = Difficulty | Performance | RelaxDifficulty | RelaxPerformance | RefreshOutdated | Import,
    }

    /// <summary>
    /// Persistent, ruleset-keyed library policy. Does not govern interactive or server calculations.
    /// Unknown rulesets inherit the defaults; settings for temporarily removed rulesets are retained.
    /// </summary>
    public sealed class BackgroundCalculationPolicy
    {
        private readonly OsuConfigManager config;
        private readonly object sync = new object();
        private string? lastJson;
        private Dictionary<string, Mode> modes = new Dictionary<string, Mode>();

        public BackgroundCalculationPolicy(OsuConfigManager config) => this.config = config;

        public sealed class Mode
        {
            public bool Enabled { get; set; } = true;
            public LibraryCalculation Calculations { get; set; } = LibraryCalculation.All;
            public int Concurrency { get; set; }
        }

        public Mode Get(string ruleset)
        {
            lock (sync)
            {
                reload();
                var mode = modes.GetValueOrDefault(ruleset) ?? new Mode();
                return new Mode { Enabled = mode.Enabled, Calculations = mode.Calculations, Concurrency = mode.Concurrency };
            }
        }

        public void Set(string ruleset, Mode mode)
        {
            lock (sync)
            {
                reload();
                modes[ruleset] = new Mode
                {
                    Enabled = mode.Enabled,
                    Calculations = mode.Calculations & LibraryCalculation.All,
                    Concurrency = Math.Clamp(mode.Concurrency, 0, 4),
                };
                lastJson = JsonConvert.SerializeObject(modes);
                config.SetValue(OsuSetting.ForkBackgroundCalculationModes, lastJson);
            }
        }

        public bool Allows(string ruleset, LibraryCalculation calculation, bool importing = false)
        {
            var mode = Get(ruleset);
            return mode.Enabled && (!importing || mode.Calculations.HasFlag(LibraryCalculation.Import))
                                && mode.Calculations.HasFlag(calculation);
        }

        public int Concurrency(string ruleset)
        {
            int global = config.Get<int>(OsuSetting.ForkBackgroundCalculationConcurrency);
            int local = Get(ruleset).Concurrency;
            return local == 0 ? global : Math.Min(global, Math.Clamp(local, 1, 4));
        }

        private void reload()
        {
            string json = config.Get<string>(OsuSetting.ForkBackgroundCalculationModes);
            if (json == lastJson)
                return;

            try
            {
                modes = JsonConvert.DeserializeObject<Dictionary<string, Mode>>(json) ?? new Dictionary<string, Mode>();
            }
            catch (JsonException)
            {
                // A hand-edited invalid value must not prevent the client from starting.
                modes = new Dictionary<string, Mode>();
            }

            lastJson = json;
        }
    }
}
