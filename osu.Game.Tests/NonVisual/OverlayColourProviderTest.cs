// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Game.Overlays;
using osuTK.Graphics;
using System;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    [NonParallelizable]
    public class OverlayColourProviderTest
    {
        private ThemeMode previousTheme;

        [SetUp]
        public void SetUp()
        {
            previousTheme = OverlayColourProvider.CurrentTheme.Value;
            OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default;
        }

        [TearDown]
        public void TearDown() => OverlayColourProvider.CurrentTheme.Value = previousTheme;

        [Test]
        public void TestColourBindableUpdatesForEveryTheme()
        {
            var provider = new OverlayColourProvider(OverlayColourScheme.Blue);
            var colour = provider.GetColourBindable(OverlayColour.Content1);

            assertEqual(provider.Content1, colour.Value);

            OverlayColourProvider.CurrentTheme.Value = ThemeMode.Light;
            assertEqual(provider.Content1, colour.Value);

            OverlayColourProvider.CurrentTheme.Value = ThemeMode.Dark;
            assertEqual(provider.Content1, colour.Value);
        }

        [Test]
        public void TestEveryReactiveColourMatchesLegacyPalette()
        {
            var provider = new OverlayColourProvider(OverlayColourScheme.Purple);

            foreach (ThemeMode theme in Enum.GetValues(typeof(ThemeMode)))
            {
                OverlayColourProvider.CurrentTheme.Value = theme;

                foreach (OverlayColour role in Enum.GetValues(typeof(OverlayColour)))
                    assertEqual(getLegacyColour(provider, role), provider.GetColourBindable(role).Value);
            }
        }

        [Test]
        public void TestColourBindableUpdatesWhenProviderHueChanges()
        {
            var provider = new OverlayColourProvider(OverlayColourScheme.Blue);
            var colour = provider.GetColourBindable(OverlayColour.Background4);

            provider.ChangeColourScheme(OverlayColourScheme.Purple);

            assertEqual(provider.Background4, colour.Value);
        }

        [Test]
        public void TestColourBindableHonoursIgnoreLightTheme()
        {
            var provider = new OverlayColourProvider(OverlayColourScheme.Blue);
            var colour = provider.GetColourBindable(OverlayColour.Content1);

            OverlayColourProvider.CurrentTheme.Value = ThemeMode.Light;
            provider.IgnoreLightTheme = true;

            assertEqual(provider.GetDefaultColour(0.4f, 1), colour.Value);
        }

        [Test]
        public void TestThemeCallbacksSeeCompletePalette()
        {
            var provider = new OverlayColourProvider(OverlayColourScheme.Blue);
            var content = provider.GetColourBindable(OverlayColour.Content1);
            Color4 observedContent2 = default;
            content.BindValueChanged(_ => observedContent2 = provider.Content2);

            OverlayColourProvider.CurrentTheme.Value = ThemeMode.Light;

            assertEqual(provider.Content2, observedContent2);
        }

        [TestCase(ThemeMode.Default, true, false, ThemeMode.Light)]
        [TestCase(ThemeMode.Dark, true, false, ThemeMode.Light)]
        [TestCase(ThemeMode.Dark, false, false, ThemeMode.Dark)]
        [TestCase(ThemeMode.Dark, true, true, ThemeMode.Dark)]
        public void TestEffectiveThemeHonoursMosuServerPolicy(ThemeMode configuredTheme, bool forceLightTheme, bool isThirdPartyServer, ThemeMode expected)
        {
            Assert.That(ThemeModeResolver.Resolve(configuredTheme, forceLightTheme, isThirdPartyServer), Is.EqualTo(expected));
        }

        private static void assertEqual(Color4 expected, Colour4 actual)
        {
            Assert.Multiple(() =>
            {
                Assert.That(actual.R, Is.EqualTo(expected.R));
                Assert.That(actual.G, Is.EqualTo(expected.G));
                Assert.That(actual.B, Is.EqualTo(expected.B));
                Assert.That(actual.A, Is.EqualTo(expected.A));
            });
        }

        private static Color4 getLegacyColour(OverlayColourProvider provider, OverlayColour colour) => colour switch
        {
            OverlayColour.Colour0 => provider.Colour0,
            OverlayColour.Colour1 => provider.Colour1,
            OverlayColour.Colour2 => provider.Colour2,
            OverlayColour.Colour3 => provider.Colour3,
            OverlayColour.Colour4 => provider.Colour4,
            OverlayColour.Highlight1 => provider.Highlight1,
            OverlayColour.Content1 => provider.Content1,
            OverlayColour.Content2 => provider.Content2,
            OverlayColour.Light1 => provider.Light1,
            OverlayColour.Light2 => provider.Light2,
            OverlayColour.Light3 => provider.Light3,
            OverlayColour.Light4 => provider.Light4,
            OverlayColour.Dark1 => provider.Dark1,
            OverlayColour.Dark2 => provider.Dark2,
            OverlayColour.Dark3 => provider.Dark3,
            OverlayColour.Dark4 => provider.Dark4,
            OverlayColour.Dark5 => provider.Dark5,
            OverlayColour.Dark6 => provider.Dark6,
            OverlayColour.Foreground1 => provider.Foreground1,
            OverlayColour.Background1 => provider.Background1,
            OverlayColour.Background2 => provider.Background2,
            OverlayColour.Background3 => provider.Background3,
            OverlayColour.Background4 => provider.Background4,
            OverlayColour.Background5 => provider.Background5,
            OverlayColour.Background6 => provider.Background6,
            _ => throw new ArgumentOutOfRangeException(nameof(colour), colour, null),
        };
    }
}
