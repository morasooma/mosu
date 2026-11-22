using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Rulesets.Catch.Skinning.Default;
using osu.Game.Rulesets.Catch.UI;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Catch.Objects.Drawables
{
    public partial class DrawableFruitNinjaFruit : DrawablePalpableCatchHitObject
    {
        public new FruitNinjaFruit HitObject => (FruitNinjaFruit)base.HitObject;

        private readonly Container fruit;
        private readonly Container bomb;

        [Resolved]
        private FruitNinjaPlayfield playfield { get; set; } = null!;

        public DrawableFruitNinjaFruit()
            : base(null)
        {
            Anchor = Anchor.TopLeft;
            ScalingContainer.Children = new Drawable[]
            {
                fruit = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new SkinnableDrawable(new CatchSkinComponentLookup(CatchSkinComponents.Fruit), _ => new FruitPiece()),
                },
                bomb = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        new Circle
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = new Color4(30, 32, 40, 255),
                            BorderThickness = 6,
                            BorderColour = Color4.OrangeRed,
                            Masking = true,
                        },
                        new SpriteIcon
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Size = new Vector2(64),
                            Icon = FontAwesome.Solid.Bomb,
                            Colour = Color4.OrangeRed,
                        },
                    },
                },
            };
        }

        protected override void OnApply()
        {
            base.OnApply();
            fruit.Alpha = HitObject.IsBomb ? 0 : 1;
            bomb.Alpha = HitObject.IsBomb ? 1 : 0;
        }

        protected override void Update()
        {
            base.Update();
            Position = HitObject.PositionAt(Time.Current);
            ScalingContainer.Rotation = (float)((Time.Current - HitObject.StartTime) * (HitObject.IsBomb ? -0.06 : 0.12));
        }

        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            if (playfield.TrySlice(HitObject))
            {
                Result!.IgnoreForHitErrorMeter = HitObject.IsBomb;
                playfield.ShowSlice(HitObject, Position, AccentColour.Value);
                if (HitObject.IsBomb)
                    ApplyMinResult();
                else
                    ApplyResult(HitObject.SliceResultAt(Time.Current));
            }
            else if (Time.Current >= HitObject.FlightEndTime)
            {
                Result!.IgnoreForHitErrorMeter = true;
                if (HitObject.IsBomb)
                    ApplyMaxResult();
                else
                    ApplyMinResult();
            }
        }

        protected override void UpdateHitStateTransforms(ArmedState state)
        {
            if (state != ArmedState.Idle)
                this.FadeOut();
        }
    }
}
