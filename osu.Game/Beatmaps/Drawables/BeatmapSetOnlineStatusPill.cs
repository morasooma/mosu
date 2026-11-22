// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Development;
using osu.Framework.Extensions;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.IO.Stores;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Performance.Debug;
using osuTK.Graphics;

namespace osu.Game.Beatmaps.Drawables
{
    public partial class BeatmapSetOnlineStatusPill : CircularContainer, IHasTooltip
    {
        /// <summary>
        /// Whether to show <see cref="BeatmapOnlineStatus.None"/> as "unknown" instead of fading out.
        /// </summary>
        public bool ShowUnknownStatus { get; init; }

        /// <summary>
        /// Whether changing status performs transition transforms.
        /// </summary>
        public bool Animated { get; init; } = true;

        public LocalisableString? TextOverride { get; init; }

        public Color4? BackgroundColourOverride { get; init; }

        public Color4? TextColourOverride { get; init; }

        public LocalisableString? TooltipOverride { get; init; }

        public BeatmapOnlineStatus Status
        {
            get => status;
            set
            {
                if (status == value)
                    return;

                status = value;

                if (IsLoaded)
                    updateState();
            }
        }

        private BeatmapOnlineStatus status;

        public float TextSize
        {
            init => statusText.Font = statusText.Font.With(size: value);
        }

        public MarginPadding TextPadding
        {
            init => statusText.Padding = value;
        }

        private readonly OsuSpriteText statusText;
        private readonly Box background;

        private const double animation_duration = 400;
        private IBindable<Colour4>? themeColour;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private OverlayColourProvider? colourProvider { get; set; }

        public BeatmapSetOnlineStatusPill()
        {
            AutoSizeAxes = Axes.Both;
            Masking = true;

            Alpha = 0;

            Children = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black,
                },
                statusText = new OsuSpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Font = OsuFont.GetFont(weight: FontWeight.Bold)
                },
            };

            Status = BeatmapOnlineStatus.None;
            TextPadding = new MarginPadding { Horizontal = 4, Bottom = 1 };
        }

        [BackgroundDependencyLoader]
        private void load(FontStore fonts, LocalisationManager localisation, CancellationToken? cancellationToken)
        {
            if (ThreadSafety.IsUpdateThread)
                return;

            // Pooled status text is assigned only on Update, after OsuSpriteText's
            // loader. Warm this finite vocabulary in the configured face on the
            // owning load thread so first autosize cannot synchronously read pages.
            var cancellation = cancellationToken ?? CancellationToken.None;
            if (TextOverride is { } customText)
                prepare(customText);
            else
            {
                foreach (BeatmapOnlineStatus onlineStatus in Enum.GetValues<BeatmapOnlineStatus>())
                    prepare(getStatusText(onlineStatus));
            }

            void prepare(LocalisableString text)
                => GameplayGlyphPrecache.Prepare(fonts, statusText.Font,
                    localisation.GetLocalisedString(text.ToUpper()), cancellation);
        }

        private static LocalisableString getStatusText(BeatmapOnlineStatus status) => status switch
        {
            BeatmapOnlineStatus.ArchivedMirror => "UNOFFICIAL MIRROR",
            BeatmapOnlineStatus.MosuAvailable => "MOSU SERVER",
            _ => status.GetLocalisableDescription(),
        };

        protected override void LoadComplete()
        {
            long started = PerformancePhaseLogger.Enabled ? Stopwatch.GetTimestamp() : 0;
            base.LoadComplete();
            long loaded = started != 0 ? Stopwatch.GetTimestamp() : 0;

            updateState(); // Reports its own time; don't count it twice as a separate slow operation.
            long stateUpdated = started != 0 ? Stopwatch.GetTimestamp() : 0;
            FinishTransforms(true);

            if (colourProvider != null)
            {
                themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
                themeColour.BindValueChanged(_ => updateState());
            }

            if (started != 0)
            {
                long finished = Stopwatch.GetTimestamp();
                double otherMs = Stopwatch.GetElapsedTime(started, loaded).TotalMilliseconds
                                 + Stopwatch.GetElapsedTime(stateUpdated, finished).TotalMilliseconds;
                if (otherMs >= 5)
                    Logger.Log($"MOSU_SLOW_STATUS_PILL operation=load_complete_other elapsed_ms={otherMs:F2} "
                               + $"base_ms={Stopwatch.GetElapsedTime(started, loaded).TotalMilliseconds:F2} "
                               + $"finish_and_bind_ms={Stopwatch.GetElapsedTime(stateUpdated, finished).TotalMilliseconds:F2}", LoggingTarget.Performance);
            }
        }

        private void updateState()
        {
            long started = PerformancePhaseLogger.Enabled ? Stopwatch.GetTimestamp() : 0;
            double duration = OverlayColourProvider.IsThemeUpdate ? 0 : (Animated ? animation_duration : 0);

            if (Status == BeatmapOnlineStatus.None && !ShowUnknownStatus)
            {
                if (duration == 0)
                {
                    ClearTransforms(targetMember: nameof(Alpha));
                    Alpha = 0;
                }
                else
                    this.FadeOut(duration, Easing.OutQuint);

                return;
            }

            // The autosize animation on this component is intended to animate horizontal sizing only.
            // To avoid vertical autosize animating from zero to non-zero, only apply the duration
            // after we have a valid size.
            if (Animated && Height > 0)
            {
                AutoSizeDuration = (float)duration;
                AutoSizeEasing = Easing.OutQuint;
            }

            if (duration == 0)
            {
                // Non-animated pills (including carousel panels) don't need to create an alpha
                // transform just to become visible. Cancel any previous fade on reuse.
                ClearTransforms(targetMember: nameof(Alpha));
                Alpha = 1;
            }
            else
                this.FadeIn(duration, Easing.OutQuint);

            long fadeDone = started != 0 ? Stopwatch.GetTimestamp() : 0;

            // Handle the case where transition from hidden to non-hidden may cause
            // a fade from a colour that doesn't make sense (due to not being able to see the previous colour).
            if (Alpha == 0)
                duration = 0;

            Color4 statusTextColour;

            // Status pills sit on fixed accent colours, not themed surfaces.
            // In light theme only, use default-theme text colours so they don't get inverted to white.
            if (OverlayColourProvider.IsLightTheme && colourProvider != null && !colourProvider.IgnoreLightTheme)
            {
                statusTextColour = status is BeatmapOnlineStatus.Graveyard or BeatmapOnlineStatus.ArchivedMirror
                    ? Color4.White
                    : colourProvider.GetDefaultColour(0.1f, 0.25f);
            }
            else if (colourProvider != null)
                statusTextColour = status is BeatmapOnlineStatus.Graveyard or BeatmapOnlineStatus.ArchivedMirror ? colourProvider.Background1 : colourProvider.Background3;
            else
                statusTextColour = status is BeatmapOnlineStatus.Graveyard or BeatmapOnlineStatus.ArchivedMirror ? colours.GreySeaFoamLight : Color4.Black;

            long colourDone = started != 0 ? Stopwatch.GetTimestamp() : 0;
            statusText.FadeColour(TextColourOverride ?? statusTextColour, duration, Easing.OutQuint);
            long textFadeDone = started != 0 ? Stopwatch.GetTimestamp() : 0;
            background.FadeColour(BackgroundColourOverride ?? OsuColour.ForBeatmapSetOnlineStatus(Status) ?? colourProvider?.Light1 ?? colours.GreySeaFoamLighter, duration, Easing.OutQuint);
            long backgroundDone = started != 0 ? Stopwatch.GetTimestamp() : 0;

            var text = (TextOverride ?? getStatusText(Status)).ToUpper();
            long textPrepared = started != 0 ? Stopwatch.GetTimestamp() : 0;
            statusText.Text = text;

            if (started != 0)
            {
                long finished = Stopwatch.GetTimestamp();
                double totalMs = Stopwatch.GetElapsedTime(started, finished).TotalMilliseconds;
                if (totalMs >= 5)
                    Logger.Log($"MOSU_SLOW_STATUS_PILL operation=update_state elapsed_ms={totalMs:F2} status={Status} "
                               + $"fade_ms={Stopwatch.GetElapsedTime(started, fadeDone).TotalMilliseconds:F2} "
                               + $"colour_ms={Stopwatch.GetElapsedTime(fadeDone, colourDone).TotalMilliseconds:F2} "
                               + $"text_fade_ms={Stopwatch.GetElapsedTime(colourDone, textFadeDone).TotalMilliseconds:F2} "
                               + $"background_ms={Stopwatch.GetElapsedTime(textFadeDone, backgroundDone).TotalMilliseconds:F2} "
                               + $"text_prepare_ms={Stopwatch.GetElapsedTime(backgroundDone, textPrepared).TotalMilliseconds:F2} "
                               + $"text_set_ms={Stopwatch.GetElapsedTime(textPrepared, finished).TotalMilliseconds:F2}", LoggingTarget.Performance);
            }
        }

        public LocalisableString TooltipText
        {
            get
            {
                if (TooltipOverride != null)
                    return TooltipOverride.Value;

                switch (Status)
                {
                    case BeatmapOnlineStatus.LocallyModified:
                        return SongSelectStrings.LocallyModifiedTooltip;

                    case BeatmapOnlineStatus.ArchivedMirror:
                        return global::osu.Game.Localisation.ClientTranslationStrings.J3217;

                    case BeatmapOnlineStatus.MosuAvailable:
                        return global::osu.Game.Localisation.ClientTranslationStrings.J3218;
                }

                return string.Empty;
            }
        }
    }
}
