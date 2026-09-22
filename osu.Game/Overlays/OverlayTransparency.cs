// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;

namespace osu.Game.Overlays
{
    /// <summary>
    /// User configuration for local backdrop blur surfaces.
    /// This class deliberately contains no overlay visibility or palette state.
    /// </summary>
    public static class OverlayTransparency
    {
        public const float SURFACE_ALPHA = 0.6f;
        public const float MAX_BLUR_SIGMA = 10f;

        public static readonly Bindable<bool> Enabled = new Bindable<bool>();
        public static readonly Bindable<double> BlurStrength = new Bindable<double>();
        public static readonly Bindable<double> DimAmount = new Bindable<double>();
    }
}
