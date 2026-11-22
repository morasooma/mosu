// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osuTK;

namespace osu.Framework.Input.Handlers.Tablet
{
    /// <summary>
    /// An interface to access OpenTabletDriverHandler.
    /// Can be considered for removal now that we're solely targeting .NET 6
    /// </summary>
    public interface ITabletHandler
    {
        /// <summary>
        /// The offset of the area which should be mapped to the game window.
        /// </summary>
        Bindable<Vector2> AreaOffset { get; }

        /// <summary>
        /// The size of the area which should be mapped to the game window.
        /// </summary>
        Bindable<Vector2> AreaSize { get; }

        /// <summary>
        /// The relative position of the output area in the game window.
        /// Values range from (0,0) for top-left to (1,1) for bottom-right.
        /// Has no effect when <see cref="OutputAreaSize"/> is (1,1).
        /// </summary>
        Bindable<Vector2> OutputAreaOffset { get; }

        /// <summary>
        /// Relative size of output area inside game window.
        /// </summary>
        Bindable<Vector2> OutputAreaSize { get; }

        /// <summary>
        /// Information on the currently connected tablet device. May be null if no tablet is detected.
        /// </summary>
        IBindable<TabletInfo?> Tablet { get; }

        /// <summary>
        /// The rotation of the tablet area in degrees.
        /// </summary>
        Bindable<float> Rotation { get; }

        /// <summary>
        /// The minimum pressure percentage required to click.
        /// </summary>
        BindableFloat PressureThreshold { get; }

        /// <summary>
        /// Anti-smoothing reconstructor weight. 1.0 means no reconstruction.
        /// </summary>
        BindableFloat ReconstructorWeight { get; }

        /// <summary>
        /// Whether Kuuube's CHATTER EXTERMINATOR RAW filter is enabled.
        /// </summary>
        BindableBool ChatterExterminatorEnabled { get; }

        /// <summary>
        /// Chatter extermination strength. Recommended values are 2-3 for dragging and 5-6 for hovering.
        /// </summary>
        BindableFloat ChatterExterminationStrength { get; }

        /// <summary>
        /// Whether AbstractQbit's Radial Follow Smoothing filter is enabled.
        /// </summary>
        BindableBool RadialFollowEnabled { get; }

        /// <summary>
        /// Maximum screen-space distance, in pixels, by which the filtered cursor may trail the tablet position.
        /// </summary>
        BindableFloat RadialFollowOuterRadius { get; }

        /// <summary>
        /// Screen-space deadzone radius, in pixels.
        /// </summary>
        BindableFloat RadialFollowInnerRadius { get; }

        /// <summary>
        /// Initial smoothing coefficient used by Radial Follow Smoothing.
        /// </summary>
        BindableFloat RadialFollowSmoothingCoefficient { get; }

        /// <summary>
        /// Soft-knee scale used by Radial Follow Smoothing.
        /// </summary>
        BindableFloat RadialFollowSoftKneeScale { get; }

        /// <summary>
        /// Amount of smoothing allowed to continue past the outer radius.
        /// </summary>
        BindableFloat RadialFollowSmoothingLeakCoefficient { get; }

        /// <summary>
        /// Whether AbstractQbit's BezierInterpolator filter is enabled.
        /// </summary>
        BindableBool BezierInterpolatorEnabled { get; }

        /// <summary>
        /// EMA weight applied to raw positions before bezier interpolation. 1 means no extra smoothing.
        /// </summary>
        BindableFloat BezierSmoothingFactor { get; }

        BindableBool Enabled { get; }
    }
}
