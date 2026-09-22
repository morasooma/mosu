// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Entities;
using osu.Game.Screens.OnlinePlay.DodgeWorld.View;
using osuTK;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.Editor
{
    /// <summary>
    /// Shows the editable properties of the selected entity, and nothing else.
    /// </summary>
    /// <remarks>
    /// The field list comes from the entity's <see cref="IWorldEntityKind"/>, so the panel only ever
    /// offers properties that the selection actually has. It used to show every property of every
    /// kind at once, as an unlabelled column of text boxes.
    /// </remarks>
    internal partial class EntityInspector : CompositeDrawable
    {
        private readonly WorldEntityRegistry registry;
        private readonly OsuColour colours;
        private readonly Action onApplied;

        private readonly OsuTextFlowContainer header;
        private readonly OsuTextFlowContainer emptyHint;
        private readonly FillFlowContainer fieldFlow;

        private readonly List<(EntityField Field, IEditorFieldControl Control)> controls = new List<(EntityField, IEditorFieldControl)>();

        private EditableWorldEntity? target;

        public EntityInspector(WorldEntityRegistry registry, OsuColour colours, Action onApplied)
        {
            this.registry = registry;
            this.colours = colours;
            this.onApplied = onApplied;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 10),
                Children = new Drawable[]
                {
                    header = WrappedText.Paragraph("ОБЪЕКТ НЕ ВЫБРАН", colours.Orange1, 16, FontWeight.Bold),
                    emptyHint = WrappedText.Paragraph("Щёлкни объект на карте, чтобы увидеть его свойства.", colours.GrayA),
                    fieldFlow = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 12),
                    },
                },
            };
        }

        /// <summary>
        /// Rebuilds the panel for a new selection.
        /// </summary>
        public void SetTarget(EditableWorldEntity? entity)
        {
            target = entity;

            controls.Clear();
            fieldFlow.Clear();

            if (entity == null)
            {
                header.Text = "ОБЪЕКТ НЕ ВЫБРАН";
                emptyHint.Alpha = 1;
                return;
            }

            emptyHint.Alpha = 0;
            header.Text = $"{entity.DisplayName.ToUpperInvariant()}  •  {describeKind(entity)}";

            // Present on every kind, so it is offered here rather than declared by each one.
            addField(new EntityField("Название", "как объект называется в редакторе и в сохранении",
                e => e.DisplayName,
                (e, text) =>
                {
                    if (!string.IsNullOrWhiteSpace(text))
                        e.SetDisplayName(text.Trim());
                }));

            IWorldEntityKind? kind = registry.Find(entity.LayoutKind);

            if (kind == null)
                return;

            foreach (EntityField field in kind.Fields)
                addField(field);
        }

        private void addField(EntityField field)
        {
            string value = field.Read(target!);

            IEditorFieldControl control = field.Toggle
                ? new LabelledToggleField(field.Label, field.Hint, value, colours, Apply)
                : new LabelledEditorField(field.Label, field.Hint, value, colours, Apply);

            controls.Add((field, control));
            fieldFlow.Add((Drawable)control);
        }

        /// <summary>
        /// Writes every field back onto the entity, then re-reads them so clamped values are visible.
        /// </summary>
        public void Apply()
        {
            if (target == null)
                return;

            // Snapshot before writing, so the undo step holds the values as they were.
            target.EditBegan?.Invoke();

            foreach ((EntityField field, IEditorFieldControl control) in controls)
                field.Write(target, control.Value);

            EditableWorldEntity applied = target;
            onApplied();

            // Re-read so a value the field clamped is shown as stored rather than as typed. Deferred
            // because Apply runs from a text box's commit event, and rebuilding the fields here would
            // dispose that text box while it is still handling it.
            Schedule(() =>
            {
                if (ReferenceEquals(target, applied))
                    SetTarget(applied);
            });
        }

        private string describeKind(EditableWorldEntity entity) => entity.LayoutKind.ToUpperInvariant();

        /// <summary>
        /// Captions currently on show, in order.
        /// </summary>
        internal IReadOnlyList<string> FieldLabels => controls.Select(entry => entry.Field.Label).ToArray();

        /// <summary>The control showing one property, for tests to read what it is and to work it.</summary>
        internal IEditorFieldControl? ControlForTesting(string label) =>
            controls.FirstOrDefault(entry => entry.Field.Label == label).Control;
    }
}
