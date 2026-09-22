// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Online.Legacy;

namespace osu.Game.Screens.OnlinePlay.Multiplayer.Match
{
    public partial class MultiplayerMatchFooter : CompositeDrawable
    {
        private const float ready_button_width = 600;
        private const float spectate_button_width = 200;

        [Resolved(CanBeNull = true)]
        private StableBanchoSession? stableBanchoSession { get; set; }

        public MultiplayerMatchFooter()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            bool supportsSpectating = stableBanchoSession == null;

            InternalChild = new GridContainer
            {
                RelativeSizeAxes = Axes.Both,
                Content = new[]
                {
                    new Drawable?[]
                    {
                        null,
                        supportsSpectating ? new MultiplayerSpectateButton
                        {
                            RelativeSizeAxes = Axes.Both,
                        } : null,
                        null,
                        new MatchStartControl
                        {
                            RelativeSizeAxes = Axes.Both,
                        },
                        null
                    }
                },
                ColumnDimensions = new[]
                {
                    new Dimension(),
                    supportsSpectating ? new Dimension(maxSize: spectate_button_width) : new Dimension(GridSizeMode.Absolute, 0),
                    new Dimension(GridSizeMode.Absolute, supportsSpectating ? 5 : 0),
                    new Dimension(maxSize: ready_button_width),
                    new Dimension()
                }
            };
        }
    }
}
