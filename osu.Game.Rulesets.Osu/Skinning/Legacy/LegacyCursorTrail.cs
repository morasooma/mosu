// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Input.Events;
using osu.Framework.Logging;
using osu.Game.Configuration;
using osu.Game.Rulesets.Osu.UI.Cursor;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Rulesets.Osu.Skinning.Legacy
{
    public partial class LegacyCursorTrail : CursorTrail
    {
        private readonly ISkin skin;
        private const double disjoint_trail_time_separation = 1000 / 60.0;

        public bool DisjointTrail { get; private set; }
        public string PlacementAlgorithm => DisjointTrail ? "disjoint_60hz" : "interpolated";
        private double lastTrailTime;

        private IBindable<float> cursorSize = null!;

        private Vector2? currentPosition;

        public LegacyCursorTrail(ISkin skin)
        {
            this.skin = skin;
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, ISkinSource skinSource)
        {
            cursorSize = config.GetBindable<float>(OsuSetting.GameplayCursorSize).GetBoundCopy();
            AllowPartRotation = skin.GetConfig<OsuSkinConfiguration, bool>(OsuSkinConfiguration.CursorTrailRotate)?.Value ?? true;

            Texture = skin.GetTexture("cursortrail");

            // Cursor and cursor trail components are sourced from potentially different skin sources.
            // Stable chooses cursor trail disjoint behaviour based on the source that provided the cursor texture.
            // In gameplay render we must avoid resolving this from the global skin manager, otherwise fallback
            // cursormiddle textures from another layer can incorrectly force the interpolated branch.
            ISkin? cursorProvider = null;

            if (skin.GetTexture("cursor") != null)
                cursorProvider = skin;

            cursorProvider ??= (skin as ISkinSource)?.FindProvider(s => s.GetTexture("cursor") != null);
            cursorProvider ??= skinSource.FindProvider(s => s.GetTexture("cursor") != null);

            DisjointTrail = cursorProvider?.GetTexture("cursormiddle") == null;

            Logger.Log($"[LegacyCursorTrail] Loaded with trailSkin={skin.GetType().FullName}, trailSkinHasCursor={skin.GetTexture("cursor") != null}, trailSkinHasCursorMiddle={skin.GetTexture("cursormiddle") != null}, cursorProvider={cursorProvider?.GetType().FullName ?? "null"}, disjoint={DisjointTrail}, placementAlgorithm={PlacementAlgorithm}, trailTexturePresent={Texture != null}",
                LoggingTarget.Runtime,
                LogLevel.Verbose);

            if (DisjointTrail)
            {
                bool centre = skin.GetConfig<OsuSkinConfiguration, bool>(OsuSkinConfiguration.CursorCentre)?.Value ?? true;

                TrailOrigin = centre ? Anchor.Centre : Anchor.TopLeft;
                Blending = BlendingParameters.Inherit;
            }
            else
            {
                Blending = BlendingParameters.Additive;
            }

            Texture?.ScaleAdjust *= LegacySkin.STABLE_MAGIC_SCALE_FACTOR;
        }

        protected override double FadeDuration => DisjointTrail ? 150 : 500;
        protected override float FadeExponent => 1;

        protected override bool InterpolateMovements => !DisjointTrail;
        public override bool RequiresHighFrequencyRenderSimulation => !DisjointTrail;

        protected override float IntervalMultiplier => 1 / Math.Max(cursorSize.Value, 1);
        protected override bool AvoidDrawingNearCursor => !DisjointTrail;

        protected override void Update()
        {
            base.Update();

            if (!DisjointTrail || !currentPosition.HasValue)
                return;

            if (Time.Current - lastTrailTime >= disjoint_trail_time_separation)
            {
                lastTrailTime = Time.Current;
                AddTrail(currentPosition.Value);
            }
        }

        protected override bool OnMouseMove(MouseMoveEvent e)
        {
            if (!DisjointTrail)
                return base.OnMouseMove(e);

            currentPosition = e.ScreenSpaceMousePosition;

            // Intentionally block the base call as we're adding the trails ourselves.
            return false;
        }

        protected override void resetState()
        {
            currentPosition = null;
            lastTrailTime = Time.Current;
        }
    }
}
