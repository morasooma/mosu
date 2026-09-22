// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Diagnostics;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Overlays;
using osuTK;
using osuTK.Graphics;
using WebCommonStrings = osu.Game.Resources.Localisation.Web.CommonStrings;

namespace osu.Game.Screens.Select
{
    public partial class PanelUpdateBeatmapButton : OsuAnimatedButton
    {
        private BeatmapInfo? beatmap;
        private BeatmapSetInfo? beatmapSet;

        public BeatmapInfo? Beatmap
        {
            get => beatmap;
            set
            {
                beatmap = value;

                if (IsLoaded)
                    beatmapChanged();
            }
        }

        public BeatmapSetInfo? BeatmapSet
        {
            get => beatmapSet;
            set
            {
                beatmapSet = value;

                if (IsLoaded)
                    beatmapChanged();
            }
        }

        private SpriteIcon icon = null!;
        private OsuSpriteText text = null!;
        private Box progressFill = null!;
        private ButtonMode mode;

        [Resolved]
        private BeatmapModelDownloader beatmapDownloader { get; set; } = null!;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private LoginOverlay? loginOverlay { get; set; }

        [Resolved]
        private IDialogOverlay? dialogOverlay { get; set; }

        public PanelUpdateBeatmapButton()
        {
            AutoSizeAxes = Axes.X;
            Height = 22f;
            // A hidden button still needs to observe in-place Realm metadata updates so it can
            // become visible when a newer server revision is discovered.
            AlwaysPresent = true;
        }

        private Bindable<bool> preferNoVideo = null!;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            const float icon_size = 12;

            preferNoVideo = config.GetBindable<bool>(OsuSetting.PreferNoVideo);

            Content.Anchor = Anchor.Centre;
            Content.Origin = Anchor.Centre;

            Content.AddRange(new Drawable[]
            {
                progressFill = new Box
                {
                    Colour = Color4.White,
                    Alpha = 0.2f,
                    Blending = BlendingParameters.Additive,
                    RelativeSizeAxes = Axes.Both,
                    Width = 0,
                },
                new FillFlowContainer
                {
                    Padding = new MarginPadding { Horizontal = 5, Vertical = 3 },
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(4),
                    Children = new Drawable[]
                    {
                        new Container
                        {
                            Size = new Vector2(icon_size),
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Children = new Drawable[]
                            {
                                icon = new SpriteIcon
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Icon = FontAwesome.Solid.SyncAlt,
                                    Size = new Vector2(icon_size),
                                },
                            }
                        },
                        text = new OsuSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Font = OsuFont.Style.Body.With(weight: FontWeight.SemiBold),
                            Text = "Update",
                        }
                    }
                },
            });

            Action = performAction;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            beatmapChanged();
        }

        protected override void Update()
        {
            base.Update();

            // Beatmap metadata is updated in-place by Realm after an online lookup. The panel
            // itself does not get rebound in that case, so keep the button mode in sync here.
            if (mode != getMode())
                beatmapChanged();
        }

        private void beatmapChanged()
        {
            mode = getMode();
            Enabled.Value = true;
            progressFill.Width = 0;

            switch (mode)
            {
                case ButtonMode.Update:
                    Alpha = 1;
                    icon.Icon = FontAwesome.Solid.SyncAlt;
                    text.Text = WebCommonStrings.ButtonsUpdate;
                    icon.Spin(4000, RotationDirection.Clockwise);
                    break;

                case ButtonMode.Help:
                    Alpha = 1;
                    icon.ClearTransforms();
                    icon.Rotation = 0;
                    icon.Icon = FontAwesome.Solid.QuestionCircle;
                    text.Text = LeaderboardStrings.BeatmapUploadHelp;
                    break;

                default:
                    Alpha = 0;
                    icon.ClearTransforms();
                    icon.Rotation = 0;
                    text.Text = string.Empty;
                    break;
            }
        }

        protected override bool OnHover(HoverEvent e)
        {
            if (mode == ButtonMode.Update)
                icon.Spin(400, RotationDirection.Clockwise, icon.Rotation);

            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            if (mode == ButtonMode.Update)
                icon.Spin(4000, RotationDirection.Clockwise, icon.Rotation);

            base.OnHoverLost(e);
        }

        public override LocalisableString TooltipText => mode switch
        {
            ButtonMode.Update when Enabled.Value => SongSelectStrings.UpdateBeatmapTooltip,
            ButtonMode.Help => LeaderboardStrings.BeatmapUploadHelpBody,
            _ => string.Empty,
        };

        private bool updateConfirmed;

        private void performAction()
        {
            switch (mode)
            {
                case ButtonMode.Help:
                    dialogOverlay?.Push(new BeatmapUploadHelpDialog());
                    return;

                case ButtonMode.Update:
                    performUpdate();
                    return;
            }
        }

        private void performUpdate()
        {
            Debug.Assert(beatmapSet != null);

            if (!api.IsLoggedIn)
            {
                loginOverlay?.Show();
                return;
            }

            if (dialogOverlay != null && beatmapSet.Status == BeatmapOnlineStatus.LocallyModified && !updateConfirmed)
            {
                dialogOverlay.Push(new UpdateLocalConfirmationDialog(() =>
                {
                    updateConfirmed = true;
                    performUpdate();
                }));

                return;
            }

            updateConfirmed = false;

            beatmapDownloader.DownloadAsUpdate(beatmapSet, preferNoVideo.Value);
            attachExistingDownload();
        }

        private void attachExistingDownload()
        {
            Debug.Assert(beatmapSet != null);
            var download = beatmapDownloader.GetExistingDownload(beatmapSet);

            if (download != null)
            {
                Enabled.Value = false;

                download.DownloadProgressed += progress => progressFill.ResizeWidthTo(progress, 100, Easing.OutQuint);
                download.Failure += _ => attachExistingDownload();
            }
            else
            {
                Enabled.Value = true;

                progressFill.ResizeWidthTo(0, 100, Easing.OutQuint);
            }
        }

        private ButtonMode getMode()
        {
            bool serverExclusive = beatmap?.OnlineID >= BeatmapApiProvider.SERVER_EXCLUSIVE_ID_THRESHOLD
                                   || beatmapSet?.OnlineID >= BeatmapApiProvider.SERVER_EXCLUSIVE_ID_THRESHOLD
                                   || beatmap?.Metadata.IsServerExclusive() == true
                                   || beatmapSet.IsServerExclusive();

            if (serverExclusive)
                return beatmapSet?.OnlineID > 0 && beatmapSet.AllBeatmapsUpToDate == false
                    ? ButtonMode.Update
                    : ButtonMode.Hidden;

            if (beatmap?.OnlineID <= 0)
                return ButtonMode.Help;

            if (beatmapSet?.OnlineID <= 0)
                return ButtonMode.Help;

            return beatmapSet?.AllBeatmapsUpToDate == false ? ButtonMode.Update : ButtonMode.Hidden;
        }

        private enum ButtonMode
        {
            Hidden,
            Update,
            Help,
        }
    }
}
