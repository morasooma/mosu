// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Skinning;
using osu.Game.Skinning.Components;

namespace osu.Game.Tests.Skins
{
    [TestFixture]
    public class SkinCustomFontSerialisationTest
    {
        [Test]
        public void TestBuiltInFontDoesNotCreateOverride()
        {
            var component = new TextElement();
            component.Font.Value = Typeface.Venera;

            var overrides = SkinCustomFontSerializer.ExtractOverrides(GlobalSkinnableContainers.MainHUDComponents, null, new Drawable[] { component });

            Assert.That(overrides, Is.Empty);
            Assert.That(component.CreateSerialisedInfo().Settings[@"font"], Is.EqualTo(Typeface.Venera));
        }

        [Test]
        public void TestCustomFontCreatesOverrideAndPreservesLayoutCompatibility()
        {
            var component = new TextElement();
            component.Font.Value = Typeface.Torus;
            component.CustomFontFamily.Value = @"Comfortaa Regular";
            component.TextWeight.Value = FontWeight.Bold;

            var overrides = SkinCustomFontSerializer.ExtractOverrides(GlobalSkinnableContainers.MainHUDComponents, null, new Drawable[] { component });

            Assert.That(overrides, Has.Count.EqualTo(1));
            Assert.That(overrides[0].FontFamily, Is.EqualTo(@"Comfortaa Regular"));
            Assert.That(overrides[0].TextWeight, Is.EqualTo(FontWeight.Bold));
            Assert.That(overrides[0].Path, Is.EqualTo(new[] { 0 }));

            var serialised = component.CreateSerialisedInfo();
            Assert.That(serialised.Settings[@"font"], Is.EqualTo(Typeface.Torus));
            Assert.That(serialised.Settings.ContainsKey(@"font_display"), Is.False);
            Assert.That(serialised.Settings.ContainsKey(@"custom_font_family"), Is.False);
        }

        [Test]
        public void TestCustomFontOverrideIsAppliedOnLoad()
        {
            var info = new SkinCustomFontInfo();
            info.SetOverridesForTarget(GlobalSkinnableContainers.MainHUDComponents, null, new[]
            {
                new SkinCustomFontOverride
                {
                    Container = GlobalSkinnableContainers.MainHUDComponents.ToString(),
                    Ruleset = @"global",
                    Path = new[] { 0 },
                    FontFamily = @"Comfortaa Regular",
                    TextWeight = FontWeight.SemiBold,
                }
            });

            var serialisedInfo = new TextElement().CreateSerialisedInfo();
            var instance = serialisedInfo.CreateInstance();

            SkinCustomFontSerializer.ApplyOverrides(
                info,
                GlobalSkinnableContainers.MainHUDComponents,
                null,
                new[] { serialisedInfo },
                new[] { instance });

            var fontComponent = (FontAdjustableSkinComponent)instance;
            Assert.That(fontComponent.CustomFontFamily.Value, Is.EqualTo(@"Comfortaa Regular"));
            Assert.That(fontComponent.TextWeight.Value, Is.EqualTo(FontWeight.SemiBold));
        }

        [Test]
        public void TestSetOverridesForTargetReplacesExistingEntries()
        {
            var info = new SkinCustomFontInfo();
            info.SetOverridesForTarget(GlobalSkinnableContainers.MainHUDComponents, null, new[]
            {
                new SkinCustomFontOverride { Container = GlobalSkinnableContainers.MainHUDComponents.ToString(), Ruleset = @"global", Path = new[] { 0 }, FontFamily = @"A" }
            });

            info.SetOverridesForTarget(GlobalSkinnableContainers.MainHUDComponents, null, new[]
            {
                new SkinCustomFontOverride { Container = GlobalSkinnableContainers.MainHUDComponents.ToString(), Ruleset = @"global", Path = new[] { 1 }, FontFamily = @"B" }
            });

            Assert.That(info.Overrides, Has.Count.EqualTo(1));
            Assert.That(info.Overrides[0].FontFamily, Is.EqualTo(@"B"));
        }
    }
}