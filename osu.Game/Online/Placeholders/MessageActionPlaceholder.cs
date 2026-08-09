// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;

namespace osu.Game.Online.Placeholders
{
    public partial class MessageActionPlaceholder : Placeholder
    {
        public Action Action;

        private readonly LocalisableString message;
        private readonly LocalisableString actionMessage;

        public MessageActionPlaceholder(LocalisableString message, LocalisableString actionMessage, IconUsage actionIcon)
        {
            this.message = message;
            this.actionMessage = actionMessage;

            AddIcon(FontAwesome.Solid.ExclamationCircle, cp =>
            {
                cp.Font = cp.Font.With(size: TEXT_SIZE);
                cp.Padding = new MarginPadding { Right = 10 };
            });

            AddText(message);
            NewLine();

            OsuAnimatedButton button;
            OsuTextFlowContainer textFlow;

            AddArbitraryDrawable(new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Padding = new MarginPadding { Top = 15 },
                Child = button = new OsuAnimatedButton
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    AutoSizeAxes = Axes.Both,
                    Action = () => Action?.Invoke()
                }
            });

            button.Add(textFlow = new OsuTextFlowContainer(cp => cp.Font = cp.Font.With(size: TEXT_SIZE))
            {
                AutoSizeAxes = Axes.Both,
                Margin = new MarginPadding(5)
            });

            textFlow.AddIcon(actionIcon, i =>
            {
                i.Padding = new MarginPadding { Right = 10 };
            });

            textFlow.AddText(actionMessage);
        }

        public override bool Equals(Placeholder other)
            => other is MessageActionPlaceholder actionPlaceholder
               && actionPlaceholder.message == message
               && actionPlaceholder.actionMessage == actionMessage;
    }
}
