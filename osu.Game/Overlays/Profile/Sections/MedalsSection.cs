// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Overlays.Profile.Sections.Medals;
using osu.Game.Resources.Localisation.Web;

namespace osu.Game.Overlays.Profile.Sections
{
    public partial class MedalsSection : ProfileSection
    {
        public override LocalisableString Title => UsersStrings.ShowExtraMedalsTitle;

        public override string Identifier => @"medals";

        public MedalsSection()
        {
            Children = new[]
            {
                new MedalsContainer(User),
            };
        }
    }
}
