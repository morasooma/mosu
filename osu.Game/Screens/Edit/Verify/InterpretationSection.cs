// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Beatmaps;
using osu.Game.Overlays.Settings;

namespace osu.Game.Screens.Edit.Verify
{
    internal partial class InterpretationSection : EditorRoundedScreenSettingsSection
    {
        protected override string HeaderText => "Interpretation";

        [BackgroundDependencyLoader]
        private void load(VerifyScreen verify)
        {
            Flow.Add(new SettingsEnumDropdown<DifficultyRating>
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                TooltipText = global::osu.Game.Localisation.ClientTranslationStrings.J4347,
                Current = verify.InterpretedDifficulty.GetBoundCopy()
            });
        }
    }
}
