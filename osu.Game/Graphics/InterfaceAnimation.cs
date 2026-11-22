using System;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Configuration;

namespace osu.Game.Graphics
{
    /// <summary>
    /// Visual UI policy only. Never use this for gameplay timing, scheduling or object lifetimes.
    /// </summary>
    public static class InterfaceAnimation
    {
        public static readonly Bindable<InterfaceAnimationMode> Mode = new Bindable<InterfaceAnimationMode>();

        public static readonly Bindable<bool> Enhanced = new Bindable<bool>();

        public static bool AllowDecorativeMovement => Mode.Value == InterfaceAnimationMode.Full;

        public static bool UseEnhancedMotion => Enhanced.Value && AllowDecorativeMovement;

        /// <summary>
        /// Opt-in timing for discrete UI transitions. Do not use for delays, lifetimes or beat-synchronised effects.
        /// </summary>
        public static double TransitionDuration(double duration) => MovementDuration(UseEnhancedMotion ? Math.Min(duration, 220) : duration);

        public static double TransitionFadeDuration(double duration) => FadeDuration(UseEnhancedMotion ? Math.Min(duration, 220) : duration);

        public static Easing TransitionEasing(Easing original) => UseEnhancedMotion ? Easing.InOutSine : original;

        public static double FadeDuration(double duration) => Mode.Value switch
        {
            InterfaceAnimationMode.Disabled => 0,
            InterfaceAnimationMode.Minimal => Math.Min(duration, 100),
            _ => duration,
        };

        public static double MovementDuration(double duration) => AllowDecorativeMovement ? duration : 0;

        /// <summary>
        /// Complete only visual properties when the mode changes. Scheduling, lifetimes and
        /// looping transforms are deliberately not flushed. Retain the returned bound copy.
        /// </summary>
        public static IBindable<InterfaceAnimationMode> Observe(Drawable drawable, bool children = false, Func<bool>? exempt = null)
        {
            var mode = Mode.GetBoundCopy();
            mode.BindValueChanged(_ =>
            {
                if (mode.Value == InterfaceAnimationMode.Full || exempt?.Invoke() == true)
                    return;

                foreach (string member in new[] { "Position", "X", "Y", "Scale", "Rotation", "Alpha", "Colour", "BorderColour", "EdgeEffect", "Size", "Width", "Height" })
                    drawable.FinishTransforms(children, member);
            });
            return mode;
        }
    }
}
