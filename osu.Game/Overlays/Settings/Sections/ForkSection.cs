// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Overlays.Settings.Sections.Fork;

namespace osu.Game.Overlays.Settings.Sections
{
    public partial class ForkSection : SettingsSection
    {
        public override LocalisableString Header => @"Mosu";

        public override Drawable CreateIcon() => new SpriteIcon
        {
            Icon = OsuIcon.Settings
        };

        public ForkSection(PerformanceOptimisationSettingsPanel performanceOptimisationSettings)
        {
            Add(new ForkConnectionSettings());
            Add(new ForkSettings());
            Add(new ForkPerformanceSettings(performanceOptimisationSettings));
            Add(new ForkInterfaceSettings());
            Add(new ForkDebugSettings());
            Add(new ForkCommunitySettings());
        }

        public ForkSection()
            : this(new PerformanceOptimisationSettingsPanel())
        {
        }
    }
}
