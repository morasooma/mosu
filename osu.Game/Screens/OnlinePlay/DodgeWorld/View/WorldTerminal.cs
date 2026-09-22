// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.View
{
    /// <summary>
    /// What one kind of kiosk looks like: the icon, the colour, and the line under its name.
    /// </summary>
    internal readonly record struct TerminalVariant(IconUsage Icon, string Subtitle, Func<OsuColour, Color4> Accent);

    internal partial class WorldTerminal : EditableWorldEntity
    {
        private readonly OsuColour colours;
        private readonly OsuSpriteText titleText;
        private readonly OsuSpriteText subtitleText;
        private readonly SpriteIcon icon;
        private readonly Box accentStripe;

        private int styleIndex;

        public override Vector2 CollisionSize => new Vector2(176, 76);
        public override string LayoutKind => "terminal";

        /// <summary>
        /// Which kiosk this is. Stored in the record's <c>style</c>, like any other appearance choice, so
        /// it no longer has to be guessed from the entity's id.
        /// </summary>
        public override bool CanStyle => true;

        public override int StyleIndex => styleIndex;

        public WorldTerminal(OsuColour colours, Func<bool> editing, Action<EditableWorldEntity> select, int style)
            : base(editing, select, colours.Orange1)
        {
            this.colours = colours;
            styleIndex = TerminalVariants.Clamp(style);

            Size = new Vector2(210, 142);
            AddRangeInternal(new Drawable[]
            {
                new CircularContainer
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.Centre,
                    Y = -2,
                    Size = new Vector2(160, 23),
                    Scale = new Vector2(1, 0.55f),
                    Alpha = 0.35f,
                    Masking = true,
                    Child = new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.Black },
                },
                new Container
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Y = -10,
                    Size = new Vector2(190, 98),
                    Masking = true,
                    CornerRadius = 14,
                    BorderThickness = 3,
                    BorderColour = colours.Gray4,
                    Children = new Drawable[]
                    {
                        new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Gray1 },
                        accentStripe = new Box
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 5,
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                        },
                        icon = new SpriteIcon
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            X = 20,
                            Size = new Vector2(34),
                        },
                        new FillFlowContainer
                        {
                            AutoSizeAxes = Axes.Both,
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            X = 69,
                            Direction = FillDirection.Vertical,
                            Children = new Drawable[]
                            {
                                titleText = new OsuSpriteText
                                {
                                    Font = OsuFont.Default.With(size: 15, weight: FontWeight.Bold),
                                },
                                subtitleText = new OsuSpriteText
                                {
                                    Font = OsuFont.Default.With(size: 12),
                                    Colour = colours.GrayA,
                                },
                            },
                        },
                    },
                },
            });

            applyVariant();
        }

        /// <summary>
        /// Steps to the next kind of kiosk. The author's way of saying which one they meant, in place of
        /// naming the object something the code recognised.
        /// </summary>
        public override void CycleStyle()
        {
            styleIndex = (styleIndex + 1) % TerminalVariants.COUNT;
            applyVariant();
        }

        public override void ApplySavedEditorState(float? width, float? height, int? style, bool? rounded, float? scale)
        {
            base.ApplySavedEditorState(width, height, style, rounded, scale);

            if (style.HasValue)
            {
                styleIndex = TerminalVariants.Clamp(style.Value);
                applyVariant();
            }
        }

        private void applyVariant()
        {
            TerminalVariant variant = TerminalVariants.Of(styleIndex);
            Color4 accent = variant.Accent(colours);

            icon.Icon = variant.Icon;
            icon.Colour = accent;
            accentStripe.Colour = accent;
            subtitleText.Text = variant.Subtitle;
        }

        public override void SetDisplayName(string value)
        {
            base.SetDisplayName(value);
            titleText.Text = value.ToUpperInvariant();
        }

        internal string SubtitleForTesting => subtitleText.Text.ToString();
    }

    /// <summary>
    /// The kinds of kiosk a world can contain.
    /// </summary>
    /// <remarks>
    /// Held apart from the drawable so that the entity kind can read a stored style into the same list
    /// without constructing anything.
    /// </remarks>
    internal static class TerminalVariants
    {
        private static readonly TerminalVariant[] all =
        {
            new TerminalVariant(FontAwesome.Solid.ClipboardList, "tasks & bounties", palette => palette.Blue1),
            new TerminalVariant(FontAwesome.Solid.ShoppingBag, "styles & items", palette => palette.Pink1),
            new TerminalVariant(FontAwesome.Solid.InfoCircle, "notices", palette => palette.YellowLight),
            new TerminalVariant(FontAwesome.Solid.Hammer, "gear & repairs", palette => palette.Orange1),
        };

        public const int QUEST_BOARD = 0;
        public const int SHOP = 1;

        public static int COUNT => all.Length;

        public static int Clamp(int style) => style < 0 || style >= all.Length ? QUEST_BOARD : style;

        public static TerminalVariant Of(int style) => all[Clamp(style)];
    }
}
