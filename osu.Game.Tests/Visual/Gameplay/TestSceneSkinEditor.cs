// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings;
using osu.Game.Overlays.SkinEditor;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Components;
using osu.Game.Screens.Play.HUD;
using osu.Game.Skinning;
using osu.Game.Skinning.Components;
using osu.Game.Tests.Resources;
using osuTK;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Gameplay
{
    public partial class TestSceneSkinEditor : PlayerTestScene
    {
        private SkinEditor skinEditor = null!;

        protected override bool Autoplay => true;

        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Blue);

        [Cached]
        public readonly EditorClipboard Clipboard = new EditorClipboard();

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        private SkinnableContainer targetContainer => Player.ChildrenOfType<SkinnableContainer>().First();

        [SetUpSteps]
        public override void SetUpSteps()
        {
            base.SetUpSteps();

            AddStep("reset skin", () => skins.CurrentSkinInfo.SetDefault());
            AddUntilStep("wait for hud load", () => targetContainer.ComponentsLoaded);

            AddStep("reload skin editor", () =>
            {
                if (skinEditor.IsNotNull())
                    skinEditor.Expire();
                Player.ScaleTo(0.4f);
                LoadComponentAsync(skinEditor = new SkinEditor(Player), Add);
            });
            AddUntilStep("wait for loaded", () => skinEditor.IsLoaded);
        }

        [Test]
        public void TestDragSelection()
        {
            BigBlackBox box1 = null!;
            BigBlackBox box2 = null!;
            BigBlackBox box3 = null!;

            AddStep("Add big black boxes", () =>
            {
                var target = Player.ChildrenOfType<SkinnableContainer>().First();
                target.Add(box1 = new BigBlackBox
                {
                    Position = new Vector2(-90),
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                });
                target.Add(box2 = new BigBlackBox
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                });
                target.Add(box3 = new BigBlackBox
                {
                    Position = new Vector2(90),
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                });
            });

            // This step is specifically added to reproduce an edge case which was found during cyclic selection development.
            // If everything is working as expected it should not affect the subsequent drag selections.
            AddRepeatStep("Select top left", () =>
            {
                InputManager.MoveMouseTo(box1.ScreenSpaceDrawQuad.TopLeft + new Vector2(box1.ScreenSpaceDrawQuad.Width / 8));
                InputManager.Click(MouseButton.Left);
            }, 2);

            AddStep("Begin drag top left", () =>
            {
                InputManager.MoveMouseTo(box1.ScreenSpaceDrawQuad.TopLeft - new Vector2(box1.ScreenSpaceDrawQuad.Width / 4, box1.ScreenSpaceDrawQuad.Height / 8));
                InputManager.PressButton(MouseButton.Left);
            });

            AddStep("Drag to bottom right", () =>
            {
                InputManager.MoveMouseTo(box3.ScreenSpaceDrawQuad.TopRight + new Vector2(-box3.ScreenSpaceDrawQuad.Width / 8, box3.ScreenSpaceDrawQuad.Height / 4));
            });

            AddStep("Release button", () =>
            {
                InputManager.ReleaseButton(MouseButton.Left);
            });

            AddAssert("First two boxes selected", () => skinEditor.SelectedComponents, () => Is.EqualTo(new[] { box1, box2 }));

            AddStep("Begin drag bottom right", () =>
            {
                InputManager.MoveMouseTo(box3.ScreenSpaceDrawQuad.BottomRight + new Vector2(box3.ScreenSpaceDrawQuad.Width / 4));
                InputManager.PressButton(MouseButton.Left);
            });

            AddStep("Drag to top left", () =>
            {
                InputManager.MoveMouseTo(box2.ScreenSpaceDrawQuad.Centre - new Vector2(box2.ScreenSpaceDrawQuad.Width / 4));
            });

            AddStep("Release button", () =>
            {
                InputManager.ReleaseButton(MouseButton.Left);
            });

            AddAssert("Last two boxes selected", () => skinEditor.SelectedComponents, () => Is.EqualTo(new[] { box2, box3 }));

            // Test cyclic selection doesn't trigger in this state.
            AddStep("click on black box stack", () => InputManager.Click(MouseButton.Left));
            AddAssert("Last two boxes still selected", () => skinEditor.SelectedComponents, () => Is.EqualTo(new[] { box2, box3 }));
        }

        [Test]
        public void TestCyclicSelection()
        {
            List<SkinBlueprint> blueprints = new List<SkinBlueprint>();

            AddStep("clear list", blueprints.Clear);

            for (int i = 0; i < 3; i++)
            {
                AddStep("Add big black box", () =>
                {
                    skinEditor.ChildrenOfType<SkinComponentToolbox.ToolboxComponentButton>().First(b => b.ChildrenOfType<BigBlackBox>().FirstOrDefault() != null).TriggerClick();
                });

                AddStep("store box", () =>
                {
                    // Add blueprints one-by-one so we have a stable order for testing reverse cyclic selection against.
                    blueprints.Add(skinEditor.ChildrenOfType<SkinBlueprint>().Single(s => s.IsSelected));
                });
            }

            AddAssert("Three black boxes added", () => targetContainer.Components.OfType<BigBlackBox>().Count(), () => Is.EqualTo(3));

            AddAssert("Selection is last", () => skinEditor.SelectedComponents.Single(), () => Is.EqualTo(blueprints[2].Item));

            AddStep("move cursor to black box", () =>
            {
                // Slightly offset from centre to avoid random failures (see https://github.com/ppy/osu-framework/issues/5669).
                InputManager.MoveMouseTo(((Drawable)blueprints[0].Item).ScreenSpaceDrawQuad.Centre + new Vector2(1));
            });

            AddStep("click on black box stack", () => InputManager.Click(MouseButton.Left));
            AddAssert("Selection is second last", () => skinEditor.SelectedComponents.Single(), () => Is.EqualTo(blueprints[1].Item));

            AddStep("click on black box stack", () => InputManager.Click(MouseButton.Left));
            AddAssert("Selection is last", () => skinEditor.SelectedComponents.Single(), () => Is.EqualTo(blueprints[0].Item));

            AddStep("click on black box stack", () => InputManager.Click(MouseButton.Left));
            AddAssert("Selection is first", () => skinEditor.SelectedComponents.Single(), () => Is.EqualTo(blueprints[2].Item));

            AddStep("select all boxes", () =>
            {
                skinEditor.SelectedComponents.Clear();
                skinEditor.SelectedComponents.AddRange(targetContainer.Components.OfType<BigBlackBox>().Skip(1));
            });

            AddAssert("all boxes selected", () => skinEditor.SelectedComponents, () => Has.Count.EqualTo(2));
            AddStep("click on black box stack", () => InputManager.Click(MouseButton.Left));
            AddStep("click on black box stack", () => InputManager.Click(MouseButton.Left));
            AddStep("click on black box stack", () => InputManager.Click(MouseButton.Left));
            AddAssert("all boxes still selected", () => skinEditor.SelectedComponents, () => Has.Count.EqualTo(2));
        }

        [Test]
        public void TestUndoEditHistory()
        {
            SkinnableContainer firstTarget = null!;
            TestSkinEditorChangeHandler changeHandler = null!;
            byte[] defaultState = null!;
            IEnumerable<ISerialisableDrawable> testComponents = null!;

            AddStep("Load necessary things", () =>
            {
                firstTarget = Player.ChildrenOfType<SkinnableContainer>().First();
                changeHandler = new TestSkinEditorChangeHandler(firstTarget);

                changeHandler.SaveState();
                defaultState = changeHandler.GetCurrentState();

                testComponents = new[]
                {
                    targetContainer.Components.First(),
                    targetContainer.Components[targetContainer.Components.Count / 2],
                    targetContainer.Components.Last()
                };
            });

            AddStep("Press undo", () => InputManager.Keys(PlatformAction.Undo));
            AddAssert("Nothing changed", () => defaultState.SequenceEqual(changeHandler.GetCurrentState()));

            AddStep("Add components", () =>
            {
                InputManager.MoveMouseTo(skinEditor.ChildrenOfType<BigBlackBox>().First());
                InputManager.Click(MouseButton.Left);
                InputManager.Click(MouseButton.Left);
                InputManager.Click(MouseButton.Left);
            });
            revertAndCheckUnchanged();

            AddStep("Move components", () =>
            {
                changeHandler.BeginChange();
                testComponents.ForEach(c => ((Drawable)c).Position += Vector2.One);
                changeHandler.EndChange();
            });
            revertAndCheckUnchanged();

            AddStep("Select components", () => skinEditor.SelectedComponents.AddRange(testComponents));
            AddStep("Bring to front", () => skinEditor.BringSelectionToFront());
            revertAndCheckUnchanged();

            AddStep("Remove components", () => testComponents.ForEach(c => firstTarget.Remove(c, false)));
            revertAndCheckUnchanged();

            void revertAndCheckUnchanged()
            {
                AddStep("Revert changes", () => changeHandler.RestoreState(int.MinValue));
                AddAssert("Current state is same as default",
                    () => Encoding.UTF8.GetString(defaultState),
                    () => Is.EqualTo(Encoding.UTF8.GetString(changeHandler.GetCurrentState())));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestBringToFront(bool alterSelectionOrder)
        {
            AddAssert("Ensure over three components available", () => targetContainer.Components.Count, () => Is.GreaterThan(3));

            IEnumerable<ISerialisableDrawable> originalOrder = null!;

            AddStep("Save order of components before operation", () => originalOrder = targetContainer.Components.Take(3).ToArray());

            if (alterSelectionOrder)
                AddStep("Select first three components in reverse order", () => skinEditor.SelectedComponents.AddRange(originalOrder.Reverse()));
            else
                AddStep("Select first three components", () => skinEditor.SelectedComponents.AddRange(originalOrder));

            AddAssert("Components are not front-most", () => targetContainer.Components.TakeLast(3).ToArray(), () => Is.Not.EqualTo(skinEditor.SelectedComponents));

            AddStep("Bring to front", () => skinEditor.BringSelectionToFront());
            AddAssert("Ensure components are now front-most in original order", () => targetContainer.Components.TakeLast(3).ToArray(), () => Is.EqualTo(originalOrder));
            AddStep("Bring to front again", () => skinEditor.BringSelectionToFront());
            AddAssert("Ensure components are still front-most in original order", () => targetContainer.Components.TakeLast(3).ToArray(), () => Is.EqualTo(originalOrder));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestSendToBack(bool alterSelectionOrder)
        {
            AddAssert("Ensure over three components available", () => targetContainer.Components.Count, () => Is.GreaterThan(3));

            IEnumerable<ISerialisableDrawable> originalOrder = null!;

            AddStep("Save order of components before operation", () => originalOrder = targetContainer.Components.TakeLast(3).ToArray());

            if (alterSelectionOrder)
                AddStep("Select last three components in reverse order", () => skinEditor.SelectedComponents.AddRange(originalOrder.Reverse()));
            else
                AddStep("Select last three components", () => skinEditor.SelectedComponents.AddRange(originalOrder));

            AddAssert("Components are not back-most", () => targetContainer.Components.Take(3).ToArray(), () => Is.Not.EqualTo(skinEditor.SelectedComponents));

            AddStep("Send to back", () => skinEditor.SendSelectionToBack());
            AddAssert("Ensure components are now back-most in original order", () => targetContainer.Components.Take(3).ToArray(), () => Is.EqualTo(originalOrder));
            AddStep("Send to back again", () => skinEditor.SendSelectionToBack());
            AddAssert("Ensure components are still back-most in original order", () => targetContainer.Components.Take(3).ToArray(), () => Is.EqualTo(originalOrder));
        }

        [Test]
        public void TestBeatmapDifficultyPlacementAndReload()
        {
            AddAssert("not inserted into default HUD", () => !globalHUDTarget.Components.OfType<BeatmapDifficultyDisplay>().Any());
            AddUntilStep("difficulty pill in Morasooma toolbox", () => skinEditor.ChildrenOfType<SkinComponentToolbox>()
                .Any(t => t.MorasoomaOnly && t.ChildrenOfType<BeatmapDifficultyDisplay>().Any()));
            AddStep("place difficulty pill", () => skinEditor.ChildrenOfType<SkinComponentToolbox.ToolboxComponentButton>()
                .Single(b => b.ChildrenOfType<BeatmapDifficultyDisplay>().Any()).RequestPlacement?.Invoke(typeof(BeatmapDifficultyDisplay)));
            AddUntilStep("pill selected", () => skinEditor.SelectedComponents.OfType<BeatmapDifficultyDisplay>().Any());
            AddStep("save skin", () => skinEditor.Save());
            AddStep("reload HUD", () => globalHUDTarget.Reload());
            AddUntilStep("pill restored", () => globalHUDTarget.ComponentsLoaded && globalHUDTarget.Components.OfType<BeatmapDifficultyDisplay>().Count() == 1);
        }

        [Test]
        public void TestScrollingKeyCounterPlacementAndReload()
        {
            ScrollingKeyCounterDisplay placed = null!;

            AddAssert("not added to the default layout", () => !globalHUDTarget.Components.OfType<ScrollingKeyCounterDisplay>().Any());
            AddUntilStep("scrolling counter available in toolbox", () => skinEditor.ChildrenOfType<SkinComponentToolbox.ToolboxComponentButton>()
                .Any(b => b.ChildrenOfType<ScrollingKeyCounterDisplay>().Any()));
            AddStep("choose scrolling counter from toolbox", () =>
            {
                var button = skinEditor.ChildrenOfType<SkinComponentToolbox.ToolboxComponentButton>()
                                       .Single(b => b.ChildrenOfType<ScrollingKeyCounterDisplay>().Any());
                button.RequestPlacement?.Invoke(typeof(ScrollingKeyCounterDisplay));
            });
            AddUntilStep("new component selected", () => skinEditor.SelectedComponents.OfType<ScrollingKeyCounterDisplay>().Any());
            AddStep("keep placed counter", () => placed = skinEditor.SelectedComponents.OfType<ScrollingKeyCounterDisplay>().Single());
            AddUntilStep("gameplay keys loaded", () => placed.ChildrenOfType<ScrollingKeyCounter>().Any());
            AddUntilStep("vertical resize handle enabled", () => skinEditor.ChildrenOfType<SkinSelectionScaleHandler>().Single().CanScaleY.Value);
            AddStep("stretch vertically with editor handle", () =>
            {
                var handler = skinEditor.ChildrenOfType<SkinSelectionScaleHandler>().Single();
                handler.Begin();
                handler.Update(new Vector2(1, 2), adjustAxis: Axes.Y);
                handler.Commit();
                placed.ScrollSpeed.Value = 180;
            });
            AddAssert("editor handle changes height only", () => placed.Height == 450 && placed.Scale == Vector2.One);
            AddAssert("speed included in settings", () => placed.CreateSerialisedInfo().Settings.ContainsKey("scroll_speed"));
            AddStep("save skin", () => skinEditor.Save());
            AddStep("reload layout", () => globalHUDTarget.Reload());
            AddUntilStep("layout reloaded", () => globalHUDTarget.ComponentsLoaded);
            AddAssert("scrolling counter restored", () => globalHUDTarget.Components.OfType<ScrollingKeyCounterDisplay>().Count(), () => Is.EqualTo(1));
            AddUntilStep("restored gameplay keys loaded", () => globalHUDTarget.Components.OfType<ScrollingKeyCounterDisplay>().Single()
                .ChildrenOfType<ScrollingKeyCounter>().Any());
            AddAssert("height and speed survive skin save", () =>
            {
                var restored = globalHUDTarget.Components.OfType<ScrollingKeyCounterDisplay>().Single();
                return restored.Height == 450 && restored.ScrollSpeed.Value == 180;
            });
        }

        [Test]
        public void TestPerComponentFontSaveAndReload()
        {
            ArgonScoreCounter score = null!;
            AddStep("add score counter", () =>
            {
                globalHUDTarget.Add(score = new ArgonScoreCounter());
                skinEditor.SelectedComponents.Clear();
                skinEditor.SelectedComponents.Add(score);
            });
            AddUntilStep("font control present", () => skinEditor.ChildrenOfType<SkinSettingsToolbox>()
                .Any(s => s.Component == score && s.FontControl != null));
            AddStep("choose font in editor", () => skinEditor.ChildrenOfType<SkinSettingsToolbox>()
                .Single(s => s.Component == score).FontControl!.Current.Value = "Inter");
            AddUntilStep("all numeric glyphs use selected font", () => score.ChildrenOfType<osu.Game.Graphics.Sprites.OsuSpriteText>()
                .Any() && score.ChildrenOfType<osu.Game.Graphics.Sprites.OsuSpriteText>().All(t => t.Font.Family == "Inter"));
            AddStep("save font", () => skinEditor.Save());
            AddStep("read separate font file", () =>
            {
                var saved = skins.CurrentSkinInfo.Value.PerformRead(s => skins.GetSkin(s));
                Assert.That(saved.CustomFontInfo.Overrides.Any(o => o.FontFamily == "Inter"), Is.True);
            });
            AddStep("reload layout", () => globalHUDTarget.Reload());
            AddUntilStep("counter restored", () => globalHUDTarget.ComponentsLoaded && globalHUDTarget.Components.OfType<ArgonScoreCounter>()
                .Any(c => SkinComponentFont.For(c).Family.Value == "Inter"));
            AddUntilStep("restored counter renders selected font", () => globalHUDTarget.Components.OfType<ArgonScoreCounter>()
                .First(c => SkinComponentFont.For(c).Family.Value == "Inter").ChildrenOfType<osu.Game.Graphics.Sprites.OsuSpriteText>().All(t => t.Font.Family == "Inter"));
        }

        [Test]
        public void TestPerElementCornerRadiusSaveAndReload()
        {
            BlurredBeatmapBackground panel = null!;
            ArgonWedgePiece wedge = null!;
            TextElement text = null!;
            AddStep("add rounded components", () =>
            {
                globalHUDTarget.Add(panel = new BlurredBeatmapBackground());
                globalHUDTarget.Add(wedge = new ArgonWedgePiece());
                globalHUDTarget.Add(text = new TextElement());
                skinEditor.SelectedComponents.Clear();
                skinEditor.SelectedComponents.Add(panel);
            });
            AddUntilStep("corner radius slider present", () => skinEditor.ChildrenOfType<SkinSettingsToolbox>()
                .Any(s => s.Component == panel && s.CornerRadiusControl != null));
            AddStep("change radius through editor control", () => skinEditor.ChildrenOfType<SkinSettingsToolbox>()
                .Single(s => s.Component == panel).CornerRadiusControl!.Current.Value = 28);
            AddAssert("panel rounding applied", () => panel.CornerRadius, () => Is.EqualTo(28));
            AddStep("set independent radii", () =>
            {
                SkinComponentGeometry.For(wedge).Radius.Value = 0;
                SkinComponentGeometry.For(text).Radius.Value = 12;
            });
            AddAssert("wedge now square", () => wedge.CornerRadius, () => Is.Zero);
            AddAssert("text layout is not given an unrelated outer mask", () => !text.Masking);
            AddStep("undo radius with snapshot", () =>
            {
                using var original = new BlurredBeatmapBackground();
                panel.ApplySerialisedInfo(original.CreateSerialisedInfo());
            });
            AddAssert("original rounding restored", () => panel.CornerRadius, () => Is.EqualTo(8));
            AddStep("set radius again and save", () =>
            {
                SkinComponentGeometry.For(panel).Radius.Value = 28;
                skinEditor.Save();
            });
            AddAssert("fork geometry stored separately", () => skins.CurrentSkin.Value.MosuSettings.ComponentGeometry.Any(o => o.Radius == 28));
            AddStep("read saved skin files from storage", () =>
            {
                var savedSkin = skins.CurrentSkinInfo.Value.PerformRead(s => skins.GetSkin(s));
                Assert.That(savedSkin.MosuSettings.ComponentGeometry.Any(o => o.Radius == 28), Is.True);
                Assert.That(savedSkin.MosuSettings.ComponentGeometry.Any(o => o.Radius == 0), Is.True);
            });
            AddStep("reload saved geometry", () => globalHUDTarget.Reload());
            AddUntilStep("rounded panel loaded", () => globalHUDTarget.ComponentsLoaded && globalHUDTarget.Components.OfType<BlurredBeatmapBackground>().Any());
            AddAssert("panel radius restored", () => globalHUDTarget.Components.OfType<BlurredBeatmapBackground>().Single().CornerRadius, () => Is.EqualTo(28));
            AddAssert("square wedge restored", () => globalHUDTarget.Components.OfType<ArgonWedgePiece>().Last().CornerRadius, () => Is.Zero);
        }

        [Test]
        public void TestMorasoomaComponentsSection()
        {
            AddUntilStep("native components loaded", () => skinEditor.ChildrenOfType<SkinComponentToolbox>()
                .Any(t => t.MorasoomaOnly && t.ChildrenOfType<BlurredBeatmapBackground>().Any()));
            AddAssert("native section is first toolbox", () => skinEditor.ChildrenOfType<SkinComponentToolbox>().First().MorasoomaOnly);
            AddAssert("native section follows working layer", () =>
            {
                var firstToolbox = skinEditor.ChildrenOfType<SkinComponentToolbox>().First();
                return ((FillFlowContainer<EditorSidebarSection>)firstToolbox.Parent!).Children.First() is EditorSidebarSection and not SkinComponentToolbox;
            });
            AddAssert("no native components in standard toolbox", () => skinEditor.ChildrenOfType<SkinComponentToolbox>()
                .Where(t => !t.MorasoomaOnly).All(t => !t.ChildrenOfType<BlurredBeatmapBackground>().Any()
                    && !t.ChildrenOfType<ScrollingKeyCounterDisplay>().Any()));
            AddAssert("only native components in native toolbox", () => skinEditor.ChildrenOfType<SkinComponentToolbox>()
                .Where(t => t.MorasoomaOnly).All(t => !t.ChildrenOfType<ArgonAccuracyCounter>().Any()));
        }

        [Test]
        public void TestBlurredBeatmapBackgroundPlacementAndReload()
        {
            BlurredBeatmapBackground placed = null!;

            AddAssert("background not in default layout", () => !globalHUDTarget.Components.OfType<BlurredBeatmapBackground>().Any());
            AddUntilStep("blurred background available in toolbox", () => skinEditor.ChildrenOfType<SkinComponentToolbox.ToolboxComponentButton>()
                .Any(b => b.ChildrenOfType<BlurredBeatmapBackground>().Any()));
            AddStep("choose blurred background", () =>
            {
                var button = skinEditor.ChildrenOfType<SkinComponentToolbox.ToolboxComponentButton>()
                                       .Single(b => b.ChildrenOfType<BlurredBeatmapBackground>().Any());
                button.RequestPlacement?.Invoke(typeof(BlurredBeatmapBackground));
            });
            AddUntilStep("background selected", () => skinEditor.SelectedComponents.OfType<BlurredBeatmapBackground>().Any());
            AddStep("resize background and send behind HUD", () =>
            {
                placed = skinEditor.SelectedComponents.OfType<BlurredBeatmapBackground>().Single();
                placed.Size = new Vector2(520, 130);
                skinEditor.SendSelectionToBack();
            });
            AddAssert("background behind other elements", () => globalHUDTarget.Components.First(), () => Is.SameAs(placed));
            AddAssert("no new settings", () => placed.CreateSerialisedInfo().Settings, () => Is.Empty);
            AddUntilStep("samples actual player background", () => placed.HasBackground);
            AddStep("save skin with background", () => skinEditor.Save());
            AddStep("reload saved layout", () => globalHUDTarget.Reload());
            AddUntilStep("saved background loaded", () => globalHUDTarget.ComponentsLoaded && globalHUDTarget.Components.OfType<BlurredBeatmapBackground>().Any());
            AddAssert("background size restored", () => globalHUDTarget.Components.OfType<BlurredBeatmapBackground>().Single().Size, () => Is.EqualTo(new Vector2(520, 130)));
            AddAssert("background layer order restored", () => globalHUDTarget.Components.First(), () => Is.TypeOf<BlurredBeatmapBackground>());
        }

        [Test]
        public void TestToggleEditor()
        {
            AddToggleStep("toggle editor visibility", _ => skinEditor.ToggleVisibility());
        }

        [Test]
        public void TestEditComponent()
        {
            ArgonAccuracyCounter accuracyCounter = null!;

            AddStep("select argon accuracy counter blueprint", () =>
            {
                var blueprint = skinEditor.ChildrenOfType<SkinBlueprint>().First(b => b.Item is ArgonAccuracyCounter);

                accuracyCounter = (ArgonAccuracyCounter)blueprint.Item;
                skinEditor.SelectedComponents.Clear();
                skinEditor.SelectedComponents.Add(blueprint.Item);
            });

            AddStep("move by keyboard", () => InputManager.Key(Key.Right));

            AddAssert("accuracyCounter moved", () => accuracyCounter.X != -20);

            AddAssert("value is default", () => accuracyCounter.WireframeOpacity.IsDefault);

            AddStep("hover first slider", () =>
            {
                InputManager.MoveMouseTo(
                    skinEditor.ChildrenOfType<SkinSettingsToolbox>().First()
                              .ChildrenOfType<SettingsSlider<float>>().First()
                              .ChildrenOfType<SliderBar<float>>().First()
                );
            });

            AddStep("adjust slider via keyboard", () => InputManager.Key(Key.Left));

            AddAssert("value is less than default", () => accuracyCounter.WireframeOpacity.Value < accuracyCounter.WireframeOpacity.Default);
        }

        [Test]
        public void TestCopyPaste()
        {
            AddStep("paste", () =>
            {
                InputManager.PressKey(Key.LControl);
                InputManager.Key(Key.V);
                InputManager.ReleaseKey(Key.LControl);
            });
            // no assertions. just make sure nothing crashes.

            AddStep("select argon accuracy counter blueprint", () =>
            {
                var blueprint = skinEditor.ChildrenOfType<SkinBlueprint>().First(b => b.Item is ArgonAccuracyCounter);
                skinEditor.SelectedComponents.Clear();
                skinEditor.SelectedComponents.Add(blueprint.Item);
            });
            AddStep("copy", () =>
            {
                InputManager.PressKey(Key.LControl);
                InputManager.Key(Key.C);
                InputManager.ReleaseKey(Key.LControl);
            });
            AddStep("paste", () =>
            {
                InputManager.PressKey(Key.LControl);
                InputManager.Key(Key.V);
                InputManager.ReleaseKey(Key.LControl);
            });
            AddAssert("two argon accuracy counters present",
                () => skinEditor.ChildrenOfType<SkinBlueprint>().Count(b => b.Item is ArgonAccuracyCounter),
                () => Is.EqualTo(2));
        }

        [Test]
        public void TestCopyPasteIdempotency()
        {
            string state = null!;
            AddStep("select everything", () => InputManager.Keys(PlatformAction.SelectAll));
            AddStep("dump state", () =>
            {
                state = JsonConvert.SerializeObject(skinEditor.SelectedComponents.Cast<Drawable>().Select(s => s.CreateSerialisedInfo()).ToArray());
            });
            AddStep("copy", () => InputManager.Keys(PlatformAction.Copy));
            AddStep("delete", () => InputManager.Keys(PlatformAction.Delete));
            AddStep("paste", () => InputManager.Keys(PlatformAction.Paste));
            AddAssert("pasted state equals dumped",
                () => JsonConvert.SerializeObject(skinEditor.SelectedComponents.Cast<Drawable>().Select(s => s.CreateSerialisedInfo()).ToArray()),
                () => Is.EqualTo(state));
        }

        private SkinnableContainer globalHUDTarget => Player.ChildrenOfType<SkinnableContainer>()
                                                            .Single(c => c.Lookup.Lookup == GlobalSkinnableContainers.MainHUDComponents && c.Lookup.Ruleset == null);

        private SkinnableContainer rulesetHUDTarget => Player.ChildrenOfType<SkinnableContainer>()
                                                             .Single(c => c.Lookup.Lookup == GlobalSkinnableContainers.MainHUDComponents && c.Lookup.Ruleset != null);

        [Test]
        public void TestMigrationArgon()
        {
            Live<SkinInfo> importedSkin = null!;

            AddStep("import old argon skin", () => skins.CurrentSkinInfo.Value = importedSkin = importSkinFromArchives(@"argon-layout-version-0.osk").SkinInfo);
            AddUntilStep("wait for load", () => globalHUDTarget.ComponentsLoaded && rulesetHUDTarget.ComponentsLoaded);
            AddAssert("no combo in global target", () => !globalHUDTarget.Components.OfType<ArgonComboCounter>().Any());
            AddAssert("combo placed in ruleset target", () => rulesetHUDTarget.Components.OfType<ArgonComboCounter>().Count() == 1);

            AddStep("add combo to global target", () => globalHUDTarget.Add(new ArgonComboCounter
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Scale = new Vector2(2f),
            }));
            AddStep("save skin", () => skins.Save(skins.CurrentSkin.Value));

            AddStep("select another skin", () => skins.CurrentSkinInfo.SetDefault());
            AddStep("select skin again", () => skins.CurrentSkinInfo.Value = importedSkin);
            AddUntilStep("wait for load", () => globalHUDTarget.ComponentsLoaded && rulesetHUDTarget.ComponentsLoaded);
            AddAssert("combo placed in global target", () => globalHUDTarget.Components.OfType<ArgonComboCounter>().Count() == 1);
            AddAssert("combo placed in ruleset target", () => rulesetHUDTarget.Components.OfType<ArgonComboCounter>().Count() == 1);
        }

        [Test]
        public void TestMigrationTriangles()
        {
            Live<SkinInfo> importedSkin = null!;

            AddStep("import old triangles skin", () => skins.CurrentSkinInfo.Value = importedSkin = importSkinFromArchives(@"triangles-layout-version-0.osk").SkinInfo);
            AddUntilStep("wait for load", () => globalHUDTarget.ComponentsLoaded && rulesetHUDTarget.ComponentsLoaded);
            AddAssert("no combo in global target", () => !globalHUDTarget.Components.OfType<DefaultComboCounter>().Any());
            AddAssert("combo placed in ruleset target", () => rulesetHUDTarget.Components.OfType<DefaultComboCounter>().Count() == 1);

            AddStep("add combo to global target", () => globalHUDTarget.Add(new DefaultComboCounter
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Scale = new Vector2(2f),
            }));
            AddStep("save skin", () => skins.Save(skins.CurrentSkin.Value));

            AddStep("select another skin", () => skins.CurrentSkinInfo.SetDefault());
            AddStep("select skin again", () => skins.CurrentSkinInfo.Value = importedSkin);
            AddUntilStep("wait for load", () => globalHUDTarget.ComponentsLoaded && rulesetHUDTarget.ComponentsLoaded);
            AddAssert("combo placed in global target", () => globalHUDTarget.Components.OfType<DefaultComboCounter>().Count() == 1);
            AddAssert("combo placed in ruleset target", () => rulesetHUDTarget.Components.OfType<DefaultComboCounter>().Count() == 1);
        }

        [Test]
        public void TestMigrationLegacy()
        {
            Live<SkinInfo> importedSkin = null!;

            AddStep("import old classic skin", () => skins.CurrentSkinInfo.Value = importedSkin = importSkinFromArchives(@"classic-layout-version-0.osk").SkinInfo);
            AddUntilStep("wait for load", () => globalHUDTarget.ComponentsLoaded && rulesetHUDTarget.ComponentsLoaded);
            AddAssert("no combo in global target", () => !globalHUDTarget.Components.OfType<LegacyDefaultComboCounter>().Any());
            AddAssert("combo placed in ruleset target", () => rulesetHUDTarget.Components.OfType<LegacyDefaultComboCounter>().Count() == 1);

            AddStep("add combo to global target", () => globalHUDTarget.Add(new LegacyDefaultComboCounter
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Scale = new Vector2(2f),
            }));
            AddStep("save skin", () => skins.Save(skins.CurrentSkin.Value));

            AddStep("select another skin", () => skins.CurrentSkinInfo.SetDefault());
            AddStep("select skin again", () => skins.CurrentSkinInfo.Value = importedSkin);
            AddUntilStep("wait for load", () => globalHUDTarget.ComponentsLoaded && rulesetHUDTarget.ComponentsLoaded);
            AddAssert("combo placed in global target", () => globalHUDTarget.Components.OfType<LegacyDefaultComboCounter>().Count() == 1);
            AddAssert("combo placed in ruleset target", () => rulesetHUDTarget.Components.OfType<LegacyDefaultComboCounter>().Count() == 1);
        }

        [Test]
        public void TestAnchorRadioButtonBehavior()
        {
            ISerialisableDrawable? selectedComponent = null;

            AddStep("Select first component", () =>
            {
                var blueprint = skinEditor.ChildrenOfType<SkinBlueprint>().First();
                skinEditor.SelectedComponents.Clear();
                skinEditor.SelectedComponents.Add(blueprint.Item);
                selectedComponent = blueprint.Item;
            });

            AddStep("Right-click to open context menu", () =>
            {
                if (selectedComponent != null)
                    InputManager.MoveMouseTo(((Drawable)selectedComponent).ScreenSpaceDrawQuad.Centre);
                InputManager.Click(MouseButton.Right);
            });

            AddStep("Click on Anchor menu", () =>
            {
                InputManager.MoveMouseTo(getMenuItemByText("Anchor"));
                InputManager.Click(MouseButton.Left);
            });

            AddStep("Click TopLeft anchor", () =>
            {
                InputManager.MoveMouseTo(getMenuItemByText("TopLeft"));
                InputManager.Click(MouseButton.Left);
            });

            AddAssert("TopLeft item checked", () => (getMenuItemByText("TopLeft").Item as TernaryStateRadioMenuItem)?.State.Value == TernaryState.True);

            AddStep("Click Centre anchor", () =>
            {
                InputManager.MoveMouseTo(getMenuItemByText("Centre"));
                InputManager.Click(MouseButton.Left);
            });

            AddAssert("Centre item checked", () => (getMenuItemByText("Centre").Item as TernaryStateRadioMenuItem)?.State.Value == TernaryState.True);
            AddAssert("TopLeft item unchecked", () => (getMenuItemByText("TopLeft").Item as TernaryStateRadioMenuItem)?.State.Value == TernaryState.False);

            AddStep("Click Closest anchor", () =>
            {
                InputManager.MoveMouseTo(getMenuItemByText("Closest"));
                InputManager.Click(MouseButton.Left);
            });

            AddAssert("Closest item checked", () => (getMenuItemByText("Closest").Item as TernaryStateRadioMenuItem)?.State.Value == TernaryState.True);
            AddAssert("Centre item unchecked", () => (getMenuItemByText("Centre").Item as TernaryStateRadioMenuItem)?.State.Value == TernaryState.False);

            Menu.DrawableMenuItem getMenuItemByText(string text)
                => this.ChildrenOfType<Menu.DrawableMenuItem>().First(m => m.Item.Text.ToString() == text);
        }

        private Skin importSkinFromArchives(string filename)
        {
            var imported = skins.Import(new ImportTask(TestResources.OpenResource($@"Archives/{filename}"), filename)).GetResultSafely();
            return imported.PerformRead(skinInfo => skins.GetSkin(skinInfo));
        }

        protected override Ruleset CreatePlayerRuleset() => new OsuRuleset();

        private partial class TestSkinEditorChangeHandler : SkinEditorChangeHandler
        {
            public TestSkinEditorChangeHandler(Drawable targetScreen)
                : base(targetScreen)
            {
            }

            public byte[] GetCurrentState()
            {
                using var stream = new MemoryStream();

                WriteCurrentStateToStream(stream);
                byte[] newState = stream.ToArray();

                return newState;
            }
        }
    }
}
