// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osuTK;
using osuTK.Graphics;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Pooling;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Osu.Objects.Drawables.Connections
{
    /// <summary>
    /// A single follow point positioned between two adjacent <see cref="DrawableOsuHitObject"/>s.
    /// </summary>
    public partial class FollowPoint : PoolableDrawable, IAnimationTimeReference
    {
        private const float width = 8;
        private bool directChildrenLifeStable;

        public override bool RemoveWhenNotAlive => false;

        public FollowPoint()
        {
            Origin = Anchor.Centre;

            InternalChild = new SkinnableDrawable(new OsuSkinComponentLookup(OsuSkinComponents.FollowPoint), _ =>
            {
                if (SkinPerformanceMode.Enabled)
                    return new PerformanceFollowPoint();

                return new CircularContainer
                {
                    Masking = true,
                    AutoSizeAxes = Axes.Both,
                    EdgeEffect = new EdgeEffectParameters
                    {
                        Type = EdgeEffectType.Glow,
                        Colour = Color4.White.Opacity(0.2f),
                        Radius = 4,
                    },
                    Child = new Box
                    {
                        Size = new Vector2(width),
                        Blending = BlendingParameters.Additive,
                        Origin = Anchor.Centre,
                        Anchor = Anchor.Centre,
                        Alpha = 0.5f,
                    }
                };
            });
        }

        protected override bool CheckChildrenLife()
        {
            if (directChildrenLifeStable)
                return false;

            bool aliveChanged = base.CheckChildrenLife();
            directChildrenLifeStable = InternalChildren.Count == AliveInternalChildren.Count;
            return aliveChanged;
        }

        private partial class PerformanceFollowPoint : CompositeDrawable
        {
            public PerformanceFollowPoint()
            {
                AutoSizeAxes = Axes.Both;
            }

            [BackgroundDependencyLoader]
            private void load(TextureStore textures)
            {
                InternalChild = new Sprite
                {
                    Size = new Vector2(width),
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Texture = textures.Get(@"Gameplay/osu/disc"),
                    Blending = BlendingParameters.Additive,
                    Alpha = 0.5f,
                };
            }
        }

        public Bindable<double> AnimationStartTime { get; } = new BindableDouble();
    }
}
