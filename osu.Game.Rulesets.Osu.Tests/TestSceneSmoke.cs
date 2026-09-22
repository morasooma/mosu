// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Input.Events;
using osu.Framework.Input.States;
using osu.Framework.Logging;
using osu.Framework.Testing.Input;
using osu.Framework.Utils;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests
{
    public partial class TestSceneSmoke : OsuSkinnableTestScene
    {
        [Test]
        public void TestSmoking()
        {
            addStep("Create short smoke", 2_000);
            addStep("Create medium smoke", 5_000);
            addStep("Create long smoke", 10_000);
        }

        [Test]
        public void TestSmokePositionWithOffsetParent()
        {
            SmokeContainer? smokeContainer = null;
            TestInputManager? inputManager = null;

            AddStep("create offset smoke container", () => SetContents(_ => inputManager = new TestInputManager
            {
                RelativeSizeAxes = Axes.Both,
                Padding = new MarginPadding
                {
                    Left = 100,
                    Top = 50,
                },
                Child = smokeContainer = new SmokeContainer
                {
                    RelativeSizeAxes = Axes.Both,
                },
            }));

            AddStep("move mouse", () => inputManager!.MoveMouseTo(smokeContainer!.ToScreenSpace(new Vector2(200, 150))));
            AddAssert("smoke position is cursor position", () => Precision.AlmostEquals(smokeContainer!.LastMousePosition, new Vector2(200, 150)));
        }

        private void addStep(string stepName, double duration)
        {
            var smokeContainers = new List<SmokeContainer>();

            AddStep(stepName, () =>
            {
                smokeContainers.Clear();
                SetContents(_ =>
                {
                    smokeContainers.Add(new TestSmokeContainer
                    {
                        Duration = duration,
                        RelativeSizeAxes = Axes.Both
                    });

                    return new SmokingInputManager
                    {
                        Duration = duration,
                        RelativeSizeAxes = Axes.Both,
                        Size = new Vector2(0.95f),
                        Child = smokeContainers[^1],
                    };
                });
            });

            AddUntilStep("Until skinnable expires", () =>
            {
                if (smokeContainers.Count == 0)
                    return false;

                Logger.Log("How many: " + smokeContainers.Count);

                foreach (var smokeContainer in smokeContainers)
                {
                    if (smokeContainer.Children.OfType<SkinnableDrawable>().Any())
                        return false;
                }

                return true;
            });
        }

        private partial class SmokingInputManager : ManualInputManager
        {
            public double Duration { get; init; }

            private double? startTime;

            public SmokingInputManager()
            {
                UseParentInput = false;
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                MoveMouseTo(ToScreenSpace(DrawSize / 2));
            }

            protected override void Update()
            {
                base.Update();

                const float spin_angle = 4 * MathF.PI;

                startTime ??= Time.Current;

                float fraction = (float)((Time.Current - startTime) / Duration);

                float angle = fraction * spin_angle;
                float radius = fraction * Math.Min(DrawSize.X, DrawSize.Y) / 2;

                Vector2 pos = radius * new Vector2(MathF.Cos(angle), MathF.Sin(angle)) + DrawSize / 2;
                MoveMouseTo(ToScreenSpace(pos));
            }
        }

        private partial class TestInputManager : ManualInputManager
        {
            public TestInputManager()
            {
                UseParentInput = false;
            }
        }

        private partial class TestSmokeContainer : SmokeContainer
        {
            public double Duration { get; init; }

            private bool isPressing;
            private bool isFinished;

            private double? startTime;

            protected override void Update()
            {
                base.Update();

                startTime ??= Time.Current + 0.1;

                if (!isPressing && !isFinished && Time.Current > startTime)
                {
                    OnPressed(new KeyBindingPressEvent<OsuAction>(new InputState(), OsuAction.Smoke));
                    isPressing = true;
                    isFinished = false;
                }

                if (isPressing && Time.Current > startTime + Duration)
                {
                    OnReleased(new KeyBindingReleaseEvent<OsuAction>(new InputState(), OsuAction.Smoke));
                    isPressing = false;
                    isFinished = true;
                }
            }
        }
    }
}
