using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Overlays.Settings.Sections.Fork
{
    public partial class BackgroundCalculationSettingsPanel : SettingsSubPanel
    {
        protected override Drawable CreateHeader() => new SettingsHeader(BackgroundCalculationStrings.Title, BackgroundCalculationStrings.Description);

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, RulesetStore rulesets)
        {
            var policy = new BackgroundCalculationPolicy(config);
            AddSection(new CalculationSection(BackgroundCalculationStrings.General,
                check(BackgroundCalculationStrings.Startup, config.GetBindable<bool>(OsuSetting.ForkBackgroundCalculationStartup)),
                slider(BackgroundCalculationStrings.Delay, config.GetBindable<int>(OsuSetting.ForkBackgroundCalculationDelay)),
                slider(BackgroundCalculationStrings.Threads, config.GetBindable<int>(OsuSetting.ForkBackgroundCalculationConcurrency), BackgroundCalculationStrings.ThreadsHint),
                slider(BackgroundCalculationStrings.Pace, config.GetBindable<int>(OsuSetting.ForkBackgroundCalculationDutyPercent), BackgroundCalculationStrings.PaceHint)));

            foreach (var info in rulesets.AvailableRulesets)
            {
                var ruleset = info.CreateInstance();
                string key = info.ShortName;
                var mode = policy.Get(key);
                var enabled = new BindableBool(mode.Enabled);
                enabled.BindValueChanged(e =>
                {
                    var current = policy.Get(key);
                    current.Enabled = e.NewValue;
                    policy.Set(key, current);
                });
                var rows = new List<Drawable> { check(BackgroundCalculationStrings.Enabled, enabled) };
                rows.Add(flag(BackgroundCalculationStrings.SR, LibraryCalculation.Difficulty));
                if (ruleset.CreatePerformanceCalculator() != null)
                    rows.Add(flag(BackgroundCalculationStrings.PP, LibraryCalculation.Performance, BackgroundCalculationStrings.PPHint));
                if (ruleset.GetModsFor(ModType.Automation).OfType<ModRelax>().Any())
                {
                    rows.Add(flag(BackgroundCalculationStrings.RXSR, LibraryCalculation.RelaxDifficulty));
                    if (ruleset.CreatePerformanceCalculator() != null)
                        rows.Add(flag(BackgroundCalculationStrings.RXPP, LibraryCalculation.RelaxPerformance, BackgroundCalculationStrings.PPHint));
                }
                rows.Add(flag(BackgroundCalculationStrings.Refresh, LibraryCalculation.RefreshOutdated));
                rows.Add(flag(BackgroundCalculationStrings.Import, LibraryCalculation.Import));
                var threads = new BindableInt(mode.Concurrency) { MinValue = 0, MaxValue = 4 };
                threads.BindValueChanged(e =>
                {
                    var current = policy.Get(key);
                    current.Concurrency = e.NewValue;
                    policy.Set(key, current);
                });
                rows.Add(slider(BackgroundCalculationStrings.LocalThreads, threads));
                AddSection(new CalculationSection(info.Name, rows.ToArray()));

                Drawable flag(LocalisableString caption, LibraryCalculation calculation, LocalisableString hint = default)
                {
                    var value = new BindableBool(mode.Calculations.HasFlag(calculation));
                    value.BindValueChanged(e =>
                    {
                        var current = policy.Get(key);
                        current.Calculations = e.NewValue ? current.Calculations | calculation : current.Calculations & ~calculation;
                        policy.Set(key, current);
                    });
                    return check(caption, value, hint);
                }
            }

            AddSection(new CalculationSection(BackgroundCalculationStrings.Maintenance,
                check(BackgroundCalculationStrings.Objects, config.GetBindable<bool>(OsuSetting.ForkBackgroundCalculationObjectCounts)),
                check(BackgroundCalculationStrings.Scores, config.GetBindable<bool>(OsuSetting.ForkBackgroundCalculationScoreStatistics)),
                check(BackgroundCalculationStrings.Online, config.GetBindable<bool>(OsuSetting.ForkBackgroundCalculationOnlineMetadata)),
                check(BackgroundCalculationStrings.Tags, config.GetBindable<bool>(OsuSetting.ForkBackgroundCalculationTags))));
            AddSection(new CalculationSection(BackgroundCalculationStrings.Queue,
                new SettingsButtonV2 { Text = BackgroundCalculationStrings.Run, Action = BackgroundDataStoreProcessor.QueueLibraryProcessing },
                new SettingsButtonV2 { Text = BackgroundCalculationStrings.Pause, Action = () => BackgroundDataStoreProcessor.SetLibraryPaused(true) },
                new SettingsButtonV2 { Text = BackgroundCalculationStrings.Resume, Action = () => BackgroundDataStoreProcessor.SetLibraryPaused(false) },
                new SettingsButtonV2 { Text = BackgroundCalculationStrings.Stop, Action = BackgroundDataStoreProcessor.StopLibraryProcessing }));
        }

        private static Drawable check(LocalisableString caption, Bindable<bool> value, LocalisableString hint = default) =>
            new SettingsItemV2(new FormCheckBox { Caption = caption, HintText = hint, Current = value });

        private static Drawable slider(LocalisableString caption, Bindable<int> value, LocalisableString hint = default) =>
            new SettingsItemV2(new FormSliderBar<int> { Caption = caption, HintText = hint, Current = value, KeyboardStep = 1, LabelFormat = v => v.ToString() });

        private partial class CalculationSection : SettingsSection
        {
            public override LocalisableString Header { get; }
            public override Drawable CreateIcon() => new SpriteIcon { Icon = OsuIcon.Settings };

            public CalculationSection(LocalisableString title, params Drawable[] rows)
            {
                Header = title;
                Children = rows;
            }
        }
    }
}
