// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Development;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Database;
using osu.Game.Graphics.UserInterfaceV2;

namespace osu.Game.Overlays.Settings.Sections.DebugSettings
{
    public partial class MemorySettings : SettingsSubsection
    {
        protected override LocalisableString Header => global::osu.Game.Localisation.ClientTranslationStrings.J4030;

        [BackgroundDependencyLoader]
        private void load(GameHost host, RealmAccess realm)
        {
            SettingsButtonV2 blockAction;
            SettingsButtonV2 unblockAction;

            Add(new SettingsButtonV2
            {
                Text = global::osu.Game.Localisation.ClientTranslationStrings.J4031,
                Action = () =>
                {
                    host.Collect();

                    // host.Collect() uses GCCollectionMode.Optimized, but we should be as aggressive as possible here.
                    GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
                }
            });

            FormEnumDropdown<GCLatencyMode> latencyModeDropdown;
            Add(new SettingsItemV2(latencyModeDropdown = new FormEnumDropdown<GCLatencyMode>
            {
                Caption = global::osu.Game.Localisation.ClientTranslationStrings.J4032,
            })
            {
                FavouriteId = "ui:MemorySettings:latencyModeDropdown",
            });

            latencyModeDropdown.Current.BindValueChanged(mode =>
            {
                Logger.Log($"Changing latency mode: {mode.NewValue}");

                switch (mode.NewValue)
                {
                    case GCLatencyMode.Default:
                        // Match the desktop host's startup mode. Gameplay may temporarily override this via
                        // HighPerformanceSessionManager and will restore it afterwards.
                        GCSettings.LatencyMode = System.Runtime.GCLatencyMode.Interactive;
                        break;

                    case GCLatencyMode.Interactive:
                        GCSettings.LatencyMode = System.Runtime.GCLatencyMode.Interactive;
                        break;
                }
            });

            if (DebugUtils.IsDebugBuild)
            {
                AddRange(new Drawable[]
                {
                    new SettingsButtonV2
                    {
                        Text = global::osu.Game.Localisation.ClientTranslationStrings.J4033,
                        Action = () =>
                        {
                            try
                            {
                                // Blocking operations implicitly causes a Compact().
                                using (realm.BlockAllOperations(@"compact"))
                                {
                                }
                            }
                            catch (Exception e)
                            {
                                Logger.Error(e, @"Compacting realm failed");
                            }
                        }
                    },
                    blockAction = new SettingsButtonV2
                    {
                        Text = global::osu.Game.Localisation.ClientTranslationStrings.J4034,
                    },
                    unblockAction = new SettingsButtonV2
                    {
                        Text = global::osu.Game.Localisation.ClientTranslationStrings.J4035,
                    }
                });

                blockAction.Action = () =>
                {
                    try
                    {
                        IDisposable? token = realm.BlockAllOperations(@"maintenance");

                        blockAction.Enabled.Value = false;

                        // As a safety measure, unblock after 10 seconds.
                        // This is to handle the case where a dev may block, but then something on the update thread
                        // accesses realm and blocks for eternity.
                        Task.Factory.StartNew(() =>
                        {
                            Thread.Sleep(10000);
                            unblock();
                        });

                        unblockAction.Action = unblock;

                        void unblock()
                        {
                            if (token.IsNull())
                                return;

                            token.Dispose();
                            token = null;

                            Scheduler.Add(() =>
                            {
                                blockAction.Enabled.Value = true;
                                unblockAction.Action = null;
                            });
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, @"Blocking realm failed");
                    }
                };
            }
        }

        private enum GCLatencyMode
        {
            Default,
            Interactive,
        }
    }
}
