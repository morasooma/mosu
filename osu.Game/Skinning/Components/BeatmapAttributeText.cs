// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading;
using JetBrains.Annotations;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Extensions;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Localisation.SkinComponents;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Select;
using osu.Game.Overlays;
using osuTK;
using osu.Game.Utils;

namespace osu.Game.Skinning.Components
{
    [UsedImplicitly]
    public partial class BeatmapAttributeText : FontAdjustableSkinComponent
    {
        [SettingSource(typeof(BeatmapAttributeTextStrings), nameof(BeatmapAttributeTextStrings.Attribute))]
        public Bindable<BeatmapAttribute> Attribute { get; } = new Bindable<BeatmapAttribute>(BeatmapAttribute.StarRating);

        [SettingSource(typeof(BeatmapAttributeTextStrings), nameof(BeatmapAttributeTextStrings.Template), nameof(BeatmapAttributeTextStrings.TemplateDescription))]
        public Bindable<string> Template { get; } = new Bindable<string>("{Label}: {Value}");

        [SettingSource(typeof(MorasoomaSkinEditorStrings), nameof(MorasoomaSkinEditorStrings.CustomTextSize))]
        public BindableBool UseCustomSize { get; } = new BindableBool();

        [SettingSource(typeof(MorasoomaSkinEditorStrings), nameof(MorasoomaSkinEditorStrings.ScrollOverflow))]
        public BindableBool ScrollOverflow { get; } = new BindableBool(true);

        [Resolved]
        private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;

        [Resolved]
        private IBindable<IReadOnlyList<Mod>> mods { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private BeatmapDifficultyCache difficultyCache { get; set; } = null!;

        [Resolved]
        private ISongSelect? songSelect { get; set; }

        private readonly AttributeMarquee marquee;
        private readonly List<SkinSpriteText> texts = new List<SkinSpriteText>();
        private FontUsage textFont;
        private Colour4 textColour = Colour4.White;
        private LocalisableString displayedText;
        private IBindable<StarDifficulty>? difficultyBindable;
        private CancellationTokenSource? difficultyCancellationSource;
        private ModSettingChangeTracker? modSettingTracker;
        private StarDifficulty? starDifficulty;
        private bool? requestedPerformance;
        private string numberedTemplate = string.Empty;
        private readonly List<BeatmapAttribute> templateAttributes = new List<BeatmapAttribute>();

        public BeatmapAttributeText()
        {
            AutoSizeAxes = Axes.Both;

            InternalChild = marquee = new AttributeMarquee
            {
                RelativeSizeAxes = Axes.None,
                AutoSizeAxes = Axes.Both,
                AllowScrolling = false,
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                CreateContent = () =>
                {
                    var text = new SkinSpriteText { Text = displayedText, Font = textFont, Colour = textColour };
                    texts.Add(text);
                    return text;
                },
            };
            UseCustomSize.BindValueChanged(_ => updateTextArea());
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            updateTextArea();
            ScrollOverflow.BindValueChanged(_ => marquee.AllowScrolling = UseCustomSize.Value && ScrollOverflow.Value, true);
            prepareTemplate();
            Attribute.BindValueChanged(_ => updateText());
            Template.BindValueChanged(_ =>
            {
                prepareTemplate();
                updateText();
            });

            beatmap.BindValueChanged(_ =>
            {
                cancelDifficultyRequest();
                updateText();
            }, true);

            mods.BindValueChanged(m =>
            {
                modSettingTracker?.Dispose();
                modSettingTracker = new ModSettingChangeTracker(m.NewValue)
                {
                    SettingChanged = _ => updateText()
                };

                updateText();
            }, true);

            ruleset.BindValueChanged(_ => updateText());

            updateText();
        }

        private void prepareTemplate()
        {
            numberedTemplate = Template.Value
                                       .Replace("{", "{{")
                                       .Replace("}", "}}")
                                       .Replace(@"{{Label}}", "{0}")
                                       .Replace(@"{{Value}}", "{1}");

            templateAttributes.Clear();
            foreach (var type in Enum.GetValues<BeatmapAttribute>())
            {
                string replaced = numberedTemplate.Replace($@"{{{{{type}}}}}", $@"{{{templateAttributes.Count + 2}}}");

                if (numberedTemplate != replaced)
                {
                    numberedTemplate = replaced;
                    templateAttributes.Add(type);
                }
            }
        }

        private void updateText()
        {
            // Use the same recognized placeholders as formatting, not a substring check.
            // The primary value remains part of the component's contract even without {Value}.
            bool needsPerformance = Attribute.Value == BeatmapAttribute.MaxPP || templateAttributes.Contains(BeatmapAttribute.MaxPP);
            bool needsStars = Attribute.Value == BeatmapAttribute.StarRating || templateAttributes.Contains(BeatmapAttribute.StarRating);
            updateDifficultyRequest(needsPerformance ? true : needsStars ? false : null);

            List<object?> values = new List<object?>(templateAttributes.Count + 2)
            {
                getLabelString(Attribute.Value),
                getValueString(Attribute.Value)
            };

            foreach (var type in templateAttributes)
                values.Add(getValueString(type));

            displayedText = LocalisableString.Format(numberedTemplate, values.ToArray());
            foreach (var text in texts) text.Text = displayedText;
        }

        private void updateDifficultyRequest(bool? calculatePerformance)
        {
            if (requestedPerformance == calculatePerformance)
                return;

            cancelDifficultyRequest();
            if (calculatePerformance == null)
                return;

            requestedPerformance = calculatePerformance;
            difficultyCancellationSource = new CancellationTokenSource();
            // Skinned song-select SR/PP must use the same admission delay as the carousel.
            // Otherwise a skin's MaxPP text starts full work for every transient selection,
            // bypassing the card/wedge debounce. Other screens retain immediate calculation;
            // cache hits and explicit full ruleset/mod refreshes remain immediate in the cache.
            int computationDelay = songSelect == null ? 0 : calculatePerformance.Value
                ? SongSelect.PERFORMANCE_CALCULATION_DEBOUNCE
                : SongSelect.DIFFICULTY_CALCULATION_DEBOUNCE;
            difficultyBindable = difficultyCache.GetBindableDifficulty(beatmap.Value.BeatmapInfo, difficultyCancellationSource.Token,
                computationDelay: computationDelay, calculatePerformance: calculatePerformance.Value);
            starDifficulty = difficultyBindable.Value;
            difficultyBindable.BindValueChanged(d =>
            {
                starDifficulty = d.NewValue;
                updateText();
            });
        }

        private void cancelDifficultyRequest()
        {
            difficultyCancellationSource?.Cancel();
            difficultyCancellationSource?.Dispose();
            difficultyCancellationSource = null;
            difficultyBindable?.UnbindAll();
            difficultyBindable = null;
            starDifficulty = null;
            requestedPerformance = null;
        }

        private LocalisableString getLabelString(BeatmapAttribute attribute)
        {
            switch (attribute)
            {
                case BeatmapAttribute.CircleSize:
                    return BeatmapsetsStrings.ShowStatsCs;

                case BeatmapAttribute.Accuracy:
                    return BeatmapsetsStrings.ShowStatsAccuracy;

                case BeatmapAttribute.HPDrain:
                    return BeatmapsetsStrings.ShowStatsDrain;

                case BeatmapAttribute.ApproachRate:
                    return BeatmapsetsStrings.ShowStatsAr;

                case BeatmapAttribute.StarRating:
                    return BeatmapsetsStrings.ShowStatsStars;

                case BeatmapAttribute.Title:
                    return EditorSetupStrings.Title;

                case BeatmapAttribute.Artist:
                    return EditorSetupStrings.Artist;

                case BeatmapAttribute.DifficultyName:
                    return EditorSetupStrings.DifficultyHeader;

                case BeatmapAttribute.Creator:
                    return EditorSetupStrings.Creator;

                case BeatmapAttribute.Source:
                    return EditorSetupStrings.Source;

                case BeatmapAttribute.Length:
                    return ArtistStrings.TracklistLength.ToTitle();

                case BeatmapAttribute.RankedStatus:
                    return BeatmapDiscussionsStrings.IndexFormBeatmapsetStatusDefault;

                case BeatmapAttribute.BPM:
                    return BeatmapsetsStrings.ShowStatsBpm;

                case BeatmapAttribute.MaxPP:
                    return BeatmapAttributeTextStrings.MaxPP;

                default:
                    return string.Empty;
            }
        }

        private LocalisableString getValueString(BeatmapAttribute attribute)
        {
            switch (attribute)
            {
                case BeatmapAttribute.Title:
                    return new RomanisableString(beatmap.Value.BeatmapInfo.Metadata.TitleUnicode, beatmap.Value.BeatmapInfo.Metadata.Title);

                case BeatmapAttribute.Artist:
                    return new RomanisableString(beatmap.Value.BeatmapInfo.Metadata.ArtistUnicode, beatmap.Value.BeatmapInfo.Metadata.Artist);

                case BeatmapAttribute.DifficultyName:
                    return beatmap.Value.BeatmapInfo.DifficultyName;

                case BeatmapAttribute.Creator:
                    return beatmap.Value.BeatmapInfo.Metadata.Author.Username;

                case BeatmapAttribute.Source:
                    return beatmap.Value.BeatmapInfo.Metadata.Source;

                case BeatmapAttribute.Length:
                    return Math.Round(beatmap.Value.BeatmapInfo.Length / ModUtils.CalculateRateWithMods(mods.Value)).ToFormattedDuration();

                case BeatmapAttribute.RankedStatus:
                    return beatmap.Value.BeatmapInfo.Status.GetLocalisableDescription();

                case BeatmapAttribute.BPM:
                    return FormatUtils.RoundBPM(beatmap.Value.BeatmapInfo.BPM, ModUtils.CalculateRateWithMods(mods.Value)).ToLocalisableString(@"0.##");

                case BeatmapAttribute.CircleSize:
                    return computeDifficulty().CircleSize.ToLocalisableString(@"0.##");

                case BeatmapAttribute.HPDrain:
                    return computeDifficulty().DrainRate.ToLocalisableString(@"0.##");

                case BeatmapAttribute.Accuracy:
                    return computeDifficulty().OverallDifficulty.ToLocalisableString(@"0.##");

                case BeatmapAttribute.ApproachRate:
                    return computeDifficulty().ApproachRate.ToLocalisableString(@"0.##");

                case BeatmapAttribute.StarRating:
                    return (starDifficulty?.Stars ?? 0).FormatStarRating();

                case BeatmapAttribute.MaxPP:
                    return Math.Round(starDifficulty?.PerformanceAttributes?.Total ?? 0, MidpointRounding.AwayFromZero).ToLocalisableString();

                default:
                    return string.Empty;
            }

            BeatmapDifficulty computeDifficulty()
            {
                return ruleset.Value is RulesetInfo rulesetInfo
                    ? rulesetInfo.CreateInstance().GetAdjustedDisplayDifficulty(beatmap.Value.BeatmapInfo, mods.Value)
                    : new BeatmapDifficulty(beatmap.Value.BeatmapInfo.Difficulty);
            }
        }

        private partial class AttributeMarquee : MarqueeContainer
        {
            public new Axes AutoSizeAxes
            {
                get => base.AutoSizeAxes;
                set => base.AutoSizeAxes = value;
            }
        }

        private void updateTextArea()
        {
            Vector2 currentSize = DrawSize;
            bool custom = UseCustomSize.Value;
            bool wasAutomatic = AutoSizeAxes != Axes.None;
            AutoSizeAxes = custom ? Axes.None : Axes.Both;
            if (custom && wasAutomatic)
                Size = new Vector2(currentSize.X > 0 ? currentSize.X : 300, currentSize.Y > 0 ? currentSize.Y : 40);
            Masking = custom;
            marquee.AutoSizeAxes = Axes.None;
            marquee.RelativeSizeAxes = custom ? Axes.X : Axes.None;
            marquee.AutoSizeAxes = custom ? Axes.Y : Axes.Both;
            marquee.AllowScrolling = custom && ScrollOverflow.Value;
            if (custom) marquee.Width = 1;
        }

        internal void RestoreTextArea(SerialisedDrawableInfo info)
        {
            if (info.Settings.TryGetValue("use_custom_size", out var custom))
                UseCustomSize.Value = Convert.ToBoolean(custom, System.Globalization.CultureInfo.InvariantCulture);
            else
            {
                // Read the short-lived slider format, but only write the toggle and normal layout dimensions.
                float width = info.Settings.TryGetValue("text_area_width", out var w) ? Convert.ToSingle(w) : 0;
                float height = info.Settings.TryGetValue("text_area_height", out var h) ? Convert.ToSingle(h) : 0;
                UseCustomSize.Value = width > 0 || height > 0;
                if (UseCustomSize.Value)
                    Size = new Vector2(width > 0 ? width : 300, height > 0 ? height : 40);
            }
        }

        protected override void SetFont(FontUsage font)
        {
            textFont = font.With(size: 40);
            foreach (var text in texts) text.Font = textFont;
        }

        protected override void SetTextColour(Colour4 colour)
        {
            textColour = colour;
            foreach (var text in texts) text.Colour = colour;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            cancelDifficultyRequest();
            modSettingTracker?.Dispose();
        }
    }

    // WARNING: DO NOT ADD ANY VALUES TO THIS ENUM ANYWHERE ELSE THAN AT THE END.
    // Doing so will break existing user skins.
    public enum BeatmapAttribute
    {
        CircleSize,
        HPDrain,
        Accuracy,
        ApproachRate,
        StarRating,
        Title,
        Artist,
        DifficultyName,
        Creator,
        Length,
        RankedStatus,
        BPM,
        Source,
        MaxPP
    }
}
