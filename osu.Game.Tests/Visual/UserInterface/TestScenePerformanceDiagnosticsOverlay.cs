// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osu.Game.Performance.Diagnostics;

namespace osu.Game.Tests.Visual.UserInterface
{
    [TestFixture]
    public partial class TestScenePerformanceDiagnosticsOverlay : OsuTestScene, IOverlayManager
    {
        private readonly Bindable<OverlayActivation> overlayActivationMode = new Bindable<OverlayActivation>(OverlayActivation.All);

        [Test]
        public void TestShowProgressBeforeLoad()
        {
            PerformanceDiagnosticsOverlay overlay = null;

            AddStep("create overlay", () => overlay = new PerformanceDiagnosticsOverlay());
            AddStep("request progress before load", () => overlay.ShowProgress(new MosuDiagnosticsSession
            {
                SessionId = "diagnostics-test",
                Phase = MosuDiagnosticsPhase.PendingRestart,
                Renderers = { RendererType.Deferred_Direct3D11 },
                Profiles = { MosuDiagnosticsProfile.CleanBaseline },
                ExpectedRenderer = RendererType.Deferred_Direct3D11,
                ExpectedProfile = MosuDiagnosticsProfile.CleanBaseline,
            }));
            AddAssert("overlay is not loaded", () => !overlay.IsLoaded);
            AddStep("load overlay", () => LoadComponentAsync(overlay, Add));
            AddUntilStep("overlay is loaded", () => overlay.IsLoaded);
            AddUntilStep("progress is shown", () => overlay.State.Value == Visibility.Visible);
        }

        [Test]
        public void TestDeepResultsShowAbsoluteRecommendedComparison()
        {
            PerformanceDiagnosticsOverlay overlay = null;
            var session = new MosuDiagnosticsSession
            {
                SessionId = "diagnostics-test",
                Phase = MosuDiagnosticsPhase.ShowResults,
                Options = { Mode = MosuDiagnosticsMode.Deep },
                Renderers = { RendererType.Deferred_Direct3D11 },
                Profiles =
                {
                    MosuDiagnosticsProfile.CleanBaseline,
                    MosuDiagnosticsProfile.Use8kPollingRate,
                    MosuDiagnosticsProfile.AllowTearing,
                    MosuDiagnosticsProfile.Recommended,
                    MosuDiagnosticsProfile.CleanBaselineVerification,
                },
                SkippedProfiles =
                {
                    new MosuDiagnosticsSkippedProfile
                    {
                        Renderer = RendererType.Deferred_Direct3D11,
                        Profile = MosuDiagnosticsProfile.VeldridPipelineLookupCache,
                        Reason = MosuDiagnosticsSkipReason.RequiresNonDeferredRenderer,
                    },
                },
                Results =
                {
                    createResult(MosuDiagnosticsProfile.CleanBaseline, 1000, 600, MosuDiagnosticsQuality.Unstable, 1, 12),
                    createResult(MosuDiagnosticsProfile.Use8kPollingRate, 990, 590, MosuDiagnosticsQuality.Reliable, 1, 1),
                    createResult(MosuDiagnosticsProfile.AllowTearing, 1010, 610, MosuDiagnosticsQuality.Reliable, 1, 1),
                    createResult(MosuDiagnosticsProfile.Recommended, 2000, 1200, MosuDiagnosticsQuality.Reliable, 2, 1),
                    createResult(MosuDiagnosticsProfile.CleanBaselineVerification, 1000, 600, MosuDiagnosticsQuality.Variable, 1, 8),
                },
            };

            AddStep("create overlay", () => overlay = new PerformanceDiagnosticsOverlay());
            AddStep("load overlay", () => LoadComponentAsync(overlay, Add));
            AddUntilStep("overlay is loaded", () => overlay.IsLoaded);
            AddStep("show deep results", () => overlay.ShowResults(session));
            AddUntilStep("results are shown", () => overlay.State.Value == Visibility.Visible);
            AddAssert("aggregate comparison has explicit title", () => getDisplayedText(overlay), () =>
                Does.Contain("Complete recommended profile vs all optimisations disabled"));
            AddAssert("aggregate comparison has absolute FPS", () => getDisplayedText(overlay), () =>
                Does.Contain("average 2000 vs 1000 FPS (+100.0%)"));
            AddAssert("aggregate comparison is before baseline details", () =>
                getDisplayedText(overlay).IndexOf("Complete recommended profile vs all optimisations disabled", StringComparison.Ordinal)
                < getDisplayedText(overlay).IndexOf("Clean baseline", StringComparison.Ordinal));
            AddAssert("baseline variation is not copied to candidate", () =>
                getDisplayedText(overlay).Split("high run-to-run variation", StringSplitOptions.None).Length - 1, () => Is.EqualTo(1));
            AddAssert("inapplicable setting remains visible", () => getDisplayedText(overlay), () =>
                Does.Contain("Experimental Veldrid pipeline lookup cache on Deferred Direct3D11: only affects the non-Deferred Veldrid renderer"));
            AddAssert("8000 Hz scope is explicit", () => getDisplayedText(overlay), () =>
                Does.Contain("does not measure mouse sensor or USB latency"));
            AddAssert("tearing scope is explicit", () => getDisplayedText(overlay), () =>
                Does.Contain("effect can depend on window mode, driver and VRR support"));
        }

        private static string getDisplayedText(PerformanceDiagnosticsOverlay overlay) =>
            string.Join(
                ' ',
                string.Join(' ', overlay.ChildrenOfType<OsuSpriteText>().Select(text => text.Text.ToString()))
                      .Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                  .Replace("- ", "-", StringComparison.Ordinal);

        private static MosuDiagnosticsRendererResult createResult(
            MosuDiagnosticsProfile profile,
            double avgFps,
            double p1Fps,
            MosuDiagnosticsQuality quality,
            double avgSpread,
            double p1Spread) => new MosuDiagnosticsRendererResult
            {
                Renderer = RendererType.Deferred_Direct3D11,
                Profile = profile,
                Started = true,
                Completed = true,
                AvgFps = avgFps,
                P1Fps = p1Fps,
                DrawWorkP99Ms = 1,
                UpdateWorkP99Ms = 1,
                InputP99Ms = 1,
                AvgFpsVariationPercent = avgSpread,
                P1FpsVariationPercent = p1Spread,
                Quality = quality,
            };

        public IBindable<OverlayActivation> OverlayActivationMode => overlayActivationMode;

        public IDisposable RegisterBlockingOverlay(OverlayContainer overlayContainer) => throw new NotImplementedException();

        public void ShowBlockingOverlay(OverlayContainer overlay)
        {
        }

        public void HideBlockingOverlay(OverlayContainer overlay)
        {
        }
    }
}
