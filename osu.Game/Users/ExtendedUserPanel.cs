// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Users.Drawables;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Metadata;
using osu.Game.Online;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osuTK.Graphics;

namespace osu.Game.Users
{
    public abstract partial class ExtendedUserPanel : UserPanel
    {
        protected TextFlowContainer LastVisitMessage { get; private set; } = null!;

        private StatusIcon statusIcon = null!;
        private StatusText statusMessage = null!;

        [Resolved]
        private MetadataClient? metadata { get; set; }

        private UserStatus? lastStatus;
        private UserActivity? lastActivity;
        private string? lastClientLabel;
        private DateTimeOffset? lastVisit;
        private IBindable<Colour4>? themeColour;

        protected ExtendedUserPanel(APIUser user)
            : base(user)
        {
            lastVisit = user.LastVisit;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            BorderColour = ColourProvider?.Light1 ?? Colours.GreyVioletLighter;

            if (ColourProvider != null)
            {
                themeColour = ColourProvider.GetColourBindable(OverlayColour.Content2);
                themeColour.BindValueChanged(colour =>
                {
                    LastVisitMessage.Colour = colour.NewValue;
                    statusMessage.Colour = colour.NewValue;
                }, true);
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            updatePresence();

            // Colour should be applied immediately on first load.
            statusIcon.FinishTransforms();
        }

        protected override void Update()
        {
            base.Update();
            updatePresence();
        }

        protected Container CreateStatusIcon() => statusIcon = new StatusIcon();

        protected FillFlowContainer CreateStatusMessage(bool rightAlignedChildren)
        {
            var statusContainer = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical
            };

            var alignment = rightAlignedChildren ? Anchor.CentreRight : Anchor.CentreLeft;

            statusContainer.Add(LastVisitMessage = new TextFlowContainer(t => t.Font = OsuFont.GetFont(size: 12, weight: FontWeight.SemiBold)).With(text =>
            {
                text.Anchor = alignment;
                text.Origin = alignment;
                text.AutoSizeAxes = Axes.Both;
                text.Alpha = 0;
            }));

            statusContainer.Add(statusMessage = new StatusText
            {
                Anchor = alignment,
                Origin = alignment,
                Font = OsuFont.GetFont(size: 14, weight: FontWeight.SemiBold)
            });

            return statusContainer;
        }

        private void updatePresence()
        {
            // TODO: we probably don't want to do this every frame.
            UserPresence? presence = metadata?.GetPresence(User.OnlineID);
            UserStatus status = presence?.Status ?? UserStatus.Offline;
            UserActivity? activity = presence?.Activity;

            string clientLabel = !MosuServerEnvironment.UsesMosuPresence || presence == null ? string.Empty
                : string.Join(" · ", new[] { presence.Value.Platform, presence.Value.BuildKind }
                    .Where(value => !string.IsNullOrWhiteSpace(value)));
            if (status == lastStatus && activity == lastActivity && clientLabel == lastClientLabel)
                return;

            if (status == UserStatus.Offline && lastVisit != null)
            {
                LastVisitMessage.FadeTo(1);
                LastVisitMessage.Clear();
                LastVisitMessage.AddText(global::osu.Game.Localisation.ClientTranslationStrings.J5654);
                LastVisitMessage.AddText(new DrawableDate(lastVisit.Value, italic: false)
                {
                    Shadow = false
                });
            }
            else
                LastVisitMessage.FadeTo(0);

            if (activity == null || status == UserStatus.Offline)
            {
                statusMessage.Text = status.GetLocalisableDescription();
                statusMessage.TooltipText = string.Empty;
            }
            else
            {
                statusMessage.Text = MosuServerEnvironment.UsesMosuPresence ? PresenceStrings.Activity(activity) : activity.GetStatus();
                statusMessage.TooltipText = activity.GetDetails() ?? string.Empty;
            }

            if (activity == null || status != UserStatus.Online)
                statusIcon.FadeColour(status.GetAppropriateColour(Colours), 500, Easing.OutQuint);
            else
                statusIcon.FadeColour(activity.GetAppropriateColour(Colours), 500, Easing.OutQuint);

            if (MosuServerEnvironment.UsesMosuPresence && status != UserStatus.Offline)
            {
                LastVisitMessage.Clear();
                LastVisitMessage.AddText(PresenceStrings.Client(presence!.Value));
                LastVisitMessage.FadeTo(string.IsNullOrEmpty(clientLabel) ? 0 : 1);
                statusMessage.TooltipText = string.Join("\n", new[] { activity?.GetDetails(), presence?.ClientVersion }
                    .Where(value => !string.IsNullOrWhiteSpace(value)));
            }
            lastClientLabel = clientLabel;
            lastStatus = status;
            lastActivity = activity;
            lastVisit = status != UserStatus.Offline ? DateTimeOffset.Now : lastVisit;
        }

        protected override bool OnHover(HoverEvent e)
        {
            BorderThickness = 2;
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            BorderThickness = 0;
            base.OnHoverLost(e);
        }

        private partial class StatusText : OsuSpriteText, IHasTooltip
        {
            public LocalisableString TooltipText { get; set; }
        }
    }
}
