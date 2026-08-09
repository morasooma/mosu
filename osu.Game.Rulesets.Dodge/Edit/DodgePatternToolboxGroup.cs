// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
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

        public bool CanRepeatSelection => repeatButton.Enabled.Value;
        public bool CanSavePrefab => savePrefabButton.Enabled.Value;
        public bool CanInsertPrefab => insertPrefabButton.Enabled.Value;

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
                createButton(DodgeEditorStrings.Ring, createRing),
                createButton(DodgeEditorStrings.Spiral, createSpiral),
                createButton(DodgeEditorStrings.Fan, createFan),
                createButton(DodgeEditorStrings.WallSafeGap, () => createWall(false)),
                createButton(DodgeEditorStrings.Sweep, () => createWall(true)),
                createButton(DodgeEditorStrings.Cross, createCross),
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
                updateActionAvailability();
            }, true);
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

        private double currentTime => Math.Min(beatSnapProvider.SnapTime(editorClock.CurrentTime), editorClock.TrackLength);

        private double snapStep(double time) => beatSnapProvider.GetBeatLengthAtTime(time) * timeStep.Value;

        private double defaultDuration(double time) => beatSnapProvider.GetBeatLengthAtTime(time) * 4;

        private void updateActionAvailability()
        {
            bool hasSelection = selectedHitObjects.OfType<DodgeHitObject>().Any();
            bool hasProjectileSelection = selectedHitObjects.Any(item => item is DodgeBullet or DodgeEmitter);

            repeatButton.Enabled.Value = hasSelection;
            savePrefabButton.Enabled.Value = hasProjectileSelection;
            insertPrefabButton.Enabled.Value = File.Exists(storage.GetFullPath(string.Format(quick_prefab_path, prefabSlot.Value)));
            selectionRequiredText.Alpha = hasSelection ? 0 : 0.6f;
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
                    DodgeHitObject copy = clone(original, step * generation, spatialOffset);

                    if (copy is DodgeBullet or DodgeEmitter)
                    {
                        var points = DodgeSelectionTransformUtils.GetPoints(copy);
                        Vector2 rotationOrigin = originalOrigin + spatialOffset;
                        DodgeSelectionTransformUtils.SetPoints(
                            copy,
                            GeometryUtils.RotatePointAroundOrigin(points.Start, rotationOrigin, rotationStep.Value * generation),
                            GeometryUtils.RotatePointAroundOrigin(points.End, rotationOrigin, rotationStep.Value * generation));
                    }

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
                Colour = settings.BulletColour.Value,
                OutlineColour = settings.BulletOutlineColour.Value,
                Opacity = settings.BulletOpacity.Value,
                OutlineThickness = settings.BulletOutlineThickness.Value,
            }));
        }

        private void saveQuickPrefab()
        {
            DodgeHitObject[] selected = editorBeatmap.SelectedHitObjects
                                                       .OfType<DodgeHitObject>()
                                                       .Where(item => item is DodgeBullet or DodgeEmitter)
                                                       .ToArray();

            if (selected.Length == 0)
                return;

            double firstTime = selected.Min(item => item.StartTime);
            Vector2 origin = DodgeSelectionTransformUtils.GetSurroundingQuad(selected).Centre;
            var prefabObjects = selected.Select(item => clone(item, -firstTime, -origin)).ToList();
            var prefab = new Beatmap<DodgeHitObject> { HitObjects = prefabObjects };

            string path = storage.GetFullPath(string.Format(quick_prefab_path, prefabSlot.Value), true);
            string? directory = Path.GetDirectoryName(path);

            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using FileStream stream = File.Create(path);
            DodgeBeatmapSerializer.Serialize(prefab, stream);
            updateActionAvailability();
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

            addAndSelect(objects.Select(item => clone(item, time, origin)));
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

        private static DodgeHitObject clone(DodgeHitObject source, double timeOffset, Vector2 positionOffset) => source switch
        {
            DodgeBullet bullet => new DodgeBullet
            {
                StartTime = bullet.StartTime + timeOffset,
                Duration = bullet.Duration,
                Position = bullet.Position + positionOffset,
                EndPosition = bullet.EndPosition + positionOffset,
                Shape = bullet.Shape,
                ContinueUntilExit = bullet.ContinueUntilExit,
                Colour = bullet.Colour,
                OutlineColour = bullet.OutlineColour,
                Opacity = bullet.Opacity,
                OutlineThickness = bullet.OutlineThickness,
            },
            DodgeEmitter emitter => new DodgeEmitter
            {
                StartTime = emitter.StartTime + timeOffset,
                Duration = emitter.Duration,
                Position = emitter.Position + positionOffset,
                AimPosition = emitter.AimPosition + positionOffset,
                MovementEndPosition = (emitter.MoveSource ? emitter.MovementEndPosition : emitter.Position) + positionOffset,
                MoveSource = emitter.MoveSource,
                BulletCount = emitter.BulletCount,
                SpreadAngle = emitter.SpreadAngle,
                BurstCount = emitter.BurstCount,
                BurstInterval = emitter.BurstInterval,
                BurstBeatDivisor = emitter.BurstBeatDivisor,
                Shape = emitter.Shape,
                ContinueUntilExit = emitter.ContinueUntilExit,
                Colour = emitter.Colour,
                OutlineColour = emitter.OutlineColour,
                Opacity = emitter.Opacity,
                OutlineThickness = emitter.OutlineThickness,
            },
            DodgeArenaChange arena => cloneArena(arena, timeOffset, positionOffset),
            DodgeCameraChange camera => new DodgeCameraChange
            {
                StartTime = camera.StartTime + timeOffset,
                Duration = camera.Duration,
                Position = camera.Position + positionOffset,
                EndPosition = camera.EndPosition + positionOffset,
                Continuous = camera.Continuous,
            },
            _ => throw new ArgumentException($"Unsupported Dodge object {source.GetType().Name}.", nameof(source)),
        };

        private static DodgeArenaChange cloneArena(DodgeArenaChange source, double timeOffset, Vector2 positionOffset)
        {
            var clone = new DodgeArenaChange
            {
                StartTime = source.StartTime + timeOffset,
                Duration = source.Duration,
                TargetPosition = source.TargetPosition + positionOffset,
                TargetSize = source.TargetSize,
            };

            clone.ClampToBaseBounds();
            return clone;
        }
    }
}
