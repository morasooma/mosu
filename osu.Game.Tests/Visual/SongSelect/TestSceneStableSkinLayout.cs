using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Pooling;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Testing;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics.Carousel;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osu.Game.Screens.Select;
using osu.Game.Screens.Select.Filter;
using osu.Game.Skinning;
using osu.Game.Skinning.Select;
using osu.Game.Tests.Resources;
using osuTK;
using osuTK.Input;

namespace osu.Game.Tests.Visual.SongSelect
{
    [HeadlessTest]
    public partial class TestSceneStableSkinLayout : OsuManualInputManagerTestScene
    {
        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Aquamarine);

        [Resolved]
        private IRenderer renderer { get; set; } = null!;

        private readonly SoloSongSelect songSelect = new SoloSongSelect();

        [Test]
        public void TestEnteringCardDoesNotInheritPreviousHorizontalOffset()
        {
            OffsetProbeCarousel carousel = null!;
            PanelGroup panel = null!;
            AddStep("load stable carousel and panel", () =>
            {
                Dependencies.Get<OsuConfigManager>().SetValue(OsuSetting.ForkSongSelectStyle, ForkSongSelectStyle.LegacySkinned);
                Child = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        carousel = new OffsetProbeCarousel
                        {
                            RelativeSizeAxes = Axes.Both,
                            BeatmapSource = new BindableList<BeatmapSetInfo>(),
                            RequestSelection = _ => { },
                            RequestRecommendedSelection = _ => { },
                        },
                        panel = new PanelGroup { Item = new CarouselItem(new GroupDefinition(0, "Group")), X = -200 },
                    },
                };
            });
            AddUntilStep("components loaded", () => carousel.IsLoaded && panel.IsLoaded);
            AddStep("first X uses current row, not stale selected X", () =>
            {
                float expected = StableCarouselLayout.CurveOffset((float)panel.DrawYPosition + panel.DrawHeight / 2, carousel.DrawHeight)
                                 - StableCarouselLayout.ExpandedOffset;
                Assert.That(carousel.ProbeX(panel), Is.EqualTo(expected).Within(0.01));
            });
        }

        private partial class OffsetProbeCarousel : BeatmapCarousel
        {
            public float ProbeX(Drawable panel) => GetPanelXOffset(panel);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestReusedCardDoesNotAnimateFromSelectedColour(bool standalone)
        {
            DrawablePool<PanelBeatmapStandalone> difficultyPool = null!;
            DrawablePool<PanelBeatmapSet> setPool = null!;
            Panel panel = null!;
            AddStep("load stable card pool", () =>
            {
                Dependencies.Get<OsuConfigManager>().SetValue(OsuSetting.ForkSongSelectStyle, ForkSongSelectStyle.LegacySkinned);
                Child = new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    CachedDependencies = new (Type, object)[] { (typeof(ISongSelect), songSelect) },
                    Children = new Drawable[]
                    {
                        difficultyPool = new DrawablePool<PanelBeatmapStandalone>(0, 1),
                        setPool = new DrawablePool<PanelBeatmapSet>(0, 1),
                    },
                };
            });
            AddStep("show selected card", () =>
            {
                var set = TestResources.CreateTestBeatmapSetInfo();
                panel = standalone
                    ? difficultyPool.Get(p => p.Item = new CarouselItem(new GroupedBeatmap(null, set.Beatmaps.First())))
                    : setPool.Get(p => p.Item = new CarouselItem(new GroupedBeatmapSet(null, set)));
                panel.Selected.Value = panel.Expanded.Value = true;
                ((Container)Child).Add(panel);
            });
            AddUntilStep("selected tint settled", () => panel.ChildrenOfType<LegacyMenuButtonBackground>().Single().Colour == StableCarouselLayout.Selected);
            AddStep("reuse for unselected card", () =>
            {
                ((Container)Child).Remove(panel, false);
                var set = TestResources.CreateTestBeatmapSetInfo();
                Panel reused = standalone
                    ? difficultyPool.Get(p => p.Item = new CarouselItem(new GroupedBeatmap(null, set.Beatmaps.First())))
                    : setPool.Get(p => p.Item = new CarouselItem(new GroupedBeatmapSet(null, set)));
                Assert.That(reused, Is.SameAs(panel));
                panel.Selected.Value = panel.Expanded.Value = false;
                ((Container)Child).Add(panel);
            });
            AddAssert("first prepared frame is inactive, not fading from selected", () =>
                panel.ChildrenOfType<LegacyMenuButtonBackground>().Single().Colour == StableCarouselLayout.Inactive);
        }

        [TestCase(64, 64)]
        [TestCase(104, 50)]
        public void TestStableStarsScaleTheWholeSkinTexture(int width, int height)
        {
            StableStarCounter counter = null!;
            Texture texture = null!;
            AddStep("create skin star larger than display slot", () =>
            {
                var skin = new LayoutSkin();
                skin.Textures["star"] = texture = renderer.CreateTexture(width, height);
                Child = new SkinProvidingContainer(skin)
                {
                    Child = counter = new StableStarCounter { Current = 5 },
                };
            });
            AddUntilStep("star sprites loaded", () => counter.ChildrenOfType<Sprite>().Count() == 20);
            AddAssert("all stars keep the full texture, not a central crop", () =>
                counter.ChildrenOfType<Sprite>().All(s => ReferenceEquals(s.Texture, texture)));
            AddAssert("full texture is scaled to fit the slot", () => counter.ChildrenOfType<Sprite>()
                .All(s => Math.Abs(Math.Max(s.DrawWidth, s.DrawHeight) - 20) < 0.01f));
        }

        [TestCase(40, 30)]
        [TestCase(150, 180)]
        [TestCase(300, 260)]
        public void TestCustomFooterButtonGeometryAndInput(int width, int height)
        {
            LegacyFooterButton button = null!;
            int clicks = 0;
            AddStep("create custom-sized button", () =>
            {
                var skin = new LayoutSkin();
                skin.Textures["selection-mods"] = renderer.CreateTexture(width, height);
                skin.Textures["selection-mods-over"] = renderer.CreateTexture(width, height);
                Child = new SkinProvidingContainer(skin)
                {
                    Child = button = new LegacyFooterButton("mods") { X = 100, Action = () => clicks++ },
                };
            });
            AddUntilStep("button ready", () => button.IsLoaded);
            AddAssert("hitbox follows texture", () => button.DrawSize == new Vector2(width, height));
            AddAssert("custom top is fixed above bottom", () => button.Origin == Anchor.TopLeft && button.Y == -86.4f);
            AddStep("click top edge outside old fixed slot", () =>
            {
                InputManager.MoveMouseTo(button.ToScreenSpace(new Vector2(width / 2f, 1)));
                InputManager.Click(MouseButton.Left);
            });
            AddAssert("top edge clicked", () => clicks == 1);
            AddStep("click beyond custom width", () =>
            {
                InputManager.MoveMouseTo(button.ToScreenSpace(new Vector2(width + 2, 1)));
                InputManager.Click(MouseButton.Left);
            });
            AddAssert("outside texture is not a button", () => clicks == 1);
        }

        [Test]
        public void TestFooterHitboxUsesHoverTextureRatherThanBaseSize()
        {
            LegacyFooterButton button = null!;
            int clicks = 0;
            AddStep("create different base and hover sizes", () =>
            {
                var skin = new LayoutSkin();
                skin.Textures["selection-mods"] = renderer.CreateTexture(150, 180);
                skin.Textures["selection-mods-over"] = renderer.CreateTexture(40, 30);
                Child = new SkinProvidingContainer(skin)
                {
                    Child = button = new LegacyFooterButton("mods") { X = 100, Action = () => clicks++ },
                };
            });
            AddUntilStep("button loaded", () => button.IsLoaded);
            AddAssert("only hover texture defines hitbox", () => button.DrawSize == new Vector2(40, 30));
            AddStep("click base-only region", () =>
            {
                InputManager.MoveMouseTo(button.ToScreenSpace(new Vector2(60, 1)));
                InputManager.Click(MouseButton.Left);
            });
            AddAssert("base-only region does not capture clicks", () => clicks == 0);
        }

        [Test]
        public void TestSuppressedModeDecorationKeepsHoverHitbox()
        {
            LegacyFooterButton button = null!;
            int clicks = 0;
            AddStep("create decorative mode button", () =>
            {
                var skin = new LayoutSkin();
                skin.Textures["selection-mods"] = renderer.CreateTexture(77, 90);
                skin.Textures["selection-mode"] = renderer.CreateTexture(1200, 900);
                skin.Textures["selection-mode-over"] = renderer.CreateTexture(94, 90);
                Child = new SkinProvidingContainer(skin)
                {
                    Child = button = new LegacyFooterButton("mode") { SuppressBaseGlyph = true, X = 100, Action = () => clicks++ },
                };
            });
            AddUntilStep("mode ready", () => button.IsLoaded);
            AddAssert("mode hitbox is not decoration-sized", () => button.DrawSize == new Vector2(94, 90));
            AddStep("click mode", () =>
            {
                InputManager.MoveMouseTo(button.ToScreenSpace(new Vector2(90, 1)));
                InputManager.Click(MouseButton.Left);
            });
            AddAssert("mode clicked", () => clicks == 1);
        }

        [Test]
        public void TestCustomTopReplacesFallbackAndReloads()
        {
            MutableSkinContainer provider = null!;
            LegacySongSelectTop top = null!;
            Texture custom = null!;
            FilterControl filter = null!;
            AddStep("load filter control", () => Child = filter = new FilterControl());
            AddUntilStep("filter loaded", () => filter.IsLoaded);
            AddStep("create custom top", () =>
            {
                var skin = new LayoutSkin();
                skin.Textures["songselect-top"] = custom = renderer.CreateTexture(1366, 155);
                Child = provider = new MutableSkinContainer(skin)
                {
                    Child = top = new LegacySongSelectTop { FilterControl = filter },
                };
            });
            AddUntilStep("top loaded", () => top.IsLoaded);
            AddAssert("exactly one top texture with no stock backing", () =>
                top.ChildrenOfType<LegacyScreenTexture>().Count() == 1
                && ReferenceEquals(top.ChildrenOfType<LegacyScreenTexture>().Single().Texture, custom));
            AddStep("remove custom top", () => provider.Change(new LayoutSkin()));
            AddUntilStep("classic fallback used", () => top.ChildrenOfType<LegacyScreenTexture>().Count() == 1
                && !ReferenceEquals(top.ChildrenOfType<LegacyScreenTexture>().Single().Texture, custom));
            AddStep("restore custom top", () =>
            {
                var skin = new LayoutSkin();
                skin.Textures["songselect-top"] = custom;
                provider.Change(skin);
            });
            AddUntilStep("custom replaces fallback again", () => top.ChildrenOfType<LegacyScreenTexture>().Count() == 1
                && ReferenceEquals(top.ChildrenOfType<LegacyScreenTexture>().Single().Texture, custom));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestCardArtistCreatorLineAndStyleSwitch(bool standalone)
        {
            Panel panel = null!;
            AddStep("create stable card", () =>
            {
                Dependencies.Get<OsuConfigManager>().SetValue(OsuSetting.ForkSongSelectStyle, ForkSongSelectStyle.LegacySkinned);
                var set = TestResources.CreateTestBeatmapSetInfo();
                var metadata = (BeatmapMetadata)set.Metadata;
                metadata.Artist = "Artist";
                metadata.ArtistUnicode = "Artist";
                metadata.Author.Username = "Creator";
                var map = set.Beatmaps.First();
                map.Metadata.Author.Username = "Creator";
                map.DifficultyName = "Difficulty";
                panel = standalone
                    ? new PanelBeatmapStandalone { Item = new CarouselItem(new GroupedBeatmap(null, map)) }
                    : new PanelBeatmapSet { Item = new CarouselItem(new GroupedBeatmapSet(null, set)) };
                Child = new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    CachedDependencies = new (Type, object)[] { (typeof(ISongSelect), songSelect) },
                    Child = panel,
                };
            });
            AddUntilStep("artist creator line present", () => panel.ChildrenOfType<OsuSpriteText>()
                .Any(t => t.IsPresent && t.Text.ToString() == "Artist // Creator"));
            AddAssert("mapped-by text hidden in stable", () => !panel.ChildrenOfType<OsuSpriteText>()
                .Any(t => t.IsPresent && t.Text.ToString().Contains("Creator") && t.Text.ToString() != "Artist // Creator"));
            AddStep("switch modern", () => Dependencies.Get<OsuConfigManager>().SetValue(OsuSetting.ForkSongSelectStyle, ForkSongSelectStyle.Modern));
            AddUntilStep("plain artist restored", () => panel.ChildrenOfType<OsuSpriteText>().Any(t => t.IsPresent && t.Text.ToString() == "Artist"));
            AddStep("switch back stable", () => Dependencies.Get<OsuConfigManager>().SetValue(OsuSetting.ForkSongSelectStyle, ForkSongSelectStyle.LegacySkinned));
            AddUntilStep("artist creator restored", () => panel.ChildrenOfType<OsuSpriteText>().Any(t => t.IsPresent && t.Text.ToString() == "Artist // Creator"));
        }

        [Test]
        public void TestGroupDisplaysDifficultyCount()
        {
            PanelGroup panel = null!;
            AddStep("create stable group", () =>
            {
                Dependencies.Get<OsuConfigManager>().SetValue(OsuSetting.ForkSongSelectStyle, ForkSongSelectStyle.LegacySkinned);
                Child = new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    CachedDependencies = new (Type, object)[] { (typeof(ISongSelect), songSelect) },
                    Child = panel = new PanelGroup
                    {
                        Item = new CarouselItem(new GroupDefinition(0, "Yesterday")) { NestedItemCount = 17 },
                    },
                };
            });
            AddUntilStep("count in group title", () => panel.ChildrenOfType<OsuSpriteText>().Any(t => t.Text.ToString() == "Yesterday (17 maps)"));
            AddStep("expand", () => panel.Expanded.Value = true);
            AddAssert("count remains on expanded group", () => panel.ChildrenOfType<OsuSpriteText>().Any(t => t.Text.ToString() == "Yesterday (17 maps)"));
            AddStep("modern style", () => Dependencies.Get<OsuConfigManager>().SetValue(OsuSetting.ForkSongSelectStyle, ForkSongSelectStyle.Modern));
            AddUntilStep("modern title unchanged", () => panel.ChildrenOfType<OsuSpriteText>().Any(t => t.Text.ToString() == "Yesterday"));
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
                songSelect.Dispose();
            base.Dispose(isDisposing);
        }

        private partial class MutableSkinContainer : SkinProvidingContainer
        {
            public MutableSkinContainer(ISkin skin) : base(skin) { }
            public void Change(ISkin skin)
            {
                SetSources(new[] { skin });
                TriggerSourceChanged();
            }
        }

        private class LayoutSkin : ISkin
        {
            public readonly Dictionary<string, Texture> Textures = new Dictionary<string, Texture>();
            public Drawable? GetDrawableComponent(ISkinComponentLookup lookup)
                => lookup is SkinnableSprite.SpriteComponentLookup sprite
                    ? this.GetAnimation(sprite.LookupName, false, false, maxSize: sprite.MaxSize)
                    : null;
            public Texture? GetTexture(string name, WrapMode wrapModeS, WrapMode wrapModeT) => Textures.GetValueOrDefault(name);
            public ISample? GetSample(ISampleInfo sampleInfo) => null;
            public IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup) where TLookup : notnull where TValue : notnull => null;
        }
    }
}
