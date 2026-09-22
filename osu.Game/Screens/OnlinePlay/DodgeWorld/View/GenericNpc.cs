// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.View
{
    /// <summary>
    /// Anything the player can walk up to and press E on: a character, or an object that happens to have
    /// something to say.
    /// </summary>
    /// <remarks>
    /// Both are the same thing to the world — something with a position, a conversation and a story flag —
    /// so they are one entity kind with a switch on it rather than two. A sign, a chest or a lever is a
    /// character that is not alive: no name over it, no shadow if the author says so, and a prompt that
    /// reads as an action rather than as a conversation.
    /// </remarks>
    internal partial class GenericNpc : InteractiveNpc, ITexturedEntity
    {
        private readonly OsuColour colours;
        private readonly Container interactionPrompt;
        private readonly OsuSpriteText promptText;
        private readonly OsuSpriteText nameText;
        private readonly Container body;
        private readonly Drawable shadow;
        private readonly Sprite portrait;
        private readonly EntityTexture texture = new EntityTexture();
        private SurfaceFillMode fillMode = SurfaceFillMode.Fit;
        private bool alive = true;
        private bool castsShadow = true;

        public string? TexturePath => texture.Path;
        public bool TextureSmoothing => texture.Smoothing;
        public float TextureOpacity => texture.Opacity;
        public bool TextureRepeats => fillMode == SurfaceFillMode.Tile;
        public int TextureFillModeIndex => (int)fillMode;
        public string? TextureFillModeName => fillMode.ToString();

        /// <summary>
        /// Whether this is a living character. False turns it into scenery: no name plate, and the prompt
        /// says what pressing E does rather than offering a conversation.
        /// </summary>
        public bool Alive
        {
            get => alive;
            set
            {
                alive = value;
                applyPresentation();
            }
        }

        /// <summary>Whether the object is drawn standing on a shadow.</summary>
        public bool CastsShadow
        {
            get => castsShadow;
            set
            {
                castsShadow = value;
                applyPresentation();
            }
        }

        private static readonly IReadOnlyDictionary<string, string[]> default_dialogue_pages = new Dictionary<string, string[]>
        {
            ["ru"] = new[] { "Привет. Я живу в этой комнате." },
            ["en"] = new[] { "Hey. I live in this room." },
        };

        /// <summary>
        /// The footprint the player cannot walk through, following the drawn size rather than being fixed:
        /// an author who made a wide object meant a wide object.
        /// </summary>
        public override Vector2 CollisionSize => new Vector2(Size.X * 0.34f, Size.Y * 0.22f);

        public override string LayoutKind => "npc";

        /// <summary>
        /// Resizable as well as scalable. Scale is kept because published worlds already store it; size is
        /// what an author reaches for when the image is not the shape of a person.
        /// </summary>
        public override bool CanResize => true;

        public GenericNpc(OsuColour colours, Func<bool> editing, Action<EditableWorldEntity> select, string npcName,
                          Func<string> languageProvider, string[]? dialogue = null, Dictionary<string, string[]>? localizedDialogue = null,
                          Dictionary<string, string[][]>? dialogueBranches = null)
            : base(editing, select, colours.Orange1, languageProvider, dialogue, localizedDialogue, dialogueBranches,
                default_dialogue_pages)
        {
            this.colours = colours;
            Size = new Vector2(170, 170);
            AddRangeInternal(new Drawable[]
            {
                shadow = new CircularContainer
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.Centre,
                    Y = -4,
                    Size = new Vector2(58, 15),
                    Scale = new Vector2(1, 0.55f),
                    Alpha = 0.35f,
                    Masking = true,
                    Child = new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.Black },
                },
                body = new CircularContainer
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Y = -10,
                    Size = new Vector2(62, 82),
                    Masking = true,
                    CornerRadius = 18,
                    BorderThickness = 3,
                    BorderColour = colours.Purple1,
                    Children = new Drawable[]
                    {
                        new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Purple4 },
                        new SpriteIcon
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Icon = FontAwesome.Solid.User,
                            Size = new Vector2(27),
                            Colour = colours.Purple0,
                        },
                    },
                },
                // Sized and placed as a fraction of the object, so resizing it resizes the image with it
                // and an NPC left at the default size is drawn exactly where it always was.
                portrait = new Sprite
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    RelativeSizeAxes = Axes.Both,
                    Size = new Vector2(160f / 170, 150f / 170),
                    RelativePositionAxes = Axes.Y,
                    Y = -6f / 170,
                    FillMode = FillMode.Fit,
                    Alpha = 0,
                },
                nameText = new OsuSpriteText
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.TopCentre,
                    Y = 2,
                    Text = npcName.ToUpperInvariant(),
                    Font = OsuFont.Default.With(size: 13, weight: FontWeight.Bold),
                },
                interactionPrompt = new Container
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.BottomCentre,
                    Y = 22,
                    Size = new Vector2(132, 38),
                    Alpha = 0,
                    Masking = true,
                    CornerRadius = 10,
                    BorderThickness = 2,
                    BorderColour = colours.Purple1,
                    Children = new Drawable[]
                    {
                        new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Gray1.Opacity(0.96f) },
                        promptText = new OsuSpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Font = OsuFont.Default.With(size: 17, weight: FontWeight.Bold),
                        },
                    },
                },
            });

            AddDialogueBubble(colours, colours.Purple2);
            applyPresentation();
        }

        /// <summary>
        /// Applies everything that follows from being alive or not: the name plate, the shadow, the
        /// placeholder body and what the prompt says.
        /// </summary>
        private void applyPresentation()
        {
            nameText.Alpha = alive ? 1 : 0;
            shadow.Alpha = castsShadow ? 0.35f : 0;

            // A character with no image gets the placeholder figure; an object gets a plain panel, because
            // a purple person standing in for a chest reads as a missing NPC rather than as a chest.
            body.Alpha = portrait.Texture != null ? 0 : 1;
            body.Size = alive ? new Vector2(62, 82) : new Vector2(96, 72);
            body.BorderColour = alive ? colours.Purple1 : colours.Gray4;

            promptText.Text = alive
                ? DialogueLanguage == "ru" ? "E   ДИАЛОГ" : "E   TALK"
                : DialogueLanguage == "ru" ? "E" : "E";
            interactionPrompt.Width = alive ? 132 : 56;
        }

        public void SetTexturePath(string? path)
        {
            texture.SetPath(path);

            if (texture.Path == null)
                SetTexture(null);
        }

        /// <summary>
        /// An image replaces the placeholder body outright rather than being drawn over it, so a
        /// character does not stand inside a purple capsule.
        /// </summary>
        public void SetTexture(Texture? value)
        {
            texture.SetResolved(value);
            portrait.Texture = value;
            portrait.Alpha = value == null ? 0 : texture.Opacity;
            applyFillMode();
            applyPresentation();
        }

        public void SetTextureOpacity(float value)
        {
            texture.SetOpacity(value);

            if (portrait.Texture != null)
                portrait.Alpha = texture.Opacity;
        }

        public void ToggleTextureSmoothing() => texture.ToggleSmoothing();

        /// <summary>
        /// Steps to the next way of covering the object. Fit is the character default — an artist's
        /// resolution should decide sharpness, not size — and the rest are for objects that are not people.
        /// </summary>
        public void CycleTextureFill()
        {
            fillMode = (SurfaceFillMode)(((int)fillMode + 1) % 4);
            applyFillMode();
        }

        public void SetTextureFill(int mode)
        {
            fillMode = (SurfaceFillMode)Math.Clamp(mode, 0, (int)SurfaceFillMode.Tile);
            applyFillMode();
        }

        /// <summary>
        /// Tiling reads a texture region larger than the texture itself, which with a repeating wrap mode
        /// starts the image over at every crossing. Kept identical to a surface's, so one image behaves the
        /// same wherever it is used.
        /// </summary>
        private void applyFillMode()
        {
            if (TextureRepeats)
            {
                portrait.FillMode = FillMode.Stretch;
                portrait.TextureRelativeSizeAxes = Axes.None;
                portrait.TextureRectangle = new RectangleF(0, 0, portrait.DrawWidth, portrait.DrawHeight);
                return;
            }

            portrait.FillMode = (FillMode)(int)fillMode;
            portrait.TextureRelativeSizeAxes = Axes.Both;
            portrait.TextureRectangle = new RectangleF(0, 0, 1, 1);
        }

        protected override void Update()
        {
            base.Update();

            // The repeat count follows the drawn size, so it has to be refreshed while the object is
            // being resized rather than only when the fill mode changes.
            if (TextureRepeats)
                portrait.TextureRectangle = new RectangleF(0, 0, portrait.DrawWidth, portrait.DrawHeight);
        }

        /// <summary>
        /// Applies the stored image configuration, before the library has resolved the image itself.
        /// </summary>
        public void ReadTexture(string? path, float? opacity, bool? smoothing) => texture.Read(path, opacity, smoothing);

        public override void SetInteractionAvailable(bool available)
        {
            if (IsDialogueOpen)
                available = false;
            interactionPrompt.FadeTo(available ? 1 : 0, 140, Easing.OutQuint);
        }

        public override void SetDisplayName(string value)
        {
            base.SetDisplayName(value);
            nameText.Text = value.ToUpperInvariant();
        }

        public override void RestoreEditorDefaults()
        {
            base.RestoreEditorDefaults();
            Size = new Vector2(170, 170);
            fillMode = SurfaceFillMode.Fit;
            alive = true;
            castsShadow = true;
            applyFillMode();
            applyPresentation();
        }
    }
}
