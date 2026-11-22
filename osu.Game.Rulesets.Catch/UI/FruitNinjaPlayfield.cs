using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Osu.UI.Cursor;
using osu.Game.Rulesets.Catch.Objects.Drawables;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.UI;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Catch.UI
{
    [Cached]
    public partial class FruitNinjaPlayfield : Playfield
    {
        private readonly Container effects;
        private CatchInputManager input = null!;
        private Vector2 previousBlade;
        private double previousTime = double.NaN;
        private bool wasSlicing;
        private bool validSwipe;
        private bool touchActive;

        public Vector2 BladePosition { get; private set; }
        public bool IsSlicing { get; private set; }
        public bool TouchActive => touchActive;

        public FruitNinjaPlayfield()
        {
            AddRangeInternal(new Drawable[]
            {
                HitObjectContainer,
                effects = new Container { RelativeSizeAxes = Axes.Both },
            });
            RegisterPool<FruitNinjaFruit, DrawableFruitNinjaFruit>(100);
        }

        [BackgroundDependencyLoader]
        private void load(CatchInputManager inputManager) => input = inputManager;

        protected override GameplayCursorContainer CreateCursor() => new OsuCursorContainer();

        public GameplayCursorContainer DetachCursor()
        {
            RemoveInternal(Cursor!, false);
            return Cursor!;
        }

        protected override HitObjectLifetimeEntry CreateLifetimeEntry(HitObject hitObject) => new FruitNinjaLifetimeEntry(hitObject);

        private class FruitNinjaLifetimeEntry : HitObjectLifetimeEntry
        {
            protected override double InitialLifetimeOffset => ((FruitNinjaFruit)HitObject).TimePreempt;

            public FruitNinjaLifetimeEntry(HitObject hitObject)
                : base(hitObject)
            {
            }
        }

        protected override bool OnTouchDown(TouchDownEvent e)
        {
            touchActive = true;
            return true;
        }

        protected override void OnTouchUp(TouchUpEvent e) => touchActive = false;

        protected override void Update()
        {
            base.Update();

            previousBlade = BladePosition;
            BladePosition = ToLocalSpace(input.CurrentState.Mouse.Position);
            IsSlicing = touchActive || input.KeyBindingContainer.PressedActions.Any(a => a is CatchAction.Dash or CatchAction.MoveLeft or CatchAction.MoveRight);

            double elapsed = Time.Current - previousTime;
            validSwipe = IsSlicing && wasSlicing && double.IsFinite(previousTime) && elapsed > 0 && elapsed <= 100;

            wasSlicing = IsSlicing;
            swipeStartTime = previousTime;
            previousTime = Time.Current;
        }

        private double swipeStartTime;

        public bool TrySlice(FruitNinjaFruit fruit)
        {
            if (!validSwipe || !fruit.IsInFlight(Time.Current))
                return false;

            return FruitNinjaFruit.IntersectsSwipe(previousBlade, BladePosition,
                fruit.PositionAt(Math.Max(swipeStartTime, fruit.StartTime - fruit.TimePreempt)),
                fruit.PositionAt(Time.Current), fruit.Radius);
        }

        public void ShowSlice(FruitNinjaFruit fruit, Vector2 position, Color4 colour)
        {
            if (fruit.IsBomb)
            {
                var flash = new Circle
                {
                    Origin = Anchor.Centre,
                    Position = position,
                    Size = new Vector2(fruit.Radius * 2),
                    Colour = Color4.OrangeRed,
                };
                effects.Add(flash);
                flash.ScaleTo(4, 250, Easing.OutQuint).FadeOut(250).Expire();
                return;
            }

            Vector2 movement = BladePosition - previousBlade;
            float angle = MathF.Atan2(movement.Y, movement.X) * 180 / MathF.PI + 90;
            float radius = fruit.Radius;

            for (int side = -1; side <= 1; side += 2)
            {
                var piece = new CaughtFruit();
                piece.RestoreState(new CatchObjectState(fruit, colour, false, fruit.IndexInBeatmap,
                    new Vector2(side < 0 ? radius : 0, radius), new Vector2(radius * 2), 0));
                var half = new Container
                {
                    Origin = side < 0 ? Anchor.CentreRight : Anchor.CentreLeft,
                    Position = position,
                    Size = new Vector2(radius, radius * 2),
                    Rotation = angle,
                    Masking = true,
                    Child = piece,
                };
                effects.Add(half);
                float radians = angle * MathF.PI / 180;
                Vector2 direction = new Vector2(MathF.Cos(radians), MathF.Sin(radians)) * side;
                half.MoveTo(position + direction * 38 + new Vector2(0, -18), 140, Easing.Out)
                    .Then().MoveTo(position + direction * 65 + new Vector2(0, 95), 280, Easing.In);
                half.RotateTo(angle + side * 65, 420).FadeOut(420).Expire();
            }

            for (int i = 0; i < 6; i++)
            {
                float angleRadians = (i * 60 + fruit.IndexInBeatmap * 23) * MathF.PI / 180;
                var juice = new Circle
                {
                    Origin = Anchor.Centre,
                    Position = position,
                    Size = new Vector2(3 + i % 3),
                    Colour = colour,
                };
                effects.Add(juice);
                juice.MoveTo(position + new Vector2(MathF.Cos(angleRadians), MathF.Sin(angleRadians)) * 45, 280, Easing.Out)
                    .FadeOut(280).Expire();
            }
        }

    }
}
