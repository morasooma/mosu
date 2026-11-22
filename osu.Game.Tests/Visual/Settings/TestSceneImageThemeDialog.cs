using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Beatmaps;
using osu.Game.Tests.Beatmaps;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Overlays.CustomTheme;
using osu.Game.Overlays.Dialog;
using osu.Game.Tests.Visual;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Tests.Visual.Settings
{
    [TestFixture]
    public partial class TestSceneImageThemeDialog : OsuTestScene
    {
        private OsuConfigManager config = null!;
        private TestDialog dialog = null!;
        private ImageThemeAnalysis analysis = null!;
        private string before = string.Empty;
        private string originalId = string.Empty;

        [BackgroundDependencyLoader]
        private void load() => Dependencies.Cache(config = new OsuConfigManager(LocalStorage));

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("prepare profile and appearance", () =>
            {
                config.SetValue(OsuSetting.ForkThemeMode, ThemeMode.Custom);
                config.SetValue(OsuSetting.ForkCustomThemeJson, new CustomThemeDefinition(ThemeMode.Dark)
                {
                    Geometry = new ThemeGeometryDefinition { PanelRadius = 19, ButtonRadius = 7, TopBarVariant = TopBarVariant.Attached },
                    Interface = new ThemeInterfaceSettings { Font = "Inter", Animations = InterfaceAnimationMode.Minimal, OverlayTransparency = true },
                }.ToJson());
                before = config.Get<string>(OsuSetting.ForkCustomThemeJson);
                originalId = config.CustomThemes.ActiveId;
                using var image = new Image<Rgba32>(32, 32, new Rgba32(180, 40, 80));
                using var stream = new MemoryStream();
                image.SaveAsPng(stream);
                analysis = new ImageThemeAnalysis(new uint[] { 0xffb42850, 0xff356a91 }, stream.ToArray());
                var api = (DummyAPIAccess)API;
                api.LocalUser.Value = new APIUser { Id = 42, AvatarUrl = "test-avatar", CoverUrl = "test-banner" };
                api.HandleRequest = request =>
                {
                    if (request is not GetUserRequest profile) return false;
                    profile.TriggerSuccess(new APIUser { Id = 42, AvatarUrl = "test-avatar", CoverUrl = "test-banner", ProfileBackgroundUrl = "test-background" });
                    return true;
                };
                Child = dialog = new TestDialog(analysis) { Width = 680, RelativeSizeAxes = Axes.None };
            });
            AddUntilStep("dialog and refreshed profile loaded", () => dialog.IsLoaded && source(ImageThemeStrings.Background).Enabled.Value);
        }

        private OsuButton source(osu.Framework.Localisation.LocalisableString text) => dialog.ChildrenOfType<OsuButton>().Single(b => b.Text == text);
        private PopupDialogOkButton apply => dialog.ChildrenOfType<PopupDialogOkButton>().Single();

        [Test]
        public void TestPreviewAndFullThemeCreation()
        {
            int count = 0;
            AddAssert("cannot apply before analysis", () => !apply.Enabled.Value);
            AddStep("select banner", () => source(ImageThemeStrings.Banner).Action.Invoke());
            AddUntilStep("preview ready", () => dialog.ChildrenOfType<ImageThemeSample>().Any() && apply.Enabled.Value);
            AddAssert("source selected", () => dialog.LastUrl == "test-banner");
            AddAssert("preview does not modify config", () => config.CustomThemes.ActiveId == originalId && config.Get<string>(OsuSetting.ForkCustomThemeJson) == before);
            AddAssert("sample keeps panel shape", () => dialog.ChildrenOfType<ImageThemeSample>().Single().CornerRadius == 19);
            AddStep("choose rounded appearance", () => dialog.ChildrenOfType<FormDropdown<ImageThemeStyle>>().Single().Current.Value = ImageThemeStyle.Rounded);
            AddAssert("sample updates geometry without applying", () => dialog.ChildrenOfType<ImageThemeSample>().Single().CornerRadius == 18 && config.Get<string>(OsuSetting.ForkCustomThemeJson) == before);
            AddStep("choose light colours", () => dialog.ChildrenOfType<FormCheckBox>().Single().Current.Value = true);
            AddStep("name and apply", () =>
            {
                dialog.ChildrenOfType<FormTextBox>().Single().Current.Value = "My banner";
                count = config.CustomThemes.Entries.Count;
                apply.Action.Invoke();
            });
            AddAssert("new independent full theme", () => config.CustomThemes.Entries.Count == count + 1 && config.CustomThemes.ActiveId != originalId
                && config.CustomThemes.Active.Name == "My banner" && config.CustomThemes.Active.Theme.BaseTheme == ThemeMode.Light
                && config.CustomThemes.Active.Theme.TopBarVariant == TopBarVariant.Floating
                && config.CustomThemes.Active.Theme.Interface is { Font: "Inter", Animations: InterfaceAnimationMode.Minimal, OverlayTransparency: true });
            AddAssert("old theme retained", () => config.CustomThemes.Entries.Single(e => e.Id == originalId).Theme.ToJson() == before);
            AddAssert("dialog closes", () => dialog.State.Value == Visibility.Hidden);
        }

        [Test]
        public void TestSourcesNameValidationAndCancel()
        {
            AddAssert("cancel has a visible label", () => dialog.ChildrenOfType<PopupDialogCancelButton>().Single().Text == ImageThemeStrings.Cancel
                && dialog.ChildrenOfType<PopupDialogCancelButton>().Single().ChildrenOfType<SpriteText>().Any(t => !string.IsNullOrWhiteSpace(t.Text.ToString())));
            foreach (var (label, url) in new[] { (ImageThemeStrings.Avatar, "test-avatar"), (ImageThemeStrings.Banner, "test-banner"), (ImageThemeStrings.Background, "test-background") })
            {
                AddStep("select " + url, () => source(label).Action.Invoke());
                AddUntilStep("analysis complete", () => dialog.LastUrl == url && apply.Enabled.Value);
            }
            AddStep("empty name", () => dialog.ChildrenOfType<FormTextBox>().Single().Current.Value = "  ");
            AddAssert("disabled for empty name", () => !apply.Enabled.Value);
            AddStep("cancel", () => dialog.ChildrenOfType<PopupDialogCancelButton>().Single().Action.Invoke());
            AddAssert("config untouched", () => config.CustomThemes.ActiveId == originalId && config.Get<string>(OsuSetting.ForkCustomThemeJson) == before);
        }

        [Test]
        public void TestStaleAnalysisAndCloseDuringLoad()
        {
            TaskCompletionSource<ImageThemeAnalysis> delayed = null!;
            AddStep("delay avatar analysis", () =>
            {
                delayed = new TaskCompletionSource<ImageThemeAnalysis>(TaskCreationOptions.RunContinuationsAsynchronously);
                dialog.Pending = delayed.Task;
                source(ImageThemeStrings.Avatar).Action.Invoke();
            });
            AddAssert("apply disabled during analysis", () => !apply.Enabled.Value);
            AddStep("switch to banner", () =>
            {
                dialog.Pending = null;
                source(ImageThemeStrings.Banner).Action.Invoke();
            });
            AddUntilStep("banner ready", () => apply.Enabled.Value);
            AddStep("old avatar fails late", () => delayed.SetException(new InvalidDataException("stale")));
            AddWaitStep("deliver old completion", 3);
            AddAssert("banner preview retained", () => apply.Enabled.Value && dialog.ChildrenOfType<ImageThemeSample>().Any());
            AddStep("load background and close", () =>
            {
                delayed = new TaskCompletionSource<ImageThemeAnalysis>(TaskCreationOptions.RunContinuationsAsynchronously);
                dialog.Pending = delayed.Task;
                source(ImageThemeStrings.Background).Action.Invoke();
                dialog.Hide();
                delayed.SetResult(analysis);
            });
            AddWaitStep("deliver after close", 3);
            AddAssert("no late preview or config change", () => !dialog.ChildrenOfType<ImageThemeSample>().Any() && config.Get<string>(OsuSetting.ForkCustomThemeJson) == before);
        }

        [Test]
        public void TestFailedAnalysisCanRetry()
        {
            AddStep("fail extraction", () =>
            {
                dialog.Pending = Task.FromException<ImageThemeAnalysis>(new InvalidDataException("bad image"));
                source(ImageThemeStrings.Avatar).Action.Invoke();
            });
            AddUntilStep("failure shown", () => dialog.StatusText == ImageThemeStrings.Failed);
            AddAssert("cannot save failed result", () => !apply.Enabled.Value);
            AddStep("retry same image", () => { dialog.Pending = null; source(ImageThemeStrings.Avatar).Action.Invoke(); });
            AddUntilStep("retry succeeds", () => apply.Enabled.Value);
        }

        [Test]
        public void TestControlsFitNarrowDialog()
        {
            AddStep("analyse", () => source(ImageThemeStrings.Avatar).Action.Invoke());
            AddUntilStep("preview ready", () => apply.Enabled.Value);
            foreach (float width in new[] { 680f, 480f })
            {
                AddStep("resize " + width, () => dialog.Width = width);
                AddWaitStep("layout settles", 4);
                AddAssert("sources fit", () => new[] { ImageThemeStrings.Avatar, ImageThemeStrings.Banner, ImageThemeStrings.Background }.All(label =>
                    source(label).ScreenSpaceDrawQuad.TopRight.X <= dialog.MainContent.ScreenSpaceDrawQuad.TopRight.X + 1));
                AddAssert("preview fits", () => dialog.ChildrenOfType<ImageThemeSample>().Single().ScreenSpaceDrawQuad.TopRight.X <= dialog.MainContent.ScreenSpaceDrawQuad.TopRight.X + 1);
            }
        }

        [Test]
        public void TestMissingSourceAndBuiltInThemeDoNotReuseDormantGeometry()
        {
            AddStep("open from built-in dark mode without profile images", () =>
            {
                config.SetValue(OsuSetting.ForkThemeMode, ThemeMode.Dark);
                ((DummyAPIAccess)API).LocalUser.Value = new APIUser { Id = 42, AvatarUrl = "test-avatar" };
                ((DummyAPIAccess)API).HandleRequest = request =>
                {
                    if (request is not GetUserRequest profile) return false;
                    profile.TriggerSuccess(new APIUser { Id = 42, AvatarUrl = "test-avatar" });
                    return true;
                };
                Child = dialog = new TestDialog(analysis) { Width = 680, RelativeSizeAxes = Axes.None };
            });
            AddUntilStep("new dialog loaded", () => dialog.IsLoaded && dialog.StatusText == ImageThemeStrings.Choose);
            AddAssert("unset images disabled", () => !source(ImageThemeStrings.Banner).Enabled.Value && !source(ImageThemeStrings.Background).Enabled.Value);
            AddStep("use avatar", () => source(ImageThemeStrings.Avatar).Action.Invoke());
            AddUntilStep("ready", () => apply.Enabled.Value);
            AddAssert("default geometry rather than dormant custom shape", () => dialog.ChildrenOfType<ImageThemeSample>().Single().CornerRadius == OverlayColourProvider.GetDefaultRadius(GeometryRole.Panel));
        }

        [Test]
        public void TestCurrentBeatmapBackground()
        {
            AddStep("select a beatmap with background and reopen", () =>
            {
                var beatmap = new Beatmap();
                beatmap.Metadata.BackgroundFile = "background.jpg";
                Beatmap.Value = new BackgroundWorkingBeatmap(beatmap, analysis.Thumbnail);
                Child = dialog = new TestDialog(analysis) { Width = 680, RelativeSizeAxes = Axes.None };
            });
            AddUntilStep("current background available", () => dialog.IsLoaded && source(ImageThemeStrings.CurrentBackground).Enabled.Value);
            AddStep("select current background", () => source(ImageThemeStrings.CurrentBackground).Action.Invoke());
            AddUntilStep("background analysed", () => apply.Enabled.Value && dialog.LastUrl == "current-background");
            AddAssert("no config changed before apply", () => config.Get<string>(OsuSetting.ForkCustomThemeJson) == before);
        }

        private sealed class BackgroundWorkingBeatmap : TestWorkingBeatmap
        {
            private readonly byte[] image;
            public BackgroundWorkingBeatmap(IBeatmap beatmap, byte[] image) : base(beatmap) => this.image = image;
            internal override Stream OpenBackgroundStream() => new MemoryStream(image);
        }

        private partial class TestDialog : ImageThemeDialog
        {
            private readonly ImageThemeAnalysis analysis;
            public Task<ImageThemeAnalysis>? Pending;
            public string LastUrl = string.Empty;
            public TestDialog(ImageThemeAnalysis analysis) => this.analysis = analysis;
            protected override Task<ImageThemeAnalysis> AnalyseBackgroundAsync(WorkingBeatmap beatmap, CancellationToken cancellationToken)
            {
                LastUrl = "current-background";
                return base.AnalyseBackgroundAsync(beatmap, cancellationToken);
            }
            protected override Task<ImageThemeAnalysis> AnalyseImageAsync(string url, CancellationToken cancellationToken)
            {
                LastUrl = url;
                return Pending ?? Task.FromResult(analysis);
            }
        }
    }
}
