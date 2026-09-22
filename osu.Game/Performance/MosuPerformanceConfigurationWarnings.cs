// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics.Sprites;
using osu.Framework.Platform;
using osu.Game.Localisation;
using osu.Game.Overlays.Notifications;

namespace osu.Game.Performance
{
    internal static class MosuPerformanceConfigurationWarnings
    {
        public static SimpleNotification? CreateExecutionModeWarning(ExecutionMode executionMode)
        {
            if (executionMode != ExecutionMode.SingleThread)
                return null;

            return new SimpleNotification
            {
                Text = ForkSettingsStrings.SingleThreadedExecutionWarning,
                Icon = FontAwesome.Solid.ExclamationTriangle,
                IsImportant = true,
                Transient = false,
            };
        }
    }
}
