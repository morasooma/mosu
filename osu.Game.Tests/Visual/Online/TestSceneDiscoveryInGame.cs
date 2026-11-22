using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Performance;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays.Discovery;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Skinning;

namespace osu.Game.Tests.Visual.Online
{
    // Keep the opaque game background: the overlay-only harness cannot expose missing
    // front-to-back depth propagation when the gameplay framebuffer is bypassed.
    public partial class TestSceneDiscoveryInGame : OsuGameTestScene
    {
        private DiscoveryGameplayPreview preview = null!;
        private bool originalSkinPerformance;
        private bool originalLifetimeCache;

        [SetUp]
        public void SaveGlobalSettings()
        {
            originalSkinPerformance = SkinPerformanceMode.Enabled;
            originalLifetimeCache = GraphicsPerformanceSettings.CacheStaticChildLifetimes;
        }

        [TearDown]
        public void RestoreGlobalSettings()
        {
            SkinPerformanceMode.Enabled = originalSkinPerformance;
            GraphicsPerformanceSettings.CacheStaticChildLifetimes = originalLifetimeCache;
        }

        [TestCase(1f)]
        [TestCase(0.75f)]
        public void TestStandardNotesInFullGame(float renderScale)
        {
            AddStep("native preview over opaque full-game background", () =>
            {
                Game.LocalConfig.SetValue(OsuSetting.BeatmapSkins, false);
                Game.LocalConfig.SetValue(OsuSetting.BeatmapColours, false);
                Game.LocalConfig.SetValue(OsuSetting.ForkSkinPerformanceMode, true);
                Game.LocalConfig.SetValue(OsuSetting.ForkStaticChildLifetimeCache, true);
                Game.LocalConfig.SetValue(OsuSetting.ForkGameplayRenderScale, renderScale);
                if (Environment.GetEnvironmentVariable("MOSU_DISCOVERY_SKIN_ARCHIVE") is string archive)
                {
                    var manager = Game.Dependencies.Get<SkinManager>();
                    manager.CurrentSkinInfo.Value = manager.Import(new RetainedImportTask(archive)).GetAwaiter().GetResult();
                }

                Beatmap map;
                byte[]? audio = null;
                if (Environment.GetEnvironmentVariable("MOSU_DISCOVERY_REAL_MEDIA") is string mediaRoot)
                {
                    map = DiscoveryMedia.Decode(File.ReadAllBytes(Path.Combine(mediaRoot, "map.osu")), new APIBeatmap
                    {
                        OnlineID = int.Parse(Environment.GetEnvironmentVariable("MOSU_DISCOVERY_REAL_MAP") ?? "131891", CultureInfo.InvariantCulture),
                        OnlineBeatmapSetID = int.Parse(Environment.GetEnvironmentVariable("MOSU_DISCOVERY_REAL_SET") ?? "41823", CultureInfo.InvariantCulture),
                        RulesetID = 0,
                    });
                    audio = File.ReadAllBytes(Path.Combine(mediaRoot, "clip"));
                }
                else
                {
                    string objects = string.Join("\n", Enumerable.Range(0, 100).Select(i => $"{100 + i % 4 * 100},{100 + i % 3 * 80},{59000 + i * 300},1,0,0:0:0:0:"));
                    string raw = "osu file format v14\n[General]\nPreviewTime:60000\nMode:0\n[Difficulty]\nCircleSize:4\nOverallDifficulty:5\nApproachRate:7\nSliderMultiplier:1.4\n[TimingPoints]\n0,344.827586,4,2,0,100,1,0\n[HitObjects]\n" + objects;
                    map = DiscoveryMedia.Decode(Encoding.UTF8.GetBytes(raw), new APIBeatmap { OnlineID = 1 });
                }

                Game.Add(new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Depth = -10000,
                    Children = new Drawable[]
                    {
                        new Box { RelativeSizeAxes = Axes.Both, Colour = Colour4.Black },
                        preview = new DiscoveryGameplayPreview(map, audio, Game.Dependencies.Get<RulesetStore>().GetRuleset(0)!),
                    },
                });
            });
            AddUntilStep("notes alive", () => preview.VisibleHitObjects > 0);
            AddWaitStep("render several frames", 30);
            // This guards loading/skin integration, not final pixel visibility. The framework
            // BufferedDrawNodeBypassTest separately guards the missing depth pass regression.
            AddUntilStep("circle skin content present", () => preview.ChildrenOfType<DrawableHitCircle>()
                .Any(c => c.Alpha > 0 && c.CirclePiece.Drawable?.IsPresent == true));
        }

        private sealed class RetainedImportTask(string path) : ImportTask(path)
        {
            public override void DeleteFile() { }
        }
    }
}
