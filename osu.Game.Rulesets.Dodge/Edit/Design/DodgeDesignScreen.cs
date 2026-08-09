// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Framework.Input.Events;
using osu.Framework.Testing;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Dodge.Beatmaps;
using osu.Game.Rulesets.Dodge.Localisation;
using osu.Game.Rulesets.Dodge.Skinning;
using osu.Game.Rulesets.Dodge.UI;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Compose.Components.Timeline;
using osu.Game.Screens.Edit.Setup;
using osu.Game.Storyboards;
using osu.Game.Storyboards.Commands;
using osu.Game.Storyboards.Drawables;
using osu.Game.Utils;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Rulesets.Dodge.Edit.Design
{
    /// <summary>
    /// Ruleset-owned Design implementation. Storyboard coordinates deliberately
    /// remain independent from the 512x384 Dodge arena.
    /// </summary>
    public partial class DodgeDesignScreen : EditorScreenWithTimeline
    {
        private static readonly string[] editable_layers = { "Background", "Fail", "Pass", "Foreground", "Overlay" };

        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        [Resolved]
        private IBindable<WorkingBeatmap> currentWorkingBeatmap { get; set; } = null!;

        [Resolved]
        private IEditorChangeHandler? editorChangeHandler { get; set; }

        [Resolved]
        private OverlayColourProvider editorColours { get; set; } = null!;

        [Resolved]
        private OsuColour osuColours { get; set; } = null!;

        private DodgeStoryboardCanvas canvas = null!;
        private FillFlowContainer objectList = null!;
        private FillFlowContainer selectedObjectInspector = null!;
        private DodgeStoryboardTimeline timeline = null!;
        private FillFlowContainer skinStatus = null!;
        private OsuDropdown<string> resourceDropdown = null!;
        private OsuDropdown<string> layerDropdown = null!;
        private OsuDropdown<DodgeSkinComponents> skinComponentDropdown = null!;
        private FormFileSelector resourceImporter = null!;
        private FormFileSelector skinImporter = null!;
        private OsuCheckbox widescreenCheckbox = null!;
        private OsuCheckbox forceStoryboardCheckbox = null!;
        private OsuCheckbox forceSkinCheckbox = null!;
        private OsuTextFlowContainer insertionSummary = null!;
        private OsuTextBox insertionDurationTextBox = null!;
        private OsuSpriteText? selectedVisibilityStatus;
        private OsuSpriteText? selectedPlayheadHeading;
        private OsuTextFlowContainer? selectedResourceStatus;
        private readonly List<(OsuTextBox TextBox, Func<float> Value)> liveInspectorFields = new List<(OsuTextBox, Func<float>)>();
        private readonly HashSet<string> collapsedLayers = new HashSet<string>(StringComparer.Ordinal);
        private string[] imageResources = Array.Empty<string>();
        private StoryboardSprite? selectedSprite;
        private StoryboardLayer? selectedLayer;
        private StoryboardSelectionLocator? selectionLocator;
        private int? selectedCommandIndex;
        private Easing selectedEasing;
        private double insertionDuration = DodgeStoryboardEditing.DEFAULT_VISIBLE_DURATION;
        private long lastDisplayedPlayhead = long.MinValue;
        private bool animationSectionExpanded;
        private bool synchronisingVisualOptions;
        private StoryboardEditMode editMode;
        private RoundedButton objectModeButton = null!;
        private RoundedButton animationModeButton = null!;
        private OsuTextFlowContainer editModeHint = null!;

        public DodgeDesignScreen()
            : base(EditorScreenMode.Design)
        {
        }

        [BackgroundDependencyLoader]
        private void loadResources()
        {
            imageResources = EditorBeatmap.BeatmapInfo.BeatmapSet?.Files
                                                  .Select(file => file.Filename)
                                                  .Where(isSupportedImage)
                                                  .Distinct(StringComparer.OrdinalIgnoreCase)
                                                  .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                                                  .ToArray()
                             ?? Array.Empty<string>();

            resourceDropdown = new OsuDropdown<string>
            {
                RelativeSizeAxes = Axes.X,
                Items = imageResources.Length == 0 && !string.IsNullOrWhiteSpace(EditorBeatmap.BeatmapInfo.Metadata.BackgroundFile)
                    ? new[] { EditorBeatmap.BeatmapInfo.Metadata.BackgroundFile }
                    : imageResources,
            };

            if (resourceDropdown.Items.Any())
                resourceDropdown.Current.Value = resourceDropdown.Items.First();

            layerDropdown = new OsuDropdown<string>
            {
                RelativeSizeAxes = Axes.X,
                Items = editable_layers,
            };
            layerDropdown.Current.Value = "Foreground";
        }

        protected override Drawable CreateMainContent()
        {
            var content = new GridContainer
            {
                RelativeSizeAxes = Axes.Both,
                ColumnDimensions = new[]
                {
                    new Dimension(GridSizeMode.Absolute, 250),
                    new Dimension(),
                    new Dimension(GridSizeMode.Absolute, 290),
                },
                Content = new[]
                {
                    new Drawable[]
                    {
                        createLeftPanel(),
                        createCanvasPanel(),
                        createInspectorPanel(),
                    },
                },
            };

            resourceDropdown.Current.BindValueChanged(_ => refreshInsertionSummary());
            layerDropdown.Current.BindValueChanged(_ => refreshInsertionSummary());
            updateEditModePresentation();
            return content;
        }

        protected override Drawable CreateTimelineContent()
        {
            timeline = new DodgeStoryboardTimeline(EditorBeatmap, editorClock)
            {
                RelativeSizeAxes = Axes.Both,
                ObjectRangeChanged = retimeSelected,
                CommandRangeChanged = retimeSelectedCommand,
                CommandSelected = rememberSelectedCommand,
                SpriteSelected = (layer, sprite) => select(layer, sprite),
                SpritesDeleteRequested = deleteStoryboardObjects,
            };
            return timeline;
        }

        protected override void ConfigureTimeline(TimelineArea timelineArea)
        {
            base.ConfigureTimeline(timelineArea);
            timeline.SetSelection(selectedSprite);
            refreshAll();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (editorChangeHandler != null)
                editorChangeHandler.OnStateChange += onEditorStateChanged;
        }

        protected override void Dispose(bool isDisposing)
        {
            if (editorChangeHandler != null)
                editorChangeHandler.OnStateChange -= onEditorStateChanged;

            base.Dispose(isDisposing);
        }

        private Drawable createLeftPanel()
        {
            resourceImporter = new FormFileSelector(SupportedExtensions.IMAGE_EXTENSIONS)
            {
                RelativeSizeAxes = Axes.X,
                Caption = DodgeEditorStrings.ImportStoryboardImage,
                PlaceholderText = DodgeEditorStrings.ChooseOrDropImage,
            };
            resourceImporter.Current.BindValueChanged(file =>
            {
                if (file.NewValue != null)
                    importStoryboardResource(file.NewValue);
            });

            skinComponentDropdown = new OsuDropdown<DodgeSkinComponents>
            {
                RelativeSizeAxes = Axes.X,
                Items = Enum.GetValues<DodgeSkinComponents>(),
            };
            skinComponentDropdown.Current.Value = DodgeSkinComponents.Player;

            skinImporter = new FormFileSelector(SupportedExtensions.IMAGE_EXTENSIONS)
            {
                RelativeSizeAxes = Axes.X,
                Caption = DodgeEditorStrings.ReplaceMapSkinComponent,
                PlaceholderText = DodgeEditorStrings.ChooseOrDropImage,
            };
            skinImporter.Current.BindValueChanged(file =>
            {
                if (file.NewValue != null)
                    importSkinResource(file.NewValue);
            });

            FillFlowContainer creationForm = null!;
            RoundedButton creationToggle = null!;
            bool creationExpanded = false;

            var storyboardTools = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(5),
                Children = new Drawable[]
                {
                    creationToggle = button("＋  Добавить объект…", () =>
                    {
                        creationExpanded = !creationExpanded;
                        creationToggle.Text = creationExpanded ? "−  Скрыть добавление" : "＋  Добавить объект…";
                        if (creationExpanded)
                            creationForm.Show();
                        else
                            creationForm.Hide();
                    }, 34),
                    creationForm = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(5),
                        Alpha = 0,
                        Children = new Drawable[]
                        {
                            label(DodgeEditorStrings.StoryboardCreateStepResource),
                            resourceImporter,
                            resourceDropdown,
                            label(DodgeEditorStrings.StoryboardCreateStepLayer),
                            layerDropdown,
                            label(DodgeEditorStrings.StoryboardCreateStepTiming),
                            createInsertionDurationField(),
                            insertionSummary = paragraph(string.Empty, editorColours.Highlight1),
                            paragraph(DodgeEditorStrings.StoryboardInsertionHint, editorColours.Content2),
                            button(DodgeEditorStrings.AddSprite, addSprite),
                            button(DodgeEditorStrings.AddAnimation, addAnimation),
                        },
                    },
                    heading(DodgeEditorStrings.LayersAndObjects),
                    objectList = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(3),
                    },
                },
            };

            var skinTools = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(5),
                Alpha = 0,
                Children = new Drawable[]
                {
                    heading(DodgeEditorStrings.MapSkin),
                    new OsuSpriteText
                    {
                        RelativeSizeAxes = Axes.X,
                        Text = DodgeEditorStrings.MapSkinHint,
                        Font = OsuFont.GetFont(size: 13),
                        Colour = editorColours.Content2,
                    },
                    skinComponentDropdown,
                    skinImporter,
                    skinStatus = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(3),
                    },
                },
            };

            RoundedButton storyboardTab = null!;
            RoundedButton skinTab = null!;
            var tabs = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(5),
                Children = new Drawable[]
                {
                    storyboardTab = compactButton(DodgeEditorStrings.StoryboardTab, () =>
                    {
                        storyboardTools.Show();
                        skinTools.Hide();
                        storyboardTab.BackgroundColour = editorColours.Highlight1;
                        skinTab.BackgroundColour = editorColours.Background3;
                    }),
                    skinTab = compactButton(DodgeEditorStrings.MapSkin, () =>
                    {
                        storyboardTools.Hide();
                        skinTools.Show();
                        storyboardTab.BackgroundColour = editorColours.Background3;
                        skinTab.BackgroundColour = editorColours.Highlight1;
                        refreshSkinStatus();
                    }),
                },
            };
            storyboardTab.BackgroundColour = editorColours.Highlight1;
            skinTab.BackgroundColour = editorColours.Background3;

            return panel(new OsuScrollContainer
            {
                RelativeSizeAxes = Axes.Both,
                Child = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(7),
                    Padding = new MarginPadding(10),
                    Children = new Drawable[] { tabs, storyboardTools, skinTools },
                },
            });
        }

        private Drawable createCanvasPanel()
        {
            RoundedButton gridButton = null!;
            RoundedButton gameplayButton = null!;

            var result = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Padding = new MarginPadding(6),
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = editorColours.Background6,
                    },
                    canvas = new DodgeStoryboardCanvas(EditorBeatmap, editorClock)
                    {
                        RelativeSizeAxes = Axes.Both,
                        SpriteSelected = (layer, sprite) => select(layer, sprite),
                        VisualEditRequested = applyVisualEdit,
                        VisualMetricsChanged = refreshInspector,
                    },
                    new FillFlowContainer
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(5),
                        Margin = new MarginPadding(10),
                        Children = new Drawable[]
                        {
                            compactButton("Весь холст", () => canvas.ShowAll()),
                            compactButton("100%", () => canvas.SetZoom(1)),
                            compactButton("Арена", () => canvas.ShowArena()),
                            gridButton = compactButton("Сетка: вкл", () =>
                            {
                                canvas.ToggleGrid();
                                gridButton.Text = canvas.GridVisible ? "Сетка: вкл" : "Сетка: выкл";
                                gridButton.BackgroundColour = canvas.GridVisible ? editorColours.Highlight1 : editorColours.Background3;
                            }),
                            gameplayButton = compactButton("Игра: выкл", () =>
                            {
                                canvas.ToggleGameplayPreview();
                                gameplayButton.Text = canvas.GameplayVisible ? "Игра: вкл" : "Игра: выкл";
                                gameplayButton.BackgroundColour = canvas.GameplayVisible ? editorColours.Highlight1 : editorColours.Background3;
                            }),
                        },
                    },
                    new Container
                    {
                        Anchor = Anchor.BottomCentre,
                        Origin = Anchor.BottomCentre,
                        AutoSizeAxes = Axes.Both,
                        Margin = new MarginPadding(10),
                        Padding = new MarginPadding { Horizontal = 8, Vertical = 4 },
                        Masking = true,
                        CornerRadius = 4,
                        Children = new Drawable[]
                        {
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = editorColours.Background6,
                                Alpha = 0.75f,
                            },
                            new OsuSpriteText
                            {
                                Text = "ЛКМ: выбор/перемещение  •  колесо: масштаб  •  средняя кнопка: панорама  •  Alt: без привязки",
                                Font = OsuFont.GetFont(size: 11),
                                Colour = editorColours.Content1,
                            },
                        },
                    },
                },
            };
            gridButton.BackgroundColour = editorColours.Highlight1;
            gameplayButton.BackgroundColour = editorColours.Background3;
            return result;
        }

        private Drawable createInspectorPanel()
        {
            forceStoryboardCheckbox = new OsuCheckbox
            {
                Name = "Force storyboard toggle",
                LabelText = DodgeEditorStrings.ForceStoryboard,
            };
            forceStoryboardCheckbox.Current.Value = DodgeBeatmapSettings.GetForceStoryboard(EditorBeatmap.Difficulty);
            forceStoryboardCheckbox.Current.BindValueChanged(value =>
            {
                if (!synchronisingVisualOptions)
                    mutate(() => DodgeBeatmapSettings.SetForceStoryboard(EditorBeatmap.Difficulty, value.NewValue));
            });

            forceSkinCheckbox = new OsuCheckbox
            {
                Name = "Force map skin toggle",
                LabelText = DodgeEditorStrings.ForceMapSkin,
            };
            forceSkinCheckbox.Current.Value = DodgeBeatmapSettings.GetForceBeatmapSkin(EditorBeatmap.Difficulty);
            forceSkinCheckbox.Current.BindValueChanged(value =>
            {
                if (!synchronisingVisualOptions)
                    mutate(() => DodgeBeatmapSettings.SetForceBeatmapSkin(EditorBeatmap.Difficulty, value.NewValue));
            });

            widescreenCheckbox = new OsuCheckbox
            {
                Name = "Widescreen storyboard toggle",
                LabelText = "Широкий холст (854 × 480)",
            };
            widescreenCheckbox.Current.Value = EditorBeatmap.WidescreenStoryboard;
            widescreenCheckbox.Current.BindValueChanged(value =>
            {
                if (synchronisingVisualOptions)
                    return;

                mutate(() =>
                {
                    EditorBeatmap.WidescreenStoryboard = value.NewValue;
                    canvas.Refresh();
                });
            });

            return panel(new OsuScrollContainer
            {
                RelativeSizeAxes = Axes.Both,
                Child = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(6),
                    Padding = new MarginPadding(10),
                    Children = new Drawable[]
                    {
                        heading(DodgeEditorStrings.StoryboardDesign),
                        new OsuSpriteText
                        {
                            RelativeSizeAxes = Axes.X,
                            Text = DodgeEditorStrings.StoryboardCanvasHint,
                            Font = OsuFont.GetFont(size: 13),
                            Colour = editorColours.Content2,
                        },
                        widescreenCheckbox,
                        forceStoryboardCheckbox,
                        forceSkinCheckbox,
                        heading(DodgeEditorStrings.StoryboardEditMode),
                        new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(5),
                            Children = new Drawable[]
                            {
                                objectModeButton = modeButton(DodgeEditorStrings.StoryboardObjectMode, StoryboardEditMode.Object),
                                animationModeButton = modeButton(DodgeEditorStrings.StoryboardAnimationMode, StoryboardEditMode.Animation),
                            },
                        },
                        editModeHint = paragraph(string.Empty, editorColours.Content2),
                        heading(DodgeEditorStrings.SelectedObject),
                        selectedObjectInspector = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(6),
                        },
                    },
                },
            });
        }

        private RoundedButton modeButton(LocalisableString text, StoryboardEditMode mode) => new RoundedButton
        {
            Width = 132,
            Height = 32,
            Text = text,
            Action = () => setEditMode(mode),
        };

        private void setEditMode(StoryboardEditMode mode)
        {
            editMode = mode;
            animationSectionExpanded = mode == StoryboardEditMode.Animation;
            updateEditModePresentation();
            refreshInspector();
        }

        private void updateEditModePresentation()
        {
            if (objectModeButton == null || animationModeButton == null || editModeHint == null)
                return;

            objectModeButton.BackgroundColour = editMode == StoryboardEditMode.Object
                ? editorColours.Highlight1
                : editorColours.Background3;
            animationModeButton.BackgroundColour = editMode == StoryboardEditMode.Animation
                ? editorColours.Highlight1
                : editorColours.Background3;
            editModeHint.Text = editMode == StoryboardEditMode.Object
                ? DodgeEditorStrings.StoryboardObjectModeHint
                : DodgeEditorStrings.StoryboardAnimationModeHint;
        }

        protected override void Update()
        {
            base.Update();

            long displayedPlayhead = (long)Math.Floor(editorClock.CurrentTimeAccurate / 50);
            if (displayedPlayhead == lastDisplayedPlayhead)
                return;

            lastDisplayedPlayhead = displayedPlayhead;
            refreshInsertionSummary();
            refreshSelectedVisibilityStatus();
            refreshSelectedPlayheadHeading();
            refreshSelectedResourceStatus();
            refreshLiveInspectorFields();
        }

        private Drawable createInsertionDurationField()
        {
            insertionDurationTextBox = new OsuTextBox
            {
                RelativeSizeAxes = Axes.X,
                Text = insertionDuration.ToString("0", CultureInfo.InvariantCulture),
                CommitOnFocusLost = true,
                PlaceholderText = DodgeEditorStrings.StoryboardVisibleDuration.ToString(),
            };
            insertionDurationTextBox.OnCommit += (sender, isNew) =>
            {
                if (isNew && double.TryParse(sender.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
                    insertionDuration = Math.Max(DodgeStoryboardEditing.MINIMUM_VISIBLE_DURATION, parsed);

                sender.Text = insertionDuration.ToString("0", CultureInfo.InvariantCulture);
                refreshInsertionSummary();
            };

            return new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(2),
                Children = new Drawable[]
                {
                    label(DodgeEditorStrings.StoryboardVisibleDuration),
                    insertionDurationTextBox,
                },
            };
        }

        private void refreshInsertionSummary()
        {
            if (insertionSummary == null || layerDropdown == null)
                return;

            if (string.IsNullOrWhiteSpace(resourceDropdown.Current.Value))
            {
                insertionSummary.Text = DodgeEditorStrings.StoryboardNoResource;
                return;
            }

            double start = Math.Max(0, editorClock.CurrentTimeAccurate);
            insertionSummary.Text = DodgeEditorStrings.StoryboardInsertionSummary(
                layerDropdown.Current.Value ?? "Foreground",
                DodgeStoryboardEditing.FormatTime(start),
                DodgeStoryboardEditing.FormatTime(start + insertionDuration));
        }

        private void refreshSelectedVisibilityStatus()
        {
            if (selectedVisibilityStatus == null || selectedSprite == null)
                return;

            double time = editorClock.CurrentTimeAccurate;
            selectedVisibilityStatus.Text = time >= selectedSprite.StartTime && time <= selectedSprite.EndTimeForDisplay
                ? DodgeEditorStrings.StoryboardObjectVisibleNow
                : DodgeEditorStrings.StoryboardObjectHiddenNow;
            selectedVisibilityStatus.Colour = time >= selectedSprite.StartTime && time <= selectedSprite.EndTimeForDisplay
                ? osuColours.Green1
                : osuColours.Orange1;
        }

        private void importStoryboardResource(FileInfo source)
        {
            if (!source.Exists || !isSupportedImage(source.Name))
                return;

            var set = currentWorkingBeatmap.Value.BeatmapSetInfo;
            string hash;
            using (var stream = source.OpenRead())
                hash = stream.ComputeSHA2Hash();

            string filename;
            var duplicate = set.Files.FirstOrDefault(file => file.File.Hash == hash);
            if (duplicate != null)
            {
                filename = duplicate.Filename;
            }
            else
            {
                filename = Path.GetFileName(source.Name);
                if (set.GetFile(filename) != null)
                    filename = NamingUtils.GetNextBestFilename(set.Files.Select(file => file.Filename), filename);

                using var stream = source.OpenRead();
                beatmaps.AddFile(set, stream, sanitisePath(filename));
            }

            refreshResources(filename);
            resourceImporter.Current.Value = null;
        }

        private void importSkinResource(FileInfo source)
        {
            if (!source.Exists || !isSupportedImage(source.Name))
                return;

            var set = currentWorkingBeatmap.Value.BeatmapSetInfo;
            string textureName = DodgeSkinTransformer.GetTextureName(skinComponentDropdown.Current.Value);
            string targetFilename = $"{textureName}{source.Extension.ToLowerInvariant()}";

            foreach (var oldFile in set.Files.Where(file =>
                         string.Equals(Path.GetFileNameWithoutExtension(file.Filename), textureName, StringComparison.OrdinalIgnoreCase) &&
                         isSupportedImage(file.Filename)).ToArray())
            {
                if (!string.Equals(oldFile.Filename, targetFilename, StringComparison.OrdinalIgnoreCase))
                    beatmaps.DeleteFile(set, oldFile);
            }

            using (var stream = source.OpenRead())
                beatmaps.AddFile(set, stream, targetFilename);

            refreshResources(targetFilename);
            refreshSkinStatus();
            skinImporter.Current.Value = null;
        }

        private void refreshResources(string? select = null)
        {
            imageResources = currentWorkingBeatmap.Value.BeatmapSetInfo.Files
                                                  .Select(file => file.Filename)
                                                  .Where(isSupportedImage)
                                                  .Distinct(StringComparer.OrdinalIgnoreCase)
                                                  .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                                                  .ToArray();
            resourceDropdown.Items = imageResources;

            if (select != null && imageResources.Contains(select, StringComparer.OrdinalIgnoreCase))
                resourceDropdown.Current.Value = select;

            refreshSkinStatus();
            canvas?.Refresh();
        }

        private void refreshSkinStatus()
        {
            if (skinStatus == null)
                return;

            skinStatus.Clear();
            var files = currentWorkingBeatmap.Value.BeatmapSetInfo.Files;

            foreach (DodgeSkinComponents component in Enum.GetValues<DodgeSkinComponents>())
            {
                string textureName = DodgeSkinTransformer.GetTextureName(component);
                string? filename = files.Select(file => file.Filename)
                                        .FirstOrDefault(path =>
                                            string.Equals(Path.GetFileNameWithoutExtension(path), textureName, StringComparison.OrdinalIgnoreCase) &&
                                            isSupportedImage(path));
                skinStatus.Add(label(filename == null
                    ? LocalisableString.Interpolate($"{component}: {DodgeEditorStrings.BuiltInFallback}")
                    : $"{component}: {filename}"));
            }
        }

        private void addSprite()
        {
            string path = resourceDropdown.Current.Value;
            if (string.IsNullOrWhiteSpace(path))
                return;

            var sprite = new StoryboardSprite(StoryboardElementSource.Beatmap, sanitisePath(path), Anchor.Centre, new Vector2(320, 240));
            DodgeStoryboardEditing.InitialiseVisibleRange(sprite, editorClock.CurrentTimeAccurate, insertionDuration);
            addObject(sprite);
        }

        private void addAnimation()
        {
            string path = resourceDropdown.Current.Value;
            if (string.IsNullOrWhiteSpace(path))
                return;

            string animationPath = getAnimationBasePath(sanitisePath(path));
            int frameCount = Enumerable.Range(0, 240)
                                       .TakeWhile(index => imageResources.Contains(getAnimationFramePath(animationPath, index), StringComparer.OrdinalIgnoreCase))
                                       .Count();

            if (frameCount == 0)
                return;

            var animation = new StoryboardAnimation(
                StoryboardElementSource.Beatmap,
                animationPath,
                Anchor.Centre,
                new Vector2(320, 240),
                frameCount,
                frameDelay: 100,
                AnimationLoopType.LoopForever);
            DodgeStoryboardEditing.InitialiseVisibleRange(animation, editorClock.CurrentTimeAccurate, insertionDuration);
            addObject(animation);
        }

        private void addObject(StoryboardSprite sprite)
        {
            mutate(() => EditorBeatmap.Storyboard.GetLayer(layerDropdown.Current.Value).Add(sprite));
            select(EditorBeatmap.Storyboard.GetLayer(layerDropdown.Current.Value), sprite, true);
        }

        private void select(StoryboardLayer layer, StoryboardSprite sprite, bool storyboardChanged = false)
        {
            bool selectionChanged = !ReferenceEquals(selectedSprite, sprite) || !ReferenceEquals(selectedLayer, layer);

            if (selectionChanged)
                selectedCommandIndex = null;

            selectedLayer = layer;
            selectedSprite = sprite;
            updateSelectionLocator(layer, sprite);
            canvas.SelectedSprite = sprite;

            // Do not rebuild the canvas/timeline under an active pointer gesture.
            // The selected overlay or timeline bar would otherwise be disposed on
            // mouse-down and could never receive the following drag events.
            if (!storyboardChanged && !selectionChanged)
                return;

            if (storyboardChanged)
            {
                refreshAll();
                return;
            }

            refreshObjectList();
            refreshInspector();
            timeline?.SetSelection(selectedSprite);
            canvas.RefreshSelection();
        }

        private void refreshAll()
        {
            refreshObjectList();
            refreshInspector();
            timeline?.SetSelection(selectedSprite);
            restoreSelectedCommand();
            canvas?.Refresh();
        }

        private void rememberSelectedCommand(IStoryboardCommand command)
        {
            if (selectedSprite == null)
                return;

            IStoryboardCommand[] commands = selectedSprite.Commands.AllCommands.ToArray();
            int index = Array.IndexOf(commands, command);
            selectedCommandIndex = index >= 0 ? index : null;
        }

        private void restoreSelectedCommand()
        {
            if (timeline == null || selectedSprite == null || selectedCommandIndex is not int index)
                return;

            IStoryboardCommand[] commands = selectedSprite.Commands.AllCommands.ToArray();
            if (index >= 0 && index < commands.Length)
                timeline.RestoreCommandSelection(commands[index]);
            else
                selectedCommandIndex = null;
        }

        private void onEditorStateChanged() => Scheduler.AddOnce(remapSelectionAfterStateChange);

        private void synchroniseVisualOptions()
        {
            if (widescreenCheckbox == null || forceStoryboardCheckbox == null || forceSkinCheckbox == null)
                return;

            synchronisingVisualOptions = true;
            try
            {
                widescreenCheckbox.Current.Value = EditorBeatmap.WidescreenStoryboard;
                forceStoryboardCheckbox.Current.Value = DodgeBeatmapSettings.GetForceStoryboard(EditorBeatmap.Difficulty);
                forceSkinCheckbox.Current.Value = DodgeBeatmapSettings.GetForceBeatmapSkin(EditorBeatmap.Difficulty);
            }
            finally
            {
                synchronisingVisualOptions = false;
            }
        }

        private void remapSelectionAfterStateChange()
        {
            synchroniseVisualOptions();

            if (selectedSprite != null && tryFindReference(selectedSprite, out StoryboardLayer liveLayer, out int liveIndex))
            {
                selectedLayer = liveLayer;
                selectionLocator = StoryboardSelectionLocator.Create(liveLayer, liveIndex, selectedSprite);
                refreshAll();
                return;
            }

            if (selectionLocator is not StoryboardSelectionLocator locator)
            {
                if (selectedSprite != null || selectedLayer != null)
                    clearSelectionAfterStateChange();
                else
                    refreshAll();

                return;
            }

            StoryboardLayer? layer = EditorBeatmap.Storyboard.Layers.FirstOrDefault(
                candidate => string.Equals(candidate.Name, locator.LayerName, StringComparison.Ordinal));

            if (layer == null)
            {
                clearSelectionAfterStateChange();
                return;
            }

            StoryboardSprite? replacement = null;
            int replacementIndex = -1;

            if (locator.RawElementIndex >= 0 &&
                locator.RawElementIndex < layer.Elements.Count &&
                layer.Elements[locator.RawElementIndex] is StoryboardSprite exactIndexCandidate &&
                locator.Matches(exactIndexCandidate))
            {
                replacement = exactIndexCandidate;
                replacementIndex = locator.RawElementIndex;
            }
            else
            {
                int nearestDistance = int.MaxValue;

                for (int i = 0; i < layer.Elements.Count; i++)
                {
                    if (layer.Elements[i] is not StoryboardSprite candidate || !locator.Matches(candidate))
                        continue;

                    int distance = Math.Abs(i - locator.RawElementIndex);
                    if (distance >= nearestDistance)
                        continue;

                    replacement = candidate;
                    replacementIndex = i;
                    nearestDistance = distance;
                }
            }

            if (replacement == null)
            {
                clearSelectionAfterStateChange();
                return;
            }

            selectedLayer = layer;
            selectedSprite = replacement;
            selectionLocator = StoryboardSelectionLocator.Create(layer, replacementIndex, replacement);
            canvas.SelectedSprite = replacement;
            refreshAll();
        }

        private bool tryFindReference(
            StoryboardSprite sprite,
            out StoryboardLayer containingLayer,
            out int rawElementIndex)
        {
            foreach (StoryboardLayer layer in EditorBeatmap.Storyboard.Layers)
            {
                int index = layer.Elements.IndexOf(sprite);
                if (index < 0)
                    continue;

                containingLayer = layer;
                rawElementIndex = index;
                return true;
            }

            containingLayer = null!;
            rawElementIndex = -1;
            return false;
        }

        private void updateSelectionLocator(StoryboardLayer layer, StoryboardSprite sprite)
        {
            int index = layer.Elements.IndexOf(sprite);
            selectionLocator = index >= 0
                ? StoryboardSelectionLocator.Create(layer, index, sprite)
                : null;
        }

        private void clearSelectionAfterStateChange()
        {
            selectedSprite = null;
            selectedLayer = null;
            selectionLocator = null;
            selectedCommandIndex = null;
            canvas.SelectedSprite = null;
            refreshAll();
        }

        private void refreshObjectList()
        {
            if (objectList == null)
                return;

            objectList.Clear();

            foreach (string layerName in editable_layers)
            {
                StoryboardLayer layer = EditorBeatmap.Storyboard.GetLayer(layerName);
                StoryboardSprite[] sprites = layer.Elements.OfType<StoryboardSprite>().ToArray();
                bool collapsed = collapsedLayers.Contains(layerName);
                objectList.Add(button(
                    $"{(collapsed ? "▸" : "▾")}  {localisedLayerName(layerName)} ({sprites.Length})",
                    () =>
                    {
                        if (!collapsedLayers.Add(layerName))
                            collapsedLayers.Remove(layerName);
                        refreshObjectList();
                    },
                    28));

                if (collapsed)
                    continue;

                if (sprites.Length == 0)
                {
                    objectList.Add(new OsuSpriteText
                    {
                        Text = DodgeEditorStrings.EmptyStoryboardLayer,
                        Font = OsuFont.GetFont(size: 12, italics: true),
                        Colour = editorColours.Content2,
                        Margin = new MarginPadding { Left = 8 },
                    });
                    continue;
                }

                for (int i = 0; i < sprites.Length; i++)
                {
                    StoryboardSprite sprite = sprites[i];
                    StoryboardSprite capturedSprite = sprite;
                    StoryboardLayer capturedLayer = layer;
                    RoundedButton objectButton = button(
                        $"{(ReferenceEquals(sprite, selectedSprite) ? "● " : string.Empty)}{i + 1}. {Path.GetFileName(sprite.Path)}  " +
                        $"{DodgeStoryboardEditing.FormatTime(sprite.StartTime)}–{DodgeStoryboardEditing.FormatTime(sprite.EndTimeForDisplay)}",
                        () => select(capturedLayer, capturedSprite),
                        34);
                    if (ReferenceEquals(sprite, selectedSprite))
                        objectButton.BackgroundColour = editorColours.Highlight1;
                    objectList.Add(objectButton);
                }
            }
        }

        private static string localisedLayerName(string layer) => layer switch
        {
            "Background" => "Фон",
            "Fail" => "Провал",
            "Pass" => "Успех",
            "Foreground" => "Передний план",
            "Overlay" => "Поверх всего",
            _ => layer,
        };

        private void refreshInspector()
        {
            if (selectedObjectInspector == null)
                return;

            selectedObjectInspector.Clear();
            selectedVisibilityStatus = null;
            selectedPlayheadHeading = null;
            selectedResourceStatus = null;
            liveInspectorFields.Clear();

            if (selectedSprite == null || selectedLayer == null)
            {
                selectedObjectInspector.Add(paragraph(
                    DodgeEditorStrings.NoStoryboardSelection,
                    editorColours.Content2));
                return;
            }

            selectedObjectInspector.Add(new OsuSpriteText
            {
                RelativeSizeAxes = Axes.X,
                Text = selectedSprite.Path,
                Font = OsuFont.GetFont(size: 15, weight: FontWeight.Bold),
                Colour = editorColours.Highlight1,
            });
            selectedObjectInspector.Add(label(DodgeEditorStrings.StoryboardObjectLayer(selectedLayer.Name)));
            selectedObjectInspector.Add(label(DodgeEditorStrings.StoryboardObjectVisibleRange(
                DodgeStoryboardEditing.FormatTime(selectedSprite.StartTime),
                DodgeStoryboardEditing.FormatTime(selectedSprite.EndTimeForDisplay),
                DodgeStoryboardEditing.FormatDuration(selectedSprite.EndTimeForDisplay - selectedSprite.StartTime))));
            selectedObjectInspector.Add(selectedVisibilityStatus = new OsuSpriteText
            {
                RelativeSizeAxes = Axes.X,
                Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold),
            });
            refreshSelectedVisibilityStatus();

            StoryboardSprite inspectedSprite = selectedSprite;
            double playhead = Math.Max(0, editorClock.CurrentTimeAccurate);
            DodgeStoryboardEditing.StoryboardVisualState state = DodgeStoryboardEditing.StateAt(inspectedSprite, playhead);
            DodgeStoryboardEditing.StoryboardVisualState liveState()
                => DodgeStoryboardEditing.StateAt(inspectedSprite, Math.Max(0, editorClock.CurrentTimeAccurate));

            selectedObjectInspector.Add(selectedResourceStatus = paragraph(string.Empty, editorColours.Content1));
            refreshSelectedResourceStatus();

            selectedObjectInspector.Add(selectedPlayheadHeading = heading(DodgeEditorStrings.StoryboardAtPlayhead(
                DodgeStoryboardEditing.FormatTime(playhead))));
            selectedObjectInspector.Add(paragraph(
                editMode == StoryboardEditMode.Object
                    ? DodgeEditorStrings.StoryboardObjectModeHint
                    : DodgeEditorStrings.StoryboardAnimationModeHint,
                editorColours.Content2));

            if (DodgeStoryboardEditing.CanRetime(inspectedSprite))
            {
                selectedObjectInspector.Add(visualNumberField(
                    DodgeEditorStrings.StoryboardPositionX,
                    state.Position.X,
                    value => replaceSelectedVisual(
                        inspectedSprite,
                        (sprite, time) =>
                        {
                            Vector2 current = DodgeStoryboardEditing.StateAt(sprite, time).Position;
                            return editPosition(sprite, time, new Vector2(value, current.Y));
                        }),
                    () => liveState().Position.X));
                selectedObjectInspector.Add(visualNumberField(
                    DodgeEditorStrings.StoryboardPositionY,
                    state.Position.Y,
                    value => replaceSelectedVisual(
                        inspectedSprite,
                        (sprite, time) =>
                        {
                            Vector2 current = DodgeStoryboardEditing.StateAt(sprite, time).Position;
                            return editPosition(sprite, time, new Vector2(current.X, value));
                        }),
                    () => liveState().Position.Y));
                selectedObjectInspector.Add(visualNumberField(
                    DodgeEditorStrings.StoryboardScaleX,
                    state.EffectiveScale.X,
                    value => replaceSelectedVisual(
                        inspectedSprite,
                        (sprite, time) =>
                        {
                            Vector2 current = DodgeStoryboardEditing.StateAt(sprite, time).EffectiveScale;
                            return editScale(sprite, time, new Vector2(Math.Max(0.001f, value), current.Y));
                        }),
                    () => liveState().EffectiveScale.X));
                selectedObjectInspector.Add(visualNumberField(
                    DodgeEditorStrings.StoryboardScaleY,
                    state.EffectiveScale.Y,
                    value => replaceSelectedVisual(
                        inspectedSprite,
                        (sprite, time) =>
                        {
                            Vector2 current = DodgeStoryboardEditing.StateAt(sprite, time).EffectiveScale;
                            return editScale(sprite, time, new Vector2(current.X, Math.Max(0.001f, value)));
                        }),
                    () => liveState().EffectiveScale.Y));
                selectedObjectInspector.Add(visualNumberField(
                    DodgeEditorStrings.StoryboardRotation,
                    state.Rotation,
                    value => replaceSelectedVisual(
                        inspectedSprite,
                        (sprite, time) => editRotation(sprite, time, value)),
                    () => liveState().Rotation));
                selectedObjectInspector.Add(visualNumberField(
                    DodgeEditorStrings.StoryboardOpacity,
                    state.Opacity * 100,
                    value => replaceSelectedVisual(
                        inspectedSprite,
                        (sprite, time) => editOpacity(sprite, time, value / 100)),
                    () => liveState().Opacity * 100));

                if (inspectedSprite.Commands.X.Count > 0 || inspectedSprite.Commands.Y.Count > 0)
                {
                    selectedObjectInspector.Add(paragraph(
                        DodgeEditorStrings.StoryboardAnimatedPositionHint,
                        osuColours.Blue1));
                }
            }
            else
            {
                selectedObjectInspector.Add(paragraph(
                    DodgeEditorStrings.StoryboardVisualEditingLocked,
                    osuColours.Orange1));
            }

            var originDropdown = new OsuDropdown<Anchor>
            {
                RelativeSizeAxes = Axes.X,
                Items = Enum.GetValues<Anchor>().Where(anchor => anchor != Anchor.Custom),
            };
            originDropdown.Current.Value = inspectedSprite.Origin;
            originDropdown.Current.BindValueChanged(value =>
            {
                if (!ReferenceEquals(inspectedSprite, selectedSprite) || value.NewValue == inspectedSprite.Origin)
                    return;

                mutate(() => inspectedSprite.Origin = value.NewValue);
                refreshAll();
            });
            selectedObjectInspector.Add(label(DodgeEditorStrings.StoryboardOrigin));
            selectedObjectInspector.Add(originDropdown);

            selectedObjectInspector.Add(heading(DodgeEditorStrings.StoryboardTimingSection));
            selectedObjectInspector.Add(button(
                DodgeEditorStrings.StoryboardGoToStart,
                () => editorClock.Seek(Math.Max(0, selectedSprite.StartTime)),
                30));
            selectedObjectInspector.Add(button(DodgeEditorStrings.StoryboardStartAtPlayhead, setSelectedStartAtPlayhead, 30));
            selectedObjectInspector.Add(button(DodgeEditorStrings.StoryboardEndAtPlayhead, setSelectedEndAtPlayhead, 30));

            if (DodgeStoryboardEditing.CanRetime(inspectedSprite))
            {
                if (inspectedSprite.EndTimeForDisplay - inspectedSprite.StartTime < DodgeStoryboardEditing.MINIMUM_VISIBLE_DURATION)
                {
                    selectedObjectInspector.Add(paragraph(
                        DodgeEditorStrings.StoryboardBrokenDurationHint,
                        osuColours.Orange1));
                    selectedObjectInspector.Add(button(
                        DodgeEditorStrings.StoryboardRepairDuration,
                        () =>
                        {
                            if (ReferenceEquals(inspectedSprite, selectedSprite))
                                replaceSelected(DodgeStoryboardEditing.RepairVisibleRange(inspectedSprite));
                        },
                        32));
                }

                selectedObjectInspector.Add(timeField(
                    DodgeEditorStrings.StoryboardStartTime,
                    inspectedSprite.StartTime,
                    value => retimeSelected(value, inspectedSprite.EndTimeForDisplay)));
                selectedObjectInspector.Add(timeField(
                    DodgeEditorStrings.StoryboardEndTime,
                    inspectedSprite.EndTimeForDisplay,
                    value => retimeSelected(inspectedSprite.StartTime, value)));
            }
            else
            {
                selectedObjectInspector.Add(paragraph(
                    DodgeEditorStrings.StoryboardRangeLocked,
                    osuColours.Orange1));
            }

            if (editMode == StoryboardEditMode.Animation)
            {
                selectedObjectInspector.Add(button(
                    animationSectionExpanded
                        ? DodgeEditorStrings.StoryboardHideAnimation
                        : DodgeEditorStrings.StoryboardShowAnimation,
                    () =>
                    {
                        animationSectionExpanded = !animationSectionExpanded;
                        refreshInspector();
                    },
                    34));

                if (animationSectionExpanded)
                {
                    selectedObjectInspector.Add(paragraph(
                        DodgeEditorStrings.StoryboardAnimationHelp,
                        editorColours.Content2));

                    var easingDropdown = new OsuDropdown<Easing>
                    {
                        RelativeSizeAxes = Axes.X,
                        Items = Enum.GetValues<Easing>(),
                    };
                    easingDropdown.Current.Value = selectedEasing;
                    easingDropdown.Current.BindValueChanged(value => selectedEasing = value.NewValue);
                    selectedObjectInspector.Add(label(DodgeEditorStrings.StoryboardEasing));
                    selectedObjectInspector.Add(easingDropdown);
                    selectedObjectInspector.Add(paragraph(
                        DodgeEditorStrings.StoryboardEasingHelp,
                        editorColours.Content2));

                    selectedObjectInspector.Add(keyButtonRow(
                        compactButton(DodgeEditorStrings.StoryboardPositionKey, () => addKey(StoryboardKeyType.Move)),
                        compactButton(DodgeEditorStrings.StoryboardScaleKey, () => addKey(StoryboardKeyType.VectorScale))));
                    selectedObjectInspector.Add(keyButtonRow(
                        compactButton(DodgeEditorStrings.StoryboardRotationKey, () => addKey(StoryboardKeyType.Rotate)),
                        compactButton(DodgeEditorStrings.StoryboardOpacityKey, () => addKey(StoryboardKeyType.Fade))));
                    selectedObjectInspector.Add(keyButtonRow(
                        compactButton(DodgeEditorStrings.StoryboardColourKey, () => addKey(StoryboardKeyType.Colour)),
                        compactButton(DodgeEditorStrings.StoryboardAdditiveKey, () => addKey(StoryboardKeyType.Additive))));
                    selectedObjectInspector.Add(keyButtonRow(
                        compactButton(DodgeEditorStrings.StoryboardFlipHKey, () => addKey(StoryboardKeyType.FlipH)),
                        compactButton(DodgeEditorStrings.StoryboardFlipVKey, () => addKey(StoryboardKeyType.FlipV))));
                }
            }

            selectedObjectInspector.Add(heading(DodgeEditorStrings.StoryboardObjectActions));
            selectedObjectInspector.Add(button(DodgeEditorStrings.StoryboardDuplicate, duplicateSelected));
            selectedObjectInspector.Add(button(DodgeEditorStrings.StoryboardMoveUp, () => reorderSelected(-1)));
            selectedObjectInspector.Add(button(DodgeEditorStrings.StoryboardMoveDown, () => reorderSelected(1)));
            selectedObjectInspector.Add(button(DodgeEditorStrings.StoryboardDelete, deleteSelected));
        }

        private Drawable visualNumberField(
            LocalisableString name,
            float value,
            Action<float> apply,
            Func<float> liveValue)
        {
            var textBox = new OsuTextBox
            {
                RelativeSizeAxes = Axes.X,
                Text = value.ToString("0.###", CultureInfo.InvariantCulture),
                CommitOnFocusLost = true,
                PlaceholderText = name,
            };
            textBox.OnCommit += (sender, isNew) =>
            {
                if (isNew && float.TryParse(sender.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
                    apply(parsed);
            };
            liveInspectorFields.Add((textBox, liveValue));

            return new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(2),
                Children = new Drawable[] { label(name), textBox },
            };
        }

        private void refreshSelectedResourceStatus()
        {
            if (selectedResourceStatus == null || selectedSprite == null)
                return;

            DodgeStoryboardEditing.StoryboardVisualState state =
                DodgeStoryboardEditing.StateAt(selectedSprite, Math.Max(0, editorClock.CurrentTimeAccurate));

            if (canvas.TryGetTextureSize(selectedSprite, out Vector2 sourceSize))
            {
                Vector2 renderedSize = new Vector2(
                    Math.Abs(sourceSize.X * state.EffectiveScale.X),
                    Math.Abs(sourceSize.Y * state.EffectiveScale.Y));
                selectedResourceStatus.Text = DodgeEditorStrings.StoryboardResourceReady(
                    sourceSize.X,
                    sourceSize.Y,
                    renderedSize.X,
                    renderedSize.Y);
                selectedResourceStatus.Colour = osuColours.Green1;
            }
            else
            {
                selectedResourceStatus.Text = canvas.IsPreviewLoaded
                    ? DodgeEditorStrings.StoryboardResourceMissing(selectedSprite.Path)
                    : DodgeEditorStrings.StoryboardResourceLoading(selectedSprite.Path);
                selectedResourceStatus.Colour = canvas.IsPreviewLoaded
                    ? osuColours.Orange1
                    : editorColours.Content2;
            }
        }

        private void refreshSelectedPlayheadHeading()
        {
            if (selectedPlayheadHeading != null)
            {
                selectedPlayheadHeading.Text = DodgeEditorStrings.StoryboardAtPlayhead(
                    DodgeStoryboardEditing.FormatTime(Math.Max(0, editorClock.CurrentTimeAccurate)));
            }
        }

        private void refreshLiveInspectorFields()
        {
            foreach ((OsuTextBox textBox, Func<float> value) in liveInspectorFields)
            {
                if (!textBox.HasFocus)
                    textBox.Text = value().ToString("0.###", CultureInfo.InvariantCulture);
            }
        }

        private StoryboardSprite editPosition(StoryboardSprite sprite, double time, Vector2 position)
            => editMode == StoryboardEditMode.Object
                ? DodgeStoryboardEditing.TranslateWholeObject(
                    sprite,
                    position - DodgeStoryboardEditing.StateAt(sprite, time).Position)
                : DodgeStoryboardEditing.SetPositionAt(sprite, time, position, selectedEasing);

        private StoryboardSprite editScale(StoryboardSprite sprite, double time, Vector2 scale)
            => editMode == StoryboardEditMode.Object
                ? DodgeStoryboardEditing.ScaleWholeObjectAt(sprite, time, scale)
                : DodgeStoryboardEditing.SetEffectiveScaleAt(sprite, time, scale, selectedEasing);

        private StoryboardSprite editRotation(StoryboardSprite sprite, double time, float rotation)
            => editMode == StoryboardEditMode.Object
                ? DodgeStoryboardEditing.RotateWholeObjectAt(sprite, time, rotation)
                : DodgeStoryboardEditing.SetRotationAt(sprite, time, rotation, selectedEasing);

        private StoryboardSprite editOpacity(StoryboardSprite sprite, double time, float opacity)
            => editMode == StoryboardEditMode.Object
                ? DodgeStoryboardEditing.SetWholeObjectOpacityAt(sprite, time, opacity)
                : DodgeStoryboardEditing.SetOpacityAt(sprite, time, opacity, selectedEasing);

        private void replaceSelectedVisual(
            StoryboardSprite inspectedSprite,
            Func<StoryboardSprite, double, StoryboardSprite> edit)
        {
            if (!ReferenceEquals(inspectedSprite, selectedSprite) ||
                selectedLayer == null ||
                !DodgeStoryboardEditing.CanRetime(inspectedSprite))
                return;

            replaceSelected(edit(inspectedSprite, Math.Max(0, editorClock.CurrentTimeAccurate)));
        }

        private Drawable timeField(LocalisableString name, double value, Action<double> apply)
        {
            var textBox = new OsuTextBox
            {
                RelativeSizeAxes = Axes.X,
                Text = value.ToString("0.###", CultureInfo.InvariantCulture),
                CommitOnFocusLost = true,
                PlaceholderText = name,
            };
            textBox.OnCommit += (sender, isNew) =>
            {
                if (isNew && double.TryParse(sender.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
                    apply(parsed);
            };

            return new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(2),
                Children = new Drawable[] { label(name), textBox },
            };
        }

        private void setSelectedStartAtPlayhead()
        {
            if (selectedSprite == null)
                return;

            double start = Math.Max(0, editorClock.CurrentTimeAccurate);
            double duration = Math.Max(
                DodgeStoryboardEditing.MINIMUM_VISIBLE_DURATION,
                selectedSprite.EndTimeForDisplay - selectedSprite.StartTime);
            double end = start < selectedSprite.EndTimeForDisplay - DodgeStoryboardEditing.MINIMUM_VISIBLE_DURATION
                ? selectedSprite.EndTimeForDisplay
                : start + duration;
            retimeSelected(start, end);
        }

        private void setSelectedEndAtPlayhead()
        {
            if (selectedSprite == null)
                return;

            double end = Math.Max(
                selectedSprite.StartTime + DodgeStoryboardEditing.MINIMUM_VISIBLE_DURATION,
                editorClock.CurrentTimeAccurate);
            retimeSelected(selectedSprite.StartTime, end);
        }

        private void retimeSelected(double startTime, double endTime)
        {
            if (selectedSprite == null || selectedLayer == null || !DodgeStoryboardEditing.CanRetime(selectedSprite))
                return;

            replaceSelected(DodgeStoryboardEditing.Retime(selectedSprite, startTime, endTime));
        }

        private void retimeSelectedCommand(IStoryboardCommand command, double startTime, double endTime)
        {
            if (selectedSprite == null || selectedLayer == null || !DodgeStoryboardEditing.CanRetime(selectedSprite))
                return;

            IStoryboardCommand[] oldCommands = selectedSprite.Commands.AllCommands.ToArray();
            int commandIndex = Array.IndexOf(oldCommands, command);
            selectedCommandIndex = commandIndex >= 0 ? commandIndex : null;
            StoryboardSprite replacement = DodgeStoryboardEditing.RetimeCommand(selectedSprite, command, startTime, endTime);
            replaceSelected(replacement);

            IStoryboardCommand[] newCommands = replacement.Commands.AllCommands.ToArray();
            if (commandIndex >= 0 && commandIndex < newCommands.Length)
                timeline.RestoreCommandSelection(newCommands[commandIndex]);
        }

        private void applyVisualEdit(StoryboardSprite sprite, StoryboardVisualEdit edit)
        {
            if (!ReferenceEquals(sprite, selectedSprite) ||
                selectedLayer == null ||
                !DodgeStoryboardEditing.CanRetime(sprite))
                return;

            StoryboardSprite replacement;

            switch (edit.Kind)
            {
                case StoryboardVisualEditKind.Position:
                    replacement = editPosition(
                        sprite,
                        editorClock.CurrentTimeAccurate,
                        edit.Value);
                    break;

                case StoryboardVisualEditKind.Scale:
                    replacement = editScale(
                        sprite,
                        editorClock.CurrentTimeAccurate,
                        edit.Value);

                    if (edit.Position != null)
                    {
                        replacement = editPosition(
                            replacement,
                            editorClock.CurrentTimeAccurate,
                            edit.Position.Value);
                    }

                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(edit));
            }

            replaceSelected(replacement);
        }

        private void replaceSelected(StoryboardSprite replacement)
        {
            if (selectedSprite == null || selectedLayer == null)
                return;

            int index = selectedLayer.Elements.IndexOf(selectedSprite);
            if (index < 0)
                return;

            mutate(() => selectedLayer.Elements[index] = replacement);
            selectedSprite = replacement;
            updateSelectionLocator(selectedLayer, replacement);
            canvas.SelectedSprite = replacement;
            refreshAll();
        }

        private void addKey(StoryboardKeyType type)
        {
            if (selectedSprite == null)
                return;

            double time = editorClock.CurrentTimeAccurate;
            DodgeStoryboardEditing.StoryboardVisualState state = DodgeStoryboardEditing.StateAt(selectedSprite, time);
            mutate(() =>
            {
                switch (type)
                {
                    case StoryboardKeyType.Fade:
                        DodgeStoryboardEditing.AddOpacityKey(selectedSprite, time, state.Opacity, selectedEasing);
                        break;

                    case StoryboardKeyType.Move:
                        DodgeStoryboardEditing.AddPositionKey(selectedSprite, time, state.Position, selectedEasing);
                        break;

                    case StoryboardKeyType.MoveX:
                        selectedSprite.Commands.AddX(selectedEasing, time, time, state.Position.X, state.Position.X);
                        break;

                    case StoryboardKeyType.MoveY:
                        selectedSprite.Commands.AddY(selectedEasing, time, time, state.Position.Y, state.Position.Y);
                        break;

                    case StoryboardKeyType.Scale:
                        selectedSprite.Commands.AddScale(selectedEasing, time, time, state.UniformScale, state.UniformScale);
                        break;

                    case StoryboardKeyType.VectorScale:
                        DodgeStoryboardEditing.AddScaleKey(selectedSprite, time, state.VectorScale, selectedEasing);
                        break;

                    case StoryboardKeyType.Rotate:
                        DodgeStoryboardEditing.AddRotationKey(selectedSprite, time, state.Rotation, selectedEasing);
                        break;

                    case StoryboardKeyType.Colour:
                        selectedSprite.Commands.AddColour(selectedEasing, time, time, Color4.White, Color4.White);
                        break;

                    case StoryboardKeyType.Additive:
                        selectedSprite.Commands.AddBlendingParameters(selectedEasing, time, time, BlendingParameters.Additive, BlendingParameters.Additive);
                        break;

                    case StoryboardKeyType.FlipH:
                        selectedSprite.Commands.AddFlipH(selectedEasing, time, time, true, true);
                        break;

                    case StoryboardKeyType.FlipV:
                        selectedSprite.Commands.AddFlipV(selectedEasing, time, time, true, true);
                        break;
                }
            });
            refreshAll();
        }

        private void duplicateSelected()
        {
            if (selectedSprite == null || selectedLayer == null)
                return;

            StoryboardSprite clone = DodgeStoryboardEditing.Clone(selectedSprite, new Vector2(12));
            mutate(() => selectedLayer.Add(clone));
            select(selectedLayer, clone, true);
        }

        private void reorderSelected(int delta)
        {
            if (selectedSprite == null || selectedLayer == null)
                return;

            int oldIndex = selectedLayer.Elements.IndexOf(selectedSprite);
            int newIndex = Math.Clamp(oldIndex + delta, 0, selectedLayer.Elements.Count - 1);
            if (oldIndex == newIndex)
                return;

            mutate(() =>
            {
                selectedLayer.Elements.RemoveAt(oldIndex);
                selectedLayer.Elements.Insert(newIndex, selectedSprite);
            });
            updateSelectionLocator(selectedLayer, selectedSprite);
            refreshAll();
        }

        private void deleteSelected()
        {
            if (selectedSprite == null || selectedLayer == null)
                return;

            deleteStoryboardObjects(new[] { (selectedLayer, selectedSprite) });
        }

        private void deleteStoryboardObjects(IReadOnlyList<(StoryboardLayer Layer, StoryboardSprite Sprite)> objects)
        {
            (StoryboardLayer Layer, StoryboardSprite Sprite)[] existing = objects
                .Where(item => item.Layer.Elements.Contains(item.Sprite))
                .Distinct()
                .ToArray();

            if (existing.Length == 0)
                return;

            bool deletesSelection = selectedSprite != null &&
                                    existing.Any(item => ReferenceEquals(item.Sprite, selectedSprite));

            mutate(() =>
            {
                foreach ((StoryboardLayer layer, StoryboardSprite sprite) in existing)
                    layer.Elements.Remove(sprite);
            });

            if (deletesSelection)
            {
                selectionLocator = null;
                selectedSprite = null;
                selectedLayer = null;
                selectedCommandIndex = null;
                canvas.SelectedSprite = null;
            }

            refreshAll();
        }

        private void mutate(Action action)
        {
            EditorBeatmap.BeginChange();
            try
            {
                action();
            }
            finally
            {
                EditorBeatmap.EndChange();
                EditorBeatmap.SaveState(); // Force save state to apply changes
            }
        }

        private static bool isSupportedImage(string path)
            => SupportedExtensions.IMAGE_EXTENSIONS.Any(extension => path.EndsWith(extension, StringComparison.OrdinalIgnoreCase));

        private static string sanitisePath(string path)
        {
            path = path.Replace('\\', '/');
            if (path.StartsWith('/') || System.IO.Path.IsPathRooted(path) ||
                path.Split('/').Any(segment => segment is "" or "." or ".."))
                throw new InvalidOperationException("Storyboard resources must use safe relative paths.");
            return path;
        }

        internal static string GetAnimationBasePathForTesting(string path) => getAnimationBasePath(path);

        private static string getAnimationBasePath(string path)
        {
            int extensionStart = path.LastIndexOf('.');
            if (extensionStart <= path.LastIndexOf('/'))
                return path;

            int digitStart = extensionStart;
            while (digitStart > 0 && char.IsDigit(path[digitStart - 1]))
                digitStart--;

            return digitStart == extensionStart ? path : path.Remove(digitStart, extensionStart - digitStart);
        }

        private static string getAnimationFramePath(string path, int frame)
            => path.Replace(".", frame.ToString(CultureInfo.InvariantCulture) + ".", StringComparison.Ordinal);

        private Drawable panel(Drawable child) => new Container
        {
            RelativeSizeAxes = Axes.Both,
            Padding = new MarginPadding(5),
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = editorColours.Background5,
                },
                child,
            },
        };

        private OsuSpriteText heading(LocalisableString text) => new OsuSpriteText
        {
            Text = text,
            Font = OsuFont.GetFont(size: 16, weight: FontWeight.Bold),
            Colour = editorColours.Content1,
            Margin = new MarginPadding { Bottom = 3 },
        };

        private OsuSpriteText label(LocalisableString text) => new OsuSpriteText
        {
            RelativeSizeAxes = Axes.X,
            Text = text,
            Font = OsuFont.GetFont(size: 13),
            Colour = editorColours.Content2,
        };

        private static OsuTextFlowContainer paragraph(LocalisableString text, Color4 colour) => new OsuTextFlowContainer(sprite =>
        {
            sprite.Font = OsuFont.GetFont(size: 12);
            sprite.Colour = colour;
        })
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Text = text,
        };

        private static Drawable keyButtonRow(params Drawable[] buttons) => new FillFlowContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Horizontal,
            Spacing = new Vector2(4),
            Children = buttons,
        };

        private static RoundedButton button(LocalisableString text, Action action, float height = 36) => new RoundedButton
        {
            RelativeSizeAxes = Axes.X,
            Height = height,
            Text = text,
            Action = action,
        };

        private static RoundedButton compactButton(LocalisableString text, Action action) => new RoundedButton
        {
            Width = 92,
            Height = 30,
            Text = text,
            Action = action,
        };

        private readonly record struct StoryboardSelectionLocator(
            string LayerName,
            int RawElementIndex,
            string Path,
            StoryboardElementSource Source,
            Type SpriteType)
        {
            public static StoryboardSelectionLocator Create(
                StoryboardLayer layer,
                int rawElementIndex,
                StoryboardSprite sprite)
                => new StoryboardSelectionLocator(
                    layer.Name,
                    rawElementIndex,
                    sprite.Path,
                    sprite.Source,
                    sprite.GetType());

            public bool Matches(StoryboardSprite candidate)
                => candidate.GetType() == SpriteType &&
                   candidate.Source == Source &&
                   string.Equals(candidate.Path, Path, StringComparison.Ordinal);
        }

        private enum StoryboardKeyType
        {
            Fade,
            Move,
            MoveX,
            MoveY,
            Scale,
            VectorScale,
            Rotate,
            Colour,
            Additive,
            FlipH,
            FlipV,
        }

        private enum StoryboardEditMode
        {
            Object,
            Animation,
        }
    }

    internal enum StoryboardVisualEditKind
    {
        Position,
        Scale,
    }

    internal readonly record struct StoryboardVisualEdit(
        StoryboardVisualEditKind Kind,
        Vector2 Value,
        Vector2? Position = null);

    internal enum StoryboardSelectionPresentation
    {
        None,
        DrawableOverlay,
        OutsideRangeNotice,
        LoadingNotice,
        MissingResourceNotice,
    }

    internal static class StoryboardSelectionState
    {
        public static bool IsActiveAt(StoryboardSprite sprite, double time)
            => time >= sprite.StartTime && time <= sprite.EndTimeForDisplay;

        public static StoryboardSelectionPresentation Resolve(
            StoryboardSprite sprite,
            double time,
            bool selected,
            bool drawableReady,
            bool previewLoaded)
        {
            bool active = IsActiveAt(sprite, time);

            if (active && drawableReady)
                return StoryboardSelectionPresentation.DrawableOverlay;

            if (!selected)
                return StoryboardSelectionPresentation.None;

            if (!active)
                return StoryboardSelectionPresentation.OutsideRangeNotice;

            return previewLoaded
                ? StoryboardSelectionPresentation.MissingResourceNotice
                : StoryboardSelectionPresentation.LoadingNotice;
        }
    }

    internal sealed class StoryboardSelectionActivityTracker
    {
        private HashSet<StoryboardSprite> activeSprites = new HashSet<StoryboardSprite>();
        private HashSet<StoryboardSprite> nextActiveSprites = new HashSet<StoryboardSprite>();

        public bool Update(IEnumerable<StoryboardSprite> sprites, double time)
        {
            nextActiveSprites.Clear();

            foreach (StoryboardSprite sprite in sprites)
            {
                if (StoryboardSelectionState.IsActiveAt(sprite, time))
                    nextActiveSprites.Add(sprite);
            }

            if (activeSprites.SetEquals(nextActiveSprites))
                return false;

            (activeSprites, nextActiveSprites) = (nextActiveSprites, activeSprites);
            return true;
        }

        public void Capture(IEnumerable<StoryboardSprite> sprites, double time)
        {
            activeSprites.Clear();

            foreach (StoryboardSprite sprite in sprites)
            {
                if (StoryboardSelectionState.IsActiveAt(sprite, time))
                    activeSprites.Add(sprite);
            }
        }
    }

    internal partial class DodgeStoryboardCanvas : CompositeDrawable
    {
        public const float NORMAL_WIDTH = 640;
        public const float WIDESCREEN_WIDTH = 480 * 16 / 9f;
        public const float CANVAS_HEIGHT = 480;
        public static readonly Vector2 ARENA_SIZE = new Vector2(512, 384);

        private readonly EditorBeatmap beatmap;
        private readonly EditorClock editorClock;
        private readonly Container camera;
        private readonly Container canvas;
        private readonly Container preview;
        private readonly Container overlayPreview;
        private readonly Container grid;
        private readonly Container selection;
        private readonly Container gameplay;
        private readonly OsuSpriteText canvasModeLabel;
        private readonly Dictionary<StoryboardSprite, DrawableStoryboardSprite> drawableSprites = new Dictionary<StoryboardSprite, DrawableStoryboardSprite>();
        private readonly StoryboardSelectionActivityTracker selectionActivity = new StoryboardSelectionActivityTracker();
        private float zoom = 1;
        private bool showGrid = true;
        private bool showGameplay;
        private int previewRevision;
        private bool previewLoaded;

        public StoryboardSprite? SelectedSprite { get; set; }
        public Action<StoryboardLayer, StoryboardSprite>? SpriteSelected { get; set; }
        public Action<StoryboardSprite, StoryboardVisualEdit>? VisualEditRequested { get; set; }
        public Action? VisualMetricsChanged { get; set; }

        public DodgeStoryboardCanvas(EditorBeatmap beatmap, EditorClock editorClock)
        {
            this.beatmap = beatmap;
            this.editorClock = editorClock;
            Masking = true;

            InternalChild = camera = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(NORMAL_WIDTH, CANVAS_HEIGHT),
                Child = canvas = new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(NORMAL_WIDTH, CANVAS_HEIGHT),
                    Children = new Drawable[]
                    {
                        new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.Black, Depth = 4 },
                        preview = new Container { RelativeSizeAxes = Axes.Both, Depth = 3 },
                        gameplay = new Container { RelativeSizeAxes = Axes.Both, Depth = 2 },
                        overlayPreview = new Container { RelativeSizeAxes = Axes.Both, Depth = 1 },
                        createGameplayHud(),
                        grid = new Container { RelativeSizeAxes = Axes.Both, Depth = 0 },
                        createStoryboardFrame(),
                        createArenaFrame(),
                        createSafeArea(),
                        selection = new Container { RelativeSizeAxes = Axes.Both, Depth = -1 },
                        new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Depth = -2,
                            Children = new Drawable[]
                            {
                                canvasModeLabel = new OsuSpriteText
                                {
                                    Position = new Vector2(8, 7),
                                    Text = DodgeEditorStrings.StoryboardCanvasCoordinates,
                                    Font = OsuFont.GetFont(size: 11, weight: FontWeight.Bold),
                                },
                                new OsuSpriteText
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.TopLeft,
                                    Position = new Vector2(-ARENA_SIZE.X / 2 + 7, -ARENA_SIZE.Y / 2 + 6),
                                    Text = DodgeEditorStrings.StoryboardArenaCoordinates,
                                    Font = OsuFont.GetFont(size: 10, weight: FontWeight.Bold),
                                    Colour = Color4.Cyan,
                                },
                            },
                        },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            ShowAll();
        }

        protected override void Update()
        {
            base.Update();

            if (selectionActivity.Update(storyboardSprites(), editorClock.CurrentTimeAccurate))
                rebuildSelection();
        }

        public void Refresh()
        {
            float width = beatmap.WidescreenStoryboard ? WIDESCREEN_WIDTH : NORMAL_WIDTH;
            camera.Size = canvas.Size = new Vector2(width, CANVAS_HEIGHT);
            canvasModeLabel.Text = beatmap.WidescreenStoryboard
                ? $"Широкий сториборд: {WIDESCREEN_WIDTH:0} × {CANVAS_HEIGHT:0} (координаты osu! остаются по центру)"
                : DodgeEditorStrings.StoryboardCanvasCoordinates;
            rebuildGrid();
            rebuildPreview();
            rebuildGameplay();
            rebuildSelection();
        }

        public void RefreshSelection() => rebuildSelection();

        public bool TryGetTextureSize(StoryboardSprite sprite, out Vector2 size)
        {
            if (drawableSprites.TryGetValue(sprite, out DrawableStoryboardSprite? drawable) &&
                drawable.Texture != null &&
                drawable.Size.X > 0 &&
                drawable.Size.Y > 0)
            {
                size = drawable.Size;
                return true;
            }

            size = Vector2.Zero;
            return false;
        }

        public bool IsPreviewLoaded => previewLoaded;
        public bool GridVisible => showGrid;
        public bool GameplayVisible => showGameplay;

        private IEnumerable<StoryboardSprite> storyboardSprites()
            => beatmap.Storyboard.Layers.SelectMany(layer => layer.Elements.OfType<StoryboardSprite>());

        public void ShowAll()
        {
            float availableWidth = Math.Max(1, DrawWidth - 30);
            float availableHeight = Math.Max(1, DrawHeight - 30);
            SetZoom(Math.Min(availableWidth / canvas.Width, availableHeight / canvas.Height));
            camera.Position = Vector2.Zero;
        }

        public void ShowArena()
        {
            float availableWidth = Math.Max(1, DrawWidth - 50);
            float availableHeight = Math.Max(1, DrawHeight - 50);
            SetZoom(Math.Min(availableWidth / ARENA_SIZE.X, availableHeight / ARENA_SIZE.Y));
            camera.Position = Vector2.Zero;
        }

        public void SetZoom(float value)
        {
            zoom = Math.Clamp(value, 0.1f, 8);
            camera.Scale = new Vector2(zoom);
        }

        public void ToggleGrid()
        {
            showGrid = !showGrid;
            grid.Alpha = showGrid ? 1 : 0;
        }

        public void ToggleGameplayPreview()
        {
            showGameplay = !showGameplay;
            gameplay.Alpha = showGameplay ? 1 : 0;
        }

        protected override bool OnScroll(ScrollEvent e)
        {
            Vector2 localPoint = camera.ToLocalSpace(e.ScreenSpaceMousePosition);
            SetZoom(zoom * (e.ScrollDelta.Y > 0 ? 1.1f : 1 / 1.1f));
            Vector2 shiftedScreenPoint = camera.ToScreenSpace(localPoint);
            camera.Position += e.ScreenSpaceMousePosition - shiftedScreenPoint;
            return true;
        }

        protected override bool OnDragStart(DragStartEvent e) => e.Button == MouseButton.Middle;

        protected override void OnDrag(DragEvent e)
        {
            base.OnDrag(e);
            if (e.Button == MouseButton.Middle)
                camera.Position += e.Delta;
        }

        private void rebuildPreview()
        {
            preview.Clear(true);
            overlayPreview.Clear(true);
            drawableSprites.Clear();
            previewLoaded = false;
            VisualMetricsChanged?.Invoke();
            int revision = ++previewRevision;
            DrawableStoryboard drawable = beatmap.Storyboard.CreateDrawable();
            drawable.Clock = editorClock;
            LoadComponentAsync(drawable, loaded =>
            {
                if (revision != previewRevision)
                {
                    loaded.Dispose();
                    return;
                }

                preview.Add(loaded);
                overlayPreview.Add(loaded.OverlayLayer.CreateProxy());
                foreach (DrawableStoryboardSprite drawableSprite in loaded.ChildrenOfType<DrawableStoryboardSprite>())
                    drawableSprites[drawableSprite.Sprite] = drawableSprite;

                previewLoaded = true;
                rebuildSelection();
                VisualMetricsChanged?.Invoke();
            });
        }

        private void rebuildGameplay()
        {
            gameplay.Clear(true);
            var playfield = new DodgePlayfield(
                beatmap.HitObjects.OfType<osu.Game.Rulesets.Dodge.Objects.DodgeHitObject>(),
                showPlayer: true,
                playfieldDim: 0.35,
                optimiseArenaFill: false)
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = ARENA_SIZE,
                Clock = editorClock,
            };
            gameplay.Add(playfield);
            gameplay.Alpha = showGameplay ? 1 : 0;
        }

        private void rebuildGrid()
        {
            grid.Clear();
            float width = canvas.Width;
            float storyboardOffsetX = (width - NORMAL_WIDTH) / 2;

            for (float x = storyboardOffsetX; x <= storyboardOffsetX + NORMAL_WIDTH; x += 32)
            {
                grid.Add(new Box
                {
                    X = x,
                    Width = 1,
                    RelativeSizeAxes = Axes.Y,
                    Colour = Color4.White,
                    Alpha = x == storyboardOffsetX + 320 ? 0.35f : 0.1f,
                });
            }

            for (float y = 0; y <= CANVAS_HEIGHT; y += 32)
            {
                grid.Add(new Box
                {
                    Y = y,
                    Height = 1,
                    RelativeSizeAxes = Axes.X,
                    Colour = Color4.White,
                    Alpha = y == 240 ? 0.35f : 0.1f,
                });
            }

            grid.Alpha = showGrid ? 1 : 0;
        }

        private void rebuildSelection()
        {
            selection.Clear();

            double time = editorClock.CurrentTimeAccurate;
            float offsetX = (canvas.Width - NORMAL_WIDTH) / 2;
            foreach (StoryboardLayer layer in beatmap.Storyboard.Layers.OrderByDescending(storyboardLayer => storyboardLayer.Depth))
            {
                foreach (StoryboardSprite sprite in layer.Elements.OfType<StoryboardSprite>())
                {
                    bool selected = ReferenceEquals(sprite, SelectedSprite);
                    bool drawableReady =
                        drawableSprites.TryGetValue(sprite, out DrawableStoryboardSprite? drawableSprite) &&
                        drawableSprite.Texture != null &&
                        drawableSprite.Size.X > 0 &&
                        drawableSprite.Size.Y > 0;
                    StoryboardSelectionPresentation presentation = StoryboardSelectionState.Resolve(
                        sprite,
                        time,
                        selected,
                        drawableReady,
                        previewLoaded);

                    if (presentation == StoryboardSelectionPresentation.DrawableOverlay)
                    {
                        selection.Add(new StoryboardDrawableSelectionOverlay(
                            sprite,
                            drawableSprite!,
                            selected,
                            () => zoom,
                            () => SpriteSelected?.Invoke(layer, sprite),
                            edit => VisualEditRequested?.Invoke(sprite, edit))
                        {
                            Depth = selected ? float.MinValue : layer.Depth,
                        });
                    }
                    else if (presentation != StoryboardSelectionPresentation.None)
                    {
                        string reason = presentation switch
                        {
                            StoryboardSelectionPresentation.OutsideRangeNotice => "Объект сейчас вне своего времени на экране.",
                            StoryboardSelectionPresentation.MissingResourceNotice => $"Не удалось загрузить: {sprite.Path}",
                            StoryboardSelectionPresentation.LoadingNotice => $"Загрузка изображения: {sprite.Path}",
                            _ => throw new ArgumentOutOfRangeException(nameof(presentation)),
                        };
                        selection.Add(new MissingStoryboardResourceNotice(
                            sprite,
                            editorClock,
                            offsetX,
                            reason));
                    }
                }
            }

            selectionActivity.Capture(storyboardSprites(), time);
        }

        private static Drawable createStoryboardFrame() => new Container
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Size = new Vector2(NORMAL_WIDTH, CANVAS_HEIGHT),
            Alpha = 0.45f,
            Depth = 0,
            Children = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.X, Height = 1, Colour = Color4.White },
                new Box { RelativeSizeAxes = Axes.X, Height = 1, Anchor = Anchor.BottomLeft, Origin = Anchor.BottomLeft, Colour = Color4.White },
                new Box { RelativeSizeAxes = Axes.Y, Width = 1, Colour = Color4.White },
                new Box { RelativeSizeAxes = Axes.Y, Width = 1, Anchor = Anchor.TopRight, Origin = Anchor.TopRight, Colour = Color4.White },
            },
        };

        private static Drawable createArenaFrame() => new Container
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Size = ARENA_SIZE,
            Depth = 0,
            Children = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.X, Height = 2, Colour = Color4.Cyan },
                new Box { RelativeSizeAxes = Axes.X, Height = 2, Anchor = Anchor.BottomLeft, Origin = Anchor.BottomLeft, Colour = Color4.Cyan },
                new Box { RelativeSizeAxes = Axes.Y, Width = 2, Colour = Color4.Cyan },
                new Box { RelativeSizeAxes = Axes.Y, Width = 2, Anchor = Anchor.TopRight, Origin = Anchor.TopRight, Colour = Color4.Cyan },
            },
        };

        private static Drawable createSafeArea() => new Container
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            RelativeSizeAxes = Axes.Both,
            Padding = new MarginPadding(24),
            Alpha = 0.35f,
            Depth = 0,
            Children = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.X, Height = 1, Colour = Color4.Yellow },
                new Box { RelativeSizeAxes = Axes.X, Height = 1, Anchor = Anchor.BottomLeft, Origin = Anchor.BottomLeft, Colour = Color4.Yellow },
                new Box { RelativeSizeAxes = Axes.Y, Width = 1, Colour = Color4.Yellow },
                new Box { RelativeSizeAxes = Axes.Y, Width = 1, Anchor = Anchor.TopRight, Origin = Anchor.TopRight, Colour = Color4.Yellow },
            },
        };

        private static Drawable createGameplayHud() => new Container
        {
            RelativeSizeAxes = Axes.Both,
            Depth = 0.5f,
            Children = new Drawable[]
            {
                new OsuSpriteText
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Position = new Vector2(-12, 10),
                    Text = "HUD  0000000  100.00%",
                    Font = OsuFont.GetFont(size: 14, weight: FontWeight.Bold),
                },
            },
        };

        private partial class StoryboardDrawableSelectionOverlay : CompositeDrawable
        {
            private readonly StoryboardSprite sprite;
            private readonly DrawableStoryboardSprite drawable;
            private readonly bool selected;
            private readonly Func<float> getZoom;
            private readonly Action select;
            private readonly Action<StoryboardVisualEdit> applyEdit;
            private readonly Container selectionBox;
            private readonly Box selectionFill;
            private readonly OsuSpriteText label;
            private readonly StoryboardScaleHandle[] scaleHandles;

            private Quad localQuad;
            private Vector2 dragStartMouse;
            private DodgeStoryboardEditing.StoryboardVisualState dragStartState;
            private Vector2 previewPosition;
            private Vector2 scalePivot;
            private Vector2 scaleFixedCorner;
            private Vector2 scaleStartPointer;
            private Vector2 previewScale;
            private int scalingCorner;
            private bool moving;
            private bool scaling;

            public StoryboardDrawableSelectionOverlay(
                StoryboardSprite sprite,
                DrawableStoryboardSprite drawable,
                bool selected,
                Func<float> getZoom,
                Action select,
                Action<StoryboardVisualEdit> applyEdit)
            {
                this.sprite = sprite;
                this.drawable = drawable;
                this.selected = selected;
                this.getZoom = getZoom;
                this.select = select;
                this.applyEdit = applyEdit;
                RelativeSizeAxes = Axes.Both;
                Name = $"Storyboard bounds: {sprite.Path}";

                selectionBox = new Container
                {
                    Masking = true,
                    Child = selectionFill = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                };
                label = new OsuSpriteText
                {
                    Text = selected
                        ? $"{sprite.Path} | перетаскивание: позиция | углы: размер | Shift: пропорционально"
                        : sprite.Path,
                    Font = OsuFont.GetFont(size: 10, weight: FontWeight.Bold),
                    Alpha = selected ? 1 : 0,
                };

                var children = new List<Drawable> { selectionBox, label };
                scaleHandles = selected && DodgeStoryboardEditing.CanRetime(sprite)
                    ? Enumerable.Range(0, 4)
                                .Select(index => new StoryboardScaleHandle(index, beginScale, updateScale, finishScale))
                                .ToArray()
                    : Array.Empty<StoryboardScaleHandle>();
                children.AddRange(scaleHandles);
                InternalChildren = children;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colours)
            {
                Color4 selectionColour = selected ? colours.Highlight1 : colours.Content1;
                selectionBox.BorderColour = selectionColour;
                selectionFill.Colour = selectionColour;
                selectionFill.Alpha = selected ? 0.125f : 0.07f;
                label.Colour = selectionColour;
            }

            public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
                => selected
                    ? drawable.ScreenSpaceDrawQuad.AABBFloat.Inflate(new Vector2(14)).Contains(screenSpacePos)
                    : drawable.ScreenSpaceDrawQuad.Contains(screenSpacePos);

            protected override bool OnHover(HoverEvent e)
            {
                selectionBox.Alpha = 1;
                label.Alpha = 1;
                return true;
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                if (!selected)
                {
                    selectionBox.Alpha = 0;
                    label.Alpha = 0;
                }

                base.OnHoverLost(e);
            }

            protected override bool OnClick(ClickEvent e)
            {
                select();
                return true;
            }

            protected override bool OnMouseDown(MouseDownEvent e) => e.Button == MouseButton.Left;

            protected override bool OnDragStart(DragStartEvent e)
            {
                if (!selected || !DodgeStoryboardEditing.CanRetime(sprite) || e.Button != MouseButton.Left)
                    return false;

                moving = true;
                dragStartMouse = ToLocalSpace(e.ScreenSpaceMousePosition);
                dragStartState = DodgeStoryboardEditing.StateAt(sprite, drawable.Time.Current);
                previewPosition = dragStartState.Position;
                beginLivePreview();
                return true;
            }

            protected override void OnDrag(DragEvent e)
            {
                base.OnDrag(e);
                if (!moving)
                    return;

                Vector2 totalDelta = ToLocalSpace(e.ScreenSpaceMousePosition) - dragStartMouse;
                previewPosition = dragStartState.Position + totalDelta;

                if (!e.AltPressed)
                {
                    previewPosition.X = MathF.Round(previewPosition.X / 16) * 16;
                    previewPosition.Y = MathF.Round(previewPosition.Y / 16) * 16;
                }

                drawable.Position = previewPosition;
            }

            protected override void OnDragEnd(DragEndEvent e)
            {
                base.OnDragEnd(e);
                if (!moving)
                    return;

                moving = false;
                applyEdit(new StoryboardVisualEdit(StoryboardVisualEditKind.Position, previewPosition));
            }

            protected override void Update()
            {
                base.Update();

                localQuad = ToLocalSpace(drawable.ScreenSpaceDrawQuad);
                Vector2 topEdge = localQuad.TopRight - localQuad.TopLeft;
                Vector2 leftEdge = localQuad.BottomLeft - localQuad.TopLeft;
                float zoom = Math.Max(0.1f, getZoom());

                selectionBox.Position = localQuad.TopLeft;
                selectionBox.Size = new Vector2(topEdge.Length, leftEdge.Length);
                selectionBox.Rotation = MathHelper.RadiansToDegrees(MathF.Atan2(topEdge.Y, topEdge.X));
                selectionBox.BorderThickness = 2 / zoom;
                selectionBox.Alpha = selected || IsHovered ? 1 : 0;

                label.Position = localQuad.TopLeft + new Vector2(0, -16 / zoom);
                label.Scale = new Vector2(1 / zoom);

                Vector2[] corners =
                {
                    localQuad.TopLeft,
                    localQuad.TopRight,
                    localQuad.BottomRight,
                    localQuad.BottomLeft,
                };

                for (int i = 0; i < scaleHandles.Length; i++)
                {
                    scaleHandles[i].Position = corners[i];
                    scaleHandles[i].Size = new Vector2(12 / zoom);
                }
            }

            private void beginLivePreview()
            {
                drawable.ClearTransforms();
                drawable.Position = dragStartState.Position;
                drawable.Scale = new Vector2(dragStartState.UniformScale);
                drawable.VectorScale = dragStartState.VectorScale;
                drawable.Rotation = dragStartState.Rotation;
                drawable.Alpha = dragStartState.Opacity;
            }

            private void beginScale(int corner, Vector2 screenSpaceMousePosition)
            {
                if (!selected || !DodgeStoryboardEditing.CanRetime(sprite))
                    return;

                scaling = true;
                scalingCorner = corner;
                dragStartState = DodgeStoryboardEditing.StateAt(sprite, drawable.Time.Current);
                previewScale = dragStartState.EffectiveScale;
                previewPosition = dragStartState.Position;
                scalePivot = ToLocalSpace(drawable.ToScreenSpace(drawable.OriginPosition));
                scaleStartPointer = ToLocalSpace(screenSpaceMousePosition);

                Quad currentQuad = ToLocalSpace(drawable.ScreenSpaceDrawQuad);
                Vector2[] corners =
                {
                    currentQuad.TopLeft,
                    currentQuad.TopRight,
                    currentQuad.BottomRight,
                    currentQuad.BottomLeft,
                };
                scaleFixedCorner = corners[(corner + 2) % corners.Length];
                beginLivePreview();
            }

            private void updateScale(int corner, Vector2 screenSpaceMousePosition, bool lockAspect)
            {
                if (!scaling || corner != scalingCorner)
                    return;

                DodgeStoryboardEditing.CornerScaleResult result = DodgeStoryboardEditing.CalculateCornerScale(
                    dragStartState.EffectiveScale,
                    dragStartState.Position,
                    scalePivot,
                    scaleFixedCorner,
                    scaleStartPointer,
                    ToLocalSpace(screenSpaceMousePosition),
                    dragStartState.Rotation,
                    lockAspect);
                previewScale = result.EffectiveScale;
                previewPosition = result.Position;
                drawable.Scale = Vector2.One;
                drawable.VectorScale = previewScale;
                drawable.Position = previewPosition;
            }

            private void finishScale(int corner)
            {
                if (!scaling || corner != scalingCorner)
                    return;

                scaling = false;
                applyEdit(new StoryboardVisualEdit(StoryboardVisualEditKind.Scale, previewScale, previewPosition));
            }

            private partial class StoryboardScaleHandle : CompositeDrawable
            {
                private readonly int corner;
                private readonly Action<int, Vector2> begin;
                private readonly Action<int, Vector2, bool> update;
                private readonly Action<int> finish;
                private readonly Box box;

                public StoryboardScaleHandle(
                    int corner,
                    Action<int, Vector2> begin,
                    Action<int, Vector2, bool> update,
                    Action<int> finish)
                {
                    this.corner = corner;
                    this.begin = begin;
                    this.update = update;
                    this.finish = finish;
                    Origin = Anchor.Centre;
                    InternalChild = box = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                    };
                }

                [BackgroundDependencyLoader]
                private void load(OverlayColourProvider colours)
                {
                    box.Colour = colours.Highlight1;
                }

                protected override bool OnDragStart(DragStartEvent e)
                {
                    if (e.Button != MouseButton.Left)
                        return false;

                    begin(corner, e.ScreenSpaceMousePosition);
                    return true;
                }

                protected override bool OnMouseDown(MouseDownEvent e) => e.Button == MouseButton.Left;

                protected override void OnDrag(DragEvent e)
                {
                    base.OnDrag(e);
                    update(corner, e.ScreenSpaceMousePosition, e.ShiftPressed);
                }

                protected override void OnDragEnd(DragEndEvent e)
                {
                    base.OnDragEnd(e);
                    finish(corner);
                }

                protected override bool OnClick(ClickEvent e) => true;
            }
        }

        private partial class MissingStoryboardResourceNotice : CompositeDrawable
        {
            private readonly StoryboardSprite sprite;
            private readonly EditorClock editorClock;
            private readonly float offsetX;
            private readonly Box background;
            private readonly OsuSpriteText message;

            public MissingStoryboardResourceNotice(
                StoryboardSprite sprite,
                EditorClock editorClock,
                float offsetX,
                string reason)
            {
                this.sprite = sprite;
                this.editorClock = editorClock;
                this.offsetX = offsetX;
                Size = new Vector2(300, 42);
                Origin = Anchor.Centre;
                Depth = float.MinValue;
                InternalChildren = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                    message = new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = reason,
                        Font = OsuFont.GetFont(size: 11, weight: FontWeight.Bold),
                    },
                };
            }

            [BackgroundDependencyLoader]
            private void load(OsuColour colours)
            {
                background.Colour = colours.Orange4;
                background.Alpha = 0.9f;
                message.Colour = colours.Orange1;
            }

            protected override void Update()
            {
                base.Update();
                Vector2 position = DodgeStoryboardEditing.PositionAt(sprite, editorClock.CurrentTimeAccurate);
                Position = new Vector2(offsetX + position.X, position.Y);
            }
        }
    }
}
