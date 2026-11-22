// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Overlays.Settings.Sections.Fork;
using osuTK;

namespace osu.Game.Overlays.Settings.Sections
{
    public partial class ForkSection : SettingsSection
    {
        public override LocalisableString Header => global::osu.Game.Localisation.ClientTranslationStrings.J4020;

        protected override bool HeaderUsesThemeColour => false;

        protected override Drawable CreateHeader() => new MorasoomaWordmark
        {
            Anchor = Anchor.TopCentre,
            Origin = Anchor.TopCentre,
            Size = new Vector2(260, 60),
            FillMode = FillMode.Fit,
        };

        public override Drawable CreateIcon() => new SpriteIcon
        {
            Icon = OsuIcon.Settings
        };

        public ForkSection(PerformanceOptimisationSettingsPanel performanceOptimisationSettings, CustomThemeSettingsPanel? customThemeSettings = null, SkinVisibilitySettingsPanel? skinVisibility = null, BackgroundCalculationSettingsPanel? backgroundCalculations = null, AiFunctionSettingsPanel? aiSettings = null)
        {
            FlowContent.Margin = new MarginPadding { Top = 68 };

            Add(new ForkConnectionSettings());
            Add(new ForkSettings());
            Add(new ForkPerformanceSettings(performanceOptimisationSettings, backgroundCalculations));
            Add(new ForkInterfaceSettings(customThemeSettings, skinVisibility));
            Add(new ForkAiSettings(aiSettings));
            Add(new ForkDebugSettings());
            Add(new ForkCommunitySettings());
            Add(new ConfigurationBackupSettings());
            Add(new ServerProfilesSettings());
        }

        public ForkSection()
            : this(new PerformanceOptimisationSettingsPanel(), new CustomThemeSettingsPanel())
        {
        }

        private partial class MorasoomaWordmark : Sprite
        {
            [Resolved(canBeNull: true)]
            private OsuGame? game { get; set; }

            [BackgroundDependencyLoader]
            private void load(TextureStore textures) => Texture = textures.Get(@"Menu/morasooma-wordmark");

            protected override bool OnClick(ClickEvent e)
            {
                game?.OpenUrlExternally(@"https://morasooma.net");
                return true;
            }
        }
    }
}
