using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Settings.Sections.Fork
{
    /// <summary>Entry point only; the catalogue lives in its own full-size overlay.</summary>
    public partial class CommunityThemeSection : SettingsSection
    {
        public override LocalisableString Header => ThemeLibraryStrings.Library;
        public override Drawable CreateIcon() => new SpriteIcon { Icon = FontAwesome.Solid.Globe };

        [BackgroundDependencyLoader]
        private void load(OsuGame? game)
        {
            Children = new Drawable[]
            {
                new SettingsButtonV2 { Text = ThemeLibraryStrings.Library, Action = () => game?.ShowThemeLibrary(), Height = 60 },
            };
        }
    }
}
