using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Testing;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osu.Game.Overlays.CustomTheme;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.HUD;
using osu.Game.Skinning;
using osu.Game.Skinning.Components;

namespace osu.Game.Tests.Visual.Gameplay
{
    public partial class TestSceneSkinComponentColours : PlayerTestScene
    {
        protected override osu.Game.Rulesets.Ruleset CreatePlayerRuleset() => new osu.Game.Rulesets.Osu.OsuRuleset();
        protected override bool Autoplay => true;

        private ThemeMode previousMode;
        private CustomThemeDefinition previousTheme = null!;
        private SkinnableContainer target => Player.ChildrenOfType<SkinnableContainer>().First();
        private ArgonCounterTextComponent counter = null!;
        private TextElement text = null!;
        private BoxElement box = null!;
        private ArgonWedgePiece wedge = null!;
        private ArgonSongProgress progress = null!;
        private ScrollingKeyCounter history = null!;
        private BlurredBeatmapBackground blur = null!;
        private TestTrigger keyTrigger = null!;
        private int captureRevision;
        private Colour4 textColour => text.ChildrenOfType<SkinSpriteText>().Single().Colour.TopLeft;

        [SetUpSteps]
        public override void SetUpSteps()
        {
            base.SetUpSteps();
            AddStep("save global palette", () =>
            {
                previousMode = OverlayColourProvider.CurrentTheme.Value;
                previousTheme = OverlayColourProvider.CustomTheme.Value;
                OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default;
            });
            AddUntilStep("HUD loaded", () => target.ComponentsLoaded);
            AddStep("add skin components", () =>
            {
                Add(counter = new ArgonCounterTextComponent(Anchor.TopLeft, "TEST") { Text = "123", ShowLabel = { Value = true } });
                target.Add(text = new TextElement());
                target.Add(box = new BoxElement());
                target.Add(wedge = new ArgonWedgePiece());
                target.Add(progress = new ArgonSongProgress());
                Add(keyTrigger = new TestTrigger());
                Add(history = new ScrollingKeyCounter(keyTrigger));
                target.Add(blur = new BlurredBeatmapBackground());
            });
            AddUntilStep("components loaded", () => blur.IsLoaded && counter.IsLoaded && progress.IsLoaded && history.IsLoaded);
        }

        [TearDownSteps]
        public void RestorePalette() => AddStep("restore global palette", () =>
        {
            OverlayColourProvider.CustomTheme.Value = previousTheme;
            OverlayColourProvider.CurrentTheme.Value = previousMode;
        });

        private static void applyTheme(Colour4 primary, Colour4 secondary, Colour4 accent, Colour4 background)
        {
            var theme = new CustomThemeDefinition();
            theme.SetOverride(OverlayColour.Content1, primary);
            theme.SetOverride(OverlayColour.Content2, secondary);
            theme.SetOverride(OverlayColour.Highlight1, accent);
            theme.SetOverride(OverlayColour.Background3, background);
            // A sidebar or results override must not leak into the gameplay HUD.
            theme.SetContextOverride(ThemeSurfaceContext.Editor, SurfaceColourRole.TextPrimary, Colour4.Yellow);
            OverlayColourProvider.CustomTheme.Value = theme;
            OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom;
        }

        [Test]
        public void TestLivePaletteAndReset()
        {
            var originalText = default(Colour4);
            var originalLabel = default(Colour4);
            AddStep("remember original colours", () =>
            {
                originalText = counter.TextColour.Value;
                originalLabel = counter.LabelColour.Value;
                captureRevision = blur.CaptureRevision;
            });
            AddStep("change existing interface colours", () => applyTheme(Colour4.Red, Colour4.Green, Colour4.Blue, Colour4.Purple));
            AddAssert("counter primary", () => (Colour4)counter.TextColour.Value, () => Is.EqualTo(Colour4.Red));
            AddAssert("counter label", () => (Colour4)counter.LabelColour.Value, () => Is.EqualTo(Colour4.Green));
            AddAssert("skin text primary", () => textColour, () => Is.EqualTo(Colour4.Red));
            AddAssert("panel background", () => (Colour4)box.Colour.TopLeft, () => Is.EqualTo(Colour4.Purple));
            AddAssert("history accent", () => (Colour4)history.ChildrenOfType<Container>().First(c => c.Masking).Colour.TopLeft, () => Is.EqualTo(Colour4.Blue));
            AddAssert("key label secondary", () => (Colour4)history.ChildrenOfType<ArgonKeyCounter>().Single().ChildrenOfType<Graphics.Sprites.OsuSpriteText>().First().Colour.TopLeft,
                () => Is.EqualTo(Colour4.Green));
            AddAssert("progress text primary", () => (Colour4)progress.ChildrenOfType<SongProgressInfo>().Single().ChildrenOfType<SizePreservingSpriteText>().First().Colour.TopLeft,
                () => Is.EqualTo(Colour4.Red));
            AddAssert("blur tint", () => blur.TintColour, () => Is.EqualTo(Colour4.Purple));
            AddAssert("palette did not recapture background", () => blur.CaptureRevision, () => Is.EqualTo(captureRevision));
            AddStep("edit palette again without changing mode", () => applyTheme(Colour4.Cyan, Colour4.Orange, Colour4.Green, Colour4.Blue));
            AddAssert("counter updated immediately", () => (Colour4)counter.TextColour.Value, () => Is.EqualTo(Colour4.Cyan));
            AddAssert("panel updated immediately", () => (Colour4)box.Colour.TopLeft, () => Is.EqualTo(Colour4.Blue));
            AddStep("hold key", () => keyTrigger.Press());
            AddAssert("held label uses primary", () => (Colour4)history.ChildrenOfType<ArgonKeyCounter>().Single().ChildrenOfType<OsuSpriteText>().First().Colour.TopLeft,
                () => Is.EqualTo(Colour4.Cyan));
            AddStep("change theme with key held", () => applyTheme(Colour4.Purple, Colour4.Green, Colour4.Blue, Colour4.Red));
            AddAssert("held label updates without release", () => (Colour4)history.ChildrenOfType<ArgonKeyCounter>().Single().ChildrenOfType<OsuSpriteText>().First().Colour.TopLeft,
                () => Is.EqualTo(Colour4.Purple));
            AddStep("release key", () => keyTrigger.Release());
            AddWaitStep("release colour transition", 5);
            AddAssert("idle label returns to new secondary", () => (Colour4)history.ChildrenOfType<ArgonKeyCounter>().Single().ChildrenOfType<OsuSpriteText>().First().Colour.TopLeft,
                () => Is.EqualTo(Colour4.Green));
            AddStep("disable custom theme", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default);
            AddAssert("original counter restored", () => (Colour4)counter.TextColour.Value, () => Is.EqualTo(originalText));
            AddAssert("original label restored", () => (Colour4)counter.LabelColour.Value, () => Is.EqualTo(originalLabel));
            AddAssert("original panel restored", () => (Colour4)box.Colour.TopLeft, () => Is.EqualTo(Colour4.White));
            AddAssert("original blur tint restored", () => blur.TintColour, () => Is.EqualTo(Colour4.Black));
        }

        [Test]
        public void TestIndividualSkinColoursAndSerialisation()
        {
            AddStep("choose individual colours", () =>
            {
                text.TextColour.Value = Colour4.Orange;
                box.AccentColour.Value = Colour4.Green;
                wedge.AccentColour.Value = Colour4.Red;
                progress.AccentColour.Value = Colour4.Purple;
            });
            AddStep("apply global palette", () => applyTheme(Colour4.Cyan, Colour4.Cyan, Colour4.Cyan, Colour4.Cyan));
            AddAssert("individual text survives", () => textColour, () => Is.EqualTo(Colour4.Orange));
            AddAssert("individual box survives", () => (Colour4)box.Colour.TopLeft, () => Is.EqualTo(Colour4.Green));
            AddAssert("individual wedge survives", () => (Colour4)wedge.ChildrenOfType<Box>().Single().Colour.BottomLeft, () => Is.EqualTo(Colour4.Red.Opacity(0.25f)));
            AddAssert("individual progress survives", () => (Colour4)progress.Colour.TopLeft, () => Is.EqualTo(Colour4.Purple));
            AddAssert("global palette not written to text setting", () => text.TextColour.Value, () => Is.EqualTo(Colour4.Orange));
            AddAssert("global palette not written to box setting", () => box.AccentColour.Value, () => Is.EqualTo(Colour4.Green));
            AddStep("roundtrip individual settings", () =>
            {
                var restoredText = (TextElement)text.CreateSerialisedInfo().CreateInstance();
                var restoredBox = (BoxElement)box.CreateSerialisedInfo().CreateInstance();
                target.Add(restoredText);
                target.Add(restoredBox);
                text = restoredText;
                box = restoredBox;
            });
            AddAssert("roundtrip text", () => textColour, () => Is.EqualTo(Colour4.Orange));
            AddAssert("roundtrip box", () => (Colour4)box.Colour.TopLeft, () => Is.EqualTo(Colour4.Green));
            AddStep("reset individual colours", () =>
            {
                text.TextColour.SetDefault();
                box.AccentColour.SetDefault();
            });
            AddAssert("reset text inherits theme", () => textColour, () => Is.EqualTo(Colour4.Cyan));
            AddAssert("reset box inherits theme", () => (Colour4)box.Colour.TopLeft, () => Is.EqualTo(Colour4.Cyan));
            AddAssert("text setting remains default", () => text.TextColour.IsDefault);
            AddAssert("box setting remains default", () => box.AccentColour.IsDefault);
        }

        [Test]
        public void TestFontsForArgonLegacyAndDynamicallyAddedKeys()
        {
            ArgonScoreCounter argon = null!;
            LegacyScoreCounter legacy = null!;
            LegacyPerformancePointsCounter pp = null!;
            AddStep("add font test counters", () =>
            {
                target.Add(argon = new ArgonScoreCounter());
                target.Add(legacy = new LegacyScoreCounter());
                target.Add(pp = new LegacyPerformancePointsCounter());
                argon.SetCountWithoutRolling(123456);
                legacy.SetCountWithoutRolling(123456);
                pp.SetCountWithoutRolling(123);
                SkinComponentFont.For(argon).Family.Value = "Inter";
                SkinComponentFont.For(legacy).Family.Value = "Inter";
                SkinComponentFont.For(pp).Family.Value = "Inter";
                SkinComponentFont.For(progress).Family.Value = "Inter";
            });
            AddUntilStep("fonts applied to loaded text", () => new CompositeDrawable[] { argon, legacy, pp, progress }
                .All(c => c.IsLoaded && c.ChildrenOfType<OsuSpriteText>().Any(t => t.IsLoaded) && c.ChildrenOfType<OsuSpriteText>()
                    .Where(t => t.IsLoaded).All(t => t.Font.Family == "Inter")));
            AddAssert("argon digits not one pixel", () => argon.ChildrenOfType<OsuSpriteText>().Where(t => t.Text.ToString().Contains('1')).All(t => t.Font.Size >= 20));
            AddAssert("legacy digits not one pixel", () => legacy.ChildrenOfType<LegacySpriteText>().Single().Font.Size, () => Is.GreaterThan(5));
            AddAssert("legacy pp suffix uses ordinary letters", () => pp.ChildrenOfType<LegacySpriteText>().Single().Text.ToString(), () => Does.EndWith("pp"));
            AddStep("change only argon family", () => SkinComponentFont.For(argon).Family.Value = "Torus");
            AddAssert("legacy family independent", () => legacy.ChildrenOfType<LegacySpriteText>().Single().Font.Family, () => Is.EqualTo("Inter"));
            AddStep("restore original fonts", () =>
            {
                SkinComponentFont.For(argon).Family.SetDefault();
                SkinComponentFont.For(legacy).Family.SetDefault();
                SkinComponentFont.For(pp).Family.SetDefault();
            });
            AddAssert("argon atlas restored at original scale", () => argon.ChildrenOfType<OsuSpriteText>()
                .Any(t => t.Font.Family == "argon-counter" && t.Font.Size == 1));
            AddAssert("legacy texture font restored", () => legacy.ChildrenOfType<LegacySpriteText>().Single().Font.Size, () => Is.EqualTo(1));
            AddAssert("legacy pp texture suffix restored", () => pp.ChildrenOfType<LegacySpriteText>().Single().Text.ToString(), () => Does.EndWith(LegacySpriteText.PP_SUFFIX_CHAR.ToString()));
        }

        [Test]
        public void TestKeyFontsAndGlobalFontPrecedence()
        {
            ScrollingKeyCounterDisplay keys = null!;
            string originalGlobalFont = null!;
            Configuration.OsuConfigManager config = null!;
            AddStep("add keys with per-component family", () =>
            {
                target.Add(keys = new ScrollingKeyCounterDisplay());
                SkinComponentFont.For(keys).Family.Value = "Nunito";
                config = Dependencies.Get<Configuration.OsuConfigManager>();
                originalGlobalFont = config.Get<string>(Configuration.OsuSetting.ForkNumericFont);
            });
            AddUntilStep("keys created after selection inherit font", () => keys.ChildrenOfType<ArgonKeyCounter>().Any()
                && keys.ChildrenOfType<OsuSpriteText>().Any(t => t.IsLoaded)
                && keys.ChildrenOfType<OsuSpriteText>().Where(t => t.IsLoaded).All(t => t.Font.Family == "Nunito"));
            AddStep("change global numeric family", () => config.SetValue(Configuration.OsuSetting.ForkNumericFont, "Inter"));
            AddAssert("individual family still wins", () => keys.ChildrenOfType<OsuSpriteText>().Where(t => t.IsLoaded).All(t => t.Font.Family == "Nunito"));
            AddStep("switch only this component", () => SkinComponentFont.For(keys).Family.Value = "Inter");
            AddAssert("all key names and counts updated", () => keys.ChildrenOfType<OsuSpriteText>().Where(t => t.IsLoaded).All(t => t.Font.Family == "Inter"));
            AddStep("restore global font", () => config.SetValue(Configuration.OsuSetting.ForkNumericFont, originalGlobalFont));
        }

        [Test]
        public void TestBlurUsesGameplayUnderlayAndCleansUpProxy()
        {
            GameplaySkinUnderlay underlay = null!;
            int previousCount = 0;
            AddStep("find gameplay underlay", () =>
            {
                underlay = Player.ChildrenOfType<GameplaySkinUnderlay>().Single();
                previousCount = underlay.Count;
            });
            AddAssert("panel draws through underlay proxy", () => blur.HasProxy && underlay.Children.Any(d => d.IsProxy));
            AddStep("remove skin panel", () => target.Remove(blur, true));
            AddUntilStep("underlay proxy removed", () => underlay.Count, () => Is.EqualTo(previousCount - 1));
        }

        [Test]
        public void TestCornerRadiusOnLoadedHudElements()
        {
            CompositeDrawable[] elements = null!;
            AddStep("round each non-native HUD element", () =>
            {
                elements = Player.ChildrenOfType<ISerialisableDrawable>().OfType<CompositeDrawable>()
                    .Where(d => !SkinComponentGeometry.HasNativeSetting(d)).ToArray();
                foreach (var element in elements)
                    SkinComponentGeometry.For(element).Radius.Value = 14;
            });
            AddWaitStep("layout updates", 5);
            AddAssert("unmasked key layout is not clipped", () => !history.Masking);
            AddAssert("blur surface rounded", () => blur.CornerRadius, () => Is.EqualTo(14));
            AddAssert("progress internals rounded", () => progress.ChildrenOfType<ArgonSongProgressBar>().Single().CornerRadius, () => Is.EqualTo(14));
            AddStep("square corners", () =>
            {
                foreach (var element in elements)
                    SkinComponentGeometry.For(element).Radius.Value = 0;
            });
            AddAssert("blur corners square", () => blur.CornerRadius, () => Is.Zero);
            AddAssert("key layout remains unclipped", () => !history.Masking);
            AddStep("reset geometry", () =>
            {
                foreach (var element in elements)
                    SkinComponentGeometry.For(element).Reset();
            });
        }

        [Test]
        public void TestJudgementColoursAreNotCosmetic()
        {
            var judgement = default(Colour4);
            ArgonJudgementCounter display = null!;
            AddStep("add judgement counter", () =>
            {
                target.ChildrenOfType<Container>().First().Add(display = new ArgonJudgementCounter(new Screens.Play.HUD.JudgementCounter.JudgementCount
                {
                    DisplayName = "Great",
                    Types = new[] { osu.Game.Rulesets.Scoring.HitResult.Great },
                    ResultCount = new osu.Framework.Bindables.BindableInt(),
                }));
            });
            AddUntilStep("judgement loaded", () => display.IsLoaded);
            AddStep("remember judgement colour", () => judgement = display.ChildrenOfType<ArgonCounterTextComponent>().Single().LabelColour.Value);
            AddStep("apply palette", () => applyTheme(Colour4.Red, Colour4.Red, Colour4.Red, Colour4.Red));
            AddAssert("judgement colour unchanged", () => (Colour4)display.ChildrenOfType<ArgonCounterTextComponent>().Single().LabelColour.Value, () => Is.EqualTo(judgement));
        }

        private partial class TestTrigger : InputTrigger
        {
            public TestTrigger() : base("K1") { }
            public void Press() => Activate();
            public void Release() => Deactivate();
        }
    }
}
