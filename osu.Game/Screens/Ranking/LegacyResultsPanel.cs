using System.Globalization;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Extensions;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Ranking
{
    /// <summary>
    /// Stable ranking layout. Positions use 480-height coordinates; skin textures
    /// use 768-height coordinates. Keep those two transforms separate.
    /// Source: stable ranking class ztagvdsX3MduXECXXVsw6GQA5UEhUa10vDZzNd5g,
    /// Up7RsTwwjl0h and VuZKloNObg4whxW7ZivYgzF6uFIe4BcyOvpvz60.
    /// </summary>
    public partial class LegacyResultsPanel : SkinReloadableDrawable
    {
        private const float coordinate_scale = 1.6f;
        public readonly Bindable<ScoreInfo?> Score = new Bindable<ScoreInfo?>();
        private readonly Container canvas;
        private bool newSkinLayout;
        internal Vector2 CanvasScale => canvas.Scale;
        internal float RequiredScrollHeight { get; private set; }

        protected override void SkinChanged(ISkinSource skin)
        {
            base.SkinChanged(skin);
            newSkinLayout = (skin.GetConfig<SkinConfiguration.LegacySetting, decimal>(SkinConfiguration.LegacySetting.Version)?.Value ?? 1) > 1;
            if (IsLoaded)
                rebuild();
        }

        public LegacyResultsPanel()
        {
            RelativeSizeAxes = Axes.Both;
            InternalChild = canvas = new Container { Size = new Vector2(1024, 768) };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Score.BindValueChanged(_ => rebuild(), true);
        }

        protected override void Update()
        {
            base.Update();
            if (DrawHeight <= 0) return;
            canvas.Scale = new Vector2(DrawHeight / 768);
            canvas.Width = DrawWidth / canvas.Scale.X;
            float bottom = 768;
            foreach (var child in canvas.Children)
            {
                float edge = canvas.ToLocalSpace(child.ScreenSpaceDrawQuad.BottomLeft).Y;
                bottom = System.Math.Max(bottom, edge);
            }
            RequiredScrollHeight = bottom * canvas.Scale.Y;
        }

        private void rebuild()
        {
            canvas.Clear();
            var score = Score.Value;
            if (score == null) return;

            image("ranking-panel", new Vector2(0, newSkinLayout ? 64 : 46));
            canvas.Add(new Box { RelativeSizeAxes = Axes.X, Height = 96, Colour = Color4.Black.Opacity(0.8f) });
            image("ranking-title", new Vector2(-20, 0), Anchor.TopRight, Anchor.TopRight);

            var metadata = score.BeatmapInfo?.Metadata;
            text($"{metadata?.Artist} - {metadata?.Title} [{score.BeatmapInfo?.DifficultyName}]", new Vector2(0, 0), 22);
            text($"Beatmap by {metadata?.Author.Username}", new Vector2(1, 20), 16);
            text($"Played by {score.User.Username} on {score.Date.LocalDateTime:g}", new Vector2(1, 34), 16);

            digits(score.GetLeaderboardScore().ToString("D8", CultureInfo.InvariantCulture), new Vector2(220, 94), Anchor.Centre, newSkinLayout ? 1.3f : 1.05f);

            var counts = score.Statistics;
            int count(HitResult result) => counts.TryGetValue(result, out int n) ? n : 0;
            // Stable has mode-specific statistics; do not reinterpret custom rulesets.
            switch (score.Ruleset.CreateNormalRuleset().OnlineID)
            {
                case 0:
                    judgement("hit300", 40, 160, count(HitResult.Great));
                    judgement("hit100", 40, 220, count(HitResult.Ok));
                    judgement("hit50", 40, 280, count(HitResult.Meh));
                    judgement("hit300g", 240, 160, score.GetCountGeki() ?? 0);
                    judgement("hit100k", 240, 220, score.GetCountKatu() ?? 0);
                    judgement("hit0", 240, 280, count(HitResult.Miss));
                    break;
                case 1:
                    judgement("taiko-hit300", 40, 160, count(HitResult.Great));
                    judgement("taiko-hit100", 40, 220, count(HitResult.Ok));
                    judgement("hit0", 40, 280, count(HitResult.Miss));
                    break;
                case 3:
                    judgement("mania-hit300", 40, 160, count(HitResult.Great));
                    judgement("mania-hit200", 40, 220, count(HitResult.Good));
                    judgement("mania-hit50", 40, 280, count(HitResult.Meh));
                    judgement("mania-hit300g", 240, 160, count(HitResult.Perfect));
                    judgement("mania-hit100", 240, 220, count(HitResult.Ok));
                    judgement("mania-hit0", 240, 280, count(HitResult.Miss));
                    break;
                default:
                    // No invented debug-statistics fallback on the ranking screen.
                    break;
            }

            image("ranking-maxcombo", new Vector2(5, newSkinLayout ? 300 : 312));
            digits($"{score.MaxCombo}x", new Vector2(15, 330));
            image("ranking-accuracy", new Vector2(182, newSkinLayout ? 300 : 312));
            digits((score.Accuracy * 100).ToString("0.00", CultureInfo.InvariantCulture) + "%", new Vector2(194, 330));
            string rank = score.Rank.ToString();
            // Stable field type 8 mirrors the X coordinate from the right edge;
            // its sprite origin is Centre, not a right-aligned bounding box.
            image("ranking-" + rank, new Vector2(-120, newSkinLayout ? 200 : 170), Anchor.Centre, Anchor.TopRight);
            // The stable perfect banner requires full-combo evidence, not just
            // zero misses (slider breaks also invalidate it). Leave it absent
            // until that evidence is available for the displayed score.
        }

        private void judgement(string name, float x, float y, int count)
        {
            image(name, new Vector2(x, y), Anchor.Centre, scale: 0.5f);
            digits($"{count}x", new Vector2(x + 40, y + (newSkinLayout ? -16 : -25)), scale: 1.12f);
        }

        private void digits(string value, Vector2 position, Anchor origin = Anchor.TopLeft, float scale = 1.12f)
        {
            canvas.Add(new LegacySpriteText(LegacyFont.Score)
            {
                Text = value, Position = position * coordinate_scale, Origin = origin,
                Scale = new Vector2(scale),
            });
        }

        private void text(string value, Vector2 position, float points)
            => canvas.Add(new OsuSpriteText
            {
                Text = value, Position = position * coordinate_scale,
                Font = OsuFont.GetFont(size: points * 96 / 72), Shadow = true,
            });

        private void image(string name, Vector2 position, Anchor origin = Anchor.TopLeft, Anchor anchor = Anchor.TopLeft, float scale = 1)
            => canvas.Add(new RankingImage(name)
            {
                Position = position * coordinate_scale, Origin = origin, Anchor = anchor, Scale = new Vector2(scale),
            });

        private partial class RankingImage : SkinReloadableDrawable
        {
            private readonly string name;
            private readonly Sprite sprite;
            public RankingImage(string name)
            {
                this.name = name;
                AutoSizeAxes = Axes.Both;
                InternalChild = sprite = new Sprite();
            }

            protected override void SkinChanged(ISkinSource skin)
            {
                base.SkinChanged(skin);
                sprite.Texture = skin.GetTexture(name);
            }
        }
    }
}
