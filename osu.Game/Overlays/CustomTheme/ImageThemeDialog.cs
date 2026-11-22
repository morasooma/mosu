using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays.Dialog;
using osu.Game.Users;
using osuTK;

namespace osu.Game.Overlays.CustomTheme
{
    /// <summary>All edits stay local to this dialog until the user explicitly creates a separate named theme.</summary>
    public partial class ImageThemeDialog : PopupDialog
    {
        public override float PreferredWidth => 700;
        protected override bool CompactAppearance => true;
        [Resolved] private OsuConfigManager config { get; set; } = null!;
        [Resolved] private IAPIProvider api { get; set; } = null!;
        [Resolved] private IBindable<WorkingBeatmap> workingBeatmap { get; set; } = null!;
        private WorkingBeatmap? currentBackgroundBeatmap;
        [Resolved] private IRenderer renderer { get; set; } = null!;
        [Resolved] private LocalisationManager localisation { get; set; } = null!;
        [Cached] private readonly OverlayColourProvider colours = new OverlayColourProvider(OverlayColourScheme.Purple, ThemeSurfaceContext.Settings);
        private readonly Bindable<ImageThemeStyle> style = new Bindable<ImageThemeStyle>();
        private readonly Bindable<bool> light = new Bindable<bool>();
        private readonly Bindable<string> name = new Bindable<string>();
        private CancellationTokenSource? cancellation;
        private GetUserRequest? profileRequest;
        private APIUser user = null!;
        private ImageThemeSource? selectedSource;
        private CustomThemeDefinition original = null!;
        private ThemeInterfaceSettings appearance = null!;
        private CustomThemeDefinition? generated;
        private ImageThemeAnalysis? analysis;
        private uint seed;
        private int generation;
        private bool closed;
        private FillFlowContainer sources = null!;
        private FillFlowContainer swatches = null!;
        private Container preview = null!;
        private OsuTextFlowContainer status = null!;
        internal LocalisableString StatusText { get; private set; }
        private void setStatus(LocalisableString text)
        {
            StatusText = text;
            status.Text = text;
        }
        private PopupDialogOkButton apply = null!;
        private Texture? thumbnail;

        public ImageThemeDialog()
        {
            Icon = FontAwesome.Solid.Palette;
            HeaderText = ImageThemeStrings.Open;
            BodyText = ImageThemeStrings.Description;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            ThemeMode mode = config.Get<ThemeMode>(OsuSetting.ForkThemeMode);
            original = mode == ThemeMode.Custom ? config.CustomThemes.Active.Theme.DeepClone() : new CustomThemeDefinition(mode);
            appearance = ThemeInterfaceSettings.Capture(config);
            light.Value = mode == ThemeMode.Light || mode == ThemeMode.Custom && original.BaseTheme == ThemeMode.Light;
            name.Value = localisation.GetLocalisedString(ImageThemeStrings.Generated);
            MainContent.Padding = new MarginPadding { Horizontal = 24 };
            MainContent.Child = new OsuScrollContainer
            {
                RelativeSizeAxes = Axes.X, Height = 420,
                Child = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y, Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 12), Padding = new MarginPadding { Right = 12, Bottom = 12 },
                    Children = new Drawable[]
                    {
                        sources = new FillFlowContainer { RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y, Spacing = new Vector2(8), Direction = FillDirection.Horizontal },
                        status = new OsuTextFlowContainer(t => t.Font = t.Font.With(size: 16)) { RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y, Text = ImageThemeStrings.Loading },
                        new FormTextBox { Caption = NamedThemeStrings.Name, Current = name, LengthLimit = CustomThemeLibrary.MAX_NAME_LENGTH },
                        new AppearanceDropdown { Caption = ImageThemeStrings.Appearance, HintText = ImageThemeStrings.AppearanceHint, Items = Enum.GetValues<ImageThemeStyle>(), Current = style },
                        new FormCheckBox { Caption = ImageThemeStrings.Light, Current = light },
                        new OsuSpriteText { Text = ImageThemeStrings.Accent, Font = OsuFont.GetFont(size: 16) },
                        swatches = new FillFlowContainer { RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y, Direction = FillDirection.Horizontal, Spacing = new Vector2(8) },
                        preview = new Container { RelativeSizeAxes = Axes.X, Height = 172 },
                    },
                },
            };
            Buttons = new PopupDialogButton[]
            {
                apply = new PopupDialogOkButton { Text = ImageThemeStrings.Apply, HideDialogOnAction = false, Action = createTheme, Enabled = { Value = false } },
                new PopupDialogCancelButton { Text = ImageThemeStrings.Cancel },
            };
            setStatus(ImageThemeStrings.Loading);
            // PopupDialog wraps Action (which re-enables ClickableContainer), so set availability after assigning Buttons.
            updateApply();
            style.BindValueChanged(_ => updatePreview());
            light.BindValueChanged(_ => updatePreview());
            name.BindValueChanged(_ => updateApply());
            State.BindValueChanged(e => { if (e.NewValue == Visibility.Hidden) cancelWork(); });
            user = api.LocalUser.Value;
            currentBackgroundBeatmap = workingBeatmap.Value;
            rebuildSources();
            if (!api.IsLoggedIn)
            {
                setStatus(ImageThemeStrings.Login);
                return;
            }
            profileRequest = new GetUserRequest(user.Id);
            profileRequest.Success += refreshed =>
            {
                if (closed || IsDisposed) return;
                user = refreshed;
                rebuildSources();
                if (StatusText == ImageThemeStrings.Loading) setStatus(ImageThemeStrings.Choose);
            };
            profileRequest.Failure += _ =>
            {
                if (!closed && !IsDisposed && analysis == null && cancellation == null) setStatus(ImageThemeStrings.ProfileFailed);
            };
            api.Queue(profileRequest);
        }

        private void rebuildSources()
        {
            sources.Clear();
            foreach (var source in Enum.GetValues<ImageThemeSource>())
            {
                bool currentBackground = source == ImageThemeSource.CurrentBackground;
                string? url = currentBackground ? null : ImageThemeGenerator.GetSourceUrl(user, source);
                bool available = currentBackground
                    ? currentBackgroundBeatmap != null && !string.IsNullOrEmpty(currentBackgroundBeatmap.Metadata?.BackgroundFile)
                    : api.IsLoggedIn && !string.IsNullOrWhiteSpace(url);
                sources.Add(new SourceButton(source, url, currentBackgroundBeatmap)
                {
                    RelativeSizeAxes = Axes.X, Width = 0.235f, Height = 100,
                    Action = () =>
                    {
                        if (!available) return;
                        selectedSource = source;
                        foreach (var button in sources.Children.OfType<SourceButton>()) button.Selected = button.Source == source;
                        _ = analyseAsync(url, currentBackground ? currentBackgroundBeatmap : null);
                    },
                    Selected = selectedSource == source,
                    Enabled = { Value = available },
                });
            }
        }

        private async Task analyseAsync(string? url, WorkingBeatmap? beatmap = null)
        {
            cancellation?.Cancel();
            int version = ++generation;
            var operation = new CancellationTokenSource();
            cancellation = operation;
            analysis = null;
            generated = null;
            apply.Enabled.Value = false;
            preview.Clear();
            swatches.Clear();
            thumbnail?.Dispose();
            thumbnail = null;
            setStatus(ImageThemeStrings.Analysing);
            try
            {
                var result = await (beatmap != null ? AnalyseBackgroundAsync(beatmap, operation.Token) : AnalyseImageAsync(url!, operation.Token)).ConfigureAwait(false);
                Scheduler.Add(() =>
                {
                    if (closed || IsDisposed || version != generation) return;
                    analysis = result;
                    seed = result.Colours[0];
                    using var stream = new MemoryStream(result.Thumbnail);
                    thumbnail = Texture.FromStream(renderer, stream);
                    foreach (uint candidate in result.Colours)
                        swatches.Add(new SwatchButton(candidate)
                        {
                            Width = 64, Height = 36,
                            Action = () => { seed = candidate; updatePreview(); },
                        });
                    updatePreview();
                });
            }
            catch (Exception)
            {
                Scheduler.Add(() => { if (!closed && !IsDisposed && version == generation) setStatus(ImageThemeStrings.Failed); });
            }
            finally
            {
                // Only the operation which owns this CTS disposes it; an older completion cannot clear a newer request.
                Scheduler.Add(() =>
                {
                    if (ReferenceEquals(cancellation, operation)) cancellation = null;
                    operation.Dispose();
                });
            }
        }

        protected virtual Task<ImageThemeAnalysis> AnalyseBackgroundAsync(WorkingBeatmap beatmap, CancellationToken cancellationToken)
            => Task.Run(() =>
            {
                using var stream = beatmap.OpenBackgroundStream() ?? throw new InvalidDataException("No current background.");
                return ImageThemeGenerator.Analyse(stream, cancellationToken);
            }, cancellationToken);

        protected virtual Task<ImageThemeAnalysis> AnalyseImageAsync(string url, CancellationToken cancellationToken)
            => ImageThemeGenerator.DownloadAsync(url, config, cancellationToken);

        private void updatePreview()
        {
            if (analysis == null || closed) return;
            generated = ImageThemeGenerator.Generate(seed, original, appearance, light.Value, style.Value);
            preview.Child = new ImageThemeSample(generated, thumbnail);
            foreach (var swatch in swatches.Children.OfType<SwatchButton>()) swatch.Selected = swatch.Argb == seed;
            setStatus(ImageThemeStrings.Ready);
            updateApply();
        }

        private void updateApply() => apply.Enabled.Value = generated != null && !string.IsNullOrWhiteSpace(name.Value)
            && name.Value.Trim().Length <= CustomThemeLibrary.MAX_NAME_LENGTH && !name.Value.Any(char.IsControl);

        private void createTheme()
        {
            if (generated == null || !apply.Enabled.Value || closed) return;
            if (!config.CustomThemes.Create(name.Value, generated)) { setStatus(NamedThemeStrings.InvalidName); return; }
            config.SetValue(OsuSetting.ForkThemeMode, ThemeMode.Custom);
            Hide();
        }

        private void cancelWork()
        {
            closed = true;
            generation++;
            cancellation?.Cancel();
            profileRequest?.Cancel();
        }

        protected override void Dispose(bool isDisposing)
        {
            cancelWork();
            base.Dispose(isDisposing);
            thumbnail?.Dispose();
        }

        private partial class AppearanceDropdown : FormDropdown<ImageThemeStyle>
        {
            protected override LocalisableString GenerateItemText(ImageThemeStyle item) => item switch
            {
                ImageThemeStyle.Rounded => ImageThemeStrings.Rounded,
                ImageThemeStyle.Sharp => ImageThemeStrings.Sharp,
                _ => ImageThemeStrings.Keep,
            };
        }

        private partial class SourceButton : OsuButton, IHasTooltip
        {
            private readonly string? url;
            private readonly WorkingBeatmap? beatmap;
            public LocalisableString TooltipText => !Enabled.Value ? ImageThemeStrings.Unavailable : Text;
            public ImageThemeSource Source { get; }
            public bool Selected
            {
                set
                {
                    Content.BorderColour = Colour4.White;
                    Content.BorderThickness = value ? 3 : 0;
                }
            }
            public SourceButton(ImageThemeSource source, string? url, WorkingBeatmap? beatmap)
            {
                this.url = url;
                this.beatmap = source == ImageThemeSource.CurrentBackground ? beatmap : null;
                Source = source;
                Text = source switch { ImageThemeSource.Avatar => ImageThemeStrings.Avatar, ImageThemeSource.Banner => ImageThemeStrings.Banner, ImageThemeSource.CurrentBackground => ImageThemeStrings.CurrentBackground, _ => ImageThemeStrings.Background };
            }
            protected override void LoadComplete()
            {
                base.LoadComplete();
                if (beatmap != null && !string.IsNullOrEmpty(beatmap.Metadata?.BackgroundFile))
                    Content.Add(new DelayedLoadWrapper(() => new CurrentBackgroundPreview(beatmap), 0)
                    { RelativeSizeAxes = Axes.Both, Alpha = 0.65f, Depth = 1 });
                else if (!string.IsNullOrWhiteSpace(url))
                    Content.Add(new UserCoverBackground { User = new APIUser { CoverUrl = url }, RelativeSizeAxes = Axes.Both, Alpha = 0.65f, Depth = 1 });
                Content.Add(new Box { RelativeSizeAxes = Axes.Both, Colour = new Colour4(0, 0, 0, 0.4f), Depth = 0.5f });
            }
            protected override SpriteText CreateText() => new TruncatingSpriteText { RelativeSizeAxes = Axes.X, Width = 0.9f, Anchor = Anchor.Centre, Origin = Anchor.Centre, Font = OsuFont.GetFont(size: 17, weight: FontWeight.SemiBold) };
        }

        [LongRunningLoad]
        private partial class CurrentBackgroundPreview : CompositeDrawable
        {
            private readonly WorkingBeatmap beatmap;
            public CurrentBackgroundPreview(WorkingBeatmap beatmap) => this.beatmap = beatmap;
            [BackgroundDependencyLoader]
            private void load() => InternalChild = new Sprite
            {
                Texture = beatmap.GetBackground(), RelativeSizeAxes = Axes.Both, FillMode = FillMode.Fill,
                Anchor = Anchor.Centre, Origin = Anchor.Centre,
            };
        }

        private partial class SwatchButton : OsuButton
        {
            public uint Argb { get; }
            public bool Selected { set => SpriteText.Text = value ? "✓" : string.Empty; }
            public SwatchButton(uint argb)
            {
                Argb = argb;
                BackgroundColour = Colour4.FromHex($"#{argb & 0xffffff:X6}");
                SpriteText.Colour = OsuColour.ForegroundTextColourFor(BackgroundColour);
            }
            protected override SpriteText CreateText() => new OsuSpriteText { Anchor = Anchor.Centre, Origin = Anchor.Centre, Font = OsuFont.GetFont(size: 24) };
        }
    }

    /// <summary>A local colour/shape sample, not a fake screenshot or a change to the application's global theme.</summary>
    public partial class ImageThemeSample : Container
    {
        public ImageThemeSample(CustomThemeDefinition theme, Texture? image)
        {
            RelativeSizeAxes = Axes.Both;
            Masking = true;
            CornerRadius = theme.Geometry.PanelRadius ?? OverlayColourProvider.GetDefaultRadius(GeometryRole.Panel);
            theme.TryGetOverride(OverlayColour.Background4, out var background);
            theme.TryGetOverride(OverlayColour.Background2, out var card);
            theme.TryGetOverride(OverlayColour.Content1, out var text);
            theme.TryGetOverride(OverlayColour.Content2, out var secondary);
            theme.TryGetOverride(OverlayColour.Highlight1, out var accent);
            Children = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = background },
                new Container
                {
                    RelativeSizeAxes = Axes.Both, Width = 0.27f,
                    Masking = true, Child = new Sprite { Texture = image, RelativeSizeAxes = Axes.Both, FillMode = FillMode.Fill, Anchor = Anchor.Centre, Origin = Anchor.Centre },
                },
                new Container
                {
                    RelativeSizeAxes = Axes.X, Width = 0.68f, Height = 32, X = 0, Anchor = Anchor.TopRight, Origin = Anchor.TopRight,
                    Y = theme.TopBarVariant == TopBarVariant.Floating ? 8 : 0,
                    Masking = true, CornerRadius = theme.TopBarVariant == TopBarVariant.Flat ? 0 : theme.TopBarRadius ?? 10,
                    Child = new Box { RelativeSizeAxes = Axes.Both, Colour = card },
                },
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X, Width = 0.68f, Anchor = Anchor.CentreRight, Origin = Anchor.CentreRight,
                    AutoSizeAxes = Axes.Y, Direction = FillDirection.Vertical, Spacing = new Vector2(0, 8), Padding = new MarginPadding { Horizontal = 14 },
                    Children = new Drawable[]
                    {
                        new TruncatingSpriteText { RelativeSizeAxes = Axes.X, Text = ImageThemeStrings.Sample, Colour = text, Font = OsuFont.GetFont(size: 21, weight: FontWeight.Bold) },
                        new TruncatingSpriteText { RelativeSizeAxes = Axes.X, Text = ImageThemeStrings.SampleDetail, Colour = secondary, Font = OsuFont.GetFont(size: 15) },
                        new Container
                        {
                            RelativeSizeAxes = Axes.X, Height = 36, Masking = true, CornerRadius = theme.Geometry.ButtonRadius ?? OverlayColourProvider.GetDefaultRadius(GeometryRole.Button),
                            Children = new Drawable[]
                            {
                                new Box { RelativeSizeAxes = Axes.Both, Colour = accent },
                                new OsuSpriteText { Text = ImageThemeStrings.SampleButton, Colour = OsuColour.ForegroundTextColourFor(accent), Anchor = Anchor.Centre, Origin = Anchor.Centre, Font = OsuFont.GetFont(size: 17) },
                            },
                        },
                    },
                },
            };
        }
    }
}
