using System;
using System.Linq;
using System.Threading;
using osu.Game.Audio;
using osu.Game.Skinning;
using osu.Game.Beatmaps;
using osu.Framework.Graphics.Shapes;
using osuTK.Graphics;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Screens;
using osu.Framework.Input;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Screens;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Components;
using osuTK;

namespace osu.Game.Rulesets.Osu.Edit
{
    /// <summary>A separate gameplay-sized recording session. Editor mutations happen only on exit.</summary>
    public partial class RealtimeMappingScreen : OsuScreen
    {
        private readonly EditorBeatmap target;
        private readonly double startTime;
        private readonly double offset;
        private readonly Action<HitObject[]> commit;
        private readonly BindableDouble tempo = new BindableDouble(1);
        private EditorClock recordingClock = null!;
        private DrawableOsuRuleset ruleset = null!;
        private RealtimeMappingRecorder recorder = null!;
        private OsuHitObject? currentPreview;
        private bool committed;
        private bool started;
        private InputManager liveInput = null!;
        private readonly SkinnableSound recordingSound = new SkinnableSound(new HitSampleInfo(HitSampleInfo.HIT_NORMAL));

        public override string Title => "Real-time mapping";
        public override bool HideOverlaysOnEnter => true;
        public override bool? AllowGlobalTrackControl => false;
        public override bool? ApplyModTrackAdjustments => false;
        public override bool CursorVisible => false;

        public RealtimeMappingScreen(EditorBeatmap target, double startTime, double offset, Action<HitObject[]> commit)
        {
            this.target = target;
            this.startTime = startTime;
            this.offset = offset;
            this.commit = commit;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var empty = new OsuBeatmap
            {
                BeatmapInfo = target.BeatmapInfo,
                ControlPointInfo = target.ControlPointInfo,
            };
            var copier = new RecordingObjectCopier(target);
            foreach (var obj in target.HitObjects)
            {
                var copy = copier.Copy(obj);
                copy.ApplyDefaults(target.ControlPointInfo, target.Difficulty);
                empty.HitObjects.Add(copy);
            }
            recordingClock = new EditorClock(empty);
            ruleset = new DrawableOsuRuleset(new OsuRuleset(), empty) { Clock = recordingClock };
            recorder = new RealtimeMappingRecorder(target, recordingClock,
                () =>
                {
                    // Read the live screen input, not the gameplay manager's potentially transformed cursor.
                    Vector2 position = liveInput.CurrentState.Mouse.Position;
                    Vector2 local = ruleset.Playfield.HitObjectContainer.ToLocalSpace(position);
                    return local.X >= 0 && local.Y >= 0 && local.X <= 512 && local.Y <= 384
                        ? local : (Vector2?)null;
                },
                updatePreview) { Offset = offset, NotePressed = () => recordingSound.Play() };
            var metronome = new RecordingMetronome(target, recordingClock);
            var metronomeButton = new RoundedButton
            {
                Text = global::osu.Game.Localisation.ClientTranslationStrings.FormatJ3329(target.BeatDivisor),
                Size = new Vector2(300, 35),
            };
            metronomeButton.Action = () =>
            {
                metronome.Enabled = !metronome.Enabled;
                metronomeButton.Text = global::osu.Game.Localisation.ClientTranslationStrings.FormatJ3330((metronome.Enabled ? "ON" : "OFF"), target.BeatDivisor);
            };
            InternalChildren = new Drawable[]
            {
                recordingClock,
                recordingSound,
                metronome,
                ruleset,
                new OsuPlayfieldAdjustmentContainer
                {
                    AlignWithStoryboard = true,
                    Child = new Container
                    {
                        Size = new Vector2(512, 384),
                        BorderThickness = 2,
                        BorderColour = Color4.Cyan,
                        Masking = true,
                        Child = new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.Cyan, Alpha = 0.025f },
                    },
                },
                new FillFlowContainer
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 8),
                    Padding = new MarginPadding(16),
                    Children = new Drawable[]
                    {
                        new OsuSpriteText { Text = global::osu.Game.Localisation.ClientTranslationStrings.J3331 },
                        metronomeButton,
                        new PlaybackControl.PlaybackTabControl { Current = tempo, Size = new Vector2(300, 25), RelativeSizeAxes = Axes.None },
                        new RoundedButton
                        {
                            Text = global::osu.Game.Localisation.ClientTranslationStrings.J3332,
                            Size = new Vector2(300, 35),
                            Action = () =>
                            {
                                if (started)
                                    return;
                                started = true;
                                recorder.Toggle();
                            },
                        },
                        new RoundedButton
                        {
                            Text = global::osu.Game.Localisation.ClientTranslationStrings.J3333,
                            Size = new Vector2(300, 35),
                            Action = () => this.Exit(),
                        },
                    }
                },
            };
        }

        private sealed class RecordingObjectCopier : OsuBeatmapConverter
        {
            private readonly IBeatmap source;
            public RecordingObjectCopier(IBeatmap source) : base(source, new OsuRuleset()) => this.source = source;
            public OsuHitObject Copy(HitObject obj) => ConvertHitObject(obj, source, CancellationToken.None).Single();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            liveInput = GetContainingInputManager();
            // DrawableRuleset installs its own children during load; attach after that, on top
            // of hit receptors, so recording consumes the configured gameplay actions first.
            ruleset.KeyBindingInputManager.KeyBindingContainer.Add(recorder);
        }

        private void updatePreview(HitObject? obj)
        {
            if (currentPreview != null)
                ruleset.RemoveHitObject(currentPreview);
            currentPreview = obj as OsuHitObject;
            if (currentPreview != null)
            {
                currentPreview.ApplyDefaults(target.ControlPointInfo, target.Difficulty);
                ruleset.AddHitObject(currentPreview);
            }
        }

        public override void OnEntering(ScreenTransitionEvent e)
        {
            base.OnEntering(e);
            recordingClock.AudioAdjustments.AddAdjustment(AdjustableProperty.Tempo, tempo);
            recordingClock.ChangeSource(Beatmap.Value.Track);
            recordingClock.Seek(startTime);
        }

        protected override void Update()
        {
            base.Update();
            if (started && recorder != null && !recorder.Recording && !committed && this.IsCurrentScreen())
                this.Exit();
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            if (base.OnExiting(e))
                return true;
            if (!committed)
            {
                recorder.Stop();
                recordingClock.Stop();
                recordingClock.UnbindAdjustments();
                committed = true;
                var objects = new HitObject[recorder.RecordedObjects.Count];
                for (int i = 0; i < objects.Length; i++)
                    objects[i] = recorder.RecordedObjects[i];
                commit(objects);
            }
            return false;
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing && recordingClock != null)
                recordingClock.UnbindAdjustments();
            base.Dispose(isDisposing);
        }
    }
}
