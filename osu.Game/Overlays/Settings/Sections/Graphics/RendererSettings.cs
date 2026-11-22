// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Configuration;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays.Dialog;

namespace osu.Game.Overlays.Settings.Sections.Graphics
{
    public partial class RendererSettings : SettingsSubsection
    {
        protected override LocalisableString Header => GraphicsSettingsStrings.RendererHeader;

        private RendererType confirmedRendererChoice;
        private bool automaticRendererInUse;
        private RendererType startupResolvedRenderer;

        [BackgroundDependencyLoader]
        private void load(FrameworkConfigManager config, OsuConfigManager osuConfig, IDialogOverlay? dialogOverlay, OsuGame? game, GameHost host)
        {
            var renderer = config.GetBindable<RendererType>(FrameworkSetting.Renderer);
            confirmedRendererChoice = renderer.Value;
            automaticRendererInUse = confirmedRendererChoice == RendererType.Automatic;
            startupResolvedRenderer = host.ResolvedRenderer;

            IEnumerable<RendererType> availableRenderers = GetAvailableRenderers(host.GetPreferredRenderersForCurrentPlatform());

            Children = new Drawable[]
            {
                new SettingsItemV2(new RendererDropdown
                {
                    Caption = GraphicsSettingsStrings.Renderer,
                    Current = renderer,
                    Items = availableRenderers,
                })
                {
                    FavouriteId = "ui:GraphicsSettingsStrings.Renderer",
                    Keywords = new[] { @"compatibility", @"directx", @"vulkan", @"opengl", @"renderer" },
                },
                new GraphicsAdapterSettings(),
                // TODO: this needs to be a custom dropdown at some point
                new SettingsItemV2(new FormEnumDropdown<FrameSync>
                {
                    Caption = GraphicsSettingsStrings.FrameLimiter,
                    Current = config.GetBindable<FrameSync>(FrameworkSetting.FrameSync),
                })
                {
                    FavouriteId = "FrameworkSetting.FrameSync",
                    Keywords = new[] { @"fps", @"framerate" },
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.UncappedFpsCaption,
                    HintText = ForkSettingsStrings.UncappedFpsHint,
                    Current = osuConfig.GetBindable<bool>(OsuSetting.ForkUncappedFrameRate),
                })
                {
                    FavouriteId = "OsuSetting.ForkUncappedFrameRate",
                    Keywords = new[] { @"fps", @"framerate", @"frametime", @"latency", @"unlimited" },
                },
                new SettingsItemV2(new FormEnumDropdown<ExecutionMode>
                {
                    Caption = GraphicsSettingsStrings.ThreadingMode,
                    Current = config.GetBindable<ExecutionMode>(FrameworkSetting.ExecutionMode)
                })
                {
                    FavouriteId = "FrameworkSetting.ExecutionMode",
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = GraphicsSettingsStrings.ShowFPS,
                    Current = osuConfig.GetBindable<bool>(OsuSetting.ShowFpsDisplay),
                })
                {
                    FavouriteId = "OsuSetting.ShowFpsDisplay",
                    Keywords = new[] { @"framerate", @"counter" },
                },
            };

            renderer.BindValueChanged(r =>
            {
                if (r.NewValue == confirmedRendererChoice)
                    return;

                // If the game started on Automatic and the user chooses the renderer that is already in use,
                // allow persisting the explicit choice without forcing a restart.
                if (automaticRendererInUse && r.NewValue == startupResolvedRenderer)
                {
                    confirmedRendererChoice = r.NewValue;
                    return;
                }

                if (game?.RestartAppWhenExited() == true)
                {
                    confirmedRendererChoice = r.NewValue;
                    game.AttemptExit();
                }
                else
                {
                    dialogOverlay?.Push(new ConfirmDialog(GraphicsSettingsStrings.ChangeRendererConfirmation, () =>
                    {
                        confirmedRendererChoice = renderer.Value;
                        game?.AttemptExit();
                    }, () => renderer.Value = confirmedRendererChoice));
                }
            });
        }

        /// <summary>
        /// Exposes platform-supported backends, including experimental Vulkan, without changing
        /// the host's automatic preference order or offering unsupported platform backends.
        /// </summary>
        public static IEnumerable<RendererType> GetAvailableRenderers(IEnumerable<RendererType> platformRenderers)
            => platformRenderers.Distinct().Order();

        private partial class RendererDropdown : FormEnumDropdown<RendererType>
        {
            private RendererType hostResolvedRenderer;
            private bool automaticRendererInUse;

            [BackgroundDependencyLoader]
            private void load(FrameworkConfigManager config, GameHost host)
            {
                var renderer = config.GetBindable<RendererType>(FrameworkSetting.Renderer);
                automaticRendererInUse = renderer.Value == RendererType.Automatic;
                hostResolvedRenderer = host.ResolvedRenderer;
            }

            protected override LocalisableString GenerateItemText(RendererType item)
            {
                if (item == RendererType.Automatic && automaticRendererInUse)
                    return LocalisableString.Interpolate($"{base.GenerateItemText(item)} ({hostResolvedRenderer.GetDescription()})");

                return base.GenerateItemText(item);
            }
        }
    }
}
