// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Rulesets.Dodge.Beatmaps;
using osu.Game.Rulesets.Dodge.Localisation;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Dodge.UI;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Utils;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Edit
{
    /// <summary>
    /// Fast authoring helpers for common Dodge patterns.
    /// </summary>
    public partial class DodgePatternToolboxGroup : EditorToolboxGroup
    {
        private const string quick_prefab_path = "dodge/prefabs/quick-{0}.dodge.json";

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        [Resolved]
        private IBeatSnapProvider beatSnapProvider { get; set; } = null!;

        [Resolved]
        private DodgeEditorSettings settings { get; set; } = null!;

        [Resolved]
        private DodgeHitObjectComposer composer { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        private readonly BindableInt objectCount = new BindableInt(8)
        {
            MinValue = 2,
            MaxValue = 32,
        };

        private readonly BindableInt timeStep = new BindableInt(1)
        {
            MinValue = 1,
            MaxValue = 16,
        };

        private readonly BindableFloat offsetX = new BindableFloat
        {
            MinValue = -128,
            MaxValue = 128,
            Precision = 1,
        };

        private readonly BindableFloat offsetY = new BindableFloat
        {
            MinValue = -128,
            MaxValue = 128,
            Precision = 1,
        };

        private readonly BindableFloat rotationStep = new BindableFloat
        {
            MinValue = -180,
            MaxValue = 180,
            Precision = 1,
        };

        private readonly BindableInt prefabSlot = new BindableInt(1)
        {
            MinValue = 1,
            MaxValue = 8,
        };

        private readonly BindableList<HitObject> selectedHitObjects = new BindableList<HitObject>();
        private ExpandableButton repeatButton = null!;
        private ExpandableButton savePrefabButton = null!;
        private ExpandableButton insertPrefabButton = null!;
        private OsuTextFlowContainer selectionRequiredText = null!;
        private GridContainer patternBrowser = null!;
        private GridContainer savedPatternBrowser = null!;
        private readonly DodgeSavedPatternPreviewCard[] savedPatternCards = new DodgeSavedPatternPreviewCard[8];

        public bool CanRepeatSelection => repeatButton.Enabled.Value;
        public bool CanSavePrefab => savePrefabButton.Enabled.Value;
        public bool CanInsertPrefab => insertPrefabButton.Enabled.Value;
        public int PatternPreviewCardCount => Enum.GetValues<DodgePatternType>().Length;
        public int SavedPatternPreviewCardCount => savedPatternCards.Length;

        public DodgePatternToolboxGroup()
            : base(DodgeEditorStrings.Patterns)
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Spacing = new Vector2(5);
            Children = new Drawable[]
            {
                new ExpandableSlider<int>
                {
                    Current = objectCount,
                    KeyboardStep = 1,
                    ExpandedLabelText = DodgeEditorStrings.ObjectRepeatCount,
                    ContractedLabelText = DodgeEditorStrings.Count(8),
                },
                new ExpandableSlider<int>
                {
                    Current = timeStep,
                    KeyboardStep = 1,
                    ExpandedLabelText = DodgeEditorStrings.TimeStep,
                    ContractedLabelText = DodgeEditorStrings.Step(1),
                },
                new ExpandableSlider<float>
                {
                    Current = offsetX,
                    KeyboardStep = 1,
                    ExpandedLabelText = DodgeEditorStrings.RepeatXOffset,
                    ContractedLabelText = "X: 0",
                },
                new ExpandableSlider<float>
                {
                    Current = offsetY,
                    KeyboardStep = 1,
                    ExpandedLabelText = DodgeEditorStrings.RepeatYOffset,
                    ContractedLabelText = "Y: 0",
                },
                new ExpandableSlider<float>
                {
                    Current = rotationStep,
                    KeyboardStep = 1,
                    ExpandedLabelText = DodgeEditorStrings.RepeatRotation,
                    ContractedLabelText = "R: 0°",
                },
                new ExpandableSlider<int>
                {
                    Current = prefabSlot,
                    KeyboardStep = 1,
                    ExpandedLabelText = DodgeEditorStrings.QuickPrefabSlot,
                    ContractedLabelText = DodgeEditorStrings.Prefab(1),
                },
                repeatButton = createButton(DodgeEditorStrings.RepeatArray, repeatSelection),
                patternBrowser = createPatternBrowser(),
                savedPatternBrowser = createSavedPatternBrowser(),
                savePrefabButton = createButton(DodgeEditorStrings.SaveQuickPrefab, saveQuickPrefab),
                insertPrefabButton = createButton(DodgeEditorStrings.InsertQuickPrefab, insertQuickPrefab),
                selectionRequiredText = new OsuTextFlowContainer(sprite => sprite.Font = OsuFont.GetFont(size: 12))
                {
                    Text = DodgeEditorStrings.PatternSelectionRequired,
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Alpha = 0.6f,
                },
            };

            objectCount.BindValueChanged(value => ((ExpandableSlider<int>)Children[0]).ContractedLabelText = DodgeEditorStrings.Count(value.NewValue), true);
            timeStep.BindValueChanged(value => ((ExpandableSlider<int>)Children[1]).ContractedLabelText = DodgeEditorStrings.Step(value.NewValue), true);
            offsetX.BindValueChanged(value => ((ExpandableSlider<float>)Children[2]).ContractedLabelText = $"X: {value.NewValue:N0}", true);
            offsetY.BindValueChanged(value => ((ExpandableSlider<float>)Children[3]).ContractedLabelText = $"Y: {value.NewValue:N0}", true);
            rotationStep.BindValueChanged(value => ((ExpandableSlider<float>)Children[4]).ContractedLabelText = $"R: {value.NewValue:N0}°", true);
            prefabSlot.BindValueChanged(value =>
            {
                ((ExpandableSlider<int>)Children[5]).ContractedLabelText = DodgeEditorStrings.Prefab(value.NewValue);

                foreach (DodgeSavedPatternPreviewCard card in savedPatternCards)
                    card.SetSelected(card.Slot == value.NewValue);

                updateActionAvailability();
            }, true);

            refreshSavedPatternPreviews();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            selectedHitObjects.BindTo(editorBeatmap.SelectedHitObjects);
            selectedHitObjects.BindCollectionChanged((_, _) => updateActionAvailability(), true);
        }

        private static ExpandableButton createButton(LocalisableString label, Action action) => new ExpandableButton
        {
            RelativeSizeAxes = Axes.X,
            ContractedLabelText = label,
            ExpandedLabelText = label,
            Action = action,
        };

        private GridContainer createPatternBrowser() => new GridContainer
        {
            Name = "Dodge pattern browser",
            RelativeSizeAxes = Axes.X,
            Height = 234,
            RowDimensions = new[]
            {
                new Dimension(GridSizeMode.Absolute, 74),
                new Dimension(GridSizeMode.Absolute, 74),
                new Dimension(GridSizeMode.Absolute, 74),
            },
            ColumnDimensions = new[]
            {
                new Dimension(),
                new Dimension(),
            },
            Content = new[]
            {
                new Drawable[]
                {
                    createPatternCard(DodgePatternType.Ring, DodgeEditorStrings.Ring),
                    createPatternCard(DodgePatternType.Spiral, DodgeEditorStrings.Spiral),
                },
                new Drawable[]
                {
                    createPatternCard(DodgePatternType.Fan, DodgeEditorStrings.Fan),
                    createPatternCard(DodgePatternType.Wall, DodgeEditorStrings.WallSafeGap),
                },
                new Drawable[]
                {
                    createPatternCard(DodgePatternType.Sweep, DodgeEditorStrings.Sweep),
                    createPatternCard(DodgePatternType.Cross, DodgeEditorStrings.Cross),
                },
            },
        };

        private DodgePatternPreviewCard createPatternCard(DodgePatternType type, LocalisableString label) => new DodgePatternPreviewCard(type, label)
        {
            RelativeSizeAxes = Axes.Both,
            Margin = new MarginPadding(3),
            Action = () => InsertPattern(type),
        };

        private GridContainer createSavedPatternBrowser() => new GridContainer
        {
            Name = "Saved Dodge patterns",
            RelativeSizeAxes = Axes.X,
            Height = 296,
            RowDimensions = Enumerable.Repeat(new Dimension(GridSizeMode.Absolute, 74), 4).ToArray(),
            ColumnDimensions = new[]
            {
                new Dimension(),
                new Dimension(),
            },
            Content = Enumerable.Range(0, 4).Select(row => new Drawable[]
            {
                createSavedPatternCard(row * 2 + 1),
                createSavedPatternCard(row * 2 + 2),
            }).ToArray(),
        };

        private DodgeSavedPatternPreviewCard createSavedPatternCard(int slot) => savedPatternCards[slot - 1] = new DodgeSavedPatternPreviewCard(slot)
        {
            RelativeSizeAxes = Axes.Both,
            Margin = new MarginPadding(3),
            Action = () => ActivateSavedPatternSlot(slot),
        };

        public void ActivateSavedPatternSlot(int slot)
        {
            if (slot < 1 || slot > savedPatternCards.Length)
                throw new ArgumentOutOfRangeException(nameof(slot));

            DodgeHitObject[] selectedObjects = editorBeatmap.SelectedHitObjects.OfType<DodgeHitObject>().ToArray();
            prefabSlot.Value = slot;

            if (savedPatternCards[slot - 1].HasPattern)
                insertQuickPrefab();
            else if (selectedObjects.Length > 0)
                saveQuickPrefab(selectedObjects);
        }

        public void InsertPattern(DodgePatternType type)
        {
            switch (type)
            {
                case DodgePatternType.Ring:
                    createRing();
                    break;

                case DodgePatternType.Spiral:
                    createSpiral();
                    break;

                case DodgePatternType.Fan:
                    createFan();
                    break;

                case DodgePatternType.Wall:
                    createWall(false);
                    break;

                case DodgePatternType.Sweep:
                    createWall(true);
                    break;

                case DodgePatternType.Cross:
                    createCross();
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        private double currentTime => Math.Min(beatSnapProvider.SnapTime(editorClock.CurrentTime), editorClock.TrackLength);

        private double snapStep(double time) => beatSnapProvider.GetBeatLengthAtTime(time) * timeStep.Value;

        private double defaultDuration(double time) => beatSnapProvider.GetBeatLengthAtTime(time) * 4;

        private void updateActionAvailability()
        {
            bool hasSelection = selectedHitObjects.OfType<DodgeHitObject>().Any();
            bool hasCloneableSelection = selectedHitObjects.OfType<DodgeHitObject>().Any();

            repeatButton.Enabled.Value = hasSelection;
            savePrefabButton.Enabled.Value = hasCloneableSelection;
            insertPrefabButton.Enabled.Value = File.Exists(storage.GetFullPath(string.Format(quick_prefab_path, prefabSlot.Value)));
            selectionRequiredText.Alpha = hasSelection ? 0 : 0.6f;

            foreach (DodgeSavedPatternPreviewCard card in savedPatternCards)
                card.SetCanSave(hasCloneableSelection);
        }

        private void repeatSelection()
        {
            DodgeHitObject[] originals = editorBeatmap.SelectedHitObjects.OfType<DodgeHitObject>().ToArray();

            if (originals.Length == 0)
                return;

            double step = snapStep(originals.Max(item => item.GetEndTime()));
            var added = new List<DodgeHitObject>();
            Vector2 originalOrigin = DodgeSelectionTransformUtils.GetSurroundingQuad(originals).Centre;

            for (int generation = 1; generation < objectCount.Value; generation++)
            {
                Vector2 spatialOffset = new Vector2(offsetX.Value, offsetY.Value) * generation;

                foreach (DodgeHitObject original in originals)
                {
                    DodgeHitObject copy = DodgeHitObjectCloner.Clone(original, step * generation, spatialOffset);

                    if (copy is DodgeBullet or DodgeEmitter or DodgeBeam or DodgeCameraChange)
                    {
                        var points = DodgeSelectionTransformUtils.GetPoints(copy);
                        Vector2 rotationOrigin = originalOrigin + spatialOffset;
                        DodgeSelectionTransformUtils.SetPoints(
                            copy,
                            GeometryUtils.RotatePointAroundOrigin(points.Start, rotationOrigin, rotationStep.Value * generation),
                            GeometryUtils.RotatePointAroundOrigin(points.End, rotationOrigin, rotationStep.Value * generation));
                    }

                    if (copy is DodgeArenaChange arena)
                        arena.ClampToBaseBounds();

                    added.Add(copy);
                }
            }

            addAndSelect(added);
        }

        private void createRing()
        {
            double time = currentTime;
            addAndSelect(new[]
            {
                new DodgeEmitter
                {
                    StartTime = time,
                    Duration = defaultDuration(time),
                    Position = DodgePlayfield.BASE_SIZE / 2,
                    AimPosition = DodgePlayfield.BASE_SIZE / 2 + new Vector2(120, 0),
                    MovementEndPosition = DodgePlayfield.BASE_SIZE / 2,
                    BulletCount = objectCount.Value,
                    SpreadAngle = 360,
                    Shape = settings.EmitterShape.Value,
                    ContinueUntilExit = settings.EmitterContinueUntilExit.Value,
                    MovementType = settings.EmitterMovementType.Value,
                    MovementEasing = settings.EmitterMovementEasing.Value,
                    WaveAmplitude = settings.EmitterWaveAmplitude.Value,
                    WaveCycles = settings.EmitterWaveCycles.Value,
                    WavePhase = settings.EmitterWavePhase.Value,
                    TrajectoryGuideStyle = settings.EmitterTrajectoryGuideStyle.Value,
                    BurstRotation = settings.EmitterBurstRotation.Value,
                    Colour = settings.EmitterColour.Value,
                    OutlineColour = settings.EmitterOutlineColour.Value,
                    Opacity = settings.EmitterOpacity.Value,
                    OutlineThickness = settings.EmitterOutlineThickness.Value,
                },
            });
        }

        private void createFan()
        {
            double time = currentTime;
            addAndSelect(new[]
            {
                new DodgeEmitter
                {
                    StartTime = time,
                    Duration = defaultDuration(time),
                    Position = new Vector2(DodgePlayfield.WIDTH / 2, 8),
                    AimPosition = DodgePlayfield.BASE_SIZE / 2,
                    MovementEndPosition = new Vector2(DodgePlayfield.WIDTH / 2, 8),
                    BulletCount = objectCount.Value,
                    SpreadAngle = settings.EmitterSpreadAngle.Value,
                    Shape = settings.EmitterShape.Value,
                    ContinueUntilExit = settings.EmitterContinueUntilExit.Value,
                    MovementType = settings.EmitterMovementType.Value,
                    MovementEasing = settings.EmitterMovementEasing.Value,
                    WaveAmplitude = settings.EmitterWaveAmplitude.Value,
                    WaveCycles = settings.EmitterWaveCycles.Value,
                    WavePhase = settings.EmitterWavePhase.Value,
                    TrajectoryGuideStyle = settings.EmitterTrajectoryGuideStyle.Value,
                    BurstRotation = settings.EmitterBurstRotation.Value,
                    Colour = settings.EmitterColour.Value,
                    OutlineColour = settings.EmitterOutlineColour.Value,
                    Opacity = settings.EmitterOpacity.Value,
                    OutlineThickness = settings.EmitterOutlineThickness.Value,
                },
            });
        }

        private void createSpiral()
        {
            double time = currentTime;
            double step = snapStep(time);
            double duration = defaultDuration(time);
            Vector2 centre = DodgePlayfield.BASE_SIZE / 2;
            var bullets = new List<DodgeHitObject>();

            for (int i = 0; i < objectCount.Value; i++)
            {
                float angle = i * 360f / objectCount.Value;
                float radius = 64 + i * 8;
                Vector2 direction = GeometryUtils.RotateVector(Vector2.UnitX, angle);
                bullets.Add(new DodgeBullet
                {
                    StartTime = time + i * step,
                    Duration = duration,
                    Position = centre,
                    EndPosition = centre + direction * radius,
                    Shape = settings.BulletShape.Value,
                    ContinueUntilExit = settings.BulletContinueUntilExit.Value,
                    MovementType = settings.BulletMovementType.Value,
                    MovementEasing = settings.BulletMovementEasing.Value,
                    WaveAmplitude = settings.BulletWaveAmplitude.Value,
                    WaveCycles = settings.BulletWaveCycles.Value,
                    WavePhase = settings.BulletWavePhase.Value,
                    TrajectoryGuideStyle = settings.BulletTrajectoryGuideStyle.Value,
                    Colour = settings.BulletColour.Value,
                    OutlineColour = settings.BulletOutlineColour.Value,
                    Opacity = settings.BulletOpacity.Value,
                    OutlineThickness = settings.BulletOutlineThickness.Value,
                });
            }

            addAndSelect(bullets);
        }

        private void createWall(bool staggered)
        {
            double time = currentTime;
            double step = snapStep(time);
            var bullets = new List<DodgeHitObject>();

            for (int i = 0; i < objectCount.Value; i++)
            {
                if (!staggered && i == objectCount.Value / 2)
                    continue;

                float x = (i + 0.5f) * DodgePlayfield.WIDTH / objectCount.Value;
                bullets.Add(new DodgeBullet
                {
                    StartTime = time + (staggered ? i * step : 0),
                    Duration = defaultDuration(time),
                    Position = new Vector2(x, 0),
                    EndPosition = new Vector2(x, DodgePlayfield.HEIGHT),
                    Shape = settings.BulletShape.Value,
                    ContinueUntilExit = settings.BulletContinueUntilExit.Value,
                    MovementType = settings.BulletMovementType.Value,
                    MovementEasing = settings.BulletMovementEasing.Value,
                    WaveAmplitude = settings.BulletWaveAmplitude.Value,
                    WaveCycles = settings.BulletWaveCycles.Value,
                    WavePhase = settings.BulletWavePhase.Value,
                    TrajectoryGuideStyle = settings.BulletTrajectoryGuideStyle.Value,
                    Colour = settings.BulletColour.Value,
                    OutlineColour = settings.BulletOutlineColour.Value,
                    Opacity = settings.BulletOpacity.Value,
                    OutlineThickness = settings.BulletOutlineThickness.Value,
                });
            }

            addAndSelect(bullets);
        }

        private void createCross()
        {
            double time = currentTime;
            double duration = defaultDuration(time);
            Vector2 centre = DodgePlayfield.BASE_SIZE / 2;
            var directions = new[] { -Vector2.UnitX, Vector2.UnitX, -Vector2.UnitY, Vector2.UnitY };

            addAndSelect(directions.Select(direction => new DodgeBullet
            {
                StartTime = time,
                Duration = duration,
                Position = centre + direction * new Vector2(DodgePlayfield.WIDTH / 2, DodgePlayfield.HEIGHT / 2),
                EndPosition = centre - direction * new Vector2(DodgePlayfield.WIDTH / 2, DodgePlayfield.HEIGHT / 2),
                Shape = settings.BulletShape.Value,
                ContinueUntilExit = settings.BulletContinueUntilExit.Value,
                MovementType = settings.BulletMovementType.Value,
                MovementEasing = settings.BulletMovementEasing.Value,
                WaveAmplitude = settings.BulletWaveAmplitude.Value,
                WaveCycles = settings.BulletWaveCycles.Value,
                WavePhase = settings.BulletWavePhase.Value,
                TrajectoryGuideStyle = settings.BulletTrajectoryGuideStyle.Value,
                Colour = settings.BulletColour.Value,
                OutlineColour = settings.BulletOutlineColour.Value,
                Opacity = settings.BulletOpacity.Value,
                OutlineThickness = settings.BulletOutlineThickness.Value,
            }));
        }

        private void saveQuickPrefab() => saveQuickPrefab(editorBeatmap.SelectedHitObjects.OfType<DodgeHitObject>().ToArray());

        private void saveQuickPrefab(IReadOnlyCollection<DodgeHitObject> selectedObjects)
        {
            if (selectedObjects.Count == 0)
                return;

            double firstTime = selectedObjects.Min(item => item.StartTime);
            Vector2 origin = DodgeSelectionTransformUtils.GetSurroundingQuad(selectedObjects).Centre;
            var prefabObjects = selectedObjects.Select(item => DodgeHitObjectCloner.Clone(item, -firstTime, -origin)).ToList();
            var prefab = new Beatmap<DodgeHitObject> { HitObjects = prefabObjects };

            string path = storage.GetFullPath(string.Format(quick_prefab_path, prefabSlot.Value), true);
            string? directory = Path.GetDirectoryName(path);

            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using (FileStream stream = File.Create(path))
                DodgeBeatmapSerializer.Serialize(prefab, stream);

            refreshSavedPatternPreviews();
            updateActionAvailability();
        }

        private void refreshSavedPatternPreviews()
        {
            for (int slot = 1; slot <= savedPatternCards.Length; slot++)
            {
                string path = storage.GetFullPath(string.Format(quick_prefab_path, slot));

                if (!File.Exists(path))
                {
                    savedPatternCards[slot - 1].SetPattern(null);
                    continue;
                }

                try
                {
                    using FileStream stream = File.OpenRead(path);
                    savedPatternCards[slot - 1].SetPattern(DodgeBeatmapSerializer.DeserializeHitObjects(stream));
                }
                catch
                {
                    savedPatternCards[slot - 1].SetPattern(null);
                }
            }
        }

        private void insertQuickPrefab()
        {
            string path = storage.GetFullPath(string.Format(quick_prefab_path, prefabSlot.Value));

            if (!File.Exists(path))
                return;

            using FileStream stream = File.OpenRead(path);
            List<DodgeHitObject> objects = DodgeBeatmapSerializer.DeserializeHitObjects(stream);
            double time = currentTime;
            Vector2 origin = composer.GridToolbox.StartPosition.Value;

            addAndSelect(objects.Select(item =>
            {
                DodgeHitObject clone = DodgeHitObjectCloner.Clone(item, time, origin);

                if (clone is DodgeArenaChange arena)
                    arena.ClampToBaseBounds();

                return clone;
            }));
        }

        private void addAndSelect(IEnumerable<DodgeHitObject> objects)
        {
            DodgeHitObject[] added = objects.Where(item => DodgeEditorTrackBounds.Constrain(item, editorClock.TrackLength, false)).ToArray();

            if (added.Length == 0)
                return;

            editorBeatmap.AddRange(added);
            editorBeatmap.SelectedHitObjects.Clear();
            editorBeatmap.SelectedHitObjects.AddRange(added);
        }

    }

    public enum DodgePatternType
    {
        Ring,
        Spiral,
        Fan,
        Wall,
        Sweep,
        Cross,
    }

    /// <summary>
    /// A compact animated preview used by the pattern browser. It deliberately uses lightweight dots
    /// instead of gameplay drawables so browsing patterns does not create hit objects or collision state.
    /// </summary>
    public partial class DodgePatternPreviewCard : OsuClickableContainer
    {
        private const int dot_count = 9;

        private readonly DodgePatternType patternType;
        private readonly CircularContainer[] dots = new CircularContainer[dot_count];
        private Box background = null!;
        private double animationStartTime;

        public DodgePatternType PatternType => patternType;

        public DodgePatternPreviewCard(DodgePatternType patternType, LocalisableString label)
        {
            this.patternType = patternType;
            Masking = true;
            CornerRadius = 6;

            AddRangeInternal(new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Colour4.FromHex("#20232B"),
                },
                new Container
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Size = new Vector2(52),
                    Children = dots.Select((_, index) => dots[index] = new CircularContainer
                    {
                    RelativePositionAxes = Axes.Both,
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.Centre,
                        Size = new Vector2(index == 0 ? 6 : 4),
                        Masking = true,
                        Child = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Colour4.White,
                        },
                    }).ToArray(),
                },
                new OsuTextFlowContainer(sprite =>
                {
                    sprite.Font = OsuFont.GetFont(size: 10, weight: FontWeight.SemiBold);
                })
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Y = -3,
                    Width = 0.94f,
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    TextAnchor = Anchor.TopCentre,
                    Text = label,
                },
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            animationStartTime = Time.Current;
        }

        protected override void Update()
        {
            base.Update();

            float phase = (float)(((Time.Current - animationStartTime) % 1800) / 1800);

            for (int i = 0; i < dots.Length; i++)
            {
                DodgePatternPreviewSample sample = DodgePatternPreviewLayout.Sample(patternType, i, phase);
                dots[i].Position = sample.Position;
                dots[i].Alpha = sample.Element == DodgePatternPreviewElement.Hidden ? 0 : sample.Alpha;
                dots[i].Colour = sample.Element == DodgePatternPreviewElement.Emitter
                    ? Colour4.FromHex("#FFB45E")
                    : Colour4.FromHex("#62D9FF");
                dots[i].Size = new Vector2(sample.Element == DodgePatternPreviewElement.Emitter ? 7 : 4);
            }

            background.Colour = IsHovered ? Colour4.FromHex("#343946") : Colour4.FromHex("#20232B");
        }
    }

    public enum DodgePatternPreviewElement
    {
        Hidden,
        Projectile,
        Emitter,
    }

    public readonly record struct DodgePatternPreviewSample(Vector2 Position, DodgePatternPreviewElement Element, float Alpha = 1);

    /// <summary>
    /// Samples a tiny gameplay-like simulation for pattern cards. Orange dots are emitters and blue dots
    /// are projectiles. The layouts intentionally mirror the objects created by <see cref="DodgePatternToolboxGroup.InsertPattern"/>.
    /// </summary>
    public static class DodgePatternPreviewLayout
    {
        private const int dot_count = 9;

        public static DodgePatternPreviewSample Sample(DodgePatternType type, int index, float phase)
        {
            phase = Math.Clamp(phase, 0, 1);
            float angle;

            switch (type)
            {
                case DodgePatternType.Ring:
                    if (index == 0)
                        return emitterAt(new Vector2(0.5f));

                    angle = MathHelper.TwoPi * (index - 1) / (dot_count - 1);
                    return projectileAt(radialPosition(new Vector2(0.5f), angle, 0.05f + phase * 0.38f));

                case DodgePatternType.Spiral:
                    float spiralAge = wrap(phase - index * 0.075f);
                    angle = MathHelper.TwoPi * index / dot_count;
                    return projectileAt(radialPosition(new Vector2(0.5f), angle, 0.04f + spiralAge * 0.39f), 0.45f + 0.55f * spiralAge);

                case DodgePatternType.Fan:
                    if (index == 0)
                        return emitterAt(new Vector2(0.5f, 0.1f));

                    float fanProgress = (float)(index - 1) / (dot_count - 2);
                    angle = MathHelper.DegreesToRadians(55 + fanProgress * 70);
                    float fanRadius = 0.04f + phase * 0.52f;
                    return projectileAt(new Vector2(
                        0.5f + MathF.Cos(angle) * fanRadius,
                        0.1f + MathF.Sin(angle) * fanRadius * 0.72f));

                case DodgePatternType.Wall:
                    if (index == dot_count / 2)
                        return hidden();

                    return projectileAt(new Vector2(0.08f + index * 0.84f / (dot_count - 1), 0.08f + phase * 0.78f));

                case DodgePatternType.Sweep:
                    float sweepProgress = (float)index / (dot_count - 1);
                    float sweepAge = wrap(phase - sweepProgress * 0.42f);
                    return projectileAt(new Vector2(0.08f + sweepProgress * 0.84f, 0.08f + sweepAge * 0.78f), 0.4f + sweepAge * 0.6f);

                case DodgePatternType.Cross:
                    return index switch
                    {
                        0 => projectileAt(new Vector2(0.08f + phase * 0.84f, 0.5f)),
                        1 => projectileAt(new Vector2(0.92f - phase * 0.84f, 0.5f)),
                        2 => projectileAt(new Vector2(0.5f, 0.08f + phase * 0.84f)),
                        3 => projectileAt(new Vector2(0.5f, 0.92f - phase * 0.84f)),
                        _ => hidden(),
                    };

                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        private static DodgePatternPreviewSample emitterAt(Vector2 position) => new DodgePatternPreviewSample(position, DodgePatternPreviewElement.Emitter);

        private static DodgePatternPreviewSample projectileAt(Vector2 position, float alpha = 1) => new DodgePatternPreviewSample(position, DodgePatternPreviewElement.Projectile, alpha);

        private static DodgePatternPreviewSample hidden() => new DodgePatternPreviewSample(Vector2.Zero, DodgePatternPreviewElement.Hidden, 0);

        private static Vector2 radialPosition(Vector2 centre, float angle, float radius) => new Vector2(
            centre.X + MathF.Cos(angle) * radius,
            centre.Y + MathF.Sin(angle) * radius);

        private static float wrap(float value) => value - MathF.Floor(value);
    }

    public enum DodgeSavedPatternMarkerRole
    {
        Projectile,
        Emitter,
        Structure,
    }

    public readonly record struct DodgeSavedPatternMarker(Vector2 Start, Vector2 End, DodgeSavedPatternMarkerRole Role, float Delay);

    /// <summary>
    /// Builds a compact preview directly from the objects stored in a local prefab.
    /// </summary>
    public static class DodgeSavedPatternPreviewLayout
    {
        public const int MAX_MARKERS = 32;

        public static IReadOnlyList<DodgeSavedPatternMarker> Build(IReadOnlyList<DodgeHitObject> objects)
        {
            if (objects.Count == 0)
                return Array.Empty<DodgeSavedPatternMarker>();

            double timelineStart = objects.Min(item => item.StartTime);
            double timelineEnd = objects.Max(item => item.GetEndTime());
            double timelineDuration = Math.Max(1, timelineEnd - timelineStart);
            var markers = new List<DodgeSavedPatternMarker>();

            foreach (DodgeHitObject hitObject in objects.OrderBy(item => item.StartTime))
            {
                switch (hitObject)
                {
                    case DodgeBullet bullet:
                        markers.Add(new DodgeSavedPatternMarker(
                            bullet.Position,
                            bullet.EndPosition,
                            DodgeSavedPatternMarkerRole.Projectile,
                            delayFor(bullet.StartTime, timelineStart, timelineDuration)));
                        break;

                    case DodgeEmitter emitter:
                        markers.Add(new DodgeSavedPatternMarker(
                            emitter.SourcePositionAt(0),
                            emitter.SourcePositionAt(emitter.EffectiveBurstCount - 1),
                            DodgeSavedPatternMarkerRole.Emitter,
                            delayFor(emitter.StartTime, timelineStart, timelineDuration)));

                        int totalProjectiles = emitter.EffectiveBulletCount * emitter.EffectiveBurstCount;
                        int previewProjectiles = Math.Min(20, totalProjectiles);

                        for (int sample = 0; sample < previewProjectiles; sample++)
                        {
                            int flatIndex = previewProjectiles == 1
                                ? 0
                                : (int)MathF.Round(sample * (totalProjectiles - 1f) / (previewProjectiles - 1));
                            int burstIndex = flatIndex / emitter.EffectiveBulletCount;
                            int bulletIndex = flatIndex % emitter.EffectiveBulletCount;
                            markers.Add(new DodgeSavedPatternMarker(
                                emitter.SourcePositionAt(burstIndex),
                                emitter.EndPositionAt(burstIndex, bulletIndex),
                                DodgeSavedPatternMarkerRole.Projectile,
                                delayFor(emitter.EmissionTimeAt(burstIndex), timelineStart, timelineDuration)));
                        }

                        break;

                    case DodgeBeam beam:
                        for (int i = 0; i < 7; i++)
                        {
                            Vector2 point = beam.Position + (beam.EndPosition - beam.Position) * (i / 6f);
                            markers.Add(new DodgeSavedPatternMarker(point, point, DodgeSavedPatternMarkerRole.Structure, 0));
                        }

                        break;

                    case DodgeArenaChange arena:
                        Vector2 topLeft = arena.TargetPosition;
                        Vector2 bottomRight = arena.TargetPosition + arena.TargetSize;
                        markers.AddRange(new[]
                        {
                            structureAt(topLeft),
                            structureAt(new Vector2(bottomRight.X, topLeft.Y)),
                            structureAt(bottomRight),
                            structureAt(new Vector2(topLeft.X, bottomRight.Y)),
                        });
                        break;

                    case DodgeCameraChange camera:
                        markers.Add(new DodgeSavedPatternMarker(camera.Position, camera.EndPosition, DodgeSavedPatternMarkerRole.Structure, 0));
                        break;
                }
            }

            return normalise(markers.Take(MAX_MARKERS).ToArray());
        }

        private static DodgeSavedPatternMarker structureAt(Vector2 position) => new DodgeSavedPatternMarker(position, position, DodgeSavedPatternMarkerRole.Structure, 0);

        private static float delayFor(double time, double timelineStart, double timelineDuration)
            => (float)Math.Clamp((time - timelineStart) / timelineDuration * 0.65, 0, 0.65);

        private static IReadOnlyList<DodgeSavedPatternMarker> normalise(IReadOnlyList<DodgeSavedPatternMarker> markers)
        {
            if (markers.Count == 0)
                return markers;

            float minX = markers.Min(marker => Math.Min(marker.Start.X, marker.End.X));
            float maxX = markers.Max(marker => Math.Max(marker.Start.X, marker.End.X));
            float minY = markers.Min(marker => Math.Min(marker.Start.Y, marker.End.Y));
            float maxY = markers.Max(marker => Math.Max(marker.Start.Y, marker.End.Y));
            float spanX = maxX - minX;
            float spanY = maxY - minY;
            float commonSpan = Math.Max(1, Math.Max(spanX, spanY));
            float centreX = (minX + maxX) / 2;
            float centreY = (minY + maxY) / 2;

            Vector2 map(Vector2 position) => new Vector2(
                Math.Clamp(0.5f + (position.X - centreX) / commonSpan * 0.8f, 0.1f, 0.9f),
                Math.Clamp(0.5f + (position.Y - centreY) / commonSpan * 0.8f, 0.1f, 0.9f));

            return markers.Select(marker => marker with
            {
                Start = map(marker.Start),
                End = map(marker.End),
            }).ToArray();
        }
    }

    public partial class DodgeSavedPatternPreviewCard : OsuClickableContainer
    {
        private readonly CircularContainer[] dots = new CircularContainer[DodgeSavedPatternPreviewLayout.MAX_MARKERS];
        private readonly OsuSpriteText emptyText;
        private readonly Box background;
        private IReadOnlyList<DodgeSavedPatternMarker> markers = Array.Empty<DodgeSavedPatternMarker>();
        private double animationStartTime;
        private bool canSave;
        private bool selected;

        public int Slot { get; }
        public bool HasPattern => markers.Count > 0;
        public int MarkerCount => markers.Count;

        public DodgeSavedPatternPreviewCard(int slot)
        {
            Slot = slot;
            Masking = true;
            CornerRadius = 6;

            AddRangeInternal(new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Colour4.FromHex("#1B1E25"),
                },
                new Container
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Size = new Vector2(52),
                    Children = dots.Select((_, index) => dots[index] = new CircularContainer
                    {
                        RelativePositionAxes = Axes.Both,
                        Anchor = Anchor.TopLeft,
                        Origin = Anchor.Centre,
                        Size = new Vector2(4),
                        Masking = true,
                        Alpha = 0,
                        Child = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Colour4.White,
                        },
                    }).ToArray(),
                },
                emptyText = new OsuSpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Y = -6,
                    Text = DodgeEditorStrings.EmptyPatternSlot,
                    Font = OsuFont.GetFont(size: 10),
                    Alpha = 0.45f,
                },
                new OsuSpriteText
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Y = -4,
                    Text = DodgeEditorStrings.Prefab(slot),
                    Font = OsuFont.GetFont(size: 10, weight: FontWeight.SemiBold),
                },
            });
        }

        public void SetPattern(IReadOnlyList<DodgeHitObject>? objects)
        {
            markers = objects == null
                ? Array.Empty<DodgeSavedPatternMarker>()
                : DodgeSavedPatternPreviewLayout.Build(objects);
            updateEmptyState();
        }

        public void SetCanSave(bool value)
        {
            canSave = value;
            updateEmptyState();
        }

        public void SetSelected(bool value)
        {
            selected = value;
            BorderThickness = selected ? 2 : 0;
            BorderColour = Colour4.FromHex("#FFB45E");
        }

        private void updateEmptyState()
        {
            emptyText.Text = canSave ? DodgeEditorStrings.SavePatternHere : DodgeEditorStrings.EmptyPatternSlot;
            emptyText.Alpha = HasPattern ? 0 : canSave ? 0.85f : 0.45f;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            animationStartTime = Time.Current;
        }

        protected override void Update()
        {
            base.Update();

            float phase = (float)(((Time.Current - animationStartTime) % 2200) / 2200);

            for (int i = 0; i < dots.Length; i++)
            {
                if (i >= markers.Count)
                {
                    dots[i].Alpha = 0;
                    continue;
                }

                DodgeSavedPatternMarker marker = markers[i];
                float progress = phase >= marker.Delay
                    ? (phase - marker.Delay) / (1 - marker.Delay)
                    : 0;
                dots[i].Position = marker.Start + (marker.End - marker.Start) * progress;
                dots[i].Alpha = phase >= marker.Delay || marker.Role != DodgeSavedPatternMarkerRole.Projectile ? 1 : 0.15f;
                dots[i].Size = new Vector2(marker.Role == DodgeSavedPatternMarkerRole.Emitter ? 7 : marker.Role == DodgeSavedPatternMarkerRole.Structure ? 3 : 4);
                dots[i].Colour = marker.Role switch
                {
                    DodgeSavedPatternMarkerRole.Emitter => Colour4.FromHex("#FFB45E"),
                    DodgeSavedPatternMarkerRole.Structure => Colour4.FromHex("#B78CFF"),
                    _ => Colour4.FromHex("#62D9FF"),
                };
            }

            background.Colour = IsHovered
                ? Colour4.FromHex("#343946")
                : selected ? Colour4.FromHex("#2E2A22")
                : HasPattern ? Colour4.FromHex("#20232B")
                : Colour4.FromHex("#1B1E25");
        }
    }
}
