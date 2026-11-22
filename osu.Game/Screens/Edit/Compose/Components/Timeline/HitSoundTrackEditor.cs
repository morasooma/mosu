using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Audio;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Screens.Edit.Components.Timelines.Summary.Parts;
using osuTK;
using osuTK.Input;

namespace osu.Game.Screens.Edit.Compose.Components.Timeline
{
    /// <summary>
    /// Sample lanes sharing the compose timeline's scroll and zoom. Re-resolves sample lists
    /// after every update, as defaults and undo can replace slider node samples.
    /// </summary>
    public partial class HitSoundTrackEditor : TimelinePart
    {
        public const float TRACK_HEIGHT = 152;
        private readonly Dictionary<HitObject, List<Drawable>> points = new Dictionary<HitObject, List<Drawable>>();
        private readonly HashSet<HitObject> pending = new HashSet<HitObject>();

        protected override void LoadComplete()
        {
            base.LoadComplete();
            EditorBeatmap.HitObjectAdded += queueUpdate;
            EditorBeatmap.HitObjectRemoved += queueUpdate;
            EditorBeatmap.HitObjectUpdated += queueUpdate;
            foreach (var hitObject in EditorBeatmap.HitObjects)
                queueUpdate(hitObject);
        }

        private void queueUpdate(HitObject hitObject)
        {
            pending.Add(hitObject);
            Scheduler.AddOnce(refresh);
        }

        private void refresh()
        {
            foreach (var hitObject in pending)
            {
                if (points.Remove(hitObject, out var oldPoints))
                    foreach (var point in oldPoints)
                        Remove(point, true);

                if (!EditorBeatmap.HitObjects.Contains(hitObject))
                    continue;

                var newPoints = points[hitObject] = new List<Drawable>();
                addPoint(-1);
                if (hitObject is IHasRepeats repeats)
                    for (int i = 0; i < Math.Min(repeats.NodeSamples.Count, repeats.RepeatCount + 2); i++)
                        addPoint(i);

                void addPoint(int node)
                {
                    double time = hitObject.StartTime;
                    if (hitObject is IHasRepeats r)
                        time += r.Duration / r.SpanCount() * (node < 0 ? 0.5 : node);
                    var point = new SampleColumn(EditorBeatmap, hitObject, node)
                    {
                        RelativePositionAxes = Axes.X,
                        X = (float)time,
                        Origin = Anchor.TopCentre,
                    };
                    newPoints.Add(point);
                    Add(point);
                }
            }
            pending.Clear();
        }

        protected override void Dispose(bool isDisposing)
        {
            EditorBeatmap.HitObjectAdded -= queueUpdate;
            EditorBeatmap.HitObjectRemoved -= queueUpdate;
            EditorBeatmap.HitObjectUpdated -= queueUpdate;
            base.Dispose(isDisposing);
        }

        private partial class SampleColumn : CompositeDrawable
        {
            private readonly EditorBeatmap beatmap;
            private readonly HitObject hitObject;
            private readonly int node;
            private IList<HitSampleInfo> samples => node < 0 ? hitObject.Samples : ((IHasRepeats)hitObject).NodeSamples[node];

            public SampleColumn(EditorBeatmap beatmap, HitObject hitObject, int node)
            {
                this.beatmap = beatmap;
                this.hitObject = hitObject;
                this.node = node;
                Size = new Vector2(18, TRACK_HEIGHT);
            }

            [BackgroundDependencyLoader]
            private void load(OsuColour colours)
            {
                string[] additions = { HitSampleInfo.HIT_WHISTLE, HitSampleInfo.HIT_FINISH, HitSampleInfo.HIT_CLAP };
                for (int i = 0; i < additions.Length; i++)
                {
                    string name = additions[i];
                    bool active = samples.Any(s => s.Name == name);
                    AddInternal(new TrackToggle
                    {
                        Y = i * 22,
                        Size = new Vector2(18, 20),
                        Colour = active ? colours.Pink : OsuColour.Gray(0.5f),
                        Alpha = active ? 1 : 0.35f,
                        TooltipText = HitSoundTracksStrings.SampleTooltip(name, node),
                        Child = new Box { RelativeSizeAxes = Axes.Both },
                        Action = () => commit(s => HitSoundTrackSamples.Toggle(hitObject, s, name)),
                    });
                }

                string bank = SamplePointPiece.GetBankValue(samples) ?? HitSampleInfo.BANK_NORMAL;
                AddInternal(new TrackToggle
                {
                    Y = 66,
                    Size = new Vector2(18, 22),
                    TooltipText = HitSoundTracksStrings.BankTooltip(bank),
                    Child = new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Font = OsuFont.Default.With(size: 13),
                        Text = bank.Length > 0 ? bank.Substring(0, 1).ToUpperInvariant() : "?",
                    },
                    Action = () => commit(s => HitSoundTrackSamples.SetBank(s,
                        HitSampleInfo.ALL_BANKS[(Array.IndexOf(HitSampleInfo.ALL_BANKS, bank) + 1) % HitSampleInfo.ALL_BANKS.Length])),
                });
                AddInternal(new VolumeBar(SamplePointPiece.GetVolumeValue(samples), v => commit(s => HitSoundTrackSamples.SetVolume(s, v)))
                {
                    Y = 90,
                    Size = new Vector2(18, 60),
                    Colour = colours.BlueLighter,
                });
            }

            private void commit(Action<IList<HitSampleInfo>> change)
            {
                beatmap.BeginChange();
                try
                {
                    change(samples);
                    beatmap.Update(hitObject);
                }
                finally
                {
                    beatmap.EndChange();
                }
            }
        }

        private partial class TrackToggle : ClickableContainer, IHasTooltip
        {
            public LocalisableString TooltipText { get; set; }
        }

        private partial class VolumeBar : CompositeDrawable, IHasTooltip
        {
            private readonly Box fill;
            private readonly Action<int> commit;
            private int value;
            public LocalisableString TooltipText => HitSoundTracksStrings.VolumeTooltip(value);

            public VolumeBar(int volume, Action<int> commit)
            {
                value = volume;
                this.commit = commit;
                InternalChildren = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Alpha = 0.15f },
                    fill = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        Height = volume / 100f,
                    },
                };
            }

            private void preview(Vector2 position)
            {
                if (DrawHeight <= 0) return;
                value = Math.Clamp((int)Math.Round(100 * (1 - position.Y / DrawHeight)), 5, 100);
                fill.Height = value / 100f;
            }

            protected override bool OnMouseDown(MouseDownEvent e) => e.Button == MouseButton.Left;
            protected override bool OnClick(ClickEvent e)
            {
                if (e.Button != MouseButton.Left) return false;
                preview(ToLocalSpace(e.ScreenSpaceMousePosition));
                commit(value);
                return true;
            }

            protected override bool OnDragStart(DragStartEvent e)
            {
                if (e.Button != MouseButton.Left) return false;
                preview(ToLocalSpace(e.ScreenSpaceMousePosition));
                return true;
            }

            protected override void OnDrag(DragEvent e) => preview(ToLocalSpace(e.ScreenSpaceMousePosition));
            protected override void OnDragEnd(DragEndEvent e) => commit(value);
        }
    }

    public static class HitSoundTrackSamples
    {
        public static void Toggle(HitObject hitObject, IList<HitSampleInfo> samples, string name)
        {
            if (samples.Any(s => s.Name == name))
            {
                for (int i = samples.Count - 1; i >= 0; i--)
                    if (samples[i].Name == name) samples.RemoveAt(i);
            }
            else
            {
                var source = samples.FirstOrDefault(s => s.Name != HitSampleInfo.HIT_NORMAL) ?? samples.FirstOrDefault();
                samples.Add(source?.With(name) ?? hitObject.CreateHitSampleInfo(name));
            }
        }

        public static void SetBank(IList<HitSampleInfo> samples, string bank)
        {
            for (int i = 0; i < samples.Count; i++)
                if (samples[i].Name == HitSampleInfo.HIT_NORMAL || samples[i].EditorAutoBank)
                    samples[i] = samples[i].With(newBank: bank);
        }

        public static void SetVolume(IList<HitSampleInfo> samples, int volume)
        {
            volume = Math.Clamp(volume, 5, 100);
            for (int i = 0; i < samples.Count; i++)
                samples[i] = samples[i].With(newVolume: volume);
        }
    }
}
