using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osuTK;

namespace osu.Game.Overlays.Toolbar
{
    public enum FilletDirection
    {
        Left,
        Right
    }

    /// <summary>
    /// Renders an inverse concave downward fillet corner transitioning smoothly between
    /// the toolbar horizontal bottom edge and the screen side border.
    /// </summary>
    public partial class InverseCornerFillet : BufferedContainer
    {
        public FilletDirection Direction { get; }

        public override bool HandlePositionalInput => false;

        private readonly Box fillBox;
        private readonly Container cutoutContainer;
        private readonly Box cutoutBox;

        public InverseCornerFillet(FilletDirection direction)
            : base(cachedFrameBuffer: false)
        {
            Direction = direction;

            InternalChildren = new Drawable[]
            {
                fillBox = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                },
                cutoutContainer = new Container
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.Centre,
                    Masking = true,
                    Child = cutoutBox = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Colour4.White,
                        Blending = new BlendingParameters
                        {
                            Source = BlendingType.Zero,
                            Destination = BlendingType.OneMinusSrcAlpha,
                            SourceAlpha = BlendingType.Zero,
                            DestinationAlpha = BlendingType.OneMinusSrcAlpha,
                            RGBEquation = BlendingEquation.Add,
                            AlphaEquation = BlendingEquation.Add,
                        }
                    }
                }
            };
        }

        public void UpdateFillet(float radius, Colour4 colour)
        {
            fillBox.Colour = colour;

            if (radius <= 0f)
            {
                Alpha = 0;
                Size = Vector2.Zero;
                return;
            }

            Alpha = 1;
            Size = new Vector2(radius);

            cutoutContainer.Size = new Vector2(radius * 2);
            cutoutContainer.CornerRadius = radius;
            cutoutContainer.Position = Direction == FilletDirection.Left
                ? new Vector2(radius, radius)
                : new Vector2(0, radius);
        }

        /// <summary>
        /// Determines whether a point in local fillet coordinates falls within the transparent circular cutout.
        /// </summary>
        public bool IsInsideCutout(Vector2 localPos)
        {
            float radius = DrawWidth;
            if (radius <= 0f)
                return false;

            Vector2 circleCenter = Direction == FilletDirection.Left
                ? new Vector2(radius, radius)
                : new Vector2(0, radius);

            return (localPos - circleCenter).LengthSquared <= radius * radius;
        }

        /// <summary>
        /// Determines whether a point in local fillet coordinates falls within the filled ear region.
        /// </summary>
        public bool IsInsideFill(Vector2 localPos)
        {
            float radius = DrawWidth;
            if (radius <= 0f)
                return false;

            if (localPos.X < 0 || localPos.X > radius || localPos.Y < 0 || localPos.Y > radius)
                return false;

            return !IsInsideCutout(localPos);
        }
    }
}
