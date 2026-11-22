using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Game.Screens.Play.HUD;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public class ScrollingKeyCounterTest
    {
        [Test]
        public void TestAvailableInSkinEditor()
        {
            Assert.That(SerialisedDrawableInfo.GetAllAvailableDrawables(), Does.Contain(typeof(ScrollingKeyCounterDisplay)));
        }

        [Test]
        public void TestLayoutRoundTripWithHeightAndSpeed()
        {
            using var display = new ScrollingKeyCounterDisplay
            {
                Position = new Vector2(120, 240),
                Scale = new Vector2(0.75f),
                Rotation = 90,
                Anchor = Anchor.BottomRight,
                Origin = Anchor.BottomRight,
                UsesFixedAnchor = true,
                Height = 420,
                ScrollSpeed = { Value = 180 },
            };

            var info = display.CreateSerialisedInfo();
            Assert.That(info.Settings, Contains.Key("scroll_speed"));
            using var restored = info.CreateInstance();
            Assert.That(restored, Is.TypeOf<ScrollingKeyCounterDisplay>());
            Assert.That(restored.Height, Is.EqualTo(420));
            Assert.That(((ScrollingKeyCounterDisplay)restored).ScrollSpeed.Value, Is.EqualTo(180));
            Assert.That(restored.Position, Is.EqualTo(display.Position));
            Assert.That(restored.Scale, Is.EqualTo(display.Scale));
            Assert.That(restored.Rotation, Is.EqualTo(display.Rotation));
            Assert.That(restored.Anchor, Is.EqualTo(display.Anchor));
            Assert.That(restored.Origin, Is.EqualTo(display.Origin));
            Assert.That(((ISerialisableDrawable)restored).UsesFixedAnchor, Is.True);
        }
    }
}
