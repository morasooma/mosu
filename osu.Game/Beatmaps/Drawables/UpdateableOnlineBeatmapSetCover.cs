// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;

namespace osu.Game.Beatmaps.Drawables
{
    public partial class UpdateableOnlineBeatmapSetCover : ModelBackedDrawable<IBeatmapSetOnlineInfo>
    {
        private readonly BeatmapSetCoverType coverType;

        public IBeatmapSetOnlineInfo OnlineInfo
        {
            get => Model;
            set => Model = value;
        }

        internal void ResetOnlineInfo(IBeatmapSetOnlineInfo info)
        {
            ResetDrawable();
            OnlineInfo = info;
        }

        public new bool Masking
        {
            get => base.Masking;
            set => base.Masking = value;
        }

        protected override double LoadDelay { get; }

        private readonly double timeBeforeUnload;

        protected override double TransformDuration => 400;

        public UpdateableOnlineBeatmapSetCover(BeatmapSetCoverType coverType = BeatmapSetCoverType.Cover, double timeBeforeLoad = 500, double timeBeforeUnload = 1000,
                                              bool displayPlaceholder = true)
        {
            LoadDelay = timeBeforeLoad;
            this.timeBeforeUnload = timeBeforeUnload;

            this.coverType = coverType;

            InternalChild = displayPlaceholder ? new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = OsuColour.Gray(0.2f),
            } : Empty();
        }

        protected override DelayedLoadWrapper CreateDelayedLoadWrapper(Func<Drawable> createContentFunc, double timeBeforeLoad)
            => new DelayedLoadUnloadWrapper(createContentFunc, timeBeforeLoad, timeBeforeUnload)
            {
                RelativeSizeAxes = Axes.Both,
            };

        protected override Drawable CreateDrawable(IBeatmapSetOnlineInfo model)
        {
            if (model == null)
                return null;

            return new OnlineBeatmapSetCover(model, coverType)
            {
                RelativeSizeAxes = Axes.Both,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                FillMode = FillMode.Fill,
            };
        }
    }
}
