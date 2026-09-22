// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Simulation;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.View
{
    /// <summary>
    /// Draws a mob. Its position, health and attack timing belong to <see cref="MobState"/>; the
    /// wander path used to be computed here, which meant the thing being fought and the thing being
    /// drawn were the same object and neither could be tested.
    /// </summary>
    internal partial class WorldMob : CompositeDrawable
    {
        /// <summary>
        /// The box a mob image is fitted into, in world units.
        /// </summary>
        /// <remarks>
        /// Close to the placeholder body's size on purpose: contact damage is measured at a fixed
        /// distance, so a mob that looked far bigger than it fights would be misleading.
        /// </remarks>
        public static readonly Vector2 SPRITE_SIZE = new Vector2(74, 74);

        private readonly Container body;
        private readonly Sprite skin;
        private readonly Box healthFill;

        public WorldMob(OsuColour colours)
        {
            Anchor = Anchor.Centre;
            Origin = Anchor.BottomCentre;
            Size = new Vector2(74, 62);
            InternalChildren = new Drawable[]
            {
                new CircularContainer
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.Centre,
                    Y = -2,
                    Size = new Vector2(36, 10),
                    Scale = new Vector2(1, 0.55f),
                    Alpha = 0.35f,
                    Masking = true,
                    Child = new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.Black },
                },
                body = new CircularContainer
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Y = -8,
                    Size = new Vector2(38),
                    Masking = true,
                    CornerRadius = 12,
                    BorderThickness = 2,
                    BorderColour = colours.Red1,
                    Children = new Drawable[]
                    {
                        new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Red4 },
                        new SpriteIcon
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Icon = FontAwesome.Solid.Ghost,
                            Size = new Vector2(21),
                            Colour = colours.Red0,
                        },
                    },
                },
                skin = new Sprite
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Y = -4,
                    Size = SPRITE_SIZE,
                    Alpha = 0,
                },
                new Container
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Size = new Vector2(50, 7),
                    Masking = true,
                    CornerRadius = 3,
                    Children = new Drawable[]
                    {
                        new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Gray0 },
                        healthFill = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Width = 1,
                            Colour = colours.Red1,
                        },
                    },
                },
            };
        }

        public void SyncFrom(MobState mob) => Position = mob.Position;

        private MobAppearance appearance = MobAppearance.None;

        /// <summary>
        /// Dresses the mob in its zone's image, replacing the placeholder body.
        /// </summary>
        /// <remarks>
        /// Called every frame rather than only when the mob appears, because a zone's image is still
        /// loading when the room's mobs are spawned: they used to keep whatever they were born with, which
        /// is why an authored mob showed as the placeholder ghost first. The swap is faded so that it
        /// reads as the image arriving rather than as a mob changing.
        /// <para>
        /// The image is sized to fit rather than letterboxed inside a fixed square. A wide image fitted
        /// into a square leaves the gap under it, which drew the mob hovering above its own shadow.
        /// </para>
        /// </remarks>
        public void SetAppearance(MobAppearance value)
        {
            if (value == appearance)
                return;

            appearance = value;
            skin.Texture = value.Texture;

            if (value.Texture == null)
            {
                skin.Alpha = 0;
                body.FadeIn(120, Easing.OutQuint);
                return;
            }

            skin.Size = MobAppearance.FitInto(value.Texture, SPRITE_SIZE);
            skin.FadeTo(value.Opacity, 120, Easing.OutQuint);
            body.FadeOut(120, Easing.OutQuint);
        }

        /// <summary>
        /// Plays the reaction to being hit, and resizes the health bar to <paramref name="healthFraction"/>.
        /// </summary>
        public void ShowDamage(float healthFraction)
        {
            healthFill.ResizeWidthTo(healthFraction, 100, Easing.OutQuint);

            // Whichever of the two is standing in for the mob's body.
            Drawable hit = skin.Alpha > 0 ? skin : body;

            hit.FlashColour(Color4.White, 140);
            hit.ScaleTo(1.16f, 55).Then().ScaleTo(1, 110, Easing.OutQuint);
        }
    }
}
