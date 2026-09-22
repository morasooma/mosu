// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Development;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Rulesets;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays.Settings
{
    public partial class SettingsFooter : FillFlowContainer
    {
        private OsuSpriteText gameNameText = null!;
        private IBindable<Colour4>? themeColour;

        [BackgroundDependencyLoader]
        private void load(OsuGameBase game, RulesetStore rulesets, OverlayColourProvider? colourProvider = null)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Padding = new MarginPadding { Top = 20, Bottom = 30, Left = SettingsPanel.CONTENT_PADDING.Left, Right = SettingsPanel.CONTENT_PADDING.Right };

            FillFlowContainer modes;

            Children = new Drawable[]
            {
                modes = new FillFlowContainer
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Direction = FillDirection.Full,
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Spacing = new Vector2(5),
                    Padding = new MarginPadding { Bottom = 10 },
                },
                gameNameText = new OsuSpriteText
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Text = game.Name,
                    Font = OsuFont.GetFont(size: 18, weight: FontWeight.Bold),
                },
                new BuildDisplay(game.Version)
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                }
            };

            if (colourProvider != null)
            {
                themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
                themeColour.BindValueChanged(_ => gameNameText.Colour = colourProvider.Content1, true);
            }

            foreach (var ruleset in rulesets.AvailableRulesets)
            {
                try
                {
                    var icon = new ConstrainedIconContainer
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Icon = ruleset.CreateInstance().CreateIcon(),
                        Colour = Color4.Gray,
                        Size = new Vector2(20),
                    };

                    modes.Add(icon);
                }
                catch (Exception e)
                {
                    RulesetStore.LogRulesetFailure(ruleset, e);
                }
            }
        }

        private partial class BuildDisplay : OsuAnimatedButton, IHasContextMenu
        {
            private readonly string version;
            private OsuSpriteText versionText = null!;
            private IBindable<Colour4>? themeColour;

            [Resolved]
            private OsuColour colours { get; set; } = null!;

            [Resolved]
            private OsuGame? game { get; set; }

            public BuildDisplay(string version)
            {
                this.version = version;

                Content.RelativeSizeAxes = Axes.Y;
                Content.AutoSizeAxes = AutoSizeAxes = Axes.X;
                Height = 20;
            }

            [BackgroundDependencyLoader]
            private void load(ChangelogOverlay? changelog, OverlayColourProvider? colourProvider = null)
            {
                Action = () => changelog?.ShowBuild(version);

                Add(versionText = new OsuSpriteText
                {
                    Font = OsuFont.GetFont(size: 16),

                    Text = version,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Padding = new MarginPadding(5),
                    Colour = DebugUtils.IsDebugBuild ? colours.Red : Color4.White,
                });

                if (!DebugUtils.IsDebugBuild && colourProvider != null)
                {
                    themeColour = colourProvider.GetColourBindable(OverlayColour.Content2);
                    themeColour.BindValueChanged(_ => versionText.Colour = colourProvider.Content2, true);
                }
            }

            public MenuItem[] ContextMenuItems => new MenuItem[]
            {
                new OsuMenuItem(SettingsStrings.CopyVersion, MenuItemType.Standard, () => game?.CopyToClipboard(version))
            };
        }
    }
}
