using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Morasooma.Resources;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Audio;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.IO;
using osu.Game.Extensions;
using osu.Game.Models;
using osu.Game.Resources;
using osu.Game.Rulesets.Catch;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Taiko;
using osu.Game.Skinning;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using osuTK.Graphics;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    [NonParallelizable]
    public partial class MorasoomaSkinTest
    {
        private bool previousPerformanceMode;

        [SetUp]
        public void SetUp()
        {
            previousPerformanceMode = SkinPerformanceMode.Enabled;
            SkinPerformanceMode.Enabled = false;
        }

        [TearDown]
        public void TearDown() => SkinPerformanceMode.Enabled = previousPerformanceMode;

        [TestCase(false)]
        [TestCase(true)]
        public void TestEmbeddedBankResolvesWithoutUpstreamGraphics(bool performanceMode)
        {
            SkinPerformanceMode.Enabled = performanceMode;
            using var resources = new DllResourceStore(typeof(ResourceAssembly).Assembly);
            var manifest = JObject.Parse(Encoding.UTF8.GetString(resources.Get("skin-manifest.json")));
            var files = (JArray)manifest["files"]!;
            using var skin = new MorasoomaSkin(new TestProvider());
            Assert.That(files.Count, Is.EqualTo(786));
            Assert.That(skin.Configuration.CustomComboColours, Is.EqualTo(new[]
            {
                Color4.White, new Color4(232, 232, 232, 255), new Color4(208, 208, 208, 255)
            }));
            foreach (var file in files)
            {
                string name = (string)file["resource"]!;
                byte[] bytes = resources.Get(name);
                Assert.That(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), Is.EqualTo((string)file["sha256"]!), name);
                if (!name.EndsWith(".png", StringComparison.Ordinal))
                    continue;
                // Resolve through LegacySkin, TextureLoaderStore and the actual embedded store.
                // @1x and @2x keys intentionally select the HD sprite where available.
                string key = Path.GetFileNameWithoutExtension(name).Replace("@2x", string.Empty);
                byte[]? hd = resources.Get("Skins/Morasooma/" + key + "@2x.png");
                byte[]? sd = resources.Get("Skins/Morasooma/" + key + ".png");
                bool preferLow = SkinPerformanceMode.PreferLowResolutionLegacyTexture(key);
                using var expected = Image.Load<Rgba32>((preferLow ? sd ?? hd : hd ?? sd)!);
                float scale = SkinPerformanceMode.LegacyTextureScaleAdjust(key);
                var texture = skin.GetTexture(key);
                Assert.That(texture, Is.Not.Null, key);
                Assert.That(texture!.Width, Is.EqualTo(Math.Max(1, (int)MathF.Round(expected.Width * scale))), key);
                Assert.That(texture.Height, Is.EqualTo(Math.Max(1, (int)MathF.Round(expected.Height * scale))), key);
            }
            foreach (string key in new[] { "cursormiddle", "spinner-background", "spinner-metre", "menu-background", "Intro/Welcome/menu-background" })
                Assert.That(skin.GetTexture(key), Is.Null, key);
            Assert.That(skin.ManiaConfigurations.Keys, Is.EquivalentTo(Enumerable.Range(1, 18)));
            for (int keys = 1; keys <= 18; keys++)
            {
                var config = skin.ManiaConfigurations[keys];
                Assert.That(config.ColumnWidth.All(w => w == 48), Is.True, keys.ToString());
                foreach (string path in config.ImageLookups.Values.Distinct())
                {
                    using var drawable = skin.GetAnimation(path, true, true);
                    Assert.That(drawable, Is.Not.Null, path);
                }
            }
            foreach (string notice in new[] { "MIT-LICENSE.txt", "OFL.txt", "NOTICE.txt", "sources.json" })
                Assert.That(resources.Get("Licenses/Skins/P1-White/" + notice), Is.Not.Empty, notice);
        }

        [TestCase("")]
        [TestCase("@2x")]
        public void TestSliderEndMissIsWhiteButTickMissStaysRed(string suffix)
        {
            using var resources = new DllResourceStore(typeof(ResourceAssembly).Assembly);
            using var end = Image.Load<Rgba32>(resources.Get($"Skins/Morasooma/sliderendmiss{suffix}.png"));
            using var tick = Image.Load<Rgba32>(resources.Get($"Skins/Morasooma/slidertickmiss{suffix}.png"));
            int visible = 0;
            for (int y = 0; y < end.Height; y++)
            {
                for (int x = 0; x < end.Width; x++)
                {
                    var pixel = end[x, y];
                    if (pixel.A == 0)
                        continue;
                    visible++;
                    Assert.That(pixel.R, Is.EqualTo(255));
                    Assert.That(pixel.G, Is.EqualTo(pixel.R));
                    Assert.That(pixel.B, Is.EqualTo(pixel.R));
                }
            }
            Assert.That(visible, Is.GreaterThan(0));
            Assert.That(tick[tick.Width / 2, tick.Height / 2].R, Is.GreaterThan(tick[tick.Width / 2, tick.Height / 2].G));
        }

        [Test]
        public void TestBuiltInHitLightingHasVisibleFeatheredPixels()
        {
            using var resources = new DllResourceStore(typeof(ResourceAssembly).Assembly);
            using var image = Image.Load<Rgba32>(resources.Get("Skins/Morasooma/lighting.png"));
            // Former 368px @2x lighting occupied 184 gameplay units.
            Assert.That(image.Width, Is.EqualTo(184));
            Assert.That(image.Height, Is.EqualTo(184));
            Assert.That(image[92, 92].A, Is.InRange(170, 180));
            Assert.That(image[92, 92].R, Is.EqualTo(255), "Lighting must accept combo tint");
            // Preserve the former broad falloff, not just non-transparent pixels.
            Assert.That(image[138, 92].A, Is.InRange(65, 80));
            Assert.That(image[166, 92].A, Is.InRange(8, 16));
            Assert.That(image[0, 0].A, Is.Zero);
            Assert.That(image[0, 92].A, Is.Zero);
            using var skin = new MorasoomaSkin(new TestProvider());
            using var classic = new DefaultLegacySkin(new TestProvider());
            foreach (var source in new LegacySkin[] { skin, classic })
            {
                using var drawable = source.GetAnimation("lighting", true, true);
                Assert.That(drawable, Is.Not.Null);
                Assert.That(drawable!.Width, Is.EqualTo(184), source.SkinInfo.Value.Name);
                Assert.That(drawable.Height, Is.EqualTo(184), source.SkinInfo.Value.Name);
            }
        }

        [Test]
        public void TestAllFourRulesetsUseLegacyTransformersAndGeneratedAnimations()
        {
            using var skin = new MorasoomaSkin(new TestProvider());
            var beatmap = new Beatmap();
            foreach (var ruleset in new osu.Game.Rulesets.Ruleset[] { new OsuRuleset(), new TaikoRuleset(), new CatchRuleset() })
            {
                var transformed = ruleset.CreateSkinTransformer(skin, beatmap);
                Assert.That(transformed, Is.InstanceOf<LegacySkinTransformer>(), ruleset.ShortName);
                Assert.That(((LegacySkinTransformer)transformed!).IsProvidingLegacyResources, Is.True, ruleset.ShortName);
            }
            var mania = new ManiaRuleset();
            var maniaBeatmap = new ManiaBeatmap(new StageDefinition(7));
            maniaBeatmap.BeatmapInfo.Ruleset = mania.RulesetInfo;
            var maniaSkin = mania.CreateSkinTransformer(skin, maniaBeatmap);
            Assert.That(maniaSkin, Is.InstanceOf<LegacySkinTransformer>());
            Assert.That(((LegacySkinTransformer)maniaSkin!).IsProvidingLegacyResources, Is.True);
            foreach (var entry in new[] { ("followpoint", "-", 72), ("sliderb", "", 10), ("pippidonclear", "", 9),
                                          ("mania-note1L", "-", 6), ("lightingL", "-", 6), ("fruit-catcher-idle", "-", 1) })
            {
                var textures = skin.GetTextures(entry.Item1, default, default, true, entry.Item2, null, out _);
                Assert.That(textures.Length, Is.EqualTo(entry.Item3), entry.Item1);
            }
        }

        [Test]
        public void TestDefaultSelectionFallbackAndSavedSkinsRemainIndependent()
        {
            using var host = new CleanRunHeadlessGameHost();
            host.Run(new SelectionTestGame(() =>
            {
                using var realm = new RealmAccess(host.Storage, OsuGameBase.CLIENT_DATABASE_FILENAME);
                using var resources = new ResourceStore<byte[]>();
                resources.AddStore(new NamespacedResourceStore<byte[]>(new DllResourceStore(typeof(ResourceAssembly).Assembly), "SoundOverrides"));
                resources.AddStore(new NamespacedResourceStore<byte[]>(new DllResourceStore(typeof(OsuGameBase).Assembly), "Resources"));
                resources.AddStore(new DllResourceStore(OsuResources.ResourceAssembly));
                var manager = new SkinManager(host.Storage, realm, host, resources, null!, host.UpdateThread.Scheduler);
                using var config = new OsuConfigManager(host.Storage);
                Assert.That(config.Get<string>(OsuSetting.Skin), Is.EqualTo(SkinInfo.MORASOOMA_SKIN.ToString()));
                Assert.That(manager.CurrentSkin.Value, Is.TypeOf<MorasoomaSkin>());
                Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(SkinInfo.MORASOOMA_SKIN));
                Assert.That(manager.GetAllUsableSkins().First().ID, Is.EqualTo(SkinInfo.MORASOOMA_SKIN));
                Assert.That(manager.AllSources.OfType<MorasoomaSkin>().Count(), Is.EqualTo(1));
                Assert.That(manager.GetAllUsableSkins().Select(s => s.ID), Does.Not.Contain(SkinInfo.RETRO_SKIN));
                Assert.That(realm.Run(r => r.Find<SkinInfo>(SkinInfo.RETRO_SKIN)), Is.Null, "Fresh libraries no longer register Retro");
                // Simulate a historical Realm record without modifying any actual user library.
                var retired = new SkinInfo("Retro", "team osu!", "osu.Game.Skinning.RetroSkin, osu.Game")
                {
                    ID = SkinInfo.RETRO_SKIN,
                    Protected = true,
                };
                realm.Write(r => r.Add(retired));
                manager.SetSkinFromConfiguration(SkinInfo.RETRO_SKIN.ToString());
                Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(SkinInfo.MORASOOMA_SKIN));
                Assert.That(manager.GetAllUsableSkins().Select(s => s.ID), Does.Not.Contain(SkinInfo.RETRO_SKIN));
                manager.CurrentSkinInfo.Value = retired.ToLive(realm);
                Assert.That(manager.CurrentSkin.Value, Is.TypeOf<MorasoomaSkin>());
                Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(SkinInfo.MORASOOMA_SKIN));
                Assert.That(realm.Run(r => r.Find<SkinInfo>(SkinInfo.RETRO_SKIN)), Is.Not.Null, "Historical records are not deleted");
                using (var historicalInstance = retired.CreateInstance(new TestProvider()))
                    Assert.That(historicalInstance, Is.TypeOf<MorasoomaSkin>());
                foreach (Guid historical in new[] { SkinInfo.CLASSIC_SKIN, SkinInfo.ARGON_SKIN })
                {
                    manager.SetSkinFromConfiguration(historical.ToString());
                    Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(historical), "Existing saved built-in choice must survive");
                    Assert.That(manager.AllSources.Skip(1).First(), Is.SameAs(manager.DefaultClassicSkin));
                }
                manager.SetSkinFromConfiguration(Guid.NewGuid().ToString());
                Assert.That(manager.CurrentSkin.Value, Is.TypeOf<MorasoomaSkin>(), "Unknown/missing selection uses the new default");
                Assert.That(manager.GetTexture("back-button-layer", default, default), Is.Not.Null);
                Assert.That(manager.DefaultClassicSkin.GetTexture("scoreboard-explosion-1"), Is.Not.Null);

                // Imported personal files precede the new fallback, including custom dimensions.
                using var image = new Image<Rgba32>(13, 17);
                using var stream = new MemoryStream();
                image.SaveAsPng(stream);
                var file = new RealmFile { Hash = Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant() };
                using (var output = host.Storage.GetStorageForDirectory("files").GetStream(file.GetStoragePath(), FileAccess.Write, FileMode.CreateNew))
                    output.Write(stream.ToArray());
                var skinInfo = new SkinInfo("Personal copy of Retro", "Test", "osu.Game.Skinning.RetroSkin, osu.Game");
                skinInfo.Files.Add(new RealmNamedFileUsage(file, "hitcircle.png"));
                realm.Write(r => r.Add(skinInfo));
                manager.CurrentSkinInfo.Value = skinInfo.ToLive(realm);
                // A source without a particular element remains separate from the fallback.
                Assert.That(manager.AllSources.First().GetTexture("back-button-layer"), Is.Null);
                Assert.That(manager.FindProvider(s => s.GetTexture("back-button-layer") != null), Is.SameAs(manager.DefaultClassicSkin));
                Assert.That(manager.GetTexture("hitcircle", default, default)!.Width, Is.EqualTo(13));
                Assert.That(manager.GetTexture("hitcircle", default, default)!.Height, Is.EqualTo(17));
                Assert.That(manager.FindProvider(s => s.GetTexture("hitcircle") != null), Is.SameAs(manager.CurrentSkin.Value));
                Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(skinInfo.ID));
            }));
        }

        private partial class SelectionTestGame : osu.Framework.Game
        {
            public SelectionTestGame(Action action)
            {
                Scheduler.Add(() =>
                {
                    try
                    {
                        action();
                    }
                    finally
                    {
                        Exit();
                    }
                });
            }
        }

        private class TestProvider : IStorageResourceProvider
        {
            public IRenderer Renderer { get; } = new DummyRenderer();
            public AudioManager AudioManager => null!;
            public IResourceStore<byte[]> Files { get; } = new ResourceStore<byte[]>();
            public IResourceStore<byte[]> Resources { get; } = new ResourceStore<byte[]>();
            public RealmAccess RealmAccess => null!;
            public IResourceStore<TextureUpload> CreateTextureLoaderStore(IResourceStore<byte[]> underlyingStore) => new TextureLoaderStore(underlyingStore);
        }

    }
}
