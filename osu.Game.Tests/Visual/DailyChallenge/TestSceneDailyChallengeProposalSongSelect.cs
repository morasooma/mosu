using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Game.Overlays.Mods;
using osu.Game.Graphics.UserInterface;
using osu.Game.Screens.Select;
using osuTK.Input;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Online.API;
using osu.Game.Online.Rooms;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Screens.OnlinePlay;
using osu.Game.Screens.OnlinePlay.DailyChallenge;
using osu.Game.Tests.Resources;
using osu.Game.Tests.Visual.OnlinePlay;

namespace osu.Game.Tests.Visual.DailyChallenge
{
    public partial class TestSceneDailyChallengeProposalSongSelect : OnlinePlayTestScene
    {
        protected override bool UseFreshStoragePerRun => true;
        private BeatmapManager manager = null!;
        private DailyChallengeProposalSongSelect screen = null!;
        private object? submitted;
        private bool fail;
        private string? previousKey;

        [BackgroundDependencyLoader]
        private void load(GameHost host, AudioManager audio)
        {
            Dependencies.Cache(new RealmRulesetStore(Realm));
            Dependencies.Cache(manager = new BeatmapManager(LocalStorage, Realm, null, audio, Resources, host, Beatmap.Default));
            Dependencies.Cache(Realm);
            var store = new RealmDetachedBeatmapStore();
            Dependencies.CacheAs<BeatmapStore>(store);
            Add(store);
            manager.Import(TestResources.CreateTestBeatmapSetInfo(3));
        }

        public override void SetUpSteps()
        {
            base.SetUpSteps();
            AddStep("setup submission endpoint", () =>
            {
                submitted = null;
                previousKey = null;
                fail = false;
                var api = (DummyAPIAccess)API;
                var fallback = api.HandleRequest;
                api.HandleRequest = request =>
                {
                    if (request is not MutateDailyChallengeProposalRequest mutation) return fallback?.Invoke(request) ?? false;
                    submitted = typeof(MutateDailyChallengeProposalRequest).GetField("payload", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(mutation);
                    if (fail) mutation.TriggerFailure(new InvalidOperationException("temporary network failure"));
                    else mutation.TriggerSuccess(new DailyChallengeProposalMutation { ID = 1 });
                    return true;
                };
                SelectedMods.Value = new Mod[] { new OsuModHidden() };
            });
        }

        [Test]
        public void TestNewProposalDoesNotInheritDailyModsAndRetriesKeepIdempotencyKey()
        {
            AddStep("open selection", () => LoadScreen(screen = new DailyChallengeProposalSongSelect()));
            AddUntilStep("carousel ready", () => screen.IsCurrentScreen() && !screen.IsFiltering && screen.Beatmap.Value.BeatmapInfo.OnlineID > 0);
            AddAssert("daily mods cleared", () => screen.Mods.Value.Count == 0);
            AddStep("choose custom DT", () =>
            {
                var dt = new OsuModDoubleTime();
                dt.SpeedChange.Value = 1.2;
                screen.Mods.Value = new Mod[] { dt };
                fail = true;
                screen.AddNewItem();
            });
            AddUntilStep("first request handled", () => submitted != null);
            AddStep("retry", () =>
            {
                var json = Newtonsoft.Json.Linq.JObject.FromObject(submitted!);
                previousKey = json.Value<string>("request_key");
                Assert.That(json.SelectToken("selection.required_mods[0].settings.speed_change")!.Value<double>(), Is.EqualTo(1.2));
                submitted = null;
                screen.AddNewItem();
            });
            AddUntilStep("retry handled", () => submitted != null);
            AddAssert("same request key", () => Newtonsoft.Json.Linq.JObject.FromObject(submitted!).Value<string>("request_key") == previousKey);
            AddStep("change selection and submit", () =>
            {
                fail = false;
                screen.Mods.Value = new Mod[] { new OsuModHidden() };
                submitted = null;
                screen.AddNewItem();
            });
            AddUntilStep("new selection gets a new key", () => submitted != null && Newtonsoft.Json.Linq.JObject.FromObject(submitted!).Value<string>("request_key") != previousKey);
            AddUntilStep("successful submission returns", () => !screen.IsCurrentScreen());
        }

        [Test]
        public void TestDifficultyAdjustOpensSettingsWhenSelected()
        {
            ModSelectOverlay overlay = null!;
            AddStep("open selection", () => LoadScreen(screen = new DailyChallengeProposalSongSelect()));
            AddUntilStep("carousel ready", () => screen.IsCurrentScreen() && !screen.IsFiltering && screen.Beatmap.Value.BeatmapInfo.OnlineID > 0);
            AddStep("open required mods", () =>
            {
                overlay = (ModSelectOverlay)screen.CreateFooterButtons().OfType<FooterButtonMods>().Single().Overlay!;
                overlay.Show();
            });
            AddUntilStep("overlay visible", () => overlay.IsPresent);
            AddStep("scroll to DA column", () =>
            {
                var column = overlay.ChildrenOfType<ModSelectOverlay.ColumnDimContainer>().Single(c => c.ChildrenOfType<ModPanel>().Any(p => p.Mod is OsuModDifficultyAdjust));
                column.RequestScroll?.Invoke(column);
            });
            AddUntilStep("DA column interactive", () => overlay.ChildrenOfType<ModSelectOverlay.ColumnDimContainer>()
                .Single(c => c.ChildrenOfType<ModPanel>().Any(p => p.Mod is OsuModDifficultyAdjust)).Active.Value);
            AddStep("select DA via mouse", () =>
            {
                var panel = overlay.ChildrenOfType<ModPanel>().Single(p => p.Mod is OsuModDifficultyAdjust);
                InputManager.MoveMouseTo(panel);
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("DA selected", () => screen.Mods.Value.OfType<OsuModDifficultyAdjust>().Any());
            AddAssert("customisation enabled", () => overlay.ChildrenOfType<ModCustomisationPanel>().Single().Enabled.Value);
            AddAssert("DA automatically opens settings", () => overlay.ChildrenOfType<ModCustomisationPanel>().Single().ExpandedState.Value == ModCustomisationPanel.ModCustomisationPanelState.ExpandedByMod);
            AddStep("collapse settings", () => overlay.ChildrenOfType<ModCustomisationPanel>().Single().ExpandedState.Value = ModCustomisationPanel.ModCustomisationPanelState.Collapsed);
            AddStep("hover fine tuning header", () => InputManager.MoveMouseTo(overlay.ChildrenOfType<ModCustomisationHeader>().Single()));
            AddUntilStep("hover opens settings", () => overlay.ChildrenOfType<ModCustomisationPanel>().Single().ExpandedState.Value != ModCustomisationPanel.ModCustomisationPanelState.Collapsed);
        }

        [Test]
        public void TestRequiredModSettingsCanBeChangedThroughOverlayAndSubmitted()
        {
            ModSelectOverlay overlay = null!;
            AddStep("open selection", () => LoadScreen(screen = new DailyChallengeProposalSongSelect()));
            AddUntilStep("carousel ready", () => screen.IsCurrentScreen() && !screen.IsFiltering && screen.Beatmap.Value.BeatmapInfo.OnlineID > 0);
            AddStep("open required mods", () =>
            {
                overlay = (ModSelectOverlay)screen.CreateFooterButtons().OfType<FooterButtonMods>().Single().Overlay!;
                overlay.Show();
            });
            AddUntilStep("overlay visible", () => overlay.IsPresent);
            AddStep("select DT via panel", () => overlay.ChildrenOfType<ModPanel>().Single(p => p.Mod is OsuModDoubleTime).TriggerClick());
            // A previous test can leave the cursor over the header while it is still disabled.
            // Re-enter it after selecting DT so this checks an actual enabled hover event.
            AddStep("move mouse off header", () => InputManager.MoveMouseTo(osuTK.Vector2.Zero));
            AddUntilStep("hover moving customisation header until expanded", () =>
            {
                InputManager.MoveMouseTo(overlay.ChildrenOfType<ModCustomisationHeader>().Single());
                return overlay.ChildrenOfType<ModCustomisationPanel>().Single().ExpandedState.Value != ModCustomisationPanel.ModCustomisationPanelState.Collapsed;
            });
            AddStep("hover speed slider", () => InputManager.MoveMouseTo(overlay.ChildrenOfType<ModCustomisationPanel>().Single().ChildrenOfType<OsuSliderBar<double>>().First()));
            AddStep("change speed", () => InputManager.Key(Key.Right));
            AddAssert("DT speed changed", () => !screen.Mods.Value.OfType<OsuModDoubleTime>().Single().SpeedChange.IsDefault);
            AddStep("close overlay", () => overlay.Hide());
            AddStep("submit proposal", () => screen.AddNewItem());
            AddUntilStep("request handled", () => submitted != null);
            AddAssert("changed speed in payload", () => JObject.FromObject(submitted!).SelectToken("selection.required_mods[0].settings.speed_change") != null);
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void TestDefaultAllowedModsMatchProposalValidation(bool directlyApprove, bool toggleFreestyle)
        {
            AddStep("open selection", () => LoadScreen(screen = new DailyChallengeProposalSongSelect(directlyApprove)));
            AddUntilStep("carousel ready", () => screen.IsCurrentScreen() && !screen.IsFiltering && screen.Beatmap.Value.BeatmapInfo.OnlineID > 0);
            AddStep("choose DA settings and optionally reset freestyle", () =>
            {
                var da = new OsuModDifficultyAdjust();
                da.ApproachRate.Value = 9.3f;
                screen.Mods.Value = new Mod[] { da };
                if (toggleFreestyle)
                {
                    var freestyle = screen.CreateFooterButtons().OfType<FooterButtonFreestyle>().Single().Freestyle;
                    freestyle.Value = true;
                    freestyle.Value = false;
                }
            });
            AddStep("submit selection", () => screen.AddNewItem());
            AddUntilStep("request captured", () => submitted != null);
            AddAssert("default allowed mods accepted", () =>
            {
                var payload = JObject.FromObject(submitted!);
                if (Environment.GetEnvironmentVariable("MOSU_DAILY_PROPOSAL_FIXTURE_DIR") is { Length: > 0 } outputDirectory)
                {
                    System.IO.Directory.CreateDirectory(outputDirectory);
                    // Serialise the actual request payload, exactly as CreateWebRequest does.
                    System.IO.File.WriteAllText(System.IO.Path.Combine(outputDirectory,
                        $"{(directlyApprove ? "direct" : "proposal")}-{(toggleFreestyle ? "all-free-mods" : "initial")}.json"), JsonConvert.SerializeObject(submitted, Formatting.Indented));
                }
                var mods = (JArray)payload.SelectToken("selection.allowed_mods")!;
                Assert.That(mods.Count, Is.LessThanOrEqualTo(64));
                Assert.That(mods.Select(m => m.Value<string>("acronym")).Where(a => a != "AA!" && a!.Any(c => !char.IsAsciiLetterOrDigit(c))), Is.Empty);
                return true;
            });
        }

        [Test]
        public void TestEditingRestoresCustomRequiredAndAllowedMods()
        {
            AddStep("open edit", () =>
            {
                int id = manager.QueryBeatmap(_ => true)!.OnlineID;
                var proposal = JsonConvert.DeserializeObject<DailyChallengeProposal>("""
                    {"id":12,"owner_id":42,"ruleset_id":0,"revision":3,"status":"pending",
                    "required_mods":[{"acronym":"DT","settings":{"speed_change":1.2}}],"allowed_mods":[{"acronym":"HD"}],"freestyle":false}
                    """)!;
                proposal.BeatmapID = id;
                LoadScreen(screen = new DailyChallengeProposalSongSelect(editing: proposal));
            });
            AddUntilStep("selection ready", () => screen.IsCurrentScreen() && !screen.IsFiltering);
            AddAssert("mod parameter restored", () => screen.Mods.Value.Single() is OsuModDoubleTime dt && dt.SpeedChange.Value == 1.2);
            AddStep("save", () => screen.AddNewItem());
            AddUntilStep("edit submitted", () => submitted != null);
            AddAssert("revision and allowed mods retained", () =>
            {
                var json = Newtonsoft.Json.Linq.JObject.FromObject(submitted!);
                return json.Value<int>("revision") == 3 && json.Value<string>("action") == "edit" &&
                    json.SelectToken("selection.allowed_mods[0].acronym")!.Value<string>() == "HD";
            });
        }
    }
}
