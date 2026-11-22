using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Testing;
using osu.Game.Graphics.UserInterface;
using osu.Game.Screens.Play.HUD;
using osu.Game.Skinning;
using osu.Game.Utils;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public partial class GameplayHudFormattingTest
    {
        [Test]
        public void TestAccuracyFormattingMatchesOriginal()
        {
            using var counter = new TestPercentageCounter();

            double[] boundaries = { 0, 0.01, 0.9899, 0.989999, 0.99, 0.9999, 0.999999, 1, -0.001, 1.001, double.NaN, double.PositiveInfinity, double.NegativeInfinity };
            foreach (double value in boundaries)
                Assert.That(counter.Format(value), Is.EqualTo(value.FormatAccuracy()), $"accuracy {value:R}");

            // Both directions matter when seeking or when the minimum achievable accuracy rises.
            for (int i = 0; i <= 10000; i++)
            {
                double value = i / 10000.0;
                Assert.That(counter.Format(value), Is.EqualTo(value.FormatAccuracy()));
                Assert.That(counter.Format(value + 0.0000001), Is.EqualTo((value + 0.0000001).FormatAccuracy()));
            }

            for (int i = 10000; i >= 0; i--)
            {
                double value = i / 10000.0;
                Assert.That(counter.Format(value), Is.EqualTo(value.FormatAccuracy()));
            }
        }

        [Test]
        public void TestArgonAccuracyTextRoundTripsCompleteValue()
        {
            using var counter = new TestArgonAccuracyCounter();
            var text = counter.CreateCounterText();
            using var drawable = (Drawable)text;

            foreach (string value in new[] { "100.00%", "99.99%", "0.00%", "98.76%", "98.76%" })
            {
                LocalisableString formatted = value;
                text.Text = formatted;
                Assert.That(text.Text, Is.EqualTo(formatted));
                var parts = drawable.ChildrenOfType<ArgonCounterTextComponent>().ToArray();
                Assert.That(parts[0].Text.ToString(), Is.EqualTo(value.Split('.')[0]));
                Assert.That(parts[1].Text.ToString(), Is.EqualTo("." + value.Split('.')[1].TrimEnd('%')));
            }
        }

        [Test]
        public void TestCachedAccuracyRemainsLocalisable()
        {
            using var counter = new TestPercentageCounter();
            LocalisableString formatted = counter.Format(0.987654);
            var originalCulture = CultureInfo.CurrentCulture;
            try
            {
                foreach (string culture in new[] { "en-US", "ru-RU", "de-DE" })
                {
                    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                    Assert.That(counter.Format(0.987655), Is.EqualTo(0.987655.FormatAccuracy()));
                    Assert.That(counter.Format(0.987655).ToString(), Is.EqualTo(formatted.ToString()));
                }
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
            }
        }

        [Test]
        public void TestAccuracyFormattingAllocationAndTiming()
        {
            using var counter = new TestPercentageCounter();
            using var baseline = new TestPercentageCounter();
            const int updates = 200000;

            // Same input stream and consumer for both paths. No renderer, audio or game profile.
            for (int i = 0; i < 10000; i++)
            {
                counter.Format(0.98765);
                baseline.FormatOriginal(0.98765);
            }

            for (int pass = 0; pass < 4; pass++)
            {
                bool original = pass == 0 || pass == 3;
                var target = original ? baseline : counter;
                LocalisableString previous = default;
                int changes = 0;
                long allocated = GC.GetAllocatedBytesForCurrentThread();
                long start = Stopwatch.GetTimestamp();
                for (int i = 0; i < updates; i++)
                {
                    // Smooth 0.01 percentage-point change per 100 updates, as in a rolling HUD.
                    double value = 0.99 - (i % 10000) * 0.000001;
                    LocalisableString formatted = original ? target.FormatOriginal(value) : target.Format(value);
                    if (formatted != previous)
                    {
                        previous = formatted;
                        changes++;
                    }
                }

                double elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
                TestContext.Out.WriteLine($"{(original ? "original" : "cached")}: {updates} updates, {elapsed:F3} ms, {allocated} bytes, {changes} display changes");
            }

            counter.Format(0.98765);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < updates; i++)
                counter.Format(0.98765 + (i % 10) * 0.0000001);
            long unchangedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(unchangedBytes, Is.LessThan(1024), "Unchanged visible digits must not allocate a new formattable value every update.");
        }

        [Test]
        public void TestArgonRepeatedTextDoesNotAllocate()
        {
            using var counter = new TestArgonAccuracyCounter();
            var text = counter.CreateCounterText();
            using var drawable = (Drawable)text;
            LocalisableString formatted = 0.987654.FormatAccuracy();
            text.Text = formatted;

            for (int i = 0; i < 10000; i++)
                text.Text = formatted;

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++)
                text.Text = formatted;
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            TestContext.Out.WriteLine($"Argon unchanged text, 10000 assignments: {bytes} bytes");
            Assert.That(bytes, Is.LessThan(1024));
        }

        [Test]
        public void TestLegacyAccuracyTextUsesCachedDescriptor()
        {
            using var counter = new LegacyAccuracyProbe();
            Assert.That(counter.Format(0.989999).ToString(), Is.EqualTo("98.99%"));
            Assert.That(counter.Format(0.99).ToString(), Is.EqualTo("99.00%"));
        }

        private partial class LegacyAccuracyProbe : LegacyAccuracyCounter
        {
            public LocalisableString Format(double value) => FormatCount(value);
        }

        private partial class TestPercentageCounter : PercentageCounter
        {
            public LocalisableString Format(double value) => FormatCount(value);
            public LocalisableString FormatOriginal(double value) => value.FormatAccuracy();
        }

        private partial class TestArgonAccuracyCounter : ArgonAccuracyCounter
        {
            public IHasText CreateCounterText() => CreateText();
        }
    }
}
