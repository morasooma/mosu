// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Overlays;

namespace osu.Game.Online.Placeholders
{
    public abstract partial class Placeholder : OsuTextFlowContainer, IEquatable<Placeholder>
    {
        protected const float TEXT_SIZE = 22;

        protected Placeholder()
            : base(cp => cp.Font = cp.Font.With(size: TEXT_SIZE))
        {
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
            TextAnchor = Anchor.TopCentre;

            Padding = new MarginPadding(20);

            AutoSizeAxes = Axes.Y;
            RelativeSizeAxes = Axes.X;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            IBindable<Colour4> themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
            themeColour.BindValueChanged(_ => Colour = colourProvider.Content1, true);
        }

        public virtual bool Equals(Placeholder other) => GetType() == other?.GetType();
    }
}
