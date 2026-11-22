// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Leaderboards;
using osu.Game.Rulesets;
using osu.Game.Scoring;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Screens.Select
{
    public partial class PanelLocalRankDisplay : CompositeDrawable
    {
        private BeatmapInfo? beatmap;
        private string? beatmapHash;
        private bool active = true;
        private RankSnapshotCoordinator? coordinator;
        private int generation;

        public bool Active
        {
            get => active;
            set
            {
                if (active == value)
                    return;

                active = value;
                if (IsLoaded)
                    updateSnapshot();
            }
        }

        public BeatmapInfo? Beatmap
        {
            get => beatmap;
            set
            {
                string? hash = value?.Hash;
                beatmap = value;
                if (beatmapHash == hash)
                    return;

                beatmapHash = hash;
                if (IsLoaded)
                    updateSnapshot();
            }
        }

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        private readonly IBindable<APIUser> localUser = new Bindable<APIUser>();
        private readonly CarouselRank updateable;

        public bool UseLegacyStyle
        {
            get => updateable.UseLegacyStyle;
            set => updateable.UseLegacyStyle = value;
        }

        public bool HasRank => updateable.Rank != null;
        internal ScoreRank? CurrentRank => updateable.Rank;
        internal string? ActiveHash => active && !IsDisposed ? beatmapHash : null;
        internal string RulesetName => ruleset.Value.ShortName;
        internal int UserId => localUser.Value.Id;
        internal int Generation => generation;

        public PanelLocalRankDisplay(BeatmapInfo? beatmap = null)
        {
            AutoSizeAxes = Axes.Both;
            InternalChild = updateable = new CarouselRank()
            {
                Size = new Vector2(40, 20),
                Alpha = 0,
            };
            Beatmap = beatmap;
        }

        [BackgroundDependencyLoader]
        private void load(IAPIProvider api) => localUser.BindTo(api.LocalUser);

        protected override void LoadComplete()
        {
            base.LoadComplete();
            ruleset.BindValueChanged(_ => updateSnapshot());
            localUser.BindValueChanged(_ => updateSnapshot(), true);
        }

        private void updateSnapshot()
        {
            generation++;
            updateable.Rank = null;
            updateable.Alpha = 0;

            if (ActiveHash == null)
            {
                coordinator?.Remove(this);
                coordinator = null;
                return;
            }

            coordinator ??= RankSnapshotCoordinator.For(realm);
            coordinator.AddOrUpdate(this);
        }

        internal void ApplySnapshot(ScoreRank? rank, int requestedGeneration)
        {
            if (IsDisposed || !active || generation != requestedGeneration || updateable.Rank == rank)
                return;

            updateable.Rank = rank;
            updateable.Alpha = rank != null ? 1 : 0;
        }

        internal void ScheduleSnapshotDelayed(Action action, double remainingMs) => Scheduler.AddDelayed(action, remainingMs);

        private partial class CarouselRank : UpdateableRank
        {
            private bool useLegacyStyle;

            public bool UseLegacyStyle
            {
                get => useLegacyStyle;
                set
                {
                    if (useLegacyStyle == value)
                        return;

                    useLegacyStyle = value;
                    // Recreate the presentation even when the score rank itself is unchanged.
                    var rank = Rank;
                    Rank = null;
                    Rank = rank;
                }
            }

            public CarouselRank()
                : base(animate: false)
            {
            }

            protected override Drawable? CreateDrawable(ScoreRank? rank)
            {
                if (!useLegacyStyle || !rank.HasValue)
                    return base.CreateDrawable(rank);

                return new LegacyCarouselRank(rank.Value);
            }
        }

        internal partial class LegacyCarouselRank : SkinReloadableDrawable
        {
            internal string TextureName { get; }
            internal Sprite Sprite { get; }

            [Resolved]
            private SkinManager skinManager { get; set; } = null!;

            public LegacyCarouselRank(ScoreRank rank)
            {
                TextureName = $"ranking-{rank}-small";
                RelativeSizeAxes = Axes.Both;
                Anchor = Anchor.Centre;
                Origin = Anchor.Centre;
                InternalChild = Sprite = new Sprite
                {
                    RelativeSizeAxes = Axes.Both,
                    FillMode = FillMode.Fit,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                };
            }

            protected override void SkinChanged(ISkinSource skin)
            {
                base.SkinChanged(skin);
                // Modern built-in skins do not supply stable's grade textures.
                Sprite.Texture = skin.GetTexture(TextureName) ?? skinManager.DefaultClassicSkin.GetTexture(TextureName);
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            generation++;
            coordinator?.Remove(this);
            coordinator = null;
            base.Dispose(isDisposing);
        }
    }
}
