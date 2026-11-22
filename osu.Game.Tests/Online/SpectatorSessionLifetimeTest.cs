using System.Collections.Generic;
using System.Linq;
using osu.Game.Online.Multiplayer.MatchTypes.TagCoop;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Online.Spectator;
using osu.Game.Replays.Legacy;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Tests.Gameplay;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Game.Tests.Online
{
    [HeadlessTest]
    public partial class SpectatorSessionLifetimeTest : OsuTestScene
    {
        [Test]
        public void TestLateFailedBeginCannotClearReplacementSession()
        {
            Probe client = null!;
            GameplayState first = null!;
            GameplayState second = null!;
            var ruleset = new OsuRuleset();
            AddStep("load spectator probe", () => Add(client = new Probe()));
            AddUntilStep("probe loaded", () => client.IsLoaded);
            AddStep("begin first session", () =>
            {
                var score = new Score { ScoreInfo = { BeatmapInfo = new BeatmapInfo(), Ruleset = ruleset.RulesetInfo } };
                first = TestGameplayState.Create(ruleset, score: score);
                client.BeginPlaying(1, first, score);
            });
            AddUntilStep("first begin pending", () => client.Begins == 1);
            AddStep("end first", () => client.EndPlaying(first));
            AddUntilStep("first ended", () => client.Ends == 1);
            AddStep("begin replacement", () =>
            {
                var score = new Score { ScoreInfo = { BeatmapInfo = new BeatmapInfo(), Ruleset = ruleset.RulesetInfo } };
                second = TestGameplayState.Create(ruleset, score: score);
                client.BeginPlaying(2, second, score);
            });
            AddUntilStep("second began", () => client.Begins == 2);
            AddStep("old begin fails late", () => client.First.TrySetResult(false));
            AddWaitStep("old completion delivered", 4);
            AddStep("supply replacement frame", () => client.HandleFrame(new OsuReplayFrame(1000, Vector2.Zero)));
            AddUntilStep("replacement continues streaming", () => client.Times.Contains(1000));
            AddStep("end replacement", () => client.EndPlaying(second));
            AddUntilStep("replacement ends normally", () => client.Ends == 2);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestStableEndWaitsForPendingFrames(bool failSend)
        {
            Probe client = null!;
            GameplayState state = null!;
            bool previous = osu.Game.Online.MosuServerEnvironment.UsesStableProtocol;
            AddStep("enable stable", () => osu.Game.Online.MosuServerEnvironment.UsesStableProtocol = true);
            AddStep("load probe", () => Add(client = new Probe { SendGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously) }));
            AddUntilStep("probe loaded", () => client.IsLoaded);
            AddStep("begin", () =>
            {
                var ruleset = new OsuRuleset();
                var score = new Score { ScoreInfo = { BeatmapInfo = new BeatmapInfo(), Ruleset = ruleset.RulesetInfo } };
                state = TestGameplayState.Create(ruleset, score: score);
                client.First.TrySetResult(true);
                client.BeginPlaying(1, state, score);
            });
            AddUntilStep("begun", () => client.Begins == 1);
            AddStep("frame and end", () =>
            {
                client.HandleFrame(new OsuReplayFrame(1000, Vector2.Zero));
                client.EndPlaying(state);
            });
            AddUntilStep("frame send blocked", () => client.Times.Contains(1000));
            AddAssert("end not sent before frame acknowledgement", () => client.Ends == 0);
            AddStep("release frame send", () =>
            {
                if (failSend)
                    client.SendGate!.TrySetException(new System.InvalidOperationException("Simulated frame send failure"));
                else
                    client.SendGate!.TrySetResult(true);
            });
            AddUntilStep("end sent after frames", () => client.Ends == 1);
            AddStep("begin next session", () =>
            {
                var ruleset = new OsuRuleset();
                var score = new Score { ScoreInfo = { BeatmapInfo = new BeatmapInfo(), Ruleset = ruleset.RulesetInfo } };
                state = TestGameplayState.Create(ruleset, score: score);
                client.BeginPlaying(2, state, score);
            });
            AddUntilStep("next session not blocked", () => client.Begins == 2);
            AddStep("end next session", () => client.EndPlaying(state));
            AddUntilStep("next session ended", () => client.Ends == 2);
            AddStep("restore profile", () => osu.Game.Online.MosuServerEnvironment.UsesStableProtocol = previous);
        }

        [Test]
        public void TestCompletedTagCoopReplayIsChunkedAndWaitsForLastPacket()
        {
            Probe client = null!;
            Task replacement = null!;
            AddStep("load probe", () => Add(client = new Probe()));
            AddUntilStep("probe loaded", () => client.IsLoaded);
            AddStep("begin tag session", () =>
            {
                var ruleset = new OsuRuleset();
                var score = new Score { ScoreInfo = { BeatmapInfo = new BeatmapInfo(), Ruleset = ruleset.RulesetInfo } };
                client.First.TrySetResult(true);
                client.ConfigureTagCoopReplay(score, new[] { new TagCoopReplayPlayer { UserID = 1 } });
                client.BeginPlaying(1, TestGameplayState.Create(ruleset, score: score), score);
            });
            AddUntilStep("begun", () => client.Begins == 1);
            AddStep("replace long replay", () =>
            {
                client.SendGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                replacement = client.HandleCompletedTagCoopReplayAsync(
                    Enumerable.Range(0, 1000).Select(i => new TagCoopReplayFrame { UserID = 1, GameplayTime = i }),
                    Enumerable.Range(0, 1000).Select(i => new OsuReplayFrame(i, new Vector2(i % 512, 0))));
            });
            AddUntilStep("first packet blocked", () => client.Bundles.Count == 1);
            AddAssert("replacement pending", () => !replacement.IsCompleted);
            AddStep("release first packet and block next", () =>
            {
                var first = client.SendGate!;
                client.SendGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                first.TrySetResult(true);
            });
            AddUntilStep("second packet blocked", () => client.Bundles.Count == 2);
            AddAssert("first packet is not completion", () => !replacement.IsCompleted);
            AddStep("release remaining packets", () => client.SendGate!.TrySetResult(true));
            AddUntilStep("replacement acknowledged", () => replacement.IsCompletedSuccessfully);
            AddAssert("all labelled frames preserved", () => client.Bundles.Sum(b => b.TagCoopFrames.Count) == 1000);
            AddAssert("all compatibility frames preserved", () => client.Bundles.Sum(b => b.Frames.Count) == 1000);
            AddAssert("packets bounded", () => client.Bundles.All(b => b.Frames.Count <= 30 && b.TagCoopFrames.Count <= 30));
            AddAssert("only first packet replaces", () => client.Bundles[0].ReplaceTagCoopReplay && client.Bundles.Skip(1).All(b => !b.ReplaceTagCoopReplay));
        }

        private partial class Probe : SpectatorClient
        {
            public readonly TaskCompletionSource<bool> First = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public TaskCompletionSource<bool>? SendGate;
            public int Begins;
            public int Ends;
            public readonly List<double> Times = new List<double>();
            public readonly List<FrameDataBundle> Bundles = new List<FrameDataBundle>();
            public override IBindable<bool> IsConnected { get; } = new BindableBool(true);
            protected override Task<bool> BeginPlayingInternal(long? token, SpectatorState state) => ++Begins == 1 ? First.Task : Task.FromResult(true);
            protected override Task EndPlayingInternal(SpectatorState state) { Ends++; return Task.CompletedTask; }
            protected override Task SendFramesInternal(FrameDataBundle bundle)
            {
                Bundles.Add(bundle);
                foreach (LegacyReplayFrame frame in bundle.Frames) Times.Add(frame.Time);
                return SendGate?.Task ?? Task.CompletedTask;
            }
            protected override Task WatchUserInternal(int id) => Task.CompletedTask;
            protected override Task StopWatchingUserInternal(int id) => Task.CompletedTask;
            protected override Task DisconnectInternal() => Task.CompletedTask;
            public override Task Reconnect() => Task.CompletedTask;
        }
    }
}
