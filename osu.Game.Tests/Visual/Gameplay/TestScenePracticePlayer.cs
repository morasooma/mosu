using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Bindings;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Input.Bindings;
using osu.Game.Overlays;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Catch;
using osu.Game.Rulesets.Dodge;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Taiko;
using osu.Game.Screens.Play;
using osu.Game.Tests.Visual;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Gameplay
{
    [TestFixture("osu")]
    [TestFixture("taiko")]
    [TestFixture("fruits")]
    [TestFixture("mania")]
    [TestFixture("dodge")]
    public partial class TestScenePracticePlayer : PlayerTestScene
    {
        private readonly string rulesetName;
        private readonly Dictionary<Guid, string> originalBindings = new Dictionary<Guid, string>();
        private Guid? addedBinding;

        [Resolved]
        private RealmAccess gameplayRealm { get; set; } = null!;

        [Cached]
        private readonly VolumeOverlay volumeOverlay = new VolumeOverlay();

        [TearDownSteps]
        public void RestoreBindings() => AddStep("restore gameplay bindings", () =>
        {
            InputManager.ReleaseKey(Key.ShiftLeft);
            gameplayRealm.Write(r =>
            {
                foreach (var original in originalBindings)
                    r.Find<RealmKeyBinding>(original.Key)!.KeyCombinationString = original.Value;
                if (addedBinding.HasValue)
                    r.Remove(r.Find<RealmKeyBinding>(addedBinding.Value)!);
            });
            originalBindings.Clear();
            addedBinding = null;
        });

        public TestScenePracticePlayer(string rulesetName)
        {
            this.rulesetName = rulesetName;
        }

        protected override Ruleset CreatePlayerRuleset() => rulesetName switch
        {
            "taiko" => new TaikoRuleset(),
            "fruits" => new CatchRuleset(),
            "mania" => new ManiaRuleset(),
            "dodge" => new DodgeRuleset(),
            _ => new OsuRuleset(),
        };

        protected override IBeatmap CreateBeatmap(RulesetInfo ruleset)
        {
            var beatmap = base.CreateBeatmap(ruleset);
            if (rulesetName != "dodge")
                return beatmap;

            var dodge = new Beatmap<DodgeHitObject> { BeatmapInfo = beatmap.BeatmapInfo };
            for (int i = 0; i < 60; i++)
                dodge.HitObjects.Add(new DodgeBullet { StartTime = 2000 + i * 1000, Duration = 500 });
            return dodge;
        }

        private PracticePlayer practicePlayer => (PracticePlayer)Player;
        protected override bool AllowFail => true;
        protected override TestPlayer CreatePlayer(Ruleset ruleset) => new PracticePlayer();

        [Test]
        public void FailureContinueKeepsScoreAndHealthAndBecomesInvincible()
        {
            double score = 0;
            double health = 0;
            AddStep("trigger failure", () => practicePlayer.Fail());
            AddUntilStep("fail menu visible", () => practicePlayer.Menu.State.Value == Visibility.Visible);
            AddStep("capture state and continue", () =>
            {
                score = practicePlayer.Points;
                health = practicePlayer.Health;
                practicePlayer.Menu.OnPracticeContinue!();
            });
            AddAssert("practice active", () => Player.IsPracticeSession && !Player.GameplayState.HasFailed);
            AddAssert("score preserved", () => practicePlayer.Points == score);
            AddAssert("health preserved", () => practicePlayer.Health == health);
            AddAssert("failure disabled", () => !practicePlayer.CanFail);
            AddAssert("online submission disabled", () => practicePlayer.SubmissionDisabled);
        }

        [Test]
        public void ShiftPausesWithoutMenuAndReleaseDoesNotEnterPractice()
        {
            AddStep("hold shift", () => InputManager.PressKey(Key.ShiftLeft));
            AddUntilStep("timeline visible", () => Player.ChildrenOfType<PracticeTimeline>().Any(t => t.State.Value == Visibility.Visible));
            AddAssert("clock stopped", () => !Player.GameplayClockContainer.IsRunning);
            AddAssert("pause menu hidden", () => !practicePlayer.PauseVisible);
            AddStep("release shift", () => InputManager.ReleaseKey(Key.ShiftLeft));
            AddAssert("not practice", () => !Player.IsPracticeSession);
        }

        [Test]
        public void ShiftClickStartsInvinciblePracticeAtSelectedTime()
        {
            AddStep("hold shift", () => InputManager.PressKey(Key.ShiftLeft));
            AddUntilStep("timeline visible", () => Player.ChildrenOfType<PracticeTimeline>().Any(t => t.State.Value == Visibility.Visible));
            AddStep("select midpoint", () =>
            {
                InputManager.MoveMouseTo(Player.ChildrenOfType<PracticeTimeline>().Single());
                InputManager.Click(MouseButton.Left);
            });
            AddAssert("practice active with checkpoint", () => Player.IsPracticeSession && Player.PracticeStartTime.HasValue);
            AddAssert("failure disabled", () => !practicePlayer.CanFail);
            AddAssert("timeline closed", () => Player.ChildrenOfType<PracticeTimeline>().Single().State.Value == Visibility.Hidden);
            AddStep("release shift", () => InputManager.ReleaseKey(Key.ShiftLeft));
            AddUntilStep("gameplay resumed", () => Player.GameplayClockContainer.IsRunning);
            int hits = 0;
            AddStep("capture judgement count", () => hits = practicePlayer.JudgedHits);
            AddUntilStep("judgement counters advance", () => practicePlayer.JudgedHits > hits);
        }

        [Test]
        public void ShiftGameplayKeySelectsCheckpoint()
        {
            Key key = Key.Unknown;
            AddStep("find active gameplay key", () =>
            {
                key = KeyCombinationKey(Player);
            });
            AddStep("hold shift", () => InputManager.PressKey(Key.ShiftLeft));
            AddUntilStep("timeline visible", () => Player.ChildrenOfType<PracticeTimeline>().Any(t => t.State.Value == Visibility.Visible));
            AddAssert("shift alone does not select", () => !Player.IsPracticeSession);
            AddStep("select with gameplay key", () =>
            {
                InputManager.MoveMouseTo(Player.ChildrenOfType<PracticeTimeline>().Single());
                InputManager.Key(key);
            });
            AddAssert("practice selected", () => Player.IsPracticeSession && Player.PracticeStartTime.HasValue);
            AddAssert("timeline hidden", () => Player.ChildrenOfType<PracticeTimeline>().Single().State.Value == Visibility.Hidden);
            AddStep("release shift", () => InputManager.ReleaseKey(Key.ShiftLeft));
            AddUntilStep("gameplay resumed", () => Player.GameplayClockContainer.IsRunning);
            int hits = 0;
            AddStep("capture hits", () => hits = practicePlayer.JudgedHits);
            AddUntilStep("judgements continue", () => practicePlayer.JudgedHits > hits);
        }

        [Test]
        public void CustomGameplayBindingSelectsCheckpoint()
        {
            AddStep("remap gameplay keys to V", () => gameplayRealm.Write(r =>
            {
                var bindings = r.All<RealmKeyBinding>().Where(b => b.RulesetName == rulesetName).ToArray();
                if (bindings.Length == 0)
                {
                    var ruleset = CreatePlayerRuleset();
                    int variant = ruleset.GetVariantForBeatmap(Player.GameplayState.Beatmap.BeatmapInfo, Player.GameplayState.Mods);
                    var binding = new RealmKeyBinding(Player.DrawableRuleset.GameplayKeyBindings.First().Action, InputKey.V, rulesetName, variant);
                    addedBinding = binding.ID;
                    r.Add(binding);
                }
                foreach (var binding in bindings
                             .Where(b => b.KeyCombination.Keys.Length == 1 && !KeyCombination.IsModifierKey(b.KeyCombination.Keys[0]))
                             .GroupBy(b => b.Variant).Select(group => group.First()))
                {
                    originalBindings[binding.ID] = binding.KeyCombinationString;
                    binding.KeyCombination = InputKey.V;
                }
            }));
            AddUntilStep("active mappings reloaded", () => Player.DrawableRuleset.GameplayKeyBindings.Any(b => b.KeyCombination.Equals((KeyCombination)InputKey.V)));
            AddStep("hold shift", () => InputManager.PressKey(Key.ShiftLeft));
            AddUntilStep("timeline visible", () => Player.ChildrenOfType<PracticeTimeline>().Any(t => t.State.Value == Visibility.Visible));
            AddStep("select with remapped key", () => InputManager.Key(Key.V));
            AddAssert("practice selected", () => Player.IsPracticeSession && Player.PracticeStartTime.HasValue);
            AddStep("release shift", () => InputManager.ReleaseKey(Key.ShiftLeft));
        }

        [Test]
        public void CheckpointSurvivesLoaderRestart()
        {
            PracticePlayer previous = null!;
            PlayerLoader loader = null!;
            double checkpoint = 0;
            AddStep("exit direct player", () => Player.Exit());
            AddUntilStep("player exited", () => !Player.IsCurrentScreen());
            AddStep("enter via loader", () => LoadScreen(loader = new PlayerLoader(() => Player = new PracticePlayer())));
            AddUntilStep("loader player current", () => loader.CurrentPlayer?.IsCurrentScreen() == true);
            AddStep("remember player and hold shift", () =>
            {
                previous = practicePlayer;
                InputManager.PressKey(Key.ShiftLeft);
            });
            AddUntilStep("timeline visible", () => previous.ChildrenOfType<PracticeTimeline>().Any(t => t.State.Value == Visibility.Visible));
            AddStep("select checkpoint", () =>
            {
                InputManager.MoveMouseTo(previous.ChildrenOfType<PracticeTimeline>().Single());
                InputManager.Key(KeyCombinationKey(previous));
            });
            AddUntilStep("checkpoint selected", () => previous.PracticeStartTime.HasValue);
            AddStep("remember checkpoint and release shift", () =>
            {
                checkpoint = previous.PracticeStartTime!.Value;
                InputManager.ReleaseKey(Key.ShiftLeft);
            });
            AddUntilStep("new player current", () => loader.CurrentPlayer != previous && loader.CurrentPlayer?.IsCurrentScreen() == true);
            AddAssert("practice checkpoint retained", () => Player.IsPracticeSession && Player.PracticeStartTime == checkpoint);
            AddAssert("failure disabled", () => !practicePlayer.CanFail);
            AddAssert("gameplay starts at checkpoint", () => Player.GameplayClockContainer.CurrentTime >= checkpoint);
            int hits = 0;
            AddStep("capture hits", () => hits = practicePlayer.JudgedHits);
            AddUntilStep("judgements continue", () => practicePlayer.JudgedHits > hits);
        }

        private static Key KeyCombinationKey(TestPlayer player) => player.DrawableRuleset.GameplayKeyBindings
            .Where(b => b.KeyCombination.Keys.Length == 1)
            .Select(b => b.KeyCombination.Keys[0])
            .Where(k => !KeyCombination.IsModifierKey(k) && k < InputKey.FirstMouseButton)
            .Select(k => (Key)k).First();

        [Test]
        public void ShiftChordDoesNotOpenTimelineUntilShiftIsReleased()
        {
            AddStep("hold control and shift", () =>
            {
                InputManager.PressKey(Key.ControlLeft);
                InputManager.PressKey(Key.ShiftLeft);
            });
            AddWaitStep("allow input processing", 3);
            AddAssert("no timeline", () => !Player.ChildrenOfType<PracticeTimeline>().Any(t => t.State.Value == Visibility.Visible));
            AddStep("release control only", () => InputManager.ReleaseKey(Key.ControlLeft));
            AddWaitStep("allow input processing", 3);
            AddAssert("still no timeline", () => !Player.ChildrenOfType<PracticeTimeline>().Any(t => t.State.Value == Visibility.Visible));
            AddStep("release shift", () => InputManager.ReleaseKey(Key.ShiftLeft));
            AddStep("press shift alone", () => InputManager.PressKey(Key.ShiftLeft));
            AddUntilStep("timeline visible", () => Player.ChildrenOfType<PracticeTimeline>().Any(t => t.State.Value == Visibility.Visible));
            AddStep("move cursor vertically", () => InputManager.MoveMouseTo(Player.ToScreenSpace(new osuTK.Vector2(Player.DrawWidth / 2, Player.DrawHeight / 3))));
            AddUntilStep("timeline follows cursor", () => System.Math.Abs(Player.ChildrenOfType<PracticeTimeline>().Single().Y - Player.DrawHeight / 3) < 1);
            AddStep("add another shortcut key", () => InputManager.PressKey(Key.Tab));
            AddUntilStep("timeline closed", () => Player.ChildrenOfType<PracticeTimeline>().Single().State.Value == Visibility.Hidden);
            AddAssert("not practice", () => !Player.IsPracticeSession);
            AddStep("release keys", () =>
            {
                InputManager.ReleaseKey(Key.Tab);
                InputManager.ReleaseKey(Key.ShiftLeft);
            });
        }

        [Test]
        public void ShiftThenTabDoesNotOpenPracticeTimeline()
        {
            AddStep("press shift then tab before activation", () =>
            {
                InputManager.PressKey(Key.ShiftLeft);
                Scheduler.AddDelayed(() => InputManager.PressKey(Key.Tab), 30);
            });
            AddWaitStep("wait beyond activation delay", 30);
            AddAssert("timeline never opened", () => !Player.ChildrenOfType<PracticeTimeline>().Any());
            AddAssert("not practice", () => !Player.IsPracticeSession);
            AddStep("release tab", () => InputManager.ReleaseKey(Key.Tab));
            AddWaitStep("shift still held", 30);
            AddAssert("timeline still absent", () => !Player.ChildrenOfType<PracticeTimeline>().Any());
            AddStep("release shift", () => InputManager.ReleaseKey(Key.ShiftLeft));
        }

        private partial class PracticePlayer : TestPlayer
        {
            public PracticePlayer() : base(true, false) { }
            public FailOverlay Menu => FailOverlay;
            public double Points => ScoreProcessor.TotalScore.Value;
            public int JudgedHits => ScoreProcessor.JudgedHits;
            public double Health => HealthProcessor.Health.Value;
            public bool CanFail => CheckModsAllowFailure();
            public bool SubmissionDisabled => OnlineRecordSendingDisabled;
            public bool PauseVisible => PauseOverlay.State.Value == Visibility.Visible;
            public void Fail() => HealthProcessor.TriggerFailure();
        }
    }
}
