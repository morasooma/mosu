// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.View
{
    internal partial class MoraNpc : InteractiveNpc, ITexturedEntity
    {
        private static readonly IReadOnlyDictionary<string, string[]> default_dialogue_pages = new Dictionary<string, string[]>
        {
            ["ru"] = new[]
            {
                "Добро пожаловать на площадь Моры. Отсюда начинаются все дороги Dodge World.",
                "Портал ведёт к совместным испытаниям Dodge, а боковые проходы связывают районы мира.",
                "Загляни к доске заданий и в магазин аксессуаров, когда будешь готов.",
            },
            ["en"] = new[]
            {
                "Welcome to Mora Plaza. Every road in Dodge World begins here.",
                "The portal leads to cooperative Dodge challenges, while the side passages connect world districts.",
                "Visit the quest board and accessory shop when you are ready.",
            },
        };

        private readonly Container floatingSprite;
        private readonly Sprite portrait;
        private readonly Texture? bundledPortrait;
        private readonly EntityTexture texture = new EntityTexture();
        private readonly Container interactionPrompt;
        private readonly OsuSpriteText nameText;

        public override Vector2 CollisionSize => new Vector2(78, 42);
        public override string LayoutKind => "mora";

        public string? TexturePath => texture.Path;
        public bool TextureSmoothing => texture.Smoothing;
        public float TextureOpacity => texture.Opacity;
        public bool TextureRepeats => false;

        /// <summary>Mora is always fitted whole into her box, so there is no choice to offer.</summary>
        public string? TextureFillModeName => null;

        public int TextureFillModeIndex => 0;

        public void CycleTextureFill()
        {
        }

        public MoraNpc(LargeTextureStore textures, OsuColour colours, Func<bool> editing, Action<EditableWorldEntity> select,
                       Func<string> languageProvider, string[]? dialogue = null, Dictionary<string, string[]>? localizedDialogue = null,
                       Dictionary<string, string[][]>? dialogueBranches = null)
            : base(editing, select, colours.Orange1, languageProvider, dialogue, localizedDialogue, dialogueBranches,
                default_dialogue_pages)
        {
            bundledPortrait = textures.Get("DodgeWorld/mora-chibi");
            Size = new Vector2(180, 220);
            AddRangeInternal(new Drawable[]
            {
                new CircularContainer
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.Centre,
                    Y = -5,
                    Size = new Vector2(92, 22),
                    Scale = new Vector2(1, 0.55f),
                    Alpha = 0.4f,
                    Masking = true,
                    Child = new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.Black },
                },
                floatingSprite = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = portrait = new Sprite
                    {
                        Anchor = Anchor.BottomCentre,
                        Origin = Anchor.BottomCentre,
                        Y = -10,
                        Texture = bundledPortrait,
                        Size = new Vector2(158, 188),
                        FillMode = FillMode.Fit,
                    },
                },
                nameText = new OsuSpriteText
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.TopCentre,
                    Y = 3,
                    Text = "MORA",
                    Font = OsuFont.Default.With(size: 14, weight: FontWeight.Bold),
                    Colour = colours.Pink0,
                    Shadow = true,
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
                    BorderColour = colours.Pink1,
                    Children = new Drawable[]
                    {
                        new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Gray1.Opacity(0.95f) },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Text = DialogueLanguage == "ru" ? "E   ДИАЛОГ" : "E   TALK",
                            Font = OsuFont.Default.With(size: 17, weight: FontWeight.Bold),
                        },
                    },
                },
            });

            AddDialogueBubble(colours, colours.Pink2);
        }

        protected override void Update()
        {
            base.Update();
            floatingSprite.Y = (float)Math.Sin(Time.Current / 3200 * Math.PI * 2) * 5;
        }

        public void SetTexturePath(string? path)
        {
            texture.SetPath(path);

            if (texture.Path == null)
                SetTexture(null);
        }

        /// <summary>
        /// Dresses Mora in an authored image, falling back to the one shipped with the client.
        /// </summary>
        public void SetTexture(Texture? value)
        {
            texture.SetResolved(value);
            portrait.Texture = value ?? bundledPortrait;
            portrait.Alpha = value == null ? 1 : texture.Opacity;
        }

        public void SetTextureOpacity(float value)
        {
            texture.SetOpacity(value);

            if (texture.Resolved != null)
                portrait.Alpha = texture.Opacity;
        }

        public void ToggleTextureSmoothing() => texture.ToggleSmoothing();

        public void ReadTexture(string? path, float? opacity, bool? smoothing) => texture.Read(path, opacity, smoothing);

        public override void SetInteractionAvailable(bool available)
        {
            if (IsDialogueOpen)
                available = false;
            interactionPrompt.FadeTo(available ? 1 : 0, 160, Easing.OutQuint);
        }

        public override void SetDisplayName(string value)
        {
            base.SetDisplayName(value);
            nameText.Text = value.ToUpperInvariant();
        }
    }
}
