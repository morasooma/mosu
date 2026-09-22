// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Framework.Threading;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Online;
using osu.Game.Overlays.Dialog;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays.Settings.Sections.Fork
{
    public partial class ForkConnectionSettings : SettingsSubsection
    {
        [Resolved]
        private OsuGame? game { get; set; }

        [Resolved(CanBeNull = true)]
        private IDialogOverlay? dialogOverlay { get; set; }

        protected override LocalisableString Header => ForkSettingsStrings.ConnectionHeader;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            Bindable<MosuConnectionRoute> connectionRoute = config.GetBindable<MosuConnectionRoute>(OsuSetting.ForkConnectionRoute);
            bool revertingChange = false;

            connectionRoute.BindValueChanged(change =>
            {
                if (revertingChange || change.NewValue == change.OldValue)
                    return;

                Scheduler.Add(() =>
                {
                    if (game == null)
                        return;

                    dialogOverlay?.Push(new ConfirmDialog(
                        ForkSettingsStrings.ConnectionProxyRestartBody,
                        restartGame,
                        () =>
                        {
                            revertingChange = true;
                            connectionRoute.Value = change.OldValue;
                            revertingChange = false;
                        }));
                });
            });

            Children = new Drawable[]
            {
                new SettingsItemV2(new FormEnumDropdown<MosuConnectionRoute>
                {
                    Caption = ForkSettingsStrings.ConnectionProxyCaption,
                    HintText = ForkSettingsStrings.ConnectionProxyHint,
                    Current = connectionRoute
                })
                {
                    Keywords = new[] { @"proxy", @"connection", @"latency", @"ping", @"Russia", @"RF", @"резервный", @"прокси", @"соединение", @"пинг" }
                },
                new ConnectionLatencyDisplay(),
            };
        }

        private partial class ConnectionLatencyDisplay : FillFlowContainer
        {
            private static readonly MosuConnectionRoute[] routes =
            {
                MosuConnectionRoute.Direct,
                MosuConnectionRoute.Proxy1,
                MosuConnectionRoute.Proxy2,
            };

            private readonly Dictionary<MosuConnectionRoute, OsuSpriteText> labels = new Dictionary<MosuConnectionRoute, OsuSpriteText>();
            private readonly CancellationTokenSource disposalCancellation = new CancellationTokenSource();
            private readonly HttpClient httpClient = new HttpClient(new HttpClientHandler
            {
                AllowAutoRedirect = false,
            })
            {
                Timeout = TimeSpan.FromSeconds(5),
            };

            private ScheduledDelegate? refreshSchedule;
            private bool refreshing;

            [Resolved]
            private OsuColour colours { get; set; } = null!;

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            public ConnectionLatencyDisplay()
            {
                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
                Direction = FillDirection.Vertical;
                Spacing = new Vector2(0, 3);
                Padding = new MarginPadding { Horizontal = 14, Vertical = 8 };
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                foreach (MosuConnectionRoute route in routes)
                {
                    string endpoint = MosuServerEnvironment.GetServerUrl(route);

                    var label = new OsuSpriteText
                    {
                        RelativeSizeAxes = Axes.X,
                        Font = OsuFont.GetFont(size: 14),
                        Colour = colourProvider.Content2,
                        Text = ForkSettingsStrings.ConnectionLatencyStatus(getRouteName(route), new Uri(endpoint).Host, ForkSettingsStrings.ConnectionLatencyMeasuring),
                    };

                    labels.Add(route, label);
                    Add(label);
                }
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                refreshLatencies();
                refreshSchedule = Scheduler.AddDelayed(refreshLatencies, 15_000, true);
            }

            private void refreshLatencies()
            {
                if (refreshing)
                    return;

                refreshing = true;

                foreach (MosuConnectionRoute route in routes)
                    updateLabel(route, ForkSettingsStrings.ConnectionLatencyMeasuring, colourProvider.Content2);

                _ = measureLatencies();
            }

            private async Task measureLatencies()
            {
                try
                {
                    LatencyResult[] results = await Task.WhenAll(routes.Select(measureLatency)).ConfigureAwait(false);

                    if (IsDisposed)
                        return;

                    Schedule(() =>
                    {
                        foreach (LatencyResult result in results)
                        {
                            if (result.Milliseconds.HasValue)
                            {
                                long milliseconds = result.Milliseconds.Value;
                                Color4 colour = milliseconds <= 150 ? colours.GreenLight : milliseconds <= 350 ? colours.YellowLight : colours.RedLight;
                                updateLabel(result.Route, ForkSettingsStrings.ConnectionLatencyMilliseconds(milliseconds), colour);
                            }
                            else
                                updateLabel(result.Route, ForkSettingsStrings.ConnectionLatencyUnavailable, colours.RedLight);
                        }

                        refreshing = false;
                    });
                }
                catch (OperationCanceledException)
                {
                    // Expected while the settings screen is being disposed.
                }
                catch
                {
                    if (!IsDisposed)
                    {
                        Schedule(() =>
                        {
                            foreach (MosuConnectionRoute route in routes)
                                updateLabel(route, ForkSettingsStrings.ConnectionLatencyUnavailable, colours.RedLight);

                            refreshing = false;
                        });
                    }
                }
            }

            private async Task<LatencyResult> measureLatency(MosuConnectionRoute route)
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Head, MosuServerEnvironment.GetServerUrl(route));
                    var stopwatch = Stopwatch.StartNew();

                    using HttpResponseMessage response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, disposalCancellation.Token).ConfigureAwait(false);
                    stopwatch.Stop();

                    return new LatencyResult(route, stopwatch.ElapsedMilliseconds);
                }
                catch (OperationCanceledException) when (!disposalCancellation.IsCancellationRequested)
                {
                    return new LatencyResult(route, null);
                }
                catch (HttpRequestException)
                {
                    return new LatencyResult(route, null);
                }
            }

            private void updateLabel(MosuConnectionRoute route, LocalisableString status, Color4 colour)
            {
                string endpoint = MosuServerEnvironment.GetServerUrl(route);
                labels[route].Text = ForkSettingsStrings.ConnectionLatencyStatus(getRouteName(route), new Uri(endpoint).Host, status);
                labels[route].Colour = colour;
            }

            private static LocalisableString getRouteName(MosuConnectionRoute route) => route switch
            {
                MosuConnectionRoute.Direct => ForkSettingsStrings.ConnectionRouteDirect,
                MosuConnectionRoute.Proxy1 => ForkSettingsStrings.ConnectionRouteProxy1,
                MosuConnectionRoute.Proxy2 => ForkSettingsStrings.ConnectionRouteProxy2,
                _ => route.ToString(),
            };

            protected override void Dispose(bool isDisposing)
            {
                refreshSchedule?.Cancel();
                disposalCancellation.Cancel();
                disposalCancellation.Dispose();
                httpClient.Dispose();

                base.Dispose(isDisposing);
            }

            private readonly struct LatencyResult
            {
                public readonly MosuConnectionRoute Route;
                public readonly long? Milliseconds;

                public LatencyResult(MosuConnectionRoute route, long? milliseconds)
                {
                    Route = route;
                    Milliseconds = milliseconds;
                }
            }
        }

        private void restartGame()
        {
            if (game == null)
                return;

            game.RestartOnExitAction = () =>
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = Environment.ProcessPath,
                    UseShellExecute = false,
                });
            };
            game.Exit();
        }
    }
}
