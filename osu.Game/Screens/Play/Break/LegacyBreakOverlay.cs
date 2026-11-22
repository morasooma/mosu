using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Audio;
using osu.Game.Beatmaps.Timing;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Scoring;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Screens.Play.Break
{
    public partial class LegacyBreakOverlay : SkinReloadableDrawable
    {
        private readonly BreakTracker tracker;
        private readonly Container pass;
        private readonly Container fail;
        private readonly SkinnableSound passSound;
        private readonly SkinnableSound failSound;
        private BreakPeriod? currentBreak;
        private bool passed;
        private double? previousTime;

        public bool Enabled { get; set; }

        public bool IsVisualVisible => Enabled && (pass.Alpha > 0 || fail.Alpha > 0);

        [Resolved(CanBeNull = true)]
        private HealthProcessor? healthProcessor { get; set; }

        public LegacyBreakOverlay(BreakTracker tracker)
        {
            this.tracker = tracker;
            RelativeSizeAxes = Axes.Both;
            InternalChildren = new Drawable[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        pass = new Container { RelativeSizeAxes = Axes.Both, Alpha = 0 },
                        fail = new Container { RelativeSizeAxes = Axes.Both, Alpha = 0 },
                    },
                },
                passSound = new SkinnableSound(new SampleInfo("sectionpass")),
                failSound = new SkinnableSound(new SampleInfo("sectionfail")),
            };
        }

        protected override void SkinChanged(ISkinSource skin)
        {
            base.SkinChanged(skin);
            setSprite(pass, skin, "section-pass", "PASS");
            setSprite(fail, skin, "section-fail", "FAIL");
        }

        private static void setSprite(Container container, ISkinSource skin, string name, string fallback)
        {
            // GetTexture honours legacy @2x textures and normal skin-source fallback.
            var texture = skin.GetTexture(name);
            container.Child = texture != null
                ? new Sprite { Texture = texture, Anchor = Anchor.Centre, Origin = Anchor.Centre }
                : new OsuSpriteText { Text = fallback, Font = osu.Game.Graphics.OsuFont.Default.With(size: 40), Anchor = Anchor.Centre, Origin = Anchor.Centre };
        }

        protected override void Update()
        {
            base.Update();

            double now = Time.Current;
            var nextBreak = tracker.CurrentBreak.Value;
            if (currentBreak != nextBreak || now < previousTime)
            {
                currentBreak = nextBreak;
                // Stable's health range is 0..200 and section pass requires >=100.
                passed = (healthProcessor?.Health.Value ?? 1) >= 0.5;
                passSound.Stop();
                failSound.Stop();
            }

            pass.Alpha = fail.Alpha = 0;
            if (!Enabled)
            {
                passSound.Stop();
                failSound.Stop();
            }

            if (currentBreak != null && LegacyBreakPresentation.GetStartTime(currentBreak.StartTime, currentBreak.EndTime) is double start)
            {
                double elapsed = now - start;
                if (Enabled)
                {
                    (passed ? pass : fail).Alpha = LegacyBreakPresentation.GetAlpha(elapsed, passed);
                    double soundTime = start + (passed ? 20 : 130);
                    // Only cross the cue during normal playback, never on seeks or initial load.
                    if (previousTime is double previous && previous < soundTime && now >= soundTime && now - previous <= 250)
                        (passed ? passSound : failSound).Play();
                }
            }

            // Stable positions use a 480-high screen, but skin texture pixels use
            // a 768-high reference (the stable sprite renderer divides by 1.6).
            // GetTexture already handles @2x; do not apply the position scale to pixels.
            pass.Child.Scale = fail.Child.Scale = new Vector2(LegacyBreakPresentation.GetTextureScale(DrawHeight));

            previousTime = now;
        }
    }
}
