using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Containers;
using osu.Game.Overlays;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays.CustomTheme;

namespace osu.Game.Tests.Visual.Online
{
    public partial class TestSceneThemeLibraryOverlay : OsuTestScene
    {
        private OsuConfigManager config = null!;
        private ThemeLibraryOverlay overlay = null!;
        private GetCommunityThemesRequest? pending;
        private CommunityThemeItem[] items = null!;
        private bool fail;
        private bool delay;
        private string target = string.Empty;

        [BackgroundDependencyLoader]
        private void load() => Dependencies.Cache(config = new OsuConfigManager(LocalStorage));

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("mock catalogue", () =>
            {
                fail = delay = false;
                pending = null;
                ((DummyAPIAccess)API).LocalUser.Value = new APIUser { Id = 42, Username = "Theme author" };
                using var image = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(640, 360);
                using var stream = new System.IO.MemoryStream();
                SixLabors.ImageSharp.ImageExtensions.SaveAsPng(image, stream);
                byte[] preview = stream.ToArray();
                items = Enumerable.Range(0, 36).Select(i => new CommunityThemeItem
                {
                    Id = 100 + i, OwnerId = i == 0 ? 42 : 43, Name = "Theme " + i, Author = "Author " + i,
                    Revision = 1, Preview = Convert.ToBase64String(preview),
                }).ToArray();
                ((DummyAPIAccess)API).HandleRequest = request =>
                {
                    switch (request)
                    {
                        case GetCommunityThemesRequest list:
                            pending = list;
                            target = (string)typeof(GetCommunityThemesRequest).GetProperty("Target", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(list)!;
                            if (!delay) reply(list);
                            return true;
                        case DownloadCommunityThemeRequest download:
                            download.TriggerSuccess(JObject.FromObject(new PortableThemePackage { Name = "Installed copy", Theme = new CustomThemeDefinition(ThemeMode.Light)
                            {
                                Interface = new ThemeInterfaceSettings { DisableShear = true, SongSelectStyle = ForkSongSelectStyle.Classic2024, Font = "Inter", OverlayTransparency = true },
                            } }));
                            return true;
                    }
                    return false;
                };
                Child = overlay = new ThemeLibraryOverlay { RelativeSizeAxes = Axes.Y, Width = 1300 };
                overlay.Show();
            });
            AddUntilStep("cards loaded", () => overlay.ChildrenOfType<ThemeLibraryCard>().Count() == 12);
        }

        private void reply(GetCommunityThemesRequest request)
        {
            if (fail) request.Fail(new InvalidOperationException("test failure"));
            else
            {
                string requestTarget = (string)typeof(GetCommunityThemesRequest).GetProperty("Target", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(request)!;
                int offset = int.Parse(System.Text.RegularExpressions.Regex.Match(requestTarget, @"offset=(\d+)").Groups[1].Value);
                request.TriggerSuccess(new CommunityThemePage { Total = items.Length, Items = items.Skip(offset).Take(12).ToArray() });
            }
        }

        private ThemeLibraryButton button(osu.Framework.Localisation.LocalisableString text) => overlay.ChildrenOfType<ThemeLibraryButton>().First(b => b.Text == text && b.IsPresent);

        [Test]
        public void TestToolbarOpensLibraryNotThemeEditor()
        {
            osu.Game.Overlays.Toolbar.ToolbarThemeLibraryButton button = null!;
            AddStep("load library toolbar button", () => Add(new DependencyProvidingContainer
            {
                CachedDependencies = new (Type, object)[] { (typeof(ThemeLibraryOverlay), overlay) },
                Width = 60, Height = osu.Game.Overlays.Toolbar.Toolbar.HEIGHT,
                Child = button = new osu.Game.Overlays.Toolbar.ToolbarThemeLibraryButton(),
            }));
            AddUntilStep("button loaded", () => button.IsLoaded);
            AddAssert("author avatars use server owner ids", () => overlay.ChildrenOfType<ThemeLibraryCard>().All(c =>
                c.ChildrenOfType<osu.Game.Users.Drawables.ClickableAvatar>().Single().TooltipContent!.Id
                == (c == overlay.ChildrenOfType<ThemeLibraryCard>().First() ? 42 : 43)));
            AddAssert("button bound to library", () => button.StateContainer == overlay && button.TooltipMain == ThemeLibraryStrings.Library);
            AddAssert("library uses standard overlay placement", () => overlay.Anchor == Anchor.TopCentre && overlay.Origin == Anchor.TopCentre);
            AddStep("close library via toolbar", () => button.Action.Invoke());
            AddAssert("library closed", () => overlay.State.Value == Visibility.Hidden);
            AddStep("open library via toolbar", () => button.Action.Invoke());
            AddUntilStep("library visible with catalogue", () => overlay.State.Value == Visibility.Visible && overlay.ChildrenOfType<ThemeLibraryCard>().Count() == 12);
            AddAssert("no theme editor", () => !this.ChildrenOfType<osu.Game.Overlays.Settings.Sections.Fork.CustomThemeSettingsPanel>().Any());
        }

        [Test]
        public void TestResponsiveCardsAndInstallation()
        {
            foreach (float width in new[] { 1300f, 900f, 480f })
            {
                AddStep("set width " + width, () => overlay.Width = width);
                AddWaitStep("layout", 4);
                AddAssert("cards fit", () => overlay.ChildrenOfType<ThemeLibraryCard>().All(c => c.DrawWidth > 0
                    && c.ScreenSpaceDrawQuad.TopRight.X <= overlay.ScreenSpaceDrawQuad.TopRight.X + 1));
                AddAssert("first card starts at viewport top", () =>
                {
                    var card = overlay.ChildrenOfType<ThemeLibraryCard>().First();
                    var scroll = overlay.ChildrenOfType<osu.Game.Graphics.Containers.OsuScrollContainer>().First(c => c.IsPresent);
                    return Math.Abs(card.ScreenSpaceDrawQuad.TopLeft.Y - scroll.ScreenSpaceDrawQuad.TopLeft.Y) < 1;
                });
                AddAssert("card rows do not overlap", () =>
                {
                    var cards = overlay.ChildrenOfType<ThemeLibraryCard>().ToArray();
                    return cards.Skip(1).All(c => Math.Abs(c.Y - cards[0].Y) < 1 || c.Y >= cards[0].Height + 17);
                });
                AddAssert("previews preserve ratio", () => overlay.ChildrenOfType<ThemeLibraryCard>().All(c =>
                {
                    var image = c.ChildrenOfType<Sprite>().Single(s => s.Texture?.Width == 640);
                    return image.RelativeSizeAxes == Axes.Both && image.Size == osuTK.Vector2.One && image.FillMode == FillMode.Fit
                           && Math.Abs(c.DrawWidth / c.ChildrenOfType<ThemeLibraryPreview>().Single().Height - 16f / 9) < 0.01;
                }));
            }
            string id = string.Empty;
            string json = string.Empty;
            int count = 0;
            AddStep("install", () =>
            {
                id = config.CustomThemes.ActiveId;
                json = config.Get<string>(OsuSetting.ForkCustomThemeJson);
                count = config.CustomThemes.Entries.Count;
                button(ThemeLibraryStrings.Install).Action.Invoke();
            });
            AddUntilStep("copy installed", () => config.CustomThemes.Entries.Count == count + 1);
            AddAssert("active theme untouched", () => config.CustomThemes.ActiveId == id && config.Get<string>(OsuSetting.ForkCustomThemeJson) == json);
            AddAssert("downloaded appearance retained in local copy", () => config.CustomThemes.Entries.Last().Theme.Interface is
                { DisableShear: true, SongSelectStyle: ForkSongSelectStyle.Classic2024, Font: "Inter", OverlayTransparency: true });
        }

        [Test]
        public void TestStandardHeaderCrossClosesLibrary()
        {
            AddAssert("standard header", () => overlay.Header.Title.Title == ThemeLibraryStrings.Library && overlay.Header.Title.Icon.Equals(FontAwesome.Solid.Palette));
            AddAssert("no text close button", () => !overlay.ChildrenOfType<ThemeLibraryButton>().Any(b => b.Text == ThemeLibraryStrings.Close));
            AddStep("click close cross", () => overlay.Header.ChildrenOfType<osu.Game.Graphics.UserInterface.IconButton>()
                .Single(b => b.Icon.Equals(FontAwesome.Solid.Times)).Action.Invoke());
            AddAssert("library hidden", () => overlay.State.Value == Visibility.Hidden);
        }

        [Test]
        public void TestLargePreviewAndOwnerActions()
        {
            AddStep("open owner theme", () => overlay.ChildrenOfType<osu.Framework.Graphics.Containers.ClickableContainer>().First(c => c.ChildrenOfType<ThemeLibraryPreview>().Any()).Action.Invoke());
            AddUntilStep("details visible", () => overlay.ChildrenOfType<ThemeLibraryButton>().Any(b => b.Text == ThemeLibraryStrings.Delete && b.IsPresent));
            AddAssert("detail includes author avatar", () => overlay.ChildrenOfType<osu.Game.Users.Drawables.ClickableAvatar>().Any(a => a.IsPresent && a.Width == 32 && a.TooltipContent!.Id == 42));
            AddAssert("large image available", () => overlay.ChildrenOfType<ThemeLibraryPreview>().Any(p => p.IsPresent && p.Height >= 360));
            AddStep("back", () => button(ThemeLibraryStrings.BackToLibrary).Action.Invoke());
            AddStep("open someone else's theme", () => overlay.ChildrenOfType<osu.Framework.Graphics.Containers.ClickableContainer>().Where(c => c.ChildrenOfType<ThemeLibraryPreview>().Any()).Skip(1).First().Action.Invoke());
            AddAssert("no owner actions", () => !overlay.ChildrenOfType<ThemeLibraryButton>().Any(b => b.Text == ThemeLibraryStrings.Delete && b.IsPresent));
        }

        [Test]
        public void TestFiltersAndInfiniteScroll()
        {
            AddStep("mine", () => button(ThemeLibraryStrings.MyThemes).Action.Invoke());
            AddUntilStep("owner filter requested", () => target.Contains("ownerId=42", StringComparison.Ordinal));
            AddStep("scroll near end", () => overlay.ChildrenOfType<osu.Game.Graphics.Containers.OsuScrollContainer>().First(c => c.IsPresent).ScrollToEnd(false));
            AddUntilStep("next batch appended", () => target.Contains("offset=12", StringComparison.Ordinal) && overlay.ChildrenOfType<ThemeLibraryCard>().Count() == 24);
            AddStep("scroll to final batch", () => overlay.ChildrenOfType<osu.Game.Graphics.Containers.OsuScrollContainer>().First(c => c.IsPresent).ScrollToEnd(false));
            AddUntilStep("all cards retained", () => overlay.ChildrenOfType<ThemeLibraryCard>().Count() == 36);
            AddWaitStep("no extra requests", 5);
            AddAssert("stops at total", () => target.Contains("offset=24", StringComparison.Ordinal));
            AddStep("all themes", () => button(ThemeLibraryStrings.AllThemes).Action.Invoke());
            AddUntilStep("owner and offset reset", () => !target.Contains("ownerId", StringComparison.Ordinal) && target.Contains("offset=0", StringComparison.Ordinal) && overlay.ChildrenOfType<ThemeLibraryCard>().Count() == 12);
        }

        [Test]
        public void TestAppendFailureCanRetryWithoutLosingCards()
        {
            AddStep("fail next batch", () =>
            {
                fail = true;
                overlay.ChildrenOfType<osu.Game.Graphics.Containers.OsuScrollContainer>().First(c => c.IsPresent).ScrollToEnd(false);
            });
            AddUntilStep("retry visible", () => overlay.ChildrenOfType<ThemeLibraryButton>().Any(b => b.Text == ThemeLibraryStrings.Retry && b.IsPresent));
            AddAssert("existing cards retained", () => overlay.ChildrenOfType<ThemeLibraryCard>().Count() == 12);
            AddStep("retry", () => { fail = false; button(ThemeLibraryStrings.Retry).Action.Invoke(); });
            AddUntilStep("next batch appended", () => overlay.ChildrenOfType<ThemeLibraryCard>().Count() == 24);
        }

        [Test]
        public void TestLoginRequired()
        {
            AddStep("sign out and reopen", () =>
            {
                overlay.Hide();
                ((DummyAPIAccess)API).LocalUser.Value = new APIUser { Id = 0 };
                overlay.Show();
            });
            AddUntilStep("login prompt", () => overlay.ChildrenOfType<OsuSpriteText>().Any(t => t.Text == ThemeLibraryStrings.LoginRequired));
            AddAssert("no stale cards", () => !overlay.ChildrenOfType<ThemeLibraryCard>().Any());
        }

        [Test]
        public void TestFailureEmptyAndStaleResponse()
        {
            AddStep("failure", () => { fail = true; button(ThemeLibraryStrings.AllThemes).Action.Invoke(); });
            AddUntilStep("failure shown", () => overlay.ChildrenOfType<OsuSpriteText>().Any(t => t.Text == ThemeLibraryStrings.Failed));
            AddStep("empty", () => { fail = false; items = Array.Empty<CommunityThemeItem>(); button(ThemeLibraryStrings.AllThemes).Action.Invoke(); });
            AddUntilStep("empty shown", () => overlay.ChildrenOfType<OsuSpriteText>().Any(t => t.Text == ThemeLibraryStrings.Empty));
            AddStep("delayed request", () => { delay = true; pending = null; button(ThemeLibraryStrings.AllThemes).Action.Invoke(); });
            AddUntilStep("request queued", () => pending != null);
            AddStep("hide before response", () => { overlay.Hide(); reply(pending!); });
            AddAssert("hidden overlay stays hidden", () => overlay.State.Value == Visibility.Hidden && !overlay.ChildrenOfType<ThemeLibraryCard>().Any());
        }
    }
}
