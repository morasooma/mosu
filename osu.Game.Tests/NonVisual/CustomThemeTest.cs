using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Overlays;
using osu.Game.Overlays.CustomTheme;
using osuTK.Graphics;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class CustomThemeTest
    {
        [SetUp]
        public void SetUp()
        {
            OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default;
            OverlayColourProvider.CustomTheme.Value = new CustomThemeDefinition();
        }

        [TearDown]
        public void TearDown()
        {
            OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default;
            OverlayColourProvider.CustomTheme.Value = new CustomThemeDefinition();
        }

        [Test]
        public void TestDefaultConstructor()
        {
            var def = new CustomThemeDefinition();
            Assert.That(def.Version, Is.EqualTo(CustomThemeDefinition.CURRENT_VERSION));
            Assert.That(def.BaseTheme, Is.EqualTo(ThemeMode.Default));
            Assert.That(def.Overrides, Is.Not.Null);
            Assert.That(def.Overrides, Is.Empty);
        }

        [Test]
        public void TestJsonRoundTrip()
        {
            var original = new CustomThemeDefinition(ThemeMode.Dark);
            original.SetOverride(OverlayColour.Colour1, Colour4.Red);
            original.SetOverride(OverlayColour.Content1, Colour4.FromHex("#AABBCC"));
            original.SetOverride(OverlayColour.Background4, Colour4.Black);

            string json = original.ToJson();
            var deserialized = CustomThemeDefinition.FromJson(json);

            Assert.That(deserialized, Is.EqualTo(original));
            Assert.That(deserialized.BaseTheme, Is.EqualTo(ThemeMode.Dark));
            Assert.That(deserialized.Overrides.Count, Is.EqualTo(3));
            Assert.That(deserialized.TryGetOverride(OverlayColour.Colour1, out var col1), Is.True);
            Assert.That(col1, Is.EqualTo(Colour4.Red));
            Assert.That(deserialized.TryGetOverride(OverlayColour.Content1, out var col2), Is.True);
            Assert.That(col2, Is.EqualTo(Colour4.FromHex("#AABBCC")));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("{ invalid json")]
        [TestCase("[]")]
        [TestCase("{\"version\": \"not an int\"}")]
        public void TestMalformedJsonFallback(string? json)
        {
            var def = CustomThemeDefinition.FromJson(json);
            Assert.That(def, Is.Not.Null);
            Assert.That(def.BaseTheme, Is.EqualTo(ThemeMode.Default));
            Assert.That(def.Overrides, Is.Empty);
        }

        [Test]
        public void TestBaseThemeCannotBeCustomRecursively()
        {
            string json = "{\"version\": 1, \"baseTheme\": \"Custom\", \"overrides\": {}}";
            var def = CustomThemeDefinition.FromJson(json);
            Assert.That(def.BaseTheme, Is.EqualTo(ThemeMode.Default));
        }

        [Test]
        public void TestPartialInheritance()
        {
            var def = new CustomThemeDefinition(ThemeMode.Light);
            def.SetOverride(OverlayColour.Colour1, Colour4.Blue);

            Assert.That(def.TryGetOverride(OverlayColour.Colour1, out var overrideCol), Is.True);
            Assert.That(overrideCol, Is.EqualTo(Colour4.Blue));

            // Content1 has no override -> should return false to indicate inheritance
            Assert.That(def.TryGetOverride(OverlayColour.Content1, out _), Is.False);
        }

        [Test]
        public void TestClearOverrideAndClearAll()
        {
            var def = new CustomThemeDefinition();
            def.SetOverride(OverlayColour.Colour1, Colour4.Lime);
            def.SetOverride(OverlayColour.Colour2, Colour4.Orange);

            Assert.That(def.Overrides.Count, Is.EqualTo(2));
            Assert.That(def.ClearOverride(OverlayColour.Colour1), Is.True);
            Assert.That(def.Overrides.Count, Is.EqualTo(1));
            Assert.That(def.TryGetOverride(OverlayColour.Colour1, out _), Is.False);

            def.ClearAllOverrides();
            Assert.That(def.Overrides, Is.Empty);
        }

        [Test]
        public void TestDeepClonePreservesValues()
        {
            var original = new CustomThemeDefinition(ThemeMode.Dark);
            original.SetOverride(OverlayColour.Content1, Colour4.Yellow);

            var clone = original.DeepClone();
            Assert.That(clone, Is.EqualTo(original));

            clone.SetOverride(OverlayColour.Content1, Colour4.Cyan);
            Assert.That(clone, Is.Not.EqualTo(original));
            Assert.That(original.TryGetOverride(OverlayColour.Content1, out var origCol), Is.True);
            Assert.That(origCol, Is.EqualTo(Colour4.Yellow));
        }

        [Test]
        public void TestCustomThemeFullInheritanceMatchesBaseTheme()
        {
            var provider = new OverlayColourProvider(OverlayColourScheme.Blue);

            OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default;
            var defaultContent = provider.Content1;
            var defaultBg = provider.Background4;

            OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom;
            OverlayColourProvider.CustomTheme.Value = new CustomThemeDefinition(ThemeMode.Default);

            Assert.That(provider.Content1, Is.EqualTo(defaultContent));
            Assert.That(provider.Background4, Is.EqualTo(defaultBg));
            Assert.That(provider.GetColourBindable(OverlayColour.Content1).Value, Is.EqualTo((Colour4)defaultContent));
            Assert.That(provider.GetColourBindable(OverlayColour.Background4).Value, Is.EqualTo((Colour4)defaultBg));
        }

        [Test]
        public void TestCustomThemePartialInheritanceOverridesSpecificTokens()
        {
            var provider = new OverlayColourProvider(OverlayColourScheme.Blue);

            var customDef = new CustomThemeDefinition(ThemeMode.Default);
            customDef.SetOverride(OverlayColour.Colour1, Colour4.FromHex("#112233"));
            customDef.SetOverride(OverlayColour.Background4, Colour4.FromHex("#445566"));

            OverlayColourProvider.CustomTheme.Value = customDef;
            OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom;

            // Overridden tokens
            Assert.That(provider.GetColourBindable(OverlayColour.Colour1).Value, Is.EqualTo(Colour4.FromHex("#112233")));
            Assert.That(provider.Colour1, Is.EqualTo((Color4)Colour4.FromHex("#112233")));
            Assert.That(provider.GetColourBindable(OverlayColour.Background4).Value, Is.EqualTo(Colour4.FromHex("#445566")));
            Assert.That(provider.Background4, Is.EqualTo((Color4)Colour4.FromHex("#445566")));

            // Non-overridden tokens must inherit from base (ThemeMode.Default)
            OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default;
            var defaultContent = provider.Content1;
            OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom;

            Assert.That(provider.Content1, Is.EqualTo(defaultContent));
            Assert.That(provider.GetColourBindable(OverlayColour.Content1).Value, Is.EqualTo((Colour4)defaultContent));
        }

        [Test]
        public void TestLiveReactivityOnOverrideMutation()
        {
            var provider = new OverlayColourProvider(OverlayColourScheme.Blue);
            var colourBindable = provider.GetColourBindable(OverlayColour.Colour1);

            OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom;
            var customDef = new CustomThemeDefinition(ThemeMode.Default);
            OverlayColourProvider.CustomTheme.Value = customDef;

            // Mutate override
            var updated = customDef.DeepClone();
            updated.SetOverride(OverlayColour.Colour1, Colour4.Red);
            OverlayColourProvider.CustomTheme.Value = updated;

            Assert.That(colourBindable.Value, Is.EqualTo(Colour4.Red));
            Assert.That(provider.Colour1, Is.EqualTo((Color4)Colour4.Red));

            // Clear override -> should revert to inherited base
            var reverted = updated.DeepClone();
            reverted.ClearOverride(OverlayColour.Colour1);
            OverlayColourProvider.CustomTheme.Value = reverted;

            Assert.That(colourBindable.Value, Is.EqualTo((Colour4)provider.GetInheritedColour(OverlayColour.Colour1, ThemeMode.Default)));
        }

        [Test]
        public void TestLiveReactivityOnBaseThemeChange()
        {
            var provider = new OverlayColourProvider(OverlayColourScheme.Blue);
            var contentBindable = provider.GetColourBindable(OverlayColour.Content1);

            OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom;
            var customDef = new CustomThemeDefinition(ThemeMode.Default);
            OverlayColourProvider.CustomTheme.Value = customDef;

            var defaultBaseColour = provider.GetInheritedColour(OverlayColour.Content1, ThemeMode.Default);
            Assert.That(contentBindable.Value, Is.EqualTo((Colour4)defaultBaseColour));

            // Switch base to Light
            var lightDef = customDef.DeepClone();
            lightDef.BaseTheme = ThemeMode.Light;
            OverlayColourProvider.CustomTheme.Value = lightDef;

            var lightBaseColour = provider.GetInheritedColour(OverlayColour.Content1, ThemeMode.Light);
            Assert.That(contentBindable.Value, Is.EqualTo((Colour4)lightBaseColour));
            Assert.That(contentBindable.Value, Is.Not.EqualTo((Colour4)defaultBaseColour));
        }

        [Test]
        public void TestContrastPolarityDerivedFromBaseTheme()
        {
            OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom;

            // Base = Default
            OverlayColourProvider.CustomTheme.Value = new CustomThemeDefinition(ThemeMode.Default);
            Assert.That(OverlayColourProvider.IsLightTheme, Is.False);
            Assert.That(OverlayColourProvider.IsDarkTheme, Is.False);

            // Base = Light
            OverlayColourProvider.CustomTheme.Value = new CustomThemeDefinition(ThemeMode.Light);
            Assert.That(OverlayColourProvider.IsLightTheme, Is.True);
            Assert.That(OverlayColourProvider.IsDarkTheme, Is.False);

            // Base = Dark
            OverlayColourProvider.CustomTheme.Value = new CustomThemeDefinition(ThemeMode.Dark);
            Assert.That(OverlayColourProvider.IsLightTheme, Is.False);
            Assert.That(OverlayColourProvider.IsDarkTheme, Is.True);
        }

        [Test]
        public void TestBaseThemeChangeTriggersCurrentThemeChange()
        {
            OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom;
            OverlayColourProvider.CustomTheme.Value = new CustomThemeDefinition(ThemeMode.Default);

            int currentThemeFiredCount = 0;
            var boundTheme = OverlayColourProvider.CurrentTheme.GetBoundCopy();
            boundTheme.BindValueChanged(_ => currentThemeFiredCount++);

            // Changing BaseTheme to Light should trigger CurrentTheme change on bound copies
            var updated = OverlayColourProvider.CustomTheme.Value.DeepClone();
            updated.BaseTheme = ThemeMode.Light;
            OverlayColourProvider.CustomTheme.Value = updated;

            Assert.That(currentThemeFiredCount, Is.GreaterThan(0));
            Assert.That(OverlayColourProvider.IsLightTheme, Is.True);
        }

        [Test]
        public void TestConfigManagerPersistence()
        {
            using var storage = new TemporaryNativeStorage(@"fork-custom-theme-test");
            using var config = new OsuConfigManager(storage);

            var initialThemeMode = config.GetBindable<ThemeMode>(OsuSetting.ForkThemeMode);
            var initialJson = config.GetBindable<string>(OsuSetting.ForkCustomThemeJson);

            Assert.That(initialThemeMode.Value, Is.EqualTo(ThemeMode.Default));
            Assert.That(initialJson.Value, Is.EqualTo(string.Empty));

            var custom = new CustomThemeDefinition(ThemeMode.Dark);
            custom.SetOverride(OverlayColour.Content1, Colour4.Lime);
            config.SetValue(OsuSetting.ForkCustomThemeJson, custom.ToJson());
            config.SetValue(OsuSetting.ForkThemeMode, ThemeMode.Custom);

            Assert.That(config.Get<ThemeMode>(OsuSetting.ForkThemeMode), Is.EqualTo(ThemeMode.Custom));

            var loaded = CustomThemeDefinition.FromJson(config.Get<string>(OsuSetting.ForkCustomThemeJson));
            Assert.That(loaded.BaseTheme, Is.EqualTo(ThemeMode.Dark));
            Assert.That(loaded.TryGetOverride(OverlayColour.Content1, out var loadedCol), Is.True);
            Assert.That(loadedCol, Is.EqualTo(Colour4.Lime));
        }

        [Test]
        public void TestV1BackwardsCompatibilityWithoutGeometry()
        {
            // Old schema v1 JSON without geometry block
            string v1Json = "{\"version\": 1, \"baseTheme\": \"Dark\", \"overrides\": {\"Colour1\": \"#FF0000\"}}";
            var def = CustomThemeDefinition.FromJson(v1Json);

            Assert.That(def, Is.Not.Null);
            Assert.That(def.BaseTheme, Is.EqualTo(ThemeMode.Dark));
            Assert.That(def.Geometry, Is.Not.Null);
            Assert.That(def.Geometry.IsEmpty, Is.True);
            Assert.That(def.TryGetRadius(GeometryRole.Button, out _), Is.False);
            Assert.That(def.TryGetRadius(GeometryRole.Input, out _), Is.False);
            Assert.That(def.TryGetRadius(GeometryRole.Card, out _), Is.False);
            Assert.That(def.TryGetRadius(GeometryRole.Panel, out _), Is.False);
            Assert.That(def.TryGetRadius(GeometryRole.Popup, out _), Is.False);
        }

        [Test]
        public void TestGeometrySerializationAndDeserialization()
        {
            var theme = new CustomThemeDefinition(ThemeMode.Default);
            theme.SetRadius(GeometryRole.Button, 12f);
            theme.SetRadius(GeometryRole.Card, 16f);
            theme.SetRadius(GeometryRole.Popup, 0f);

            string json = theme.ToJson();
            var restored = CustomThemeDefinition.FromJson(json);

            Assert.That(restored.TryGetRadius(GeometryRole.Button, out float buttonRadius), Is.True);
            Assert.That(buttonRadius, Is.EqualTo(12f));

            Assert.That(restored.TryGetRadius(GeometryRole.Card, out float cardRadius), Is.True);
            Assert.That(cardRadius, Is.EqualTo(16f));

            Assert.That(restored.TryGetRadius(GeometryRole.Popup, out float popupRadius), Is.True);
            Assert.That(popupRadius, Is.EqualTo(0f));

            Assert.That(restored.TryGetRadius(GeometryRole.Input, out _), Is.False);
            Assert.That(restored.TryGetRadius(GeometryRole.Panel, out _), Is.False);
        }

        [Test]
        public void TestGeometryClamping()
        {
            var theme = new CustomThemeDefinition();

            // Button: bounds [0, 20]
            theme.SetRadius(GeometryRole.Button, -5f);
            Assert.That(theme.TryGetRadius(GeometryRole.Button, out float buttonLow), Is.True);
            Assert.That(buttonLow, Is.EqualTo(0f));

            theme.SetRadius(GeometryRole.Button, 50f);
            Assert.That(theme.TryGetRadius(GeometryRole.Button, out float buttonHigh), Is.True);
            Assert.That(buttonHigh, Is.EqualTo(20f));

            // Card: bounds [0, 30]
            theme.SetRadius(GeometryRole.Card, 100f);
            Assert.That(theme.TryGetRadius(GeometryRole.Card, out float cardHigh), Is.True);
            Assert.That(cardHigh, Is.EqualTo(30f));
        }

        [Test]
        public void TestGeometrySparseOverridesAndClear()
        {
            var theme = new CustomThemeDefinition();
            theme.SetRadius(GeometryRole.Button, 14f);
            theme.SetRadius(GeometryRole.Input, 8f);
            theme.SetOverride(OverlayColour.Colour1, Colour4.Red);

            Assert.That(theme.TryGetRadius(GeometryRole.Button, out _), Is.True);
            Assert.That(theme.TryGetRadius(GeometryRole.Input, out _), Is.True);

            // Revert individual role
            Assert.That(theme.ClearRadius(GeometryRole.Button), Is.True);
            Assert.That(theme.TryGetRadius(GeometryRole.Button, out _), Is.False);
            Assert.That(theme.TryGetRadius(GeometryRole.Input, out _), Is.True);

            // ClearAllRadii does not affect color overrides
            theme.ClearAllRadii();
            Assert.That(theme.Geometry.IsEmpty, Is.True);
            Assert.That(theme.TryGetOverride(OverlayColour.Colour1, out _), Is.True);

            // ClearAllOverrides does not affect geometry
            theme.SetRadius(GeometryRole.Panel, 18f);
            theme.ClearAllOverrides();
            Assert.That(theme.Overrides, Is.Empty);
            Assert.That(theme.TryGetRadius(GeometryRole.Panel, out float panelRadius), Is.True);
            Assert.That(panelRadius, Is.EqualTo(18f));
        }

        [Test]
        public void TestGeometryDeepCloneAndEquality()
        {
            var original = new CustomThemeDefinition();
            original.SetRadius(GeometryRole.Button, 10f);
            original.SetRadius(GeometryRole.Card, 20f);

            var clone = original.DeepClone();
            Assert.That(clone, Is.EqualTo(original));
            Assert.That(clone.Geometry, Is.EqualTo(original.Geometry));

            clone.SetRadius(GeometryRole.Button, 15f);
            Assert.That(clone, Is.Not.EqualTo(original));
            Assert.That(original.TryGetRadius(GeometryRole.Button, out float origVal), Is.True);
            Assert.That(origVal, Is.EqualTo(10f));
        }

        [Test]
        public void TestGeometryUnknownFieldsPreserved()
        {
            string jsonWithFutureField = "{\"version\": 2, \"baseTheme\": \"Default\", \"overrides\": {}, \"geometry\": {\"buttonRadius\": 11, \"futureWidgetRadius\": 99}}";
            var theme = CustomThemeDefinition.FromJson(jsonWithFutureField);

            Assert.That(theme.TryGetRadius(GeometryRole.Button, out float buttonRadius), Is.True);
            Assert.That(buttonRadius, Is.EqualTo(11f));
            Assert.That(theme.Geometry.Extensions, Is.Not.Null);
            Assert.That(theme.Geometry.Extensions!.ContainsKey("futureWidgetRadius"), Is.True);

            string roundTripped = theme.ToJson();
            Assert.That(roundTripped, Does.Contain("futureWidgetRadius"));
        }

        [Test]
        public void TestOverlayColourProviderGeometryRadiusReactivity()
        {
            var buttonRadiusBindable = OverlayColourProvider.GetGeometryRadius(GeometryRole.Button);
            var cardRadiusBindable = OverlayColourProvider.GetGeometryRadius(GeometryRole.Card);

            // Under Default theme, returns defaults
            OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default;
            Assert.That(buttonRadiusBindable.Value, Is.EqualTo(OverlayColourProvider.GetDefaultRadius(GeometryRole.Button)));
            Assert.That(cardRadiusBindable.Value, Is.EqualTo(OverlayColourProvider.GetDefaultRadius(GeometryRole.Card)));

            // Switch to Custom theme with overrides
            var customTheme = new CustomThemeDefinition();
            customTheme.SetRadius(GeometryRole.Button, 14f);
            OverlayColourProvider.CustomTheme.Value = customTheme;
            OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom;

            Assert.That(buttonRadiusBindable.Value, Is.EqualTo(14f));
            // Card was not overridden, so it retains its default radius
            Assert.That(cardRadiusBindable.Value, Is.EqualTo(OverlayColourProvider.GetDefaultRadius(GeometryRole.Card)));

            // Live update custom theme geometry
            var updated = customTheme.DeepClone();
            updated.SetRadius(GeometryRole.Button, 2f);
            updated.SetRadius(GeometryRole.Card, 22f);
            OverlayColourProvider.CustomTheme.Value = updated;

            Assert.That(buttonRadiusBindable.Value, Is.EqualTo(2f));
            Assert.That(cardRadiusBindable.Value, Is.EqualTo(22f));

            // Reverting back to Default theme restores default radii
            OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default;
            Assert.That(buttonRadiusBindable.Value, Is.EqualTo(OverlayColourProvider.GetDefaultRadius(GeometryRole.Button)));
            Assert.That(cardRadiusBindable.Value, Is.EqualTo(OverlayColourProvider.GetDefaultRadius(GeometryRole.Card)));
        }

        [Test]
        public void TestOverlayColourProviderFallbackRadius()
        {
            float customFallback = 15f;
            var bindable = OverlayColourProvider.GetGeometryRadius(GeometryRole.Button, customFallback);

            OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default;
            Assert.That(bindable.Value, Is.EqualTo(customFallback));

            // Custom theme with explicit override overrides the fallback
            var customTheme = new CustomThemeDefinition();
            customTheme.SetRadius(GeometryRole.Button, 8f);
            OverlayColourProvider.CustomTheme.Value = customTheme;
            OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom;

            Assert.That(bindable.Value, Is.EqualTo(8f));

            // Clear radius override -> falls back to customFallback
            var cleared = customTheme.DeepClone();
            cleared.ClearRadius(GeometryRole.Button);
            OverlayColourProvider.CustomTheme.Value = cleared;

            Assert.That(bindable.Value, Is.EqualTo(customFallback));
        }

        [Test]
        public void TestOverlayColourProviderFallbackRadiusSurvivesGarbageCollection()
        {
            float customFallback = 15f;
            var bindable = OverlayColourProvider.GetGeometryRadius(GeometryRole.Button, customFallback);

            OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default;
            Assert.That(bindable.Value, Is.EqualTo(customFallback));

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var customTheme = new CustomThemeDefinition();
            customTheme.SetRadius(GeometryRole.Button, 8f);
            OverlayColourProvider.CustomTheme.Value = customTheme;
            OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom;

            Assert.That(bindable.Value, Is.EqualTo(8f));

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var cleared = customTheme.DeepClone();
            cleared.ClearRadius(GeometryRole.Button);
            OverlayColourProvider.CustomTheme.Value = cleared;

            Assert.That(bindable.Value, Is.EqualTo(customFallback));
        }

        [Test]
        public void TestCustomThemeDefinitionEqualsNullGeometry()
        {
            var a = new CustomThemeDefinition();
            var b = new CustomThemeDefinition();
            a.Geometry = null!;
            b.Geometry = null!;
            Assert.That(a.Equals(b), Is.True);

            b.Geometry = new ThemeGeometryDefinition();
            Assert.That(a.Equals(b), Is.False);
            Assert.That(b.Equals(a), Is.False);
        }

        [Test]
        public void TestV2ToV3MigrationCompatibility()
        {
            string v2Json = "{\"version\": 2, \"baseTheme\": \"Dark\", \"overrides\": {}, \"geometry\": {\"buttonRadius\": 10, \"panelRadius\": 14}}";
            var def = CustomThemeDefinition.FromJson(v2Json);

            Assert.That(def, Is.Not.Null);
            Assert.That(def.BaseTheme, Is.EqualTo(ThemeMode.Dark));
            Assert.That(def.TopBarVariant, Is.EqualTo(TopBarVariant.Flat));
            Assert.That(def.TopBarRadius, Is.Null);
            Assert.That(def.TryGetRadius(GeometryRole.Button, out float buttonRadius), Is.True);
            Assert.That(buttonRadius, Is.EqualTo(10f));
            Assert.That(def.TryGetRadius(GeometryRole.Panel, out float panelRadius), Is.True);
            Assert.That(panelRadius, Is.EqualTo(14f));
        }

        [TestCase(TopBarVariant.Flat)]
        [TestCase(TopBarVariant.Rounded)]
        [TestCase(TopBarVariant.Attached)]
        [TestCase(TopBarVariant.Floating)]
        [TestCase(TopBarVariant.DownwardRounded)]
        public void TestTopBarVariantSerializationRoundTrip(TopBarVariant variant)
        {
            var original = new CustomThemeDefinition(ThemeMode.Default);
            original.TopBarVariant = variant;
            original.TopBarRadius = 16f;

            string json = original.ToJson();
            var restored = CustomThemeDefinition.FromJson(json);

            Assert.That(restored.TopBarVariant, Is.EqualTo(variant));
            Assert.That(restored.TopBarRadius, Is.EqualTo(16f));
            Assert.That(restored, Is.EqualTo(original));
        }

        [Test]
        public void TestTopBarRadiusClamping()
        {
            var theme = new CustomThemeDefinition();

            theme.TopBarRadius = -5f;
            Assert.That(theme.TopBarRadius, Is.EqualTo(0f));

            theme.TopBarRadius = 50f;
            Assert.That(theme.TopBarRadius, Is.EqualTo(24f));

            float safeFallback = ThemeGeometryDefinition.ClampTopBarRadius(float.NaN);
            Assert.That(safeFallback, Is.EqualTo(10f));
        }

        [Test]
        public void TestTopBarReactivePropagation()
        {
            var variantBindable = OverlayColourProvider.GetTopBarVariant();
            var radiusBindable = OverlayColourProvider.GetTopBarRadius();

            // Under Default theme, returns Flat and inherits panel radius
            OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default;
            Assert.That(variantBindable.Value, Is.EqualTo(TopBarVariant.Flat));
            Assert.That(radiusBindable.Value, Is.EqualTo(OverlayColourProvider.GetDefaultRadius(GeometryRole.Panel)));

            // Switch to Custom theme with Floating variant and custom radius
            var customTheme = new CustomThemeDefinition();
            customTheme.TopBarVariant = TopBarVariant.Floating;
            customTheme.TopBarRadius = 18f;
            OverlayColourProvider.CustomTheme.Value = customTheme;
            OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom;

            Assert.That(variantBindable.Value, Is.EqualTo(TopBarVariant.Floating));
            Assert.That(radiusBindable.Value, Is.EqualTo(18f));

            // Null TopBarRadius inherits from panel radius
            var nullRadiusTheme = customTheme.DeepClone();
            nullRadiusTheme.TopBarRadius = null;
            nullRadiusTheme.SetRadius(GeometryRole.Panel, 12f);
            OverlayColourProvider.CustomTheme.Value = nullRadiusTheme;

            Assert.That(variantBindable.Value, Is.EqualTo(TopBarVariant.Floating));
            Assert.That(radiusBindable.Value, Is.EqualTo(12f));

            // Reverting to Default theme restores Flat
            OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default;
            Assert.That(variantBindable.Value, Is.EqualTo(TopBarVariant.Flat));
        }

        [TestCase(typeof(osu.Game.Graphics.UserInterfaceV2.RoundedButton), true)]
        [TestCase(typeof(osu.Game.Graphics.UserInterface.OsuMenu), true)]
        [TestCase(typeof(osu.Game.Overlays.Volume.MuteButton), false)]
        [TestCase(typeof(osu.Game.Screens.Edit.Components.Menus.EditorMenuBar), false)]
        [TestCase(typeof(osu.Game.Screens.Edit.Components.Timelines.Summary.TestGameplayButton), false)]
        [TestCase(typeof(osu.Game.Graphics.UserInterfaceV2.FormButton.Button), false)]
        [TestCase(typeof(osu.Game.Screens.Edit.Components.FormSampleSet.SampleButton), false)]
        public void TestExemptControlsOptOutOfGenericGeometryRadius(Type type, bool expectedApply)
        {
            var prop = type.GetProperty("ApplyGeometryRadius", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            Assert.That(prop, Is.Not.Null, $"{type.Name} must declare or inherit ApplyGeometryRadius");

            var obj = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(type);
            var val = (bool)prop!.GetValue(obj)!;
            Assert.That(val, Is.EqualTo(expectedApply), $"{type.Name}.ApplyGeometryRadius expected {expectedApply}");
        }

        [Test]
        public void TestNestedExemptControlsOptOutOfGenericGeometryRadius()
        {
            var subMenuType = typeof(osu.Game.Screens.Edit.Components.Menus.EditorMenuBar).GetNestedType("SubMenu", System.Reflection.BindingFlags.NonPublic);
            Assert.That(subMenuType, Is.Not.Null);
            var subMenuProp = subMenuType!.GetProperty("ApplyGeometryRadius", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            Assert.That(subMenuProp, Is.Not.Null);
            var subMenuObj = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(subMenuType);
            Assert.That((bool)subMenuProp!.GetValue(subMenuObj)!, Is.False);

            var inlineButtonType = typeof(osu.Game.Screens.Edit.Timing.TapTimingControl).GetNestedType("InlineButton", System.Reflection.BindingFlags.NonPublic);
            Assert.That(inlineButtonType, Is.Not.Null);
            var inlineButtonProp = inlineButtonType!.GetProperty("ApplyGeometryRadius", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            Assert.That(inlineButtonProp, Is.Not.Null);
            var inlineButtonObj = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(inlineButtonType);
            Assert.That((bool)inlineButtonProp!.GetValue(inlineButtonObj)!, Is.False);
        }

        [Test]
        public void TestClearAllRadiiPreservesTopBarVariant()
        {
            var def = new CustomThemeDefinition();
            def.SetRadius(GeometryRole.Button, 12f);
            def.TopBarRadius = 14f;
            def.TopBarVariant = TopBarVariant.Floating;

            def.ClearAllRadii();

            Assert.That(def.TopBarVariant, Is.EqualTo(TopBarVariant.Floating));
            Assert.That(def.TopBarRadius, Is.Null);
            Assert.That(def.TryGetRadius(GeometryRole.Button, out _), Is.False);

            def.Geometry.ClearAll();
            Assert.That(def.TopBarVariant, Is.EqualTo(TopBarVariant.Flat));
        }

        [Test]
        public void TestContextOverridesJsonRoundTrip()
        {
            var original = new CustomThemeDefinition(ThemeMode.Dark);
            original.SetContextOverride(ThemeSurfaceContext.SongSelect, SurfaceColourRole.SurfaceBackground, Colour4.FromHex("#112233"));
            original.SetContextOverride(ThemeSurfaceContext.SongSelect, SurfaceColourRole.Accent, Colour4.FromHex("#FF5588"));
            original.SetContextOverride(ThemeSurfaceContext.InfiniteGlass, SurfaceColourRole.TextPrimary, Colour4.White);

            string json = original.ToJson();
            var restored = CustomThemeDefinition.FromJson(json);

            Assert.That(restored.Version, Is.EqualTo(CustomThemeDefinition.CURRENT_VERSION));
            Assert.That(restored.TryGetContextOverride(ThemeSurfaceContext.SongSelect, SurfaceColourRole.SurfaceBackground, out var bgCol), Is.True);
            Assert.That(bgCol, Is.EqualTo(Colour4.FromHex("#112233")));
            Assert.That(restored.TryGetContextOverride(ThemeSurfaceContext.SongSelect, SurfaceColourRole.Accent, out var accentCol), Is.True);
            Assert.That(accentCol, Is.EqualTo(Colour4.FromHex("#FF5588")));
            Assert.That(restored.TryGetContextOverride(ThemeSurfaceContext.InfiniteGlass, SurfaceColourRole.TextPrimary, out var textCol), Is.True);
            Assert.That(textCol, Is.EqualTo(Colour4.White));
            Assert.That(restored.TryGetContextOverride(ThemeSurfaceContext.ModSelect, SurfaceColourRole.SurfaceBackground, out _), Is.False);
            Assert.That(restored, Is.EqualTo(original));
        }

        [Test]
        public void TestV3ToV4ContextMigrationCompatibility()
        {
            string v3Json = "{\"version\": 3, \"baseTheme\": \"Light\", \"overrides\": {\"Highlight1\": \"#123456\"}, \"geometry\": {\"topBarVariant\": \"Floating\", \"topBarRadius\": 14}}";
            var def = CustomThemeDefinition.FromJson(v3Json);

            Assert.That(def, Is.Not.Null);
            Assert.That(def.Version, Is.EqualTo(CustomThemeDefinition.CURRENT_VERSION));
            Assert.That(def.BaseTheme, Is.EqualTo(ThemeMode.Light));
            Assert.That(def.TopBarVariant, Is.EqualTo(TopBarVariant.Floating));
            Assert.That(def.TopBarRadius, Is.EqualTo(14f));
            Assert.That(def.ContextOverrides, Is.Not.Null);
            Assert.That(def.ContextOverrides, Is.Empty);
            Assert.That(def.TryGetContextOverride(ThemeSurfaceContext.SongSelect, SurfaceColourRole.SurfaceBackground, out _), Is.False);
        }

        [Test]
        public void TestMalformedContextOverridesNullDictionaryDoesNotThrow()
        {
            // JSON with null inner dictionary
            string malformedJson = "{\"version\": 4, \"contextOverrides\": {\"SongSelect\": null}}";
            var def = CustomThemeDefinition.FromJson(malformedJson);

            Assert.That(def, Is.Not.Null);
            Assert.That(def.ContextOverrides, Is.Not.Null);
            Assert.That(def.TryGetContextOverride(ThemeSurfaceContext.SongSelect, SurfaceColourRole.SurfaceBackground, out _), Is.False);

            // Manual injection of null dictionary in memory
            def.ContextOverrides[ThemeSurfaceContext.SongSelect] = null!;
            Assert.DoesNotThrow(() => def.TryGetContextOverride(ThemeSurfaceContext.SongSelect, SurfaceColourRole.SurfaceBackground, out _));
            Assert.DoesNotThrow(() => def.ClearContextOverride(ThemeSurfaceContext.SongSelect, SurfaceColourRole.SurfaceBackground));
            Assert.DoesNotThrow(() => def.SetContextOverride(ThemeSurfaceContext.SongSelect, SurfaceColourRole.SurfaceBackground, Colour4.Red));
            Assert.DoesNotThrow(() => def.DeepClone());
            Assert.DoesNotThrow(() => _ = def.GetHashCode());
            Assert.DoesNotThrow(() => _ = def.Equals(new CustomThemeDefinition()));
        }

        [Test]
        public void TestUnscopedOverlayColourProviderConsistency()
        {
            // Un-scoped provider (SurfaceContext is null)
            var provider = new OverlayColourProvider(OverlayColourScheme.Blue);
            OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom;

            var theme = new CustomThemeDefinition(ThemeMode.Default);
            theme.SetContextOverride(ThemeSurfaceContext.SongSelect, SurfaceColourRole.SurfaceBackground, Colour4.FromHex("#FF1122"));
            OverlayColourProvider.CustomTheme.Value = theme;

            // GetSurfaceColour should NOT return SongSelect override when provider is unscoped
            Color4 surfaceColour = provider.GetSurfaceColour(SurfaceColourRole.SurfaceBackground);
            Color4 bindableColour = provider.GetSurfaceColourBindable(SurfaceColourRole.SurfaceBackground).Value;

            Assert.That((Colour4)surfaceColour, Is.Not.EqualTo(Colour4.FromHex("#FF1122")));
            Assert.That((Colour4)surfaceColour, Is.EqualTo((Colour4)bindableColour));
        }

        [Test]
        public void TestContextInheritanceFallback()
        {
            var provider = new OverlayColourProvider(OverlayColourScheme.Blue, ThemeSurfaceContext.SongSelect);

            OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom;
            var theme = new CustomThemeDefinition(ThemeMode.Default);
            OverlayColourProvider.CustomTheme.Value = theme;

            // Default base calculation when no overrides exist
            var baseBg = provider.GetInheritedColour(OverlayColour.Background3, ThemeMode.Default);
            Assert.That((Colour4)provider.Background3, Is.EqualTo((Colour4)baseBg));

            // Set global semantic override for Background3
            var withGlobal = theme.DeepClone();
            withGlobal.SetOverride(OverlayColour.Background3, Colour4.FromHex("#334455"));
            OverlayColourProvider.CustomTheme.Value = withGlobal;

            Assert.That((Colour4)provider.Background3, Is.EqualTo(Colour4.FromHex("#334455")));

            // Set context-specific override for SongSelect SurfaceBackground -> should take precedence over global override
            var withContext = withGlobal.DeepClone();
            withContext.SetContextOverride(ThemeSurfaceContext.SongSelect, SurfaceColourRole.SurfaceBackground, Colour4.FromHex("#778899"));
            OverlayColourProvider.CustomTheme.Value = withContext;

            Assert.That((Colour4)provider.Background3, Is.EqualTo(Colour4.FromHex("#778899")));

            // Another provider without this context retains the global override
            var otherProvider = new OverlayColourProvider(OverlayColourScheme.Blue, ThemeSurfaceContext.Editor);
            Assert.That((Colour4)otherProvider.Background3, Is.EqualTo(Colour4.FromHex("#334455")));
        }

        [Test]
        public void TestClearContextOverrideAndClearAll()
        {
            var theme = new CustomThemeDefinition();
            theme.SetContextOverride(ThemeSurfaceContext.SongSelect, SurfaceColourRole.SurfaceBackground, Colour4.Red);
            theme.SetContextOverride(ThemeSurfaceContext.SongSelect, SurfaceColourRole.Accent, Colour4.Green);
            theme.SetContextOverride(ThemeSurfaceContext.ModSelect, SurfaceColourRole.SurfaceBackground, Colour4.Blue);
            theme.SetOverride(OverlayColour.Colour1, Colour4.Yellow);

            Assert.That(theme.TryGetContextOverride(ThemeSurfaceContext.SongSelect, SurfaceColourRole.SurfaceBackground, out _), Is.True);
            Assert.That(theme.TryGetContextOverride(ThemeSurfaceContext.SongSelect, SurfaceColourRole.Accent, out _), Is.True);
            Assert.That(theme.TryGetContextOverride(ThemeSurfaceContext.ModSelect, SurfaceColourRole.SurfaceBackground, out _), Is.True);

            // Clear single role in SongSelect
            Assert.That(theme.ClearContextOverride(ThemeSurfaceContext.SongSelect, SurfaceColourRole.SurfaceBackground), Is.True);
            Assert.That(theme.TryGetContextOverride(ThemeSurfaceContext.SongSelect, SurfaceColourRole.SurfaceBackground, out _), Is.False);
            Assert.That(theme.TryGetContextOverride(ThemeSurfaceContext.SongSelect, SurfaceColourRole.Accent, out _), Is.True);

            // Clear all in ModSelect
            Assert.That(theme.ClearContextOverrides(ThemeSurfaceContext.ModSelect), Is.True);
            Assert.That(theme.TryGetContextOverride(ThemeSurfaceContext.ModSelect, SurfaceColourRole.SurfaceBackground, out _), Is.False);

            // ClearAllOverrides clears all contexts and global overrides
            theme.ClearAllOverrides();
            Assert.That(theme.Overrides, Is.Empty);
            Assert.That(theme.ContextOverrides, Is.Empty);
        }

        [Test]
        public void TestOverlayColourProviderContextSurfaceMethods()
        {
            var provider = new OverlayColourProvider(OverlayColourScheme.Blue, ThemeSurfaceContext.InfiniteGlass);
            OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom;

            var theme = new CustomThemeDefinition();
            theme.SetContextOverride(ThemeSurfaceContext.InfiniteGlass, SurfaceColourRole.SurfaceBackground, Colour4.FromHex("#050510"));
            OverlayColourProvider.CustomTheme.Value = theme;

            Assert.That((Colour4)provider.GetSurfaceColour(SurfaceColourRole.SurfaceBackground), Is.EqualTo(Colour4.FromHex("#050510")));

            var bindable = provider.GetSurfaceColourBindable(SurfaceColourRole.SurfaceBackground);
            Assert.That(bindable.Value, Is.EqualTo(Colour4.FromHex("#050510")));

            var updated = theme.DeepClone();
            updated.SetContextOverride(ThemeSurfaceContext.InfiniteGlass, SurfaceColourRole.SurfaceBackground, Colour4.FromHex("#151520"));
            OverlayColourProvider.CustomTheme.Value = updated;

            Assert.That((Colour4)provider.GetSurfaceColour(SurfaceColourRole.SurfaceBackground), Is.EqualTo(Colour4.FromHex("#151520")));
            Assert.That(bindable.Value, Is.EqualTo(Colour4.FromHex("#151520")));
        }

        [Test]
        public void TestSchemaVersion5MigrationAndRoundtrip()
        {
            string v4Json = "{\"version\": 4, \"baseTheme\": \"Dark\", \"overrides\": {}, \"contextOverrides\": {\"TopBar\": {\"SurfaceBackground\": \"#123456\"}}}";
            var def = CustomThemeDefinition.FromJson(v4Json);
            Assert.That(def.Version, Is.EqualTo(CustomThemeDefinition.CURRENT_VERSION));
            Assert.That(def.Version, Is.EqualTo(7));
            Assert.That(def.TryGetContextOverride(ThemeSurfaceContext.TopBar, SurfaceColourRole.SurfaceBackground, out var col), Is.True);
            Assert.That(col, Is.EqualTo(Colour4.FromHex("#123456")));

            string json = def.ToJson();
            var roundtrip = CustomThemeDefinition.FromJson(json);
            Assert.That(roundtrip.Version, Is.EqualTo(7));
            Assert.That(roundtrip.TryGetContextOverride(ThemeSurfaceContext.TopBar, SurfaceColourRole.SurfaceBackground, out var col2), Is.True);
            Assert.That(col2, Is.EqualTo(Colour4.FromHex("#123456")));
        }

        [Test]
        public void TestTopBarAndResultsContextOverrides()
        {
            var topBarProvider = new OverlayColourProvider(OverlayColourScheme.Blue, ThemeSurfaceContext.TopBar);
            var resultsProvider = new OverlayColourProvider(OverlayColourScheme.Blue, ThemeSurfaceContext.Results);
            OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom;

            var theme = new CustomThemeDefinition();
            theme.SetContextOverride(ThemeSurfaceContext.TopBar, SurfaceColourRole.SurfaceBackground, Colour4.FromHex("#112233"));
            theme.SetContextOverride(ThemeSurfaceContext.TopBar, SurfaceColourRole.ControlHover, Colour4.FromHex("#336699"));
            theme.SetContextOverride(ThemeSurfaceContext.TopBar, SurfaceColourRole.Accent, Colour4.FromHex("#FF5500"));
            theme.SetContextOverride(ThemeSurfaceContext.Results, SurfaceColourRole.SurfaceBackground, Colour4.FromHex("#223344"));
            theme.SetContextOverride(ThemeSurfaceContext.Results, SurfaceColourRole.SurfaceElevated, Colour4.FromHex("#334455"));
            theme.SetContextOverride(ThemeSurfaceContext.Results, SurfaceColourRole.Accent, Colour4.FromHex("#00CCFF"));
            OverlayColourProvider.CustomTheme.Value = theme;

            Assert.That((Colour4)topBarProvider.Background5, Is.EqualTo(Colour4.FromHex("#112233")));
            Assert.That((Colour4)topBarProvider.Light4, Is.EqualTo(Colour4.FromHex("#336699")));
            Assert.That((Colour4)topBarProvider.GetSurfaceColour(SurfaceColourRole.ControlHover), Is.EqualTo(Colour4.FromHex("#336699")));
            Assert.That((Colour4)topBarProvider.Colour3, Is.EqualTo(Colour4.FromHex("#FF5500")));
            Assert.That((Colour4)topBarProvider.GetSurfaceColour(SurfaceColourRole.Accent), Is.EqualTo(Colour4.FromHex("#FF5500")));
            Assert.That((Colour4)resultsProvider.Background5, Is.EqualTo(Colour4.FromHex("#223344")));
            Assert.That((Colour4)resultsProvider.Background4, Is.EqualTo(Colour4.FromHex("#334455")));
            Assert.That((Colour4)resultsProvider.GetSurfaceColour(SurfaceColourRole.SurfaceElevated), Is.EqualTo(Colour4.FromHex("#334455")));
            Assert.That((Colour4)resultsProvider.Highlight1, Is.EqualTo(Colour4.FromHex("#00CCFF")));
            Assert.That((Colour4)resultsProvider.GetSurfaceColour(SurfaceColourRole.Accent), Is.EqualTo(Colour4.FromHex("#00CCFF")));
        }

        [Test]
        public void TestEditorContextOverridesAllRoles()
        {
            var editorProvider = new OverlayColourProvider(OverlayColourScheme.Blue, ThemeSurfaceContext.Editor);
            OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom;

            var theme = new CustomThemeDefinition();
            theme.SetContextOverride(ThemeSurfaceContext.Editor, SurfaceColourRole.SurfaceElevated, Colour4.FromHex("#101010"));
            theme.SetContextOverride(ThemeSurfaceContext.Editor, SurfaceColourRole.SurfaceBackground, Colour4.FromHex("#202020"));
            theme.SetContextOverride(ThemeSurfaceContext.Editor, SurfaceColourRole.ControlIdle, Colour4.FromHex("#303030"));
            theme.SetContextOverride(ThemeSurfaceContext.Editor, SurfaceColourRole.ControlHover, Colour4.FromHex("#404040"));
            theme.SetContextOverride(ThemeSurfaceContext.Editor, SurfaceColourRole.ControlSelected, Colour4.FromHex("#505050"));
            theme.SetContextOverride(ThemeSurfaceContext.Editor, SurfaceColourRole.Accent, Colour4.FromHex("#606060"));
            theme.SetContextOverride(ThemeSurfaceContext.Editor, SurfaceColourRole.TextPrimary, Colour4.FromHex("#707070"));
            theme.SetContextOverride(ThemeSurfaceContext.Editor, SurfaceColourRole.TextSecondary, Colour4.FromHex("#808080"));
            OverlayColourProvider.CustomTheme.Value = theme;

            Assert.That((Colour4)editorProvider.Background5, Is.EqualTo(Colour4.FromHex("#101010")));
            Assert.That((Colour4)editorProvider.Background4, Is.EqualTo(Colour4.FromHex("#101010")));
            Assert.That((Colour4)editorProvider.Background6, Is.EqualTo(Colour4.FromHex("#202020")));
            Assert.That((Colour4)editorProvider.Background3, Is.EqualTo(Colour4.FromHex("#303030")));
            Assert.That((Colour4)editorProvider.Background2, Is.EqualTo(Colour4.FromHex("#404040")));
            Assert.That((Colour4)editorProvider.Background1, Is.EqualTo(Colour4.FromHex("#505050")));
            Assert.That((Colour4)editorProvider.Light3, Is.EqualTo(Colour4.FromHex("#606060")));
            Assert.That((Colour4)editorProvider.Colour3, Is.EqualTo(Colour4.FromHex("#606060")));
            Assert.That((Colour4)editorProvider.Content1, Is.EqualTo(Colour4.FromHex("#707070")));
            Assert.That((Colour4)editorProvider.Content2, Is.EqualTo(Colour4.FromHex("#808080")));
        }

        [Test]
        public void TestUserProfileAccentExemptFromCustomTheme()
        {
            var profileProvider = new OverlayColourProvider(OverlayColourScheme.Green)
            {
                ExemptAccentFromCustomTheme = true
            };

            OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom;

            var customTheme = new CustomThemeDefinition();
            customTheme.SetOverride(OverlayColour.Highlight1, Colour4.Red);
            customTheme.SetOverride(OverlayColour.Colour3, Colour4.Blue);
            OverlayColourProvider.CustomTheme.Value = customTheme;

            // Highlight1 and Colour3 should calculate from profileProvider.Hue (Green hue ~130), NOT from custom theme Red/Blue overrides!
            Assert.That((Colour4)profileProvider.Highlight1, Is.Not.EqualTo(Colour4.Red));
            Assert.That((Colour4)profileProvider.Colour3, Is.Not.EqualTo(Colour4.Blue));

            var nonExemptProvider = new OverlayColourProvider(OverlayColourScheme.Green)
            {
                ExemptAccentFromCustomTheme = false
            };
            Assert.That((Colour4)nonExemptProvider.Highlight1, Is.EqualTo(Colour4.Red));
            Assert.That((Colour4)nonExemptProvider.Colour3, Is.EqualTo(Colour4.Blue));
        }

        [Test]
        public void TestModSelectContextDefaultHueIsGreen()
        {
            Assert.That(OverlayColourProvider.GetDefaultHueForContext(ThemeSurfaceContext.ModSelect), Is.EqualTo(125));
            var defaultProvider = new OverlayColourProvider(OverlayColourScheme.Purple);
            var greenDefault = defaultProvider.GetInheritedContextColour(ThemeSurfaceContext.ModSelect, SurfaceColourRole.SurfaceBackground);
            // Default background should have hue 125, not 255 (purple)
            var (sat, light) = OverlayColourProvider.GetDefaultParametersFor(OverlayColour.Background3);
            var expectedGreen = OverlayColourProvider.CalculateBaseColourForHue(125, ThemeMode.Default, sat, light);
            Assert.That(greenDefault, Is.EqualTo(expectedGreen));
        }

        [Test]
        public void TestEditorContextDefaultHueIsAquamarine()
        {
            Assert.That(OverlayColourProvider.GetDefaultHueForContext(ThemeSurfaceContext.Editor), Is.EqualTo(160));
            var defaultProvider = new OverlayColourProvider(OverlayColourScheme.Purple);
            var aquamarineDefault = defaultProvider.GetInheritedContextColour(ThemeSurfaceContext.Editor, SurfaceColourRole.SurfaceElevated);
            var (sat, light) = OverlayColourProvider.GetDefaultParametersFor(OverlayColour.Background5);
            var expectedAquamarine = OverlayColourProvider.CalculateBaseColourForHue(160, ThemeMode.Default, sat, light);
            Assert.That(aquamarineDefault, Is.EqualTo(expectedAquamarine));
        }

        [Test]
        public void TestModSelectContextOverridesAllRoles()
        {
            var modSelectProvider = new OverlayColourProvider(OverlayColourScheme.Green, ThemeSurfaceContext.ModSelect);
            OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom;

            var theme = new CustomThemeDefinition();
            theme.SetContextOverride(ThemeSurfaceContext.ModSelect, SurfaceColourRole.SurfaceBackground, Colour4.FromHex("#112233"));
            theme.SetContextOverride(ThemeSurfaceContext.ModSelect, SurfaceColourRole.ControlIdle, Colour4.FromHex("#223344"));
            theme.SetContextOverride(ThemeSurfaceContext.ModSelect, SurfaceColourRole.SurfaceElevated, Colour4.FromHex("#334455"));
            theme.SetContextOverride(ThemeSurfaceContext.ModSelect, SurfaceColourRole.ControlHover, Colour4.FromHex("#445566"));
            theme.SetContextOverride(ThemeSurfaceContext.ModSelect, SurfaceColourRole.Accent, Colour4.FromHex("#556677"));
            theme.SetContextOverride(ThemeSurfaceContext.ModSelect, SurfaceColourRole.TextPrimary, Colour4.FromHex("#667788"));
            theme.SetContextOverride(ThemeSurfaceContext.ModSelect, SurfaceColourRole.TextSecondary, Colour4.FromHex("#778899"));
            OverlayColourProvider.CustomTheme.Value = theme;

            Assert.That((Colour4)modSelectProvider.Background3, Is.EqualTo(Colour4.FromHex("#112233")));
            Assert.That((Colour4)modSelectProvider.Background2, Is.EqualTo(Colour4.FromHex("#223344")));
            Assert.That((Colour4)modSelectProvider.Background4, Is.EqualTo(Colour4.FromHex("#334455")));
            Assert.That((Colour4)modSelectProvider.Light4, Is.EqualTo(Colour4.FromHex("#445566")));
            Assert.That((Colour4)modSelectProvider.Highlight1, Is.EqualTo(Colour4.FromHex("#556677")));
            Assert.That((Colour4)modSelectProvider.Content1, Is.EqualTo(Colour4.FromHex("#667788")));
            Assert.That((Colour4)modSelectProvider.Content2, Is.EqualTo(Colour4.FromHex("#778899")));
        }

        [Test]
        public void TestPaletteVersionIncrementsOnThemeChange()
        {
            var provider = new OverlayColourProvider(OverlayColourScheme.Green, ThemeSurfaceContext.ModSelect);
            int initialVersion = provider.PaletteVersion.Value;

            OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom;
            var theme = new CustomThemeDefinition();
            theme.SetContextOverride(ThemeSurfaceContext.ModSelect, SurfaceColourRole.SurfaceBackground, Colour4.Yellow);
            OverlayColourProvider.CustomTheme.Value = theme;

            Assert.That(provider.PaletteVersion.Value, Is.GreaterThan(initialVersion));
        }

        [Test]
        public void TestClearFooterOverridesPreservesOtherContexts()
        {
            var def = new CustomThemeDefinition();
            def.SetContextOverride(ThemeSurfaceContext.Footer, SurfaceColourRole.SurfaceBackground, Colour4.Red);
            def.SetContextOverride(ThemeSurfaceContext.FooterMods, SurfaceColourRole.Accent, Colour4.Green);
            def.SetContextOverride(ThemeSurfaceContext.SongSelect, SurfaceColourRole.SurfaceBackground, Colour4.Blue);
            def.SetContextOverride(ThemeSurfaceContext.Editor, SurfaceColourRole.Accent, Colour4.Yellow);

            Assert.That(def.TryGetContextOverride(ThemeSurfaceContext.Footer, SurfaceColourRole.SurfaceBackground, out _), Is.True);
            Assert.That(def.TryGetContextOverride(ThemeSurfaceContext.FooterMods, SurfaceColourRole.Accent, out _), Is.True);
            Assert.That(def.TryGetContextOverride(ThemeSurfaceContext.SongSelect, SurfaceColourRole.SurfaceBackground, out _), Is.True);
            Assert.That(def.TryGetContextOverride(ThemeSurfaceContext.Editor, SurfaceColourRole.Accent, out _), Is.True);

            def.ClearFooterOverrides();

            Assert.That(def.TryGetContextOverride(ThemeSurfaceContext.Footer, SurfaceColourRole.SurfaceBackground, out _), Is.False);
            Assert.That(def.TryGetContextOverride(ThemeSurfaceContext.FooterMods, SurfaceColourRole.Accent, out _), Is.False);
            Assert.That(def.TryGetContextOverride(ThemeSurfaceContext.SongSelect, SurfaceColourRole.SurfaceBackground, out var songCol), Is.True);
            Assert.That(songCol, Is.EqualTo(Colour4.Blue));
            Assert.That(def.TryGetContextOverride(ThemeSurfaceContext.Editor, SurfaceColourRole.Accent, out var editCol), Is.True);
            Assert.That(editCol, Is.EqualTo(Colour4.Yellow));
        }

        [Test]
        public void TestEnableGradientsJsonRoundtrip()
        {
            var def = new CustomThemeDefinition();
            Assert.That(def.EnableGradients, Is.True);

            def.EnableGradients = false;
            string json = def.ToJson();
            Assert.That(json, Does.Contain("\"enableGradients\":false"));

            var roundtrip = CustomThemeDefinition.FromJson(json);
            Assert.That(roundtrip.EnableGradients, Is.False);
        }

        [Test]
        public void TestModTypeOverridesJsonRoundtripAndMethods()
        {
            var def = new CustomThemeDefinition();
            Assert.That(def.TryGetModTypeOverride(osu.Game.Rulesets.Mods.ModType.DifficultyReduction, out _), Is.False);

            def.SetModTypeOverride(osu.Game.Rulesets.Mods.ModType.DifficultyReduction, Colour4.Lime);
            def.SetModTypeOverride(osu.Game.Rulesets.Mods.ModType.DifficultyIncrease, Colour4.Crimson);

            Assert.That(def.TryGetModTypeOverride(osu.Game.Rulesets.Mods.ModType.DifficultyReduction, out var diffRed), Is.True);
            Assert.That(diffRed, Is.EqualTo(Colour4.Lime));
            Assert.That(def.TryGetModTypeOverride(osu.Game.Rulesets.Mods.ModType.DifficultyIncrease, out var diffInc), Is.True);
            Assert.That(diffInc, Is.EqualTo(Colour4.Crimson));

            string json = def.ToJson();
            var roundtrip = CustomThemeDefinition.FromJson(json);

            Assert.That(roundtrip.TryGetModTypeOverride(osu.Game.Rulesets.Mods.ModType.DifficultyReduction, out var rtDiffRed), Is.True);
            Assert.That(rtDiffRed, Is.EqualTo(Colour4.Lime));
            Assert.That(roundtrip.TryGetModTypeOverride(osu.Game.Rulesets.Mods.ModType.DifficultyIncrease, out var rtDiffInc), Is.True);
            Assert.That(rtDiffInc, Is.EqualTo(Colour4.Crimson));

            // Test clear individual
            roundtrip.ClearModTypeOverride(osu.Game.Rulesets.Mods.ModType.DifficultyReduction);
            Assert.That(roundtrip.TryGetModTypeOverride(osu.Game.Rulesets.Mods.ModType.DifficultyReduction, out _), Is.False);
            Assert.That(roundtrip.TryGetModTypeOverride(osu.Game.Rulesets.Mods.ModType.DifficultyIncrease, out _), Is.True);

            // Test clear all
            roundtrip.ClearModTypeOverrides();
            Assert.That(roundtrip.TryGetModTypeOverride(osu.Game.Rulesets.Mods.ModType.DifficultyIncrease, out _), Is.False);
        }

        [Test]
        public void TestModTypeColourOnlyAppliesInModSelectContext()
        {
            var osuColours = new osu.Game.Graphics.OsuColour();
            var modSelectProvider = new OverlayColourProvider(OverlayColourScheme.Green, ThemeSurfaceContext.ModSelect);
            var generalProvider = new OverlayColourProvider(OverlayColourScheme.Green, ThemeSurfaceContext.SongSelect);

            var defaultRedColour = osuColours.ForModType(osu.Game.Rulesets.Mods.ModType.DifficultyReduction);
            var customColour = Colour4.FromHex("#123456");

            // Under default theme mode, should always be default color
            Assert.That(modSelectProvider.GetModTypeColour(osu.Game.Rulesets.Mods.ModType.DifficultyReduction, osuColours), Is.EqualTo((Color4)defaultRedColour));
            Assert.That(generalProvider.GetModTypeColour(osu.Game.Rulesets.Mods.ModType.DifficultyReduction, osuColours), Is.EqualTo((Color4)defaultRedColour));

            // Set custom theme with override
            OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom;
            var theme = new CustomThemeDefinition();
            theme.SetModTypeOverride(osu.Game.Rulesets.Mods.ModType.DifficultyReduction, customColour);
            OverlayColourProvider.CustomTheme.Value = theme;

            // Inside ModSelect, custom colour is returned
            Assert.That(modSelectProvider.GetModTypeColour(osu.Game.Rulesets.Mods.ModType.DifficultyReduction, osuColours), Is.EqualTo((Color4)customColour));

            // Outside ModSelect context (e.g. SongSelect / gameplay / anywhere else), default colour is preserved (no global leak!)
            Assert.That(generalProvider.GetModTypeColour(osu.Game.Rulesets.Mods.ModType.DifficultyReduction, osuColours), Is.EqualTo((Color4)defaultRedColour));

            // Unset ModType in custom theme falls back to default even in ModSelect
            Assert.That(modSelectProvider.GetModTypeColour(osu.Game.Rulesets.Mods.ModType.Automation, osuColours), Is.EqualTo((Color4)osuColours.ForModType(osu.Game.Rulesets.Mods.ModType.Automation)));
        }

        [Test]
        public void TestModSelectHeaderAndBackdropContextOverrides()
        {
            var modSelectProvider = new OverlayColourProvider(OverlayColourScheme.Green, ThemeSurfaceContext.ModSelect);

            OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom;
            var theme = new CustomThemeDefinition();
            theme.SetContextOverride(ThemeSurfaceContext.ModSelect, SurfaceColourRole.SurfaceBackground, Colour4.FromHex("#111111"));
            theme.SetContextOverride(ThemeSurfaceContext.ModSelect, SurfaceColourRole.HeaderBackground, Colour4.FromHex("#222222"));
            theme.SetContextOverride(ThemeSurfaceContext.ModSelect, SurfaceColourRole.Backdrop, Colour4.FromHex("#333333"));
            OverlayColourProvider.CustomTheme.Value = theme;

            // Card surface background resolves to Background3
            Assert.That((Colour4)modSelectProvider.Background3, Is.EqualTo(Colour4.FromHex("#111111")));
            // Header background resolves to Dark3
            Assert.That((Colour4)modSelectProvider.Dark3, Is.EqualTo(Colour4.FromHex("#222222")));
            // Overlay backdrop resolves to Background6
            Assert.That((Colour4)modSelectProvider.Background6, Is.EqualTo(Colour4.FromHex("#333333")));
        }

        [Test]
        public void TestResultsButtonsContextOverrideWithGreenFallback()
        {
            var osuColours = new osu.Game.Graphics.OsuColour();

            // Default: no override -> returns exact colours.Green
            var effectiveColDefault = OverlayColourProvider.GetEffectiveContextColour(
                ThemeSurfaceContext.Results,
                SurfaceColourRole.Accent,
                osuColours.Green);
            Assert.That((Color4)effectiveColDefault, Is.EqualTo(osuColours.Green));

            // Custom theme with Results Accent override
            OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom;
            var theme = new CustomThemeDefinition();
            theme.SetContextOverride(ThemeSurfaceContext.Results, SurfaceColourRole.Accent, Colour4.FromHex("#FF8800"));
            OverlayColourProvider.CustomTheme.Value = theme;

            var effectiveColCustom = OverlayColourProvider.GetEffectiveContextColour(
                ThemeSurfaceContext.Results,
                SurfaceColourRole.Accent,
                osuColours.Green);
            Assert.That(effectiveColCustom, Is.EqualTo(Colour4.FromHex("#FF8800")));

            // Custom theme without Results Accent override -> still falls back to exact green!
            var themeNoAccent = new CustomThemeDefinition();
            OverlayColourProvider.CustomTheme.Value = themeNoAccent;

            var effectiveColNoAccent = OverlayColourProvider.GetEffectiveContextColour(
                ThemeSurfaceContext.Results,
                SurfaceColourRole.Accent,
                osuColours.Green);
            Assert.That((Color4)effectiveColNoAccent, Is.EqualTo(osuColours.Green));
        }

        [Test]
        public void TestThemeShareExportAndImportRoundTrip()
        {
            using var storage = new TemporaryNativeStorage($"theme-share-test-{Guid.NewGuid():N}");

            var theme = new CustomThemeDefinition(ThemeMode.Dark);
            theme.SetOverride(OverlayColour.Colour1, Colour4.FromHex("#FF55AA"));
            theme.SetOverride(OverlayColour.Content1, Colour4.FromHex("#112233"));
            theme.SetContextOverride(ThemeSurfaceContext.ModSelect, SurfaceColourRole.SurfaceBackground, Colour4.FromHex("#223344"));
            theme.SetModTypeOverride(osu.Game.Rulesets.Mods.ModType.DifficultyReduction, Colour4.FromHex("#445566"));
            theme.Geometry.ButtonRadius = 14f;
            theme.Geometry.TopBarVariant = TopBarVariant.Rounded;

            bool exportResult = ThemeShareManager.TryExportTheme(theme, storage, "CyberpunkTheme", out string savedFileName, out string? exportError);
            Assert.That(exportResult, Is.True, exportError);
            Assert.That(storage.Exists(savedFileName), Is.True);

            using var stream = storage.GetStream(savedFileName, FileAccess.Read);
            bool importResult = ThemeShareManager.ValidateAndImport(stream, out var importedTheme, out string themeName, out var importError);

            Assert.That(importResult, Is.True, importError.ToString());
            Assert.That(themeName, Is.EqualTo("CyberpunkTheme"));
            Assert.That(importedTheme, Is.Not.Null);
            Assert.That(importedTheme!.BaseTheme, Is.EqualTo(ThemeMode.Dark));
            Assert.That(importedTheme.Geometry.TopBarVariant, Is.EqualTo(TopBarVariant.Rounded));
            Assert.That(importedTheme.Geometry.ButtonRadius, Is.EqualTo(14f));

            Assert.That(importedTheme.TryGetOverride(OverlayColour.Colour1, out var col1), Is.True);
            Assert.That(col1, Is.EqualTo(Colour4.FromHex("#FF55AA")));

            Assert.That(importedTheme.TryGetContextOverride(ThemeSurfaceContext.ModSelect, SurfaceColourRole.SurfaceBackground, out var ctxCol), Is.True);
            Assert.That(ctxCol, Is.EqualTo(Colour4.FromHex("#223344")));

            Assert.That(importedTheme.TryGetModTypeOverride(osu.Game.Rulesets.Mods.ModType.DifficultyReduction, out var modCol), Is.True);
            Assert.That(modCol, Is.EqualTo(Colour4.FromHex("#445566")));
        }

        [Test]
        public void TestThemeShareImportsCompleteFileBeyondStreamReaderBuffer()
        {
            using var storage = new TemporaryNativeStorage($"theme-share-large-{Guid.NewGuid():N}");
            var theme = new CustomThemeDefinition(ThemeMode.Dark);
            foreach (OverlayColour role in Enum.GetValues<OverlayColour>())
                theme.SetOverride(role, Colour4.FromHex("#123456"));
            foreach (ThemeSurfaceContext context in Enum.GetValues<ThemeSurfaceContext>())
                theme.SetContextOverride(context, SurfaceColourRole.SurfaceBackground, Colour4.FromHex("#ABCDEF"));

            Assert.That(ThemeShareManager.TryExportTheme(theme, storage, "large-theme", out var filename, out var exportError), Is.True, exportError);
            using var stream = storage.GetStream(filename, FileAccess.Read);
            Assert.That(stream.Length, Is.GreaterThan(1024));
            Assert.That(ThemeShareManager.ValidateAndImport(stream, out var imported, out _, out var importError), Is.True, importError.ToString());
            Assert.That(imported!.Overrides.Count, Is.EqualTo(theme.Overrides.Count));
            Assert.That(imported.ContextOverrides.Count, Is.EqualTo(theme.ContextOverrides.Count));
        }

        [TestCase(@"{""version"":6,""overrides"":{""Colour1"":null}}")]
        [TestCase(@"{""version"":6,""contextOverrides"":{""ModSelect"":{""Accent"":null}}}")]
        [TestCase(@"{""version"":6,""modTypeOverrides"":{""Automation"":null}}")]
        public void TestThemeShareRejectsNullColourValuesWithoutThrowing(string json)
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            Assert.That(ThemeShareManager.ValidateAndImport(stream, out var imported, out _, out var error), Is.False, error.ToString());
            Assert.That(imported, Is.Null);
        }

        [TestCase(@"{""format"": ""MorasoomaTheme"", ""formatVersion"": {}, ""name"": ""Test"", ""theme"": {""version"":6}}")]
        [TestCase(@"{""format"": ""MorasoomaTheme"", ""formatVersion"": 1, ""name"": {}, ""theme"": {""version"":6}}")]
        [TestCase(@"{""format"": [], ""formatVersion"": 1, ""theme"": {""version"":6}}")]
        public void TestThemeShareRejectsMalformedEnvelopeWithoutThrowing(string json)
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            Assert.That(ThemeShareManager.ValidateAndImport(stream, out var imported, out _, out _), Is.False);
            Assert.That(imported, Is.Null);
        }

        [Test]
        public void TestThemeShareDoesNotExportUnknownLocalExtensionFields()
        {
            using var storage = new TemporaryNativeStorage($"theme-share-extensions-{Guid.NewGuid():N}");
            var theme = new CustomThemeDefinition();
            theme.Extensions = new System.Collections.Generic.Dictionary<string, Newtonsoft.Json.Linq.JToken> { ["private_note"] = Newtonsoft.Json.Linq.JToken.FromObject("DO_NOT_SHARE") };
            theme.Geometry.Extensions = new System.Collections.Generic.Dictionary<string, Newtonsoft.Json.Linq.JToken> { ["private_geometry"] = Newtonsoft.Json.Linq.JToken.FromObject("DO_NOT_SHARE") };
            Assert.That(ThemeShareManager.TryExportTheme(theme, storage, "clean-theme", out var filename, out var error), Is.True, error);
            using var stream = storage.GetStream(filename, FileAccess.Read);
            using var reader = new StreamReader(stream);
            string json = reader.ReadToEnd();
            Assert.That(json, Does.Not.Contain("DO_NOT_SHARE"));
            Assert.That(json, Does.Not.Contain("private_note"));
        }

        [Test]
        public void TestThemeShareRawThemeJsonCompatibility()
        {
            var rawTheme = new CustomThemeDefinition(ThemeMode.Light);
            rawTheme.SetOverride(OverlayColour.Colour2, Colour4.FromHex("#123456"));
            string rawJson = rawTheme.ToJson();

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(rawJson));
            bool importResult = ThemeShareManager.ValidateAndImport(stream, out var importedTheme, out string themeName, out var errorMessage);

            Assert.That(importResult, Is.True, errorMessage.ToString());
            Assert.That(themeName, Is.EqualTo("Imported Theme"));
            Assert.That(importedTheme, Is.Not.Null);
            Assert.That(importedTheme!.BaseTheme, Is.EqualTo(ThemeMode.Light));
            Assert.That(importedTheme.TryGetOverride(OverlayColour.Colour2, out var col), Is.True);
            Assert.That(col, Is.EqualTo(Colour4.FromHex("#123456")));
        }

        [Test]
        public void TestThemeShareOversizedFileRejected()
        {
            // Create a payload larger than 256 KB
            byte[] oversizedBytes = new byte[ThemeShareManager.MAX_THEME_FILE_SIZE_BYTES + 1024];
            Array.Fill(oversizedBytes, (byte)' ');
            // Make beginning valid JSON whitespace
            oversizedBytes[0] = (byte)'{';
            oversizedBytes[oversizedBytes.Length - 1] = (byte)'}';

            using var stream = new MemoryStream(oversizedBytes);
            bool importResult = ThemeShareManager.ValidateAndImport(stream, out var importedTheme, out _, out var errorMessage);

            Assert.That(importResult, Is.False);
            Assert.That(importedTheme, Is.Null);
            Assert.That(errorMessage.ToString(), Does.Contain("maximum allowed size").Or.Contain("too large"));
        }

        [Test]
        public void TestThemeShareFutureSchemaVersionRejected()
        {
            string futureJson = @"{
                ""format"": ""MorasoomaTheme"",
                ""formatVersion"": 9999,
                ""name"": ""Future Theme"",
                ""theme"": {
                    ""version"": 1,
                    ""baseTheme"": ""Default"",
                    ""overrides"": {}
                }
            }";

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(futureJson));
            bool importResult = ThemeShareManager.ValidateAndImport(stream, out var importedTheme, out _, out var errorMessage);

            Assert.That(importResult, Is.False);
            Assert.That(importedTheme, Is.Null);
            Assert.That(errorMessage.ToString(), Does.Contain("Unsupported theme package version"));
        }

        [Test]
        public void TestThemeShareInvalidHexFormatRejected()
        {
            string invalidColourJson = @"{
                ""format"": ""MorasoomaTheme"",
                ""formatVersion"": 1,
                ""name"": ""Bad Colour"",
                ""theme"": {
                    ""version"": 1,
                    ""baseTheme"": ""Default"",
                    ""overrides"": {
                        ""Colour1"": ""not-a-valid-hex-code""
                    }
                }
            }";

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(invalidColourJson));
            bool importResult = ThemeShareManager.ValidateAndImport(stream, out var importedTheme, out _, out var errorMessage);

            Assert.That(importResult, Is.False);
            Assert.That(importedTheme, Is.Null);
            Assert.That(errorMessage.ToString(), Does.Contain("Invalid hex colour format"));
        }

        [Test]
        public void TestThemeShareGeometryRadiiClamped()
        {
            string excessiveRadiiJson = @"{
                ""format"": ""MorasoomaTheme"",
                ""formatVersion"": 1,
                ""name"": ""Extreme Radii"",
                ""theme"": {
                    ""version"": 1,
                    ""baseTheme"": ""Default"",
                    ""geometry"": {
                        ""buttonRadius"": -50.0,
                        ""cardRadius"": 500.0,
                        ""topBarRadius"": 1000.0,
                        ""topBarVariant"": ""Rounded""
                    }
                }
            }";

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(excessiveRadiiJson));
            bool importResult = ThemeShareManager.ValidateAndImport(stream, out var importedTheme, out _, out var errorMessage);

            Assert.That(importResult, Is.True, errorMessage.ToString());
            Assert.That(importedTheme, Is.Not.Null);
            Assert.That(importedTheme!.Geometry.ButtonRadius, Is.EqualTo(0f)); // negative clamped to 0
            Assert.That(importedTheme.Geometry.CardRadius, Is.EqualTo(30f)); // 500 clamped to 30
            Assert.That(importedTheme.Geometry.TopBarRadius, Is.EqualTo(24f)); // 1000 clamped to 24
            Assert.That(importedTheme.Geometry.TopBarVariant, Is.EqualTo(TopBarVariant.Rounded));
        }

        [Test]
        public void TestThemeShareBaseThemeCustomFallback()
        {
            string recursiveCustomJson = @"{
                ""format"": ""MorasoomaTheme"",
                ""formatVersion"": 1,
                ""name"": ""Recursive Custom"",
                ""theme"": {
                    ""version"": 1,
                    ""baseTheme"": ""Custom"",
                    ""overrides"": {}
                }
            }";

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(recursiveCustomJson));
            bool importResult = ThemeShareManager.ValidateAndImport(stream, out var importedTheme, out _, out var errorMessage);

            Assert.That(importResult, Is.True, errorMessage.ToString());
            Assert.That(importedTheme, Is.Not.Null);
            Assert.That(importedTheme!.BaseTheme, Is.EqualTo(ThemeMode.Default));
        }

        [Test]
        public void TestThemeShareUniqueExportFilenameDisambiguation()
        {
            using var storage = new TemporaryNativeStorage($"theme-unique-test-{Guid.NewGuid():N}");
            var theme = new CustomThemeDefinition();

            bool res1 = ThemeShareManager.TryExportTheme(theme, storage, "MyTheme", out string file1, out string? err1);
            Assert.That(res1, Is.True, err1);
            Assert.That(storage.Exists(file1), Is.True);

            // Re-exporting with identical requested name produces a unique file
            bool res2 = ThemeShareManager.TryExportTheme(theme, storage, "MyTheme", out string file2, out string? err2);
            Assert.That(res2, Is.True, err2);
            Assert.That(storage.Exists(file2), Is.True);
        }

        [Test]
        public void TestThemeShareNameSanitizedAndConstrained()
        {
            string longName = new string('A', 100) + "\r\n\tControl";
            string json = $@"{{
                ""format"": ""MorasoomaTheme"",
                ""formatVersion"": 1,
                ""name"": ""{longName}"",
                ""theme"": {{
                    ""version"": 1,
                    ""baseTheme"": ""Default""
                }}
            }}";

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            Assert.That(ThemeShareManager.ValidateAndImport(stream, out var imported, out string themeName, out var error), Is.True, error.ToString());
            Assert.That(themeName.Length, Is.LessThanOrEqualTo(64));
            Assert.That(themeName, Does.Not.Contain("\r").And.Not.Contain("\n").And.Not.Contain("\t"));

            string emptyNameJson = @"{
                ""format"": ""MorasoomaTheme"",
                ""formatVersion"": 1,
                ""name"": ""   "",
                ""theme"": {
                    ""version"": 1,
                    ""baseTheme"": ""Default""
                }
            }";

            using var emptyStream = new MemoryStream(Encoding.UTF8.GetBytes(emptyNameJson));
            Assert.That(ThemeShareManager.ValidateAndImport(emptyStream, out _, out string emptyName, out _), Is.True);
            Assert.That(emptyName, Is.EqualTo("Custom Theme"));
        }

        [TestCase(ThemeMode.Default)]
        [TestCase(ThemeMode.Light)]
        [TestCase(ThemeMode.Dark)]
        [TestCase(ThemeMode.Custom)]
        public void TestThemeImportConfirmationDialogLocalisation(ThemeMode baseTheme)
        {
            var dialog = new ThemeImportConfirmationDialog("Test Theme", baseTheme, 5, () => { });
            Assert.That(dialog.HeaderText, Is.Not.Null);
            Assert.That(dialog.BodyText, Is.Not.Null);
            Assert.That(dialog.Buttons.Count(), Is.EqualTo(2));
        }

        [Test]
        public void TestForkSettingsStringsFallbackParity()
        {
            var properties = typeof(osu.Game.Localisation.ForkSettingsStrings).GetProperties(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

            Assert.That(properties.Length, Is.GreaterThan(100));

            foreach (var prop in properties)
            {
                var val = prop.GetValue(null);
                Assert.That(val, Is.Not.Null, $"Property {prop.Name} returned null");

                if (val is TranslatableString ts)
                {
                    Assert.That(ts.Key, Does.StartWith("osu.Game.Resources.Localisation.ForkSettings:"), $"Property {prop.Name} has invalid key prefix");
                    Assert.That(ts.Format, Is.Not.Null.And.Not.Empty, $"Property {prop.Name} has empty format/fallback");
                    Assert.That(ts.Format.Any(c => c >= 0x0400 && c <= 0x04FF), Is.False, $"Property {prop.Name} contains Cyrillic in C# fallback: {ts.Format}");
                }
            }
        }
    }
}
