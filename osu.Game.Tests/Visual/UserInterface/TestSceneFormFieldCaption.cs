using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays;

namespace osu.Game.Tests.Visual.UserInterface
{
    public partial class TestSceneFormFieldCaption : OsuTestScene
    {
        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Blue);

        [Resolved]
        private FrameworkConfigManager frameworkConfig { get; set; } = null!;

        [TestCase(false)]
        [TestCase(true)]
        public void TestCaptionPreparedBeforeFirstUpdate(bool withTooltip)
        {
            FormFieldCaption caption = null!;
            SpriteText[] preparedText = Array.Empty<SpriteText>();
            SpriteIcon[] preparedIcons = Array.Empty<SpriteIcon>();
            bool ready = false;

            AddStep("load caption off tree", () => LoadComponentAsync(caption = new FormFieldCaption
            {
                Width = 300,
                Caption = "Prepared caption words",
                TooltipText = withTooltip ? "Prepared tooltip" : default,
            }, loaded =>
            {
                preparedText = loaded.ChildrenOfType<SpriteText>().ToArray();
                preparedIcons = loaded.ChildrenOfType<SpriteIcon>().ToArray();
                ready = true;
            }));
            AddUntilStep("load completed", () => ready);
            AddAssert("caption is ready, not updated", () => caption.LoadState == LoadState.Ready);
            AddAssert("words already prepared", () => preparedText.Length == (withTooltip ? 4 : 3) && preparedText.All(t => t.LoadState == LoadState.Ready));
            AddAssert("tooltip icon prepared when required", () => preparedIcons.Length == (withTooltip ? 1 : 0)
                                                                            && preparedIcons.All(i => i.LoadState == LoadState.Ready));
            AddStep("attach caption", () => Child = caption);
            AddUntilStep("caption loaded", () => caption.IsLoaded);
            AddAssert("first update keeps prepared words", () => caption.ChildrenOfType<SpriteText>().SequenceEqual(preparedText));
            AddAssert("first update keeps prepared icons", () => caption.ChildrenOfType<SpriteIcon>().SequenceEqual(preparedIcons));
        }

        [Test]
        public void TestReadyAndLoadedCaptionChanges()
        {
            FormFieldCaption caption = null!;
            bool ready = false;
            AddStep("load off tree", () => LoadComponentAsync(caption = new FormFieldCaption
            {
                Width = 300,
                Caption = "Original",
            }, _ => ready = true));
            AddUntilStep("ready", () => ready);
            AddStep("change before attachment", () =>
            {
                caption.Caption = "Ready replacement";
                caption.TooltipText = "Ready hint";
                Child = caption;
            });
            AddUntilStep("replacement loaded", () => caption.IsLoaded
                                                    && string.Concat(caption.ChildrenOfType<SpriteText>().Select(t => t.Text.ToString())).Trim() == "Ready replacement");
            AddAssert("one hint icon", () => caption.ChildrenOfType<SpriteIcon>().Count() == 1);
            AddStep("change loaded caption and remove hint", () =>
            {
                caption.Caption = "Loaded";
                caption.TooltipText = default;
            });
            AddUntilStep("loaded replacement", () => caption.ChildrenOfType<SpriteText>().SingleOrDefault()?.Text.ToString() == "Loaded");
            AddAssert("hint removed", () => !caption.ChildrenOfType<SpriteIcon>().Any());
        }

        [Test]
        public void TestLocalisationAfterBackgroundPreparation()
        {
            string previousLocale = string.Empty;
            FormFieldCaption caption = null!;
            string initial = string.Empty;
            AddStep("set English and create caption", () =>
            {
                previousLocale = frameworkConfig.Get<string>(FrameworkSetting.Locale);
                frameworkConfig.SetValue(FrameworkSetting.Locale, "en");
                LoadComponentAsync(caption = new FormFieldCaption
                {
                    Width = 300,
                    Caption = SettingsStrings.HeaderTitle,
                    TooltipText = "Hint",
                }, loaded => Child = loaded);
            });
            AddUntilStep("English caption loaded", () => caption.IsLoaded && caption.ChildrenOfType<SpriteText>().Any());
            AddStep("record text and switch locale", () =>
            {
                initial = string.Concat(caption.ChildrenOfType<SpriteText>().Select(t => t.Text.ToString()));
                frameworkConfig.SetValue(FrameworkSetting.Locale, "ru");
            });
            AddUntilStep("caption re-localised", () => string.Concat(caption.ChildrenOfType<SpriteText>().Select(t => t.Text.ToString())) != initial);
            AddAssert("tooltip icon remains single", () => caption.ChildrenOfType<SpriteIcon>().Count() == 1);
            AddStep("restore locale", () => frameworkConfig.SetValue(FrameworkSetting.Locale, previousLocale));
        }
    }
}
