// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Performance;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.UI
{
    public partial class AimAssistTargetDisplay : CompositeDrawable
    {
        private const float current_marker_padding = 0f;
        private const float next_marker_padding = 0f;
        private const int max_flow_debug_segments = 192;

        private readonly BindableBool showTargets = new BindableBool();
        private readonly BindableBool showFlowDebug = new BindableBool();
        private readonly AimAssistController aimAssistController;

        private Container infoContainer = null!;
        private Container flowPathContainer = null!;
        private CircularContainer currentTargetMarker = null!;
        private CircularContainer assistRadiusMarker = null!;
        private CircularContainer currentAssistPointMarker = null!;
        private CircularContainer nextTargetMarker = null!;
        private CircularContainer flowAnchorMarker = null!;
        private CircularContainer rawCursorMarker = null!;
        private CircularContainer assistedCursorMarker = null!;
        private Box offsetLine = null!;
        private FillFlowContainer<OsuSpriteText> infoTextFlow = null!;
        private Box[] flowDebugSegments = Array.Empty<Box>();
        private bool flowDebugSegmentsAttached;

        public AimAssistTargetDisplay(AimAssistController aimAssistController)
        {
            this.aimAssistController = aimAssistController;

            // AlwaysPresent keeps this whole subtree inside UpdateSubTree and GenerateDrawNodeSubtree even at
            // Alpha 0 (CompositeDrawable.UpdateSubTree bails on `!IsPresent`). The debug visuals are off for
            // effectively every real session, so instead of holding the tree open we let AimAssistController --
            // which runs every frame regardless -- push us back to visible via SetDebugVisible().
            if (!MosuOptimisationToggles.LazyDebugOverlays)
                AlwaysPresent = true;
        }

        /// <summary>
        /// Called from <see cref="AimAssistController"/> so this display can be revived from its dormant
        /// (non-present, untraversed) state when debug visuals are switched on mid-session.
        /// </summary>
        public void SetDebugVisible(bool visible)
        {
            if (visible)
                Alpha = 1;
            else if (Alpha != 0)
            {
                detachFlowDebugSegments();
                Alpha = 0;
            }
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            aimAssistController.AttachDebugDisplay(this);

            config.BindWith(OsuSetting.ForkAimAssistShowTargets, showTargets);
            config.BindWith(OsuSetting.ForkAimAssistShowFlowDebug, showFlowDebug);

            InternalChildren = new Drawable[]
            {
                flowPathContainer = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                },
                nextTargetMarker = createMarker(Color4.Gold, 2, 0f),
                assistRadiusMarker = createMarker(Color4.Orange, 2, 0f),
                currentTargetMarker = createMarker(Color4.LimeGreen, 2, 0f), // thin ring to clearly show the target radius boundary
                currentAssistPointMarker = createPointMarker(),
                flowAnchorMarker = createAnchorMarker(),
                rawCursorMarker = createCursorMarker(Color4.Red, 2), // real/physical cursor (hollow style)
                assistedCursorMarker = createCursorMarker(Color4.Cyan, 3),
                offsetLine = new Box { Depth = -1000f },
                infoContainer = new Container
                {
                    AutoSizeAxes = Axes.Both,
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                    Margin = new MarginPadding(12),
                    Alpha = 0,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = new Color4(12, 16, 20, 200)
                        },
                        infoTextFlow = new FillFlowContainer<OsuSpriteText>
                        {
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Vertical,
                            Padding = new MarginPadding(8)
                        }
                    }
                }
            };

            // Configure the offset line (real vs assist visual)
            offsetLine.Anchor = Anchor.TopLeft;
            offsetLine.Origin = Anchor.CentreLeft;
            offsetLine.Colour = Color4.Cyan;
            offsetLine.Alpha = 0;

            // NOTE: Depths are set inside the creation expressions (createCursorMarker + inline for offsetLine below).
            // Setting .Depth AFTER InternalChildren assignment throws InvalidOperationException (see runtime log).
            // Do NOT assign Depth here.

            showTargets.BindValueChanged(_ => updateDisplayPresence(), true);
            showFlowDebug.BindValueChanged(_ => updateDisplayPresence(), true);
        }

        protected override void Update()
        {
            base.Update();

            updateDisplayPresence();

            bool showMarkerDebug = showTargets.Value || aimAssistController.ForceShowDebugFromMod;
            bool showFlowOverlay = showFlowDebug.Value || aimAssistController.ForceShowDebugFromMod;

            updateRawAssistedMarkers();  // the function will decide internally based on Force or other

            if (!isDebugOverlayVisible && !aimAssistController.ForceShowDebugFromMod)
                return;

            bool showTargetAndRadius = showMarkerDebug || aimAssistController.ForceShowDebugFromMod;
            if (!showTargetAndRadius)
            {
                currentTargetMarker.Hide();
                assistRadiusMarker.Hide();
                currentAssistPointMarker.Hide();
                nextTargetMarker.Hide();
            }
            else
            {
                updateMarker(currentTargetMarker, aimAssistController.CurrentTargetPosition, aimAssistController.CurrentTargetRadius, current_marker_padding);
                updateMarker(assistRadiusMarker, aimAssistController.CurrentTargetPosition, aimAssistController.CurrentAssistRadius, 0);
                updatePointMarker(currentAssistPointMarker, aimAssistController.CurrentAssistPointPosition);
                updateMarker(nextTargetMarker, aimAssistController.CurrentNextTargetPosition, aimAssistController.CurrentNextTargetRadius, next_marker_padding);
            }

            if (showFlowOverlay)
                updateFlowDebug();
            else
                detachFlowDebugSegments();

            if (!showMarkerDebug && !showFlowOverlay && !aimAssistController.ForceShowDebugFromMod)
            {
                infoContainer.Hide();
                return;
            }

            bool effectiveShowMarkerForInfo = showMarkerDebug || aimAssistController.ForceShowDebugFromMod;
            updateInfo(effectiveShowMarkerForInfo, showFlowOverlay);
        }


        private bool isDebugOverlayVisible => showTargets.Value || showFlowDebug.Value || aimAssistController.ForceShowDebugFromMod;

        private void updateDisplayPresence() => SetDebugVisible(isDebugOverlayVisible || aimAssistController.ForceShowDebugFromMod);

        private void updateMarker(CircularContainer marker, Vector2? screenSpacePosition, float radius, float padding)
        {
            if (screenSpacePosition == null || radius <= 0)
            {
                marker.Hide();
                return;
            }

            marker.Show();
            marker.Position = ToLocalSpace(screenSpacePosition.Value);
            float localRadius = toLocalRadius(screenSpacePosition.Value, radius);
            float localPadding = toLocalRadius(screenSpacePosition.Value, padding);
            float markerSize = localRadius * 2 + localPadding * 2;
            marker.Size = new Vector2(markerSize, markerSize);
        }

        private float toLocalRadius(Vector2 screenSpaceCentre, float screenSpaceRadius)
        {
            Vector2 localCentre = ToLocalSpace(screenSpaceCentre);
            Vector2 localEdge = ToLocalSpace(screenSpaceCentre + new Vector2(screenSpaceRadius, 0));
            return (localEdge - localCentre).Length;
        }

        private void updateInfo(bool showMarkerDebug, bool showFlowDebugOverlay)
        {
            infoContainer.Show();
            // Make the info box more visible when forced from mod
            infoContainer.Alpha = aimAssistController.ForceShowDebugFromMod ? 1f : 0.9f;
            
            string[] lines;
            if (showMarkerDebug)
            {
                lines = new[]
                {
                    $"mode: {aimAssistController.CurrentModeName}",
                    $"state: {aimAssistController.CurrentAssistStateName}",
                    $"AA: target-gravity / authority: {aimAssistController.CurrentAssistAuthority:P0}",
                    $"markers: red=raw  cyan=assist  green=hitbox  orange=assist field",
                    $"real→assist offset: {(aimAssistController.CurrentOutputPosition - aimAssistController.RawCursorPosition).Length:0.0}px",
                    $"offset: {aimAssistController.CurrentOffsetMagnitude:0.0}px",
                    $"focus delta: {(aimAssistController.CurrentFocusTime.HasValue ? aimAssistController.CurrentFocusTime.Value - Time.Current : double.NaN):0.0}ms",
                    $"radius: {aimAssistController.CurrentTargetRadius:0.0}px (green circle size)",
                    $"base radius: {aimAssistController.CurrentBaseTargetRadius:0.0}px",
                    $"assist radius: {aimAssistController.CurrentAssistRadius:0.0}px",
                    $"adaptive scale: x{aimAssistController.CurrentAdaptiveRadiusScale:0.00}",
                    $"intent: {aimAssistController.CurrentIntentScore:0.00}",
                    $"filters: {(aimAssistController.PassedActivationFilters ? "on" : "off")}",
                    $"anti-jitter: {(aimAssistController.IsAntiJitterActive ? "on" : "off")}",
                    $"anti-aim: {(aimAssistController.IsAntiAimActive ? "on" : "off")}"
                };
            }
            else if (showFlowDebugOverlay)
            {
                lines = new[]
                {
                    $"mode: {aimAssistController.CurrentFlowDebugModeName}",
                    $"AA: target-gravity / authority: {aimAssistController.CurrentAssistAuthority:P0}",
                    $"real→assist offset: {(aimAssistController.CurrentOutputPosition - aimAssistController.RawCursorPosition).Length:0.0}px"
                };
            }
            else
            {
                lines = Array.Empty<string>();
            }

            while (infoTextFlow.Count < lines.Length)
            {
                infoTextFlow.Add(new OsuSpriteText
                {
                    Font = OsuFont.GetFont(size: 14, weight: FontWeight.SemiBold),
                    Colour = Color4.White,
                });
            }

            for (int i = 0; i < infoTextFlow.Count; i++)
            {
                if (i < lines.Length)
                {
                    infoTextFlow[i].Text = lines[i];
                    infoTextFlow[i].Show();
                }
                else
                {
                    infoTextFlow[i].Hide();
                }
            }
        }

        private void updatePointMarker(CircularContainer marker, Vector2? screenSpacePosition)
        {
            if (screenSpacePosition == null)
            {
                marker.Hide();
                return;
            }

            marker.Show();
            marker.Position = ToLocalSpace(screenSpacePosition.Value);
        }

        private void updateFlowDebug()
        {
            ensureFlowDebugSegments();
            updateFlowSegments(aimAssistController.CurrentFlowDebugPathPoints);
            updatePointMarker(flowAnchorMarker, aimAssistController.CurrentFlowDebugAnchorPosition);
        }

        private void ensureFlowDebugSegments()
        {
            if (flowDebugSegments.Length == 0)
            {
                flowDebugSegments = new Box[max_flow_debug_segments];

                for (int i = 0; i < flowDebugSegments.Length; i++)
                    flowDebugSegments[i] = createFlowSegment();
            }

            if (flowDebugSegmentsAttached)
                return;

            flowPathContainer.AddRange(flowDebugSegments);
            flowDebugSegmentsAttached = true;
        }

        private void detachFlowDebugSegments()
        {
            if (!flowDebugSegmentsAttached)
                return;

            hideFlowDebug();
            flowPathContainer.Clear(false);
            flowDebugSegmentsAttached = false;
        }

        private void updateFlowSegments(IReadOnlyList<Vector2> pathPoints)
        {
            int visibleSegments = 0;

            for (int i = 1; i < pathPoints.Count && visibleSegments < flowDebugSegments.Length; i++)
            {
                Vector2 start = ToLocalSpace(pathPoints[i - 1]);
                Vector2 end = ToLocalSpace(pathPoints[i]);
                Vector2 delta = end - start;
                float length = delta.Length;

                if (length <= 0.1f)
                    continue;

                Box segment = flowDebugSegments[visibleSegments++];
                segment.Show();
                segment.Position = start;
                segment.Size = new Vector2(length, 2.5f);
                segment.Rotation = MathHelper.RadiansToDegrees(MathF.Atan2(delta.Y, delta.X));
            }

            for (int i = visibleSegments; i < flowDebugSegments.Length; i++)
                flowDebugSegments[i].Hide();
        }

        private void hideFlowDebug()
        {
            flowAnchorMarker.Hide();

            foreach (Box segment in flowDebugSegments)
                segment.Hide();
        }

        private static CircularContainer createMarker(Color4 colour, float borderThickness, float fillAlpha) => new CircularContainer
        {
            Anchor = Anchor.TopLeft,
            Origin = Anchor.Centre,
            Masking = true,
            Alpha = 0,
            BorderThickness = borderThickness,
            BorderColour = colour,
            Child = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = colour,
                Alpha = fillAlpha,
            }
        };

        private static CircularContainer createPointMarker() => new CircularContainer
        {
            Anchor = Anchor.TopLeft,
            Origin = Anchor.Centre,
            Masking = true,
            Alpha = 0,
            Size = new Vector2(10f),
            BorderThickness = 2,
            BorderColour = Color4.DeepSkyBlue,
            Child = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.DeepSkyBlue,
                Alpha = 0.22f,
            }
        };

        private static CircularContainer createAnchorMarker() => new CircularContainer
        {
            Anchor = Anchor.TopLeft,
            Origin = Anchor.Centre,
            Masking = true,
            Alpha = 0,
            Size = new Vector2(14f),
            BorderThickness = 2,
            BorderColour = Color4.CornflowerBlue,
            Child = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.CornflowerBlue,
                Alpha = 0.18f,
            }
        };

        private static CircularContainer createCursorMarker(Color4 colour, float borderThickness) => new CircularContainer
        {
            Anchor = Anchor.TopLeft,
            Origin = Anchor.Centre,
            Masking = true,
            Alpha = 0,
            Size = new Vector2(12f),
            BorderThickness = borderThickness,
            BorderColour = colour,
            // Use large negative Depth so raw/assisted debug markers draw on top of hitobjects.
            // (Smaller Depth values are drawn last / on top in framework.)
            Depth = -1000f,
            Child = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = colour,
                Alpha = 0.35f,
            }
        };

        private static Box createFlowSegment() => new Box
        {
            Anchor = Anchor.TopLeft,
            Origin = Anchor.CentreLeft,
            Alpha = 0,
            Colour = Color4.CornflowerBlue
        };

        private void updateRawAssistedMarkers()
        {
            // Show raw (orange) / assisted (cyan) / offset line ONLY when debug visuals requested.
            // This ties directly to:
            // - global config toggles (showTargets / showFlowDebug)
            // - OR the per-mod ShowDebug checkbox -> controller.ForceShowDebugFromMod
            // Previously included || IsDrivingCursor || IsEnabled which would show debug markers
            // unconditionally whenever assist was active (even with mod checkbox OFF).
            bool show = aimAssistController.ForceShowDebugFromMod || showTargets.Value || showFlowDebug.Value;
            if (!show)
            {
                hide(rawCursorMarker);
                hide(assistedCursorMarker);
                hide(offsetLine);
                return;
            }

            Vector2 raw = aimAssistController.RawCursorPosition;
            Vector2 assisted = aimAssistController.CurrentOutputPosition;

            // real (physical) cursor - red outline
            rawCursorMarker.Show();
            rawCursorMarker.Alpha = 1f;
            rawCursorMarker.Position = ToLocalSpace(raw);
            rawCursorMarker.Size = new Vector2(16f, 16f);
            if (rawCursorMarker.Child is Box rawBox)
                rawBox.Alpha = 0f;

            // assisted (what the game actually sees / cursor position after assist) - cyan
            assistedCursorMarker.Show();
            assistedCursorMarker.Alpha = 1f;
            assistedCursorMarker.Position = ToLocalSpace(assisted);
            assistedCursorMarker.Size = new Vector2(60f, 60f);
            if (assistedCursorMarker.Child is Box assistBox)
                assistBox.Alpha = 0.9f;

            // Draw line from real cursor to assisted position to visualize the offset
            Vector2 rawLocal = ToLocalSpace(raw);
            Vector2 assistedLocal = ToLocalSpace(assisted);
            Vector2 delta = assistedLocal - rawLocal;
            float len = delta.Length;

            if (len > 3f)
            {
                offsetLine.Show();
                offsetLine.Position = rawLocal;
                offsetLine.Size = new Vector2(len, 4f);
                offsetLine.Rotation = MathHelper.RadiansToDegrees(MathF.Atan2(delta.Y, delta.X));
                offsetLine.Alpha = 1f;
            }
            else
            {
                hide(offsetLine);
            }
        }

        /// <summary>
        /// Hides <paramref name="drawable"/> without going through <see cref="Drawable.Hide"/>.
        /// </summary>
        /// <remarks>
        /// <c>Hide()</c> is <c>this.FadeOut()</c>, which allocates a <c>TransformAlpha</c> and a
        /// <c>TransformSequence</c> on every call even when the drawable is already hidden. These markers are
        /// hidden on every update frame while debug visuals are off, so the churn is pure GC pressure.
        /// </remarks>
        private static void hide(Drawable drawable)
        {
            if (drawable.Alpha != 0)
                drawable.Alpha = 0;
        }
    }
}
