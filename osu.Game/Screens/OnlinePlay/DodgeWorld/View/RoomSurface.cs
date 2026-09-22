// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Graphics;
using osuTK;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.View
{
    /// <summary>
    /// A stretch of one side of a panel, from <see cref="From"/> to <see cref="To"/> as fractions of that
    /// side's length, measured from its first corner: left to right, or top to bottom.
    /// </summary>
    internal readonly record struct EdgeSpan(Edges Side, float From, float To);

    internal partial class RoomSurface : EditableWorldEntity, ITexturedEntity
    {
        private readonly OsuColour colours;
        private readonly Container panel;
        private readonly Box fill;
        private readonly Sprite textureSprite;
        private readonly EntityTexture texture = new EntityTexture();
        private readonly Vector2 defaultSize;
        private readonly int defaultStyle;
        private readonly float defaultCornerRadius;
        private int styleIndex;
        private float cornerRadiusValue;
        private SurfaceFillMode fillMode = SurfaceFillMode.Fill;

        /// <summary>Thickness of the panel's outline, in world units.</summary>
        private const float border_thickness = 3;

        /// <summary>
        /// The outline, drawn as boxes along the stretches of each side no neighbour covers.
        /// </summary>
        /// <remarks>
        /// A masking border cannot do this: it is one uniform frame around the whole container. It is still
        /// what draws the outline of a panel standing on its own, because it is the only thing that follows
        /// a corner radius.
        /// </remarks>
        private readonly Container outline;

        /// <summary>The stretches of this panel's sides that neighbouring panels cover.</summary>
        private readonly List<EdgeSpan> covered = new List<EdgeSpan>();

        public override bool BlocksMovement => false;
        public override float? FixedDepth => 9000;
        public override string LayoutKind => "surface";
        public override bool CanResize => true;
        public override bool CanScale => false;
        public override bool CanRotate => true;
        public override bool CanStyle => true;
        public override bool CanRound => true;
        public override int StyleIndex => styleIndex;
        public override bool Rounded => cornerRadiusValue > 0;
        public float CornerRadiusValue => cornerRadiusValue;
        public string? TexturePath => texture.Path;
        public int TextureFillModeIndex => (int)fillMode;
        public string? TextureFillModeName => fillMode.ToString();
        public float TextureOpacity => texture.Opacity;
        public bool TextureSmoothing => texture.Smoothing;
        public bool TextureRepeats => fillMode == SurfaceFillMode.Tile;
        public float RenderedCornerRadius => panel.CornerRadius;
        public float RenderedSelectionCornerRadius => SelectionCornerRadius;

        public RoomSurface(OsuColour colours, Func<bool> editing, Action<EditableWorldEntity> select, Vector2 size, int style, bool rounded)
            : base(editing, select, colours.Orange1)
        {
            this.colours = colours;
            defaultSize = size;
            defaultStyle = style;
            defaultCornerRadius = rounded ? Math.Min(size.X, size.Y) * 0.11f : 0;
            styleIndex = style;
            cornerRadiusValue = defaultCornerRadius;

            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
            Size = size;
            AddInternal(panel = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Masking = true,
                BorderThickness = border_thickness,
                Children = new Drawable[]
                {
                    fill = new Box { RelativeSizeAxes = Axes.Both },
                    textureSprite = new Sprite
                    {
                        RelativeSizeAxes = Axes.Both,
                        FillMode = FillMode.Fill,
                        Alpha = 0,
                    },
                    outline = new Container { RelativeSizeAxes = Axes.Both },
                },
            });
            updateAppearance();
        }

        /// <summary>
        /// One stretch of the outline, from <paramref name="from"/> to <paramref name="to"/> along its
        /// side, in world units measured from the side's first corner.
        /// </summary>
        private static Box edge(Edges side, float from, float to)
        {
            bool horizontal = side is Edges.Top or Edges.Bottom;
            Anchor corner = side switch
            {
                Edges.Top => Anchor.TopLeft,
                Edges.Bottom => Anchor.BottomLeft,
                Edges.Left => Anchor.TopLeft,
                _ => Anchor.TopRight,
            };

            return new Box
            {
                Anchor = corner,
                Origin = corner,
                Position = horizontal ? new Vector2(from, 0) : new Vector2(0, from),
                Size = horizontal ? new Vector2(to - from, border_thickness) : new Vector2(border_thickness, to - from),
            };
        }

        /// <summary>
        /// The rounded part of the outline at one corner, drawn as a quarter of a ring.
        /// </summary>
        /// <remarks>
        /// Needed because the straight stretches cannot follow a corner radius, and the masking frame that
        /// could is all-or-nothing. A panel joined along one side keeps its radius everywhere else, so
        /// without these its far corners would be curved and unoutlined.
        /// </remarks>
        private static CircularProgress corner(Anchor at, float radius) => new CircularProgress
        {
            Anchor = at,
            Origin = Anchor.Centre,
            Position = new Vector2(
                at is Anchor.TopLeft or Anchor.BottomLeft ? radius : -radius,
                at is Anchor.TopLeft or Anchor.TopRight ? radius : -radius),
            Size = new Vector2(radius * 2),
            // Thickness is a fraction of the whole diameter, so this is the same three units as the
            // straight stretches rather than twice them.
            InnerRadius = Math.Clamp(border_thickness / (radius * 2), 0.001f, 1),
            Progress = 0.25f,
            // A quarter turn each, starting at twelve o'clock: the arc fills clockwise, so the top left
            // corner starts a quarter turn back from the top.
            Rotation = at switch
            {
                Anchor.TopLeft => -90,
                Anchor.TopRight => 0,
                Anchor.BottomRight => 90,
                _ => 180,
            },
        };

        /// <summary>
        /// Drops the outline along the stretches of each side another panel covers, so panels laid flush
        /// against each other read as one floor.
        /// </summary>
        /// <remarks>
        /// While nothing is covered this stays with the masking border, which is the only outline that
        /// follows a corner radius by itself. Once something is covered the frame steps aside for stretches
        /// and arcs — but the radius stays: a corridor meeting the middle of a rounded floor says nothing
        /// about that floor's corners, and squaring them off because of it was wrong. Only a side covered
        /// end to end squares the panel, because a panel with a whole side inside the floor really is in
        /// the middle of a larger shape, and there the rounding was leaving a notch.
        /// </remarks>
        public void SetCoveredSpans(List<EdgeSpan> spans)
        {
            if (spans.Count == covered.Count)
            {
                bool same = true;

                for (int i = 0; i < spans.Count && same; i++)
                    same = spans[i] == covered[i];

                if (same)
                    return;
            }

            covered.Clear();
            covered.AddRange(spans);
            applyBorder();
        }

        /// <summary>The size and rounding the drawn outline was worked out for.</summary>
        private Vector2 outlineSize;

        private float outlineRadius = -1;

        private void applyBorder()
        {
            panel.BorderThickness = covered.Count == 0 ? border_thickness : 0;
            applyCornerRadius();
            outlineSize = Size;
            outlineRadius = panel.CornerRadius;
            outline.Clear();
            DrawnOutlineForTesting = Edges.None;
            DrawnStretchesForTesting.Clear();

            if (covered.Count == 0)
                return;

            float radius = panel.CornerRadius;

            foreach (Edges side in all_sides)
            {
                bool horizontal = side is Edges.Top or Edges.Bottom;
                float length = horizontal ? Size.X : Size.Y;
                float drawn = 0;

                foreach ((float from, float to) in coveredSpansAlong(side))
                {
                    if (from > drawn)
                        addStretch(side, drawn, from, length, radius);

                    drawn = Math.Max(drawn, to);
                }

                if (drawn < 1)
                    addStretch(side, drawn, 1, length, radius);
            }

            if (radius >= 1)
            {
                foreach (Anchor at in all_corners)
                {
                    if (cornerIsExposed(at))
                        outline.Add(corner(at, radius));
                }
            }
        }

        /// <summary>
        /// Draws one stretch of a side, keeping it clear of the rounded corners the arcs take care of.
        /// </summary>
        private void addStretch(Edges side, float from, float to, float length, float radius)
        {
            DrawnOutlineForTesting |= side;
            DrawnStretchesForTesting.Add(new EdgeSpan(side, from, to));

            float start = Math.Max(from * length, radius);
            float end = Math.Min(to * length, length - radius);

            if (end > start)
                outline.Add(edge(side, start, end));
        }

        private static readonly Anchor[] all_corners = { Anchor.TopLeft, Anchor.TopRight, Anchor.BottomRight, Anchor.BottomLeft };

        /// <summary>
        /// Whether a corner is still on the outside of the floor, which is true when neither of the sides
        /// meeting there is covered at that end.
        /// </summary>
        private bool cornerIsExposed(Anchor at)
        {
            bool top = at is Anchor.TopLeft or Anchor.TopRight;
            bool left = at is Anchor.TopLeft or Anchor.BottomLeft;

            return endIsExposed(top ? Edges.Top : Edges.Bottom, atStart: left)
                   && endIsExposed(left ? Edges.Left : Edges.Right, atStart: top);
        }

        private bool endIsExposed(Edges side, bool atStart) =>
            !coveredSpansAlong(side).Any(span => atStart ? span.From <= 0.001f : span.To >= 0.999f);

        private static readonly Edges[] all_sides = { Edges.Top, Edges.Bottom, Edges.Left, Edges.Right };

        /// <summary>
        /// What is covered along one side, in order and with overlaps left in — <see cref="applyBorder"/>
        /// walks them and only ever moves forwards.
        /// </summary>
        private IEnumerable<(float From, float To)> coveredSpansAlong(Edges side) =>
            covered.Where(span => span.Side == side)
                   .Select(span => (span.From, span.To))
                   .OrderBy(span => span.From);

        /// <summary>
        /// Adds the stretches of <paramref name="surface"/>'s sides that <paramref name="other"/> covers.
        /// </summary>
        /// <remarks>
        /// Stretch by stretch, rather than whole sides: a wide panel meeting a narrow one across part of
        /// its edge should lose the outline exactly where the two meet and keep the rest, which is the
        /// difference between a corridor with a mouth and a corridor with a line drawn across it. A panel
        /// that swallows another whole covers nothing — it is the ground the other one is lying on, and
        /// the one on top is meant to be outlined against it.
        /// <para>
        /// Answered from upright rectangles, so a turned panel neither joins nor is joined to. Its edges
        /// are not the edges of its bounding box, and an outline dropped along the wrong side would be a
        /// worse bug than a visible seam.
        /// </para>
        /// </remarks>
        public static void AddCoveredSpans(RoomSurface surface, RoomSurface other, List<EdgeSpan> into)
        {
            if (surface.FacingDegrees != 0 || other.FacingDegrees != 0)
                return;

            RectangleF mine = surface.bounds();
            RectangleF theirs = other.bounds();

            if (theirs.Inflate(join_tolerance).Contains(mine))
                return;

            // Along the side, a neighbour is allowed to fall a hair short of reaching it. Across it, the
            // neighbour has to genuinely carry on outwards: a panel whose own edge is flush with mine does
            // not continue the floor past it, and dropping my outline for it left the floor open where
            // nothing led. That is also what stops two panels merely touching at a corner from each
            // rubbing a sliver off the other's outline.
            float reach = join_tolerance;

            if (theirs.Left < mine.Right + reach && theirs.Right > mine.Left - reach)
            {
                float from = (Math.Max(theirs.Left - reach, mine.Left) - mine.Left) / mine.Width;
                float to = (Math.Min(theirs.Right + reach, mine.Right) - mine.Left) / mine.Width;

                if (continues(theirs.Top, theirs.Bottom, mine.Top, outwardsIsLess: true))
                    add(Edges.Top, from, to, mine.Width);

                if (continues(theirs.Top, theirs.Bottom, mine.Bottom, outwardsIsLess: false))
                    add(Edges.Bottom, from, to, mine.Width);
            }

            if (theirs.Top < mine.Bottom + reach && theirs.Bottom > mine.Top - reach)
            {
                float from = (Math.Max(theirs.Top - reach, mine.Top) - mine.Top) / mine.Height;
                float to = (Math.Min(theirs.Bottom + reach, mine.Bottom) - mine.Top) / mine.Height;

                if (continues(theirs.Left, theirs.Right, mine.Left, outwardsIsLess: true))
                    add(Edges.Left, from, to, mine.Height);

                if (continues(theirs.Left, theirs.Right, mine.Right, outwardsIsLess: false))
                    add(Edges.Right, from, to, mine.Height);
            }

            void add(Edges side, float from, float to, float length)
            {
                // A join of a couple of units is two panels grazing each other, not a floor carrying on.
                if ((to - from) * length >= minimum_join)
                    into.Add(new EdgeSpan(side, Math.Clamp(from, 0, 1), Math.Clamp(to, 0, 1)));
            }
        }

        /// <summary>
        /// Whether a neighbour spanning <paramref name="near"/>..<paramref name="far"/> reaches one of my
        /// edges and carries on past it, away from me.
        /// </summary>
        private static bool continues(float near, float far, float edge, bool outwardsIsLess)
        {
            float outside = outwardsIsLess ? Math.Min(near, far) : Math.Max(near, far);
            float inside = outwardsIsLess ? Math.Max(near, far) : Math.Min(near, far);

            bool reachesMe = outwardsIsLess ? inside >= edge - join_tolerance : inside <= edge + join_tolerance;
            bool goesPastMe = outwardsIsLess ? outside <= edge - join_tolerance : outside >= edge + join_tolerance;

            return reachesMe && goesPastMe;
        }

        /// <summary>How far apart two panels may be and still count as joined, in world units.</summary>
        private const float join_tolerance = 2;

        /// <summary>The shortest join that reads as a floor carrying on, in world units.</summary>
        private const float minimum_join = 8;

        private RectangleF bounds() => new RectangleF(Position.X - Size.X / 2, Position.Y - Size.Y / 2, Size.X, Size.Y);

        /// <summary>The sides the outline is actually drawn along, for tests to read what is on screen.</summary>
        internal Edges DrawnOutlineForTesting { get; private set; } = Edges.None;

        /// <summary>
        /// Every stretch of outline on screen, so a test can tell "the whole side" from "the side either
        /// side of a doorway".
        /// </summary>
        internal readonly List<EdgeSpan> DrawnStretchesForTesting = new List<EdgeSpan>();

        /// <summary>Whether the panel is still outlined by the frame that follows its corner radius.</summary>
        internal bool FramedOutlineForTesting => panel.BorderThickness > 0;

        public override void CycleStyle()
        {
            styleIndex = (styleIndex + 1) % 5;
            updateAppearance();
        }

        public override void ToggleShape()
        {
            cornerRadiusValue = cornerRadiusValue > 0 ? 0 : 32;
            updateAppearance();
        }

        public void SetCornerRadius(float value)
        {
            cornerRadiusValue = Math.Clamp(value, 0, 240);
            applyCornerRadius();
        }

        public void SetTexturePath(string? path)
        {
            texture.SetPath(path);

            if (texture.Path == null)
                SetTexture(null);
        }

        public void SetTexture(Texture? value)
        {
            texture.SetResolved(value);
            textureSprite.Texture = value;
            textureSprite.Alpha = value == null ? 0 : texture.Opacity;
            applyFillMode();
        }

        public void SetTextureOpacity(float value)
        {
            texture.SetOpacity(value);

            if (textureSprite.Texture != null)
                textureSprite.Alpha = texture.Opacity;
        }

        public void CycleTextureFill()
        {
            fillMode = (SurfaceFillMode)(((int)fillMode + 1) % 4);
            applyFillMode();
        }

        public void ToggleTextureSmoothing() => texture.ToggleSmoothing();

        public void ReadTexture(string? path, float? opacity, bool? smoothing) => texture.Read(path, opacity, smoothing);

        public void ApplyMaterialState(float? savedCornerRadius, string? texturePath, int? savedFillMode, float? opacity, bool? smoothing)
        {
            if (savedCornerRadius.HasValue && float.IsFinite(savedCornerRadius.Value))
                SetCornerRadius(savedCornerRadius.Value);

            ReadTexture(texturePath, opacity, smoothing);
            fillMode = (SurfaceFillMode)Math.Clamp(savedFillMode ?? (int)SurfaceFillMode.Fill, 0, (int)SurfaceFillMode.Tile);
            applyFillMode();
        }

        public override void RestoreEditorDefaults()
        {
            base.RestoreEditorDefaults();
            Position = Vector2.Zero;
            Size = defaultSize;
            styleIndex = defaultStyle;
            cornerRadiusValue = defaultCornerRadius;
            texture.Clear();
            textureSprite.Texture = null;
            textureSprite.Alpha = 0;
            fillMode = SurfaceFillMode.Fill;
            applyFillMode();
            updateAppearance();
        }

        public override void ApplySavedEditorState(float? width, float? height, int? style, bool? savedRounded, float? scale)
        {
            base.ApplySavedEditorState(width, height, style, savedRounded, scale);
            if (style.HasValue)
                styleIndex = Math.Clamp(style.Value, 0, 4);
            if (savedRounded.HasValue)
                cornerRadiusValue = savedRounded.Value ? Math.Max(cornerRadiusValue, 32) : 0;
            updateAppearance();
        }

        protected override void Update()
        {
            base.Update();
            applyCornerRadius();

            // The stretches and arcs are placed in world units, so they have to be laid out again while the
            // panel is being resized or its rounding changed — the joins themselves may not have moved.
            if (covered.Count > 0 && (outlineSize != Size || Math.Abs(outlineRadius - panel.CornerRadius) > 0.01f))
                applyBorder();

            // The repeat count follows the surface's size, so it has to be refreshed while it is
            // being resized rather than only when the fill mode changes.
            if (TextureRepeats)
                textureSprite.TextureRectangle = new RectangleF(0, 0, DrawWidth, DrawHeight);
        }

        /// <summary>
        /// Tiling is a stretched sprite reading a texture region larger than the texture itself: with
        /// a repeating wrap mode, the image starts over every time the region crosses its edge. One
        /// texture pixel covers one world unit, so a tile keeps its size whatever the surface does.
        /// </summary>
        private void applyFillMode()
        {
            if (TextureRepeats)
            {
                textureSprite.FillMode = FillMode.Stretch;
                textureSprite.TextureRelativeSizeAxes = Axes.None;
                textureSprite.TextureRectangle = new RectangleF(0, 0, DrawWidth, DrawHeight);
                return;
            }

            textureSprite.FillMode = (FillMode)(int)fillMode;
            textureSprite.TextureRelativeSizeAxes = Axes.Both;
            textureSprite.TextureRectangle = new RectangleF(0, 0, 1, 1);
        }

        private void applyCornerRadius()
        {
            // A panel with a whole side inside the floor is in the middle of a larger shape, and a larger
            // shape cannot be rounded off in the middle: there the rounding left a notch of bare floor at
            // the join. A side merely met in the middle by a corridor says nothing about the corners.
            float requested = hasFullyCoveredSide() ? 0 : cornerRadiusValue;
            float renderedRadius = Math.Min(requested, Math.Min(DrawWidth, DrawHeight) / 2);
            panel.CornerRadius = renderedRadius;
            SelectionCornerRadius = renderedRadius;
        }

        private bool hasFullyCoveredSide()
        {
            foreach (Edges side in all_sides)
            {
                float reached = 0;

                foreach ((float from, float to) in coveredSpansAlong(side))
                {
                    if (from > reached + 0.001f)
                        break;

                    reached = Math.Max(reached, to);
                }

                if (reached >= 0.999f)
                    return true;
            }

            return false;
        }

        private void updateAppearance()
        {
            fill.Colour = styleIndex switch
            {
                1 => colours.Gray3,
                2 => colours.Pink4,
                3 => colours.Purple4,
                4 => colours.Blue4,
                _ => colours.Gray2,
            };
            osuTK.Graphics.Color4 outlineColour = styleIndex switch
            {
                2 => colours.Pink2,
                3 => colours.Purple2,
                4 => colours.Blue2,
                _ => colours.Gray4,
            };

            panel.BorderColour = outlineColour;
            outline.Colour = outlineColour;
        }
    }
}
