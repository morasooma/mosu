using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Game.Input.Bindings;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Users.Drawables;
using osu.Game.Overlays.Dialog;
using osuTK;

namespace osu.Game.Overlays.CustomTheme
{
    /// <summary>A standalone catalogue. Browsing and installation never apply a theme to the interface.</summary>
    public partial class ThemeLibraryOverlay : FullscreenOverlay<ThemeLibraryHeader>
    {
        public override IconUsage Icon => osu.Game.Graphics.OsuIcon.NavigationPalette;
        private OverlayColourProvider colours => ColourProvider;
        [Resolved] private IAPIProvider api { get; set; } = null!;
        [Resolved] private OsuConfigManager config { get; set; } = null!;
        [Resolved] private IRenderer renderer { get; set; } = null!;
        [Resolved(canBeNull: true)] private IDialogOverlay? dialogs { get; set; }
        private readonly Bindable<string> search = new Bindable<string>(string.Empty);
        private Container cards = null!;
        private OsuScrollContainer catalogue = null!;
        private OsuScrollContainer detail = null!;
        private FillFlowContainer detailContent = null!;
        private FillFlowContainer toolbar = null!;
        private Container searchContainer = null!;
        private OsuSpriteText status = null!;
        private ThemeLibraryButton retry = null!;
        private bool loading;
        private bool hasMore;
        private bool loadFailed;
        private double nextLoadAt;
        private readonly System.Collections.Generic.HashSet<long> loadedIds = new System.Collections.Generic.HashSet<long>();
        private ThemeLibraryButton allThemes = null!;
        private ThemeLibraryButton myThemes = null!;
        private ThemeLibraryButton publishButton = null!;
        private APIRequest? listRequest;
        private APIRequest? mutationRequest;
        private readonly CancellationTokenSource captureCancellation = new CancellationTokenSource();
        private IBindable<Colour4> themeColour = null!;
        private GridContainer layout = null!;
        private float currentToolbarHeight = 82;
        private int offset;
        private bool mine;
        private bool busy;
        private int generation;
        private string? retryKey;
        private string? retryJson;
        private byte[]? retryPreview;

        public ThemeLibraryOverlay() : base(OverlayColourScheme.Purple, ThemeSurfaceContext.Settings)
        {
        }

        protected override ThemeLibraryHeader CreateHeader() => new ThemeLibraryHeader();

        [BackgroundDependencyLoader]
        private void load()
        {
            var searchBox = new FormTextBox { Caption = ThemeLibraryStrings.Search, Current = search, LengthLimit = 64 };
            searchBox.OnCommit += (_, _) => { offset = 0; refresh(); };
            Header.Add(new IconButton
            {
                Anchor = Anchor.TopRight, Origin = Anchor.TopRight, Position = new Vector2(-12, 8),
                Size = new Vector2(40), Icon = FontAwesome.Solid.Times, TooltipText = ThemeLibraryStrings.Close, Action = Hide,
            });
            Child = layout = new GridContainer
            {
                RelativeSizeAxes = Axes.Both,
                Padding = new MarginPadding { Bottom = 24 },
                RowDimensions = new[]
                {
                    new Dimension(GridSizeMode.AutoSize),
                    new Dimension(GridSizeMode.Absolute, 82),
                    new Dimension(GridSizeMode.Absolute, 32),
                    new Dimension(),
                },
                Content = new[]
                {
                    new Drawable[] { Header },
                    new Drawable[]
                    {
                        toolbar = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y, Spacing = new Vector2(12), Padding = new MarginPadding { Horizontal = 24, Vertical = 12 },
                            Children = new Drawable[]
                            {
                                searchContainer = new Container { Height = 58, Width = 400, Child = searchBox },
                                allThemes = new ThemeLibraryButton { Text = ThemeLibraryStrings.AllThemes, Width = 150, Action = () => { mine = false; refresh(); } },
                                myThemes = new ThemeLibraryButton { Text = ThemeLibraryStrings.MyThemes, Width = 180, Action = () => { mine = true; refresh(); } },
                                publishButton = new ThemeLibraryButton { Text = ThemeLibraryStrings.Publish, Width = 250, Primary = true, Action = () => confirmPublish(null) },
                            },
                        },
                    },
                    new Drawable[]
                    {
                        new Container
                        {
                            RelativeSizeAxes = Axes.Both, Padding = new MarginPadding { Horizontal = 24 },
                            Children = new Drawable[]
                            {
                                status = new OsuSpriteText { Font = OsuFont.GetFont(size: 17), Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft },
                                retry = new ThemeLibraryButton { Text = ThemeLibraryStrings.Retry, Width = 130, Height = 30, Anchor = Anchor.CentreRight, Origin = Anchor.CentreRight, Alpha = 0, Action = loadMore },
                            },
                        },
                    },
                    new Drawable[]
                    {
                        new Container
                        {
                            RelativeSizeAxes = Axes.Both, Padding = new MarginPadding { Horizontal = 24 },
                            Children = new Drawable[]
                            {
                                catalogue = new OsuScrollContainer
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Child = cards = new Container { RelativeSizeAxes = Axes.X, Padding = new MarginPadding { Right = 12, Bottom = 18 } },
                                },
                                detail = new OsuScrollContainer
                                {
                                    RelativeSizeAxes = Axes.Both, Alpha = 0,
                                    Child = detailContent = new FillFlowContainer { RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y, Direction = FillDirection.Vertical, Spacing = new Vector2(12), Padding = new MarginPadding { Right = 12, Bottom = 18 } },
                                },
                            },
                        },
                    },
                },
            };
            themeColour = colours.GetColourBindable(OverlayColour.Background4);
            themeColour.BindValueChanged(_ =>
            {
                status.Colour = colours.Content2;
            }, true);
        }

        protected override void PopIn()
        {
            base.PopIn();
            refresh();
        }

        protected override void PopOut()
        {
            base.PopOut();
            generation++;
            listRequest?.Cancel();
        }

        protected override void Update()
        {
            base.Update();
            float available = Math.Max(0, cards.DrawWidth - cards.Padding.TotalHorizontal);
            int columns = available >= 1100 ? 3 : available >= 650 ? 2 : 1;
            const float spacing = 18;
            float width = Math.Max(0, (available - spacing * (columns - 1)) / columns);
            int index = 0;
            foreach (var card in cards.Children.OfType<ThemeLibraryCard>())
            {
                card.Size = new Vector2(width, width * 9 / 16 + 88);
                card.Position = new Vector2(index % columns * (width + spacing), index / columns * (card.Height + spacing));
                index++;
            }
            int rows = (index + columns - 1) / columns;
            cards.Height = rows == 0 ? 0 : rows * (width * 9 / 16 + 88 + spacing) - spacing + cards.Padding.TotalVertical;
            float toolbarWidth = Math.Max(0, toolbar.DrawWidth - toolbar.Padding.TotalHorizontal);
            searchContainer.Width = Math.Max(0, toolbarWidth >= 980 ? toolbarWidth - 616 : toolbarWidth);
            float toolbarHeight = toolbarWidth >= 980 ? 82 : toolbarWidth >= 604 ? 142 : 202;
            if (currentToolbarHeight != toolbarHeight)
            {
                currentToolbarHeight = toolbarHeight;
                layout.RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize), new Dimension(GridSizeMode.Absolute, toolbarHeight), new Dimension(GridSizeMode.Absolute, 32), new Dimension() };
            }
            if (!loading && !loadFailed && hasMore && Clock.CurrentTime >= nextLoadAt && catalogue.IsPresent && catalogue.Current >= catalogue.ScrollableExtent - 300)
                loadMore();
            foreach (var image in detailContent.Children.OfType<ThemeLibraryPreview>())
                image.Height = Math.Min(ThemePreviewRenderer.HEIGHT, detailContent.DrawWidth * 9 / 16);
        }

        public override bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Action == GlobalAction.Back && detail.IsPresent && !e.Repeat)
            {
                showCatalogue();
                return true;
            }
            return base.OnPressed(e);
        }

        private void showCatalogue()
        {
            detail.Hide();
            detailContent.Clear();
            catalogue.Show();
        }

        private void refresh()
        {
            showCatalogue();
            listRequest?.Cancel();
            generation++;
            cards.Clear();
            loadedIds.Clear();
            offset = 0;
            loading = loadFailed = false;
            hasMore = true;
            retry.Hide();
            allThemes.Selected = !mine;
            myThemes.Selected = mine;
            catalogue.ScrollToStart(false);
            if (api.LocalUser.Value.Id <= 1) { hasMore = false; status.Text = ThemeLibraryStrings.LoginRequired; return; }
            loadMore();
        }

        private void loadMore()
        {
            if (loading || !hasMore) return;
            int requestGeneration = generation;
            loading = true;
            loadFailed = false;
            retry.Hide();
            status.Text = ThemeLibraryStrings.Loading;
            var request = new GetCommunityThemesRequest(search.Value, mine ? api.LocalUser.Value.Id : null, offset);
            listRequest = request;
            request.Success += page =>
            {
                if (IsDisposed || requestGeneration != generation) return;
                loading = false;
                // Allow the appended rows to complete layout before testing the new scroll extent.
                nextLoadAt = Clock.CurrentTime + 100;
                foreach (var item in page.Items)
                    if (loadedIds.Add(item.Id))
                        cards.Add(new ThemeLibraryCard(item, createPreview(item), () => openPreview(item), () => install(item)));
                offset += page.Items.Length;
                hasMore = page.Items.Length > 0 && offset < page.Total;
                status.Text = loadedIds.Count == 0 ? ThemeLibraryStrings.Empty : ThemeLibraryStrings.ThemeCount(loadedIds.Count);
            };
            request.Failure += _ =>
            {
                if (IsDisposed || requestGeneration != generation) return;
                loading = false;
                loadFailed = true;
                status.Text = ThemeLibraryStrings.Failed;
                retry.Show();
            };
            api.Queue(request);
        }

        private ThemeLibraryPreview createPreview(CommunityThemeItem item)
        {
            Texture? texture = null;
            try
            {
                if (item.Preview.Length <= 350000)
                {
                    using var stream = new MemoryStream(Convert.FromBase64String(item.Preview));
                    var info = SixLabors.ImageSharp.Image.Identify(stream);
                    if (info.Width == ThemePreviewRenderer.WIDTH && info.Height == ThemePreviewRenderer.HEIGHT)
                    {
                        stream.Position = 0;
                        texture = Texture.FromStream(renderer, stream);
                    }
                }
            }
            catch (Exception exception) when (exception is FormatException or ArgumentException or SixLabors.ImageSharp.UnknownImageFormatException or SixLabors.ImageSharp.InvalidImageContentException)
            { }
            return new ThemeLibraryPreview(texture);
        }

        private void openPreview(CommunityThemeItem item)
        {
            detailContent.Clear();
            detailContent.Add(new ThemeLibraryButton { Text = ThemeLibraryStrings.BackToLibrary, Width = 250, Action = showCatalogue });
            detailContent.Add(new TruncatingSpriteText { Text = item.Name, Font = OsuFont.GetFont(size: 28, weight: FontWeight.Bold), RelativeSizeAxes = Axes.X });
            detailContent.Add(new Container
            {
                RelativeSizeAxes = Axes.X, Height = 32,
                Children = new Drawable[]
                {
                    ThemeLibraryCard.CreateAuthorAvatar(item, 32),
                    new TruncatingSpriteText { Text = item.Author, Font = OsuFont.GetFont(size: 18), RelativeSizeAxes = Axes.X,
                        Padding = new MarginPadding { Left = 42 }, Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft },
                },
            });
            detailContent.Add(new ThemeLibraryButton { Text = ThemeLibraryStrings.Install, Width = 260, Primary = true, Action = () => install(item) });
            detailContent.Add(createPreview(item));
            if (item.OwnerId == api.LocalUser.Value.Id)
            {
                detailContent.Add(new ThemeLibraryButton { Text = ThemeLibraryStrings.Update, Width = 320, Action = () => confirmPublish(item) });
                detailContent.Add(new ThemeLibraryButton
                {
                    Text = ThemeLibraryStrings.Delete, Width = 260,
                    Action = () => dialogs?.Push(new ThemeActionDialog(ThemeLibraryStrings.Delete, ThemeLibraryStrings.DeleteConfirm(item.Name), () => delete(item))),
                });
            }
            catalogue.Hide();
            detail.Show();
            detail.ScrollToStart(false);
        }

        private void install(CommunityThemeItem item)
        {
            if (busy) return;
            setBusy(true);
            var request = new DownloadCommunityThemeRequest(item.Id);
            mutationRequest = request;
            request.Success += package =>
            {
                setBusy(false);
                if (IsDisposed) return;
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(package.ToString(Formatting.None)));
                status.Text = ThemeShareManager.ValidateAndImport(stream, out var theme, out string name, out _) && config.CustomThemes.Create(name, theme, select: false)
                    ? ThemeLibraryStrings.Installed : ThemeLibraryStrings.Failed;
            };
            request.Failure += _ => { setBusy(false); if (!IsDisposed) status.Text = ThemeLibraryStrings.Failed; };
            api.Queue(request);
        }

        private void setBusy(bool value)
        {
            busy = value;
            if (!IsLoaded || IsDisposed) return;
            publishButton.Enabled.Value = !value;
        }

        private void confirmPublish(CommunityThemeItem? item)
        {
            if (busy) return;
            if (api.LocalUser.Value.Id <= 1) { status.Text = ThemeLibraryStrings.LoginRequired; return; }
            var entry = config.CustomThemes.Active;
            if (string.IsNullOrWhiteSpace(entry.Name)) { status.Text = NamedThemeStrings.InvalidName; return; }
            var theme = config.CustomThemes.GetActiveThemeForSharing();
            string name = entry.Name;
            dialogs?.Push(new ThemeActionDialog(item == null ? ThemeLibraryStrings.Publish : ThemeLibraryStrings.Update,
                item == null ? ThemeLibraryStrings.PublishConfirm(name) : ThemeLibraryStrings.UpdateConfirm(item.Name, name),
                () => publish(name, theme, item)));
        }

        private async void publish(string name, CustomThemeDefinition theme, CommunityThemeItem? item)
        {
            if (busy) return;
            setBusy(true);
            status.Text = ThemeLibraryStrings.Loading;
            try
            {
                string fingerprint = name + theme.ToJson() + item?.Id + ":" + item?.Revision;
                if (retryJson != fingerprint)
                {
                    retryKey = Guid.NewGuid().ToString("N");
                    retryJson = fingerprint;
                    retryPreview = null;
                }
                byte[] preview = retryPreview ?? await ThemePreviewRenderer.RenderAsync(theme, captureCancellation.Token).ConfigureAwait(false);
                Scheduler.Add(() =>
                {
                    if (IsDisposed) return;
                    // The server compares the complete retry payload, including PNG bytes.
                    retryPreview = preview;
                    var request = new PublishCommunityThemeRequest(name, retryKey!, theme, preview, item?.Id, item?.Revision ?? 0);
                    mutationRequest = request;
                    request.Success += _ =>
                    {
                        setBusy(false); retryKey = retryJson = null; retryPreview = null;
                        if (IsDisposed) return;
                        mine = true; offset = 0; refresh();
                    };
                    request.Failure += _ => { setBusy(false); if (!IsDisposed) status.Text = ThemeLibraryStrings.Failed; };
                    api.Queue(request);
                });
            }
            catch (Exception)
            {
                Scheduler.Add(() => { setBusy(false); if (!IsDisposed) status.Text = ThemeLibraryStrings.Failed; });
            }
        }

        private void delete(CommunityThemeItem item)
        {
            if (busy) return;
            setBusy(true);
            var request = new DeleteCommunityThemeRequest(item.Id, item.Revision);
            mutationRequest = request;
            request.Success += _ => { setBusy(false); if (!IsDisposed) refresh(); };
            request.Failure += _ => { setBusy(false); if (!IsDisposed) status.Text = ThemeLibraryStrings.Failed; };
            api.Queue(request);
        }

        protected override void Dispose(bool isDisposing)
        {
            generation++;
            captureCancellation.Cancel();
            captureCancellation.Dispose();
            listRequest?.Cancel();
            mutationRequest?.Cancel();
            base.Dispose(isDisposing);
        }

        private partial class ThemeActionDialog : PopupDialog
        {
            public ThemeActionDialog(LocalisableString title, LocalisableString body, Action confirm)
            {
                HeaderText = title; BodyText = body; Icon = FontAwesome.Solid.Palette;
                Buttons = new PopupDialogButton[]
                {
                    new PopupDialogOkButton { Text = title, Action = confirm },
                    new PopupDialogCancelButton(),
                };
            }
        }
    }

    public partial class ThemeLibraryHeader : OverlayHeader
    {
        protected override OverlayTitle CreateTitle() => new ThemeLibraryTitle();

        private partial class ThemeLibraryTitle : OverlayTitle
        {
            public ThemeLibraryTitle()
            {
                Title = ThemeLibraryStrings.Library;
                Description = ThemeLibraryStrings.OpenHint;
                Icon = FontAwesome.Solid.Palette;
            }
        }
    }

    public partial class ThemeLibraryCard : CompositeDrawable
    {
        private readonly ThemeLibraryPreview preview;

        internal static ClickableAvatar CreateAuthorAvatar(CommunityThemeItem item, float size, float y = 0) => new ClickableAvatar(
            new APIUser { Id = item.OwnerId is > 1 and <= int.MaxValue ? (int)item.OwnerId : 1, Username = item.Author })
        {
            Size = new Vector2(size), Y = y, Masking = true, CornerRadius = 4,
        };

        public ThemeLibraryCard(CommunityThemeItem item, ThemeLibraryPreview preview, Action open, Action install)
        {
            this.preview = preview;
            InternalChildren = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = Colour4.Black.Opacity(0.15f) },
                new ClickableContainer
                {
                    RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y, Action = open, Child = preview,
                },
                new Container
                {
                    RelativeSizeAxes = Axes.X, Anchor = Anchor.BottomLeft, Origin = Anchor.BottomLeft, Height = 88, Padding = new MarginPadding(12),
                    Children = new Drawable[]
                    {
                        new TruncatingSpriteText { Text = item.Name, RelativeSizeAxes = Axes.X, Padding = new MarginPadding { Right = 150 }, Font = OsuFont.GetFont(size: 22, weight: FontWeight.Bold) },
                        CreateAuthorAvatar(item, 24, 30),
                        new TruncatingSpriteText { Text = item.Author, RelativeSizeAxes = Axes.X, Padding = new MarginPadding { Left = 32, Right = 150 }, Y = 34, Font = OsuFont.GetFont(size: 16) },
                        new ThemeLibraryButton { Text = ThemeLibraryStrings.Install, Width = 144, Height = 40, Anchor = Anchor.CentreRight, Origin = Anchor.CentreRight, Primary = true, Action = install },
                    },
                },
            };
        }
        protected override void Update()
        {
            base.Update();
            preview.Height = DrawWidth * 9 / 16;
        }
    }

    public partial class ThemeLibraryPreview : Container
    {
        public ThemeLibraryPreview(Texture? texture)
        {
            RelativeSizeAxes = Axes.X;
            Height = 220;
            Child = new Sprite
            {
                Texture = texture, RelativeSizeAxes = Axes.Both, Size = Vector2.One, FillMode = FillMode.Fit,
                Anchor = Anchor.Centre, Origin = Anchor.Centre,
            };
        }
    }

    /// <summary>Quiet catalogue controls, without animated triangle decoration competing with the theme previews.</summary>
    public partial class ThemeLibraryButton : OsuButton
    {
        public bool Primary { get; set; }
        public bool Selected { get; set; }
        private Colour4? lastBackground;
        private OverlayColourProvider colours = null!;
        public ThemeLibraryButton() { Height = 48; }
        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours) => this.colours = colours;
        protected override void Update()
        {
            base.Update();
            var background = Primary || Selected ? colours.Colour3 : colours.Background2;
            if (lastBackground != (Colour4)background)
            {
                lastBackground = background;
                DefaultBackgroundColour = background;
                SpriteText.Colour = OsuColour.ForegroundTextColourFor(background);
            }
        }
        protected override SpriteText CreateText() => new OsuSpriteText
        {
            Anchor = Anchor.Centre, Origin = Anchor.Centre, Font = OsuFont.GetFont(size: 17, weight: FontWeight.SemiBold),
        };
    }
}
