// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Configuration;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Graphics.Cursor
{
    /// <summary>
    /// Renders the selected skin's legacy gameplay cursor for use outside gameplay.
    /// </summary>
    public partial class SkinnableMenuCursor : CompositeDrawable
    {
        public const float BASE_SIZE = 50;

        private const float pressed_scale = 1.3f;
        private const int revolution_duration = 10000;

        private Container scaleContainer = null!;
        private Drawable expandTarget = null!;

        [Resolved(canBeNull: true)]
        private ISkinSource? skinSource { get; set; }

        public SkinnableMenuCursor()
        {
            Size = new Vector2(BASE_SIZE);
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            InternalChild = scaleContainer = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            };

            config.GetBindable<float>(OsuSetting.GameplayCursorSize)
                  .BindValueChanged(size => scaleContainer.Scale = new Vector2(size.NewValue), true);

            rebuild();

            if (skinSource != null)
                skinSource.SourceChanged += onSkinSourceChanged;
        }

        public void Expand() => expandTarget.ScaleTo(1).ScaleTo(pressed_scale, 100, Easing.Out);

        public void Contract() => expandTarget.ScaleTo(1, 100, Easing.Out);

        private void onSkinSourceChanged() => Schedule(rebuild);

        private void rebuild()
        {
            ISkin? provider = skinSource?.FindProvider(skin => skin.GetTexture(@"cursor") != null);
            Texture? cursorTexture = provider?.GetTexture(@"cursor");

            if (cursorTexture == null)
            {
                scaleContainer.Child = expandTarget = createFallback();
                return;
            }

            bool rotates = provider!.GetConfig<LegacyCursorConfiguration, bool>(LegacyCursorConfiguration.CursorRotate)?.Value ?? true;
            Texture? middleTexture = provider.GetTexture(@"cursormiddle");

            var cursor = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                AutoSizeAxes = Axes.Both,
                Child = new Sprite
                {
                    Texture = cursorTexture,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                },
            };

            if (middleTexture != null)
            {
                cursor.Add(new Sprite
                {
                    Texture = middleTexture,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                });
            }

            scaleContainer.Child = expandTarget = cursor;

            if (rotates && !SkinPerformanceMode.Enabled)
                cursor.Spin(revolution_duration, RotationDirection.Clockwise);
        }

        private static Drawable createFallback() => new CircularContainer
        {
            Size = new Vector2(28),
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Masking = true,
            MaskingSmoothness = 2,
            BorderThickness = 2.5f,
            BorderColour = Color4.White.Opacity(0.95f),
            EdgeEffect = new EdgeEffectParameters
            {
                Type = EdgeEffectType.Glow,
                Radius = 6,
                Colour = new Color4(255, 130, 195, 130),
            },
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(255, 138, 211, 110),
                },
                new CircularContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                    Size = new Vector2(0.32f),
                    Masking = true,
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.White,
                    },
                },
            },
        };

        protected override void Dispose(bool isDisposing)
        {
            if (skinSource != null)
                skinSource.SourceChanged -= onSkinSourceChanged;

            base.Dispose(isDisposing);
        }

        private enum LegacyCursorConfiguration
        {
            CursorRotate,
        }
    }
}
