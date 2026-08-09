// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Configuration;
using osu.Game.Scoring.Render;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.UI
{
    public partial class OriginalInputDisplay : CompositeDrawable
    {
        private readonly BindableBool showInput = new BindableBool();
        private readonly BindableBool showAimAssistRadius = new BindableBool();
        private readonly BindableBool aimAssistEnabled = new BindableBool();
        private readonly BindableFloat aimAssistFovRadius = new BindableFloat();

        private CircularContainer radiusPreview = null!;
        private CircularContainer marker = null!;
        private OsuInputManager? inputManager;

        [Resolved(canBeNull: true)]
        private OsuGameBase? game { get; set; }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            config.BindWith(OsuSetting.ForkShowInput, showInput);
            config.BindWith(OsuSetting.ForkShowAimAssistRadius, showAimAssistRadius);
            config.BindWith(OsuSetting.ForkAimAssistEnabled, aimAssistEnabled);
            config.BindWith(OsuSetting.ForkAimAssistFovRadius, aimAssistFovRadius);

            InternalChildren = new Drawable[]
            {
                radiusPreview = new CircularContainer
                {
                    Size = Vector2.Zero,
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.Centre,
                    Masking = true,
                    BorderThickness = 2,
                    BorderColour = Color4.OrangeRed,
                    Alpha = 0,
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.OrangeRed,
                        Alpha = 0.04f,
                    }
                },
                marker = new CircularContainer
                {
                    Size = new Vector2(18),
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.Centre,
                    Masking = true,
                    BorderThickness = 3,
                    BorderColour = Color4.OrangeRed,
                    Alpha = 0,
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.OrangeRed,
                        Alpha = 0.15f,
                    }
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            inputManager = GetContainingInputManager() as OsuInputManager;
        }

        protected override void Update()
        {
            base.Update();

            bool showRadius = showAimAssistRadius.Value && aimAssistEnabled.Value && aimAssistFovRadius.Value > 0;
            bool showMarker = showInput.Value;

            // Both features are off for effectively every real session. Bail before ToLocalSpace() rather than
            // walking the rest of the method to hide two already-hidden drawables on every update frame.
            if (!showRadius && !showMarker)
            {
                hide(radiusPreview);
                hide(marker);
                return;
            }

            if (!tryGetDisplayedInputPosition(out Vector2 displayPosition))
            {
                hide(radiusPreview);
                hide(marker);
                return;
            }

            Vector2 inputPosition = ToLocalSpace(displayPosition);

            if (showRadius)
            {
                float localRadius = toLocalRadius(displayPosition, aimAssistFovRadius.Value);
                radiusPreview.Alpha = 1;
                radiusPreview.Position = inputPosition;
                radiusPreview.Size = new Vector2(localRadius * 2);
            }
            else
                hide(radiusPreview);

            if (showMarker)
            {
                marker.Alpha = 1;
                marker.Position = inputPosition;
            }
            else
                hide(marker);
        }

        /// <summary>
        /// Hides <paramref name="drawable"/> without allocating. <see cref="Drawable.Hide"/> is
        /// <c>this.FadeOut()</c>, which allocates a transform plus a transform sequence on every call, including
        /// when the drawable is already hidden.
        /// </summary>
        private static void hide(Drawable drawable)
        {
            if (drawable.Alpha != 0)
                drawable.Alpha = 0;
        }

        private bool tryGetDisplayedInputPosition(out Vector2 position)
        {
            position = Vector2.Zero;

            if (inputManager == null || game is ReplayRenderGame)
                return false;

            if (inputManager.ReplayBotActive && inputManager.HasReplayCursorPosition)
            {
                position = inputManager.ReplayCursorPosition;
                return true;
            }

            if (!inputManager.HasOriginalUserCursorPosition)
                return false;

            position = inputManager.OriginalUserCursorPosition;
            return true;
        }

        private float toLocalRadius(Vector2 screenSpaceCentre, float screenSpaceRadius)
        {
            Vector2 localCentre = ToLocalSpace(screenSpaceCentre);
            Vector2 localEdge = ToLocalSpace(screenSpaceCentre + new Vector2(screenSpaceRadius, 0));
            return (localEdge - localCentre).Length;
        }
    }
}
