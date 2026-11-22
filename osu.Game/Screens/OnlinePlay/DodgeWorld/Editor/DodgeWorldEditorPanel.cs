// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Entities;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Model;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Textures;
using osu.Game.Screens.OnlinePlay.DodgeWorld.View;
using osuTK;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.Editor
{
    /// <summary>
    /// The operations the editor panel asks the screen to carry out.
    /// </summary>
    internal interface IDodgeWorldEditorHost
    {
        void ApplyRoomProperties(string name, string width, string height);
        void SetSpawnAtPlayer();

        void AddSurface();
        void AddCollisionZone();
        void AddNpc();
        void AddMobSpawnZone();
        void AddEmitter();
        void AddBeam();
        void AddWarp();
        void AddPortal();
        void AddSidePassage();
        void AddTerminal();

        /// <summary>Adds a kiosk already set up as a shop, rather than one to be switched over by hand.</summary>
        void AddShop();

        void CreateConnectedRoom();

        /// <summary>Asks which room the selected passage leads to, on the world map.</summary>
        void LinkSelectedPassage();

        /// <summary>Points the selected passage back at the room the author came from.</summary>
        void LinkSelectedPassageToPreviousRoom();

        /// <summary>
        /// Says that the middle of the view is where the player should come out, and asks which room they
        /// will be arriving from.
        /// </summary>
        void SetArrivalHere();

        void OpenLinkedRoom();
        void ReturnToRootRoom();
        void ToggleWorldMap();

        /// <summary>Makes the current room the one the world starts in, and the one death returns to.</summary>
        void MakeThisRoomInitial();

        /// <summary>Removes the current room and unpoints the passages that led to it. Asks twice.</summary>
        void DeleteThisRoom();

        void PublishWorld();
        void ResetRoom();
        void CloseEditor();

        /// <summary>Restores the room to before the last change.</summary>
        void Undo();

        void AdjustSpawnCount(int delta);
        void CycleSelectedStyle();
        void ToggleSelectedShape();
        void DeleteSelected();

        /// <summary>Copies the selection, so it can be put down again here or in another room.</summary>
        void CopySelected();

        void PasteCopied();

        /// <summary>Copies the selection next to itself, for laying out a row of the same thing.</summary>
        void DuplicateSelected();

        /// <summary>Draws the selection over the objects it shares a depth with.</summary>
        void BringSelectionToFront();

        void SendSelectionToBack();

        /// <summary>Called after the inspector writes new values onto an entity.</summary>
        void OnSelectionEdited();

        void ImportTexture();
        void ApplySelectedTextureToSurface();
        void ApplySelectedTextureToWeapon();
        void ClearSurfaceTexture();
        void CycleTextureFill();
        void ToggleTextureSmoothing();
        void SelectTextureAsset(string assetId);

        void SetSelectedCornerRadius(float value);
        void SetSelectedTextureOpacity(float value);

        /// <summary>Sets what one swing of the world's sword takes off a mob.</summary>
        void SetSwordDamage(int damage);
    }

    /// <summary>
    /// The world editor's side panel.
    /// </summary>
    internal partial class DodgeWorldEditorPanel : CompositeDrawable
    {
        /// <summary>
        /// Panel width. The screen insets the world by this while the editor is open, so that the
        /// right-hand edge of an object — and its resize handles — are not left underneath the panel.
        /// </summary>
        public const float PANEL_WIDTH = 420;

        private enum Section
        {
            Room,
            Object,
            Dialogue,
            Material,
            World,
        }

        public EntityInspector Inspector { get; }

        public DialogueEditor Dialogue { get; }

        /// <summary>
        /// The corner rounding of the selected surface. Named apart from the panel's own inherited
        /// <c>CornerRadius</c>, which has nothing to do with what is being edited.
        /// </summary>
        public BindableFloat SurfaceCornerRadius { get; } = new BindableFloat { MinValue = 0, MaxValue = 240 };

        public BindableFloat TextureOpacity { get; } = new BindableFloat(1) { MinValue = 0, MaxValue = 1 };

        /// <summary>
        /// What one swing takes off a mob. A property of the world's sword rather than of any selection,
        /// which is why it sits with the weapon instead of in the object inspector.
        /// </summary>
        public BindableInt SwordDamage { get; } = new BindableInt(WeaponSkin.DEFAULT_DAMAGE)
        {
            MinValue = WeaponSkin.MIN_DAMAGE,
            // Not the stored maximum: a slider that reaches a thousand cannot be set to seven. Numbers
            // past this belong to a world that is being balanced through the API.
            MaxValue = 50,
        };

        private readonly OsuColour colours;
        private readonly IDodgeWorldEditorHost host;
        private readonly DodgeWorldTextureLibrary library;

        private readonly Dictionary<Section, Drawable> sections = new Dictionary<Section, Drawable>();
        private readonly Dictionary<Section, RoundedButton> tabs = new Dictionary<Section, RoundedButton>();

        private readonly OsuTextFlowContainer status;
        private readonly OsuSpriteText title;
        private readonly OsuTextFlowContainer textureSelection;
        private readonly FillFlowContainer textureCards;
        private readonly RoundedButton fillButton;
        private readonly RoundedButton smoothingButton;

        private readonly LabelledEditorField roomName;
        private readonly LabelledEditorField roomWidth;
        private readonly LabelledEditorField roomHeight;

        public DodgeWorldEditorPanel(OsuColour colours, WorldEntityRegistry registry, DodgeWorldTextureLibrary library,
                                     IDodgeWorldEditorHost host)
        {
            this.colours = colours;
            this.host = host;
            this.library = library;

            Inspector = new EntityInspector(registry, colours, host.OnSelectionEdited);
            Dialogue = new DialogueEditor(colours, SetStatus);

            RelativeSizeAxes = Axes.Y;
            Width = PANEL_WIDTH;
            Anchor = Anchor.TopRight;
            Origin = Anchor.TopRight;
            Alpha = 0;

            sections[Section.Room] = section(
                heading(global::osu.Game.Localisation.ClientTranslationStrings.J4536, colours.Pink1, 15),
                roomName = new LabelledEditorField(global::osu.Game.Localisation.ClientTranslationStrings.J4537, global::osu.Game.Localisation.ClientTranslationStrings.J4538, string.Empty, colours, applyRoom),
                roomWidth = new LabelledEditorField(global::osu.Game.Localisation.ClientTranslationStrings.J4539, global::osu.Game.Localisation.ClientTranslationStrings.J4540, string.Empty, colours, applyRoom),
                roomHeight = new LabelledEditorField(global::osu.Game.Localisation.ClientTranslationStrings.J4541, global::osu.Game.Localisation.ClientTranslationStrings.J4542, string.Empty, colours, applyRoom),
                button(global::osu.Game.Localisation.ClientTranslationStrings.J4543, applyRoom),
                button(global::osu.Game.Localisation.ClientTranslationStrings.J4544, host.SetSpawnAtPlayer));

            sections[Section.Object] = section(
                Inspector,
                button(global::osu.Game.Localisation.ClientTranslationStrings.J4545, Inspector.Apply),
                button(global::osu.Game.Localisation.ClientTranslationStrings.J4546, () => host.AdjustSpawnCount(1)),
                button(global::osu.Game.Localisation.ClientTranslationStrings.J4547, () => host.AdjustSpawnCount(-1)),
                button(global::osu.Game.Localisation.ClientTranslationStrings.J4548, host.CycleSelectedStyle),
                button(global::osu.Game.Localisation.ClientTranslationStrings.J4549, host.ToggleSelectedShape),
                heading(global::osu.Game.Localisation.ClientTranslationStrings.J4550, colours.Pink1, 15),
                heading(global::osu.Game.Localisation.ClientTranslationStrings.J4551, colours.GrayA),
                button(global::osu.Game.Localisation.ClientTranslationStrings.J4552, host.DuplicateSelected),
                button(global::osu.Game.Localisation.ClientTranslationStrings.J4553, host.CopySelected),
                button(global::osu.Game.Localisation.ClientTranslationStrings.J4554, host.PasteCopied),
                button(global::osu.Game.Localisation.ClientTranslationStrings.J4555, host.BringSelectionToFront),
                button(global::osu.Game.Localisation.ClientTranslationStrings.J4556, host.SendSelectionToBack),
                button(global::osu.Game.Localisation.ClientTranslationStrings.J4557, host.DeleteSelected),
                heading(global::osu.Game.Localisation.ClientTranslationStrings.J4558, colours.Pink1, 15),
                heading(global::osu.Game.Localisation.ClientTranslationStrings.J4559, colours.GrayA),
                heading(global::osu.Game.Localisation.ClientTranslationStrings.J4560, colours.GrayA),
                heading(global::osu.Game.Localisation.ClientTranslationStrings.J4561, colours.GrayA),
                heading(global::osu.Game.Localisation.ClientTranslationStrings.J4562, colours.GrayA),
                heading(global::osu.Game.Localisation.ClientTranslationStrings.J4563,
                    colours.GrayA),
                heading(global::osu.Game.Localisation.ClientTranslationStrings.J4564, colours.GrayA));

            sections[Section.Dialogue] = section(Dialogue);

            sections[Section.Material] = section(
                heading(global::osu.Game.Localisation.ClientTranslationStrings.J4565, colours.Pink1, 15),
                heading(global::osu.Game.Localisation.ClientTranslationStrings.J4566,
                    colours.GrayA),
                heading(global::osu.Game.Localisation.ClientTranslationStrings.J4567, colours.GrayA),
                new RoundedSliderBar<float>
                {
                    RelativeSizeAxes = Axes.X,
                    Current = SurfaceCornerRadius,
                    AccentColour = colours.Pink1,
                },
                heading(global::osu.Game.Localisation.ClientTranslationStrings.J4568, colours.GrayA),
                new RoundedSliderBar<float>
                {
                    RelativeSizeAxes = Axes.X,
                    Current = TextureOpacity,
                    DisplayAsPercentage = true,
                    AccentColour = colours.Pink1,
                },
                fillButton = button(global::osu.Game.Localisation.ClientTranslationStrings.J4569, host.CycleTextureFill),
                smoothingButton = button(global::osu.Game.Localisation.ClientTranslationStrings.J4570, host.ToggleTextureSmoothing),
                heading(global::osu.Game.Localisation.ClientTranslationStrings.J4571, colours.Pink1, 15),
                textureSelection = heading(global::osu.Game.Localisation.ClientTranslationStrings.J4572, colours.GrayA),
                new OsuScrollContainer(Direction.Horizontal)
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 112,
                    Child = textureCards = new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(8),
                    },
                },
                button(global::osu.Game.Localisation.ClientTranslationStrings.J4573, host.ImportTexture),
                button(global::osu.Game.Localisation.ClientTranslationStrings.J4574, host.ApplySelectedTextureToSurface),
                button(global::osu.Game.Localisation.ClientTranslationStrings.J4575, host.ApplySelectedTextureToWeapon),
                heading(global::osu.Game.Localisation.ClientTranslationStrings.J4576, colours.GrayA),
                heading(global::osu.Game.Localisation.ClientTranslationStrings.J4577, colours.GrayA),
                new RoundedSliderBar<int>
                {
                    RelativeSizeAxes = Axes.X,
                    Current = SwordDamage,
                    AccentColour = colours.Pink1,
                },
                button(global::osu.Game.Localisation.ClientTranslationStrings.J4578, host.ClearSurfaceTexture));

            sections[Section.World] = section(
                heading(global::osu.Game.Localisation.ClientTranslationStrings.J4579, colours.Pink1, 15),
                button(global::osu.Game.Localisation.ClientTranslationStrings.J4580, host.AddSurface),
                button(global::osu.Game.Localisation.ClientTranslationStrings.J4581, host.AddCollisionZone),
                button(global::osu.Game.Localisation.ClientTranslationStrings.J4582, host.AddNpc),
                heading(global::osu.Game.Localisation.ClientTranslationStrings.J4583, colours.GrayA),
                button(global::osu.Game.Localisation.ClientTranslationStrings.J4584, host.AddTerminal),
                button(global::osu.Game.Localisation.ClientTranslationStrings.J4585, host.AddShop),
                button(global::osu.Game.Localisation.ClientTranslationStrings.J4586, host.AddMobSpawnZone),
                heading(global::osu.Game.Localisation.ClientTranslationStrings.J4587, colours.Pink1, 15),
                heading(global::osu.Game.Localisation.ClientTranslationStrings.J4588, colours.GrayA),
                button(global::osu.Game.Localisation.ClientTranslationStrings.J4589, host.AddEmitter),
                button(global::osu.Game.Localisation.ClientTranslationStrings.J4590, host.AddBeam),
                heading(global::osu.Game.Localisation.ClientTranslationStrings.J4591, colours.Pink1, 15),
                heading(global::osu.Game.Localisation.ClientTranslationStrings.J4592, colours.GrayA),
                heading(global::osu.Game.Localisation.ClientTranslationStrings.J4593, colours.GrayA),
                button(global::osu.Game.Localisation.ClientTranslationStrings.J4594, host.AddPortal),
                button(global::osu.Game.Localisation.ClientTranslationStrings.J4595, host.AddSidePassage),
                button(global::osu.Game.Localisation.ClientTranslationStrings.J4596, host.AddWarp),
                button(global::osu.Game.Localisation.ClientTranslationStrings.J4597, host.LinkSelectedPassage),
                button(global::osu.Game.Localisation.ClientTranslationStrings.J4598, host.LinkSelectedPassageToPreviousRoom),
                button(global::osu.Game.Localisation.ClientTranslationStrings.J4599, host.SetArrivalHere),
                button(global::osu.Game.Localisation.ClientTranslationStrings.J4600, host.CreateConnectedRoom),
                button(global::osu.Game.Localisation.ClientTranslationStrings.J4601, host.OpenLinkedRoom),
                button(global::osu.Game.Localisation.ClientTranslationStrings.J4602, host.ReturnToRootRoom),
                button(global::osu.Game.Localisation.ClientTranslationStrings.J4603, host.ToggleWorldMap),
                heading(global::osu.Game.Localisation.ClientTranslationStrings.J4604, colours.Pink1, 15),
                heading(global::osu.Game.Localisation.ClientTranslationStrings.J4605, colours.GrayA),
                button(global::osu.Game.Localisation.ClientTranslationStrings.J4606, host.MakeThisRoomInitial),
                button(global::osu.Game.Localisation.ClientTranslationStrings.J4607, host.DeleteThisRoom),
                button(global::osu.Game.Localisation.ClientTranslationStrings.J4608, host.Undo),
                button(global::osu.Game.Localisation.ClientTranslationStrings.J4609, host.PublishWorld),
                button(global::osu.Game.Localisation.ClientTranslationStrings.J4610, host.ResetRoom),
                button(global::osu.Game.Localisation.ClientTranslationStrings.J4611, host.CloseEditor));

            var tabFlow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(5),
            };

            addTab(tabFlow, Section.Room, global::osu.Game.Localisation.ClientTranslationStrings.J4612);
            addTab(tabFlow, Section.Object, global::osu.Game.Localisation.ClientTranslationStrings.J4613);
            addTab(tabFlow, Section.Dialogue, global::osu.Game.Localisation.ClientTranslationStrings.J4614);
            addTab(tabFlow, Section.Material, global::osu.Game.Localisation.ClientTranslationStrings.J4615);
            addTab(tabFlow, Section.World, global::osu.Game.Localisation.ClientTranslationStrings.J4616);

            InternalChildren = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Gray1.Opacity(0.98f) },
                new OsuScrollContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    // Clears the game's toolbar, which overlays the top of the screen.
                    Padding = new MarginPadding { Top = 62, Bottom = 20, Horizontal = 18 },
                    Child = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 10),
                        Children = new Drawable[]
                        {
                            title = new OsuSpriteText
                            {
                                Text = global::osu.Game.Localisation.ClientTranslationStrings.J4617,
                                Font = OsuFont.Default.With(size: 24, weight: FontWeight.Bold),
                                Colour = colours.Pink1,
                            },
                            WrappedText.Paragraph(
                                global::osu.Game.Localisation.ClientTranslationStrings.J4618, colours.GrayA),
                            tabFlow,
                            new Box { RelativeSizeAxes = Axes.X, Height = 2, Colour = colours.Gray3 },
                            new Container
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Children = sections.Values.ToArray(),
                            },
                            status = WrappedText.Paragraph(global::osu.Game.Localisation.ClientTranslationStrings.J4619, colours.Gray9, 13),
                        },
                    },
                },
            };

            SurfaceCornerRadius.BindValueChanged(value => host.SetSelectedCornerRadius(value.NewValue));
            TextureOpacity.BindValueChanged(value => host.SetSelectedTextureOpacity(value.NewValue));
            SwordDamage.BindValueChanged(value =>
            {
                // Showing the world's own number must not read as an edit of it: a world that carries no
                // sword of its own would be given one just by being opened.
                if (!showingSwordDamage)
                    host.SetSwordDamage(value.NewValue);
            });

            library.Changed += RebuildTextureBrowser;
            RebuildTextureBrowser();

            showSection(Section.Room);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            library.Changed -= RebuildTextureBrowser;
        }

        public void SetStatus(LocalisableString text) => status.Text = text;

        private bool showingSwordDamage;

        /// <summary>
        /// Shows the world's sword damage without reporting it as an edit.
        /// </summary>
        public void ShowSwordDamage(int damage)
        {
            showingSwordDamage = true;
            SwordDamage.Value = damage;
            showingSwordDamage = false;
        }

        public void SetRoom(string name, Vector2 size)
        {
            roomName.SetValue(name);
            roomWidth.SetValue(EditorValue.Format(size.X));
            roomHeight.SetValue(EditorValue.Format(size.Y));
        }

        public void SetZoomDisplay(float zoom) => title.Text = global::osu.Game.Localisation.ClientTranslationStrings.FormatJ4620(zoom);

        /// <summary>
        /// Points the inspector and the dialogue editor at a new selection.
        /// </summary>
        public void SetSelection(EditableWorldEntity? entity)
        {
            Inspector.SetTarget(entity);
            Dialogue.SetTarget(entity as InteractiveNpc);

            if (entity is RoomSurface surface)
                SurfaceCornerRadius.Value = surface.CornerRadiusValue;

            if (entity is ITexturedEntity textured)
            {
                TextureOpacity.Value = textured.TextureOpacity;
                SetMaterialLabels(textured);
            }
        }

        public void SetMaterialLabels(ITexturedEntity? entity)
        {
            // A character has no fill choice, so the button reports why rather than showing a value.
            fillButton.Text = global::osu.Game.Localisation.ClientTranslationStrings.FormatJ4621(entity?.TextureFillModeName ?? "—");
            smoothingButton.Text = global::osu.Game.Localisation.ClientTranslationStrings.FormatJ4622((entity == null ? "—" : entity.TextureSmoothing ? "Linear" : "Nearest"));
        }

        public void RebuildTextureBrowser()
        {
            textureCards.Clear();

            foreach (TextureLibraryEntry entry in library.Entries.Values
                                                         .OrderByDescending(entry => entry.BuiltIn)
                                                         .ThenBy(entry => entry.Name))
            {
                var card = new TextureLibraryCard(entry.Id, entry.Name, colours, () => host.SelectTextureAsset(entry.Id))
                {
                    Selected = entry.Id == library.SelectedAssetId,
                };

                textureCards.Add(card);
                library.LoadPreview(card, entry);
            }

            textureSelection.Text = library.Selected == null ? global::osu.Game.Localisation.ClientTranslationStrings.J4572 : global::osu.Game.Localisation.ClientTranslationStrings.FormatJ4623(library.Selected.Name);
        }

        private void applyRoom() => host.ApplyRoomProperties(roomName.Value, roomWidth.Value, roomHeight.Value);

        private void addTab(FillFlowContainer flow, Section target, LocalisableString text)
        {
            var tab = new RoundedButton
            {
                Width = 78,
                Height = 28,
                Text = text,
                Action = () => showSection(target),
            };

            tabs[target] = tab;
            flow.Add(tab);
        }

        private void showSection(Section target)
        {
            foreach ((Section key, Drawable drawable) in sections)
                drawable.Alpha = key == target ? 1 : 0;

            foreach ((Section key, RoundedButton tab) in tabs)
                tab.Alpha = key == target ? 1 : 0.6f;
        }

        private FillFlowContainer section(params Drawable[] children) => new FillFlowContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Vertical,
            Spacing = new Vector2(0, 8),
            Children = children,
        };

        private static OsuTextFlowContainer heading(LocalisableString text, ColourInfo colour, float size = 12) =>
            WrappedText.Paragraph(text, colour, size, FontWeight.Bold);

        private RoundedButton button(LocalisableString text, Action action) => new RoundedButton
        {
            RelativeSizeAxes = Axes.X,
            Height = 32,
            Text = text,
            Action = action,
        };
    }
}
