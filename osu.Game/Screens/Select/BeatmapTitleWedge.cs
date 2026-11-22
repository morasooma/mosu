// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Overlays.BeatmapSet;
using osu.Game.Performance.Debug;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Utils;
using osuTK;

namespace osu.Game.Screens.Select
{
    public partial class BeatmapTitleWedge : VisibilityContainer
    {
        private const float corner_radius = 10;

        [Resolved]
        private IBindable<WorkingBeatmap> working { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private IBindable<IReadOnlyList<Mod>> mods { get; set; } = null!;

        [Resolved]
        private IBindable<SongSelect.BeatmapSetLookupResult?> onlineLookupResult { get; set; } = null!;

        public float TopPadding { get; init; }

        protected override bool StartHidden => true;

        private ModSettingChangeTracker? settingChangeTracker;

        private BeatmapSetOnlineStatusPill statusPill = null!;
        private ServerExclusiveBeatmapBadge exclusiveBadge = null!;
        private OsuHoverContainer titleLink = null!;
        private MarqueeContainer titleLabel = null!;
        private OsuHoverContainer artistLink = null!;
        private MarqueeContainer artistLabel = null!;

        internal string DisplayedTitle { get; private set; } = string.Empty;
        internal string DisplayedArtist { get; private set; } = string.Empty;

        private StatisticPlayCount playCount = null!;
        private FavouriteButton favouriteButton = null!;
        private Statistic lengthStatistic = null!;
        private Statistic bpmStatistic = null!;

        [Resolved]
        private ISongSelect? songSelect { get; set; }

        [Resolved]
        private LocalisationManager localisation { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private Online.Leaderboards.LeaderboardManager leaderboardManager { get; set; } = null!;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        private readonly Bindable<bool> disableShear = new BindableBool();
        private FillFlowContainer statisticsFlow = null!;
        private IBindable<Colour4> themeColour = null!;
        private IBindable<float>? radiusBindable;
        private Action<ValueChangedEvent<Guid?>>? onMosuResolvedBeatmapChanged;

        public BeatmapTitleWedge()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Masking = true;
            CornerRadius = corner_radius;

            disableShear.BindTo(OsuGame.DisableShear);

            radiusBindable = colourProvider.GetRadiusBindable(osu.Game.Overlays.CustomTheme.GeometryRole.Panel, corner_radius);

            void updateRadius()
            {
                float target = radiusBindable?.Value ?? corner_radius;
                float maxSafe = !disableShear.Value ? 12f : 25f;
                CornerRadius = Math.Min(target, maxSafe);
            }

            disableShear.BindValueChanged(_ => updateRadius());
            radiusBindable.BindValueChanged(_ => updateRadius(), true);

            InternalChildren = new Drawable[]
            {
                new WedgeBackground(),
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Padding = new MarginPadding
                    {
                        Top = SongSelect.WEDGE_CONTENT_MARGIN + TopPadding,
                        Left = SongSelect.WEDGE_CONTENT_MARGIN
                    },
                    Spacing = new Vector2(0f, 4f),
                    Children = new Drawable[]
                    {
                        new ShearAligningWrapper(new FillFlowContainer
                        {
                            Shear = -OsuGame.SHEAR,
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(4f, 0f),
                            Children = new Drawable[]
                            {
                                statusPill = new BeatmapSetOnlineStatusPill
                                {
                                    ShowUnknownStatus = true,
                                    TextSize = OsuFont.Style.Caption1.Size,
                                    TextPadding = new MarginPadding { Horizontal = 6, Vertical = 1 },
                                },
                                exclusiveBadge = new ServerExclusiveBeatmapBadge
                                {
                                    Alpha = 0
                                }
                            }
                        }),
                        new ShearAligningWrapper(new Container
                        {
                            Shear = -OsuGame.SHEAR,
                            RelativeSizeAxes = Axes.X,
                            Height = OsuFont.Style.Title.Size,
                            Margin = new MarginPadding { Bottom = -4f },
                            Child = titleLink = new OsuHoverContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Child = titleLabel = new MarqueeContainer
                                {
                                    OverflowSpacing = 50,
                                }
                            }
                        }),
                        new ShearAligningWrapper(new Container
                        {
                            Shear = -OsuGame.SHEAR,
                            RelativeSizeAxes = Axes.X,
                            Height = OsuFont.Style.Heading2.Size,
                            Margin = new MarginPadding { Left = 1f },
                            Child = artistLink = new OsuHoverContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Child = artistLabel = new MarqueeContainer
                                {
                                    OverflowSpacing = 50,
                                }
                            }
                        }),
                        new ShearAligningWrapper(statisticsFlow = new FillFlowContainer
                        {
                            Shear = -OsuGame.SHEAR,
                            AutoSizeAxes = Axes.X,
                            Height = 30,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(2f, 0f),
                            Children = new Drawable[]
                            {
                                playCount = new StatisticPlayCount(background: true, leftPadding: SongSelect.WEDGE_CONTENT_MARGIN, minSize: 50f)
                                {
                                    Margin = new MarginPadding { Left = -SongSelect.WEDGE_CONTENT_MARGIN },
                                },
                                favouriteButton = new FavouriteButton(),
                                lengthStatistic = new Statistic(OsuIcon.Clock),
                                bpmStatistic = new Statistic(OsuIcon.Metronome)
                                {
                                    TooltipText = BeatmapsetsStrings.ShowStatsBpm,
                                    Margin = new MarginPadding { Left = 5f },
                                },
                            },
                        }),
                        new ShearAligningWrapper(new Container
                        {
                            Shear = -OsuGame.SHEAR,
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Margin = new MarginPadding { Left = -SongSelect.WEDGE_CONTENT_MARGIN },
                            Padding = new MarginPadding { Right = -SongSelect.WEDGE_CONTENT_MARGIN },
                            Child = new DifficultyDisplay(),
                        }),
                    },
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            working.BindValueChanged(_ => updateDisplay());
            ruleset.BindValueChanged(_ => updateDisplay());
            onlineLookupResult.BindValueChanged(_ => updateDisplay());
            onMosuResolvedBeatmapChanged = e =>
            {
                if (e.NewValue == working.Value?.BeatmapInfo.ID && working.Value.BeatmapInfo.Status == BeatmapOnlineStatus.None)
                    statusPill.Status = BeatmapOnlineStatus.MosuAvailable;
            };
            leaderboardManager.MosuResolvedBeatmap.ValueChanged += onMosuResolvedBeatmapChanged;

            themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
            themeColour.BindValueChanged(_ => updateDisplay());

            mods.BindValueChanged(m =>
            {
                settingChangeTracker?.Dispose();

                updateLengthAndBpmStatistics();

                settingChangeTracker = new ModSettingChangeTracker(m.NewValue);
                settingChangeTracker.SettingChanged += _ => updateLengthAndBpmStatistics();
            });

            updateDisplay();

            statisticsFlow.AutoSizeDuration = 100;
            statisticsFlow.AutoSizeEasing = Easing.OutQuint;
        }

        private int measuredInitialUpdateFrames;

        public override bool UpdateSubTree()
        {
            if (!PerformancePhaseLogger.Enabled || measuredInitialUpdateFrames >= 3)
                return base.UpdateSubTree();

            measuredInitialUpdateFrames++;
            long start = Stopwatch.GetTimestamp();
            try
            {
                return base.UpdateSubTree();
            }
            finally
            {
                Logger.Log($"MOSU_SONG_SELECT_INITIAL_TREE component=BeatmapTitleWedge frame={measuredInitialUpdateFrames} "
                           + $"elapsed_ms={Stopwatch.GetElapsedTime(start).TotalMilliseconds:F2}", LoggingTarget.Performance);
            }
        }

        protected override void PopIn()
        {
            this.MoveToX(0, SongSelect.ENTER_DURATION, Easing.OutQuint)
                .FadeIn(SongSelect.ENTER_DURATION / 3, Easing.In);
        }

        protected override void PopOut()
        {
            this.MoveToX(-150, SongSelect.ENTER_DURATION, Easing.OutQuint)
                .FadeOut(SongSelect.ENTER_DURATION / 3, Easing.In);
        }

        private void updateDisplay()
        {
            long started = PerformancePhaseLogger.Enabled ? Stopwatch.GetTimestamp() : 0;
            var metadata = working.Value.Metadata;
            var beatmapInfo = working.Value.BeatmapInfo;

            statusPill.Status = beatmapInfo.Status == BeatmapOnlineStatus.None
                                && leaderboardManager.IsMosuResolved(beatmapInfo.ID)
                ? BeatmapOnlineStatus.MosuAvailable
                : beatmapInfo.Status;
            long statusDone = started != 0 ? Stopwatch.GetTimestamp() : 0;

            var titleText = new RomanisableString(metadata.TitleUnicode, metadata.Title);
            titleLabel.CreateContent = () => new OsuSpriteText
            {
                AsyncText = titleText,
                Shadow = true,
                Font = OsuFont.Style.Title,
                Colour = colourProvider.Content1,
            };
            titleLink.Action = () => songSelect?.AddToSearch(titleText.GetPreferred(localisation.CurrentParameters.Value.PreferOriginalScript));
            DisplayedTitle = titleText.ToString();
            long titleDone = started != 0 ? Stopwatch.GetTimestamp() : 0;

            var artistText = new RomanisableString(metadata.ArtistUnicode, metadata.Artist);
            artistLabel.CreateContent = () => new OsuSpriteText
            {
                AsyncText = artistText,
                Shadow = true,
                Font = OsuFont.Style.Heading2,
                Colour = colourProvider.Content2,
            };
            artistLink.Action = () => songSelect?.AddToSearch(artistText.GetPreferred(localisation.CurrentParameters.Value.PreferOriginalScript));
            DisplayedArtist = artistText.ToString();
            long artistDone = started != 0 ? Stopwatch.GetTimestamp() : 0;

            if (metadata.IsServerExclusive())
                exclusiveBadge.Show();
            else
                exclusiveBadge.Hide();

            long badgeDone = started != 0 ? Stopwatch.GetTimestamp() : 0;
            updateLengthAndBpmStatistics();
            long statisticsDone = started != 0 ? Stopwatch.GetTimestamp() : 0;
            updateOnlineDisplay();

            if (started != 0)
            {
                long finished = Stopwatch.GetTimestamp();
                double totalMs = Stopwatch.GetElapsedTime(started, finished).TotalMilliseconds;
                if (totalMs >= 5)
                {
                    Logger.Log($"MOSU_SLOW_TITLE_WEDGE_DISPLAY elapsed_ms={totalMs:F2} "
                               + $"status_ms={Stopwatch.GetElapsedTime(started, statusDone).TotalMilliseconds:F2} "
                               + $"title_ms={Stopwatch.GetElapsedTime(statusDone, titleDone).TotalMilliseconds:F2} "
                               + $"artist_ms={Stopwatch.GetElapsedTime(titleDone, artistDone).TotalMilliseconds:F2} "
                               + $"badge_ms={Stopwatch.GetElapsedTime(artistDone, badgeDone).TotalMilliseconds:F2} "
                               + $"statistics_ms={Stopwatch.GetElapsedTime(badgeDone, statisticsDone).TotalMilliseconds:F2} "
                               + $"online_ms={Stopwatch.GetElapsedTime(statisticsDone, finished).TotalMilliseconds:F2}", LoggingTarget.Performance);
                }
            }
        }

        private CancellationTokenSource? lengthBpmCancellationSource;
        private ScheduledDelegate? lengthBpmAdmission;
        private WorkingBeatmap? statisticsWorkingBeatmap;
        private double statisticsRate;
        internal int LengthBpmJobsStarted { get; private set; }

        private void updateLengthAndBpmStatistics()
        {
            // Capture everything the worker needs on the update thread. A ruleset/theme/online refresh
            // must not cancel an in-flight calculation for the same map and effective rate.
            WorkingBeatmap selectedBeatmap = working.Value;
            double rate = ModUtils.CalculateRateWithMods(mods.Value);

            if (ReferenceEquals(statisticsWorkingBeatmap, selectedBeatmap) && statisticsRate == rate)
                return;

            lengthBpmCancellationSource?.Cancel();
            lengthBpmAdmission?.Cancel();
            lengthBpmAdmission = null;
            lengthBpmCancellationSource = new CancellationTokenSource();
            statisticsWorkingBeatmap = selectedBeatmap;
            statisticsRate = rate;
            LengthBpmJobsStarted++;

            var token = lengthBpmCancellationSource.Token;
            // Length is already stored with the map. Exact BPM range/drain statistics need the
            // decoded objects, so wait for selection to settle instead of forcing cold decoding
            // for every transient map. Never leave the preceding map's BPM/tooltip visible.
            lengthStatistic.Text = Math.Round(selectedBeatmap.BeatmapInfo.Length / rate).ToFormattedDuration();
            lengthStatistic.TooltipText = default;
            bpmStatistic.Text = null;

            if (selectedBeatmap.BeatmapLoaded)
                calculateLengthAndBpmStatistics(selectedBeatmap, rate, token);
            else
                lengthBpmAdmission = Scheduler.AddDelayed(() =>
                {
                    lengthBpmAdmission = null;
                    calculateLengthAndBpmStatistics(selectedBeatmap, rate, token);
                }, SongSelect.PERFORMANCE_CALCULATION_DEBOUNCE);
        }

        private void calculateLengthAndBpmStatistics(WorkingBeatmap selectedBeatmap, double rate, CancellationToken token)
        {
            if (token.IsCancellationRequested)
                return;

            Task.Run(() =>
            {
                if (token.IsCancellationRequested)
                    return;

                // This can take time as it is a synchronous task.
                var beatmap = selectedBeatmap.Beatmap;
                if (token.IsCancellationRequested || beatmap == null)
                    return;

                int bpmMax = FormatUtils.RoundBPM(beatmap.ControlPointInfo.BPMMaximum, rate);
                int bpmMin = FormatUtils.RoundBPM(beatmap.ControlPointInfo.BPMMinimum, rate);
                int mostCommonBPM = FormatUtils.RoundBPM(60000 / beatmap.GetMostCommonBeatLength(), rate);
                if (token.IsCancellationRequested)
                    return;

                double drainLength = Math.Round(beatmap.CalculateDrainLength() / rate);
                if (token.IsCancellationRequested)
                    return;

                Schedule(() =>
                {
                    if (token.IsCancellationRequested)
                        return;

                    lengthStatistic.TooltipText = BeatmapsetsStrings.ShowStatsTotalLength(drainLength.ToFormattedDuration());

                    bpmStatistic.Text = bpmMin == bpmMax
                        ? $"{bpmMin}"
                        : LocalisableString.Interpolate($"{bpmMin}-{bpmMax} ({SongSelectStrings.MostlyBPM(mostCommonBPM)})");
                });
            }, token);
        }

        private CancellationTokenSource? onlineDisplayCancellationSource;

        private void updateOnlineDisplay()
        {
            onlineDisplayCancellationSource?.Cancel();
            onlineDisplayCancellationSource = null;

            if (onlineLookupResult.Value?.Status != SongSelect.BeatmapSetLookupStatus.Completed)
            {
                playCount.Value = null;
                favouriteButton.SetLoading();
            }
            else
            {
                var onlineBeatmap = onlineLookupResult.Value.Result?.Beatmaps.SingleOrDefault(b => b.OnlineID == working.Value.BeatmapInfo.OnlineID);
                playCount.Value = new StatisticPlayCount.Data(onlineBeatmap?.PlayCount ?? -1, onlineBeatmap?.UserPlayCount ?? -1);
                favouriteButton.SetBeatmapSet(onlineLookupResult.Value.Result);

                onlineDisplayCancellationSource = new CancellationTokenSource();
                var token = onlineDisplayCancellationSource.Token;

                // the online fetch may have also updated the beatmap's status.
                // this needs to be checked against the *local* beatmap model rather than the online one, because it's not known here whether the status change has occurred or not
                // (think scenarios like the beatmap being locally modified).
                // it also has to be handled explicitly like this because the working beatmap's `BeatmapInfo` will not receive these updates due to being detached
                // (and because of https://github.com/ppy/osu/blob/4b73afd1957a9161e2956fc4191c8114d9958372/osu.Game/Screens/SelectV2/SongSelect.cs#L487-L488
                // which prevents working beatmap refetches caused by changes to the realm model of perceived low importance).
                realm.RunAsync(r =>
                {
                    var refetchedBeatmap = r.Find<BeatmapInfo>(working.Value.BeatmapInfo.ID);
                    return refetchedBeatmap?.Status;
                }, token).ContinueWith(t =>
                {
                    var status = t.GetResultSafely();

                    if (status != null)
                    {
                        Schedule(() =>
                        {
                            if (token.IsCancellationRequested)
                                return;

                            statusPill.Status = status.Value == BeatmapOnlineStatus.None
                                                && leaderboardManager.IsMosuResolved(working.Value.BeatmapInfo.ID)
                                ? BeatmapOnlineStatus.MosuAvailable
                                : status.Value;
                        });
                    }
                }, token);
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            if (onMosuResolvedBeatmapChanged != null)
                leaderboardManager.MosuResolvedBeatmap.ValueChanged -= onMosuResolvedBeatmapChanged;

            lengthBpmAdmission?.Cancel();
            lengthBpmCancellationSource?.Cancel();
            lengthBpmCancellationSource?.Dispose();
            lengthBpmCancellationSource = null;
            settingChangeTracker?.Dispose();
            onlineDisplayCancellationSource?.Dispose();
            onlineDisplayCancellationSource = null;
            base.Dispose(isDisposing);
        }
    }
}
