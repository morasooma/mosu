// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Resources;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Dodge.Configuration;
using osu.Game.Rulesets.Dodge.Skinning;
using osu.Game.Rulesets.Dodge.Skinning.Components;
using osu.Game.Rulesets.Dodge.UI;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Dodge.Tests
{
    [TestFixture]
    public class DodgeSkinningTest
    {
        [Test]
        public void TestDefaultRulesetHudContainsOnlyGraze()
        {
            var ruleset = new DodgeRuleset();
            ISkin transformer = ruleset.CreateSkinTransformer(new EmptySkin(), new Beatmap())!;
            var rulesetInfo = new RulesetInfo("dodge", "Dodge", string.Empty, DodgeRuleset.ONLINE_ID);

            var container = (DefaultSkinComponentsContainer)transformer.GetDrawableComponent(
                new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.MainHUDComponents, rulesetInfo))!;

            Assert.Multiple(() =>
            {
                Assert.That(container.Children, Has.Count.EqualTo(1));
                Assert.That(container.Children.Single(), Is.TypeOf<DodgeGrazeCounter>());
            });
        }

        [Test]
        public void TestGrazeCounterIsAvailableInDodgeSkinEditorToolbox()
        {
            var rulesetInfo = new RulesetInfo(
                "dodge",
                "Dodge",
                typeof(DodgeRuleset).AssemblyQualifiedName!,
                DodgeRuleset.ONLINE_ID)
            {
                Available = true,
            };

            Assert.That(SerialisedDrawableInfo.GetAllAvailableDrawables(rulesetInfo), Does.Contain(typeof(DodgeGrazeCounter)));
        }

        [Test]
        public void TestEveryDeclaredMapSkinComponentHasUniqueResourceName()
        {
            string[] names = Enum.GetValues<DodgeSkinComponents>()
                                 .Select(DodgeSkinTransformer.GetTextureName)
                                 .ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(names, Has.All.Not.Empty);
                Assert.That(names.Distinct(StringComparer.OrdinalIgnoreCase).Count(), Is.EqualTo(names.Length));
            });
        }

        [Test]
        public void TestMissingOptionalPlayerVisualsDoNotRunOrChangeHitbox()
        {
            var player = new DodgePlayer
            {
                PlayerSize = 24,
            };

            player.Trail.Reset(player.Position, 0);
            player.Trail.UpdatePosition(player.Position + new osuTK.Vector2(8, 0), DodgePlayerTrail.SAMPLE_INTERVAL);
            player.TriggerMissFlash();
            player.TriggerGrazeEffect();

            Assert.Multiple(() =>
            {
                Assert.That(player.Trail.Component, Is.EqualTo(DodgeSkinComponents.PlayerTrail));
                Assert.That(player.Trail.HasVisual, Is.False);
                Assert.That(player.Trail.EmittedSegmentCount, Is.Zero);
                Assert.That(player.Trail.SegmentSize, Is.EqualTo(24));
                Assert.That(player.CollisionEffect.Component, Is.EqualTo(DodgeSkinComponents.CollisionEffect));
                Assert.That(player.CollisionEffect.HasVisual, Is.False);
                Assert.That(player.CollisionEffect.PlayCount, Is.Zero);
                Assert.That(player.GrazeEffect.Component, Is.EqualTo(DodgeSkinComponents.GrazeEffect));
                Assert.That(player.GrazeEffect.HasVisual, Is.False);
                Assert.That(player.GrazeEffect.PlayCount, Is.Zero);
                Assert.That(player.MissFlashCount, Is.EqualTo(1));
                Assert.That(player.PlayerSize, Is.EqualTo(24));
                Assert.That(player.Size, Is.EqualTo(new osuTK.Vector2(24)));
            });
        }

        [Test]
        public void TestMissSampleUsesDedicatedSkinLookup()
        {
            Assert.That(DodgeMissSampleInfo.Default.LookupNames, Is.EqualTo(new[]
            {
                "Gameplay/dodge-miss",
            }));
        }

        [Test]
        public void TestBuiltInMissSampleExists()
        {
            using var resources = new DllResourceStore(OsuResources.ResourceAssembly);

            Assert.That(resources.Get("Samples/Gameplay/dodge-miss.wav"), Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void TestMissSoundConfigurationDefaults()
        {
            var ruleset = new DodgeRuleset();
            using var config = new DodgeRulesetConfigManager(null, ruleset.RulesetInfo);
            var volume = (IBindableNumber<double>)config.GetBindable<double>(DodgeRulesetSetting.MissSoundVolume);
            var playfieldDim = (IBindableNumber<double>)config.GetBindable<double>(DodgeRulesetSetting.PlayfieldDim);
            var grazeIndicatorBrightness = (IBindableNumber<double>)config.GetBindable<double>(DodgeRulesetSetting.GrazeIndicatorBrightness);

            Assert.Multiple(() =>
            {
                Assert.That(config.Get<bool>(DodgeRulesetSetting.MissSoundEnabled), Is.True);
                Assert.That(volume.Value, Is.EqualTo(DodgeRulesetConfigManager.DEFAULT_MISS_SOUND_VOLUME));
                Assert.That(volume.MinValue, Is.Zero);
                Assert.That(volume.MaxValue, Is.EqualTo(100));
                Assert.That(playfieldDim.Value, Is.EqualTo(DodgeRulesetConfigManager.DEFAULT_PLAYFIELD_DIM));
                Assert.That(playfieldDim.MinValue, Is.Zero);
                Assert.That(playfieldDim.MaxValue, Is.EqualTo(1));
                Assert.That(grazeIndicatorBrightness.Value, Is.EqualTo(DodgeRulesetConfigManager.DEFAULT_GRAZE_INDICATOR_BRIGHTNESS));
                Assert.That(grazeIndicatorBrightness.MinValue, Is.Zero);
                Assert.That(grazeIndicatorBrightness.MaxValue, Is.EqualTo(1));
            });
        }

        private sealed class EmptySkin : ISkin
        {
            public Drawable? GetDrawableComponent(ISkinComponentLookup lookup) => null;

            public Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT) => null;

            public ISample? GetSample(ISampleInfo sampleInfo) => null;

            public IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup)
                where TLookup : notnull
                where TValue : notnull
                => null;
        }
    }
}
