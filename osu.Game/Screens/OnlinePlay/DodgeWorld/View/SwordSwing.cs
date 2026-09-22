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
using osu.Game.Screens.OnlinePlay.DodgeWorld.Model;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Simulation;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Textures;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.View
{
    internal partial class SwordSwing : CompositeDrawable
    {
        private readonly Container weapon;
        private readonly float startRotation;
        private readonly float endRotation;
        private bool playPending;
        private double animationStartTime = double.NaN;

        public int AnimationState { get; private set; }
        public float PeakAlpha { get; private set; }
        public double AnimationElapsed { get; private set; } = -1;
        public float RenderedWidth { get; }
        public float HandOrbitRadius => CombatRules.WEAPON_HAND_ORBIT_RADIUS;

        public SwordSwing(Texture? texture, WeaponSkin skin, Color4 accent, float aimDirection, bool clockwise)
        {
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
            // The blade lives mostly outside its pivot. A 1x1 parent made the complete
            // swing eligible for subtree culling even though the Sprite itself was valid.
            Size = new Vector2(300);
            AlwaysPresent = true;

            float width = Math.Clamp(skin.DisplayWidth, 48, 128);
            RenderedWidth = width;
            bool hasTexture = DodgeWorldTexture.IsUsable(texture);
            float height = hasTexture ? width * texture!.Height / texture.Width : width / 4;
            float halfArc = CombatRules.ATTACK_ARC_DEGREES / 2;
            float startDirection = aimDirection + (clockwise ? -halfArc : halfArc);
            float endDirection = aimDirection + (clockwise ? halfArc : -halfArc);
            endRotation = endDirection;
            startRotation = startDirection;
            var weaponChildren = new List<Drawable>
            {
                new CircularContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.CentreRight,
                    Size = new Vector2(Math.Max(42, width * Math.Clamp(skin.PivotX, 0.25f, 1)), Math.Max(7, height * 0.18f)),
                    Alpha = hasTexture ? 0.28f : 0.9f,
                    Masking = true,
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = accent.Lighten(0.45f),
                    },
                },
                new Box
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(Math.Max(5, height * 0.14f), Math.Max(24, height * 0.7f)),
                    Rotation = 45,
                    Alpha = hasTexture ? 0.25f : 1,
                    Colour = accent,
                },
            };

            if (hasTexture)
            {
                weaponChildren.Add(new Sprite
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.TopLeft,
                    Texture = texture,
                    Size = new Vector2(width, height),
                    Position = new Vector2(-Math.Clamp(skin.PivotX, 0, 1) * width, -Math.Clamp(skin.PivotY, 0, 1) * height),
                });
            }

            var blade = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = Vector2.One,
                Rotation = -skin.BladeDirectionDegrees,
                Children = weaponChildren,
            };

            weapon = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Axes.Both,
                Rotation = startRotation,
                Child = new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Position = new Vector2(CombatRules.WEAPON_HAND_ORBIT_RADIUS, 0),
                    Size = Vector2.One,
                    Child = blade,
                },
            };

            InternalChild = weapon;
            Alpha = 0;
        }

        public void Play()
        {
            // The drawable receives its local clock on its first update after insertion.
            playPending = true;
            AnimationState = 1;
        }

        protected override void Update()
        {
            base.Update();
            if (!playPending)
            {
                if (double.IsNaN(animationStartTime))
                    return;
            }
            else
            {
                playPending = false;
                AnimationState = 2;
                animationStartTime = Time.Current;
            }

            double elapsed = Math.Max(0, Time.Current - animationStartTime);
            AnimationElapsed = elapsed;
            float progress = Math.Clamp((float)(elapsed / CombatRules.ATTACK_SWING_DURATION), 0, 1);
            float easedProgress = 1 - MathF.Pow(1 - progress, 3);
            weapon.Rotation = startRotation + (endRotation - startRotation) * easedProgress;

            const double fade_start = CombatRules.ATTACK_SWING_DURATION - 70;
            Alpha = elapsed <= fade_start
                ? 1
                : Math.Clamp(1 - (float)((elapsed - fade_start) / 105), 0, 1);
            PeakAlpha = Math.Max(PeakAlpha, Alpha);

            if (elapsed >= CombatRules.ATTACK_COOLDOWN)
                Expire();
        }
    }
}
