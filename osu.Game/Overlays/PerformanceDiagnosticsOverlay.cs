// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Input.Bindings;
using osu.Game.Localisation;
using osu.Game.Performance.Diagnostics;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays
{
    [Cached(typeof(PerformanceDiagnosticsOverlay))]
    public partial class PerformanceDiagnosticsOverlay : OsuFocusedOverlayContainer
    {
        private const double enter_duration = 400;
        private const double exit_duration = 200;

        protected override string PopInSampleName => @"UI/overlay-big-pop-in";
        protected override string PopOutSampleName => @"UI/overlay-big-pop-out";

        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Blue);

        private FillFlowContainer setupContent = null!;
        private FillFlowContainer progressContent = null!;
        private FillFlowContainer resultsContent = null!;
        private BindableBool useDefaultSkin = null!;
        private BindableBool exportZip = null!;
        private RoundedButton primaryButton = null!;
        private RoundedButton quickModeButton = null!;
        private RoundedButton deepModeButton = null!;
        private RoundedButton rendererModeButton = null!;
        private OsuTextFlowContainer modeDescription = null!;
        private MosuDiagnosticsMode selectedMode = MosuDiagnosticsMode.Quick;
        private OsuColour colours = null!;

        // The diagnostics runner may request presentation while this overlay is still queued behind other asynchronous overlay loads.
        private readonly object presentationLock = new object();
        private Action? pendingPresentation;
        private bool presentationReady;

        [Resolved(CanBeNull = true)]
        private IPerformanceDiagnosticsManager? diagnosticsManager { get; set; }

        public PerformanceDiagnosticsOverlay()
        {
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
            RelativeSizeAxes = Axes.Both;
            Size = new Vector2(0.72f, 0.82f);
            Masking = true;
            CornerRadius = 12;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            this.colours = colours;
            useDefaultSkin = new BindableBool(true);
            exportZip = new BindableBool(true);

            Children = new Drawable[]
            {
                new Box
                {
                    Colour = colours.GreySeaFoamDark,
                    RelativeSizeAxes = Axes.Both,
                },
                new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding(20),
                    RowDimensions = new[]
                    {
                        new Dimension(GridSizeMode.AutoSize),
                        new Dimension(),
                        new Dimension(GridSizeMode.AutoSize),
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            new Container
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Children = new Drawable[]
                                {
                                    new OsuSpriteText
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        Text = ForkSettingsStrings.DiagnosticsOverlayTitle,
                                        Font = OsuFont.GetFont(size: 28, weight: FontWeight.Bold),
                                    },
                                    new IconButton
                                    {
                                        Anchor = Anchor.CentreRight,
                                        Origin = Anchor.CentreRight,
                                        Icon = FontAwesome.Solid.Times,
                                        Action = closeFromHeader,
                                    },
                                },
                            },
                        },
                        new Drawable[]
                        {
                            new OsuScrollContainer
                            {
                                RelativeSizeAxes = Axes.Both,
                                Child = new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Vertical,
                                    Spacing = new Vector2(0, 12),
                                    Children = new Drawable[]
                                    {
                                        setupContent = new FillFlowContainer
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Direction = FillDirection.Vertical,
                                            Spacing = new Vector2(0, 12),
                                            Children = new Drawable[]
                                            {
                                                createParagraph(ForkSettingsStrings.DiagnosticsOverlayDescription, 16, colours.Gray9),
                                                new OsuSpriteText
                                                {
                                                    Text = ForkSettingsStrings.DiagnosticsModeSelectionTitle,
                                                    Font = OsuFont.GetFont(size: 18, weight: FontWeight.Bold),
                                                },
                                                quickModeButton = createModeButton(ForkSettingsStrings.DiagnosticsQuickModeButton, MosuDiagnosticsMode.Quick),
                                                deepModeButton = createModeButton(ForkSettingsStrings.DiagnosticsDeepModeButton, MosuDiagnosticsMode.Deep),
                                                rendererModeButton = createModeButton(ForkSettingsStrings.DiagnosticsRendererModeButton, MosuDiagnosticsMode.RendererComparison),
                                                modeDescription = createParagraphContainer(14, colours.Gray7),
                                                createCheckbox(ForkSettingsStrings.DiagnosticsUseDefaultSkinCaption, useDefaultSkin),
                                                createParagraph(ForkSettingsStrings.DiagnosticsUseDefaultSkinHint, 13, colours.Gray7),
                                                createCheckbox(ForkSettingsStrings.DiagnosticsExportZipCaption, exportZip),
                                                createParagraph(ForkSettingsStrings.DiagnosticsExportZipHint, 13, colours.Gray7),
                                            },
                                        },
                                        progressContent = new FillFlowContainer
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Direction = FillDirection.Vertical,
                                            Spacing = new Vector2(0, 10),
                                            Alpha = 0,
                                        },
                                        resultsContent = new FillFlowContainer
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Direction = FillDirection.Vertical,
                                            Spacing = new Vector2(0, 8),
                                            Alpha = 0,
                                        },
                                    },
                                },
                            },
                        },
                        new Drawable[]
                        {
                            primaryButton = new RoundedButton
                            {
                                RelativeSizeAxes = Axes.X,
                                Text = ForkSettingsStrings.DiagnosticsStartButton,
                                Action = startDiagnostics,
                            },
                        },
                    },
                },
            };

            selectMode(MosuDiagnosticsMode.Quick);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Action? presentation;

            lock (presentationLock)
            {
                presentationReady = true;
                presentation = pendingPresentation;
                pendingPresentation = null;
            }

            presentation?.Invoke();
        }

        private static Drawable createCheckbox(LocalisableString caption, BindableBool bindable)
        {
            return new LabelledSwitchButton
            {
                Label = caption,
                Current = bindable,
            };
        }

        private RoundedButton createModeButton(LocalisableString text, MosuDiagnosticsMode mode) => new RoundedButton
        {
            RelativeSizeAxes = Axes.X,
            Height = 46,
            Text = text,
            Action = () => selectMode(mode),
        };

        private void selectMode(MosuDiagnosticsMode mode)
        {
            selectedMode = mode;
            quickModeButton.BackgroundColour = mode == MosuDiagnosticsMode.Quick ? colours.Blue3 : colours.Gray4;
            deepModeButton.BackgroundColour = mode == MosuDiagnosticsMode.Deep ? colours.Pink3 : colours.Gray4;
            rendererModeButton.BackgroundColour = mode == MosuDiagnosticsMode.RendererComparison ? colours.Purple3 : colours.Gray4;
            modeDescription.Text = mode switch
            {
                MosuDiagnosticsMode.Deep => ForkSettingsStrings.DiagnosticsDeepModeDescription,
                MosuDiagnosticsMode.Extended => ForkSettingsStrings.DiagnosticsExtendedModeDescription,
                MosuDiagnosticsMode.RendererComparison => ForkSettingsStrings.DiagnosticsRendererModeDescription,
                _ => ForkSettingsStrings.DiagnosticsQuickModeDescription,
            };
        }

        public void ShowSetup() => presentWhenReady(showSetup);

        private void showSetup()
        {
            setupContent.Show();
            progressContent.Clear();
            progressContent.Hide();
            resultsContent.Clear();
            resultsContent.Hide();
            configurePrimaryButton(ForkSettingsStrings.DiagnosticsStartButton, startDiagnostics, colours.Blue3);
            Show();
        }

        public void ShowProgress(MosuDiagnosticsSession session) => presentWhenReady(() => showProgress(session));

        private void showProgress(MosuDiagnosticsSession session)
        {
            setupContent.Hide();
            resultsContent.Clear();
            resultsContent.Hide();
            progressContent.Clear();
            progressContent.Show();

            int total = Math.Max(1, session.TotalSteps);
            int completed = Math.Min(session.CompletedSteps, total);
            float progress = Math.Clamp((float)completed / total, 0, 1);

            progressContent.Add(new OsuSpriteText
            {
                Text = ForkSettingsStrings.DiagnosticsProgressTitle,
                Font = OsuFont.GetFont(size: 22, weight: FontWeight.Bold),
            });
            progressContent.Add(new OsuSpriteText
            {
                Text = ForkSettingsStrings.DiagnosticsProgressSummary(
                    completed.ToString(),
                    total.ToString(),
                    getModeName(session.Options.Mode)),
                Font = OsuFont.GetFont(size: 16),
                Colour = colours.Gray9,
            });
            progressContent.Add(createProgressBar(progress));

            if (completed < total)
            {
                double secondsPerStep = MosuDiagnosticsDefaults.EstimatedStepDurationSeconds;

                if (completed > 0)
                {
                    double elapsedSeconds = Math.Max(0, (DateTimeOffset.Now - session.StartedAt).TotalSeconds);
                    secondsPerStep = Math.Clamp(elapsedSeconds / completed, 60, 180);
                }

                int remainingMinutes = Math.Max(1, (int)Math.Ceiling((total - completed) * secondsPerStep / 60));
                progressContent.Add(createParagraph(
                    ForkSettingsStrings.DiagnosticsProgressRemaining(remainingMinutes.ToString(CultureInfo.CurrentCulture)),
                    14,
                    colours.Gray9));
            }

            if (session.ExpectedRenderer != null && session.ExpectedProfile != null)
            {
                progressContent.Add(createCard(
                    ForkSettingsStrings.DiagnosticsCurrentStepTitle,
                    ForkSettingsStrings.DiagnosticsCurrentStep(
                        getRendererName(session.ExpectedRenderer.Value),
                        getProfileName(session.ExpectedProfile.Value),
                        (completed + 1).ToString(),
                        total.ToString()),
                    colours.Blue4,
                    colours.Blue0));
            }

            progressContent.Add(createParagraph(ForkSettingsStrings.DiagnosticsProgressHint, 14, colours.Gray7));

            configurePrimaryButton(ForkSettingsStrings.DiagnosticsCancelButton, cancelDiagnostics, colours.Red3);
            Show();
        }

        public void ShowPaused(MosuDiagnosticsSession session) => presentWhenReady(() => showPaused(session));

        private void showPaused(MosuDiagnosticsSession session)
        {
            setupContent.Hide();
            resultsContent.Clear();
            resultsContent.Hide();
            progressContent.Clear();
            progressContent.Show();

            int total = Math.Max(1, session.TotalSteps);
            int completed = Math.Min(session.CompletedSteps, total);

            progressContent.Add(new OsuSpriteText
            {
                Text = ForkSettingsStrings.DiagnosticsPausedTitle,
                Font = OsuFont.GetFont(size: 22, weight: FontWeight.Bold),
            });
            progressContent.Add(createParagraph(
                ForkSettingsStrings.DiagnosticsPausedDescription(completed.ToString(), total.ToString()),
                15,
                colours.Gray9));

            if (session.ExpectedRenderer != null && session.ExpectedProfile != null)
            {
                progressContent.Add(createCard(
                    ForkSettingsStrings.DiagnosticsPausedStageTitle,
                    ForkSettingsStrings.DiagnosticsCurrentStep(
                        getRendererName(session.ExpectedRenderer.Value),
                        getProfileName(session.ExpectedProfile.Value),
                        (completed + 1).ToString(),
                        total.ToString()),
                    colours.Blue4,
                    colours.Blue0));
            }

            progressContent.Add(new RoundedButton
            {
                RelativeSizeAxes = Axes.X,
                Height = 42,
                Text = ForkSettingsStrings.DiagnosticsCancelButton,
                BackgroundColour = colours.Red3,
                Action = cancelDiagnostics,
            });

            configurePrimaryButton(ForkSettingsStrings.DiagnosticsResumeButton, resumeDiagnostics, colours.Green3);
            Show();
        }

        public void ShowResults(MosuDiagnosticsSession session) => presentWhenReady(() => showResults(session));

        private void showResults(MosuDiagnosticsSession session)
        {
            setupContent.Hide();
            progressContent.Clear();
            progressContent.Hide();
            resultsContent.Clear();
            resultsContent.Show();

            resultsContent.Add(new OsuSpriteText
            {
                Text = ForkSettingsStrings.DiagnosticsResultsTitle,
                Font = OsuFont.GetFont(size: 22, weight: FontWeight.Bold),
            });

            resultsContent.Add(createParagraph(
                ForkSettingsStrings.DiagnosticsSessionSummary(
                    session.SessionId,
                    session.Renderers.Count.ToString(),
                    session.Results.Count(r => r.Completed).ToString()),
                14,
                colours.Gray9));

            switch (session.Options.Mode)
            {
                case MosuDiagnosticsMode.Deep:
                case MosuDiagnosticsMode.Extended:
                    addDeepDiagnosticsResults(session);
                    break;

                case MosuDiagnosticsMode.RendererComparison:
                    addRendererComparisonResults(session);
                    break;

                default:
                    addRecommendationSummary(session);

                    foreach (var rendererGroup in session.Results.GroupBy(r => r.Renderer))
                        resultsContent.Add(createRendererCard(rendererGroup.Key, rendererGroup.ToList()));

                    break;
            }

            var allStutters = session.Results.SelectMany(r => r.Stutters).ToList();

            if (allStutters.Count > 0)
            {
                MosuDiagnosticsStutterEvent worst = allStutters.MaxBy(stutter => stutter.FrameMs)!;
                resultsContent.Add(createCard(
                    ForkSettingsStrings.DiagnosticsStuttersHeader(allStutters.Count.ToString()),
                    ForkSettingsStrings.DiagnosticsStutterSummary(
                        worst.Thread,
                        worst.FrameMs.ToString(@"0.##", CultureInfo.CurrentCulture)),
                    colours.Orange4,
                    colours.Orange0));
            }

            if (!string.IsNullOrEmpty(session.ExportZipPath))
            {
                Drawable exportPath = createParagraph(ForkSettingsStrings.DiagnosticsExportPath(session.ExportZipPath), 14, colours.Gray9);
                exportPath.Margin = new MarginPadding { Top = 12 };
                resultsContent.Add(exportPath);
                resultsContent.Add(new RoundedButton
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 42,
                    Text = ForkSettingsStrings.DiagnosticsOpenExportFolder,
                    BackgroundColour = colours.Green3,
                    Action = () => diagnosticsManager?.OpenExportFolder(),
                });
            }

            configurePrimaryButton(ForkSettingsStrings.DiagnosticsCloseButton, closeResults, colours.Blue3);
            Show();
        }

        public new void Hide()
        {
            lock (presentationLock)
                pendingPresentation = null;

            base.Hide();
        }

        private void presentWhenReady(Action action)
        {
            lock (presentationLock)
            {
                if (!presentationReady)
                {
                    pendingPresentation = action;
                    return;
                }
            }

            action();
        }

        private void startDiagnostics()
        {
            if (diagnosticsManager == null || diagnosticsManager.IsRunning)
                return;

            diagnosticsManager.BeginSession(new MosuDiagnosticsSetupOptions
            {
                Mode = selectedMode,
                UseDefaultSkin = useDefaultSkin.Value,
                ExportZip = exportZip.Value,
            });
        }

        private void addRecommendationSummary(MosuDiagnosticsSession session)
        {
            var comparisons = session.Results
                                     .Where(r => r.Completed && r.Profile == MosuDiagnosticsProfile.Recommended)
                                     .Select(recommended => new
                                     {
                                         Recommended = recommended,
                                         Current = session.Results.FirstOrDefault(r =>
                                             r.Completed
                                             && r.Renderer == recommended.Renderer
                                             && r.Profile == MosuDiagnosticsProfile.Current),
                                     })
                                     .Where(pair => pair.Current != null)
                                     .Select(pair => new DiagnosticsComparison(pair.Current!, pair.Recommended))
                                     .Where(comparison => worstQuality(comparison.Current.Quality, comparison.Recommended.Quality) <= MosuDiagnosticsQuality.Variable)
                                     .OrderBy(comparison => worstQuality(comparison.Current.Quality, comparison.Recommended.Quality))
                                     .ThenByDescending(comparison => comparison.Recommended.P1Fps ?? 0)
                                     .ThenByDescending(comparison => comparison.Recommended.AvgFps ?? 0)
                                     .ToList();

            if (comparisons.Count == 0)
            {
                resultsContent.Add(createCard(
                    ForkSettingsStrings.DiagnosticsNoRecommendationTitle,
                    ForkSettingsStrings.DiagnosticsNoRecommendationBody,
                    colours.Red4,
                    colours.Red0));
                return;
            }

            DiagnosticsComparison best = comparisons[0];
            bool recommendedWins = best.Score >= 1;
            bool currentWins = best.Score <= -1;
            LocalisableString title = recommendedWins
                ? ForkSettingsStrings.DiagnosticsRecommendedWinsTitle
                : currentWins
                    ? ForkSettingsStrings.DiagnosticsCurrentWinsTitle
                    : ForkSettingsStrings.DiagnosticsSimilarTitle;
            LocalisableString body = ForkSettingsStrings.DiagnosticsComparisonSummary(
                getRendererName(best.Recommended.Renderer),
                formatSigned(best.AvgDelta),
                formatSigned(best.P1Delta),
                (best.Current.StutterCount - best.Recommended.StutterCount).ToString(),
                getQualityName(worstQuality(best.Current.Quality, best.Recommended.Quality)));
            Color4 background = recommendedWins ? colours.Green4 : currentWins ? colours.Orange4 : colours.Blue4;
            Color4 foreground = recommendedWins ? colours.Green0 : currentWins ? colours.Orange0 : colours.Blue0;

            resultsContent.Add(createCard(title, body, background, foreground));

            if (recommendedWins)
            {
                resultsContent.Add(new RoundedButton
                {
                    RelativeSizeAxes = Axes.X,
                    Text = ForkSettingsStrings.DiagnosticsApplyRecommendedButton,
                    BackgroundColour = colours.Green3,
                    Action = () =>
                    {
                        diagnosticsManager?.ApplyRecommendedSettings(best.Recommended.Renderer);
                        closeResults();
                    },
                });
            }
        }

        private void addDeepDiagnosticsResults(MosuDiagnosticsSession session)
        {
            MosuDiagnosticsRendererResult? baseline = session.Results.FirstOrDefault(result =>
                result.Profile == MosuDiagnosticsProfile.CleanBaseline
                && result.Completed
                && result.Quality != MosuDiagnosticsQuality.Failed);
            MosuDiagnosticsRendererResult? classicBaseline = session.Results.FirstOrDefault(result =>
                result.Profile == MosuDiagnosticsProfile.ClassicSkinBaseline
                && result.Completed
                && result.Quality != MosuDiagnosticsQuality.Failed);
            MosuDiagnosticsRendererResult? verificationBaseline = session.Results.FirstOrDefault(result =>
                result.Profile == MosuDiagnosticsProfile.CleanBaselineVerification
                && result.Completed
                && result.Quality != MosuDiagnosticsQuality.Failed);
            if (baseline == null)
            {
                resultsContent.Add(createCard(
                    ForkSettingsStrings.DiagnosticsNoRecommendationTitle,
                    ForkSettingsStrings.DiagnosticsDeepBaselineMissing,
                    colours.Red4,
                    colours.Red0));
                addSkippedProfiles(session);
                return;
            }

            MosuDiagnosticsRendererResult? recommendedResult = session.Results.FirstOrDefault(result =>
                result.Profile == MosuDiagnosticsProfile.Recommended
                && result.Completed
                && result.Quality != MosuDiagnosticsQuality.Failed);
            DeepDiagnosticsComparison? recommendedComparison = session.Profiles.Contains(MosuDiagnosticsProfile.Recommended)
                ? addDeepProfileResult(session, MosuDiagnosticsProfile.Recommended)
                : null;

            addSkippedProfiles(session);

            resultsContent.Add(createCard(
                ForkSettingsStrings.DiagnosticsDeepBaselineTitle,
                ForkSettingsStrings.DiagnosticsDeepBaselineResult(
                    (baseline.AvgFps ?? 0).ToString(@"0", CultureInfo.CurrentCulture),
                    (baseline.P1Fps ?? 0).ToString(@"0", CultureInfo.CurrentCulture),
                    getQualityDetails(baseline)),
                colours.Blue4,
                colours.Blue0));

            if (verificationBaseline != null)
            {
                double baselineDrift = calculatePercentChange(baseline.AvgFps, verificationBaseline.AvgFps);
                resultsContent.Add(createCard(
                    ForkSettingsStrings.DiagnosticsDeepVerificationBaselineTitle,
                    ForkSettingsStrings.DiagnosticsDeepVerificationBaselineResult(
                        (verificationBaseline.AvgFps ?? 0).ToString(@"0", CultureInfo.CurrentCulture),
                        formatSigned(baselineDrift),
                        getQualityDetails(verificationBaseline)),
                    Math.Abs(baselineDrift) >= 10 ? colours.Orange4 : colours.Blue4,
                    Math.Abs(baselineDrift) >= 10 ? colours.Orange0 : colours.Blue0));
            }

            if (baseline.Quality >= MosuDiagnosticsQuality.Variable
                || verificationBaseline?.Quality >= MosuDiagnosticsQuality.Variable)
            {
                resultsContent.Add(createCard(
                    ForkSettingsStrings.DiagnosticsDeepBaselineVariationTitle,
                    ForkSettingsStrings.DiagnosticsDeepBaselineVariation(
                        formatVariation(baseline.AvgFpsVariationPercent),
                        formatVariation(baseline.P1FpsVariationPercent),
                        formatVariation(verificationBaseline?.AvgFpsVariationPercent),
                        formatVariation(verificationBaseline?.P1FpsVariationPercent)),
                    colours.Orange4,
                    colours.Orange0));
            }

            // Older sessions could contain a separate Classic baseline. Keep displaying it
            // for accurate interpretation of an already completed session, but new sessions never switch skins.
            if (classicBaseline != null)
            {
                resultsContent.Add(createCard(
                    ForkSettingsStrings.DiagnosticsDeepClassicBaselineTitle,
                    ForkSettingsStrings.DiagnosticsDeepBaselineResult(
                        (classicBaseline.AvgFps ?? 0).ToString(@"0", CultureInfo.CurrentCulture),
                        (classicBaseline.P1Fps ?? 0).ToString(@"0", CultureInfo.CurrentCulture),
                        getQualityDetails(classicBaseline)),
                    colours.Blue4,
                    colours.Blue0));
            }

            resultsContent.Add(createParagraph(ForkSettingsStrings.DiagnosticsDeepResultsHint, 14, colours.Gray9));

            foreach (MosuDiagnosticsProfile profile in session.Profiles
                                                                      .Where(profile => !MosuDiagnosticsProfiles.IsReferenceProfile(profile))
                                                                      .Where(profile => profile != MosuDiagnosticsProfile.Recommended))
                addDeepProfileResult(session, profile);

            if (recommendedResult != null
                && recommendedComparison != null
                && (recommendedComparison.ComparisonQuality <= MosuDiagnosticsQuality.Variable
                    ? recommendedComparison.Score >= 1
                    : recommendedComparison.Score >= 5))
            {
                addApplyRecommendedButton(recommendedResult.Renderer);
            }
        }

        private void addSkippedProfiles(MosuDiagnosticsSession session)
        {
            if (session.SkippedProfiles.Count == 0)
                return;

            var lines = session.SkippedProfiles
                               .Select(skipped => ForkSettingsStrings.DiagnosticsSkippedProfile(
                                   getProfileName(skipped.Profile),
                                   getRendererName(skipped.Renderer),
                                   getSkipReasonName(skipped.Reason)))
                               .ToList();

            resultsContent.Add(createCard(
                ForkSettingsStrings.DiagnosticsSkippedProfilesTitle(lines.Count.ToString(CultureInfo.CurrentCulture)),
                lines,
                colours.Gray4,
                colours.Gray9));
        }

        private DeepDiagnosticsComparison? addDeepProfileResult(MosuDiagnosticsSession session, MosuDiagnosticsProfile profile)
        {
            MosuDiagnosticsRendererResult? result = session.Results.FirstOrDefault(candidate => candidate.Profile == profile);

            if (result == null
                || !result.Completed
                || result.Quality == MosuDiagnosticsQuality.Failed)
            {
                resultsContent.Add(createCard(
                    getProfileName(profile),
                    ForkSettingsStrings.DiagnosticsProfileFailed(getProfileName(profile)),
                    colours.Red4,
                    colours.Red0));
                return null;
            }

            MosuDiagnosticsRendererResult? comparisonBaseline = MosuDiagnosticsAnalysis.CreateComparisonBaseline(session, result);

            if (comparisonBaseline == null)
            {
                resultsContent.Add(createCard(
                    getProfileName(profile),
                    ForkSettingsStrings.DiagnosticsDeepBaselineMissing,
                    colours.Red4,
                    colours.Red0));
                return null;
            }

            var comparison = new DeepDiagnosticsComparison(comparisonBaseline, result);
            DeepImpact impact = classifyDeepImpact(comparison);
            (LocalisableString label, Color4 background, Color4 foreground) = getDeepImpactAppearance(impact);
            LocalisableString title = profile == MosuDiagnosticsProfile.Recommended
                ? ForkSettingsStrings.DiagnosticsDeepRecommendedComparisonTitle
                : getProfileName(profile);

            var bodyLines = new List<LocalisableString>
            {
                ForkSettingsStrings.DiagnosticsDeepProfileResult(
                    label,
                    (result.AvgFps ?? 0).ToString(@"0", CultureInfo.CurrentCulture),
                    (comparisonBaseline.AvgFps ?? 0).ToString(@"0", CultureInfo.CurrentCulture),
                    formatSigned(comparison.AvgDelta),
                    (result.P1Fps ?? 0).ToString(@"0", CultureInfo.CurrentCulture),
                    (comparisonBaseline.P1Fps ?? 0).ToString(@"0", CultureInfo.CurrentCulture),
                    formatSigned(comparison.P1Delta),
                    formatSigned(comparison.DrawP99Improvement),
                    formatSigned(comparison.UpdateP99Improvement),
                    formatSigned(comparison.InputP99Improvement),
                    comparison.StutterDelta.ToString(@"+0;-0;0", CultureInfo.CurrentCulture),
                    getQualityDetails(result)),
            };

            if (profile == MosuDiagnosticsProfile.Use8kPollingRate)
                bodyLines.Add(ForkSettingsStrings.DiagnosticsInputPollingScope);
            else if (profile == MosuDiagnosticsProfile.AllowTearing)
                bodyLines.Add(ForkSettingsStrings.DiagnosticsTearingScope);

            resultsContent.Add(createCard(
                title,
                bodyLines,
                background,
                foreground));

            return comparison;
        }

        private void addRendererComparisonResults(MosuDiagnosticsSession session)
        {
            var candidates = session.Results
                                    .Where(result => result.Profile == MosuDiagnosticsProfile.Recommended
                                                     && result.Completed
                                                     && result.Quality <= MosuDiagnosticsQuality.Variable)
                                    .OrderByDescending(result => result.P1Fps ?? 0)
                                    .ThenByDescending(result => result.AvgFps ?? 0)
                                    .ThenBy(result => result.Quality)
                                    .ToList();

            if (candidates.Count == 0)
            {
                resultsContent.Add(createCard(
                    ForkSettingsStrings.DiagnosticsNoRecommendationTitle,
                    ForkSettingsStrings.DiagnosticsNoRecommendationBody,
                    colours.Red4,
                    colours.Red0));
            }
            else
            {
                MosuDiagnosticsRendererResult best = candidates[0];
                resultsContent.Add(createCard(
                    ForkSettingsStrings.DiagnosticsRendererWinnerTitle,
                    ForkSettingsStrings.DiagnosticsRendererWinnerResult(
                        getRendererName(best.Renderer),
                        (best.AvgFps ?? 0).ToString(@"0", CultureInfo.CurrentCulture),
                        (best.P1Fps ?? 0).ToString(@"0", CultureInfo.CurrentCulture),
                        best.StutterCount.ToString(),
                        getQualityName(best.Quality)),
                    colours.Green4,
                    colours.Green0));
                addApplyRecommendedButton(best.Renderer);
            }

            foreach (var rendererGroup in session.Results.GroupBy(result => result.Renderer))
                resultsContent.Add(createRendererCard(rendererGroup.Key, rendererGroup.ToList()));
        }

        private void addApplyRecommendedButton(RendererType renderer)
        {
            resultsContent.Add(new RoundedButton
            {
                RelativeSizeAxes = Axes.X,
                Text = ForkSettingsStrings.DiagnosticsApplyRecommendedButton,
                BackgroundColour = colours.Green3,
                Action = () =>
                {
                    diagnosticsManager?.ApplyRecommendedSettings(renderer);
                    closeResults();
                },
            });
        }

        private DeepImpact classifyDeepImpact(DeepDiagnosticsComparison comparison)
        {
            double[] metrics =
            {
                comparison.AvgDelta,
                comparison.P1Delta,
                comparison.DrawP99Improvement,
                comparison.UpdateP99Improvement,
                comparison.InputP99Improvement,
            };

            if (Math.Abs(comparison.Score) < 2
                && metrics.Any(value => value >= 5)
                && metrics.Any(value => value <= -5))
                return DeepImpact.TradeOff;

            if (comparison.Score >= 5)
                return DeepImpact.High;

            if (comparison.Score >= 2)
                return DeepImpact.Medium;

            if (comparison.Score >= 0.5)
                return DeepImpact.Small;

            if (comparison.Score <= -0.5)
                return DeepImpact.Negative;

            return DeepImpact.Neutral;
        }

        private (LocalisableString Label, Color4 Background, Color4 Foreground) getDeepImpactAppearance(DeepImpact impact) => impact switch
        {
            DeepImpact.High => (ForkSettingsStrings.PerformanceImpactHigh, colours.Green4, colours.Green0),
            DeepImpact.Medium => (ForkSettingsStrings.PerformanceImpactMedium, colours.Lime4, colours.Lime0),
            DeepImpact.Small => (ForkSettingsStrings.PerformanceImpactLow, colours.Blue4, colours.Blue0),
            DeepImpact.TradeOff => (ForkSettingsStrings.PerformanceImpactMixed, colours.DarkOrange4, colours.DarkOrange0),
            DeepImpact.Negative => (ForkSettingsStrings.PerformanceImpactNegative, colours.Red4, colours.Red0),
            _ => (ForkSettingsStrings.DiagnosticsImpactNeutral, colours.Purple4, colours.Purple0),
        };

        private Drawable createRendererCard(RendererType renderer, IReadOnlyList<MosuDiagnosticsRendererResult> rendererResults)
        {
            var lines = new List<LocalisableString>();

            foreach (var result in rendererResults.OrderBy(r => r.Profile))
            {
                if (!result.Completed || result.Quality == MosuDiagnosticsQuality.Failed)
                {
                    lines.Add(ForkSettingsStrings.DiagnosticsProfileFailed(getProfileName(result.Profile)));
                    continue;
                }

                lines.Add(ForkSettingsStrings.DiagnosticsProfileResult(
                    getProfileName(result.Profile),
                    (result.AvgFps ?? 0).ToString(@"0", CultureInfo.CurrentCulture),
                    (result.P1Fps ?? 0).ToString(@"0", CultureInfo.CurrentCulture),
                    result.StutterCount.ToString(),
                    getQualityName(result.Quality)));
            }

            return createCard(getRendererName(renderer), lines, colours.Purple4, colours.Purple0);
        }

        private static Drawable createProgressBar(float progress)
        {
            return new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = 14,
                CornerRadius = 7,
                Masking = true,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.Black,
                        Alpha = 0.35f,
                    },
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Width = progress,
                        Colour = Color4.DeepSkyBlue,
                    },
                },
            };
        }

        private static Drawable createParagraph(LocalisableString content, float size, Color4 colour)
        {
            var paragraph = createParagraphContainer(size, colour);
            paragraph.AddText(content);
            return paragraph;
        }

        private static OsuTextFlowContainer createParagraphContainer(float size, Color4 colour) =>
            new OsuTextFlowContainer(sprite => sprite.Font = OsuFont.GetFont(size: size))
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Colour = colour,
            };

        private static Drawable createCard(LocalisableString title, LocalisableString body, Color4 background, Color4 foreground)
            => createCard(title, new[] { body }, background, foreground);

        private static Drawable createCard(LocalisableString title, IReadOnlyList<LocalisableString> bodyLines, Color4 background, Color4 foreground)
        {
            var text = new OsuTextFlowContainer(sprite => sprite.Font = OsuFont.GetFont(size: 15))
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Padding = new MarginPadding(12),
                Colour = foreground,
            };
            text.AddText(title, sprite => sprite.Font = OsuFont.GetFont(size: 16, weight: FontWeight.Bold));

            foreach (LocalisableString line in bodyLines)
            {
                text.NewLine();
                text.AddText(line);
            }

            return new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                CornerRadius = 8,
                Masking = true,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = background,
                    },
                    text,
                },
            };
        }

        private void configurePrimaryButton(LocalisableString text, Action action, Color4 background)
        {
            primaryButton.Text = text;
            primaryButton.Action = action;
            primaryButton.BackgroundColour = background;
        }

        private void cancelDiagnostics() => diagnosticsManager?.CancelSession();

        private void resumeDiagnostics() => diagnosticsManager?.ResumeSession();

        private void closeResults() => diagnosticsManager?.DismissResults();

        private void closeFromHeader()
        {
            if (diagnosticsManager?.CurrentSession?.Phase == MosuDiagnosticsPhase.Paused)
                Hide();
            else if (diagnosticsManager?.IsRunning == true)
                cancelDiagnostics();
            else if (resultsContent.IsPresent)
                closeResults();
            else
                Hide();
        }

        private static string getRendererName(RendererType renderer) => renderer.ToString().Replace('_', ' ');

        private static LocalisableString getModeName(MosuDiagnosticsMode mode) => mode switch
        {
            MosuDiagnosticsMode.Deep => ForkSettingsStrings.DiagnosticsModeDeep,
            MosuDiagnosticsMode.Extended => ForkSettingsStrings.DiagnosticsModeExtended,
            MosuDiagnosticsMode.RendererComparison => ForkSettingsStrings.DiagnosticsModeRenderers,
            _ => ForkSettingsStrings.DiagnosticsModeQuick,
        };

        private static LocalisableString getProfileName(MosuDiagnosticsProfile profile) => profile switch
        {
            MosuDiagnosticsProfile.Current => ForkSettingsStrings.DiagnosticsProfileCurrent,
            MosuDiagnosticsProfile.Recommended => ForkSettingsStrings.DiagnosticsProfileRecommended,
            MosuDiagnosticsProfile.CleanBaseline => ForkSettingsStrings.DiagnosticsProfileBaseline,
            MosuDiagnosticsProfile.ClassicSkinBaseline => ForkSettingsStrings.DiagnosticsProfileClassicBaseline,
            MosuDiagnosticsProfile.CleanBaselineVerification => ForkSettingsStrings.DiagnosticsProfileVerificationBaseline,
            MosuDiagnosticsProfile.WindowsUltraPerformanceMode => ForkSettingsStrings.WindowsUltraPerfCaption,
            MosuDiagnosticsProfile.Use8kPollingRate => ForkSettingsStrings.Use8kPollingRateCaption,
            MosuDiagnosticsProfile.LargeTextureAtlas => ForkSettingsStrings.LargeTextureAtlasCaption,
            MosuDiagnosticsProfile.DeferredVertexUploadBatching => ForkSettingsStrings.DeferredVertexBatchingCaption,
            MosuDiagnosticsProfile.DeferredDirectVertexUpload => ForkSettingsStrings.DeferredDirectVertexUploadCaption,
            MosuDiagnosticsProfile.DeferredDirectUniformUpload => ForkSettingsStrings.DeferredDirectUniformUploadCaption,
            MosuDiagnosticsProfile.VeldridPipelineLookupCache => ForkSettingsStrings.VeldridPipelineLookupCacheCaption,
            MosuDiagnosticsProfile.StaticChildLifetimeCache => ForkSettingsStrings.StaticChildLifetimeCacheCaption,
            MosuDiagnosticsProfile.AllowTearing => ForkSettingsStrings.AllowTearingCaption,
            MosuDiagnosticsProfile.UpdateThreadSpinWait => ForkSettingsStrings.UpdateSpinWaitCaption,
            MosuDiagnosticsProfile.SkinPerformanceMode => ForkSettingsStrings.DiagnosticsProfileSkinPackage,
            _ => profile.ToString(),
        };

        private static LocalisableString getSkipReasonName(MosuDiagnosticsSkipReason reason) => reason switch
        {
            MosuDiagnosticsSkipReason.RequiresWindows => ForkSettingsStrings.DiagnosticsSkipRequiresWindows,
            MosuDiagnosticsSkipReason.RequiresDeferredRenderer => ForkSettingsStrings.DiagnosticsSkipRequiresDeferredRenderer,
            MosuDiagnosticsSkipReason.RequiresNonDeferredRenderer => ForkSettingsStrings.DiagnosticsSkipRequiresNonDeferredRenderer,
            _ => reason.ToString(),
        };

        private static LocalisableString getQualityName(MosuDiagnosticsQuality quality) => quality switch
        {
            MosuDiagnosticsQuality.Reliable => ForkSettingsStrings.DiagnosticsQualityReliable,
            MosuDiagnosticsQuality.SingleRun => ForkSettingsStrings.DiagnosticsQualitySingleRun,
            MosuDiagnosticsQuality.Variable => ForkSettingsStrings.DiagnosticsQualityVariable,
            MosuDiagnosticsQuality.Unstable => ForkSettingsStrings.DiagnosticsQualityUnstable,
            _ => ForkSettingsStrings.DiagnosticsQualityFailed,
        };

        private static LocalisableString getQualityDetails(MosuDiagnosticsRendererResult result) =>
            ForkSettingsStrings.DiagnosticsQualityDetails(
                getQualityName(result.Quality),
                formatVariation(result.AvgFpsVariationPercent),
                formatVariation(result.P1FpsVariationPercent));

        private static MosuDiagnosticsQuality worstQuality(MosuDiagnosticsQuality first, MosuDiagnosticsQuality second) =>
            (MosuDiagnosticsQuality)Math.Max((int)first, (int)second);

        private static string formatSigned(double value) => value.ToString(@"+0.0;-0.0;0.0", CultureInfo.CurrentCulture) + '%';

        private static string formatVariation(double? value) =>
            value != null ? value.Value.ToString(@"0.0", CultureInfo.CurrentCulture) + '%' : @"n/a";

        private static double calculatePercentChange(double? baseline, double? candidate) =>
            baseline is > 0 && candidate != null ? (candidate.Value - baseline.Value) / baseline.Value * 100 : 0;

        private sealed class DiagnosticsComparison
        {
            public MosuDiagnosticsRendererResult Current { get; }
            public MosuDiagnosticsRendererResult Recommended { get; }
            public double AvgDelta { get; }
            public double P1Delta { get; }
            public double Score => AvgDelta * 0.35 + P1Delta * 0.65;

            public DiagnosticsComparison(MosuDiagnosticsRendererResult current, MosuDiagnosticsRendererResult recommended)
            {
                Current = current;
                Recommended = recommended;
                AvgDelta = percentChange(current.AvgFps, recommended.AvgFps);
                P1Delta = percentChange(current.P1Fps, recommended.P1Fps);
            }

            private static double percentChange(double? baseline, double? candidate) =>
                baseline is > 0 && candidate != null ? (candidate.Value - baseline.Value) / baseline.Value * 100 : 0;
        }

        private sealed class DeepDiagnosticsComparison
        {
            public MosuDiagnosticsQuality ComparisonQuality { get; }
            public double AvgDelta { get; }
            public double P1Delta { get; }
            public double DrawP99Improvement { get; }
            public double UpdateP99Improvement { get; }
            public double InputP99Improvement { get; }
            public int StutterDelta { get; }
            public double Score => AvgDelta * 0.3
                                   + P1Delta * 0.4
                                   + DrawP99Improvement * 0.15
                                   + UpdateP99Improvement * 0.1
                                   + InputP99Improvement * 0.05;

            public DeepDiagnosticsComparison(MosuDiagnosticsRendererResult baseline, MosuDiagnosticsRendererResult candidate)
            {
                ComparisonQuality = worstQuality(baseline.Quality, candidate.Quality);
                AvgDelta = increasePercent(baseline.AvgFps, candidate.AvgFps);
                P1Delta = increasePercent(baseline.P1Fps, candidate.P1Fps);
                DrawP99Improvement = decreasePercent(
                    baseline.DrawWorkP99Ms ?? baseline.DrawP99Ms,
                    candidate.DrawWorkP99Ms ?? candidate.DrawP99Ms);
                UpdateP99Improvement = decreasePercent(
                    baseline.UpdateWorkP99Ms ?? baseline.UpdateP99Ms,
                    candidate.UpdateWorkP99Ms ?? candidate.UpdateP99Ms);
                InputP99Improvement = decreasePercent(baseline.InputP99Ms, candidate.InputP99Ms);
                StutterDelta = baseline.StutterCount - candidate.StutterCount;
            }

            private static double increasePercent(double? baseline, double? candidate) =>
                baseline is > 0 && candidate != null ? (candidate.Value - baseline.Value) / baseline.Value * 100 : 0;

            private static double decreasePercent(double? baseline, double? candidate) =>
                baseline is > 0 && candidate != null ? (baseline.Value - candidate.Value) / baseline.Value * 100 : 0;
        }

        private enum DeepImpact
        {
            High,
            Medium,
            Small,
            Neutral,
            TradeOff,
            Negative,
        }

        protected override void PopIn()
        {
            this.FadeIn(enter_duration, Easing.OutQuint);
            this.ScaleTo(0.9f).Then().ScaleTo(1f, enter_duration, Easing.OutQuint);
        }

        protected override void PopOut()
        {
            base.PopOut();
            this.FadeOut(exit_duration, Easing.InSine);
            this.ScaleTo(0.9f, exit_duration);
        }

        public override bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Action == GlobalAction.Back)
            {
                closeFromHeader();
                return true;
            }

            return base.OnPressed(e);
        }
    }
}
