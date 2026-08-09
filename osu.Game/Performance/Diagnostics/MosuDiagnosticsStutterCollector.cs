// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Configuration;

namespace osu.Game.Performance.Diagnostics
{
    public static class MosuDiagnosticsStutterCollector
    {
        private static readonly object sync = new object();
        private static bool collecting;
        private static bool measuredGameplayActive;
        private static RendererType rendererSetting;
        private static RendererType resolvedRenderer;
        private static readonly List<MosuDiagnosticsStutterEvent> events = new List<MosuDiagnosticsStutterEvent>();

        public static bool IsCollecting
        {
            get
            {
                lock (sync)
                    return collecting;
            }
        }

        public static void BeginCollection(RendererType setting, RendererType resolved)
        {
            lock (sync)
            {
                collecting = true;
                measuredGameplayActive = false;
                rendererSetting = setting;
                resolvedRenderer = resolved;
                events.Clear();
            }
        }

        public static void EndCollection()
        {
            lock (sync)
            {
                collecting = false;
                measuredGameplayActive = false;
            }
        }

        /// <summary>
        /// Limits diagnostics stutters to measured gameplay. Startup, loading, results and warm-up stalls
        /// are not properties of the profile being compared.
        /// </summary>
        public static void SetMeasuredGameplayActive(bool active)
        {
            lock (sync)
                measuredGameplayActive = collecting && active;
        }

        public static IReadOnlyList<MosuDiagnosticsStutterEvent> TakeEvents()
        {
            lock (sync)
            {
                var copy = events.ToArray();
                events.Clear();
                return copy;
            }
        }

        public static void TryRecord(MosuDiagnosticsStutterEvent stutter)
        {
            lock (sync)
            {
                if (!collecting || !measuredGameplayActive)
                    return;

                stutter.RendererSetting = rendererSetting.ToString();
                stutter.ResolvedRenderer = resolvedRenderer.ToString();
                events.Add(stutter);
            }
        }

        public static void Reset()
        {
            lock (sync)
            {
                collecting = false;
                measuredGameplayActive = false;
                events.Clear();
            }
        }
    }
}
