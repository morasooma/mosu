using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Extensions;
using osu.Framework.Platform;
using osu.Framework.Localisation;
using osu.Framework.Configuration;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Rulesets;
using osu.Game.Screens.Edit.Submission;
using osu.Game.Tests.Resources;

namespace osu.Game.Tests.Visual.Editing
{
    public partial class TestSceneServerBeatmapSubmission : ScreenTestScene
    {
        protected override bool UseFreshStoragePerRun => true;

        private BeatmapManager beatmaps = null!;

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
            dependencies.CacheAs<RulesetStore>(new RealmRulesetStore(Realm));
            dependencies.Cache(beatmaps = new BeatmapManager(LocalStorage, Realm, null, dependencies.Get<AudioManager>(), Resources,
                dependencies.Get<GameHost>(), Beatmap.Default));
            dependencies.CacheAs<Storage>(LocalStorage);
            return dependencies;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestOrdinaryMapOnDefaultMosuProfileDoesNotUseBss(bool existing)
        {
            BeatmapSubmissionScreen screen = null!;
            UploadServerBeatmapSetRequest? submissionRequest = null;
            GetServerBeatmapSetUploadStateRequest? stateRequest = null;
            BeatmapSubmissionOverlay overlay = null!;
            int bssRequests = 0;

            AddStep("configure main Morasooma API and ordinary map", () =>
            {
                var api = (DummyAPIAccess)API;
                api.SetState(APIState.Online);
                api.Endpoints.APIUrl = "http://127.0.0.1:18082";
                api.Endpoints.BeatmapSubmissionServiceUrl = "http://127.0.0.1:18082/beatmap-submission";
                api.HandleRequest = request =>
                {
                    switch (request)
                    {
                        case GetServerBeatmapSetUploadStateRequest state:
                            stateRequest = state;
                            state.TriggerSuccess(new ServerBeatmapSetUploadState { BeatmapSetID = 2_000_000_000, Revision = 7, UpdatesRemaining = 1 });
                            return true;

                        case UploadServerBeatmapSetRequest upload:
                            submissionRequest = upload;
                            upload.Fail(new InvalidOperationException("Controlled submission rejection"));
                            return true;

                        case PutBeatmapSetRequest:
                            bssRequests++;
                            request.Fail(new InvalidOperationException("Unexpected BSS request"));
                            return true;

                        default:
                            return false;
                    }
                };
                beatmaps.Import(TestResources.GetQuickTestBeatmapForImport()).WaitSafely();
                var set = beatmaps.GetAllUsableBeatmapSets().First();
                Realm.Write(r => r.Find<BeatmapSetInfo>(set.ID)!.OnlineID = existing ? 2_000_000_000 : -1);
                var map = set.Beatmaps.First();
                Beatmap.Value = beatmaps.GetWorkingBeatmap(beatmaps.QueryBeatmap(b => b.ID == map.ID)!);
                if (existing)
                {
                    var edited = Beatmap.Value.Beatmap;
                    edited.Difficulty.ApproachRate += 0.1f;
                    beatmaps.Save(Beatmap.Value.BeatmapInfo, edited);
                }
                LoadScreen(screen = new BeatmapSubmissionScreen());
            });
            AddUntilStep("submission screen loaded", () => screen.IsLoaded);
            AddStep("find submission wizard", () => overlay = screen.ChildrenOfType<BeatmapSubmissionOverlay>().Single());
            AddUntilStep("wizard loaded", () => overlay.CurrentScreen != null);
            if (!existing)
            {
                AddStep("acknowledge content permissions", () => typeof(BeatmapSubmissionOverlay).BaseType!
                    .GetMethod("ShowNextStep", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(overlay, null));
                AddUntilStep("FAQ loaded", () => overlay.CurrentScreen is ScreenFrequentlyAskedQuestions);
                AddStep("continue to settings", () => typeof(BeatmapSubmissionOverlay).BaseType!
                    .GetMethod("ShowNextStep", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(overlay, null));
            }
            AddUntilStep("submission settings loaded", () => overlay.CurrentScreen is ScreenSubmissionSettings);
            AddAssert("confirmation distinguishes update from publication", () => overlay.CurrentScreen!.NextStepText!.Value,
                () => Is.EqualTo(existing ? BeatmapSubmissionStrings.UpdateBeatmap : BeatmapSubmissionStrings.ConfirmSubmission));
            AddAssert("Russian confirmation resolves without English fallback", () =>
            {
                using var config = new LocalisationConfig();
                using var manager = new LocalisationManager(config);
                manager.AddLanguage("ru", new ResourceManagerLocalisationStore("ru"));
                config.SetValue(FrameworkSetting.Locale, "ru");
                return manager.GetLocalisedString(overlay.CurrentScreen!.NextStepText!.Value)
                    == (existing ? "Обновить карту!" : "Опубликовать карту!");
            });
            AddStep("confirm submission", () => typeof(BeatmapSubmissionOverlay).BaseType!
                .GetMethod("ShowNextStep", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(overlay, null));
            AddUntilStep("submission request queued", () => submissionRequest != null);
            AddAssert("main profile uses revision API instead of BSS", () => bssRequests == 0 && (existing
                ? stateRequest != null && submissionRequest is { BeatmapSetID: 2_000_000_000, BaseRevision: 7 }
                : stateRequest == null && submissionRequest is { BeatmapSetID: null, BaseRevision: null }));
        }

        private sealed class LocalisationConfig : FrameworkConfigManager
        {
            protected override string Filename => null!;
            public LocalisationConfig() : base(null!) { }
        }
    }
}
